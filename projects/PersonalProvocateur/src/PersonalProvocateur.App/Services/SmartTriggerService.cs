using System.Runtime.InteropServices;
using System.Text;
using PersonalProvocateur.App.Models;
using PersonalProvocateur.App.Storage;
using PersonalProvocateur.App.WindowsInterop;
using Timer = System.Threading.Timer;

namespace PersonalProvocateur.App.Services;

/// <summary>
/// Procrastination and idle detector service that triggers provocations
/// when entertainment applications/tabs are opened or when the system is idle.
/// </summary>
public class SmartTriggerService : IDisposable
{
    private readonly SettingsStore _settingsStore;
    private readonly OverlayCoordinator _overlayCoordinator;
    private readonly Func<string>? _activeWindowTitleProvider;
    private readonly Func<TimeSpan>? _idleTimeProvider;
    private readonly Func<DateTime> _utcNowProvider;
    private readonly object _syncLock = new();

    private Timer? _timer;
    private int _entertainmentActiveSeconds;
    private DateTime? _lastSmartTriggerUtc;
    private DateTime _lastTickTime;
    private bool _isDisposed;

    /// <summary>
    /// Occurs when smart trigger criteria are satisfied and provocation is requested.
    /// </summary>
    public event EventHandler? SmartProvocationRequested;

    /// <summary>
    /// Gets the number of continuous active seconds accumulated on entertainment windows.
    /// </summary>
    public int EntertainmentActiveSeconds
    {
        get
        {
            lock (_syncLock)
            {
                return _entertainmentActiveSeconds;
            }
        }
    }

