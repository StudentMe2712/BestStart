using System.Diagnostics;
using System.Threading.Channels;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;

namespace Stepwise.Core.Engine;

/// <summary>
/// Главный оркестрирующий движок записи сценариев и действий пользователя.
/// Реализует неблокирующий конвейер:
/// RawInput (мышь/клавиатура) -> Channel -> EventCorrelator -> ActionCorrelated ->
/// ITargetResolver -> IRecordingPolicy -> IStepDetector -> ICaptureCoordinator -> IProjectRepository -> StepRecorded.
/// </summary>
public sealed class RecordingEngine : IRecordingEngine
{
    private readonly IInputMonitoringService _inputMonitor;
    private readonly IActiveWindowTracker? _windowTracker;
    private readonly IEventCorrelator _correlator;
    private readonly ITargetResolver _targetResolver;
    private readonly IRecordingPolicy _policy;
    private readonly IStepDetector _stepDetector;
    private readonly ICaptureCoordinator _captureCoordinator;
    private readonly IProjectRepository? _repository;
    private readonly RecordingSessionStateMachine _stateMachine = new();
    private readonly object _startStopLock = new();

    private Channel<RawInputQueueItem>? _rawInputChannel;
    private Channel<SemanticAction>? _actionChannel;
    private CancellationTokenSource? _cts;
    private Task? _processingTask;
    private Task? _actionProcessingTask;
    private WindowContext _currentWindowContext = WindowContext.Empty;
    private int _sequenceIndex;
    private bool _isDisposed;

    /// <inheritdoc />
    public RecordingSessionState State => _stateMachine.CurrentState;

    /// <inheritdoc />
    public bool IsRecording => State == RecordingSessionState.Recording;

    /// <inheritdoc />
    public event EventHandler<Step>? StepRecorded;

    /// <inheritdoc />
    public event EventHandler<RecordingSessionState>? StateChanged;

    /// <summary>
    /// Создает экземпляр оркестрирующего движка записи.
    /// </summary>
    public RecordingEngine(
        IInputMonitoringService inputMonitor,
        IActiveWindowTracker? windowTracker,
        IEventCorrelator correlator,
        ITargetResolver targetResolver,
        IRecordingPolicy policy,
        IStepDetector stepDetector,
        ICaptureCoordinator captureCoordinator,
        IProjectRepository? repository = null)
    {
        _inputMonitor = inputMonitor ?? throw new ArgumentNullException(nameof(inputMonitor));
        _windowTracker = windowTracker;
        _correlator = correlator ?? throw new ArgumentNullException(nameof(correlator));
        _targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _stepDetector = stepDetector ?? throw new ArgumentNullException(nameof(stepDetector));
        _captureCoordinator = captureCoordinator ?? throw new ArgumentNullException(nameof(captureCoordinator));
        _repository = repository;

        _stateMachine.StateChanged += (_, newState) => StateChanged?.Invoke(this, newState);
    }

    /// <inheritdoc />
    public void StartRecording()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        lock (_startStopLock)
        {
            if (_stateMachine.CurrentState == RecordingSessionState.Completed ||
                _stateMachine.CurrentState == RecordingSessionState.Failed)
            {
                _stateMachine.ResetToIdle();
            }

            if (_stateMachine.CurrentState != RecordingSessionState.Idle)
            {
                return;
            }

            _sequenceIndex = 0;

            // Инициализируем текущий контекст окна
            var activeWindow = _windowTracker?.GetActiveWindow();
            _currentWindowContext = activeWindow != null
                ? WindowContext.FromActiveWindowInfo(activeWindow)
                : WindowContext.Empty;

            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            _rawInputChannel = Channel.CreateBounded<RawInputQueueItem>(
                new BoundedChannelOptions(5000)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = true,
                    SingleWriter = false
                });

