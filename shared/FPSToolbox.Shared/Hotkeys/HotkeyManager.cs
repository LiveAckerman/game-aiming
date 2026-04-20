using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using FPSToolbox.Shared.Native;

namespace FPSToolbox.Shared.Hotkeys;

/// <summary>
/// 全局热键管理器（基于 RegisterHotKey）。三个 exe 共用。
/// </summary>
public sealed class HotkeyManager : IDisposable
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

    public int Register(uint modifiers, uint vk, Action callback)
    {
        int id = _idCounter++;
        if (NativeMethods.RegisterHotKey(_hwnd, id, modifiers | NativeMethods.MOD_NOREPEAT, vk))
            _hotkeys[id] = callback;
        return id;
    }

    public void Unregister(int id)
    {
        if (_hotkeys.Remove(id)) NativeMethods.UnregisterHotKey(_hwnd, id);
    }

    public void UnregisterAll()
    {
        foreach (var id in _hotkeys.Keys.ToList())
            NativeMethods.UnregisterHotKey(_hwnd, id);
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

    /// <summary>
    /// 解析 "Ctrl+Shift+F8" 之类的字符串为 Win32 modifiers 和 VK。
    /// </summary>
    public static bool TryParseHotkey(string hotkey, out uint modifiers, out uint vk)
    {
        modifiers = NativeMethods.MOD_NONE;
        vk = 0;
        if (string.IsNullOrWhiteSpace(hotkey)) return false;

        var parts = hotkey.Split('+');
        var keyPart = parts[^1].Trim();

        foreach (var part in parts[..^1])
        {
            switch (part.Trim().ToLowerInvariant())
            {
                case "ctrl": modifiers |= NativeMethods.MOD_CONTROL; break;
                case "alt": modifiers |= NativeMethods.MOD_ALT; break;
                case "shift": modifiers |= NativeMethods.MOD_SHIFT; break;
                case "win": modifiers |= NativeMethods.MOD_WIN; break;
            }
        }

        if (Enum.TryParse<Key>(keyPart, true, out var key))
        {
            vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            return vk != 0;
        }
        return false;
    }
}
