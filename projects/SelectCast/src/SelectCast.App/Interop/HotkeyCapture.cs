using System.Windows.Input;

namespace SelectCast.App.Interop;

/// <summary>Maps a WPF key event to a Win32 hotkey (modifiers, vk) and formats one as display text.</summary>
internal static class HotkeyCapture
{
    /// <summary>
    /// Converts the pressed combination to (modifiers, vk), or null if it isn't a valid global
    /// hotkey yet (a modifier alone, or no modifier — global hotkeys require at least one).
    /// </summary>
    public static (uint Modifiers, uint Vk)? FromKeyEvent(KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (IsModifierKey(key))
            return null;

        uint modifiers = 0;
        ModifierKeys mods = Keyboard.Modifiers;
        if (mods.HasFlag(ModifierKeys.Control)) modifiers |= NativeMethods.MOD_CONTROL;
        if (mods.HasFlag(ModifierKeys.Alt)) modifiers |= NativeMethods.MOD_ALT;
        if (mods.HasFlag(ModifierKeys.Shift)) modifiers |= NativeMethods.MOD_SHIFT;
        if (mods.HasFlag(ModifierKeys.Windows)) modifiers |= NativeMethods.MOD_WIN;

        if (modifiers == 0)
            return null;

        int vk = KeyInterop.VirtualKeyFromKey(key);
        return vk == 0 ? null : (modifiers, (uint)vk);
    }

    public static string Format(uint modifiers, uint vk)
    {
        var parts = new List<string>(4);
        if ((modifiers & NativeMethods.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((modifiers & NativeMethods.MOD_ALT) != 0) parts.Add("Alt");
        if ((modifiers & NativeMethods.MOD_SHIFT) != 0) parts.Add("Shift");
        if ((modifiers & NativeMethods.MOD_WIN) != 0) parts.Add("Win");
        parts.Add(KeyInterop.KeyFromVirtualKey((int)vk).ToString());
        return string.Join("+", parts);
    }

    private static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;
}
