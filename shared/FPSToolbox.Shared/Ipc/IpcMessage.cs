using System.Text.Json;
using System.Text.Json.Serialization;

namespace FPSToolbox.Shared.Ipc;

/// <summary>
/// IPC 消息的基础形态：JSON 行协议 (NDJSON)。每条消息一行，UTF-8 编码。
/// 三种 kind：request（需要响应） / response（对 request 的应答） / event（单向事件）。
/// </summary>
public sealed class IpcMessage
{
    [JsonPropertyName("id")]       public long Id { get; set; }
    [JsonPropertyName("kind")]     public string Kind { get; set; } = "event";
    [JsonPropertyName("action")]   public string? Action { get; set; }
    [JsonPropertyName("topic")]    public string? Topic { get; set; }
    [JsonPropertyName("ok")]       public bool? Ok { get; set; }
    [JsonPropertyName("error")]    public string? Error { get; set; }
    [JsonPropertyName("payload")]  public JsonElement? Payload { get; set; }

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string Serialize() => JsonSerializer.Serialize(this, JsonOptions);

    public static IpcMessage? TryParse(string line)
    {
        try { return JsonSerializer.Deserialize<IpcMessage>(line, JsonOptions); }
        catch { return null; }
    }

    public T? GetPayload<T>()
    {
        if (Payload == null) return default;
        try { return Payload.Value.Deserialize<T>(JsonOptions); }
        catch { return default; }
    }

    public static IpcMessage Request(long id, string action, object? payload = null) =>
        new()
        {
            Id = id,
            Kind = "request",
            Action = action,
            Payload = payload == null ? null : JsonSerializer.SerializeToElement(payload, JsonOptions)
        };

    public static IpcMessage Response(long id, bool ok, object? data = null, string? error = null) =>
        new()
        {
            Id = id,
            Kind = "response",
            Ok = ok,
            Error = error,
            Payload = data == null ? null : JsonSerializer.SerializeToElement(data, JsonOptions)
        };

    public static IpcMessage Event(string topic, object? payload = null) =>
        new()
        {
            Kind = "event",
            Topic = topic,
            Payload = payload == null ? null : JsonSerializer.SerializeToElement(payload, JsonOptions)
        };
}

/// <summary>已知的 Action 字符串。</summary>
public static class IpcActions
{
    // Toolbox → any child
    public const string Shutdown = "shutdown";
    public const string Ping = "ping";

    // Toolbox → CrosshairTool
    public const string CrosshairShow = "crosshair.show";
    public const string CrosshairHide = "crosshair.hide";
    public const string CrosshairToggle = "crosshair.toggle";
    public const string CrosshairOpenSettings = "crosshair.openSettings";
    public const string CrosshairReloadConfig = "crosshair.reloadConfig";
    public const string CrosshairApplyPreset = "crosshair.applyPreset";

    // Toolbox → GammaTool
    public const string GammaOpenPanel = "gamma.openPanel";
    public const string GammaApplyPreset = "gamma.applyPreset";
    public const string GammaListSchemes = "gamma.listSchemes";
    public const string GammaResetSystem = "gamma.resetSystem";
}

/// <summary>已知的 Event topic 字符串。</summary>
public static class IpcTopics
{
    public const string ToolReady = "tool.ready";
    public const string ToolExiting = "tool.exiting";

    public const string CrosshairVisibility = "crosshair.visibility";
    public const string CrosshairConfigChanged = "crosshair.configChanged";

    public const string GammaPreviewState = "gamma.previewState";
    public const string GammaSchemeApplied = "gamma.schemeApplied";
    public const string GammaSchemesChanged = "gamma.schemesChanged";
}
