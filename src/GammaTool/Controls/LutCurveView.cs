using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GammaTool.Core;
using GammaTool.Models;

namespace GammaTool.Controls;

/// <summary>
/// 简易 LUT 曲线预览控件。绘制 RGB（或 Master）三条曲线。
/// </summary>
public class LutCurveView : FrameworkElement
{
    public static readonly DependencyProperty ConfigProperty =
        DependencyProperty.Register(nameof(Config), typeof(GammaConfig), typeof(LutCurveView),
            new FrameworkPropertyMetadata(GammaConfig.Default,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public GammaConfig Config
    {
        get => (GammaConfig)GetValue(ConfigProperty);
        set => SetValue(ConfigProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w < 8 || h < 8) return;

        // 背景
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)), null,
            new Rect(0, 0, w, h));

        // 网格
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 0.5);
        gridPen.Freeze();
        for (int k = 1; k < 4; k++)
        {
            double x = k * w / 4, y = k * h / 4;
            dc.DrawLine(gridPen, new Point(x, 0), new Point(x, h));
            dc.DrawLine(gridPen, new Point(0, y), new Point(w, y));
        }

        // 对角参考线
        var refPen = new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 0.5)
        { DashStyle = DashStyles.Dash };
        refPen.Freeze();
        dc.DrawLine(refPen, new Point(0, h), new Point(w, 0));

        var cfg = Config ?? GammaConfig.Default;

        if (cfg.LinkedRgb)
        {
            DrawCurve(dc, cfg.Master, Color.FromRgb(220, 220, 220), w, h);
        }
        else
        {
            DrawCurve(dc, cfg.Red, Color.FromRgb(255, 80, 80), w, h);
            DrawCurve(dc, cfg.Green, Color.FromRgb(80, 255, 80), w, h);
            DrawCurve(dc, cfg.Blue, Color.FromRgb(80, 150, 255), w, h);
        }
    }

    private static void DrawCurve(DrawingContext dc, ChannelParams p, Color color, double w, double h)
    {
        var lut = LutCalculator.BuildNormalized(p);
        var pen = new Pen(new SolidColorBrush(color), 1.5);
        pen.Freeze();

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(0, h - lut[0] * h), false, false);
            for (int i = 1; i < 256; i++)
            {
                double x = i * w / 255.0;
                double y = h - lut[i] * h;
                ctx.LineTo(new Point(x, y), true, false);
            }
        }
        geo.Freeze();
        dc.DrawGeometry(null, pen, geo);
    }
}
