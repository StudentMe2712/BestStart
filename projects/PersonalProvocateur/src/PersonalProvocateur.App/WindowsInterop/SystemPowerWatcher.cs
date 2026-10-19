using Microsoft.Win32;
using PersonalProvocateur.App.Services;
using PersonalProvocateur.App.Storage;

namespace PersonalProvocateur.App.WindowsInterop;

/// <summary>
/// Monitors Windows power and session events (suspend, resume, lock, unlock)
/// to halt reminders when the user is away and reschedule fresh intervals on return.
/// </summary>
public sealed class SystemPowerWatcher : IDisposable
{
    private readonly RandomReminderScheduler _scheduler;
    private readonly SettingsStore _settingsStore;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of <see cref="SystemPowerWatcher"/> and hooks Windows system events.
    /// </summary>
    /// <param name="scheduler">The reminder scheduler instance to control.</param>
    /// <param name="settingsStore">The settings store containing current min/max interval configurations.</param>
    public SystemPowerWatcher(RandomReminderScheduler scheduler, SettingsStore settingsStore)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));

        try
        {
            SystemEvents.SessionSwitch += OnSessionSwitch;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
        }
        catch
        {
            // Suppress initialization exceptions if running in unsupported/headless environments
        }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        HandleSessionSwitch(e.Reason);
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        HandlePowerModeChanged(e.Mode);
    }

    /// <summary>
    /// Handles session switch events. Internal for deterministic testing.
    /// </summary>
    internal void HandleSessionSwitch(SessionSwitchReason reason)
    {
        if (reason == SessionSwitchReason.SessionLock)
        {
            _scheduler.OnSystemSuspendOrLock();
        }
        else if (reason == SessionSwitchReason.SessionUnlock)
        {
            var min = _settingsStore.CurrentSettings.MinIntervalMinutes;
            var max = _settingsStore.CurrentSettings.MaxIntervalMinutes;
            _scheduler.OnSystemResumeOrUnlock(min, max);
        }
    }

    /// <summary>
    /// Handles power mode changes. Internal for deterministic testing.
    /// </summary>
    internal void HandlePowerModeChanged(PowerModes mode)
    {
        if (mode == PowerModes.Suspend)
        {
            _scheduler.OnSystemSuspendOrLock();
        }
        else if (mode == PowerModes.Resume)
        {
            var min = _settingsStore.CurrentSettings.MinIntervalMinutes;
            var max = _settingsStore.CurrentSettings.MaxIntervalMinutes;
            _scheduler.OnSystemResumeOrUnlock(min, max);
        }
    }

    /// <summary>
    /// Unsubscribes from Windows system events and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        try
        {
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        }
        catch
        {
            // Suppress cleanup exceptions
        }
    }
}
