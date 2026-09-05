namespace Stepwise.Core.Models;

/// <summary>
/// Элемент очереди необработанного пользовательского ввода для неблокирующей конвейерной обработки.
/// </summary>
public sealed record RawInputQueueItem
{
    /// <summary>
    /// Низкоуровневое событие мыши (если элемент представляет ввод мыши).
    /// </summary>
    public RawMouseEvent? MouseEvent { get; init; }

    /// <summary>
    /// Низкоуровневое событие клавиатуры (если элемент представляет ввод клавиатуры).
    /// </summary>
    public RawKeyboardEvent? KeyboardEvent { get; init; }

    /// <summary>
    /// Контекст активного окна на момент возникновения события ввода.
    /// </summary>
    public WindowContext Context { get; init; } = WindowContext.Empty;

    /// <summary>
    /// Признак события мыши.
    /// </summary>
    public bool IsMouse => MouseEvent != null;

    /// <summary>
    /// Признак события клавиатуры.
    /// </summary>
    public bool IsKeyboard => KeyboardEvent != null;

    /// <summary>
    /// Создает элемент очереди для события мыши.
    /// </summary>
    public static RawInputQueueItem Mouse(RawMouseEvent mouseEvent, WindowContext? context = null) =>
        new() { MouseEvent = mouseEvent, Context = context ?? WindowContext.Empty };

    /// <summary>
    /// Создает элемент очереди для события клавиатуры.
    /// </summary>
    public static RawInputQueueItem Keyboard(RawKeyboardEvent keyboardEvent, WindowContext? context = null) =>
        new() { KeyboardEvent = keyboardEvent, Context = context ?? WindowContext.Empty };
}
