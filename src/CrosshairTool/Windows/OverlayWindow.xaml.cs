using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using CrosshairTool.Core;
using CrosshairTool.Rendering;
using AppCtx = CrosshairTool.Core.AppContext;
using Point = System.Windows.Point;

namespace CrosshairTool.Windows;

public partial class OverlayWindow : Window
{
    private readonly CrosshairRenderer _renderer = new();

    // 每隔此毫秒数主动重新设置 TOPMOST，防止被游戏压下去
    private const int TopmostIntervalMs = 200;

    private DispatcherTimer? _topmostTimer;
    private IntPtr           _hwnd = IntPtr.Zero;

    public OverlayWindow()
    {
        InitializeComponent();
        Loaded  += OnLoaded;
        Closed  += OnClosed;

        // 覆盖全部虚拟屏幕（多显示器兼容）
        Left   = SystemParameters.VirtualScreenLeft;
        Top    = SystemParameters.VirtualScreenTop;
        Width  = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        AppCtx.Instance.ConfigChanged    += (_, _) => InvalidateVisual();
        AppCtx.Instance.VisibilityChanged += OnVisibilityChanged;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  初始化
    // ──────────────────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;

        ApplyWindowStyle();
        ForceTopmost();

        // Hook 窗口消息，拦截 Z 序变更
        var source = HwndSource.FromHwnd(_hwnd);
        source?.AddHook(WndProc);

        // 定时器：周期性重新置顶
        _topmostTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(TopmostIntervalMs)
        };
        _topmostTimer.Tick += (_, _) => ForceTopmost();
        _topmostTimer.Start();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _topmostTimer?.Stop();
        _topmostTimer = null;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Win32 工具方法
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>设置点击穿透、分层、不出现在 Alt+Tab 等扩展样式。</summary>
    private void ApplyWindowStyle()
    {
        int exStyle = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE,
            exStyle
            | NativeMethods.WS_EX_TRANSPARENT
            | NativeMethods.WS_EX_LAYERED
            | NativeMethods.WS_EX_TOOLWINDOW
            | NativeMethods.WS_EX_NOACTIVATE);
    }

    /// <summary>强制将窗口置于最顶层（不激活、不移动、不改变大小）。</summary>
    private void ForceTopmost()
    {
        if (_hwnd == IntPtr.Zero || !IsVisible) return;

        NativeMethods.SetWindowPos(
            _hwnd,
            NativeMethods.HWND_TOPMOST,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  WndProc Hook：拦截 Z 序变更
    // ──────────────────────────────────────────────────────────────────────────

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam,
                           ref bool handled)
    {
        if (msg == NativeMethods.WM_WINDOWPOSCHANGING)
        {
            // 读取传入的 WINDOWPOS 结构体
            var pos = Marshal.PtrToStructure<NativeMethods.WINDOWPOS>(lParam);

            // 若有人试图改变 Z 序（SWP_NOZORDER 未置位），
            // 强制将 hwndInsertAfter 改回 HWND_TOPMOST 并清除 SWP_NOZORDER 标志
            if ((pos.flags & NativeMethods.SWP_NOZORDER) == 0)
            {
                pos.hwndInsertAfter = NativeMethods.HWND_TOPMOST;
                pos.flags |= NativeMethods.SWP_NOZORDER; // 告知系统不需要改变 Z 序
                Marshal.StructureToPtr(pos, lParam, false);
            }
        }

        return IntPtr.Zero;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  可见性切换
    // ──────────────────────────────────────────────────────────────────────────

    private void OnVisibilityChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            bool visible = AppCtx.Instance.IsOverlayVisible;
            Visibility = visible ? Visibility.Visible : Visibility.Hidden;

            // 隐藏时暂停定时器节省资源，显示时立即重新置顶
            if (visible)
            {
                ForceTopmost();
                _topmostTimer?.Start();
            }
            else
            {
                _topmostTimer?.Stop();
            }
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  渲染
    // ──────────────────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (!AppCtx.Instance.IsOverlayVisible) return;

        var config = AppCtx.Instance.Config;

        var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
        int screenWidth  = primaryScreen?.Bounds.Width  ?? (int)SystemParameters.PrimaryScreenWidth;
        int screenHeight = primaryScreen?.Bounds.Height ?? (int)SystemParameters.PrimaryScreenHeight;
        int screenLeft   = primaryScreen?.Bounds.Left ?? 0;
        int screenTop    = primaryScreen?.Bounds.Top  ?? 0;

        // 将物理像素坐标转换为 WPF DIP
        var source = PresentationSource.FromVisual(this);
        double dpiX = 1.0, dpiY = 1.0;
        if (source?.CompositionTarget != null)
        {
            dpiX = source.CompositionTarget.TransformFromDevice.M11;
            dpiY = source.CompositionTarget.TransformFromDevice.M22;
        }

        double centerX = (screenLeft + screenWidth  / 2.0) * dpiX + config.OffsetX;
        double centerY = (screenTop  + screenHeight / 2.0) * dpiY + config.OffsetY;

        // 转换为相对于 OverlayWindow 的局部坐标
        centerX -= Left;
        centerY -= Top;

        _renderer.Draw(drawingContext, config, new Point(centerX, centerY));
    }
}
