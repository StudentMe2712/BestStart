using System.ComponentModel;
using System.IO;
using System.Runtime.ExceptionServices;
using PersonalProvocateur.App.Models;
using PersonalProvocateur.App.Services;
using PersonalProvocateur.App.Storage;
using PersonalProvocateur.App.UI;
using Xunit;

namespace PersonalProvocateur.Tests;

public class OverlayCoordinatorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;

    public OverlayCoordinatorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "pp_coord_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "settings.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Suppress cleanup exceptions
        }
    }

    private SettingsStore CreateTestSettingsStore(bool isPaused = false)
    {
        var store = new SettingsStore(_settingsPath);
        store.Update(s =>
        {
            s.IsPaused = isPaused;
            s.MinIntervalMinutes = 15;
            s.MaxIntervalMinutes = 45;
            s.DimmingOpacity = 0.60;
        });
        return store;
    }

    private static void RunInSta(Action action)
    {
        Exception? caught = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (caught != null)
        {
            ExceptionDispatchInfo.Capture(caught).Throw();
        }
    }

    [Fact]
    public void Constructor_InitialState_ReflectsSettings()
    {
        var storeIdle = CreateTestSettingsStore(isPaused: false);
        var scheduler1 = new RandomReminderScheduler();
        var videoLib1 = new VideoLibraryService(storeIdle);
        using var coordIdle = new OverlayCoordinator(
            scheduler1,
            videoLib1,
            storeIdle,
            overlayWindowFactory: _ => null,
            videoWindowFactory: _ => null,
            dispatcherInvoker: a => a());

        Assert.Equal(SessionState.BackgroundIdle, coordIdle.CurrentState);

        var storePaused = CreateTestSettingsStore(isPaused: true);
        var scheduler2 = new RandomReminderScheduler();
        var videoLib2 = new VideoLibraryService(storePaused);
        using var coordPaused = new OverlayCoordinator(
            scheduler2,
            videoLib2,
            storePaused,
            overlayWindowFactory: _ => null,
            videoWindowFactory: _ => null,
            dispatcherInvoker: a => a());

        Assert.Equal(SessionState.Paused, coordPaused.CurrentState);
    }

    [Fact]
    public void TriggerProvocation_WhenIdle_TransitionsToMascotOverlayActive()
    {
        var store = CreateTestSettingsStore(isPaused: false);
        var scheduler = new RandomReminderScheduler();
        scheduler.StartNextCycle(15, 45);
        Assert.True(scheduler.IsRunning);

        var videoLib = new VideoLibraryService(store);
        int stateChangedCount = 0;
        SessionState? lastReportedState = null;

        using var coordinator = new OverlayCoordinator(
            scheduler,
            videoLib,
            store,
            overlayWindowFactory: _ => null,
            videoWindowFactory: _ => null,
            dispatcherInvoker: a => a());

        coordinator.StateChanged += (s, state) =>
        {
            stateChangedCount++;
            lastReportedState = state;
        };

        coordinator.TriggerProvocation(isManualTest: false);

        Assert.Equal(SessionState.MascotOverlayActive, coordinator.CurrentState);
        Assert.Equal(SessionState.MascotOverlayActive, lastReportedState);
        Assert.Equal(1, stateChangedCount);
        Assert.True(scheduler.IsSessionActive);
        Assert.False(scheduler.IsRunning);
    }

    [Fact]
    public void TriggerProvocation_WhenPaused_NormalTriggerIgnored()
    {
        var store = CreateTestSettingsStore(isPaused: true);
        var scheduler = new RandomReminderScheduler();
        var videoLib = new VideoLibraryService(store);

        using var coordinator = new OverlayCoordinator(
            scheduler,
            videoLib,
            store,
            overlayWindowFactory: _ => null,
            videoWindowFactory: _ => null,
            dispatcherInvoker: a => a());

        coordinator.TriggerProvocation(isManualTest: false);

        Assert.Equal(SessionState.Paused, coordinator.CurrentState);
        Assert.False(scheduler.IsSessionActive);
    }

    [Fact]
    public void TriggerProvocation_WhenPaused_ManualTestProceeds()
    {
        var store = CreateTestSettingsStore(isPaused: true);
        var scheduler = new RandomReminderScheduler();
        var videoLib = new VideoLibraryService(store);

        using var coordinator = new OverlayCoordinator(
            scheduler,
            videoLib,
            store,
            overlayWindowFactory: _ => null,
            videoWindowFactory: _ => null,
            dispatcherInvoker: a => a());

        coordinator.TriggerProvocation(isManualTest: true);

        Assert.Equal(SessionState.MascotOverlayActive, coordinator.CurrentState);
        Assert.True(scheduler.IsSessionActive);
    }

    [Fact]
    public void TriggerProvocation_WhenAlreadyActive_DoesNotDuplicateSession()
    {
        var store = CreateTestSettingsStore(isPaused: false);
        var scheduler = new RandomReminderScheduler();
        var videoLib = new VideoLibraryService(store);

        int overlayCreationCount = 0;
        using var coordinator = new OverlayCoordinator(
            scheduler,
            videoLib,
            store,
            overlayWindowFactory: _ =>
            {
                overlayCreationCount++;
                return null;
            },
            videoWindowFactory: _ => null,
            dispatcherInvoker: a => a());

        coordinator.TriggerProvocation(isManualTest: false);
        Assert.Equal(1, overlayCreationCount);
        Assert.Equal(SessionState.MascotOverlayActive, coordinator.CurrentState);

        // Second trigger attempt while active
        coordinator.TriggerProvocation(isManualTest: true);
        Assert.Equal(1, overlayCreationCount);
        Assert.Equal(SessionState.MascotOverlayActive, coordinator.CurrentState);
    }

    [Fact]
    public void OnMascotClicked_WithAvailableVideos_TransitionsToVideoPlaying()
    {
        var store = CreateTestSettingsStore(isPaused: false);
        var testVideoPath = Path.Combine(_tempDir, "clip.mp4");
        File.WriteAllText(testVideoPath, "dummy video content");

        var videoLib = new VideoLibraryService(store, fileExistsChecker: _ => true);
        videoLib.AddVideo(testVideoPath, "Test Clip");

        var scheduler = new RandomReminderScheduler();

        using var coordinator = new OverlayCoordinator(
            scheduler,
            videoLib,
            store,
            overlayWindowFactory: _ => null,
            videoWindowFactory: _ => null,
            dispatcherInvoker: a => a());

        coordinator.TriggerProvocation(isManualTest: false);
        Assert.Equal(SessionState.MascotOverlayActive, coordinator.CurrentState);

        coordinator.OnMascotClicked();
        Assert.Equal(SessionState.VideoPlaying, coordinator.CurrentState);
    }

    [Fact]
    public void OnMascotClicked_WithEmptyLibrary_RemainsInMascotOverlayActive()
    {
        var store = CreateTestSettingsStore(isPaused: false);
        var videoLib = new VideoLibraryService(store, fileExistsChecker: _ => false);
        var scheduler = new RandomReminderScheduler();

        using var coordinator = new OverlayCoordinator(
            scheduler,
            videoLib,
            store,
            overlayWindowFactory: _ => null,
            videoWindowFactory: _ => null,
            dispatcherInvoker: a => a());

        coordinator.TriggerProvocation(isManualTest: false);
        Assert.Equal(SessionState.MascotOverlayActive, coordinator.CurrentState);

        coordinator.OnMascotClicked();
        // Since no videos exist, stays in MascotOverlayActive with empty notice displayed
        Assert.Equal(SessionState.MascotOverlayActive, coordinator.CurrentState);
    }

    [Fact]
    public void OnMascotClicked_WhenNotInMascotOverlayActive_Ignored()
    {
        var store = CreateTestSettingsStore(isPaused: false);
        var videoLib = new VideoLibraryService(store);
        var scheduler = new RandomReminderScheduler();

        using var coordinator = new OverlayCoordinator(
            scheduler,
            videoLib,
            store,
            overlayWindowFactory: _ => null,
            videoWindowFactory: _ => null,
            dispatcherInvoker: a => a());

        coordinator.OnMascotClicked();
        Assert.Equal(SessionState.BackgroundIdle, coordinator.CurrentState);
    }

    [Fact]
    public void CompleteSession_TransitionsToBackgroundIdleAndRestartsScheduler()
    {
        var store = CreateTestSettingsStore(isPaused: false);
        var scheduler = new RandomReminderScheduler();
        var videoLib = new VideoLibraryService(store);

        using var coordinator = new OverlayCoordinator(
            scheduler,
            videoLib,
            store,
            overlayWindowFactory: _ => null,
            videoWindowFactory: _ => null,
            dispatcherInvoker: a => a());

        coordinator.TriggerProvocation(isManualTest: false);
        Assert.Equal(SessionState.MascotOverlayActive, coordinator.CurrentState);

        coordinator.CompleteSession();

        Assert.Equal(SessionState.BackgroundIdle, coordinator.CurrentState);
        Assert.False(scheduler.IsSessionActive);
        Assert.True(scheduler.IsRunning);
    }

    [Fact]
    public void EmergencyDismiss_FromActiveState_TransitionsToBackgroundIdleAndRestartsScheduler()
    {
        var store = CreateTestSettingsStore(isPaused: false);
        var scheduler = new RandomReminderScheduler();
        var videoLib = new VideoLibraryService(store);

        using var coordinator = new OverlayCoordinator(
            scheduler,
            videoLib,
            store,
            overlayWindowFactory: _ => null,
            videoWindowFactory: _ => null,
            dispatcherInvoker: a => a());

        coordinator.TriggerProvocation(isManualTest: false);
        Assert.Equal(SessionState.MascotOverlayActive, coordinator.CurrentState);

        coordinator.EmergencyDismiss();

        Assert.Equal(SessionState.BackgroundIdle, coordinator.CurrentState);
        Assert.False(scheduler.IsSessionActive);
        Assert.True(scheduler.IsRunning);
    }

    [Fact]
    public void SetPaused_TogglesPauseStateCorrectly()
    {
        var store = CreateTestSettingsStore(isPaused: false);
        var scheduler = new RandomReminderScheduler();
        var videoLib = new VideoLibraryService(store);

        using var coordinator = new OverlayCoordinator(
            scheduler,
            videoLib,
            store,
            overlayWindowFactory: _ => null,
            videoWindowFactory: _ => null,
            dispatcherInvoker: a => a());

        Assert.Equal(SessionState.BackgroundIdle, coordinator.CurrentState);

        // Pause
        coordinator.SetPaused(true);
        Assert.Equal(SessionState.Paused, coordinator.CurrentState);
        Assert.True(scheduler.IsPaused);
        Assert.True(store.CurrentSettings.IsPaused);

        // Resume
        coordinator.SetPaused(false);
        Assert.Equal(SessionState.BackgroundIdle, coordinator.CurrentState);
        Assert.False(scheduler.IsPaused);
        Assert.False(store.CurrentSettings.IsPaused);
        Assert.True(scheduler.IsRunning);
    }

    [Fact]
    public void OverlayWindow_AltF4Protection_BlocksClosingWhenCanCloseIsFalse()
    {
        RunInSta(() =>
        {
            var store = CreateTestSettingsStore(isPaused: false);
            var scheduler = new RandomReminderScheduler();
            var videoLib = new VideoLibraryService(store);
            using var coordinator = new OverlayCoordinator(scheduler, videoLib, store);

            var window = new OverlayWindow(coordinator, store);
            Assert.False(window.CanClose);

            // Attempt to safe close sets CanClose to true
            window.SafeClose();
            Assert.True(window.CanClose);
        });
    }

    [Fact]
    public void TriggerProvocation_WhenInVideoPlaying_Ignored()
    {
        var store = CreateTestSettingsStore(isPaused: false);
        var testVideoPath = Path.Combine(_tempDir, "clip2.mp4");
        File.WriteAllText(testVideoPath, "dummy video content");

        var videoLib = new VideoLibraryService(store, fileExistsChecker: _ => true);
        videoLib.AddVideo(testVideoPath, "Test Clip 2");

        var scheduler = new RandomReminderScheduler();

        int overlayCreationCount = 0;
        using var coordinator = new OverlayCoordinator(
            scheduler,
            videoLib,
            store,
            overlayWindowFactory: _ =>
            {
                overlayCreationCount++;
                return null;
            },
            videoWindowFactory: _ => null,
            dispatcherInvoker: a => a());

        coordinator.TriggerProvocation(isManualTest: false);
        Assert.Equal(1, overlayCreationCount);
        coordinator.OnMascotClicked();
        Assert.Equal(SessionState.VideoPlaying, coordinator.CurrentState);

        // Attempting to trigger while video is playing should do nothing
        coordinator.TriggerProvocation(isManualTest: true);
        Assert.Equal(1, overlayCreationCount);
        Assert.Equal(SessionState.VideoPlaying, coordinator.CurrentState);
    }

    [Fact]
    public void OverlayCoordinator_StateChanged_TransitionsThroughEntireCycle()
    {
        var store = CreateTestSettingsStore(isPaused: false);
        var testVideoPath = Path.Combine(_tempDir, "clip3.mp4");
        File.WriteAllText(testVideoPath, "clip 3");

        var videoLib = new VideoLibraryService(store, fileExistsChecker: _ => true);
        videoLib.AddVideo(testVideoPath, "Clip 3");
        var scheduler = new RandomReminderScheduler();

        var stateHistory = new List<SessionState>();
        using var coordinator = new OverlayCoordinator(
            scheduler,
            videoLib,
            store,
            overlayWindowFactory: _ => null,
            videoWindowFactory: _ => null,
            dispatcherInvoker: a => a());

        coordinator.StateChanged += (s, state) => stateHistory.Add(state);

        // 1. Trigger
        coordinator.TriggerProvocation(isManualTest: false);
        // 2. Mascot clicked
        coordinator.OnMascotClicked();
        // 3. Complete
        coordinator.CompleteSession();

        Assert.Equal(
            new[] { SessionState.MascotOverlayActive, SessionState.VideoPlaying, SessionState.BackgroundIdle },
            stateHistory);
    }

    [Fact]
    public void OverlayCoordinator_SettingsChanged_UpdatesPausedStateAutomatically()
    {
        var store = CreateTestSettingsStore(isPaused: false);
        var scheduler = new RandomReminderScheduler();
        var videoLib = new VideoLibraryService(store);

        using var coordinator = new OverlayCoordinator(
            scheduler,
            videoLib,
            store,
            overlayWindowFactory: _ => null,
            videoWindowFactory: _ => null,
            dispatcherInvoker: a => a());

        Assert.Equal(SessionState.BackgroundIdle, coordinator.CurrentState);

        // Update settings directly
        store.Update(s => s.IsPaused = true);
        Assert.Equal(SessionState.Paused, coordinator.CurrentState);

        store.Update(s => s.IsPaused = false);
        Assert.Equal(SessionState.BackgroundIdle, coordinator.CurrentState);
    }

    [Fact]
    public void OverlayWindow_ShowEmptyLibraryNotice_SwitchesVisibility()
    {
        RunInSta(() =>
        {
            var store = CreateTestSettingsStore(isPaused: false);
            var scheduler = new RandomReminderScheduler();
            var videoLib = new VideoLibraryService(store);
            using var coordinator = new OverlayCoordinator(scheduler, videoLib, store);

            var window = new OverlayWindow(coordinator, store);
            Assert.Equal(System.Windows.Visibility.Visible, window.MascotContainer.Visibility);
            Assert.Equal(System.Windows.Visibility.Collapsed, window.EmptyLibraryNotice.Visibility);

            window.ShowEmptyLibraryNotice();
            Assert.Equal(System.Windows.Visibility.Collapsed, window.MascotContainer.Visibility);
            Assert.Equal(System.Windows.Visibility.Visible, window.EmptyLibraryNotice.Visibility);

            window.SafeClose();
        });
    }
}
