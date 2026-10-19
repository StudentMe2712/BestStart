using Microsoft.Win32;
using PersonalProvocateur.App.Services;
using PersonalProvocateur.App.Storage;
using PersonalProvocateur.App.WindowsInterop;
using Xunit;

namespace PersonalProvocateur.Tests;

public class SystemPowerWatcherTests : IDisposable
{
    private readonly string _tempSettingsFile;

    public SystemPowerWatcherTests()
    {
        _tempSettingsFile = Path.Combine(Path.GetTempPath(), $"settings_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempSettingsFile))
        {
            try
            {
                File.Delete(_tempSettingsFile);
            }
            catch
            {
                // Ignore
            }
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void HandleSessionSwitch_SessionLock_StopsScheduler()
    {
        var settingsStore = new SettingsStore(_tempSettingsFile);
        var scheduler = new RandomReminderScheduler(new Random(42));
        scheduler.StartNextCycle(10, 20);
        Assert.True(scheduler.IsRunning);

        using var watcher = new SystemPowerWatcher(scheduler, settingsStore);
        watcher.HandleSessionSwitch(SessionSwitchReason.SessionLock);

        Assert.False(scheduler.IsRunning);
    }

    [Fact]
    public void HandleSessionSwitch_SessionUnlock_ResumesSchedulerWithStoreIntervals()
    {
        var settingsStore = new SettingsStore(_tempSettingsFile);
        settingsStore.CurrentSettings.MinIntervalMinutes = 15;
        settingsStore.CurrentSettings.MaxIntervalMinutes = 30;

        var scheduler = new RandomReminderScheduler(new Random(42));
        using var watcher = new SystemPowerWatcher(scheduler, settingsStore);

        Assert.False(scheduler.IsRunning);

        watcher.HandleSessionSwitch(SessionSwitchReason.SessionUnlock);

        Assert.True(scheduler.IsRunning);
        Assert.NotNull(scheduler.CurrentScheduledInterval);
        Assert.True(scheduler.CurrentScheduledInterval.Value.TotalMinutes >= 15);
        Assert.True(scheduler.CurrentScheduledInterval.Value.TotalMinutes <= 30);
    }

    [Fact]
    public void HandlePowerModeChanged_Suspend_StopsScheduler()
    {
        var settingsStore = new SettingsStore(_tempSettingsFile);
        var scheduler = new RandomReminderScheduler(new Random(42));
        scheduler.StartNextCycle(10, 20);
        Assert.True(scheduler.IsRunning);

        using var watcher = new SystemPowerWatcher(scheduler, settingsStore);
        watcher.HandlePowerModeChanged(PowerModes.Suspend);

        Assert.False(scheduler.IsRunning);
    }

    [Fact]
    public void HandlePowerModeChanged_Resume_ResumesSchedulerWithStoreIntervals()
    {
        var settingsStore = new SettingsStore(_tempSettingsFile);
        settingsStore.CurrentSettings.MinIntervalMinutes = 20;
        settingsStore.CurrentSettings.MaxIntervalMinutes = 40;

        var scheduler = new RandomReminderScheduler(new Random(42));
        using var watcher = new SystemPowerWatcher(scheduler, settingsStore);

        Assert.False(scheduler.IsRunning);

        watcher.HandlePowerModeChanged(PowerModes.Resume);

        Assert.True(scheduler.IsRunning);
        Assert.NotNull(scheduler.CurrentScheduledInterval);
        Assert.True(scheduler.CurrentScheduledInterval.Value.TotalMinutes >= 20);
        Assert.True(scheduler.CurrentScheduledInterval.Value.TotalMinutes <= 40);
    }

    [Fact]
    public void Dispose_UnsubscribesCleanlyWithoutException()
    {
        var settingsStore = new SettingsStore(_tempSettingsFile);
        var scheduler = new RandomReminderScheduler(new Random(42));

        var watcher = new SystemPowerWatcher(scheduler, settingsStore);
        var ex = Record.Exception(() => watcher.Dispose());

        Assert.Null(ex);
    }
}
