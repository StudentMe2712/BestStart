using PersonalProvocateur.App.Services;
using Xunit;

namespace PersonalProvocateur.Tests;

public class RandomReminderSchedulerTests
{
    [Fact]
    public void StartNextCycle_SchedulesWithinMinAndMaxMinutes()
    {
        Action? scheduledAction = null;
        TimeSpan scheduledInterval = TimeSpan.Zero;

        var random = new Random(12345);
        using var scheduler = new RandomReminderScheduler(
            random,
            (action, interval) =>
            {
                scheduledAction = action;
                scheduledInterval = interval;
            });

        scheduler.StartNextCycle(10, 20);

        Assert.True(scheduler.IsRunning);
        Assert.NotNull(scheduler.NextTriggerTimeLocal);
        Assert.NotNull(scheduler.CurrentScheduledInterval);
        Assert.True(scheduledInterval >= TimeSpan.FromMinutes(10));
        Assert.True(scheduledInterval <= TimeSpan.FromMinutes(20));
        Assert.NotNull(scheduledAction);
    }

    [Fact]
    public void StartNextCycle_DoesNothing_WhenSessionActiveOrPaused()
    {
        var scheduledCount = 0;
        using var scheduler = new RandomReminderScheduler(
            timerScheduler: (_, _) => scheduledCount++);

        scheduler.IsSessionActive = true;
        scheduler.StartNextCycle(10, 20);

        Assert.False(scheduler.IsRunning);
        Assert.Null(scheduler.NextTriggerTimeLocal);
        Assert.Equal(0, scheduledCount);

        scheduler.IsSessionActive = false;
        scheduler.SetPaused(true, 10, 20);
        scheduler.StartNextCycle(10, 20);

        Assert.False(scheduler.IsRunning);
        Assert.Equal(0, scheduledCount);
    }

    [Fact]
    public void Stop_ResetsRunningStateAndScheduledTime()
    {
        using var scheduler = new RandomReminderScheduler(
            timerScheduler: (_, _) => { });

        scheduler.StartNextCycle(15, 30);
        Assert.True(scheduler.IsRunning);
        Assert.NotNull(scheduler.NextTriggerTimeLocal);

        scheduler.Stop();
        Assert.False(scheduler.IsRunning);
        Assert.Null(scheduler.NextTriggerTimeLocal);
        Assert.Null(scheduler.CurrentScheduledInterval);
    }

    [Fact]
    public void SetPaused_TogglesStateAndControlsCycle()
    {
        var cyclesStarted = 0;
        using var scheduler = new RandomReminderScheduler(
            timerScheduler: (_, _) => cyclesStarted++);

        scheduler.StartNextCycle(15, 30);
        Assert.Equal(1, cyclesStarted);
        Assert.True(scheduler.IsRunning);

        scheduler.SetPaused(true, 15, 30);
        Assert.True(scheduler.IsPaused);
        Assert.False(scheduler.IsRunning);

        scheduler.SetPaused(false, 15, 30);
        Assert.False(scheduler.IsPaused);
        Assert.True(scheduler.IsRunning);
        Assert.Equal(2, cyclesStarted);
    }

    [Fact]
    public void OnSystemSuspend_StopsTimer_AndResumeStartsFreshCycle()
    {
        var cyclesStarted = 0;
        using var scheduler = new RandomReminderScheduler(
            timerScheduler: (_, _) => cyclesStarted++);

        scheduler.StartNextCycle(15, 30);
        Assert.Equal(1, cyclesStarted);

        scheduler.OnSystemSuspendOrLock();
        Assert.False(scheduler.IsRunning);
        Assert.Null(scheduler.NextTriggerTimeLocal);

        scheduler.OnSystemResumeOrUnlock(15, 30);
        Assert.True(scheduler.IsRunning);
        Assert.Equal(2, cyclesStarted);
    }

    [Fact]
    public void CompleteSession_ResetsActiveFlagAndStartsNextCycle()
    {
        var cyclesStarted = 0;
        using var scheduler = new RandomReminderScheduler(
            timerScheduler: (_, _) => cyclesStarted++);

        scheduler.IsSessionActive = true;
        scheduler.CompleteSession(15, 30);

        Assert.False(scheduler.IsSessionActive);
        Assert.True(scheduler.IsRunning);
        Assert.Equal(1, cyclesStarted);
    }

    [Fact]
    public void TriggerNowForTesting_FiresEventImmediately()
    {
        using var scheduler = new RandomReminderScheduler(
            timerScheduler: (_, _) => { });

        var triggered = false;
        scheduler.TimerTriggered += (_, _) => triggered = true;

        scheduler.StartNextCycle(15, 30);
        scheduler.TriggerNowForTesting();

        Assert.True(triggered);
        Assert.False(scheduler.IsRunning);
        Assert.Null(scheduler.NextTriggerTimeLocal);
    }
}
