using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FPSToolbox.Models;
using FPSToolbox.Shared.Ipc;

namespace FPSToolbox.Core;

/// <summary>
/// 通过 GitHub Releases API 检查主框架和子工具的最新版本。
/// Tag 命名约定:toolbox-v1.0.0 / crosshair-v1.0.0 / gamma-v1.0.0
/// </summary>
public class UpdateChecker
{
    private readonly HttpClient _http;
    private readonly ToolRegistry _registry;

    public UpdateChecker(ToolRegistry registry)
    {
        _registry = registry;
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
        // GitHub API 强制要求 User-Agent,否则返回 403
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("FPSToolbox-Updater/1.0");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        var result = new UpdateCheckResult();
        try
        {
            var releases = await _http.GetFromJsonAsync<List<GhRelease>>(
                UpdateConstants.ReleasesListEndpoint, ct);
            if (releases == null)
            {
                result.ErrorMessage = "GitHub API 返回空";
                return result;
            }

            // 三个组件各自找前缀匹配的最新 release
            var toolboxLatest = PickLatest(releases, UpdateConstants.TagPrefix.Toolbox);
            var crosshairLatest = PickLatest(releases, UpdateConstants.TagPrefix.Crosshair);
            var gammaLatest = PickLatest(releases, UpdateConstants.TagPrefix.Gamma);

            result.Toolbox = BuildToolboxInfo(toolboxLatest);
            result.Crosshair = BuildToolInfo(ToolIds.CrosshairTool, "屏幕准心工具",
                crosshairLatest, UpdateConstants.AssetNamePattern.CrosshairZip);
            result.Gamma = BuildToolInfo(ToolIds.GammaTool, "屏幕调节工具",
                gammaLatest, UpdateConstants.AssetNamePattern.GammaZip);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
        }
        return result;
    }

    private ComponentUpdateInfo BuildToolboxInfo(GhRelease? release)
    {
        var info = new ComponentUpdateInfo
        {
            ComponentId = UpdateConstants.ComponentId.Toolbox,
            DisplayName = "FPS 工具箱",
            CurrentVersion = GetCurrentToolboxVersion(),
        };
        if (release == null) return info;

        info.LatestVersion = StripPrefix(release.TagName, UpdateConstants.TagPrefix.Toolbox);
        info.ReleaseNotes = release.Body;
        info.PublishedAt = release.PublishedAt;
        info.ToolboxOnlineInstallerUrl = FindAsset(release,
            UpdateConstants.AssetNamePattern.ToolboxOnlineExe,
            excludePattern: UpdateConstants.AssetNamePattern.ToolboxOfflineExe);
        info.ToolboxOfflineInstallerUrl = FindAsset(release,
            UpdateConstants.AssetNamePattern.ToolboxOfflineExe);
        info.HasUpdate = IsNewer(info.LatestVersion, info.CurrentVersion);
        return info;
    }

    private ComponentUpdateInfo BuildToolInfo(string toolId, string displayName,
        GhRelease? release, string zipPattern)
    {
        var installed = _registry.Get(toolId);
        var info = new ComponentUpdateInfo
        {
            ComponentId = toolId,
            DisplayName = displayName,
            CurrentVersion = installed?.Version,
        };
        if (release == null) return info;

        var prefix = toolId == ToolIds.CrosshairTool
            ? UpdateConstants.TagPrefix.Crosshair
            : UpdateConstants.TagPrefix.Gamma;
        info.LatestVersion = StripPrefix(release.TagName, prefix);
        info.ReleaseNotes = release.Body;
        info.PublishedAt = release.PublishedAt;
        info.ToolZipUrl = FindAsset(release, zipPattern);
        info.HasUpdate = installed != null && IsNewer(info.LatestVersion, info.CurrentVersion);
        return info;
    }

    private static string GetCurrentToolboxVersion()
    {
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        if (v == null) return "0.0.0";
        return $"{v.Major}.{v.Minor}.{v.Build}";
    }

    /// <summary>
    /// 从 releases 列表里筛出指定前缀的 tag,按语义化版本选最新。
    /// </summary>
    private static GhRelease? PickLatest(List<GhRelease> releases, string prefix)
    {
        return releases
            .Where(r => !r.Draft && !r.Prerelease)
            .Where(r => r.TagName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => ParseSemVer(StripPrefix(r.TagName, prefix)))
            .FirstOrDefault();
    }

    private static string StripPrefix(string tag, string prefix)
    {
        if (tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return tag.Substring(prefix.Length);
        return tag;
    }

    private static string? FindAsset(GhRelease release, string pattern, string? excludePattern = null)
    {
        foreach (var a in release.Assets ?? new())
        {
            if (!MatchGlob(a.Name, pattern)) continue;
            if (excludePattern != null && MatchGlob(a.Name, excludePattern)) continue;
            return a.BrowserDownloadUrl;
        }
        return null;
    }

    /// <summary>极简 glob:只支持 '*' 通配</summary>
    private static bool MatchGlob(string name, string pattern)
    {
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(
            name, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    /// <summary>简化的语义化版本排序键</summary>
    private static (int, int, int) ParseSemVer(string s)
    {
        var parts = s.TrimStart('v', 'V').Split('.', '-');
        int.TryParse(parts.ElementAtOrDefault(0), out var major);
        int.TryParse(parts.ElementAtOrDefault(1), out var minor);
        int.TryParse(parts.ElementAtOrDefault(2), out var patch);
        return (major, minor, patch);
    }

    private static bool IsNewer(string? remote, string? local)
    {
        if (string.IsNullOrWhiteSpace(remote) || string.IsNullOrWhiteSpace(local)) return false;
        return ParseSemVer(remote).CompareTo(ParseSemVer(local)) > 0;
    }

    // ──────────────────────────────────────────────────────────────
    // GitHub API DTO
    // ──────────────────────────────────────────────────────────────
    private class GhRelease
    {
        [JsonPropertyName("tag_name")] public string TagName { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("body")] public string? Body { get; set; }
        [JsonPropertyName("published_at")] public DateTime PublishedAt { get; set; }
        [JsonPropertyName("draft")] public bool Draft { get; set; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
        [JsonPropertyName("assets")] public List<GhAsset>? Assets { get; set; }
    }
    private class GhAsset
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("browser_download_url")] public string BrowserDownloadUrl { get; set; } = "";
        [JsonPropertyName("size")] public long Size { get; set; }
    }
}
