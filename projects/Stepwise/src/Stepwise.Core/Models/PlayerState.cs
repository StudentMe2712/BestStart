namespace Stepwise.Core.Models;

/// <summary>
/// Состояние воспроизведения интерактивной инструкции.
/// </summary>
public enum PlayerState
{
    Idle,
    Playing,
    Paused,
    Completed,
    Failed
}

/// <summary>
/// Снимок состояния сессии воспроизведения инструкции.
/// </summary>
public sealed record PlayerSession(
    Guid GuideId,
    int CurrentIndex,
    int TotalSteps,
    PlayerState State,
    DateTimeOffset? StartedAt = null,
    DateTimeOffset? CompletedAt = null
);

/// <summary>
/// Делегат для события изменения состояния воспроизведения.
/// </summary>
public delegate void PlayerStateChangedHandler(PlayerState oldState, PlayerState newState);

/// <summary>
/// Потокобезопасный конечный автомат состояний воспроизведения инструкции.
/// </summary>
public sealed class PlayerStateMachine
{
    private readonly object _syncLock = new();
    private PlayerState _currentState = PlayerState.Idle;
    private PlayerSession? _session;
    private string? _lastError;

    /// <summary>
    /// Инициализирует новый экземпляр конечного автомата воспроизведения.
    /// </summary>
    public PlayerStateMachine(PlayerSession? session = null)
    {
        _session = session;
        _currentState = session?.State ?? PlayerState.Idle;
    }

    /// <summary>
    /// Текущее состояние воспроизведения.
    /// </summary>
    public PlayerState CurrentState
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
    /// Текущие метаданные сессии воспроизведения.
    /// </summary>
    public PlayerSession? Session
    {
        get
        {
            lock (_syncLock)
            {
                return _session;
            }
        }
        set
        {
            lock (_syncLock)
            {
                _session = value;
            }
        }
    }

    /// <summary>
    /// Последнее зарегистрированное сообщение об ошибке (при переходе в Failed).
    /// </summary>
    public string? LastError
    {
        get
        {
            lock (_syncLock)
            {
                return _lastError;
            }
        }
    }

    /// <summary>
    /// Событие смены состояния воспроизведения.
    /// </summary>
    public event PlayerStateChangedHandler? StateChanged;

    /// <summary>
    /// Воспроизведение находится в исходном/неактивном состоянии.
    /// </summary>
    public bool IsIdle => CurrentState == PlayerState.Idle;

    /// <summary>
    /// Инструкция находится в процессе активного воспроизведения.
    /// </summary>
    public bool IsPlaying => CurrentState == PlayerState.Playing;

    /// <summary>
    /// Воспроизведение приостановлено пользователем.
    /// </summary>
    public bool IsPaused => CurrentState == PlayerState.Paused;

    /// <summary>
    /// Воспроизведение всех шагов успешно завершено.
    /// </summary>
    public bool IsCompleted => CurrentState == PlayerState.Completed;

    /// <summary>
    /// Воспроизведение прервано ошибкой.
    /// </summary>
    public bool IsFailed => CurrentState == PlayerState.Failed;

    /// <summary>
    /// Проверяет допустимость перехода между двумя состояниями.
    /// </summary>
    public static bool IsValidTransition(PlayerState from, PlayerState to)
    {
        if (from == to)
        {
            return false;
        }

        return (from, to) switch
        {
            (PlayerState.Idle, PlayerState.Playing) => true,
            (PlayerState.Playing, PlayerState.Paused) => true,
            (PlayerState.Paused, PlayerState.Playing) => true,
            (PlayerState.Playing, PlayerState.Completed) => true,
            (PlayerState.Paused, PlayerState.Completed) => true,
            (PlayerState.Playing, PlayerState.Failed) => true,
            (PlayerState.Paused, PlayerState.Failed) => true,
            (PlayerState.Completed, PlayerState.Playing) => true,
            (_, PlayerState.Idle) => true,
            _ => false
        };
    }

    /// <summary>
    /// Проверяет, возможен ли переход из текущего состояния в целевое.
    /// </summary>
    public bool CanTransitionTo(PlayerState targetState)
    {
        lock (_syncLock)
        {
            return IsValidTransition(_currentState, targetState);
        }
    }

    /// <summary>
    /// Пытается выполнить потокобезопасный переход в целевое состояние.
    /// </summary>
    /// <param name="targetState">Целевое состояние воспроизведения.</param>
    /// <param name="error">Сообщение об ошибке, если переход недопустим.</param>
    /// <returns><c>true</c>, если переход успешно совершен; иначе <c>false</c>.</returns>
    public bool TryTransition(PlayerState targetState, out string? error)
    {
        PlayerStateChangedHandler? handler;
        PlayerState oldState;

        lock (_syncLock)
        {
            oldState = _currentState;
            if (!IsValidTransition(oldState, targetState))
            {
                error = $"Invalid state transition from {oldState} to {targetState}.";
                return false;
            }

            _currentState = targetState;
            if (_session != null)
            {
                _session = _session with
                {
                    State = targetState,
                    StartedAt = targetState == PlayerState.Playing ? (_session.StartedAt ?? DateTimeOffset.UtcNow) : _session.StartedAt,
                    CompletedAt = targetState == PlayerState.Completed ? (_session.CompletedAt ?? DateTimeOffset.UtcNow) : _session.CompletedAt
                };
            }
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
    public void TransitionOrThrow(PlayerState targetState)
    {
        if (!TryTransition(targetState, out var error))
        {
            throw new InvalidOperationException(error);
        }
    }

    /// <summary>
    /// Сбрасывает состояние в <see cref="PlayerState.Idle"/> из любого состояния.
    /// Если автомат уже в состоянии Idle, вызов не производит действий.
    /// </summary>
    public void ResetToIdle()
    {
        PlayerStateChangedHandler? handler = null;
        PlayerState oldState;

        lock (_syncLock)
        {
            oldState = _currentState;
            if (oldState == PlayerState.Idle)
            {
                return;
            }

            _currentState = PlayerState.Idle;
            _lastError = null;
            if (_session != null)
            {
                _session = _session with { State = PlayerState.Idle };
            }
            handler = StateChanged;
        }

        handler?.Invoke(oldState, PlayerState.Idle);
    }

    /// <summary>
    /// Переводит автомат в состояние ошибки с сохранением диагностической информации.
    /// </summary>
    /// <param name="error">Сообщение об ошибке или причина сбоя.</param>
    public void SetFailed(string? error)
    {
        PlayerStateChangedHandler? handler = null;
        PlayerState oldState;

        lock (_syncLock)
        {
            oldState = _currentState;
            if (oldState == PlayerState.Failed)
            {
                _lastError = error;
                return;
            }

            if (!IsValidTransition(oldState, PlayerState.Failed))
            {
                throw new InvalidOperationException($"Cannot transition from {oldState} to {PlayerState.Failed}. Error: {error}");
            }

            _currentState = PlayerState.Failed;
            _lastError = error;
            if (_session != null)
            {
                _session = _session with
                {
                    State = PlayerState.Failed,
                    CompletedAt = _session.CompletedAt ?? DateTimeOffset.UtcNow
                };
            }
            handler = StateChanged;
        }

        handler?.Invoke(oldState, PlayerState.Failed);
    }
}