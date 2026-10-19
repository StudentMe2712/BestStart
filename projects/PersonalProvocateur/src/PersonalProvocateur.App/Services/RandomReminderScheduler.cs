using Timer = System.Threading.Timer;

namespace PersonalProvocateur.App.Services;

/// <summary>
/// Manages the randomized timing intervals for triggering provocations.
/// Handles system suspend, screen lock, and pause transitions.
/// </summary>
public class RandomReminderScheduler : IDisposable
{
    private readonly Random _random;
    private readonly Action<Action, TimeSpan>? _timerScheduler;
    private readonly Timer? _timer;
    private readonly object _lock = new();

    private bool _isSessionActive;
    private bool _isPaused;
    private bool _isRunning;
    private DateTime? _nextTriggerTimeLocal;
    private TimeSpan? _currentScheduledInterval;

    /// <summary>
    /// Occurs when the scheduled random reminder time arrives.
    /// </summary>
    public event EventHandler? TimerTriggered;

    /// <summary>
    /// Gets the next scheduled local trigger time, or null if idle or paused.
    /// </summary>
    public DateTime? NextTriggerTimeLocal
    {
        get
        {
            lock (_lock)
            {
                return _nextTriggerTimeLocal;
            }
        }
        private set
        {
            lock (_lock)
            {
                _nextTriggerTimeLocal = value;
            }
        }
    }

    /// <summary>
    /// Gets the next scheduled local trigger time.
    /// </summary>
    public DateTime? NextTriggerTime => NextTriggerTimeLocal;

    /// <summary>
    /// Updates interval bounds and reschedules the next reminder cycle.
    /// </summary>
    public void UpdateIntervals(int minMinutes, int maxMinutes)
    {
        StartNextCycle(minMinutes, maxMinutes);
    }

    /// <summary>
    /// Gets or sets whether a provocation session (mascot or video) is currently active on screen.
    /// While true, new scheduled cycles will not start.
    /// </summary>
    public bool IsSessionActive
    {
        get
        {
            lock (_lock)
            {
                return _isSessionActive;
            }
        }
        set
        {
            lock (_lock)
            {
                _isSessionActive = value;
            }
        }
    }

    /// <summary>
    /// Gets whether reminders are currently paused by the user.
    /// </summary>
    public bool IsPaused
    {
        get
        {
            lock (_lock)
            {
                return _isPaused;
            }
        }
        private set
        {
            lock (_lock)
            {
                _isPaused = value;
            }
        }
    }

    /// <summary>
    /// Gets whether a reminder cycle is currently actively counting down.
    /// </summary>
    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                return _isRunning;
            }
        }
        private set
        {
            lock (_lock)
            {
                _isRunning = value;
            }
        }
    }

    /// <summary>
    /// Gets the most recently scheduled interval duration.
    /// </summary>
    public TimeSpan? CurrentScheduledInterval
    {
        get
        {
            lock (_lock)
            {
                return _currentScheduledInterval;
            }
        }
        private set
        {
            lock (_lock)
            {
                _currentScheduledInterval = value;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of <see cref="RandomReminderScheduler"/>.
    /// </summary>
    /// <param name="random">Optional custom Random instance for deterministic testing.</param>
    /// <param name="timerScheduler">Optional custom scheduler delegate for mocking timer execution in tests.</param>
    public RandomReminderScheduler(Random? random = null, Action<Action, TimeSpan>? timerScheduler = null)
    {
        _random = random ?? new Random();
        _timerScheduler = timerScheduler;

        if (_timerScheduler == null)
        {
            _timer = new Timer(_ => OnTimerElapsed(), null, Timeout.Infinite, Timeout.Infinite);
        }
    }

    /// <summary>
    /// Starts a new cycle with a random interval between minMinutes and maxMinutes.
    /// Does nothing if a session is currently active or reminders are paused.
    /// </summary>
    public void StartNextCycle(int minMinutes, int maxMinutes)
    {
        TimeSpan interval;

        lock (_lock)
        {
            if (_isSessionActive || _isPaused)
            {
                return;
            }

            int minSeconds = Math.Max(1, minMinutes * 60);
            int maxSeconds = Math.Max(minSeconds, maxMinutes * 60);
            int randomSeconds = _random.Next(minSeconds, maxSeconds + 1);
            interval = TimeSpan.FromSeconds(randomSeconds);

            _currentScheduledInterval = interval;
            _nextTriggerTimeLocal = DateTime.Now.Add(interval);
            _isRunning = true;

            if (_timerScheduler != null)
            {
                _timerScheduler(OnTimerElapsed, interval);
            }
            else
            {
                _timer?.Change(interval, Timeout.InfiniteTimeSpan);
            }
        }
    }

    /// <summary>
    /// Stops the current running timer and resets scheduled trigger time.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            _nextTriggerTimeLocal = null;
            _currentScheduledInterval = null;
            _isRunning = false;
        }
    }

    /// <summary>
    /// Sets paused state. When paused, stops timer. When unpaused, starts a fresh cycle if no session is active.
    /// </summary>
    public void SetPaused(bool paused, int minMinutes, int maxMinutes)
    {
        lock (_lock)
        {
            _isPaused = paused;
        }

        if (paused)
        {
            Stop();
        }
        else
        {
            lock (_lock)
            {
                if (_isSessionActive)
                {
                    return;
                }
            }

            StartNextCycle(minMinutes, maxMinutes);
        }
    }

    /// <summary>
    /// Halts timer during system suspend or screen lock to prevent immediate popups on unlock.
    /// </summary>
    public void OnSystemSuspendOrLock()
    {
        Stop();
    }

    /// <summary>
    /// Schedules a fresh random interval when the system resumes or is unlocked.
    /// </summary>
    public void OnSystemResumeOrUnlock(int minMinutes, int maxMinutes)
    {
        lock (_lock)
        {
            if (_isPaused || _isSessionActive)
            {
                return;
            }
        }

        StartNextCycle(minMinutes, maxMinutes);
    }

    /// <summary>
    /// Completes the active provocation session and starts the next countdown cycle.
    /// </summary>
    public void CompleteSession(int minMinutes, int maxMinutes)
    {
        lock (_lock)
        {
            _isSessionActive = false;
        }

        StartNextCycle(minMinutes, maxMinutes);
    }

    /// <summary>
    /// Instantly triggers the timer event for testing purposes.
    /// </summary>
    public void TriggerNowForTesting()
    {
        lock (_lock)
        {
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            _isRunning = false;
            _nextTriggerTimeLocal = null;
        }

        TimerTriggered?.Invoke(this, EventArgs.Empty);
    }

    private void OnTimerElapsed()
    {
        lock (_lock)
        {
            if (!_isRunning || _isPaused || _isSessionActive)
            {
                return;
            }

            _isRunning = false;
            _nextTriggerTimeLocal = null;
        }

        TimerTriggered?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Disposes the underlying timer.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _isRunning = false;
            _nextTriggerTimeLocal = null;
        }

        GC.SuppressFinalize(this);
    }
}
