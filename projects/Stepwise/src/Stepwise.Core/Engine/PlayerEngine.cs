namespace Stepwise.Core.Engine;

using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;

/// <summary>
/// Потокобезопасная реализация движка воспроизведения и навигации по шагам инструкции.
/// Строго read-only по отношению к шагам инструкции.
/// </summary>
public sealed class PlayerEngine : IPlayerEngine
{
    private readonly object _syncLock = new();
    private readonly PlayerStateMachine _stateMachine;
    private IReadOnlyList<Step> _steps = Array.Empty<Step>();
    private Guid? _currentGuideId;
    private int _currentIndex = -1;

    /// <summary>
    /// Инициализирует новый экземпляр движка воспроизведения.
    /// </summary>
    /// <param name="stateMachine">Опциональный конечный автомат состояний (для тестов или кастомной конфигурации).</param>
    public PlayerEngine(PlayerStateMachine? stateMachine = null)
    {
        _stateMachine = stateMachine ?? new PlayerStateMachine();
        _stateMachine.StateChanged += OnStateMachineStateChanged;
    }

    /// <inheritdoc />
    public PlayerState State => _stateMachine.CurrentState;

    /// <inheritdoc />
    public int CurrentIndex
    {
        get
        {
            lock (_syncLock)
            {
                return _currentIndex;
            }
        }
    }

    /// <inheritdoc />
    public int TotalSteps
    {
        get
        {
            lock (_syncLock)
            {
                return _steps.Count;
            }
        }
    }

