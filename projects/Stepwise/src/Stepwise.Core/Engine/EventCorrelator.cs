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
    public const int DefaultScrollFlushTimeoutMs = 300;

    private readonly ISystemMetricsProvider _metricsProvider;
    private readonly int _flushTimeoutMs;
    private readonly int _scrollTimeoutMs;
    private readonly object _syncLock = new();
    private readonly StringBuilder _textBuffer = new();
    private readonly Timer _textFlushTimer;
    private readonly Timer _scrollFlushTimer;

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

    // Метаданные буфера прокрутки (Scroll)
    private int _scrollTotalDelta;
    private int _scrollX;
    private int _scrollY;
    private DateTime _scrollTimestamp;
    private WindowContext _scrollContext = WindowContext.Empty;

    /// <summary>
    /// Событие формирования скоррелированного семантического действия.
    /// </summary>
    public event EventHandler<SemanticAction>? ActionCorrelated;

    /// <summary>
    /// Создает экземпляр <see cref="EventCorrelator"/>.
    /// </summary>
    /// <param name="metricsProvider">Провайдер системных метрик (по умолчанию <see cref="DefaultSystemMetricsProvider"/>).</param>
    /// <param name="flushTimeoutMs">Таймаут неактивности для сброса текста в мс (по умолчанию 600 мс).</param>
    /// <param name="scrollTimeoutMs">Таймаут неактивности для сброса скролла в мс (по умолчанию 300 мс).</param>
    public EventCorrelator(
        ISystemMetricsProvider? metricsProvider = null,
        int flushTimeoutMs = DefaultTextFlushTimeoutMs,
        int scrollTimeoutMs = DefaultScrollFlushTimeoutMs)
    {
        _metricsProvider = metricsProvider ?? new DefaultSystemMetricsProvider();
        _flushTimeoutMs = flushTimeoutMs > 0 ? flushTimeoutMs : DefaultTextFlushTimeoutMs;
        _scrollTimeoutMs = scrollTimeoutMs > 0 ? scrollTimeoutMs : DefaultScrollFlushTimeoutMs;
        _textFlushTimer = new Timer(OnTextFlushTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
        _scrollFlushTimer = new Timer(OnScrollFlushTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <inheritdoc />
    public void ProcessMouseEvent(RawMouseEvent mouseEvent, WindowContext? context = null)
    {
        SemanticAction? pendingTextAction = null;
        SemanticAction? pendingScrollAction = null;
        SemanticAction? mouseAction = null;

        lock (_syncLock)
        {
            if (_isDisposed)
            {
                return;
            }

            var effectiveContext = (context != null && context != WindowContext.Empty)
                ? context
                : _cachedContext;

            // Сброс буферов при смене контекста окна
            if (_cachedContext != WindowContext.Empty &&
                _cachedContext.WindowHandle != 0 &&
                effectiveContext.WindowHandle != 0 &&
                effectiveContext.WindowHandle != _cachedContext.WindowHandle)
            {
                pendingTextAction = FlushPendingInternalLocked();
                pendingScrollAction = FlushPendingScrollInternalLocked();
                _lastLeftClick = null;
                _isMouseDown = false;
            }

            if (context != null && context != WindowContext.Empty)
            {
                _cachedContext = context;
            }

            if (mouseEvent.EventType == RawMouseEventType.Wheel)
            {
                _lastLeftClick = null;
                var textFlush = FlushPendingInternalLocked();
                if (textFlush != null)
                {
                    pendingTextAction ??= textFlush;
                }

                if (mouseEvent.Delta != 0)
                {
                    bool signChanged = (_scrollTotalDelta > 0 && mouseEvent.Delta < 0) ||
                                       (_scrollTotalDelta < 0 && mouseEvent.Delta > 0);

                    if (signChanged)
                    {
                        var scrollFlush = FlushPendingScrollInternalLocked();
                        if (scrollFlush != null)
                        {
                            pendingScrollAction ??= scrollFlush;
                        }
                    }

                    if (_scrollTotalDelta == 0)
                    {
                        _scrollTotalDelta = mouseEvent.Delta;
                        _scrollX = mouseEvent.X;
                        _scrollY = mouseEvent.Y;
                        _scrollTimestamp = mouseEvent.Timestamp;
                        _scrollContext = effectiveContext;
                    }
                    else
                    {
                        _scrollTotalDelta += mouseEvent.Delta;
                        _scrollX = mouseEvent.X;
                        _scrollY = mouseEvent.Y;
                        _scrollTimestamp = mouseEvent.Timestamp;
                        _scrollContext = effectiveContext;
                    }

                    _scrollFlushTimer.Change(_scrollTimeoutMs, Timeout.Infinite);
                }

                goto EmitActions;
            }

            if (mouseEvent.EventType == RawMouseEventType.MouseDown)
            {
                var scrollFlush = FlushPendingScrollInternalLocked();
                if (scrollFlush != null)
                {
                    pendingScrollAction ??= scrollFlush;
                }

                _isMouseDown = true;
                _mouseDownButton = mouseEvent.Button;
                _mouseDownX = mouseEvent.X;
                _mouseDownY = mouseEvent.Y;
                _mouseDownTimestamp = mouseEvent.Timestamp;
                goto EmitActions;
            }

            if (mouseEvent.EventType == RawMouseEventType.MouseUp)
            {
                if (!_isMouseDown || _mouseDownButton != mouseEvent.Button)
                {
                    goto EmitActions;
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
                    var textFlush = FlushPendingInternalLocked();
                    if (textFlush != null)
                    {
                        pendingTextAction ??= textFlush;
                    }

                    int seq = Interlocked.Increment(ref _sequenceIndex);
                    mouseAction = SemanticAction.CreateDragAndDrop(
                        startX: _mouseDownX,
                        startY: _mouseDownY,
                        endX: mouseEvent.X,
                        endY: mouseEvent.Y,
                        button: _mouseDownButton,
                        context: effectiveContext,
                        timestamp: mouseEvent.Timestamp,
                        sequenceIndex: seq
                    );
                }
                else
                {
                    // Сбрасываем накопленный текст перед испусканием действия мыши
                    var textFlush = FlushPendingInternalLocked();
                    if (textFlush != null)
                    {
                        pendingTextAction ??= textFlush;
                    }

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

        EmitActions:;
        }

        if (pendingTextAction != null)
        {
            EmitAction(pendingTextAction);
        }

        if (pendingScrollAction != null)
        {
            EmitAction(pendingScrollAction);
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
        SemanticAction? pendingScrollAction = null;
        SemanticAction? keyAction = null;

        lock (_syncLock)
        {
            if (_isDisposed)
            {
                return;
            }

            var effectiveContext = (context != null && context != WindowContext.Empty)
                ? context
                : _cachedContext;

            // Сброс буферов при смене контекста окна
            if (_cachedContext != WindowContext.Empty &&
                _cachedContext.WindowHandle != 0 &&
                effectiveContext.WindowHandle != 0 &&
                effectiveContext.WindowHandle != _cachedContext.WindowHandle)
            {
                pendingTextAction = FlushPendingInternalLocked();
                pendingScrollAction = FlushPendingScrollInternalLocked();
                _lastLeftClick = null;
                _isMouseDown = false;
            }

            if (context != null && context != WindowContext.Empty)
            {
                _cachedContext = context;
            }

            // Любое клавиатурное действие сбрасывает ожидание двойного клика мыши и накопленный скролл
            _lastLeftClick = null;
            var scrollFlush = FlushPendingScrollInternalLocked();
            if (scrollFlush != null)
            {
                pendingScrollAction ??= scrollFlush;
            }

            if (keyboardEvent.IsShortcut)
            {
                var textFlush = FlushPendingInternalLocked();
                if (textFlush != null) pendingTextAction ??= textFlush;
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
                var textFlush = FlushPendingInternalLocked();
                if (textFlush != null) pendingTextAction ??= textFlush;
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
                    goto EmitActions;
                }

                // Навигационные и редактирующие клавиши (Backspace, Delete, стрелки, Home, End и т.д.)
                var textFlush = FlushPendingInternalLocked();
                if (textFlush != null) pendingTextAction ??= textFlush;
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

        EmitActions:;
        }

        if (pendingTextAction != null)
        {
            EmitAction(pendingTextAction);
        }

        if (pendingScrollAction != null)
        {
            EmitAction(pendingScrollAction);
        }

        if (keyAction != null)
        {
            EmitAction(keyAction);
        }
    }

    /// <inheritdoc />
    public void FlushPending()
    {
        SemanticAction? textAction = null;
        SemanticAction? scrollAction = null;

        lock (_syncLock)
        {
            if (_isDisposed)
            {
                return;
            }

            textAction = FlushPendingInternalLocked();
            scrollAction = FlushPendingScrollInternalLocked();
        }

        if (textAction != null)
        {
            EmitAction(textAction);
        }

        if (scrollAction != null)
        {
            EmitAction(scrollAction);
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

            _scrollFlushTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _scrollTotalDelta = 0;
            _scrollX = 0;
            _scrollY = 0;
            _scrollTimestamp = default;
            _scrollContext = WindowContext.Empty;

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
        SemanticAction? remainingTextAction = null;
        SemanticAction? remainingScrollAction = null;

        lock (_syncLock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            remainingTextAction = FlushPendingInternalLocked();
            remainingScrollAction = FlushPendingScrollInternalLocked();
            _textFlushTimer.Dispose();
            _scrollFlushTimer.Dispose();
        }

        if (remainingTextAction != null)
        {
            EmitAction(remainingTextAction);
        }

        if (remainingScrollAction != null)
        {
            EmitAction(remainingScrollAction);
        }
    }

    private SemanticAction? FlushPendingScrollInternalLocked()
    {
        _scrollFlushTimer.Change(Timeout.Infinite, Timeout.Infinite);

        if (_scrollTotalDelta == 0)
        {
            return null;
        }

        int totalDelta = _scrollTotalDelta;
        int x = _scrollX;
        int y = _scrollY;
        DateTime timestamp = _scrollTimestamp;
        WindowContext context = (_scrollContext != WindowContext.Empty && _scrollContext.WindowHandle != 0)
            ? _scrollContext
            : _cachedContext;

        _scrollTotalDelta = 0;
        _scrollX = 0;
        _scrollY = 0;
        _scrollTimestamp = default;
        _scrollContext = WindowContext.Empty;

        int seq = Interlocked.Increment(ref _sequenceIndex);
        return SemanticAction.CreateScroll(
            x: x,
            y: y,
            delta: totalDelta,
            context: context,
            timestamp: timestamp,
            sequenceIndex: seq
        );
    }

    private void OnScrollFlushTimerElapsed(object? state)
    {
        try
        {
            SemanticAction? scrollAction = null;

            lock (_syncLock)
            {
                if (_isDisposed)
                {
                    return;
                }

                scrollAction = FlushPendingScrollInternalLocked();
            }

            if (scrollAction != null)
            {
                EmitAction(scrollAction);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EventCorrelator] Ошибка при сбросе скролла по таймеру: {ex.Message}");
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
