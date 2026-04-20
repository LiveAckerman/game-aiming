namespace GammaTool.Models;

/// <summary>
/// 单通道的三参数：gamma / brightness / contrast。
/// gamma ∈ [0.1, 4.0]（1.0 表示线性）
/// brightness ∈ [-1.0, 1.0]
/// contrast ∈ [-1.0, 1.0]
/// </summary>
public class ChannelParams
{
    public double Gamma { get; set; } = 1.0;
    public double Brightness { get; set; } = 0.0;
    public double Contrast { get; set; } = 0.0;

    public ChannelParams Clone() => new()
    {
        Gamma = Gamma,
        Brightness = Brightness,
        Contrast = Contrast
    };

    public bool IsDefault =>
        Math.Abs(Gamma - 1.0) < 1e-4 &&
        Math.Abs(Brightness) < 1e-4 &&
        Math.Abs(Contrast) < 1e-4;
}

/// <summary>
/// 一组 Gamma 配置：可以是 RGB 联动（只用 Master），也可以拆成三通道。
/// </summary>
public class GammaConfig
{
    public bool LinkedRgb { get; set; } = true;
    public ChannelParams Master { get; set; } = new();
    public ChannelParams Red { get; set; } = new();
    public ChannelParams Green { get; set; } = new();
    public ChannelParams Blue { get; set; } = new();

    public GammaConfig Clone() => new()
    {
        LinkedRgb = LinkedRgb,
        Master = Master.Clone(),
        Red = Red.Clone(),
        Green = Green.Clone(),
        Blue = Blue.Clone(),
    };

    public static GammaConfig Default => new();
}

/// <summary>配色方案（预设）。</summary>
public class GammaScheme
{
    public string Name { get; set; } = "默认";
    public GammaConfig Config { get; set; } = new();
    public string? Hotkey { get; set; }
    public Dictionary<string, GammaConfig> PerMonitor { get; set; } = new();
    public bool ApplyToAllMonitors { get; set; } = true;
}
