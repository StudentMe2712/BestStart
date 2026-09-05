using System.Diagnostics;
using System.Text;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;

namespace Stepwise.Core.Engine;

/// <summary>
/// Коррелятор низкоуровневых событий мыши и клавиатуры в высокоуровневые семантические действия (<see cref="SemanticAction"/>).
/// Реализует чистую доменную логику (.NET 9):
/// - распознавание кликов (Left, Right, Middle),
/// - сопоставление порогов двойного клика через <see cref="ISystemMetricsProvider"/>,
/// - фильтрацию дрожания курсора и защиту от ложных кликов при перетаскивании (drag tolerance),
/// - группировку последовательного текстового ввода с тайм-аутом неактивности (600 мс),
/// - обработку шорткатов, навигационных клавиш (Enter, Escape, Tab, Backspace, Delete, стрелки),
/// - автоматический сброс накопленного текста перед кликом мыши, шорткатом или специальной клавишей,
/// - потокобезопасную монотонную нумерацию SequenceIndex.
/// </summary>
public sealed class EventCorrelator : IEventCorrelator
{
    public const int DefaultTextFlushTimeoutMs = 600;

    private readonly ISystemMetricsProvider _metricsProvider;
    private readonly int _flushTimeoutMs;
    private readonly object _syncLock = new();
    private readonly StringBuilder _textBuffer = new();
    private readonly Timer _textFlushTimer;

    private int _sequenceIndex;
    private bool _isDisposed;

    // Кэш последнего активного контекста окна
    private WindowContext _cachedContext = WindowContext.Empty;

    // Состояние мыши для распознавания кликов
    private bool _isMouseDown;
    private RawMouseButton _mouseDownButton = RawMouseButton.None;
    private int _mouseDownX;
    private int _mouseDownY;
    private DateTime _mouseDownTimestamp;

    // Кандидат на двойной клик (время и координаты первого левого клика)
    private (DateTime Timestamp, int X, int Y)? _lastLeftClick;

    // Метаданные буфера текстового ввода
    private DateTime _textStartedAt;
    private DateTime _textCompletedAt;
    private int _textCharacterCount;

    /// <summary>
    /// Событие формирования скоррелированного семантического действия.
    /// </summary>
    public event EventHandler<SemanticAction>? ActionCorrelated;

