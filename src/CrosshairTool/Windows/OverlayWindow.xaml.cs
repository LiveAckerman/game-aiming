using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CrosshairTool.Core;
using CrosshairTool.Rendering;
using AppCtx = CrosshairTool.Core.AppContext;
using Point = System.Windows.Point;

namespace CrosshairTool.Windows;

public partial class OverlayWindow : Window
{
    private readonly CrosshairRenderer _renderer = new();

    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;

        // 覆盖全部虚拟屏幕（多显示器兼容）
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        AppCtx.Instance.ConfigChanged += (_, _) => InvalidateVisual();
        AppCtx.Instance.VisibilityChanged += OnVisibilityChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SetWindowProperties();
    }

    private void SetWindowProperties()
    {
        var hwnd = new WindowInteropHelper(this).Handle;

        // 永远置顶
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

        // 点击穿透 + 分层窗口 + 不在 Alt+Tab 中显示
        int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
            exStyle
            | NativeMethods.WS_EX_TRANSPARENT
            | NativeMethods.WS_EX_LAYERED
            | NativeMethods.WS_EX_TOOLWINDOW
            | NativeMethods.WS_EX_NOACTIVATE);
    }

    private void OnVisibilityChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            Visibility = AppCtx.Instance.IsOverlayVisible ? Visibility.Visible : Visibility.Hidden;
        });
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (!AppCtx.Instance.IsOverlayVisible) return;

        var config = AppCtx.Instance.Config;

        // 使用整个屏幕宽高除以2定位主屏中心
        // WPF 虚拟坐标系中，主屏占据 (0,0) 到 (primaryWidth, primaryHeight)
        var screenWidth = System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Width
                          ?? (int)SystemParameters.PrimaryScreenWidth;
        var screenHeight = System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Height
                           ?? (int)SystemParameters.PrimaryScreenHeight;
        var screenLeft = System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Left ?? 0;
        var screenTop = System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Top ?? 0;

        // 转换为 WPF DIP（设备无关像素）
        var source = PresentationSource.FromVisual(this);
        double dpiX = 1.0, dpiY = 1.0;
        if (source?.CompositionTarget != null)
        {
            dpiX = source.CompositionTarget.TransformFromDevice.M11;
            dpiY = source.CompositionTarget.TransformFromDevice.M22;
        }

        double centerX = (screenLeft + screenWidth / 2.0) * dpiX + config.OffsetX;
        double centerY = (screenTop + screenHeight / 2.0) * dpiY + config.OffsetY;

        // 转换为相对于 OverlayWindow 的坐标
        centerX -= Left;
        centerY -= Top;

        _renderer.Draw(drawingContext, config, new Point(centerX, centerY));
    }
}
