namespace Stepwise.Core.Models;

/// <summary>
/// Состояние жизненного цикла сессии записи действий пользователя.
/// </summary>
public enum RecordingSessionState
{
    Idle,
    Recording,
    Paused,
    Stopping,
    Completed,
    Failed
}

/// <summary>
/// Делегат для события изменения состояния сессии записи.
/// </summary>
public delegate void RecordingStateChangedHandler(RecordingSessionState oldState, RecordingSessionState newState);

/// <summary>
/// Потокобезопасный конечный автомат состояний сессии записи.
/// </summary>
public sealed class RecordingSessionStateMachine
{
    private readonly object _syncLock = new();
    private RecordingSessionState _currentState = RecordingSessionState.Idle;

    /// <summary>
    /// Текущее состояние сессии записи.
    /// </summary>
    public RecordingSessionState CurrentState
    {
        get
        {
            lock (_syncLock)
            {
                return _currentState;
            }
        }
    }

    /// <summary>
    /// Событие смены состояния сессии записи.
    /// </summary>
    public event RecordingStateChangedHandler? StateChanged;

    /// <summary>
    /// Сессия находится в состоянии ожидания (готова к началу записи).
    /// </summary>
    public bool IsIdle => CurrentState == RecordingSessionState.Idle;

    /// <summary>
    /// Сессия находится в активном состоянии записи событий.
    /// </summary>
    public bool IsRecording => CurrentState == RecordingSessionState.Recording;

    /// <summary>
    /// Сессия приостановлена.
    /// </summary>
    public bool IsPaused => CurrentState == RecordingSessionState.Paused;

    /// <summary>
    /// Сессия завершает работу и сохраняет накопленные данные.
    /// </summary>
    public bool IsStopping => CurrentState == RecordingSessionState.Stopping;

    /// <summary>
    /// Сессия успешно завершена.
    /// </summary>
    public bool IsCompleted => CurrentState == RecordingSessionState.Completed;

    /// <summary>
    /// Сессия завершилась с ошибкой.
    /// </summary>
    public bool IsFailed => CurrentState == RecordingSessionState.Failed;

    /// <summary>
    /// Проверяет допустимость перехода между двумя состояниями.
    /// </summary>
    public static bool IsValidTransition(RecordingSessionState from, RecordingSessionState to)
    {
        return (from, to) switch
        {
            (RecordingSessionState.Idle, RecordingSessionState.Recording) => true,
            (RecordingSessionState.Recording, RecordingSessionState.Paused) => true,
            (RecordingSessionState.Paused, RecordingSessionState.Recording) => true,
            (RecordingSessionState.Recording, RecordingSessionState.Stopping) => true,
            (RecordingSessionState.Paused, RecordingSessionState.Stopping) => true,
            (RecordingSessionState.Stopping, RecordingSessionState.Completed) => true,
            (RecordingSessionState.Stopping, RecordingSessionState.Failed) => true,
            _ => false
        };
    }

    /// <summary>
    /// Проверяет, возможен ли переход из текущего состояния в целевое.
    /// </summary>
    public bool CanTransitionTo(RecordingSessionState targetState)
    {
        lock (_syncLock)
        {
            return IsValidTransition(_currentState, targetState);
        }
    }

    /// <summary>
    /// Пытается выполнить потокобезопасный переход в целевое состояние.
    /// </summary>
    /// <param name="targetState">Целевое состояние сессии.</param>
    /// <param name="error">Сообщение об ошибке, если переход недопустим.</param>
    /// <returns><c>true</c>, если переход успешно совершен; иначе <c>false</c>.</returns>
    public bool TryTransition(RecordingSessionState targetState, out string? error)
    {
        RecordingStateChangedHandler? handler;
        RecordingSessionState oldState;

        lock (_syncLock)
        {
            oldState = _currentState;
            if (!IsValidTransition(oldState, targetState))
            {
                error = $"Invalid state transition from {oldState} to {targetState}.";
                return false;
            }

            _currentState = targetState;
            error = null;
            handler = StateChanged;
        }

        handler?.Invoke(oldState, targetState);
        return true;
    }

    /// <summary>
    /// Выполняет переход в целевое состояние.
    /// Выбрасывает <see cref="InvalidOperationException"/> при недопустимом переходе.
    /// </summary>
    public void Transition(RecordingSessionState targetState)
    {
        if (!TryTransition(targetState, out var error))
        {
            throw new InvalidOperationException(error);
        }
    }

    /// <summary>
    /// Сбрасывает состояние в <see cref="RecordingSessionState.Idle"/> из терминальных состояний (Completed или Failed).
    /// Если автомат уже в состоянии Idle, вызов не производит действий.
    /// </summary>
    public void ResetToIdle()
    {
        RecordingStateChangedHandler? handler = null;
        RecordingSessionState oldState;

        lock (_syncLock)
        {
            oldState = _currentState;
            if (oldState == RecordingSessionState.Idle)
            {
                return;
            }

            if (oldState != RecordingSessionState.Completed && oldState != RecordingSessionState.Failed)
            {
                throw new InvalidOperationException($"Cannot reset to Idle from state {oldState}. The session must be in Completed or Failed state.");
            }

            _currentState = RecordingSessionState.Idle;
            handler = StateChanged;
        }

        handler?.Invoke(oldState, RecordingSessionState.Idle);
    }
}
