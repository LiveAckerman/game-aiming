using System.IO;
using System.Threading;
using System.Windows;
using FPSToolbox.Shared;
using FPSToolbox.Shared.Ipc;
using GammaTool.Core;
using GammaTool.Windows;

namespace GammaTool;

public partial class App : System.Windows.Application
{
    private static readonly string LogPath = PathService.GetLogFile(ToolIds.GammaTool);

    private Mutex? _mutex;
    private IpcClient? _ipc;
    private ParentWatcher? _parentWatcher;
    private GammaApplier? _applier;
    private SchemeManager? _schemes;
    private GammaPanelWindow? _panel;

    protected override async void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => WriteLog(ex.ExceptionObject as Exception);
        DispatcherUnhandledException += (_, ex) => { WriteLog(ex.Exception); ex.Handled = true; };

        try
        {
            var args = StartupArgs.Parse(e.Args);
            if (!args.IsValid)
            {
                MessageBox.Show(
                    "屏幕调节工具必须通过 FPS 工具箱启动。\n\n请打开 FPS 工具箱主程序，从卡片界面启动本工具。",
                    "无法独立运行", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown();
                return;
            }

            _mutex = new Mutex(true, @"Global\FPSToolbox_GammaTool_SingleInstance", out bool createdNew);
            if (!createdNew)
            {
                _mutex = null;
                MessageBox.Show("屏幕调节工具已经在运行中。", "FPS 工具箱",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            base.OnStartup(e);

            _applier = new GammaApplier();
            _applier.Initialize();
            _schemes = new SchemeManager();

            _ipc = new IpcClient(args.PipeName!);
            _ipc.OnRequest = HandleIpcRequest;
            _ipc.OnDisconnected = () => Dispatcher.Invoke(() => Shutdown());

            _panel = new GammaPanelWindow(_applier, _schemes, _ipc);
            _panel.Show();

            try
            {
                await _ipc.StartAsync();
                await _ipc.SendEventAsync(IpcTopics.ToolReady, new
                {
                    tool = ToolIds.GammaTool,
                    version = "1.0.0",
                    pid = Environment.ProcessId
                });
            }
            catch (Exception ex)
            {
                WriteLog(ex);
                MessageBox.Show("无法连接 FPS 工具箱。", "FPS 工具箱",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            _parentWatcher = new ParentWatcher(args.ParentPid!.Value,
                () => Dispatcher.Invoke(() => Shutdown()));
            _parentWatcher.Start();
        }
        catch (Exception ex)
        {
            WriteLog(ex);
            MessageBox.Show($"屏幕调节工具启动失败：\n\n{ex.Message}\n\n日志：{LogPath}",
                "FPS 工具箱", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private async Task HandleIpcRequest(IpcMessage req)
    {
        try
        {
            switch (req.Action)
            {
                case IpcActions.Ping:
                    await _ipc!.SendResponseAsync(req.Id, true, new { pong = true });
                    break;

                case IpcActions.GammaOpenPanel:
                    Dispatcher.Invoke(() =>
                    {
                        if (_panel == null) return;
                        if (!_panel.IsVisible) _panel.Show();
                        if (_panel.WindowState == WindowState.Minimized)
                            _panel.WindowState = WindowState.Normal;
                        _panel.Activate();
                    });
                    await _ipc!.SendResponseAsync(req.Id, true);
                    break;

                case IpcActions.GammaResetSystem:
                    Dispatcher.Invoke(() => _applier?.RestoreSystemDefaults());
                    await _ipc!.SendResponseAsync(req.Id, true);
                    break;

                case IpcActions.GammaListSchemes:
                {
                    var list = _schemes?.LoadAllSchemes()
                        .Select(s => new { s.Name, s.Hotkey }).ToList() ?? new();
                    await _ipc!.SendResponseAsync(req.Id, true, list);
                    break;
                }

                case IpcActions.Shutdown:
                    await _ipc!.SendResponseAsync(req.Id, true);
                    Dispatcher.Invoke(() => Shutdown());
                    break;

                default:
                    await _ipc!.SendResponseAsync(req.Id, false, error: $"unknown action: {req.Action}");
                    break;
            }
        }
        catch (Exception ex)
        {
            WriteLog(ex);
            try { await _ipc!.SendResponseAsync(req.Id, false, error: ex.Message); } catch { }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _applier?.RestoreSystemDefaults();
            _applier?.Dispose();
            _parentWatcher?.Dispose();
            _ = _ipc?.SendEventAsync(IpcTopics.ToolExiting,
                new { tool = ToolIds.GammaTool, reason = "exit" });
        }
        catch { }
        finally
        {
            if (_mutex != null)
            {
                try { _mutex.ReleaseMutex(); } catch { }
                _mutex = null;
            }
        }
        base.OnExit(e);
    }

    private static void WriteLog(Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{ex}\n\n");
        }
        catch { }
    }
}
