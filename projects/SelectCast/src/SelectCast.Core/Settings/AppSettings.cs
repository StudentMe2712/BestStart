namespace SelectCast.Core.Settings;

/// <summary>
/// User-configurable settings, persisted as JSON by <see cref="SettingsService"/>.
/// Hotkey is stored as a Win32 modifier mask + virtual-key code so it round-trips losslessly.
/// </summary>
public sealed record AppSettings
{
    // Win32 fsModifiers bits: MOD_ALT=0x1, MOD_CONTROL=0x2, MOD_SHIFT=0x4, MOD_WIN=0x8.
    /// <summary>Hotkey modifier mask. Default Ctrl+Alt.</summary>
    public uint HotkeyModifiers { get; init; } = 0x0002 | 0x0001;

    /// <summary>Hotkey virtual-key code. Default 'C' (0x43).</summary>
    public uint HotkeyVk { get; init; } = 0x43;

    /// <summary>Launch on Windows sign-in.</summary>
    public bool Autostart { get; init; } = true;
}
