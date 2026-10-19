using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using PersonalProvocateur.App.Models;
using PersonalProvocateur.App.Services;
using PersonalProvocateur.App.Storage;
using PersonalProvocateur.App.UI;
using Xunit;

namespace PersonalProvocateur.Tests;

public class VideoWindowTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;

    public VideoWindowTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "pp_videowin_tests_" + Guid.NewGuid().ToString("N"));
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
            // Suppress cleanup exceptions in tests
        }
    }

    private SettingsStore CreateTestSettingsStore()
    {
        var store = new SettingsStore(_settingsPath);
        store.Update(s =>
        {
            s.VideoVolume = 0.85;
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
    public void VideoWindow_Instantiation_InitializesComponentsAndWatchdog()
    {
        RunInSta(() =>
        {
            var store = CreateTestSettingsStore();
            var scheduler = new RandomReminderScheduler();
            var videoLib = new VideoLibraryService(store);
            using var coordinator = new OverlayCoordinator(scheduler, videoLib, store);

            var window = new VideoWindow(coordinator, store);

            Assert.NotNull(window.PlaybackProgressBar);
            Assert.NotNull(window.RemainingTimeTextBlock);
            Assert.NotNull(window.MandatoryWatchNotice);
            Assert.NotNull(window.CloseButton);
            Assert.NotNull(window.VideoTitleTextBlock);
            Assert.NotNull(window.Player);
            Assert.NotNull(window.PlaybackWatchdogTimer);
            Assert.Equal(TimeSpan.FromMilliseconds(100), window.PlaybackWatchdogTimer.Interval);
            Assert.False(window.AllowClose);
            Assert.False(window.IsManualTest);

            window.AllowClose = true;
            window.Close();
        });
    }

    [Fact]
    public void VideoWindow_PlayVideo_ConfiguresManualTestStateAndCloseButtonVisibility()
    {
        RunInSta(() =>
        {
            var store = CreateTestSettingsStore();
            var scheduler = new RandomReminderScheduler();
            var videoLib = new VideoLibraryService(store);
            using var coordinator = new OverlayCoordinator(scheduler, videoLib, store);

            var window = new VideoWindow(coordinator, store);

            // Regular provocation mode: CloseButton is collapsed
            window.PlayVideo("test.mp4", "Test Clip", isManualTest: false);
            Assert.False(window.IsManualTest);
            Assert.Equal(Visibility.Collapsed, window.CloseButton.Visibility);

            // Manual test mode: CloseButton is visible
            window.PlayVideo("test.mp4", "Test Clip", isManualTest: true);
            Assert.True(window.IsManualTest);
            Assert.Equal(Visibility.Visible, window.CloseButton.Visibility);

            window.AllowClose = true;
            window.Close();
        });
    }

    [Fact]
    public void VideoWindow_CloseButton_InManualTest_CallsCompleteSession()
    {
        RunInSta(() =>
        {
            var store = CreateTestSettingsStore();
            var scheduler = new RandomReminderScheduler();
            var videoLib = new VideoLibraryService(store, fileExistsChecker: _ => true);

            bool sessionCompleted = false;
            using var coordinator = new OverlayCoordinator(
                scheduler,
                videoLib,
                store,
                overlayWindowFactory: coord => new OverlayWindow(coord, store),
                videoWindowFactory: coord => new VideoWindow(coord, store),
                dispatcherInvoker: a => a(),
                fileExistsChecker: _ => true);

            coordinator.TriggerProvocation(isManualTest: true);
            coordinator.StateChanged += (s, state) =>
            {
                if (state == SessionState.BackgroundIdle)
                {
                    sessionCompleted = true;
                }
            };

            var window = new VideoWindow(coordinator, store)
            {
                IsManualTest = true
            };

            var closeMethod = typeof(VideoWindow).GetMethod(
                "CloseButton_Click",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(closeMethod);

            closeMethod.Invoke(window, new object[] { window.CloseButton, new RoutedEventArgs() });

            Assert.True(sessionCompleted);
            window.AllowClose = true;
            window.Close();
        });
    }

    [Fact]
    public void VideoWindow_MandatoryWatchLock_BlocksCloseButtonAndEscapeKey_WhenNotManualTest()
    {
        RunInSta(() =>
        {
            var store = CreateTestSettingsStore();
            var scheduler = new RandomReminderScheduler();
            var videoLib = new VideoLibraryService(store, fileExistsChecker: _ => true);

            bool sessionCompleted = false;
            using var coordinator = new OverlayCoordinator(
                scheduler,
                videoLib,
                store,
                overlayWindowFactory: coord => new OverlayWindow(coord, store),
                videoWindowFactory: coord => new VideoWindow(coord, store),
                dispatcherInvoker: a => a(),
                fileExistsChecker: _ => true);

            coordinator.TriggerProvocation(isManualTest: false);
            coordinator.StateChanged += (s, state) =>
            {
                if (state == SessionState.BackgroundIdle)
                {
                    sessionCompleted = true;
                }
            };

            var window = new VideoWindow(coordinator, store)
            {
                IsManualTest = false,
                AllowClose = false
            };

            // 1. Close button click must NOT close the window
            var closeMethod = typeof(VideoWindow).GetMethod(
                "CloseButton_Click",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(closeMethod);

            closeMethod.Invoke(window, new object[] { window.CloseButton, new RoutedEventArgs() });
            Assert.False(sessionCompleted);

            // 2. Escape key press must NOT close the window
            var keyDownMethod = typeof(VideoWindow).GetMethod(
                "Window_KeyDown",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(keyDownMethod);

            var keyEventArgs = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                new System.Windows.Interop.HwndSource(0, 0, 0, 0, 0, "", IntPtr.Zero),
                0,
                Key.Escape)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };

            keyDownMethod.Invoke(window, new object[] { window, keyEventArgs });
            Assert.False(sessionCompleted);
            Assert.True(keyEventArgs.Handled);

            window.AllowClose = true;
            window.Close();
        });
    }

    [Fact]
    public void VideoWindow_OnClosing_BlocksClosingWhenAllowCloseIsFalse_AllowsWhenTrue()
    {
        RunInSta(() =>
        {
            var store = CreateTestSettingsStore();
            var scheduler = new RandomReminderScheduler();
            var videoLib = new VideoLibraryService(store);
            using var coordinator = new OverlayCoordinator(scheduler, videoLib, store);

            var window = new VideoWindow(coordinator, store)
            {
                AllowClose = false,
                IsManualTest = false
            };

            var onClosingMethod = typeof(VideoWindow).GetMethod(
                "OnClosing",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(onClosingMethod);

            // Attempting to close when AllowClose is false must be cancelled
            var cancelArgs = new CancelEventArgs();
            onClosingMethod.Invoke(window, new object[] { cancelArgs });
            Assert.True(cancelArgs.Cancel);

            // Once AllowClose is set to true, closing is permitted
            window.AllowClose = true;
            var allowArgs = new CancelEventArgs();
            onClosingMethod.Invoke(window, new object[] { allowArgs });
            Assert.False(allowArgs.Cancel);

            window.Close();
        });
    }

    [Fact]
    public void VideoWindow_EscapeKey_InManualTest_TriggersCompleteSession()
    {
        RunInSta(() =>
        {
            var store = CreateTestSettingsStore();
            var scheduler = new RandomReminderScheduler();
            var videoLib = new VideoLibraryService(store, fileExistsChecker: _ => true);

            bool sessionCompleted = false;
            using var coordinator = new OverlayCoordinator(
                scheduler,
                videoLib,
                store,
                overlayWindowFactory: coord => new OverlayWindow(coord, store),
                videoWindowFactory: coord => new VideoWindow(coord, store),
                dispatcherInvoker: a => a(),
                fileExistsChecker: _ => true);

            coordinator.TriggerProvocation(isManualTest: true);
            coordinator.StateChanged += (s, state) =>
            {
                if (state == SessionState.BackgroundIdle)
                {
                    sessionCompleted = true;
                }
            };

            var window = new VideoWindow(coordinator, store)
            {
                IsManualTest = true
            };

            var keyDownMethod = typeof(VideoWindow).GetMethod(
                "Window_KeyDown",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(keyDownMethod);

            var keyEventArgs = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                new System.Windows.Interop.HwndSource(0, 0, 0, 0, 0, "", IntPtr.Zero),
                0,
                Key.Escape)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };

            keyDownMethod.Invoke(window, new object[] { window, keyEventArgs });

            Assert.True(sessionCompleted);
            window.AllowClose = true;
            window.Close();
        });
    }

    [Fact]
    public void VideoWindow_WatchdogTimer_SafeWhenNaturalDurationNotAvailable()
    {
        RunInSta(() =>
        {
            var store = CreateTestSettingsStore();
            var scheduler = new RandomReminderScheduler();
            var videoLib = new VideoLibraryService(store, fileExistsChecker: _ => true);

            bool sessionCompleted = false;
            using var coordinator = new OverlayCoordinator(
                scheduler,
                videoLib,
                store,
                overlayWindowFactory: coord => new OverlayWindow(coord, store),
                videoWindowFactory: coord => new VideoWindow(coord, store),
                dispatcherInvoker: a => a(),
                fileExistsChecker: _ => true);

            coordinator.TriggerProvocation(isManualTest: true);
            coordinator.StateChanged += (s, state) =>
            {
                if (state == SessionState.BackgroundIdle)
                {
                    sessionCompleted = true;
                }
            };

            var window = new VideoWindow(coordinator, store);
            window.CheckPlaybackProgress();

            Assert.False(sessionCompleted);
            Assert.Equal(0, window.PlaybackProgressBar.Value);

            window.AllowClose = true;
            window.Close();
        });
    }

    [Fact]
    public void VideoWindow_WatchdogTimer_UpdatesProgressAndTriggersCompletionAtEndOfVideo()
    {
        RunInSta(() =>
        {
            var store = CreateTestSettingsStore();
            var scheduler = new RandomReminderScheduler();
            var videoLib = new VideoLibraryService(store, fileExistsChecker: _ => true);

            bool sessionCompleted = false;
            using var coordinator = new OverlayCoordinator(
                scheduler,
                videoLib,
                store,
                overlayWindowFactory: coord => new OverlayWindow(coord, store),
                videoWindowFactory: coord => new VideoWindow(coord, store),
                dispatcherInvoker: a => a(),
                fileExistsChecker: _ => true);

            coordinator.TriggerProvocation(isManualTest: true);
            coordinator.StateChanged += (s, state) =>
            {
                if (state == SessionState.BackgroundIdle)
                {
                    sessionCompleted = true;
                }
            };

            var window = new VideoWindow(coordinator, store);

            // 1. Check midway (5.0s / 10.0s) -> 50% progress, 0:05 remaining, no completion
            window.CheckPlaybackProgress(
                overridePosition: TimeSpan.FromSeconds(5.0),
                overrideDuration: TimeSpan.FromSeconds(10.0));

            Assert.False(sessionCompleted);
            Assert.Equal(50.0, window.PlaybackProgressBar.Value);
            Assert.Equal("0:05", window.RemainingTimeTextBlock.Text);

            // 2. Check near end (9.9s / 10.0s >= 10.0s - 120ms) -> triggers completion
            window.CheckPlaybackProgress(
                overridePosition: TimeSpan.FromSeconds(9.9),
                overrideDuration: TimeSpan.FromSeconds(10.0));

            Assert.True(sessionCompleted);
            Assert.True(window.HasCompleted);

            window.AllowClose = true;
            window.Close();
        });
    }

    [Fact]
    public void VideoWindow_FlashMandatoryWatchNotice_DoesNotThrow()
    {
        RunInSta(() =>
        {
            var store = CreateTestSettingsStore();
            var scheduler = new RandomReminderScheduler();
            var videoLib = new VideoLibraryService(store);
            using var coordinator = new OverlayCoordinator(scheduler, videoLib, store);

            var window = new VideoWindow(coordinator, store);
            window.FlashMandatoryWatchNotice();

            Assert.NotNull(window.MandatoryWatchNotice);
            window.AllowClose = true;
            window.Close();
        });
    }
}