            _actionChannel = Channel.CreateUnbounded<SemanticAction>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false
                });

            _correlator.Reset();

            // Подписываемся на события коррелятора и монитора ввода
            _correlator.ActionCorrelated += OnActionCorrelated;
            _inputMonitor.MouseEventReceived += OnRawMouseEvent;
            _inputMonitor.KeyboardEventReceived += OnRawKeyboardEvent;

            if (_windowTracker != null)
            {
                _windowTracker.ActiveWindowChanged += OnActiveWindowChanged;
            }

            // Запускаем конвейерные фоновые задачи
            var rawReader = _rawInputChannel.Reader;
            var actionReader = _actionChannel.Reader;
            _processingTask = Task.Run(() => ProcessQueueAsync(rawReader, ct), ct);
            _actionProcessingTask = Task.Run(() => ProcessActionsAsync(actionReader, ct), ct);

            // Запускаем сервисы мониторинга
            _inputMonitor.Start();
            _windowTracker?.Start();

            // Переводим автомат в состояние записи
            _stateMachine.Transition(RecordingSessionState.Recording);
        }
    }

    /// <inheritdoc />
    public void PauseRecording()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        lock (_startStopLock)
        {
            if (_stateMachine.CurrentState == RecordingSessionState.Recording)
            {
                _stateMachine.Transition(RecordingSessionState.Paused);
            }
        }
    }

    /// <inheritdoc />
    public void ResumeRecording()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        lock (_startStopLock)
        {
            if (_stateMachine.CurrentState == RecordingSessionState.Paused)
            {
                _stateMachine.Transition(RecordingSessionState.Recording);
            }
        }
    }

    /// <inheritdoc />
    public void StopRecording()
    {
        Task.Run(() => StopRecordingAsync(CancellationToken.None)).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task StopRecordingAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Task? processingTask;
        Task? actionProcessingTask;

        lock (_startStopLock)
        {
            if (_stateMachine.CurrentState != RecordingSessionState.Recording &&
                _stateMachine.CurrentState != RecordingSessionState.Paused)
            {
                return;
            }

            _stateMachine.Transition(RecordingSessionState.Stopping);

            // 1. Отключаем низкоуровневые хуки ввода
            _inputMonitor.MouseEventReceived -= OnRawMouseEvent;
            _inputMonitor.KeyboardEventReceived -= OnRawKeyboardEvent;
            _inputMonitor.Stop();

            if (_windowTracker != null)
            {
                _windowTracker.ActiveWindowChanged -= OnActiveWindowChanged;
                _windowTracker.Stop();
            }

            // 2. Сигнализируем очереди сырого ввода о завершении
            _rawInputChannel?.Writer.TryComplete();

            processingTask = _processingTask;
            actionProcessingTask = _actionProcessingTask;
        }

        try
        {
            // 3. Дожидаемся обработки всех накопленных сырых событий ввода
            if (processingTask != null)
            {
                await processingTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            // 4. Сбрасываем буферы коррелятора (например, отложенный текст)
            _correlator.FlushPending();
            _correlator.ActionCorrelated -= OnActionCorrelated;

            // 5. Завершаем очередь действий и дожидаемся обработки всех шагов
            _actionChannel?.Writer.TryComplete();
            if (actionProcessingTask != null)
            {
                await actionProcessingTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            lock (_startStopLock)
            {
                if (_stateMachine.CurrentState == RecordingSessionState.Stopping)
                {
                    _stateMachine.Transition(RecordingSessionState.Completed);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _cts?.Cancel();
            lock (_startStopLock)
            {
                if (_stateMachine.CurrentState == RecordingSessionState.Stopping)
                {
                    _stateMachine.Transition(RecordingSessionState.Failed);
                }
            }
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RecordingEngine] Error while stopping recording: {ex}");
            lock (_startStopLock)
            {
                if (_stateMachine.CurrentState == RecordingSessionState.Stopping)
                {
                    _stateMachine.Transition(RecordingSessionState.Failed);
                }
            }
            throw;
        }
        finally
        {
            CleanupSessionResources();
        }
    }

    private void OnRawMouseEvent(object? sender, RawMouseEvent e)
    {
        if (!IsRecording)
        {
            return;
        }

        _rawInputChannel?.Writer.TryWrite(RawInputQueueItem.Mouse(e, _currentWindowContext));
    }

    private void OnRawKeyboardEvent(object? sender, RawKeyboardEvent e)
    {
        if (!IsRecording)
        {
            return;
        }

        _rawInputChannel?.Writer.TryWrite(RawInputQueueItem.Keyboard(e, _currentWindowContext));
    }

    private void OnActiveWindowChanged(object? sender, ActiveWindowInfo e)
    {
        _currentWindowContext = WindowContext.FromActiveWindowInfo(e);
    }

    private void OnActionCorrelated(object? sender, SemanticAction action)
    {
        _actionChannel?.Writer.TryWrite(action);
    }

    private async Task ProcessQueueAsync(ChannelReader<RawInputQueueItem> reader, CancellationToken ct)
    {
        try
        {
            while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                while (reader.TryRead(out var item))
                {
                    ct.ThrowIfCancellationRequested();

                    if (item.MouseEvent is { } mouseEvent)
                    {
                        _correlator.ProcessMouseEvent(mouseEvent, item.Context);
                    }
                    else if (item.KeyboardEvent is { } keyboardEvent)
                    {
                        _correlator.ProcessKeyboardEvent(keyboardEvent, item.Context);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Корректное завершение при отмене
        }
        catch (Exception ex)
        {
            HandleFatalError(ex);
        }
    }

    private async Task ProcessActionsAsync(ChannelReader<SemanticAction> reader, CancellationToken ct)
    {
        try
        {
            while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                while (reader.TryRead(out var action))
                {
                    ct.ThrowIfCancellationRequested();

                    // 1. Определение целевого UI-элемента
                    var target = await _targetResolver.ResolveTargetAsync(action, ct).ConfigureAwait(false);

                    // 2. Применение политики записи и фильтрации
                    var decision = _policy.Evaluate(action, target);
                    if (decision == RecordingPolicyDecision.Suppress)
                    {
                        continue;
                    }

                    // 3. Формирование шага сценария
                    int sequenceIndex = Interlocked.Increment(ref _sequenceIndex);
                    var step = _stepDetector.DetectStep(action, target, decision, sequenceIndex);
                    if (step == null)
                    {
                        Interlocked.Decrement(ref _sequenceIndex);
                        continue;
                    }

                    // 4. Захват скриншота экрана
                    var screenshotPath = await _captureCoordinator.CaptureStepAsync(step.SequenceIndex, target, ct).ConfigureAwait(false);
                    if (screenshotPath != null)
                    {
                        step = step with { ScreenshotPath = screenshotPath };
                    }

                    // 5. Сохранение в локальное хранилище
                    try
                    {
                        _repository?.SaveStep(step);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[RecordingEngine] Warning: Repository save failed for step {step.SequenceIndex}: {ex.Message}");
                    }

                    // 6. Оповещение подписчиков о записи шага
                    StepRecorded?.Invoke(this, step);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Корректное завершение при отмене
        }
        catch (Exception ex)
        {
            HandleFatalError(ex);
        }
    }

    private void HandleFatalError(Exception ex)
    {
        Debug.WriteLine($"[RecordingEngine] Fatal error during background processing: {ex}");
        lock (_startStopLock)
        {
            if (_stateMachine.CurrentState == RecordingSessionState.Recording ||
                _stateMachine.CurrentState == RecordingSessionState.Paused)
            {
                _stateMachine.TryTransition(RecordingSessionState.Stopping, out _);
                _stateMachine.TryTransition(RecordingSessionState.Failed, out _);
            }
            else if (_stateMachine.CurrentState == RecordingSessionState.Stopping)
            {
                _stateMachine.TryTransition(RecordingSessionState.Failed, out _);
            }
        }
    }

    private void CleanupSessionResources()
    {
        lock (_startStopLock)
        {
            _cts?.Dispose();
            _cts = null;
            _rawInputChannel = null;
            _actionChannel = null;
            _processingTask = null;
            _actionProcessingTask = null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        try
        {
            StopRecording();
        }
        catch
        {
            // Подавляем исключения при Dispose
        }

        CleanupSessionResources();
        _correlator.Dispose();
        _inputMonitor.Dispose();
        _windowTracker?.Dispose();
        _repository?.Dispose();
    }
}
