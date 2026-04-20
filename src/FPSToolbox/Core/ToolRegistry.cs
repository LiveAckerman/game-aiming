using System.IO;
using FPSToolbox.Models;
using FPSToolbox.Shared;
using FPSToolbox.Shared.Config;
using FPSToolbox.Shared.Ipc;

namespace FPSToolbox.Core;

/// <summary>
/// 已安装子工具的元数据注册表。读写 <c>%AppData%\FPSToolbox\installed-tools.json</c>。
/// </summary>
public class ToolRegistry
{
    // ──────────────────────────────────────────────────────────────
    // 子工具下载策略:
    //   主框架启动时会调 GitHub Releases API 拿最新 tag 的 zip 直链,所以
    //   正常情况下这里的 DownloadUrl 留空即可。
    //
    //   这里的 URL 只作为"GitHub API 不可达时的最后兜底"(比如用户在内网),
    //   填的是当前 release.ps1 已经发出去的版本 tag 下的 zip 直链。
    //   升级时在发版脚本里 bump 版本号即可,这里不用改。
    // ──────────────────────────────────────────────────────────────
    private const string FallbackVersion = "1.0.0";
    private static string ToolZipUrl(string prefix, string toolName) =>
        $"https://github.com/{UpdateConstants.GitHubOwner}/{UpdateConstants.GitHubRepo}/releases/download/{prefix}v{FallbackVersion}/{toolName}-v{FallbackVersion}.zip";

    public static IReadOnlyList<ToolDescriptor> AllDescriptors { get; } = new[]
    {
        new ToolDescriptor
        {
            Name = ToolIds.CrosshairTool,
            DisplayName = "屏幕准心工具",
            Description = "全屏置顶的自定义准心，点击穿透不影响游戏操作。支持多种样式、颜色、轮廓、热键切换。",
            DefaultExeRelativePath = @"tools\CrosshairTool\CrosshairTool.exe",
            DownloadUrl = ToolZipUrl(UpdateConstants.TagPrefix.Crosshair, "CrosshairTool"),
        },
        new ToolDescriptor
        {
            Name = ToolIds.GammaTool,
            DisplayName = "屏幕调节工具",
            Description = "复刻 Gamma Panel 的屏幕灰度 / 亮度 / 对比度调节工具，支持 RGB 通道独立、LUT 曲线预览和配色方案。",
            DefaultExeRelativePath = @"tools\GammaTool\GammaTool.exe",
            DownloadUrl = ToolZipUrl(UpdateConstants.TagPrefix.Gamma, "GammaTool"),
        },
    };

    public static ToolDescriptor? GetDescriptor(string name) =>
        AllDescriptors.FirstOrDefault(d => d.Name == name);

    private InstalledToolsManifest _manifest = new();

    public void Load() =>
        _manifest = JsonConfigStore.Load<InstalledToolsManifest>(PathService.InstalledToolsManifestPath);

    public void Save() =>
        JsonConfigStore.Save(PathService.InstalledToolsManifestPath, _manifest);

    public InstalledTool? Get(string name) =>
        _manifest.Tools.FirstOrDefault(t => t.Name == name);

    public bool IsInstalled(string name)
    {
        var t = Get(name);
        return t != null && File.Exists(t.ExePath);
    }

    public void Upsert(InstalledTool tool)
    {
        _manifest.Tools.RemoveAll(t => t.Name == tool.Name);
        _manifest.Tools.Add(tool);
        Save();
    }

    public void Remove(string name)
    {
        _manifest.Tools.RemoveAll(t => t.Name == name);
        Save();
    }

    /// <summary>
    /// 自检：扫描 tools\ 目录，发现 exe 即视为已安装（兼容安装器直接落盘未更新 manifest 的情况）。
    /// </summary>
    public void AutoDiscover(string baseDir)
    {
        foreach (var desc in AllDescriptors)
        {
            var path = Path.Combine(baseDir, desc.DefaultExeRelativePath);
            if (File.Exists(path) && Get(desc.Name) == null)
            {
                _manifest.Tools.Add(new InstalledTool
                {
                    Name = desc.Name,
                    Version = "1.0.0",
                    ExePath = path,
                    InstalledAt = DateTime.Now
                });
            }
        }
        Save();
    }
}
