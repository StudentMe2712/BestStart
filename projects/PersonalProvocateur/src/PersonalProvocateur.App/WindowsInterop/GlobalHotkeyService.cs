using System.Windows.Input;
using System.Windows.Interop;

namespace PersonalProvocateur.App.WindowsInterop;

/// <summary>
/// Hotkey registration validation states.
/// </summary>
public enum HotkeyValidationState
{
    Valid,
    Empty,
    InvalidFormat,
    OccupiedByAnotherApp
}

/// <summary>
/// Manages global system-wide hotkeys (Trigger and Panic) using Win32 RegisterHotKey and WPF HwndSource.
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    public const int TriggerHotkeyId = 9001;
    public const int PanicHotkeyId = 9002;

    private readonly IntPtr _externalHandle;
    private HwndSource? _hwndSource;
    private IntPtr _hwnd;
    private bool _isDisposed;

    private Action? _triggerCallback;
    private Action? _panicCallback;
    private string? _currentTriggerHotkeyStr;
    private string? _currentPanicHotkeyStr;
    private bool _isTriggerRegistered;
    private bool _isPanicRegistered;
    private readonly object _lock = new();

    /// <summary>
    /// Gets the currently registered trigger hotkey string.
    /// </summary>
    public string? CurrentTriggerHotkey
    {
        get
        {
            lock (_lock)
            {
                return _currentTriggerHotkeyStr;
            }
        }
    }

    /// <summary>
    /// Gets the currently registered panic hotkey string.
    /// </summary>
    public string? CurrentPanicHotkey
    {
        get
        {
            lock (_lock)
            {
                return _currentPanicHotkeyStr;
            }
        }
    }

    /// <summary>
    /// Gets whether the trigger hotkey is currently registered with Windows.
    /// </summary>
    public bool IsTriggerRegistered
    {
        get
        {
            lock (_lock)
            {
                return _isTriggerRegistered;
            }
        }
    }

    /// <summary>
    /// Gets whether the panic hotkey is currently registered with Windows.
    /// </summary>
    public bool IsPanicRegistered
    {
        get
        {
            lock (_lock)
            {
                return _isPanicRegistered;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of <see cref="GlobalHotkeyService"/>.
    /// </summary>
    /// <param name="windowHandle">Optional window handle. If default/Zero, an invisible HwndSource is created lazily.</param>
    public GlobalHotkeyService(IntPtr windowHandle = default)
    {
        _externalHandle = windowHandle;
    }

    /// <summary>
    /// Parses a hotkey string into Win32 modifier flags and virtual key code.
    /// Supports modifiers (Ctrl, Alt, Shift, Win) and keys (A-Z, 0-9, F1-F24, Esc, Space, etc.).
    /// </summary>
    /// <param name="hotkeyStr">Hotkey string representation (e.g. "Ctrl+Alt+P", "Ctrl+Alt+Shift+P").</param>
    /// <param name="modifiers">Parsed Win32 modifier bitmask.</param>
    /// <param name="virtualKey">Parsed Win32 virtual key code.</param>
    /// <returns>True if parsing succeeded and a valid key was found; otherwise false.</returns>
    public static bool TryParseHotkey(string? hotkeyStr, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;

        if (string.IsNullOrWhiteSpace(hotkeyStr))
        {
            return false;
        }

        var parts = hotkeyStr.Split('+');
        if (parts.Length == 0)
        {
            return false;
        }

        bool hasMainKey = false;

        foreach (var rawPart in parts)
        {
            var part = rawPart.Trim();
            if (string.IsNullOrEmpty(part))
            {
                return false;
            }

            var lower = part.ToLowerInvariant();
            if (lower is "ctrl" or "control")
            {
                modifiers |= NativeMethods.MOD_CONTROL;
            }
            else if (lower is "alt" or "menu")
            {
                modifiers |= NativeMethods.MOD_ALT;
            }
            else if (lower is "shift")
            {
                modifiers |= NativeMethods.MOD_SHIFT;
            }
            else if (lower is "win" or "windows" or "super")
            {
                modifiers |= NativeMethods.MOD_WIN;
            }
            else
            {
                if (hasMainKey)
                {
                    // Cannot have multiple primary keys (e.g. "Ctrl+A+B")
                    modifiers = 0;
                    virtualKey = 0;
                    return false;
                }

                if (!TryParseKey(part, out virtualKey))
                {
                    modifiers = 0;
                    virtualKey = 0;
                    return false;
                }

                hasMainKey = true;
            }
        }

        if (!hasMainKey)
        {
            modifiers = 0;
            virtualKey = 0;
            return false;
        }

        return true;
    }

    private static bool TryParseKey(string part, out uint vk)
    {
        vk = 0;
        if (string.IsNullOrWhiteSpace(part))
        {
            return false;
        }

        if (part.Length == 1)
        {
            char c = char.ToUpperInvariant(part[0]);
            if (c is >= 'A' and <= 'Z')
            {
                vk = (uint)c;
                return true;
            }
            if (c is >= '0' and <= '9')
            {
                vk = (uint)c;
                return true;
            }
        }

        var lower = part.ToLowerInvariant();

        // Function keys F1-F24
        if (lower.Length >= 2 && lower[0] == 'f' && int.TryParse(lower.AsSpan(1), out int fNum))
        {
            if (fNum is >= 1 and <= 24)
            {
                vk = (uint)(0x70 + (fNum - 1));
                return true;
            }
        }

        switch (lower)
        {
            case "esc":
            case "escape":
                vk = 0x1B;
                return true;
            case "space":
            case "spacebar":
                vk = 0x20;
                return true;
            case "tab":
                vk = 0x09;
                return true;
            case "enter":
            case "return":
                vk = 0x0D;
                return true;
            case "backspace":
            case "back":
                vk = 0x08;
                return true;
            case "delete":
            case "del":
                vk = 0x2E;
                return true;
            case "insert":
            case "ins":
                vk = 0x2D;
                return true;
            case "home":
                vk = 0x24;
                return true;
            case "end":
                vk = 0x23;
                return true;
            case "pageup":
            case "pgup":
                vk = 0x21;
                return true;
            case "pagedown":
            case "pgdn":
            case "pgdown":
                vk = 0x22;
                return true;
            case "up":
                vk = 0x26;
                return true;
            case "down":
                vk = 0x28;
                return true;
            case "left":
                vk = 0x25;
                return true;
            case "right":
                vk = 0x27;
                return true;
            case "printscreen":
            case "prtsc":
            case "snapshot":
                vk = 0x2C;
                return true;
            case "pause":
            case "break":
                vk = 0x13;
                return true;
            case "capslock":
            case "capital":
                vk = 0x14;
                return true;
            case "plus":
            case "add":
                vk = 0xBB;
                return true;
            case "minus":
            case "subtract":
                vk = 0xBD;
                return true;
        }

        if (Enum.TryParse<Key>(part, true, out var wpfKey) && wpfKey != Key.None)
        {
            int keyInterop = KeyInterop.VirtualKeyFromKey(wpfKey);
            if (keyInterop > 0)
            {
                vk = (uint)keyInterop;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Registers the global trigger hotkey.
    /// </summary>
    /// <param name="hotkeyStr">Hotkey string representation.</param>
    /// <param name="callback">Action invoked when hotkey is pressed.</param>
    /// <returns>True if registered successfully; false if invalid or occupied by another application.</returns>
    public bool RegisterTriggerHotkey(string hotkeyStr, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (_isDisposed)
        {
            return false;
        }

        if (!TryParseHotkey(hotkeyStr, out uint modifiers, out uint virtualKey))
        {
            return false;
        }

        lock (_lock)
        {
            IntPtr hwnd = EnsureHandle();
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            if (_isTriggerRegistered)
            {
                NativeMethods.UnregisterHotKey(hwnd, TriggerHotkeyId);
                _isTriggerRegistered = false;
            }

            bool registered = NativeMethods.RegisterHotKey(hwnd, TriggerHotkeyId, modifiers | NativeMethods.MOD_NOREPEAT, virtualKey);
            if (!registered)
            {
                registered = NativeMethods.RegisterHotKey(hwnd, TriggerHotkeyId, modifiers, virtualKey);
            }

            if (registered)
            {
                _triggerCallback = callback;
                _currentTriggerHotkeyStr = hotkeyStr;
                _isTriggerRegistered = true;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Registers the global panic hotkey for emergency dismissal.
    /// </summary>
    /// <param name="hotkeyStr">Hotkey string representation.</param>
    /// <param name="callback">Action invoked when hotkey is pressed.</param>
    /// <returns>True if registered successfully; false if invalid or occupied by another application.</returns>
    public bool RegisterPanicHotkey(string hotkeyStr, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (_isDisposed)
        {
            return false;
        }

        if (!TryParseHotkey(hotkeyStr, out uint modifiers, out uint virtualKey))
        {
            return false;
        }

        lock (_lock)
        {
            IntPtr hwnd = EnsureHandle();
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            if (_isPanicRegistered)
            {
                NativeMethods.UnregisterHotKey(hwnd, PanicHotkeyId);
                _isPanicRegistered = false;
            }

            bool registered = NativeMethods.RegisterHotKey(hwnd, PanicHotkeyId, modifiers | NativeMethods.MOD_NOREPEAT, virtualKey);
            if (!registered)
            {
                registered = NativeMethods.RegisterHotKey(hwnd, PanicHotkeyId, modifiers, virtualKey);
            }

            if (registered)
            {
                _panicCallback = callback;
                _currentPanicHotkeyStr = hotkeyStr;
                _isPanicRegistered = true;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Unregisters all registered global hotkeys.
    /// </summary>
    public void UnregisterAll()
    {
        lock (_lock)
        {
            if (_hwnd != IntPtr.Zero)
            {
                if (_isTriggerRegistered)
                {
                    NativeMethods.UnregisterHotKey(_hwnd, TriggerHotkeyId);
                    _isTriggerRegistered = false;
                }

                if (_isPanicRegistered)
                {
                    NativeMethods.UnregisterHotKey(_hwnd, PanicHotkeyId);
                    _isPanicRegistered = false;
                }
            }

            _triggerCallback = null;
            _panicCallback = null;
            _currentTriggerHotkeyStr = null;
            _currentPanicHotkeyStr = null;
        }
    }

    /// <summary>
    /// Checks the validation and registration availability of a candidate hotkey string.
    /// </summary>
    /// <param name="hotkeyStr">Candidate hotkey string (e.g. "Ctrl+Alt+P").</param>
    /// <param name="currentActiveHotkey">Optional currently registered hotkey to treat as valid.</param>
    /// <returns>Validation state of the hotkey.</returns>
    public static HotkeyValidationState CheckHotkeyStatus(string? hotkeyStr, string? currentActiveHotkey = null)
    {
        if (string.IsNullOrWhiteSpace(hotkeyStr))
        {
            return HotkeyValidationState.Empty;
        }

        if (!TryParseHotkey(hotkeyStr, out uint modifiers, out uint virtualKey))
        {
            return HotkeyValidationState.InvalidFormat;
        }

        if (!string.IsNullOrWhiteSpace(currentActiveHotkey) &&
            string.Equals(hotkeyStr.Trim(), currentActiveHotkey.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return HotkeyValidationState.Valid;
        }

        try
        {
            var parameters = new HwndSourceParameters("PP_HotkeyTester")
            {
                WindowStyle = 0,
                Width = 0,
                Height = 0,
                PositionX = 0,
                PositionY = 0
            };
            using var tempSource = new HwndSource(parameters);
            IntPtr hwnd = tempSource.Handle;
            const int testId = 9999;

            bool success = NativeMethods.RegisterHotKey(hwnd, testId, modifiers | NativeMethods.MOD_NOREPEAT, virtualKey);
            if (!success)
            {
                success = NativeMethods.RegisterHotKey(hwnd, testId, modifiers, virtualKey);
            }

            if (success)
            {
                NativeMethods.UnregisterHotKey(hwnd, testId);
                return HotkeyValidationState.Valid;
            }

            return HotkeyValidationState.OccupiedByAnotherApp;
        }
        catch
        {
            return HotkeyValidationState.Valid;
        }
    }

    private IntPtr EnsureHandle()
    {
        if (_hwnd != IntPtr.Zero)
        {
            return _hwnd;
        }

        if (_externalHandle != IntPtr.Zero)
        {
            _hwnd = _externalHandle;
            try
            {
                var source = HwndSource.FromHwnd(_externalHandle);
                source?.AddHook(HwndHook);
            }
            catch
            {
                // Non-WPF window handle fallback
            }
            return _hwnd;
        }

        try
        {
            var parameters = new HwndSourceParameters("PersonalProvocateur_HotkeyListener")
            {
                WindowStyle = 0,
                Width = 0,
                Height = 0,
                PositionX = 0,
                PositionY = 0,
            };
            _hwndSource = new HwndSource(parameters);
            _hwndSource.AddHook(HwndHook);
            _hwnd = _hwndSource.Handle;
            return _hwnd;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            Action? callback = null;

            lock (_lock)
            {
                if (id == TriggerHotkeyId)
                {
                    callback = _triggerCallback;
                }
                else if (id == PanicHotkeyId)
                {
                    callback = _panicCallback;
                }
            }

            if (callback != null)
            {
                handled = true;
                try
                {
                    callback();
                }
                catch
                {
                    // Suppress callback exceptions
                }
            }
        }

        return IntPtr.Zero;
    }

    internal void SimulateHotkey(int id)
    {
        bool handled = false;
        HwndHook(IntPtr.Zero, NativeMethods.WM_HOTKEY, (IntPtr)id, IntPtr.Zero, ref handled);
    }

    /// <summary>
    /// Unregisters hotkeys and cleans up the HwndSource.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        UnregisterAll();

        if (_hwndSource != null)
        {
            try
            {
                _hwndSource.RemoveHook(HwndHook);
                _hwndSource.Dispose();
            }
            catch
            {
                // Ignore cleanup error
            }

            _hwndSource = null;
        }
    }
}
