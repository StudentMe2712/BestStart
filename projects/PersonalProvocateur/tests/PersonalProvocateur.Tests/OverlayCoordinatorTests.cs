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
            Assert.Equal(System.Windows.Visibility.Visible, window.Mascot.Visibility);
            Assert.Equal(System.Windows.Visibility.Collapsed, window.EmptyLibraryNotice.Visibility);

            window.ShowEmptyLibraryNotice();
            Assert.Equal(System.Windows.Visibility.Collapsed, window.Mascot.Visibility);
            Assert.Equal(System.Windows.Visibility.Visible, window.EmptyLibraryNotice.Visibility);

            window.SafeClose();
        });
    }

    [Fact]
    public void FindNextPlayableVideo_SkipsMissingFiles()
    {
        var store = CreateTestSettingsStore(isPaused: false);
        var scheduler = new RandomReminderScheduler();

        var existingPath = Path.Combine(_tempDir, "real.mp4");
        var missingPath = Path.Combine(_tempDir, "missing.mp4");

        var shuffleBag = new VideoShuffleBag(fileExistsChecker: _ => true);
        var videoLib = new VideoLibraryService(store, shuffleBag, fileExistsChecker: _ => true);
        videoLib.AddVideo(missingPath, "Missing");
        videoLib.AddVideo(existingPath, "Real");

        using var coordinator = new OverlayCoordinator(
            scheduler,
            videoLib,
            store,
            overlayWindowFactory: _ => null,
            videoWindowFactory: _ => null,
            dispatcherInvoker: a => a(),
            fileExistsChecker: p => p == existingPath);

        var playable = coordinator.FindNextPlayableVideo();
        Assert.NotNull(playable);
        Assert.Equal(existingPath, playable.FilePath);
    }

    [Fact]
    public void OnMascotClicked_WhenFirstVideoMissing_PlaysSecondAvailableVideo()
    {
        var store = CreateTestSettingsStore(isPaused: false);
        var scheduler = new RandomReminderScheduler();

        var missingPath = Path.Combine(_tempDir, "broken.mp4");
        var goodPath = Path.Combine(_tempDir, "good.mp4");

        var shuffleBag = new VideoShuffleBag(fileExistsChecker: _ => true);
        var videoLib = new VideoLibraryService(store, shuffleBag, fileExistsChecker: _ => true);
        videoLib.AddVideo(missingPath, "Broken");
        videoLib.AddVideo(goodPath, "Good");

        using var coordinator = new OverlayCoordinator(
            scheduler,
            videoLib,
            store,
            overlayWindowFactory: _ => null,
            videoWindowFactory: _ => null,
            dispatcherInvoker: a => a(),
            fileExistsChecker: p => p == goodPath);

        coordinator.TriggerProvocation(isManualTest: true);
        coordinator.OnMascotClicked();

        Assert.Equal(SessionState.VideoPlaying, coordinator.CurrentState);
    }

    [Fact]
    public void OnMascotClicked_WhenAllVideosMissing_ShowsEmptyLibraryNotice()
    {
        RunInSta(() =>
        {
            var store = CreateTestSettingsStore(isPaused: false);
            var scheduler = new RandomReminderScheduler();

            var missingPath = Path.Combine(_tempDir, "ghost.mp4");
            var shuffleBag = new VideoShuffleBag(fileExistsChecker: _ => true);
            var videoLib = new VideoLibraryService(store, shuffleBag, fileExistsChecker: _ => true);
            videoLib.AddVideo(missingPath, "Ghost");

            OverlayWindow? overlayWin = null;
            using var coordinator = new OverlayCoordinator(
                scheduler,
                videoLib,
                store,
                overlayWindowFactory: coord => overlayWin = new OverlayWindow(coord, store),
                videoWindowFactory: _ => null,
                dispatcherInvoker: a => a(),
                fileExistsChecker: _ => false);

            coordinator.TriggerProvocation(isManualTest: true);
            Assert.NotNull(overlayWin);
            Assert.Equal(System.Windows.Visibility.Visible, overlayWin.Mascot.Visibility);

            coordinator.OnMascotClicked();

            Assert.Equal(System.Windows.Visibility.Collapsed, overlayWin.Mascot.Visibility);
            Assert.Equal(System.Windows.Visibility.Visible, overlayWin.EmptyLibraryNotice.Visibility);

            overlayWin.SafeClose();
        });
    }

    [Fact]
    public void OnVideoPlaybackFailed_SwitchesToNextVideo()
    {
        var store = CreateTestSettingsStore(isPaused: false);
        var scheduler = new RandomReminderScheduler();

        var corruptPath = Path.Combine(_tempDir, "corrupt.mp4");
        var backupPath = Path.Combine(_tempDir, "backup.mp4");

        var shuffleBag = new VideoShuffleBag(fileExistsChecker: _ => true);
        var videoLib = new VideoLibraryService(store, shuffleBag, fileExistsChecker: _ => true);
        videoLib.AddVideo(corruptPath, "Corrupt");
        videoLib.AddVideo(backupPath, "Backup");

        using var coordinator = new OverlayCoordinator(
            scheduler,
            videoLib,
            store,
            overlayWindowFactory: _ => null,
            videoWindowFactory: _ => null,
            dispatcherInvoker: a => a(),
            fileExistsChecker: _ => true);

        coordinator.TriggerProvocation(isManualTest: true);
        coordinator.OnMascotClicked();
        Assert.Equal(SessionState.VideoPlaying, coordinator.CurrentState);

        coordinator.OnVideoPlaybackFailed(corruptPath);
        Assert.Equal(SessionState.VideoPlaying, coordinator.CurrentState);
    }

    [Fact]
    public void OnVideoPlaybackFailed_WhenNoOtherVideos_ShowsNoticeAndCompletesSession()
    {
        RunInSta(() =>
        {
            var store = CreateTestSettingsStore(isPaused: false);
            var scheduler = new RandomReminderScheduler();

            var onlyVideo = Path.Combine(_tempDir, "single.mp4");
            var shuffleBag = new VideoShuffleBag(fileExistsChecker: _ => true);
            var videoLib = new VideoLibraryService(store, shuffleBag, fileExistsChecker: _ => true);
            videoLib.AddVideo(onlyVideo, "Single");

            OverlayWindow? overlayWin = null;
            using var coordinator = new OverlayCoordinator(
                scheduler,
                videoLib,
                store,
                overlayWindowFactory: coord => overlayWin = new OverlayWindow(coord, store),
                videoWindowFactory: _ => null,
                dispatcherInvoker: a => a(),
                fileExistsChecker: _ => true);

            coordinator.TriggerProvocation(isManualTest: true);
            coordinator.OnMascotClicked();
            Assert.Equal(SessionState.VideoPlaying, coordinator.CurrentState);

            coordinator.OnVideoPlaybackFailed(onlyVideo);
            Assert.NotNull(overlayWin);
            Assert.Equal(System.Windows.Visibility.Collapsed, overlayWin.Mascot.Visibility);
            Assert.Equal(System.Windows.Visibility.Visible, overlayWin.EmptyLibraryNotice.Visibility);

            overlayWin.SafeClose();
        });
    }

    [Fact]
    public void OverlayCoordinator_CompleteSession_EnablesAllowCloseAndCanCloseOnActiveWindows()
    {
        RunInSta(() =>
        {
            var store = CreateTestSettingsStore(isPaused: false);
            var scheduler = new RandomReminderScheduler();

            var testVideo = Path.Combine(_tempDir, "close_guard.mp4");
            var shuffleBag = new VideoShuffleBag(fileExistsChecker: _ => true);
            var videoLib = new VideoLibraryService(store, shuffleBag, fileExistsChecker: _ => true);
            videoLib.AddVideo(testVideo, "Close Guard Clip");

            OverlayWindow? overlayWin = null;
            VideoWindow? videoWin = null;

            using var coordinator = new OverlayCoordinator(
                scheduler,
                videoLib,
                store,
                overlayWindowFactory: coord => overlayWin = new OverlayWindow(coord, store),
                videoWindowFactory: coord => videoWin = new VideoWindow(coord, store),
                dispatcherInvoker: a => a(),
                fileExistsChecker: _ => true);

            coordinator.TriggerProvocation(isManualTest: false);
            Assert.False(coordinator.IsManualTestSession);
            Assert.NotNull(overlayWin);
            Assert.False(overlayWin.CanClose);

            coordinator.OnMascotClicked();
            Assert.NotNull(videoWin);
            Assert.False(videoWin.AllowClose);
            Assert.False(videoWin.IsManualTest);

            // CompleteSession MUST set AllowClose = true and CanClose = true to ensure 100% reliable close
            coordinator.CompleteSession();

            Assert.True(overlayWin.CanClose);
            Assert.True(videoWin.AllowClose);
            Assert.Equal(SessionState.BackgroundIdle, coordinator.CurrentState);
            Assert.True(scheduler.IsRunning);
        });
    }

    [Fact]
    public void TriggerProvocation_MultiMonitor_OpensOverlaysOnAllScreensWithTargetPrimary()
    {
        RunInSta(() =>
        {
            var store = CreateTestSettingsStore(isPaused: false);
            var scheduler = new RandomReminderScheduler();
            var videoLib = new VideoLibraryService(store);

            var createdOverlays = new List<OverlayWindow>();
            var screen = System.Windows.Forms.Screen.PrimaryScreen ??
                (System.Windows.Forms.Screen.AllScreens.Length > 0 ? System.Windows.Forms.Screen.AllScreens[0] : null);

            using var coordinator = new OverlayCoordinator(
                scheduler,
                videoLib,
                store,
                screensProvider: () => screen != null ? new[] { screen, screen } : System.Windows.Forms.Screen.AllScreens,
                multiOverlayWindowFactory: (coord, isDimmer) =>
                {
                    var win = new OverlayWindow(coord, store, isDimmer);
                    createdOverlays.Add(win);
                    return win;
                },
                videoWindowFactory: _ => null,
                dispatcherInvoker: a => a());

            coordinator.TriggerProvocation(isManualTest: true);

            if (screen != null)
            {
                Assert.Equal(2, coordinator.ActiveOverlayWindows.Count);
                Assert.Equal(2, createdOverlays.Count);

                var primary = coordinator.ActiveOverlayWindow;
                Assert.NotNull(primary);
                Assert.False(primary.IsSecondaryDimmerOnly);

                var secondary = coordinator.ActiveOverlayWindows[1];
                Assert.True(secondary.IsSecondaryDimmerOnly);
                Assert.Equal(System.Windows.Visibility.Collapsed, secondary.Mascot.Visibility);
            }

            coordinator.CompleteSession();

            Assert.Empty(coordinator.ActiveOverlayWindows);
            Assert.Null(coordinator.ActiveOverlayWindow);
            foreach (var win in createdOverlays)
            {
                Assert.True(win.CanClose);
            }
        });
    }

    [Fact]
    public void CompleteSession_InvokesMemoryOptimizerTrim()
    {
        var store = CreateTestSettingsStore(isPaused: false);
        var scheduler = new RandomReminderScheduler();
        var videoLib = new VideoLibraryService(store);

        bool trimCalled = false;
        using var coordinator = new OverlayCoordinator(
            scheduler,
            videoLib,
            store,
            overlayWindowFactory: _ => null,
            videoWindowFactory: _ => null,
            dispatcherInvoker: a => a(),
            memoryTrimmer: () => trimCalled = true);

        coordinator.TriggerProvocation(isManualTest: true);
        Assert.Equal(SessionState.MascotOverlayActive, coordinator.CurrentState);

        coordinator.CompleteSession();

        Assert.True(trimCalled);
        Assert.Equal(SessionState.BackgroundIdle, coordinator.CurrentState);
    }

    [Fact]
    public void EmergencyDismiss_InvokesMemoryOptimizerTrim()
    {
        var store = CreateTestSettingsStore(isPaused: false);
        var scheduler = new RandomReminderScheduler();
        var videoLib = new VideoLibraryService(store);

        bool trimCalled = false;
        using var coordinator = new OverlayCoordinator(
            scheduler,
            videoLib,
            store,
            overlayWindowFactory: _ => null,
            videoWindowFactory: _ => null,
            dispatcherInvoker: a => a(),
            memoryTrimmer: () => trimCalled = true);

        coordinator.TriggerProvocation(isManualTest: true);
        Assert.Equal(SessionState.MascotOverlayActive, coordinator.CurrentState);

        coordinator.EmergencyDismiss();

        Assert.True(trimCalled);
        Assert.Equal(SessionState.BackgroundIdle, coordinator.CurrentState);
    }

    [Fact]
    public void OnMascotClicked_WithTextContent_DisplaysSpotlightAndTransitionsToVideoPlaying()
    {
        RunInSta(() =>
        {
            var store = CreateTestSettingsStore(isPaused: false);
            var scheduler = new RandomReminderScheduler();
            var videoLib = new VideoLibraryService(store);
            videoLib.AddTextItem("Сделай глубокий вдох");

            OverlayWindow? overlayWin = null;
            using var coordinator = new OverlayCoordinator(
                scheduler,
                videoLib,
                store,
                overlayWindowFactory: coord => overlayWin = new OverlayWindow(coord, store),
                videoWindowFactory: _ => null,
                dispatcherInvoker: a => a());

            coordinator.TriggerProvocation(isManualTest: true);
            Assert.NotNull(overlayWin);
            Assert.Equal(SessionState.MascotOverlayActive, coordinator.CurrentState);
            Assert.Equal(System.Windows.Visibility.Visible, overlayWin.Mascot.Visibility);
            Assert.Equal(System.Windows.Visibility.Collapsed, overlayWin.SpotlightContainer.Visibility);

            coordinator.OnMascotClicked();

            Assert.Equal(SessionState.VideoPlaying, coordinator.CurrentState);
            Assert.Equal(System.Windows.Visibility.Collapsed, overlayWin.Mascot.Visibility);
            Assert.Equal(System.Windows.Visibility.Visible, overlayWin.SpotlightContainer.Visibility);
            Assert.Equal("Сделай глубокий вдох", overlayWin.SpotlightTextBlock.Text);

            // Complete session via Done button
            overlayWin.SpotlightDoneButton.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));

            Assert.Equal(SessionState.BackgroundIdle, coordinator.CurrentState);
            Assert.True(scheduler.IsRunning);
        });
    }
}
