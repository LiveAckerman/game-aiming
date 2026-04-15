namespace CrosshairTool.Models;

public class CrosshairConfig
{
    // 基础样式
    public CrosshairStyle Style { get; set; } = CrosshairStyle.Cross;
    public string Color { get; set; } = "#00FF00";
    public double Opacity { get; set; } = 1.0;
    public int Size { get; set; } = 20;
    public int Thickness { get; set; } = 2;
    public int Gap { get; set; } = 4;

    // 轮廓
    public bool OutlineEnabled { get; set; } = true;
    public string OutlineColor { get; set; } = "#000000";
    public int OutlineThickness { get; set; } = 1;

    // 中心点
    public bool DotEnabled { get; set; } = false;
    public int DotSize { get; set; } = 3;
    public string DotColor { get; set; } = "#00FF00";

    // 顶部线条
    public bool TopLineEnabled { get; set; } = true;

    // 位置偏移
    public int OffsetX { get; set; } = 0;
    public int OffsetY { get; set; } = 0;

    // 热键
    public string ToggleHotkey { get; set; } = "F8";

    // 当前预设名称
    public string PresetName { get; set; } = "默认";

    public CrosshairConfig Clone()
    {
        return new CrosshairConfig
        {
            Style = Style,
            Color = Color,
            Opacity = Opacity,
            Size = Size,
            Thickness = Thickness,
            Gap = Gap,
            OutlineEnabled = OutlineEnabled,
            OutlineColor = OutlineColor,
            OutlineThickness = OutlineThickness,
            DotEnabled = DotEnabled,
            DotSize = DotSize,
            DotColor = DotColor,
            TopLineEnabled = TopLineEnabled,
            OffsetX = OffsetX,
            OffsetY = OffsetY,
            ToggleHotkey = ToggleHotkey,
            PresetName = PresetName
        };
    }
}

public enum CrosshairStyle
{
    Cross,      // 十字准心
    Dot,        // 纯中心点
    Circle,     // 圆形
    CrossDot,   // 十字 + 中心点
}
