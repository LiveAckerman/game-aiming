namespace FPSToolbox.Models;

/// <summary>
/// 针对某个组件(Toolbox / CrosshairTool / GammaTool)的一次更新检查结果。
/// </summary>
public class ComponentUpdateInfo
{
    public string ComponentId { get; set; } = "";
    public string DisplayName { get; set; } = "";

    /// <summary>本地当前版本(已安装/运行中的),形如 "1.0.0"。未安装 = null。</summary>
    public string? CurrentVersion { get; set; }

    /// <summary>GitHub 上找到的最新版本(无前缀),形如 "1.2.0"。查不到 = null。</summary>
    public string? LatestVersion { get; set; }

    /// <summary>主框架专用:在线版 installer 直链</summary>
    public string? ToolboxOnlineInstallerUrl { get; set; }

    /// <summary>主框架专用:离线版 installer 直链</summary>
    public string? ToolboxOfflineInstallerUrl { get; set; }

    /// <summary>子工具专用:zip 直链</summary>
    public string? ToolZipUrl { get; set; }

    public string? ReleaseNotes { get; set; }
    public DateTime? PublishedAt { get; set; }

    /// <summary>远端版本 > 本地版本 = true;没装 / 查不到 = false</summary>
    public bool HasUpdate { get; set; }
}

/// <summary>
/// 整体检查结果(三个组件之和)
/// </summary>
public class UpdateCheckResult
{
    public ComponentUpdateInfo Toolbox { get; set; } = new();
    public ComponentUpdateInfo Crosshair { get; set; } = new();
    public ComponentUpdateInfo Gamma { get; set; } = new();
    public DateTime CheckedAt { get; set; } = DateTime.Now;
    public string? ErrorMessage { get; set; }
}
