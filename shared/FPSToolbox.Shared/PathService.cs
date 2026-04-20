using System.IO;

namespace FPSToolbox.Shared;

/// <summary>
/// 统一的数据 / 日志路径服务。所有子工具和 Toolbox 主程序都通过这里取路径。
/// 根目录：%AppData%\FPSToolbox\
/// </summary>
public static class PathService
{
    public const string ProductFolder = "FPSToolbox";

    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        ProductFolder);

    public static string ToolboxSettingsPath => Path.Combine(Root, "toolbox.json");

    public static string InstalledToolsManifestPath => Path.Combine(Root, "installed-tools.json");

    public static string LogsDir => EnsureDir(Path.Combine(Root, "logs"));

    public static string GetToolDir(string toolName) =>
        EnsureDir(Path.Combine(Root, toolName));

    public static string GetToolConfigPath(string toolName) =>
        Path.Combine(GetToolDir(toolName), "config.json");

    public static string GetToolPresetsDir(string toolName) =>
        EnsureDir(Path.Combine(GetToolDir(toolName), "presets"));

    public static string GetLogFile(string toolName) =>
        Path.Combine(LogsDir, $"{toolName}-{DateTime.Now:yyyyMMdd}.log");

    private static string EnsureDir(string path)
    {
        try { Directory.CreateDirectory(path); } catch { /* ignore */ }
        return path;
    }
}
