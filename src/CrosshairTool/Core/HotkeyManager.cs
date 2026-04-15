using System.Windows;
using System.Windows.Interop;

namespace CrosshairTool.Core;

public class HotkeyManager : IDisposable
{
    private HwndSource? _source;
    private readonly Dictionary<int, Action> _hotkeys = new();
    private IntPtr _hwnd;
    private int _idCounter = 9000;
    private bool _disposed;

    public void Initialize(Window window)
    {
        _hwnd = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
    }

    /// <summary>
    /// 注册全局热键，返回热键 ID（用于注销）
    /// </summary>
    public int Register(uint modifiers, uint vk, Action callback)
    {
        int id = _idCounter++;
        if (NativeMethods.RegisterHotKey(_hwnd, id, modifiers | NativeMethods.MOD_NOREPEAT, vk))
        {
            _hotkeys[id] = callback;
        }
        return id;
    }

    /// <summary>
    /// 注销指定 ID 的热键
    /// </summary>
    public void Unregister(int id)
    {
        if (_hotkeys.ContainsKey(id))
        {
            NativeMethods.UnregisterHotKey(_hwnd, id);
            _hotkeys.Remove(id);
        }
    }

    /// <summary>
    /// 注销所有热键
    /// </summary>
    public void UnregisterAll()
    {
        foreach (var id in _hotkeys.Keys.ToList())
        {
            NativeMethods.UnregisterHotKey(_hwnd, id);
        }
        _hotkeys.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && _hotkeys.TryGetValue(wParam.ToInt32(), out var action))
        {
            action?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UnregisterAll();
        _source?.RemoveHook(WndProc);
    }
}