    /// <summary>
    /// Создает экземпляр <see cref="EventCorrelator"/>.
    /// </summary>
    /// <param name="metricsProvider">Провайдер системных метрик (по умолчанию <see cref="DefaultSystemMetricsProvider"/>).</param>
    /// <param name="flushTimeoutMs">Таймаут неактивности для сброса текста в мс (по умолчанию 600 мс).</param>
    public EventCorrelator(
        ISystemMetricsProvider? metricsProvider = null,
        int flushTimeoutMs = DefaultTextFlushTimeoutMs)
    {
        _metricsProvider = metricsProvider ?? new DefaultSystemMetricsProvider();
        _flushTimeoutMs = flushTimeoutMs > 0 ? flushTimeoutMs : DefaultTextFlushTimeoutMs;
        _textFlushTimer = new Timer(OnTextFlushTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <inheritdoc />
    public void ProcessMouseEvent(RawMouseEvent mouseEvent, WindowContext? context = null)
    {
        SemanticAction? pendingTextAction = null;
        SemanticAction? mouseAction = null;

        lock (_syncLock)
        {
            if (_isDisposed)
            {
                return;
            }

            if (context != null && context != WindowContext.Empty)
            {
                _cachedContext = context;
            }
            var effectiveContext = (context != null && context != WindowContext.Empty)
                ? context
                : _cachedContext;

            if (mouseEvent.EventType == RawMouseEventType.MouseDown)
            {
                _isMouseDown = true;
                _mouseDownButton = mouseEvent.Button;
                _mouseDownX = mouseEvent.X;
                _mouseDownY = mouseEvent.Y;
                _mouseDownTimestamp = mouseEvent.Timestamp;
                return;
            }

            if (mouseEvent.EventType == RawMouseEventType.MouseUp)
            {
                if (!_isMouseDown || _mouseDownButton != mouseEvent.Button)
                {
                    return;
                }

                _isMouseDown = false;

                int dx = Math.Abs(mouseEvent.X - _mouseDownX);
                int dy = Math.Abs(mouseEvent.Y - _mouseDownY);
                int dragToleranceX = _metricsProvider.DoubleClickWidth * 2;
                int dragToleranceY = _metricsProvider.DoubleClickHeight * 2;

                if (dx > dragToleranceX || dy > dragToleranceY)
                {
                    // Перемещение превысило допуск — это drag-and-drop, а не одиночный клик
                    _lastLeftClick = null;
                    return;
                }

                // Сбрасываем накопленный текст перед испусканием действия мыши
                pendingTextAction = FlushPendingInternalLocked();

                if (mouseEvent.Button == RawMouseButton.Left)
                {
                    bool isDoubleClick = false;
                    if (_lastLeftClick.HasValue)
                    {
                        var (lastTime, lastX, lastY) = _lastLeftClick.Value;
                        var elapsed = (mouseEvent.Timestamp - lastTime).TotalMilliseconds;
                        var clickDx = Math.Abs(mouseEvent.X - lastX);
                        var clickDy = Math.Abs(mouseEvent.Y - lastY);

                        if (elapsed >= 0 &&
                            elapsed <= _metricsProvider.DoubleClickTimeMs &&
                            clickDx <= _metricsProvider.DoubleClickWidth &&
                            clickDy <= _metricsProvider.DoubleClickHeight)
                        {
                            isDoubleClick = true;
                        }
                    }

                    if (isDoubleClick)
                    {
                        // Двойной клик распознан — сбрасываем трекер (следующий клик будет одиночным)
                        _lastLeftClick = null;
                        int seq = Interlocked.Increment(ref _sequenceIndex);
                        mouseAction = SemanticAction.CreateMouseClick(
                            SemanticActionType.DoubleLeftClick,
                            mouseEvent.X,
                            mouseEvent.Y,
                            effectiveContext,
                            mouseEvent.Timestamp,
                            seq
                        );
                    }
                    else
                    {
                        // Одиночный левый клик — фиксируем как кандидата на двойной клик
                        _lastLeftClick = (mouseEvent.Timestamp, mouseEvent.X, mouseEvent.Y);
                        int seq = Interlocked.Increment(ref _sequenceIndex);
                        mouseAction = SemanticAction.CreateMouseClick(
                            SemanticActionType.LeftClick,
                            mouseEvent.X,
                            mouseEvent.Y,
                            effectiveContext,
                            mouseEvent.Timestamp,
                            seq
                        );
                    }
                }
                else if (mouseEvent.Button == RawMouseButton.Right)
                {
                    _lastLeftClick = null;
                    int seq = Interlocked.Increment(ref _sequenceIndex);
                    mouseAction = SemanticAction.CreateMouseClick(
                        SemanticActionType.RightClick,
                        mouseEvent.X,
                        mouseEvent.Y,
                        effectiveContext,
                        mouseEvent.Timestamp,
                        seq
                    );
                }
                else if (mouseEvent.Button == RawMouseButton.Middle)
                {
                    _lastLeftClick = null;
                    int seq = Interlocked.Increment(ref _sequenceIndex);
                    mouseAction = SemanticAction.CreateMouseClick(
                        SemanticActionType.MiddleClick,
                        mouseEvent.X,
                        mouseEvent.Y,
                        effectiveContext,
                        mouseEvent.Timestamp,
                        seq
                    );
                }
            }
        }

        if (pendingTextAction != null)
        {
            EmitAction(pendingTextAction);
        }

        if (mouseAction != null)
        {
            EmitAction(mouseAction);
        }
    }

    /// <inheritdoc />
    public void ProcessKeyboardEvent(RawKeyboardEvent keyboardEvent, WindowContext? context = null)
    {
        // Рассматриваем только события нажатия клавиш
        if (keyboardEvent.EventType != RawKeyboardEventType.KeyDown)
        {
            return;
        }

        // Клавиши-модификаторы сами по себе не генерируют семантических действий
        if (IsModifierKey(keyboardEvent.VirtualKey))
        {
            return;
        }

        SemanticAction? pendingTextAction = null;
        SemanticAction? keyAction = null;

        lock (_syncLock)
        {
            if (_isDisposed)
            {
                return;
            }

            if (context != null && context != WindowContext.Empty)
            {
                _cachedContext = context;
            }
            var effectiveContext = (context != null && context != WindowContext.Empty)
                ? context
                : _cachedContext;

            // Любое клавиатурное действие сбрасывает ожидание двойного клика мыши
            _lastLeftClick = null;

            if (keyboardEvent.IsShortcut)
            {
                pendingTextAction = FlushPendingInternalLocked();
                string keyName = GetKeyName(keyboardEvent.VirtualKey);
                int seq = Interlocked.Increment(ref _sequenceIndex);
                keyAction = SemanticAction.CreateShortcut(
                    virtualKey: keyboardEvent.VirtualKey,
                    keyName: keyName,
                    modifiers: keyboardEvent.Modifiers,
                    context: effectiveContext,
                    timestamp: keyboardEvent.Timestamp,
                    sequenceIndex: seq
                );
            }
            else if (keyboardEvent.VirtualKey is 13 or 27 or 9)
            {
                pendingTextAction = FlushPendingInternalLocked();
                string keyName = keyboardEvent.VirtualKey switch
                {
                    13 => "Enter",
                    27 => "Escape",
                    9 => "Tab",
                    _ => GetKeyName(keyboardEvent.VirtualKey)
                };
                int seq = Interlocked.Increment(ref _sequenceIndex);
                keyAction = SemanticAction.CreateKeyPress(
                    virtualKey: keyboardEvent.VirtualKey,
                    keyName: keyName,
                    modifiers: keyboardEvent.Modifiers,
                    context: effectiveContext,
                    timestamp: keyboardEvent.Timestamp,
                    sequenceIndex: seq
                );
            }
            else if (keyboardEvent.IsTextInput)
            {
                if (_textBuffer.Length == 0)
                {
                    _textStartedAt = keyboardEvent.Timestamp;
                }
                _textCompletedAt = keyboardEvent.Timestamp;
                _textBuffer.Append(keyboardEvent.Character);
                _textCharacterCount += keyboardEvent.Character?.Length ?? 1;

                _textFlushTimer.Change(_flushTimeoutMs, Timeout.Infinite);
            }
            else
            {
                if (keyboardEvent.IsDeadKey)
                {
                    return;
                }

                // Навигационные и редактирующие клавиши (Backspace, Delete, стрелки, Home, End и т.д.)
                pendingTextAction = FlushPendingInternalLocked();
                string keyName = GetKeyName(keyboardEvent.VirtualKey);
                int seq = Interlocked.Increment(ref _sequenceIndex);
                keyAction = SemanticAction.CreateKeyPress(
                    virtualKey: keyboardEvent.VirtualKey,
                    keyName: keyName,
                    modifiers: keyboardEvent.Modifiers,
                    context: effectiveContext,
                    timestamp: keyboardEvent.Timestamp,
                    sequenceIndex: seq
                );
            }
        }

        if (pendingTextAction != null)
        {
            EmitAction(pendingTextAction);
        }

        if (keyAction != null)
        {
            EmitAction(keyAction);
        }
    }

    /// <inheritdoc />
    public void FlushPending()
    {
        SemanticAction? actionToEmit = null;

        lock (_syncLock)
        {
            if (_isDisposed)
            {
                return;
            }

            actionToEmit = FlushPendingInternalLocked();
        }

        if (actionToEmit != null)
        {
            EmitAction(actionToEmit);
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (_syncLock)
        {
            _textFlushTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _textBuffer.Clear();
            _textCharacterCount = 0;
            _textStartedAt = default;
            _textCompletedAt = default;
            _isMouseDown = false;
            _mouseDownButton = RawMouseButton.None;
            _mouseDownX = 0;
            _mouseDownY = 0;
            _mouseDownTimestamp = default;
            _lastLeftClick = null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        SemanticAction? remainingAction = null;

        lock (_syncLock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            remainingAction = FlushPendingInternalLocked();
            _textFlushTimer.Dispose();
        }

        if (remainingAction != null)
        {
            EmitAction(remainingAction);
        }
    }

    private SemanticAction? FlushPendingInternalLocked()
    {
        _textFlushTimer.Change(Timeout.Infinite, Timeout.Infinite);

        if (_textBuffer.Length == 0)
        {
            return null;
        }

        string text = _textBuffer.ToString();
        DateTime startedAt = _textStartedAt;
        DateTime completedAt = _textCompletedAt;
        WindowContext context = _cachedContext;

        _textBuffer.Clear();
        _textCharacterCount = 0;

        int seq = Interlocked.Increment(ref _sequenceIndex);
        return SemanticAction.CreateTextInput(
            text: text,
            context: context,
            startedAt: startedAt,
            completedAt: completedAt,
            isSensitive: false,
            sequenceIndex: seq
        );
    }

    private void OnTextFlushTimerElapsed(object? state)
    {
        try
        {
            FlushPending();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EventCorrelator] Ошибка при сбросе текста по таймеру: {ex.Message}");
        }
    }

    private void EmitAction(SemanticAction action)
    {
        try
        {
            ActionCorrelated?.Invoke(this, action);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EventCorrelator] Исключение в обработчике события ActionCorrelated: {ex.Message}");
        }
    }

    private static bool IsModifierKey(int vk) => vk is
        0x10 or 0xA0 or 0xA1 or // Shift, LShift, RShift
        0x11 or 0xA2 or 0xA3 or // Control, LControl, RControl
        0x12 or 0xA4 or 0xA5 or // Alt, LAlt, RAlt
        0x5B or 0x5C;           // LWin, RWin

    private static string GetKeyName(int virtualKey) => virtualKey switch
    {
        8 => "Backspace",
        9 => "Tab",
        13 => "Enter",
        19 => "Pause",
        20 => "CapsLock",
        27 => "Escape",
        32 => "Space",
        33 => "PageUp",
        34 => "PageDown",
        35 => "End",
        36 => "Home",
        37 => "Left",
        38 => "Up",
        39 => "Right",
        40 => "Down",
        44 => "PrintScreen",
        45 => "Insert",
        46 => "Delete",
        >= 48 and <= 57 => ((char)virtualKey).ToString(),
        >= 65 and <= 90 => ((char)virtualKey).ToString(),
        >= 96 and <= 105 => $"NumPad{virtualKey - 96}",
        106 => "Multiply",
        107 => "Add",
        109 => "Subtract",
        110 => "Decimal",
        111 => "Divide",
        >= 112 and <= 123 => $"F{virtualKey - 111}",
        144 => "NumLock",
        145 => "ScrollLock",
        _ => $"Key_{virtualKey}"
    };
}
