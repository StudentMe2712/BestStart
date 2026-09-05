namespace Stepwise.Core.Models;

/// <summary>
/// Тип семантического высокоуровневого действия пользователя.
/// </summary>
public enum SemanticActionType
{
    LeftClick,
    RightClick,
    DoubleLeftClick,
    MiddleClick,
    TextInput,
    KeyPress,
    Shortcut,
    DragAndDrop,
    Scroll,
    MouseDown,
    MouseUp,
    WindowActivated,
    WindowClosed,
    ManualStep
}

/// <summary>
/// Контекст окна, в котором произошло семантическое действие.
/// </summary>
public sealed record WindowContext(
    long WindowHandle,
    int ProcessId,
    string ProcessName,
    string WindowTitle,
    BoundingBox Bounds,
    DateTime Timestamp
)
{
    /// <summary>
    /// Пустой контекст окна.
    /// </summary>
    public static WindowContext Empty => new(
        WindowHandle: 0,
        ProcessId: 0,
        ProcessName: string.Empty,
        WindowTitle: string.Empty,
        Bounds: BoundingBox.Empty,
        Timestamp: DateTime.MinValue
    );

    /// <summary>
    /// Создает <see cref="WindowContext"/> на основе <see cref="ActiveWindowInfo"/>.
    /// </summary>
    public static WindowContext FromActiveWindowInfo(ActiveWindowInfo windowInfo) => new(
        WindowHandle: windowInfo.WindowHandle,
        ProcessId: windowInfo.ProcessId,
        ProcessName: windowInfo.ProcessName,
        WindowTitle: windowInfo.WindowTitle,
        Bounds: windowInfo.Bounds,
        Timestamp: windowInfo.Timestamp
    );
}