    /// <inheritdoc />
    public Step? CurrentStep
    {
        get
        {
            lock (_syncLock)
            {
                return (_currentIndex >= 0 && _currentIndex < _steps.Count)
                    ? _steps[_currentIndex]
                    : null;
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<Step> Steps
    {
        get
        {
            lock (_syncLock)
            {
                return _steps;
            }
        }
    }

    /// <inheritdoc />
    public Guid? CurrentGuideId
    {
        get
        {
            lock (_syncLock)
            {
                return _currentGuideId;
            }
        }
    }

    /// <inheritdoc />
    public bool CanNext
    {
        get
        {
            lock (_syncLock)
            {
                return _steps.Count > 0 && !_stateMachine.IsCompleted && !_stateMachine.IsFailed;
            }
        }
    }

    /// <inheritdoc />
    public bool CanPrevious
    {
        get
        {
            lock (_syncLock)
            {
                return _steps.Count > 0 && _currentIndex > 0 && !_stateMachine.IsFailed;
            }
        }
    }

    /// <inheritdoc />
    public bool CanRestart
    {
        get
        {
            lock (_syncLock)
            {
                return _steps.Count > 0 && !_stateMachine.IsFailed;
            }
        }
    }

    /// <inheritdoc />
    public bool CanPlay
    {
        get
        {
            lock (_syncLock)
            {
                return _steps.Count > 0 && (_stateMachine.IsIdle || _stateMachine.IsPaused || _stateMachine.IsCompleted);
            }
        }
    }

    /// <inheritdoc />
    public bool CanPause
    {
        get
        {
            lock (_syncLock)
            {
                return _stateMachine.IsPlaying;
            }
        }
    }

    /// <inheritdoc />
    public bool CanFirst
    {
        get
        {
            lock (_syncLock)
            {
                return _steps.Count > 0 && !_stateMachine.IsFailed && (_currentIndex > 0 || _stateMachine.IsCompleted);
            }
        }
    }

    /// <inheritdoc />
    public bool CanLast
    {
        get
        {
            lock (_syncLock)
            {
                return _steps.Count > 0 && !_stateMachine.IsFailed && _currentIndex < _steps.Count - 1;
            }
        }
    }

    /// <inheritdoc />
    public event PlayerStateChangedHandler? StateChanged;

    /// <inheritdoc />
    public event Action<int, Step?>? StepChanged;

    /// <inheritdoc />
    public bool LoadGuide(Guid guideId, IReadOnlyList<Step>? steps)
    {
        int newIndex;
        Step? newStep;

        lock (_syncLock)
        {
            if (guideId == Guid.Empty || steps == null || steps.Count == 0)
            {
                _steps = Array.Empty<Step>();
                _currentGuideId = guideId == Guid.Empty ? null : guideId;
                _currentIndex = -1;
                _stateMachine.ResetToIdle();
                _stateMachine.Session = null;
                newIndex = -1;
                newStep = null;
            }
            else
            {
                _currentGuideId = guideId;
                _steps = steps.ToList().AsReadOnly();
                _currentIndex = 0;
                _stateMachine.ResetToIdle();
                _stateMachine.Session = new PlayerSession(guideId, 0, _steps.Count, PlayerState.Idle);
                newIndex = 0;
                newStep = _steps[0];
            }
        }

        StepChanged?.Invoke(newIndex, newStep);
        return newIndex != -1;
    }

    /// <inheritdoc />
    public void Unload()
    {
        lock (_syncLock)
        {
            _steps = Array.Empty<Step>();
            _currentGuideId = null;
            _currentIndex = -1;
            _stateMachine.ResetToIdle();
            _stateMachine.Session = null;
        }

        StepChanged?.Invoke(-1, null);
    }

    /// <inheritdoc />
    public bool Play()
    {
        lock (_syncLock)
        {
            if (_steps.Count == 0 || _stateMachine.IsFailed)
            {
                return false;
            }

            if (_stateMachine.IsPlaying)
            {
                return false;
            }

            if (_stateMachine.IsIdle || _stateMachine.IsPaused || _stateMachine.IsCompleted)
            {
                return _stateMachine.TryTransition(PlayerState.Playing, out _);
            }

            return false;
        }
    }

    /// <inheritdoc />
    public bool Pause()
    {
        lock (_syncLock)
        {
            if (!_stateMachine.IsPlaying)
            {
                return false;
            }

            return _stateMachine.TryTransition(PlayerState.Paused, out _);
        }
    }

    /// <inheritdoc />
    public bool Resume()
    {
        lock (_syncLock)
        {
            if (!_stateMachine.IsPaused)
            {
                return false;
            }

            return _stateMachine.TryTransition(PlayerState.Playing, out _);
        }
    }

    /// <inheritdoc />
    public bool Next()
    {
        int newIndex = -1;
        Step? newStep = null;
        bool stepChanged = false;

        lock (_syncLock)
        {
            if (_steps.Count == 0 || _stateMachine.IsFailed || _stateMachine.IsCompleted)
            {
                return false;
            }

            if (_currentIndex < _steps.Count - 1)
            {
                _currentIndex++;
                newIndex = _currentIndex;
                newStep = _steps[_currentIndex];
                stepChanged = true;
                UpdateSessionUnderLock();
            }
            else
            {
                if (_stateMachine.IsIdle)
                {
                    _stateMachine.TryTransition(PlayerState.Playing, out _);
                }

                _stateMachine.TryTransition(PlayerState.Completed, out _);
                UpdateSessionUnderLock();
            }
        }

        if (stepChanged)
        {
            StepChanged?.Invoke(newIndex, newStep);
        }

        return true;
    }

    /// <inheritdoc />
    public bool Previous()
    {
        int newIndex;
        Step? newStep;

        lock (_syncLock)
        {
            if (_steps.Count == 0 || _stateMachine.IsFailed || _currentIndex <= 0)
            {
                return false;
            }

            _currentIndex--;
            newIndex = _currentIndex;
            newStep = _steps[_currentIndex];

            if (_stateMachine.IsCompleted)
            {
                _stateMachine.TryTransition(PlayerState.Playing, out _);
            }

            UpdateSessionUnderLock();
        }

        StepChanged?.Invoke(newIndex, newStep);
        return true;
    }

    /// <inheritdoc />
    public bool First()
    {
        int newIndex;
        Step? newStep;

        lock (_syncLock)
        {
            if (_steps.Count == 0 || _stateMachine.IsFailed)
            {
                return false;
            }

            _currentIndex = 0;
            newIndex = 0;
            newStep = _steps[0];

            if (_stateMachine.IsCompleted)
            {
                _stateMachine.TryTransition(PlayerState.Playing, out _);
            }

            UpdateSessionUnderLock();
        }

        StepChanged?.Invoke(newIndex, newStep);
        return true;
    }

    /// <inheritdoc />
    public bool Last()
    {
        int newIndex;
        Step? newStep;

        lock (_syncLock)
        {
            if (_steps.Count == 0 || _stateMachine.IsFailed)
            {
                return false;
            }

            _currentIndex = _steps.Count - 1;
            newIndex = _currentIndex;
            newStep = _steps[_currentIndex];

            UpdateSessionUnderLock();
        }

        StepChanged?.Invoke(newIndex, newStep);
        return true;
    }

    /// <inheritdoc />
    public bool Restart()
    {
        int newIndex;
        Step? newStep;

        lock (_syncLock)
        {
            if (_steps.Count == 0)
            {
                return false;
            }

            _currentIndex = 0;
            newIndex = 0;
            newStep = _steps[0];

            if (_stateMachine.IsFailed)
            {
                _stateMachine.ResetToIdle();
            }

            if (_stateMachine.IsIdle || _stateMachine.IsPaused || _stateMachine.IsCompleted)
            {
                _stateMachine.TryTransition(PlayerState.Playing, out _);
            }

            UpdateSessionUnderLock();
        }

        StepChanged?.Invoke(newIndex, newStep);
        return true;
    }

    /// <inheritdoc />
    public void Fail(string reason)
    {
        lock (_syncLock)
        {
            if (_stateMachine.IsFailed)
            {
                return;
            }

            if (_stateMachine.IsIdle || _stateMachine.IsCompleted)
            {
                _stateMachine.TryTransition(PlayerState.Playing, out _);
            }

            _stateMachine.SetFailed(reason);
            UpdateSessionUnderLock();
        }
    }

    private void OnStateMachineStateChanged(PlayerState oldState, PlayerState newState)
    {
        StateChanged?.Invoke(oldState, newState);
    }

    private void UpdateSessionUnderLock()
    {
        if (_currentGuideId.HasValue && _steps.Count > 0)
        {
            var existing = _stateMachine.Session;
            _stateMachine.Session = new PlayerSession(
                GuideId: _currentGuideId.Value,
                CurrentIndex: _currentIndex,
                TotalSteps: _steps.Count,
                State: _stateMachine.CurrentState,
                StartedAt: existing?.StartedAt,
                CompletedAt: existing?.CompletedAt
            );
        }
    }
}