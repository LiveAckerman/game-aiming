using System.IO;
using System.Threading;
using System.Windows.Input;
using CrosshairTool.Core;
using CrosshairTool.Windows;
using FPSToolbox.Shared;
using FPSToolbox.Shared.Hotkeys;
using FPSToolbox.Shared.Ipc;
using AppCtx = CrosshairTool.Core.AppContext;
using Application = System.Windows.Application;
using ExitEventArgs = System.Windows.ExitEventArgs;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using StartupEventArgs = System.Windows.StartupEventArgs;

namespace CrosshairTool;

public partial class App : Application
{
    private static readonly string LogPath = PathService.GetLogFile(ToolIds.CrosshairTool);

    private static Mutex? _mutex;
    private OverlayWindow? _overlayWindow;
    private SettingsWindow? _settingsWindow;
    private HotkeyManager? _hotkeyManager;
    private ConfigManager? _configManager;
    private int _hotkeyId = -1;

    private IpcClient? _ipc;
    private ParentWatcher? _parentWatcher;

    protected override async void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        try
        {
            var startup = StartupArgs.Parse(e.Args);
            if (!startup.IsValid)
            {
                MessageBox.Show(
                    "屏幕准心工具必须通过 FPS 工具箱启动。\n\n请打开 FPS 工具箱主程序，从卡片界面启动本工具。",
                    "无法独立运行", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown();
                return;
            }

            _mutex = new Mutex(true, @"Global\FPSToolbox_CrosshairTool_SingleInstance", out bool createdNew);
            if (!createdNew)
            {
                _mutex = null;
                MessageBox.Show("屏幕准心工具已经在运行中。", "FPS 工具箱",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            base.OnStartup(e);

            _configManager = new ConfigManager();
            AppCtx.Instance.Config = _configManager.Load();
            AppCtx.Instance.IsOverlayVisible = true;

            _overlayWindow = new OverlayWindow();
            _overlayWindow.Show();

            _hotkeyManager = new HotkeyManager();
            _hotkeyManager.Initialize(_overlayWindow);
            RegisterToggleHotkey();

            AppCtx.Instance.ConfigChanged += OnConfigChanged;
            AppCtx.Instance.VisibilityChanged += OnVisibilityChanged;

            // 连接 Toolbox 的 IPC
            _ipc = new IpcClient(startup.PipeName!);
            _ipc.OnRequest = HandleIpcRequest;
            _ipc.OnDisconnected = () => Dispatcher.Invoke(ExitApp);

            try
            {
                await _ipc.StartAsync();
                await _ipc.SendEventAsync(IpcTopics.ToolReady, new
                {
                    tool = ToolIds.CrosshairTool,
                    version = "1.0.0",
                    pid = Environment.ProcessId
                });
            }
            catch (Exception ex)
            {
                WriteLog(ex);
                MessageBox.Show("无法连接 FPS 工具箱。\n请通过工具箱重新启动此工具。",
                    "FPS 工具箱", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            // 父进程监控
            _parentWatcher = new ParentWatcher(startup.ParentPid!.Value,
                () => Dispatcher.Invoke(ExitApp));
            _parentWatcher.Start();
        }
        catch (Exception ex)
        {
            WriteLog(ex);
            MessageBox.Show(
                $"屏幕准心工具启动失败：\n\n{ex.Message}\n\n详细日志：{LogPath}",
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

                case IpcActions.CrosshairShow:
                    Dispatcher.Invoke(() =>
                    {
                        if (!AppCtx.Instance.IsOverlayVisible) AppCtx.Instance.ToggleVisibility();
                    });
                    await _ipc!.SendResponseAsync(req.Id, true);
                    break;

                case IpcActions.CrosshairHide:
                    Dispatcher.Invoke(() =>
                    {
                        if (AppCtx.Instance.IsOverlayVisible) AppCtx.Instance.ToggleVisibility();
                    });
                    await _ipc!.SendResponseAsync(req.Id, true);
                    break;

                case IpcActions.CrosshairToggle:
                    Dispatcher.Invoke(() => AppCtx.Instance.ToggleVisibility());
                    await _ipc!.SendResponseAsync(req.Id, true);
                    break;

                case IpcActions.CrosshairOpenSettings:
                    Dispatcher.Invoke(OpenSettings);
                    await _ipc!.SendResponseAsync(req.Id, true);
                    break;

                case IpcActions.CrosshairReloadConfig:
                    Dispatcher.Invoke(() =>
                    {
                        AppCtx.Instance.Config = _configManager!.Load();
                        AppCtx.Instance.NotifyConfigChanged();
                    });
                    await _ipc!.SendResponseAsync(req.Id, true);
                    break;

                case IpcActions.CrosshairApplyPreset:
                {
                    var data = req.GetPayload<ApplyPresetPayload>();
                    if (data != null && !string.IsNullOrEmpty(data.Name))
                    {
                        var preset = _configManager!.LoadPreset(data.Name);
                        if (preset != null)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                AppCtx.Instance.Config = preset;
                                AppCtx.Instance.NotifyConfigChanged();
                                _configManager.Save(preset);
                            });
                            await _ipc!.SendResponseAsync(req.Id, true);
                            await _ipc.SendEventAsync(IpcTopics.CrosshairConfigChanged,
                                new { presetName = data.Name });
                            break;
                        }
                    }
                    await _ipc!.SendResponseAsync(req.Id, false, error: "preset not found");
                    break;
                }

                case IpcActions.Shutdown:
                    await _ipc!.SendResponseAsync(req.Id, true);
                    Dispatcher.Invoke(ExitApp);
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

    private sealed class ApplyPresetPayload { public string? Name { get; set; } }

    private void RegisterToggleHotkey()
    {
        if (_hotkeyManager == null) return;
        if (_hotkeyId >= 0) _hotkeyManager.Unregister(_hotkeyId);

        var hotkeyStr = AppCtx.Instance.Config.ToggleHotkey;
        if (HotkeyManager.TryParseHotkey(hotkeyStr, out uint modifiers, out uint vk))
            _hotkeyId = _hotkeyManager.Register(modifiers, vk, ToggleOverlay);
    }

    private void OnConfigChanged(object? sender, EventArgs e) => RegisterToggleHotkey();

    private void OnVisibilityChanged(object? sender, EventArgs e)
    {
        _ = _ipc?.SendEventAsync(IpcTopics.CrosshairVisibility,
            new { visible = AppCtx.Instance.IsOverlayVisible });
    }

    private void ToggleOverlay() => Dispatcher.Invoke(() => AppCtx.Instance.ToggleVisibility());

    private void OpenSettings()
    {
        if (_settingsWindow != null && _settingsWindow.IsVisible)
        {
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = new SettingsWindow(_configManager!);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private void ExitApp()
    {
        try
        {
            _configManager?.Save(AppCtx.Instance.Config);
            _hotkeyManager?.Dispose();
            _overlayWindow?.Close();
            _settingsWindow?.Close();
            _parentWatcher?.Dispose();
            _ = _ipc?.SendEventAsync(IpcTopics.ToolExiting,
                new { tool = ToolIds.CrosshairTool, reason = "user-or-ipc" });
        }
        finally
        {
            ReleaseMutexSafe();
            Shutdown();
        }
    }

    private void ReleaseMutexSafe()
    {
        if (_mutex == null) return;
        try { _mutex.ReleaseMutex(); }
        catch (SynchronizationLockException) { }
        finally { _mutex = null; }
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        WriteLog(e.ExceptionObject as Exception);
    }

    private void OnDispatcherUnhandledException(object sender,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        WriteLog(e.Exception);
        e.Handled = true;
    }

    private static void WriteLog(Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{ex}\n\n");
        }
        catch { }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ReleaseMutexSafe();
        base.OnExit(e);
    }
}
