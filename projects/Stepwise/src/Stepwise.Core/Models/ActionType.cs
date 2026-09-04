namespace Stepwise.Core.Models;

/// <summary>
/// Тип действия пользователя, зафиксированного в процессе записи.
/// </summary>
public enum ActionType
{
    LeftClick,
    RightClick,
    DoubleLeftClick,
    MiddleClick,
    DragAndDrop,
    KeyPress,
    TextInput
}
