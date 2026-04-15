using System.IO;
using System.Text.Json;
using CrosshairTool.Models;

namespace CrosshairTool.Core;

public class ConfigManager
{
    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CrosshairTool");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");
    private static readonly string PresetsDir = Path.Combine(ConfigDir, "presets");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public CrosshairConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
                return new CrosshairConfig();

            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<CrosshairConfig>(json, JsonOptions) ?? new CrosshairConfig();
        }
        catch
        {
            return new CrosshairConfig();
        }
    }

    public void Save(CrosshairConfig config)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // 写入失败时静默处理，不影响主程序运行
        }
    }

    public List<string> GetPresetNames()
    {
        if (!Directory.Exists(PresetsDir))
            return new List<string>();

        return Directory.GetFiles(PresetsDir, "*.json")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .ToList();
    }

    public CrosshairConfig? LoadPreset(string name)
    {
        try
        {
            var path = Path.Combine(PresetsDir, $"{SanitizeFileName(name)}.json");
            if (!File.Exists(path)) return null;

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<CrosshairConfig>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void SavePreset(string name, CrosshairConfig config)
    {
        Directory.CreateDirectory(PresetsDir);
        var clone = config.Clone();
        clone.PresetName = name;
        var json = JsonSerializer.Serialize(clone, JsonOptions);
        File.WriteAllText(Path.Combine(PresetsDir, $"{SanitizeFileName(name)}.json"), json);
    }

    public void DeletePreset(string name)
    {
        var path = Path.Combine(PresetsDir, $"{SanitizeFileName(name)}.json");
        if (File.Exists(path))
            File.Delete(path);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
