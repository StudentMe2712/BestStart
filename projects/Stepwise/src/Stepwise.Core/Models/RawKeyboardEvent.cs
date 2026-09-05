namespace Stepwise.Core.Models;

/// <summary>
/// Тип события сырого ввода клавиатуры.
/// </summary>
public enum RawKeyboardEventType
{
    KeyDown,
    KeyUp
}

/// <summary>
/// Флаги клавиш-модификаторов.
/// </summary>
[Flags]
public enum KeyboardModifiers
{
    None = 0,
    Shift = 1 << 0,
    Control = 1 << 1,
    Alt = 1 << 2,
    Windows = 1 << 3,
    AltGr = 1 << 4
}

/// <summary>
/// Необработанное событие ввода клавиатуры, полученное от низкоуровневого хука Windows.
/// </summary>
public readonly record struct RawKeyboardEvent(
    RawKeyboardEventType EventType,
    int VirtualKey,
    int ScanCode,
    KeyboardModifiers Modifiers,
    string? Character,
    bool IsDeadKey,
    bool IsExtendedKey,
    DateTime Timestamp
)
{
    public bool IsKeyDown => EventType == RawKeyboardEventType.KeyDown;
    public bool IsKeyUp => EventType == RawKeyboardEventType.KeyUp;
    public bool HasModifier(KeyboardModifiers modifier) => (Modifiers & modifier) == modifier;
    public bool HasShift => (Modifiers & KeyboardModifiers.Shift) != 0;
    public bool HasControl => (Modifiers & KeyboardModifiers.Control) != 0;
    public bool HasAlt => (Modifiers & KeyboardModifiers.Alt) != 0;
    public bool HasWindows => (Modifiers & KeyboardModifiers.Windows) != 0;

    /// <summary>
    /// Флаг AltGr указывает на нажатие правого Alt (AltGr) для ввода специальных символов.
    /// </summary>
    public bool IsAltGr => HasModifier(KeyboardModifiers.AltGr);

    /// <summary>
    /// Является ли событие клавиатурным сочетанием (хоткеем), а не простым вводом символа.
    /// </summary>
    public bool IsShortcut => (HasControl || HasAlt || HasWindows) && !IsAltGr;

    /// <summary>
    /// Является ли событие вводом печатного текста (нажатие KeyDown, не шорткат, не дедкей, присутствует символ).
    /// </summary>
    public bool IsTextInput => IsKeyDown && !string.IsNullOrEmpty(Character) && !IsDeadKey && !IsShortcut && Character[0] >= 32;
}
