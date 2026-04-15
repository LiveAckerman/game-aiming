using System.IO;
using System.Threading;
using System.Windows.Input;
using CrosshairTool.Core;
using CrosshairTool.TrayIcon;
using CrosshairTool.Windows;
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
    private static readonly string LogPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CrosshairTool", "startup.log");

    private static Mutex? _mutex;
    private OverlayWindow? _overlayWindow;
    private SettingsWindow? _settingsWindow;
    private TrayIconManager? _trayIconManager;
    private HotkeyManager? _hotkeyManager;
    private ConfigManager? _configManager;
    private int _hotkeyId = -1;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 全局异常捕获，防止启动时静默崩溃
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        try
        {
            // 防止重复启动
            _mutex = new Mutex(true, "CrosshairTool_SingleInstance", out bool createdNew);
            if (!createdNew)
            {
                // 未拿到锁，将 _mutex 置 null 避免 OnExit 中错误释放
                _mutex = null;
                MessageBox.Show("CrosshairTool 已经在运行中。\n请在系统托盘查找图标。",
                    "CrosshairTool", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            base.OnStartup(e);

            // 初始化配置
            _configManager = new ConfigManager();
            AppCtx.Instance.Config = _configManager.Load();
            AppCtx.Instance.IsOverlayVisible = true;

            // 创建覆盖层窗口
            _overlayWindow = new OverlayWindow();
            _overlayWindow.Show();

            // 初始化系统托盘
            _trayIconManager = new TrayIconManager();
            _trayIconManager.Initialize(
                onToggle: ToggleOverlay,
                onSettings: OpenSettings,
                onExit: ExitApp
            );

            // 初始化热键（必须在窗口显示后）
            _hotkeyManager = new HotkeyManager();
            _hotkeyManager.Initialize(_overlayWindow);
            RegisterToggleHotkey();

            // 监听配置变更（热键可能被用户修改）
            AppCtx.Instance.ConfigChanged += OnConfigChanged;
            AppCtx.Instance.VisibilityChanged += OnVisibilityChanged;
        }
        catch (Exception ex)
        {
            WriteLog(ex);
            MessageBox.Show(
                $"CrosshairTool 启动失败：\n\n{ex.Message}\n\n详细日志：{LogPath}",
                "CrosshairTool 错误", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        WriteLog(ex);
        MessageBox.Show(
            $"CrosshairTool 发生未处理异常：\n\n{ex?.Message}\n\n详细日志：{LogPath}",
            "CrosshairTool 错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void OnDispatcherUnhandledException(object sender,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        WriteLog(e.Exception);
        MessageBox.Show(
            $"CrosshairTool 发生界面异常：\n\n{e.Exception.Message}\n\n详细日志：{LogPath}",
            "CrosshairTool 错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
        catch { /* 日志写失败时不再抛出 */ }
    }

    private void RegisterToggleHotkey()
    {
        if (_hotkeyManager == null) return;

        // 先注销旧热键
        if (_hotkeyId >= 0)
            _hotkeyManager.Unregister(_hotkeyId);

        var hotkeyStr = AppCtx.Instance.Config.ToggleHotkey;
        if (TryParseHotkey(hotkeyStr, out uint modifiers, out uint vk))
        {
            _hotkeyId = _hotkeyManager.Register(modifiers, vk, ToggleOverlay);
        }
    }

    private void OnConfigChanged(object? sender, EventArgs e)
    {
        // 如果热键配置变化，重新注册
        RegisterToggleHotkey();
    }

    private void OnVisibilityChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _trayIconManager?.UpdateToggleText(AppCtx.Instance.IsOverlayVisible);
        });
    }

    private void ToggleOverlay()
    {
        Dispatcher.Invoke(() =>
        {
            AppCtx.Instance.ToggleVisibility();
        });
    }

    private void OpenSettings()
    {
        Dispatcher.Invoke(() =>
        {
            if (_settingsWindow != null && _settingsWindow.IsVisible)
            {
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow = new SettingsWindow(_configManager!);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        });
    }

    private void ExitApp()
    {
        Dispatcher.Invoke(() =>
        {
            // 保存配置
            _configManager?.Save(AppCtx.Instance.Config);

            // 清理资源
            _hotkeyManager?.Dispose();
            _trayIconManager?.Dispose();

            _overlayWindow?.Close();
            _settingsWindow?.Close();

            ReleaseMutexSafe();
            Shutdown();
        });
    }

    private void ReleaseMutexSafe()
    {
        if (_mutex == null) return;
        try { _mutex.ReleaseMutex(); }
        catch (SynchronizationLockException) { /* 已被其他路径释放，忽略 */ }
        finally { _mutex = null; }
    }

    /// <summary>
    /// 解析热键字符串（如 "F8"、"Ctrl+Shift+F8"）为 Win32 modifiers 和 VK
    /// </summary>
    private static bool TryParseHotkey(string hotkey, out uint modifiers, out uint vk)
    {
        modifiers = NativeMethods.MOD_NONE;
        vk = 0;

        if (string.IsNullOrEmpty(hotkey)) return false;

        var parts = hotkey.Split('+');
        var keyPart = parts[^1].Trim();

        foreach (var part in parts[..^1])
        {
            switch (part.Trim().ToLower())
            {
                case "ctrl": modifiers |= NativeMethods.MOD_CONTROL; break;
                case "alt": modifiers |= NativeMethods.MOD_ALT; break;
                case "shift": modifiers |= NativeMethods.MOD_SHIFT; break;
                case "win": modifiers |= NativeMethods.MOD_WIN; break;
            }
        }

        if (Enum.TryParse<Key>(keyPart, true, out var key))
        {
            vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            return vk != 0;
        }

        return false;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ReleaseMutexSafe();
        base.OnExit(e);
    }
}
