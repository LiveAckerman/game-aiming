using FPSToolbox.Shared.Native;

namespace GammaTool.Core;

/// <summary>一个物理显示器的元数据。</summary>
public class MonitorInfo
{
    public string DeviceName { get; set; } = "";
    public int Left { get; set; }
    public int Top { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsPrimary { get; set; }

    public string DisplayName =>
        $"{(IsPrimary ? "[主] " : "")}{DeviceName}  ({Width}x{Height})";
}

/// <summary>
/// 枚举所有显示器。键为 DeviceName（\\.\\DISPLAY1 之类），可以传给 CreateDC 获取 HDC。
/// </summary>
public static class MonitorEnumerator
{
    public static List<MonitorInfo> EnumerateAll()
    {
        var list = new List<MonitorInfo>();
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr _,
            ref NativeMethods.RECT rc, IntPtr _) =>
        {
            var info = new NativeMethods.MONITORINFOEX
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFOEX>(),
                szDevice = ""
            };
            if (NativeMethods.GetMonitorInfo(hMon, ref info))
            {
                list.Add(new MonitorInfo
                {
                    DeviceName = info.szDevice,
                    Left = info.rcMonitor.Left,
                    Top = info.rcMonitor.Top,
                    Width = info.rcMonitor.Right - info.rcMonitor.Left,
                    Height = info.rcMonitor.Bottom - info.rcMonitor.Top,
                    IsPrimary = (info.dwFlags & 0x1) != 0,
                });
            }
            return true;
        }, IntPtr.Zero);
        return list;
    }
}
