namespace Stepwise.Core.Models;

/// <summary>
/// Событие клика мыши, полученное от низкоуровневого хука.
/// </summary>
public readonly record struct MouseClickEvent(
    int X,
    int Y,
    ActionType Action,
    DateTime Timestamp
);
