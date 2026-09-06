using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Stepwise.Core.Interfaces;

namespace Stepwise.WindowsIntegration.Hotkeys;

/// <summary>
/// Реализация <see cref="IGlobalHotkeyService"/> на базе Win32 RegisterHotKey и Window Subclassing.
/// Обеспечивает глобальное переключение записи по нажатию Ctrl+Shift+R или F9
/// из любого приложения в системе без использования агрессивных хуков.
/// </summary>
public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_NOREPEAT = 0x4000;
    private const uint VK_R = 0x52;
    private const uint VK_F9 = 0x78;

    private const int HOTKEY_ID_CTRL_SHIFT_R = 9001;
    private const int HOTKEY_ID_F9 = 9002;
    private const nuint SUBCLASS_ID = 4242;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hWnd);

    private delegate nint SubclassWndProc(nint hWnd, uint uMsg, nuint wParam, nint lParam, nuint uIdSubclass, nuint dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(nint hWnd, SubclassWndProc pfnSubclass, nuint uIdSubclass, nuint dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(nint hWnd, SubclassWndProc pfnSubclass, nuint uIdSubclass);

    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(nint hWnd, uint uMsg, nuint wParam, nint lParam);

    private readonly object _syncLock = new();
    private readonly SubclassWndProc _subclassProc;
    private nint _subclassedHwnd = nint.Zero;
    private bool _isRegistered;
    private bool _isDisposed;

    public event EventHandler? RecordingHotkeyPressed;

    public GlobalHotkeyService()
    {
        _subclassProc = SubclassCallback;
    }

    public void Register(nint windowHandle)
    {
        lock (_syncLock)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            if (windowHandle == nint.Zero || !IsWindow(windowHandle))
            {
                Debug.WriteLine("[GlobalHotkeyService] Invalid HWND provided for hotkey registration.");
                return;
            }

            if (_isRegistered)
            {
                UnregisterInternal();
            }

            _subclassedHwnd = windowHandle;

            // 1. Устанавливаем безопасный Subclass для обработки WM_HOTKEY
            bool subclassOk = SetWindowSubclass(windowHandle, _subclassProc, SUBCLASS_ID, 0);
            if (!subclassOk)
            {
                Debug.WriteLine("[GlobalHotkeyService] Failed to set window subclass for WM_HOTKEY.");
            }

            // 2. Регистрируем Ctrl + Shift + R
            bool regR = RegisterHotKey(windowHandle, HOTKEY_ID_CTRL_SHIFT_R, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, VK_R);
            if (!regR)
            {
                Debug.WriteLine($"[GlobalHotkeyService] Warning: Failed to register Ctrl+Shift+R (Error: {Marshal.GetLastWin32Error()}).");
            }

            // 3. Регистрируем F9
            bool regF9 = RegisterHotKey(windowHandle, HOTKEY_ID_F9, MOD_NOREPEAT, VK_F9);
            if (!regF9)
            {
                Debug.WriteLine($"[GlobalHotkeyService] Warning: Failed to register F9 (Error: {Marshal.GetLastWin32Error()}).");
            }

            _isRegistered = true;
            Debug.WriteLine($"[GlobalHotkeyService] Hotkeys registered on HWND 0x{windowHandle:X}: Ctrl+Shift+R={regR}, F9={regF9}");
        }
    }

    public void Unregister()
    {
        lock (_syncLock)
        {
            UnregisterInternal();
        }
    }

    private void UnregisterInternal()
    {
        if (_subclassedHwnd != nint.Zero && IsWindow(_subclassedHwnd))
        {
            try
            {
                UnregisterHotKey(_subclassedHwnd, HOTKEY_ID_CTRL_SHIFT_R);
                UnregisterHotKey(_subclassedHwnd, HOTKEY_ID_F9);
                RemoveWindowSubclass(_subclassedHwnd, _subclassProc, SUBCLASS_ID);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GlobalHotkeyService] Error unregistering hotkeys: {ex.Message}");
            }
        }

        _subclassedHwnd = nint.Zero;
        _isRegistered = false;
    }

    private nint SubclassCallback(nint hWnd, uint uMsg, nuint wParam, nint lParam, nuint uIdSubclass, nuint dwRefData)
    {
        if (uMsg == WM_HOTKEY)
        {
            int hotkeyId = (int)wParam;
            if (hotkeyId is HOTKEY_ID_CTRL_SHIFT_R or HOTKEY_ID_F9)
            {
                Debug.WriteLine($"[GlobalHotkeyService] Global recording hotkey triggered! ID={hotkeyId}");
                try
                {
                    RecordingHotkeyPressed?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GlobalHotkeyService] Error in RecordingHotkeyPressed handler: {ex.Message}");
                }

                return nint.Zero; // Сообщение обработано
            }
        }

        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    public void Dispose()
    {
        lock (_syncLock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            UnregisterInternal();
        }
    }
}