/// <summary>
/// Семантическое действие пользователя, скоррелированное из одного или нескольких низкоуровневых событий ввода.
/// </summary>
public sealed record SemanticAction(
    Guid Id,
    int SequenceIndex,
    SemanticActionType ActionType,
    DateTime Timestamp,
    WindowContext Context,
    int? X = null,
    int? Y = null,
    string? Text = null,
    int? VirtualKey = null,
    string? KeyName = null,
    KeyboardModifiers? Modifiers = null,
    int CharacterCount = 0,
    DateTime StartedAt = default,
    DateTime CompletedAt = default,
    bool IsSensitive = false,
    int? EndX = null,
    int? EndY = null,
    int? Delta = null
)
{
    /// <summary>
    /// Фабричный метод для создания действия клика мыши.
    /// </summary>
    public static SemanticAction CreateMouseClick(
        SemanticActionType actionType,
        int x,
        int y,
        WindowContext context,
        DateTime timestamp,
        int sequenceIndex = 0)
    {
        return new SemanticAction(
            Id: Guid.NewGuid(),
            SequenceIndex: sequenceIndex,
            ActionType: actionType,
            Timestamp: timestamp,
            Context: context,
            X: x,
            Y: y,
            StartedAt: timestamp,
            CompletedAt: timestamp
        );
    }

    /// <summary>
    /// Фабричный метод для создания действия текстового ввода (последовательность нажатий клавиш).
    /// </summary>
    public static SemanticAction CreateTextInput(
        string text,
        WindowContext context,
        DateTime startedAt,
        DateTime completedAt,
        bool isSensitive = false,
        int sequenceIndex = 0)
    {
        return new SemanticAction(
            Id: Guid.NewGuid(),
            SequenceIndex: sequenceIndex,
            ActionType: SemanticActionType.TextInput,
            Timestamp: completedAt,
            Context: context,
            Text: text,
            CharacterCount: text.Length,
            StartedAt: startedAt,
            CompletedAt: completedAt,
            IsSensitive: isSensitive
        );
    }

    /// <summary>
    /// Фабричный метод для создания действия нажатия одиночной клавиши.
    /// </summary>
    public static SemanticAction CreateKeyPress(
        int virtualKey,
        string? keyName,
        KeyboardModifiers modifiers,
        WindowContext context,
        DateTime timestamp,
        int sequenceIndex = 0)
    {
        return new SemanticAction(
            Id: Guid.NewGuid(),
            SequenceIndex: sequenceIndex,
            ActionType: SemanticActionType.KeyPress,
            Timestamp: timestamp,
            Context: context,
            VirtualKey: virtualKey,
            KeyName: keyName,
            Modifiers: modifiers,
            StartedAt: timestamp,
            CompletedAt: timestamp
        );
    }

    /// <summary>
    /// Фабричный метод для создания действия клавиатурного сочетания (хоткея).
    /// </summary>
    public static SemanticAction CreateShortcut(
        int virtualKey,
        string? keyName,
        KeyboardModifiers modifiers,
        WindowContext context,
        DateTime timestamp,
        int sequenceIndex = 0)
    {
        return new SemanticAction(
            Id: Guid.NewGuid(),
            SequenceIndex: sequenceIndex,
            ActionType: SemanticActionType.Shortcut,
            Timestamp: timestamp,
            Context: context,
            VirtualKey: virtualKey,
            KeyName: keyName,
            Modifiers: modifiers,
            StartedAt: timestamp,
            CompletedAt: timestamp
        );
    }

    /// <summary>
    /// Фабричный метод для создания действия перетаскивания (Drag and Drop).
    /// </summary>
    public static SemanticAction CreateDragAndDrop(
        int startX,
        int startY,
        int endX,
        int endY,
        RawMouseButton button,
        WindowContext context,
        DateTime timestamp,
        int sequenceIndex = 0)
    {
        return new SemanticAction(
            Id: Guid.NewGuid(),
            SequenceIndex: sequenceIndex,
            ActionType: SemanticActionType.DragAndDrop,
            Timestamp: timestamp,
            Context: context,
            X: startX,
            Y: startY,
            EndX: endX,
            EndY: endY,
            StartedAt: timestamp,
            CompletedAt: timestamp
        );
    }

    /// <summary>
    /// Фабричный метод для создания действия прокрутки колесика мыши (Scroll).
    /// </summary>
    public static SemanticAction CreateScroll(
        int x,
        int y,
        int delta,
        WindowContext context,
        DateTime timestamp,
        int sequenceIndex = 0)
    {
        return new SemanticAction(
            Id: Guid.NewGuid(),
            SequenceIndex: sequenceIndex,
            ActionType: SemanticActionType.Scroll,
            Timestamp: timestamp,
            Context: context,
            X: x,
            Y: y,
            Delta: delta,
            StartedAt: timestamp,
            CompletedAt: timestamp
        );
    }

    /// <summary>
    /// Фабричный метод для создания ручного шага инструкции.
    /// </summary>
    public static SemanticAction CreateManualStep(
        WindowContext context,
        DateTime timestamp,
        int sequenceIndex = 0)
    {
        return new SemanticAction(
            Id: Guid.NewGuid(),
            SequenceIndex: sequenceIndex,
            ActionType: SemanticActionType.ManualStep,
            Timestamp: timestamp,
            Context: context,
            StartedAt: timestamp,
            CompletedAt: timestamp
        );
    }

    /// <summary>
    /// Фабричный метод для создания действия активации (переключения на) окна.
    /// </summary>
    public static SemanticAction CreateWindowActivated(
        WindowContext context,
        DateTime timestamp,
        int sequenceIndex = 0)
    {
        return new SemanticAction(
            Id: Guid.NewGuid(),
            SequenceIndex: sequenceIndex,
            ActionType: SemanticActionType.WindowActivated,
            Timestamp: timestamp,
            Context: context,
            StartedAt: timestamp,
            CompletedAt: timestamp
        );
    }

    /// <summary>
    /// Фабричный метод для создания действия закрытия окна.
    /// </summary>
    public static SemanticAction CreateWindowClosed(
        WindowContext context,
        DateTime timestamp,
        int sequenceIndex = 0)
    {
        return new SemanticAction(
            Id: Guid.NewGuid(),
            SequenceIndex: sequenceIndex,
            ActionType: SemanticActionType.WindowClosed,
            Timestamp: timestamp,
            Context: context,
            StartedAt: timestamp,
            CompletedAt: timestamp
        );
    }

    /// <summary>
    /// Определяет, является ли действие кликом мыши или манипуляцией мышью.
    /// </summary>
    public bool IsMouseAction => ActionType is SemanticActionType.LeftClick
        or SemanticActionType.RightClick
        or SemanticActionType.DoubleLeftClick
        or SemanticActionType.MiddleClick
        or SemanticActionType.DragAndDrop
        or SemanticActionType.Scroll
        or SemanticActionType.MouseDown
        or SemanticActionType.MouseUp;

    /// <summary>
    /// Определяет, является ли действие клавиатурным событием.
    /// </summary>
    public bool IsKeyboardAction => ActionType is SemanticActionType.TextInput
        or SemanticActionType.KeyPress
        or SemanticActionType.Shortcut;

    /// <summary>
    /// Преобразует <see cref="SemanticActionType"/> в стандартный <see cref="Stepwise.Core.Models.ActionType"/>.
    /// </summary>
    public Stepwise.Core.Models.ActionType ToStepActionType() => ActionType switch
    {
        SemanticActionType.LeftClick => Stepwise.Core.Models.ActionType.LeftClick,
        SemanticActionType.RightClick => Stepwise.Core.Models.ActionType.RightClick,
        SemanticActionType.DoubleLeftClick => Stepwise.Core.Models.ActionType.DoubleLeftClick,
        SemanticActionType.MiddleClick => Stepwise.Core.Models.ActionType.MiddleClick,
        SemanticActionType.TextInput => Stepwise.Core.Models.ActionType.TextInput,
        SemanticActionType.KeyPress => Stepwise.Core.Models.ActionType.KeyPress,
        SemanticActionType.Shortcut => Stepwise.Core.Models.ActionType.KeyPress,
        SemanticActionType.DragAndDrop => Stepwise.Core.Models.ActionType.DragAndDrop,
        SemanticActionType.Scroll => Stepwise.Core.Models.ActionType.Scroll,
        SemanticActionType.MouseDown => Stepwise.Core.Models.ActionType.MouseDown,
        SemanticActionType.MouseUp => Stepwise.Core.Models.ActionType.MouseUp,
        SemanticActionType.WindowActivated => Stepwise.Core.Models.ActionType.WindowActivated,
        SemanticActionType.WindowClosed => Stepwise.Core.Models.ActionType.WindowClosed,
        SemanticActionType.ManualStep => Stepwise.Core.Models.ActionType.ManualStep,
        _ => Stepwise.Core.Models.ActionType.LeftClick
    };
}
