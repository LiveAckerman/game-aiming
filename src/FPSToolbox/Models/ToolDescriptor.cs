namespace FPSToolbox.Models;

/// <summary>
/// Toolbox 自身的设置（%AppData%\FPSToolbox\toolbox.json）。
/// </summary>
public class ToolboxSettings
{
    public bool AutoStart { get; set; } = false;
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public string Theme { get; set; } = "dark";
    public Dictionary<string, string?> GlobalHotkeys { get; set; } = new();
    public List<string> LastUsedTools { get; set; } = new();
}

/// <summary>
/// 已安装子工具清单（%AppData%\FPSToolbox\installed-tools.json）。
/// </summary>
public class InstalledToolsManifest
{
    public List<InstalledTool> Tools { get; set; } = new();
}

public class InstalledTool
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public string ExePath { get; set; } = "";
    public DateTime InstalledAt { get; set; } = DateTime.Now;
}

/// <summary>
/// UI 层用于卡片显示的工具元数据。
/// </summary>
public class ToolDescriptor
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string IconResource { get; set; } = "";
    public string DefaultExeRelativePath { get; set; } = "";

    /// <summary>
    /// 子工具 zip 包的下载地址（直链）。用户未勾选安装该工具时，点击"下载并安装"按钮会从此 URL 下载。
    /// 支持 http/https 直链，也支持 file:// 本地路径（开发测试用）。
    /// 空字符串 = 禁用下载，按钮会提示"未配置下载链接"。
    /// </summary>
    public string DownloadUrl { get; set; } = "";
}
