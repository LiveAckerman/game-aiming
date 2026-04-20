using System.IO;
using System.Net.Http;
using FPSToolbox.Models;

namespace FPSToolbox.Core;

/// <summary>
/// 从 URL 下载子工具 zip 包，然后交给 <see cref="ToolPackageInstaller"/> 安装。
/// 支持 http/https 直链，也支持 file:// 和本地绝对路径（方便测试）。
/// </summary>
public static class ToolDownloader
{
    private static readonly HttpClient _http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
        };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(5),
        };
        // 模拟浏览器请求头。很多网盘(蓝奏云/OneDrive/等)会根据 UA 决定返回 HTML 分享页还是文件流。
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Accept.ParseAdd("*/*");
        return client;
    }

    public static async Task<InstalledTool> DownloadAndInstallAsync(
        string downloadUrl,
        string toolboxBaseDir,
        IProgress<(long received, long? total)>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(downloadUrl))
            throw new InvalidOperationException("未配置下载链接。请让开发者在 ToolRegistry 里填写 DownloadUrl。");

        var tmpZip = Path.Combine(Path.GetTempPath(),
            $"fpstoolbox-dl-{Guid.NewGuid():N}.zip");

        try
        {
            // 本地路径 / file:// 协议 → 直接复制,避免本地调试时依赖 HTTP
            if (IsLocalPath(downloadUrl, out var localPath))
            {
                if (!File.Exists(localPath))
                    throw new FileNotFoundException("本地 zip 不存在", localPath);
                File.Copy(localPath, tmpZip, overwrite: true);
                progress?.Report((new FileInfo(tmpZip).Length, new FileInfo(tmpZip).Length));
            }
            else
            {
                await DownloadHttpAsync(downloadUrl, tmpZip, progress, ct);
            }

            return ToolPackageInstaller.InstallFromZip(tmpZip, toolboxBaseDir);
        }
        finally
        {
            try { if (File.Exists(tmpZip)) File.Delete(tmpZip); } catch { /* 清理失败忽略 */ }
        }
    }

    /// <summary>
    /// 下载任意 URL 到指定本地文件(不解压),供主框架 installer 升级使用。
    /// </summary>
    public static async Task DownloadRawAsync(
        string url,
        string destPath,
        IProgress<(long received, long? total)>? progress = null,
        CancellationToken ct = default)
    {
        if (IsLocalPath(url, out var localPath))
        {
            File.Copy(localPath, destPath, overwrite: true);
            progress?.Report((new FileInfo(destPath).Length, new FileInfo(destPath).Length));
            return;
        }
        await DownloadHttpAsync(url, destPath, progress, ct);
    }

    private static bool IsLocalPath(string url, out string localPath)
    {
        if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            localPath = new Uri(url).LocalPath;
            return true;
        }
        if (Path.IsPathRooted(url) && !url.Contains("://"))
        {
            localPath = url;
            return true;
        }
        localPath = "";
        return false;
    }

    private static async Task DownloadHttpAsync(
        string url,
        string destPath,
        IProgress<(long received, long? total)>? progress,
        CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        // 某些 CDN(如蓝奏云)会检查 Referer
        req.Headers.Referrer = new Uri(new Uri(url).GetLeftPart(UriPartial.Authority));

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        // 防呆:如果网盘返回了 HTML 页面而不是二进制文件,直接给出明确错误
        var contentType = resp.Content.Headers.ContentType?.MediaType ?? "";
        if (contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("application/xhtml", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "服务器返回的是 HTML 分享页面,不是 zip 文件。\n\n" +
                "原因:你填的 URL 不是\"直链\"。\n" +
                "常见坑:\n" +
                " • 蓝奏云/百度网盘/夸克/123网盘 的分享链接都是中转页,不是直链\n" +
                " • OneDrive 分享链接需要末尾加 ?download=1\n" +
                " • GitHub Release 资源 URL 是天然直链,推荐使用\n\n" +
                $"实际 Content-Type: {contentType}");
        }

        var total = resp.Content.Headers.ContentLength;
        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        await using var dst = File.Create(destPath);

        var buffer = new byte[81920];
        long received = 0;
        int read;
        var lastReport = DateTime.MinValue;
        while ((read = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, read), ct);
            received += read;

            // 限速回调：每 150ms 一次,避免 UI 卡顿
            if ((DateTime.Now - lastReport).TotalMilliseconds >= 150)
            {
                progress?.Report((received, total));
                lastReport = DateTime.Now;
            }
        }
        progress?.Report((received, total));
    }
}
