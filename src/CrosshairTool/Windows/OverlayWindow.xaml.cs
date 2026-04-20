using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using CrosshairTool.Core;
using CrosshairTool.Rendering;
using FPSToolbox.Shared.Native;
using AppCtx = CrosshairTool.Core.AppContext;
using Point = System.Windows.Point;

namespace CrosshairTool.Windows;

public partial class OverlayWindow : Window
{
    private readonly CrosshairRenderer _renderer = new();

    private const int TopmostIntervalMs = 200;

    private DispatcherTimer? _topmostTimer;
    private IntPtr _hwnd = IntPtr.Zero;

    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        AppCtx.Instance.ConfigChanged += (_, _) => InvalidateVisual();
        AppCtx.Instance.VisibilityChanged += OnVisibilityChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;

        ApplyWindowStyle();
        ForceTopmost();

        var source = HwndSource.FromHwnd(_hwnd);
        source?.AddHook(WndProc);

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

    private void ForceTopmost()
    {
        if (_hwnd == IntPtr.Zero || !IsVisible) return;

        NativeMethods.SetWindowPos(
            _hwnd,
            NativeMethods.HWND_TOPMOST,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_WINDOWPOSCHANGING)
        {
            var pos = Marshal.PtrToStructure<NativeMethods.WINDOWPOS>(lParam);
            if ((pos.flags & NativeMethods.SWP_NOZORDER) == 0)
            {
                pos.hwndInsertAfter = NativeMethods.HWND_TOPMOST;
                pos.flags |= NativeMethods.SWP_NOZORDER;
                Marshal.StructureToPtr(pos, lParam, false);
            }
        }
        return IntPtr.Zero;
    }

    private void OnVisibilityChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            bool visible = AppCtx.Instance.IsOverlayVisible;
            Visibility = visible ? Visibility.Visible : Visibility.Hidden;

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

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (!AppCtx.Instance.IsOverlayVisible) return;

        var config = AppCtx.Instance.Config;

        var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
        int screenWidth = primaryScreen?.Bounds.Width ?? (int)SystemParameters.PrimaryScreenWidth;
        int screenHeight = primaryScreen?.Bounds.Height ?? (int)SystemParameters.PrimaryScreenHeight;
        int screenLeft = primaryScreen?.Bounds.Left ?? 0;
        int screenTop = primaryScreen?.Bounds.Top ?? 0;

        var source = PresentationSource.FromVisual(this);
        double dpiX = 1.0, dpiY = 1.0;
        if (source?.CompositionTarget != null)
        {
            dpiX = source.CompositionTarget.TransformFromDevice.M11;
            dpiY = source.CompositionTarget.TransformFromDevice.M22;
        }

        double centerX = (screenLeft + screenWidth / 2.0) * dpiX + config.OffsetX;
        double centerY = (screenTop + screenHeight / 2.0) * dpiY + config.OffsetY;

        centerX -= Left;
        centerY -= Top;

        _renderer.Draw(drawingContext, config, new Point(centerX, centerY));
    }
}
