using FPSToolbox.Shared.Native;
using GammaTool.Models;

namespace GammaTool.Core;

/// <summary>
/// 根据 <see cref="GammaConfig"/> 计算 256 项 LUT（Gamma Ramp）。
///
/// 基础公式（每个通道独立计算）：
///   x        = i / 255                              // 归一化输入
///   xContr   = (x - 0.5) * (1 + contrast) + 0.5     // 对比度（围绕 0.5 展开）
///   xBright  = xContr + brightness                   // 亮度（平移）
///   xGamma   = pow(clamp(xBright, 0, 1), 1 / gamma) // Gamma
///   lut[i]   = round(xGamma * 65535)                // 16-bit 输出
/// </summary>
public static class LutCalculator
{
    public static NativeMethods.GammaRamp Build(GammaConfig cfg)
    {
        var ramp = new NativeMethods.GammaRamp
        {
            Red = new ushort[256],
            Green = new ushort[256],
            Blue = new ushort[256]
        };

        var r = cfg.LinkedRgb ? cfg.Master : cfg.Red;
        var g = cfg.LinkedRgb ? cfg.Master : cfg.Green;
        var b = cfg.LinkedRgb ? cfg.Master : cfg.Blue;

        for (int i = 0; i < 256; i++)
        {
            ramp.Red[i] = Sample(i, r);
            ramp.Green[i] = Sample(i, g);
            ramp.Blue[i] = Sample(i, b);
        }
        return ramp;
    }

    public static double[] BuildNormalized(ChannelParams p)
    {
        var arr = new double[256];
        for (int i = 0; i < 256; i++) arr[i] = SampleNormalized(i, p);
        return arr;
    }

    private static ushort Sample(int i, ChannelParams p)
    {
        double y = SampleNormalized(i, p);
        int v = (int)Math.Round(y * 65535.0);
        if (v < 0) v = 0;
        if (v > 65535) v = 65535;
        return (ushort)v;
    }

    private static double SampleNormalized(int i, ChannelParams p)
    {
        double x = i / 255.0;
        double xc = (x - 0.5) * (1.0 + p.Contrast) + 0.5;
        double xb = xc + p.Brightness;
        if (xb < 0) xb = 0;
        if (xb > 1) xb = 1;
        double gamma = p.Gamma <= 0.01 ? 0.01 : p.Gamma;
        return Math.Pow(xb, 1.0 / gamma);
    }
}
