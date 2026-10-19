using System.Windows;
using PersonalProvocateur.App.Models;
using PersonalProvocateur.App.Storage;
using PersonalProvocateur.App.UI;

namespace PersonalProvocateur.App.Services;

/// <summary>
/// Orchestrates the entire provocation session life-cycle and state transitions.
/// Coordinates between scheduler, video library, and overlay windows.
/// </summary>
public class OverlayCoordinator : IDisposable
{
    private readonly RandomReminderScheduler _scheduler;
    private readonly VideoLibraryService _videoLibrary;
    private readonly SettingsStore _settingsStore;
    private readonly Func<OverlayCoordinator, OverlayWindow?> _overlayWindowFactory;
    private readonly Func<OverlayCoordinator, VideoWindow?> _videoWindowFactory;
    private readonly Action<Action> _dispatcherInvoker;
    private readonly object _lock = new();

    /// <summary>
    /// Gets the current session state.
    /// </summary>
    public SessionState CurrentState { get; private set; }

    /// <summary>
    /// Occurs when the session state changes.
    /// </summary>
    public event EventHandler<SessionState>? StateChanged;

    /// <summary>
    /// Gets the currently active overlay window, if open.
    /// </summary>
    public OverlayWindow? ActiveOverlayWindow { get; private set; }

    /// <summary>
    /// Gets the currently active video window, if open.
    /// </summary>
    public VideoWindow? ActiveVideoWindow { get; private set; }

