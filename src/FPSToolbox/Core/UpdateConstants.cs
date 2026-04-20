namespace FPSToolbox.Core;

/// <summary>
/// GitHub Release 相关常量。集中在一处,方便以后迁仓库。
/// </summary>
public static class UpdateConstants
{
    // ──────────────────────────────────────────────────────────────
    // ▼▼▼ 仓库配置(迁仓库只改这两行) ▼▼▼
    public const string GitHubOwner = "LiveAckerman";
    public const string GitHubRepo = "game-aiming";
    // ▲▲▲

    public const string ApiBase = "https://api.github.com";
    public const string ReleasesListEndpoint =
        $"{ApiBase}/repos/{GitHubOwner}/{GitHubRepo}/releases?per_page=30";

    public const string ReleasesWebUrl =
        $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases";

    /// <summary>
    /// 各组件的 tag 前缀。发版时 tag 命名:
    ///   toolbox-v1.0.0 / crosshair-v1.0.0 / gamma-v1.0.0
    /// </summary>
    public static class TagPrefix
    {
        public const string Toolbox = "toolbox-v";
        public const string Crosshair = "crosshair-v";
        public const string Gamma = "gamma-v";
    }

    /// <summary>
    /// 组件 id(与 <see cref="FPSToolbox.Shared.Ipc.ToolIds"/> 里的子工具名一致,方便 UI 映射)
    /// </summary>
    public static class ComponentId
    {
        public const string Toolbox = "Toolbox";
        public const string CrosshairTool = "CrosshairTool";
        public const string GammaTool = "GammaTool";
    }

    /// <summary>
    /// Asset 文件名匹配规则(防止误匹配其它 zip/exe)
    /// </summary>
    public static class AssetNamePattern
    {
        public const string ToolboxOnlineExe = "FPSToolbox_Setup_v*.exe";       // 在线版(小)
        public const string ToolboxOfflineExe = "FPSToolbox_Setup_v*_offline.exe"; // 离线版(~60MB)
        public const string CrosshairZip = "CrosshairTool-v*.zip";
        public const string GammaZip = "GammaTool-v*.zip";
    }
}
