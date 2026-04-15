using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CrosshairTool.TrayIcon;

public class TrayIconManager : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private bool _disposed;

    public void Initialize(Action onToggle, Action onSettings, Action onExit)
    {
        _notifyIcon = new NotifyIcon
        {
            Text = "CrosshairTool - 屏幕准心",
            Visible = true,
            ContextMenuStrip = BuildContextMenu(onToggle, onSettings, onExit)
        };

        // 尝试加载图标，失败则使用系统默认图标
        try
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "icon.ico");
            if (File.Exists(iconPath))
                _notifyIcon.Icon = new Icon(iconPath);
            else
                _notifyIcon.Icon = SystemIcons.Application;
        }
        catch
        {
            _notifyIcon.Icon = SystemIcons.Application;
        }

        _notifyIcon.DoubleClick += (_, _) => onSettings();
    }

    public void UpdateToggleText(bool isVisible)
    {
        if (_notifyIcon?.ContextMenuStrip?.Items.Count > 0)
        {
            _notifyIcon.ContextMenuStrip.Items[0].Text =
                isVisible ? "隐藏准心 (F8)" : "显示准心 (F8)";
        }
    }

    private static ContextMenuStrip BuildContextMenu(Action toggle, Action settings, Action exit)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("隐藏准心 (F8)", null, (_, _) => toggle());
        menu.Items.Add("设置...", null, (_, _) => settings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => exit());
        return menu;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifyIcon?.Dispose();
    }
}
