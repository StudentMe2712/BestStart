namespace Stepwise.Core.Models;

/// <summary>
/// Тип события сырого ввода мыши.
/// </summary>
public enum RawMouseEventType
{
    MouseDown,
    MouseUp,
    Move,
    Wheel
}

/// <summary>
/// Кнопка мыши для событий сырого ввода.
/// </summary>
public enum RawMouseButton
{
    None,
    Left,
    Right,
    Middle,
    XButton1,
    XButton2
}

/// <summary>
/// Необработанное событие ввода мыши, полученное от низкоуровневого хука Windows.
/// </summary>
public readonly record struct RawMouseEvent(
    RawMouseEventType EventType,
    RawMouseButton Button,
    int X,
    int Y,
    int Delta,
    DateTime Timestamp
);
