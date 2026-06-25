using System.Windows.Interop;

namespace SelectCast.App.Interop;

/// <summary>
/// Registers a single global hotkey on an existing window handle and raises
/// <see cref="Pressed"/> when WM_HOTKEY arrives.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int Id = 0x5343; // 'SC'

    private readonly nint _hwnd;
    private readonly HwndSource _source;
    private bool _registered;
    private bool _disposed;

    public event Action? Pressed;

    public HotkeyService(nint hwnd)
    {
        _hwnd = hwnd;
        _source = HwndSource.FromHwnd(hwnd)
            ?? throw new InvalidOperationException("No HwndSource for the given window handle.");
        _source.AddHook(WndProc);
    }

    public bool Register(uint modifiers, uint vk)
    {
        if (_registered)
            return true;

        _registered = NativeMethods.RegisterHotKey(_hwnd, Id, modifiers, vk);
        return _registered;
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == Id)
        {
            handled = true;
            Pressed?.Invoke();
        }

        return nint.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_registered)
        {
            NativeMethods.UnregisterHotKey(_hwnd, Id);
            _registered = false;
        }

        try
        {
            _source.RemoveHook(WndProc);
        }
        catch
        {
            // Window/source already torn down — nothing to detach.
        }
    }
}
