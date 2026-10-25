using System.IO;
using System.Windows;
using PersonalProvocateur.App.Models;
using PersonalProvocateur.App.Storage;
using PersonalProvocateur.App.UI;
using PersonalProvocateur.App.WindowsInterop;

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
    private readonly Func<string, bool> _fileExistsChecker;
    private readonly Func<System.Windows.Forms.Screen[]> _screensProvider;
    private readonly Func<OverlayCoordinator, bool, OverlayWindow?> _overlayWindowFactory;
    private readonly Func<OverlayCoordinator, VideoWindow?> _videoWindowFactory;
    private readonly Action<Action> _dispatcherInvoker;
    private readonly Action _memoryTrimmer;
    private readonly List<OverlayWindow> _activeOverlayWindows = new();
    private readonly object _lock = new();
    private bool _isManualTestSession;

    /// <summary>
    /// Gets whether the current provocation session was triggered in manual test mode.
    /// </summary>
    public bool IsManualTestSession => _isManualTestSession;

    /// <summary>
    /// Gets the scheduler instance.
    /// </summary>
    public RandomReminderScheduler Scheduler => _scheduler;

    /// <summary>
    /// Gets the video library service.
    /// </summary>
    public VideoLibraryService VideoLibrary => _videoLibrary;

    /// <summary>
    /// Gets the current session state.
    /// </summary>
    public SessionState CurrentState { get; private set; }

    /// <summary>
    /// Occurs when the session state changes.
    /// </summary>
    public event EventHandler<SessionState>? StateChanged;

    /// <summary>
    /// Gets the primary active overlay window, if open.
    /// </summary>
    public OverlayWindow? ActiveOverlayWindow { get; private set; }

    /// <summary>
    /// Gets the collection of all currently active overlay windows across all monitors.
    /// </summary>
    public IReadOnlyList<OverlayWindow> ActiveOverlayWindows => _activeOverlayWindows;

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
    /// <param name="fileExistsChecker">Optional file existence checker function.</param>
    /// <param name="screensProvider">Optional provider for connected display screens.</param>
    /// <param name="multiOverlayWindowFactory">Optional multi-monitor overlay window factory delegate.</param>
    /// <param name="memoryTrimmer">Optional memory trimming action for testing.</param>
    public OverlayCoordinator(
        RandomReminderScheduler scheduler,
        VideoLibraryService videoLibrary,
        SettingsStore settingsStore,
        Func<OverlayCoordinator, OverlayWindow?>? overlayWindowFactory = null,
        Func<OverlayCoordinator, VideoWindow?>? videoWindowFactory = null,
        Action<Action>? dispatcherInvoker = null,
        Func<string, bool>? fileExistsChecker = null,
        Func<System.Windows.Forms.Screen[]>? screensProvider = null,
        Func<OverlayCoordinator, bool, OverlayWindow?>? multiOverlayWindowFactory = null,
        Action? memoryTrimmer = null)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _videoLibrary = videoLibrary ?? throw new ArgumentNullException(nameof(videoLibrary));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _fileExistsChecker = fileExistsChecker ?? File.Exists;
        _screensProvider = screensProvider ?? (() => System.Windows.Forms.Screen.AllScreens);
        _memoryTrimmer = memoryTrimmer ?? MemoryOptimizer.TrimProcessMemory;

        if (multiOverlayWindowFactory != null)
        {
            _overlayWindowFactory = multiOverlayWindowFactory;
        }
        else if (overlayWindowFactory != null)
        {
            _overlayWindowFactory = (coord, isDimmerOnly) =>
                isDimmerOnly ? new OverlayWindow(coord, _settingsStore, true) : overlayWindowFactory(coord);
        }
        else
        {
            _overlayWindowFactory = (coord, isDimmerOnly) => new OverlayWindow(coord, _settingsStore, isDimmerOnly);
        }

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

            if (settings.TriggerMode == "SmartOnly")
            {
                _scheduler.Stop();
            }
            else if (!settings.IsPaused && !_scheduler.IsRunning && !_scheduler.IsSessionActive && CurrentState == SessionState.BackgroundIdle)
            {
                var min = settings.MinIntervalMinutes;
                var max = settings.MaxIntervalMinutes;
                _scheduler.StartNextCycle(min, max);
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

                _isManualTestSession = isManualTest;
                _scheduler.Stop();
                _scheduler.IsSessionActive = true;

                TransitionTo(SessionState.MascotOverlayActive);

                var screens = _screensProvider() ?? System.Windows.Forms.Screen.AllScreens;
                if (screens.Length == 0)
                {
                    screens = System.Windows.Forms.Screen.AllScreens;
                }

                int targetIdx = Math.Clamp(_settingsStore.CurrentSettings.TargetMonitorIndex, 0, Math.Max(0, screens.Length - 1));

                var primaryScreen = screens.Length > 0 ? screens[targetIdx] : null;
                var primaryOverlay = _overlayWindowFactory(this, false);
                if (primaryOverlay != null)
                {
                    if (primaryScreen != null)
                    {
                        primaryOverlay.PositionOnScreen(primaryScreen);
                    }
                    ActiveOverlayWindow = primaryOverlay;
                    _activeOverlayWindows.Add(primaryOverlay);
                    primaryOverlay.Show();

                    if (screens.Length > 1)
                    {
                        for (int i = 0; i < screens.Length; i++)
                        {
                            if (i == targetIdx)
                            {
                                continue;
                            }

                            var secondaryOverlay = _overlayWindowFactory(this, true);
                            if (secondaryOverlay != null)
                            {
                                secondaryOverlay.PositionOnScreen(screens[i]);
                                _activeOverlayWindows.Add(secondaryOverlay);
                                secondaryOverlay.Show();
                            }
                        }
                    }
                }
            }
        });
    }

    /// <summary>
    /// Finds the next playable item (video or text) that exists, skipping broken/missing files up to PoolCount attempts.
    /// </summary>
    /// <param name="excludePath">Optional file path to exclude (e.g. video that just failed playback).</param>
    /// <returns>Next playable VideoItem, or null if all available items are invalid or missing.</returns>
    public VideoItem? FindNextPlayableVideo(string? excludePath = null)
    {
        int poolCount = _videoLibrary.ShuffleBag.PoolCount;
        int allCount = _videoLibrary.GetAllVideos().Count;
        int maxAttempts = Math.Max(3, Math.Max(poolCount, allCount) + 1);

        for (int i = 0; i < maxAttempts; i++)
        {
            var candidate = _videoLibrary.GetNextVideo();
            if (candidate == null)
            {
                break;
            }

            if (!string.IsNullOrWhiteSpace(excludePath) &&
                string.Equals(candidate.FilePath, excludePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (candidate.ContentType == ProvocationContentType.Text)
            {
                if (!string.IsNullOrWhiteSpace(candidate.TextContent))
                {
                    return candidate;
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(candidate.FilePath) && _fileExistsChecker(candidate.FilePath))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Handles user click on the provocateur mascot.
    /// Transitions state to VideoPlaying and opens the video window or Spotlight text card.
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

                var nextItem = FindNextPlayableVideo();
                if (nextItem == null)
                {
                    // Video library is empty or all files are missing
                    ActiveOverlayWindow?.ShowEmptyLibraryNotice();
                    return;
                }

                if (nextItem.ContentType == ProvocationContentType.Text)
                {
                    TransitionTo(SessionState.VideoPlaying);
                    ActiveOverlayWindow?.ShowSpotlightText(nextItem.TextContent);
                }
                else
                {
                    TransitionTo(SessionState.VideoPlaying);

                    ActiveVideoWindow = _videoWindowFactory(this);
                    if (ActiveOverlayWindow != null && ActiveVideoWindow != null && ActiveOverlayWindow.IsVisible)
                    {
                        try
                        {
                            ActiveVideoWindow.Owner = ActiveOverlayWindow;
                        }
                        catch
                        {
                            // Suppress if owner window is not visible in test/mock environment
                        }
                    }
                    ActiveVideoWindow?.Show();
                    ActiveVideoWindow?.PlayVideo(nextItem.FilePath, nextItem.Title, _isManualTestSession);
                }
            }
        });
    }

    /// <summary>
    /// Handles user clicking on the backdrop/dimming layer outside the video player during video playback.
    /// Enforces Strict Mandatory Watch Lock (ADR-010): does NOT close any window, refocuses the video window,
    /// and triggers visual warning pulse on the player.
    /// </summary>
    public void OnBackdropClickedDuringVideo()
    {
        _dispatcherInvoker(() =>
        {
            lock (_lock)
            {
                if (CurrentState == SessionState.VideoPlaying)
                {
                    ActiveVideoWindow?.Activate();
                    ActiveVideoWindow?.PulseMandatoryWatchWarning();
                }
            }
        });
    }

    /// <summary>
    /// Handles media playback failure (e.g. corrupt codec or unreadable file).
    /// Attempts to automatically switch to the next valid video in the shuffle bag.
    /// If all videos fail, displays notice and closes cleanly without crashing.
    /// </summary>
    /// <param name="failedPath">Path to the video file that failed playback.</param>
    public void OnVideoPlaybackFailed(string failedPath)
    {
        _dispatcherInvoker(() =>
        {
            lock (_lock)
            {
                if (CurrentState != SessionState.VideoPlaying)
                {
                    return;
                }

                var nextVideo = FindNextPlayableVideo(failedPath);
                if (nextVideo != null && !string.IsNullOrWhiteSpace(nextVideo.FilePath))
                {
                    ActiveVideoWindow?.PlayVideo(nextVideo.FilePath, nextVideo.Title, _isManualTestSession);
                }
                else
                {
                    // All videos are exhausted or unplayable
                    if (ActiveVideoWindow != null)
                    {
                        ActiveVideoWindow.AllowClose = true;
                        try
                        {
                            ActiveVideoWindow.Close();
                        }
                        catch
                        {
                        }
                        ActiveVideoWindow = null;
                    }

                    if (ActiveOverlayWindow != null)
                    {
                        ActiveOverlayWindow.ShowEmptyLibraryNotice();
                    }
                    else
                    {
                        CompleteSession();
                    }
                }
            }
        });
    }

    /// <summary>
    /// Completes the active session, closes all overlay/video windows,
    /// trims process working set memory, sets state to BackgroundIdle, and restarts the reminder schedule.
    /// </summary>
    public void CompleteSession()
    {
        _dispatcherInvoker(() =>
        {
            lock (_lock)
            {
                CloseAllWindows();
                _memoryTrimmer();

                _scheduler.IsSessionActive = false;
                TransitionTo(SessionState.BackgroundIdle);

                if (_settingsStore.CurrentSettings.TriggerMode != "SmartOnly")
                {
                    var min = _settingsStore.CurrentSettings.MinIntervalMinutes;
                    var max = _settingsStore.CurrentSettings.MaxIntervalMinutes;
                    _scheduler.StartNextCycle(min, max);
                }
            }
        });
    }

    /// <summary>
    /// Instantly dismisses all active provocation windows on emergency panic hotkey,
    /// trims process memory, returns state to BackgroundIdle, and schedules a fresh cycle.
    /// </summary>
    public void EmergencyDismiss()
    {
        _dispatcherInvoker(() =>
        {
            lock (_lock)
            {
                CloseAllWindows();
                _memoryTrimmer();

                _scheduler.IsSessionActive = false;
                TransitionTo(SessionState.BackgroundIdle);

                if (_settingsStore.CurrentSettings.TriggerMode != "SmartOnly")
                {
                    var min = _settingsStore.CurrentSettings.MinIntervalMinutes;
                    var max = _settingsStore.CurrentSettings.MaxIntervalMinutes;
                    _scheduler.StartNextCycle(min, max);
                }
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

        if (_settingsStore.CurrentSettings.TriggerMode != "SmartOnly")
        {
            var min = _settingsStore.CurrentSettings.MinIntervalMinutes;
            var max = _settingsStore.CurrentSettings.MaxIntervalMinutes;
            _scheduler.SetPaused(paused, min, max);
        }
        else
        {
            _scheduler.Stop();
        }

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
            ActiveVideoWindow.AllowClose = true;
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

        foreach (var overlay in _activeOverlayWindows)
        {
            overlay.CanClose = true;
            try
            {
                overlay.SafeClose();
            }
            catch
            {
                // Suppress window close exceptions
            }
        }
        _activeOverlayWindows.Clear();

        if (ActiveOverlayWindow != null)
        {
            ActiveOverlayWindow.CanClose = true;
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
