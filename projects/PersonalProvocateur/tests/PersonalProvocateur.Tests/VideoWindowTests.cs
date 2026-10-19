using System;
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
            Assert.NotNull(window.CloseButton);
            Assert.NotNull(window.VideoTitleTextBlock);
            Assert.NotNull(window.Player);
            Assert.NotNull(window.PlaybackWatchdogTimer);
            Assert.Equal(TimeSpan.FromMilliseconds(200), window.PlaybackWatchdogTimer.Interval);

            window.Close();
        });
    }

    [Fact]
    public void VideoWindow_CloseButton_CallsCompleteSession()
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
            var closeMethod = typeof(VideoWindow).GetMethod(
                "CloseButton_Click",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(closeMethod);

            closeMethod.Invoke(window, new object[] { window.CloseButton, new RoutedEventArgs() });

            Assert.True(sessionCompleted);
            window.Close();
        });
    }

    [Fact]
    public void VideoWindow_EscapeKey_TriggersCompleteSession()
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

            window.Close();
        });
    }
}
