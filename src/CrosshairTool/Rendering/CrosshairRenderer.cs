using System.Windows.Media;
using CrosshairTool.Models;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CrosshairTool.Rendering;

public class CrosshairRenderer
{
    public void Draw(DrawingContext dc, CrosshairConfig config, Point center)
    {
        var color = ParseColor(config.Color);
        var alpha = (byte)(config.Opacity * 255);
        var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
        brush.Freeze();

        var pen = new Pen(brush, config.Thickness);
        pen.Freeze();

        Pen? outlinePen = null;
        if (config.OutlineEnabled)
        {
            var outlineColor = ParseColor(config.OutlineColor);
            var outlineBrush = new SolidColorBrush(Color.FromArgb(alpha, outlineColor.R, outlineColor.G, outlineColor.B));
            outlineBrush.Freeze();
            outlinePen = new Pen(outlineBrush, config.Thickness + config.OutlineThickness * 2);
            outlinePen.Freeze();
        }

        switch (config.Style)
        {
            case CrosshairStyle.Cross:
                DrawCross(dc, config, center, pen, outlinePen);
                break;
            case CrosshairStyle.CrossDot:
                DrawCross(dc, config, center, pen, outlinePen);
                DrawDot(dc, config, center, alpha);
                break;
            case CrosshairStyle.Circle:
                DrawCircle(dc, config, center, pen, outlinePen);
                break;
            case CrosshairStyle.Dot:
                DrawDot(dc, config, center, alpha);
                break;
        }

        // 单独启用中心点（Cross 样式下叠加）
        if (config.Style == CrosshairStyle.Cross && config.DotEnabled)
        {
            DrawDot(dc, config, center, alpha);
        }
    }

    private void DrawCross(DrawingContext dc, CrosshairConfig cfg,
        Point c, Pen pen, Pen? outlinePen)
    {
        int halfGap = cfg.Gap;
        int halfLen = halfGap + cfg.Size;

        // 先画轮廓（在下层）
        if (outlinePen != null)
        {
            if (cfg.TopLineEnabled)
                dc.DrawLine(outlinePen, new Point(c.X, c.Y - halfLen), new Point(c.X, c.Y - halfGap));
            dc.DrawLine(outlinePen, new Point(c.X, c.Y + halfGap), new Point(c.X, c.Y + halfLen));
            dc.DrawLine(outlinePen, new Point(c.X - halfLen, c.Y), new Point(c.X - halfGap, c.Y));
            dc.DrawLine(outlinePen, new Point(c.X + halfGap, c.Y), new Point(c.X + halfLen, c.Y));
        }

        // 主体线条
        if (cfg.TopLineEnabled)
            dc.DrawLine(pen, new Point(c.X, c.Y - halfLen), new Point(c.X, c.Y - halfGap));
        dc.DrawLine(pen, new Point(c.X, c.Y + halfGap), new Point(c.X, c.Y + halfLen));
        dc.DrawLine(pen, new Point(c.X - halfLen, c.Y), new Point(c.X - halfGap, c.Y));
        dc.DrawLine(pen, new Point(c.X + halfGap, c.Y), new Point(c.X + halfLen, c.Y));
    }

    private void DrawCircle(DrawingContext dc, CrosshairConfig cfg,
        Point c, Pen pen, Pen? outlinePen)
    {
        double radius = cfg.Size;

        if (outlinePen != null)
            dc.DrawEllipse(null, outlinePen, c, radius, radius);

        dc.DrawEllipse(null, pen, c, radius, radius);
    }

    private void DrawDot(DrawingContext dc, CrosshairConfig cfg, Point c, byte alpha)
    {
        var dotColor = ParseColor(cfg.DotColor);
        var dotBrush = new SolidColorBrush(Color.FromArgb(alpha, dotColor.R, dotColor.G, dotColor.B));
        dotBrush.Freeze();

        double radius = cfg.DotSize / 2.0;
        dc.DrawEllipse(dotBrush, null, c, radius, radius);
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Colors.Green;
        }
    }
}
