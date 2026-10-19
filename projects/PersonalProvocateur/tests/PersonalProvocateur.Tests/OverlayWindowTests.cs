using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using PersonalProvocateur.App.Models;
using PersonalProvocateur.App.Services;
using PersonalProvocateur.App.Storage;
using PersonalProvocateur.App.UI;
using Xunit;

namespace PersonalProvocateur.Tests;

public class OverlayWindowTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;

    public OverlayWindowTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "pp_overlay_tests_" + Guid.NewGuid().ToString("N"));
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
            s.IsPaused = false;
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
    public void OverlayWindow_Instantiation_InitializesTopCenterMascotControl()
    {
        RunInSta(() =>
        {
            var store = CreateTestSettingsStore();
            var scheduler = new RandomReminderScheduler();
            var videoLib = new VideoLibraryService(store);
            using var coordinator = new OverlayCoordinator(scheduler, videoLib, store);

            var window = new OverlayWindow(coordinator, store);

            Assert.NotNull(window.Mascot);
            Assert.False(window.CanClose);
            Assert.False(window.IsClickHandled);
            Assert.Equal(System.Windows.HorizontalAlignment.Center, window.Mascot.HorizontalAlignment);
            Assert.Equal(System.Windows.VerticalAlignment.Top, window.Mascot.VerticalAlignment);
            Assert.Equal(new Thickness(0), window.Mascot.Margin);
            Assert.Equal(Visibility.Collapsed, window.EmptyLibraryNotice.Visibility);

            window.SafeClose();
        });
    }

    [Fact]
    public void PositionMascot_EnsuresTopCenterAlignment()
    {
        RunInSta(() =>
        {
            var store = CreateTestSettingsStore();
            var scheduler = new RandomReminderScheduler();
            var videoLib = new VideoLibraryService(store);
            using var coordinator = new OverlayCoordinator(scheduler, videoLib, store);

            var window = new OverlayWindow(coordinator, store);
            window.PositionMascot();

            Assert.Equal(System.Windows.HorizontalAlignment.Center, window.Mascot.HorizontalAlignment);
            Assert.Equal(System.Windows.VerticalAlignment.Top, window.Mascot.VerticalAlignment);
            Assert.Equal(new Thickness(0), window.Mascot.Margin);

            window.SafeClose();
        });
    }

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(2560, 1440)]
    [InlineData(3840, 2160)]
    [InlineData(1366, 768)]
    [InlineData(1280, 720)]
    public void CalculateSafeMascotPosition_CalculatesTopCenterCoordinates(double screenWidth, double screenHeight)
    {
        const double mascotWidth = 260.0;
        const double mascotHeight = 260.0;

        var (left, top) = OverlayWindow.CalculateSafeMascotPosition(screenWidth, screenHeight, mascotWidth, mascotHeight);

        Assert.Equal(0.0, top);
        double expectedLeft = (screenWidth - mascotWidth) / 2.0;
        Assert.Equal(expectedLeft, left);
    }

    [Fact]
    public void BackdropClick_DuringVideoPlaying_DismissesSession()
    {
        RunInSta(() =>
        {
            var store = CreateTestSettingsStore();
            var scheduler = new RandomReminderScheduler();
            var videoLib = new VideoLibraryService(store, fileExistsChecker: _ => true);

            OverlayWindow? overlay = null;
            using var coordinator = new OverlayCoordinator(
                scheduler,
                videoLib,
                store,
                overlayWindowFactory: coord => overlay = new OverlayWindow(coord, store),
                videoWindowFactory: coord => new VideoWindow(coord, store),
                dispatcherInvoker: a => a(),
                fileExistsChecker: _ => true);

            coordinator.TriggerProvocation(isManualTest: true);
            Assert.NotNull(overlay);
            Assert.Equal(SessionState.MascotOverlayActive, coordinator.CurrentState);

            // Backdrop click while mascot is active does not dismiss
            overlay.HandleBackdropClick();
            Assert.Equal(SessionState.MascotOverlayActive, coordinator.CurrentState);

            // Transition to video playing
            videoLib.AddVideo("sample.mp4", "Sample Video");
            coordinator.OnMascotClicked();
            Assert.Equal(SessionState.VideoPlaying, coordinator.CurrentState);

            // Now click on backdrop dismisses active video session
            overlay.HandleBackdropClick();
            Assert.Equal(SessionState.BackgroundIdle, coordinator.CurrentState);
        });
    }

    [Fact]
    public void OverlayWindow_AltF4Protection_BlocksClosingWhenCanCloseIsFalse()
    {
        RunInSta(() =>
        {
            var store = CreateTestSettingsStore();
            var scheduler = new RandomReminderScheduler();
            var videoLib = new VideoLibraryService(store);
            using var coordinator = new OverlayCoordinator(scheduler, videoLib, store);

            var window = new OverlayWindow(coordinator, store);
            Assert.False(window.CanClose);

            // Simulate closing event when CanClose is false
            var onClosingMethod = typeof(OverlayWindow).GetMethod(
                "OnClosing",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(onClosingMethod);

            var cancelArgs = new CancelEventArgs();
            onClosingMethod.Invoke(window, new object[] { cancelArgs });

            // Closing must be cancelled
            Assert.True(cancelArgs.Cancel);

            // After SafeClose, CanClose is true
            window.SafeClose();
            Assert.True(window.CanClose);
        });
    }

    [Fact]
    public void SafeClose_StopsMascotAnimationsAndSetsCanClose()
    {
        RunInSta(() =>
        {
            var store = CreateTestSettingsStore();
            var scheduler = new RandomReminderScheduler();
            var videoLib = new VideoLibraryService(store);
            using var coordinator = new OverlayCoordinator(scheduler, videoLib, store);

            var window = new OverlayWindow(coordinator, store);

            // Start idle animation
            window.Mascot.PlayIdleAnimation();

            // SafeClose stops animations and permits window to close
            window.SafeClose();

            Assert.True(window.CanClose);
            Assert.Equal(1.0, window.Mascot.MascotScale.ScaleX);
            Assert.Equal(1.0, window.Mascot.MascotScale.ScaleY);
            Assert.Equal(0.0, window.Mascot.MascotRotate.Angle);
            Assert.Equal(0.0, window.Mascot.MascotTranslate.X);
            Assert.Equal(0.0, window.Mascot.MascotTranslate.Y);
        });
    }

    [Fact]
    public void ShowEmptyLibraryNotice_HidesMascot_StopsAnimations_AndShowsNotice()
    {
        RunInSta(() =>
        {
            var store = CreateTestSettingsStore();
            var scheduler = new RandomReminderScheduler();
            var videoLib = new VideoLibraryService(store);
            using var coordinator = new OverlayCoordinator(scheduler, videoLib, store);

            var window = new OverlayWindow(coordinator, store);
            window.Mascot.PlayIdleAnimation();

            window.ShowEmptyLibraryNotice();

            Assert.Equal(Visibility.Collapsed, window.Mascot.Visibility);
            Assert.Equal(Visibility.Visible, window.EmptyLibraryNotice.Visibility);
            Assert.Equal(1.0, window.Mascot.MascotScale.ScaleX);
            Assert.Equal(1.0, window.Mascot.MascotScale.ScaleY);

            window.SafeClose();
        });
    }

    [Fact]
    public void HandleMascotClick_PreventsRapidDoubleClicks()
    {
        RunInSta(() =>
        {
            var store = CreateTestSettingsStore();
            var scheduler = new RandomReminderScheduler();
            var videoLib = new VideoLibraryService(store);
            using var coordinator = new OverlayCoordinator(scheduler, videoLib, store);

            var window = new OverlayWindow(coordinator, store);

            Assert.False(window.IsClickHandled);

            window.HandleMascotClick();
            Assert.True(window.IsClickHandled);
            Assert.Equal(MascotState.Clicked, window.Mascot.CurrentState);

            // Second click should be safely ignored
            window.HandleMascotClick();
            Assert.True(window.IsClickHandled);

            window.SafeClose();
        });
    }
}
