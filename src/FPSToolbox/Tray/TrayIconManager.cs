using System.Drawing;
using System.IO;
using System.Windows.Forms;
using FPSToolbox.Core;
using FPSToolbox.Models;
using FPSToolbox.Shared.Ipc;

namespace FPSToolbox.Tray;

/// <summary>
/// Toolbox 的系统托盘图标（唯一）。菜单按工具分组，通过 IPC 控制子工具。
/// </summary>
public class TrayIconManager : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private readonly ToolManager _toolManager;
    private readonly ToolRegistry _registry;
    private readonly Action _onShowMain;
    private readonly Action _onExitAll;
    private bool _disposed;

    private ToolStripMenuItem? _crosshairRoot;
    private ToolStripMenuItem? _crosshairToggle;
    private ToolStripMenuItem? _gammaRoot;

    public TrayIconManager(ToolManager tm, ToolRegistry registry,
        Action onShowMain, Action onExitAll)
    {
        _toolManager = tm;
        _registry = registry;
        _onShowMain = onShowMain;
        _onExitAll = onExitAll;
    }

    public void Initialize()
    {
        _notifyIcon = new NotifyIcon
        {
            Text = "FPS 工具箱",
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };

        try
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "icon.ico");
            _notifyIcon.Icon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;
        }
        catch { _notifyIcon.Icon = SystemIcons.Application; }

        _notifyIcon.DoubleClick += (_, _) => _onShowMain();
        _toolManager.StateChanged += _ => RefreshMenu();
        _toolManager.OnToolEvent += (_, msg) =>
        {
            if (msg.Topic == IpcTopics.CrosshairVisibility && _crosshairToggle != null)
            {
                var payload = msg.GetPayload<VisibilityPayload>();
                _crosshairToggle.Checked = payload?.Visible ?? false;
            }
        };
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        // ── 屏幕准心工具 ──
        _crosshairRoot = new ToolStripMenuItem("屏幕准心工具");
        _crosshairToggle = new ToolStripMenuItem("显示 / 隐藏", null,
            (_, _) => SendAsync(ToolIds.CrosshairTool, IpcActions.CrosshairToggle))
        { CheckOnClick = false };
        _crosshairRoot.DropDownItems.Add(_crosshairToggle);
        _crosshairRoot.DropDownItems.Add("打开设置...", null,
            (_, _) => SendAsync(ToolIds.CrosshairTool, IpcActions.CrosshairOpenSettings));
        _crosshairRoot.DropDownItems.Add(new ToolStripSeparator());
        _crosshairRoot.DropDownItems.Add("启动", null,
            (_, _) => _toolManager.Start(ToolIds.CrosshairTool));
        _crosshairRoot.DropDownItems.Add("停止", null,
            async (_, _) => await _toolManager.StopAsync(ToolIds.CrosshairTool));
        menu.Items.Add(_crosshairRoot);

        // ── 屏幕调节工具 ──
        _gammaRoot = new ToolStripMenuItem("屏幕调节工具");
        _gammaRoot.DropDownItems.Add("打开面板...", null,
            (_, _) => SendAsync(ToolIds.GammaTool, IpcActions.GammaOpenPanel));
        _gammaRoot.DropDownItems.Add("重置为系统默认", null,
            (_, _) => SendAsync(ToolIds.GammaTool, IpcActions.GammaResetSystem));
        _gammaRoot.DropDownItems.Add(new ToolStripSeparator());
        _gammaRoot.DropDownItems.Add("启动", null,
            (_, _) => _toolManager.Start(ToolIds.GammaTool));
        _gammaRoot.DropDownItems.Add("停止", null,
            async (_, _) => await _toolManager.StopAsync(ToolIds.GammaTool));
        menu.Items.Add(_gammaRoot);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("打开主界面", null, (_, _) => _onShowMain());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("完全退出（关闭所有工具）", null, (_, _) => _onExitAll());

        return menu;
    }

    private async void SendAsync(string toolName, string action)
    {
        var info = _toolManager.Get(toolName);
        if (info.Session == null)
        {
            if (info.State is ToolRuntimeState.Installed)
                _toolManager.Start(toolName);
            return;
        }
        try { await info.Session.SendRequestAsync(action, timeout: TimeSpan.FromSeconds(2)); }
        catch { }
    }

    public void RefreshMenu()
    {
        if (_crosshairRoot == null || _gammaRoot == null) return;

        var cross = _toolManager.Get(ToolIds.CrosshairTool);
        var gamma = _toolManager.Get(ToolIds.GammaTool);

        _crosshairRoot.Enabled = cross.State != ToolRuntimeState.NotInstalled;
        _gammaRoot.Enabled = gamma.State != ToolRuntimeState.NotInstalled;

        _crosshairRoot.Text = $"屏幕准心工具  [{StateToText(cross.State)}]";
        _gammaRoot.Text = $"屏幕调节工具  [{StateToText(gamma.State)}]";
    }

    private static string StateToText(ToolRuntimeState s) => s switch
    {
        ToolRuntimeState.NotInstalled => "未安装",
        ToolRuntimeState.Installed => "已安装",
        ToolRuntimeState.Starting => "启动中",
        ToolRuntimeState.Running => "运行中",
        ToolRuntimeState.Disconnected => "失联",
        _ => ""
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }

    private sealed class VisibilityPayload { public bool Visible { get; set; } }
}