    /// <summary>
    /// Gets the UTC timestamp of the most recent smart trigger.
    /// </summary>
    public DateTime? LastSmartTriggerUtc
    {
        get
        {
            lock (_syncLock)
            {
                return _lastSmartTriggerUtc;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of <see cref="SmartTriggerService"/>.
    /// </summary>
    /// <param name="settingsStore">Application settings store.</param>
    /// <param name="overlayCoordinator">Overlay coordinator.</param>
    /// <param name="activeWindowTitleProvider">Optional delegate returning foreground window title.</param>
    /// <param name="idleTimeProvider">Optional delegate returning system idle time.</param>
    /// <param name="utcNowProvider">Optional delegate returning current UTC time.</param>
    /// <param name="startTimer">Whether to start the background periodic polling timer immediately.</param>
    public SmartTriggerService(
        SettingsStore settingsStore,
        OverlayCoordinator overlayCoordinator,
        Func<string>? activeWindowTitleProvider = null,
        Func<TimeSpan>? idleTimeProvider = null,
        Func<DateTime>? utcNowProvider = null,
        bool startTimer = true)
    {
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _overlayCoordinator = overlayCoordinator ?? throw new ArgumentNullException(nameof(overlayCoordinator));
        _activeWindowTitleProvider = activeWindowTitleProvider;
        _idleTimeProvider = idleTimeProvider;
        _utcNowProvider = utcNowProvider ?? (() => DateTime.UtcNow);

        _lastTickTime = _utcNowProvider();

        if (startTimer)
        {
            _timer = new Timer(_ => CheckTriggers(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        }
    }

    /// <summary>
    /// Evaluates current window title and system idle state against smart trigger criteria.
    /// </summary>
    /// <param name="explicitElapsedSeconds">Optional explicit elapsed interval for deterministic testing.</param>
    public void CheckTriggers(int? explicitElapsedSeconds = null)
    {
        lock (_syncLock)
        {
            if (_isDisposed)
            {
                return;
            }

            var settings = _settingsStore.CurrentSettings;
            var now = _utcNowProvider();

            int elapsed = explicitElapsedSeconds ?? (int)Math.Max(1, Math.Round((now - _lastTickTime).TotalSeconds));
            _lastTickTime = now;

            // 1. Suppress if paused
            if (settings.IsPaused)
            {
                return;
            }

            // 2. Suppress if session is currently active
            if (_overlayCoordinator.CurrentState == SessionState.MascotOverlayActive ||
                _overlayCoordinator.CurrentState == SessionState.VideoPlaying)
            {
                return;
            }

            // 3. Suppress if trigger mode is RandomTimer
            if (string.Equals(settings.TriggerMode, "RandomTimer", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // 4. Check smart cooldown
            if (_lastSmartTriggerUtc.HasValue)
            {
                var timeSinceLast = now - _lastSmartTriggerUtc.Value;
                if (timeSinceLast < TimeSpan.FromMinutes(settings.SmartCooldownMinutes))
                {
                    return;
                }
            }

            // 5. Entertainment window monitoring
            bool entertainmentTriggered = false;
            if (settings.TriggerOnEntertainment)
            {
                string title = GetActiveWindowTitle();
                if (MatchesEntertainmentKeywords(title, settings.EntertainmentKeywords))
                {
                    _entertainmentActiveSeconds += elapsed;
                    if (_entertainmentActiveSeconds >= settings.EntertainmentGraceSeconds)
                    {
                        entertainmentTriggered = true;
                    }
                }
                else
                {
                    _entertainmentActiveSeconds = 0;
                }
            }
            else
            {
                _entertainmentActiveSeconds = 0;
            }

            // 6. Idle threshold monitoring
            bool idleTriggered = false;
            if (settings.TriggerOnIdle)
            {
                var idle = GetSystemIdleTime();
                if (idle >= TimeSpan.FromMinutes(settings.IdleThresholdMinutes))
                {
                    idleTriggered = true;
                }
            }

            // 7. Fire trigger if criteria met
            if (entertainmentTriggered || idleTriggered)
            {
                _lastSmartTriggerUtc = now;
                _entertainmentActiveSeconds = 0;

                SmartProvocationRequested?.Invoke(this, EventArgs.Empty);
                _overlayCoordinator.TriggerProvocation(isManualTest: false);
            }
        }
    }

    /// <summary>
    /// Gets the duration of user inactivity (idle time) on the system.
    /// </summary>
    public TimeSpan GetSystemIdleTime()
    {
        if (_idleTimeProvider != null)
        {
            return _idleTimeProvider();
        }

        var lii = new NativeMethods.LASTINPUTINFO();
        lii.cbSize = (uint)Marshal.SizeOf<NativeMethods.LASTINPUTINFO>();
        if (NativeMethods.GetLastInputInfo(ref lii))
        {
            uint tickCount = (uint)Environment.TickCount;
            uint idleTicks = unchecked(tickCount - lii.dwTime);
            return TimeSpan.FromMilliseconds(idleTicks);
        }

        return TimeSpan.Zero;
    }

    /// <summary>
    /// Gets the title bar text of the current foreground active window.
    /// </summary>
    public string GetActiveWindowTitle()
    {
        if (_activeWindowTitleProvider != null)
        {
            return _activeWindowTitleProvider();
        }

        IntPtr hWnd = NativeMethods.GetForegroundWindow();
        if (hWnd == IntPtr.Zero)
        {
            return string.Empty;
        }

        int length = NativeMethods.GetWindowTextLength(hWnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>
    /// Checks whether the window title matches any of the entertainment keywords (case-insensitive).
    /// </summary>
    public bool MatchesEntertainmentKeywords(string windowTitle, IEnumerable<string>? keywords)
    {
        if (string.IsNullOrWhiteSpace(windowTitle) || keywords == null)
        {
            return false;
        }

        return keywords.Any(keyword =>
            !string.IsNullOrWhiteSpace(keyword) &&
            windowTitle.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Resets accumulated entertainment seconds and cooldown state.
    /// </summary>
    public void Reset()
    {
        lock (_syncLock)
        {
            _entertainmentActiveSeconds = 0;
            _lastSmartTriggerUtc = null;
            _lastTickTime = _utcNowProvider();
        }
    }

    /// <summary>
    /// Disposes the periodic background timer.
    /// </summary>
    public void Dispose()
    {
        lock (_syncLock)
        {
            _isDisposed = true;
            _timer?.Dispose();
            _timer = null;
        }

        GC.SuppressFinalize(this);
    }
}
