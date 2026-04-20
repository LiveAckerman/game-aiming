using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using FPSToolbox.Models;
using FPSToolbox.Shared.Ipc;

namespace FPSToolbox.Core;

/// <summary>
/// 从 .zip 包安装子工具。zip 内必须有 manifest.json：
///   { "name": "CrosshairTool", "version": "1.0.0", "exeName": "CrosshairTool.exe", "sha256": "..." }
/// 安装目标：&lt;ToolboxBaseDir&gt;\tools\&lt;Name&gt;\
/// </summary>
public static class ToolPackageInstaller
{
    public sealed class Manifest
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string ExeName { get; set; } = "";
        public string? Sha256 { get; set; }
    }

    public static InstalledTool InstallFromZip(string zipPath, string toolboxBaseDir)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("zip 文件不存在", zipPath);

        using var zip = ZipFile.OpenRead(zipPath);
        var manifestEntry = zip.GetEntry("manifest.json")
            ?? throw new InvalidDataException("zip 缺少 manifest.json");

        Manifest manifest;
        using (var sr = new StreamReader(manifestEntry.Open()))
        {
            manifest = JsonSerializer.Deserialize<Manifest>(sr.ReadToEnd(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("manifest.json 解析失败");
        }

        if (!ToolIds.IsKnown(manifest.Name))
            throw new InvalidOperationException(
                $"未知工具：{manifest.Name}（仅支持 CrosshairTool / GammaTool）");

        if (string.IsNullOrEmpty(manifest.ExeName))
            throw new InvalidDataException("manifest.exeName 为空");

        var targetDir = Path.Combine(toolboxBaseDir, "tools", manifest.Name);
        if (Directory.Exists(targetDir))
            Directory.Delete(targetDir, recursive: true);
        Directory.CreateDirectory(targetDir);

        foreach (var entry in zip.Entries)
        {
            if (entry.FullName.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.IsNullOrEmpty(entry.Name)) continue; // 目录

            var destPath = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));
            if (!destPath.StartsWith(targetDir, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("zip 包含越界路径，已中止");

            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            entry.ExtractToFile(destPath, overwrite: true);
        }

        var exePath = Path.Combine(targetDir, manifest.ExeName);
        if (!File.Exists(exePath))
            throw new InvalidDataException($"未在 zip 内找到 exe：{manifest.ExeName}");

        if (!string.IsNullOrEmpty(manifest.Sha256))
        {
            var actual = ComputeSha256(exePath);
            if (!string.Equals(actual, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("SHA-256 校验失败，文件可能被篡改");
        }

        return new InstalledTool
        {
            Name = manifest.Name,
            Version = manifest.Version,
            ExePath = exePath,
            InstalledAt = DateTime.Now,
        };
    }

    private static string ComputeSha256(string file)
    {
        using var fs = File.OpenRead(file);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(fs);
        return Convert.ToHexString(hash);
    }
}