    /// <summary>
    /// Initializes a new instance of <see cref="OverlayCoordinator"/>.
    /// </summary>
    /// <param name="scheduler">Reminder scheduler instance.</param>
    /// <param name="videoLibrary">Video library service.</param>
    /// <param name="settingsStore">Application settings store.</param>
    /// <param name="overlayWindowFactory">Optional factory delegate for creating overlay windows.</param>
    /// <param name="videoWindowFactory">Optional factory delegate for creating video windows.</param>
    /// <param name="dispatcherInvoker">Optional UI dispatcher invoker for testability.</param>
    public OverlayCoordinator(
        RandomReminderScheduler scheduler,
        VideoLibraryService videoLibrary,
        SettingsStore settingsStore,
        Func<OverlayCoordinator, OverlayWindow?>? overlayWindowFactory = null,
        Func<OverlayCoordinator, VideoWindow?>? videoWindowFactory = null,
        Action<Action>? dispatcherInvoker = null)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _videoLibrary = videoLibrary ?? throw new ArgumentNullException(nameof(videoLibrary));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));

        _overlayWindowFactory = overlayWindowFactory ?? (coord => new OverlayWindow(coord, _settingsStore));
        _videoWindowFactory = videoWindowFactory ?? (coord => new VideoWindow(coord, _settingsStore));

        _dispatcherInvoker = dispatcherInvoker ?? new Action<Action>(action =>
        {
            var app = System.Windows.Application.Current;
            if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
            {
                app.Dispatcher.InvokeAsync(action);
            }
            else
            {
                action();
            }
        });

        CurrentState = _settingsStore.CurrentSettings.IsPaused
            ? SessionState.Paused
            : SessionState.BackgroundIdle;

        _settingsStore.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        lock (_lock)
        {
            if (CurrentState == SessionState.BackgroundIdle && settings.IsPaused)
            {
                TransitionTo(SessionState.Paused);
            }
            else if (CurrentState == SessionState.Paused && !settings.IsPaused)
            {
                TransitionTo(SessionState.BackgroundIdle);
            }
        }
    }

    /// <summary>
    /// Triggers a mascot provocation session.
    /// Ignores the call if a session is already active or reminders are paused (unless manual test).
    /// </summary>
    /// <param name="isManualTest">True if invoked manually from settings or tray menu.</param>
    public void TriggerProvocation(bool isManualTest = false)
    {
        _dispatcherInvoker(() =>
        {
            lock (_lock)
            {
                // Prevent duplicate active sessions
                if (CurrentState == SessionState.MascotOverlayActive || CurrentState == SessionState.VideoPlaying)
                {
                    return;
                }

                // If not manual test, must be in BackgroundIdle
                if (CurrentState != SessionState.BackgroundIdle && CurrentState != SessionState.Paused && !isManualTest)
                {
                    return;
                }

                if (CurrentState == SessionState.Paused && !isManualTest)
                {
                    return;
                }

                _scheduler.Stop();
                _scheduler.IsSessionActive = true;

                TransitionTo(SessionState.MascotOverlayActive);

                ActiveOverlayWindow = _overlayWindowFactory(this);
                ActiveOverlayWindow?.Show();
            }
        });
    }

    /// <summary>
    /// Handles user click on the provocateur mascot.
    /// Transitions state to VideoPlaying and opens the video window with the next shuffled clip.
    /// </summary>
    public void OnMascotClicked()
    {
        _dispatcherInvoker(() =>
        {
            lock (_lock)
            {
                if (CurrentState != SessionState.MascotOverlayActive)
                {
                    return;
                }

                var nextVideo = _videoLibrary.GetNextVideo();
                if (nextVideo != null && !string.IsNullOrWhiteSpace(nextVideo.FilePath))
                {
                    TransitionTo(SessionState.VideoPlaying);

                    ActiveVideoWindow = _videoWindowFactory(this);
                    if (ActiveOverlayWindow != null && ActiveVideoWindow != null)
                    {
                        ActiveVideoWindow.Owner = ActiveOverlayWindow;
                    }
                    ActiveVideoWindow?.Show();
                    ActiveVideoWindow?.PlayVideo(nextVideo.FilePath, nextVideo.Title);
                }
                else
                {
                    // Video library is empty or files are missing
                    ActiveOverlayWindow?.ShowEmptyLibraryNotice();
                }
            }
        });
    }

    /// <summary>
    /// Completes the active session, closes all overlay/video windows,
    /// sets state to BackgroundIdle, and restarts the reminder schedule.
    /// </summary>
    public void CompleteSession()
    {
        _dispatcherInvoker(() =>
        {
            lock (_lock)
            {
                CloseAllWindows();

                _scheduler.IsSessionActive = false;
                TransitionTo(SessionState.BackgroundIdle);

                var min = _settingsStore.CurrentSettings.MinIntervalMinutes;
                var max = _settingsStore.CurrentSettings.MaxIntervalMinutes;
                _scheduler.StartNextCycle(min, max);
            }
        });
    }

    /// <summary>
    /// Instantly dismisses all active provocation windows on emergency panic hotkey,
    /// returns state to BackgroundIdle, and schedules a fresh cycle.
    /// </summary>
    public void EmergencyDismiss()
    {
        _dispatcherInvoker(() =>
        {
            lock (_lock)
            {
                CloseAllWindows();

                _scheduler.IsSessionActive = false;
                TransitionTo(SessionState.BackgroundIdle);

                var min = _settingsStore.CurrentSettings.MinIntervalMinutes;
                var max = _settingsStore.CurrentSettings.MaxIntervalMinutes;
                _scheduler.StartNextCycle(min, max);
            }
        });
    }

    /// <summary>
    /// Toggles application pause state.
    /// </summary>
    /// <param name="paused">True to pause; false to resume.</param>
    public void SetPaused(bool paused)
    {
        _settingsStore.Update(s => s.IsPaused = paused);

        var min = _settingsStore.CurrentSettings.MinIntervalMinutes;
        var max = _settingsStore.CurrentSettings.MaxIntervalMinutes;
        _scheduler.SetPaused(paused, min, max);

        lock (_lock)
        {
            if (paused && CurrentState == SessionState.BackgroundIdle)
            {
                TransitionTo(SessionState.Paused);
            }
            else if (!paused && CurrentState == SessionState.Paused)
            {
                TransitionTo(SessionState.BackgroundIdle);
            }
        }
    }

    private void CloseAllWindows()
    {
        if (ActiveVideoWindow != null)
        {
            try
            {
                ActiveVideoWindow.Close();
            }
            catch
            {
                // Suppress window close exceptions
            }
            ActiveVideoWindow = null;
        }

        if (ActiveOverlayWindow != null)
        {
            try
            {
                ActiveOverlayWindow.SafeClose();
            }
            catch
            {
                // Suppress window close exceptions
            }
            ActiveOverlayWindow = null;
        }
    }

    private void TransitionTo(SessionState newState)
    {
        if (CurrentState == newState)
        {
            return;
        }

        CurrentState = newState;
        StateChanged?.Invoke(this, newState);
    }

    /// <summary>
    /// Disposes resources and dismisses any active windows.
    /// </summary>
    public void Dispose()
    {
        _settingsStore.SettingsChanged -= OnSettingsChanged;
        EmergencyDismiss();
        GC.SuppressFinalize(this);
    }
}
