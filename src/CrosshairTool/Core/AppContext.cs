using CrosshairTool.Models;

namespace CrosshairTool.Core;

/// <summary>
/// 全局状态单例，集中管理应用状态
/// </summary>
public class AppContext
{
    private static AppContext? _instance;
    public static AppContext Instance => _instance ??= new AppContext();

    private AppContext() { }

    public CrosshairConfig Config { get; set; } = new CrosshairConfig();
    public bool IsOverlayVisible { get; set; } = true;

    /// <summary>
    /// 配置变更时触发，OverlayWindow 监听此事件刷新渲染
    /// </summary>
    public event EventHandler? ConfigChanged;

    /// <summary>
    /// 准心显示状态变更时触发
    /// </summary>
    public event EventHandler? VisibilityChanged;

    public void NotifyConfigChanged()
    {
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleVisibility()
    {
        IsOverlayVisible = !IsOverlayVisible;
        VisibilityChanged?.Invoke(this, EventArgs.Empty);
    }
}
