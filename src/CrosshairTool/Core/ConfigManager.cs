using System.IO;
using CrosshairTool.Models;
using FPSToolbox.Shared;
using FPSToolbox.Shared.Config;
using FPSToolbox.Shared.Ipc;

namespace CrosshairTool.Core;

/// <summary>
/// 准心工具的配置 / 预设读写。路径由 <see cref="PathService"/> 统一提供。
/// </summary>
public class ConfigManager
{
    private static readonly string ConfigPath = PathService.GetToolConfigPath(ToolIds.CrosshairTool);
    private static readonly string PresetsDir = PathService.GetToolPresetsDir(ToolIds.CrosshairTool);

    public CrosshairConfig Load() => JsonConfigStore.Load<CrosshairConfig>(ConfigPath);

    public void Save(CrosshairConfig config) => JsonConfigStore.Save(ConfigPath, config);

    public List<string> GetPresetNames()
    {
        if (!Directory.Exists(PresetsDir)) return new List<string>();
        return Directory.GetFiles(PresetsDir, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .ToList();
    }

    public CrosshairConfig? LoadPreset(string name)
    {
        var path = Path.Combine(PresetsDir, $"{JsonConfigStore.SanitizeFileName(name)}.json");
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return System.Text.Json.JsonSerializer.Deserialize<CrosshairConfig>(json,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                });
        }
        catch { return null; }
    }

    public void SavePreset(string name, CrosshairConfig config)
    {
        var clone = config.Clone();
        clone.PresetName = name;
        var path = Path.Combine(PresetsDir, $"{JsonConfigStore.SanitizeFileName(name)}.json");
        JsonConfigStore.Save(path, clone);
    }

    public void DeletePreset(string name)
    {
        var path = Path.Combine(PresetsDir, $"{JsonConfigStore.SanitizeFileName(name)}.json");
        if (File.Exists(path)) File.Delete(path);
    }
}
