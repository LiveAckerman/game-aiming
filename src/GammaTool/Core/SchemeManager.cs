using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FPSToolbox.Shared;
using FPSToolbox.Shared.Config;
using FPSToolbox.Shared.Ipc;
using GammaTool.Models;

namespace GammaTool.Core;

/// <summary>
/// 配色方案管理。每个方案一个 json 文件，存在 %AppData%\FPSToolbox\GammaTool\presets\。
/// </summary>
public class SchemeManager
{
    private static readonly string PresetsDir = PathService.GetToolPresetsDir(ToolIds.GammaTool);
    private static readonly string ConfigPath = PathService.GetToolConfigPath(ToolIds.GammaTool);

    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public GammaToolConfig LoadConfig() => JsonConfigStore.Load<GammaToolConfig>(ConfigPath);
    public void SaveConfig(GammaToolConfig cfg) => JsonConfigStore.Save(ConfigPath, cfg);

    public List<GammaScheme> LoadAllSchemes()
    {
        var list = new List<GammaScheme>();
        if (!Directory.Exists(PresetsDir)) return list;
        foreach (var file in Directory.GetFiles(PresetsDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var scheme = JsonSerializer.Deserialize<GammaScheme>(json, JsonOpt);
                if (scheme != null) list.Add(scheme);
            }
            catch { }
        }
        return list;
    }

    public void Save(GammaScheme scheme)
    {
        Directory.CreateDirectory(PresetsDir);
        var path = Path.Combine(PresetsDir,
            $"{JsonConfigStore.SanitizeFileName(scheme.Name)}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(scheme, JsonOpt));
    }

    public void Delete(string name)
    {
        var path = Path.Combine(PresetsDir,
            $"{JsonConfigStore.SanitizeFileName(name)}.json");
        if (File.Exists(path)) File.Delete(path);
    }
}

public class GammaToolConfig
{
    public string? LastSchemeName { get; set; }
    public bool ApplyOnStart { get; set; } = false;
    public string? GlobalResetHotkey { get; set; } = "Ctrl+Alt+G";
}
