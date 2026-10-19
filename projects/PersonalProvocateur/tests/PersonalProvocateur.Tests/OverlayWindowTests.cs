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

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(2560, 1440)]
    [InlineData(3840, 2160)]
    [InlineData(1366, 768)]
    [InlineData(1280, 720)]
    [InlineData(800, 600)]
    public void CalculateSafeMascotPosition_StaysWithinSafeBounds(double screenWidth, double screenHeight)
    {
        const double mascotWidth = 260.0;
        const double mascotHeight = 260.0;
        const double minEdgeMargin = 60.0;
        const double minTopMargin = 80.0;
        const double minBottomMargin = 120.0;

        for (int seed = 0; seed < 100; seed++)
        {
            var rng = new Random(seed);
            var (left, top) = OverlayWindow.CalculateSafeMascotPosition(
                screenWidth,
                screenHeight,
                mascotWidth,
                mascotHeight,
                rng);

            // Left edge safe margin
            Assert.True(left >= minEdgeMargin, $"Left {left} < {minEdgeMargin} for screen {screenWidth}x{screenHeight}");

            // Right edge safe margin (screenWidth - left - mascotWidth >= minEdgeMargin)
            double rightRemaining = screenWidth - (left + mascotWidth);
            Assert.True(rightRemaining >= minEdgeMargin || left <= minEdgeMargin + 20,
                $"Right remaining {rightRemaining} < {minEdgeMargin} for screen {screenWidth}x{screenHeight}");

            // Top edge safe margin
            Assert.True(top >= minTopMargin, $"Top {top} < {minTopMargin} for screen {screenWidth}x{screenHeight}");

            // Bottom edge safe margin (accounting for taskbar)
            double bottomRemaining = screenHeight - (top + mascotHeight);
            Assert.True(bottomRemaining >= minBottomMargin || top <= minTopMargin + 20,
                $"Bottom remaining {bottomRemaining} < {minBottomMargin} for screen {screenWidth}x{screenHeight}");
        }
    }

    [Fact]
    public void CalculateSafeMascotPosition_AllCandidateZonesCovered()
    {
        const double screenWidth = 1920.0;
        const double screenHeight = 1080.0;
        var zonesHit = new bool[5];

        for (int i = 0; i < 50; i++)
        {
            var rng = new Random(i * 37 + 11);
            var (left, top) = OverlayWindow.CalculateSafeMascotPosition(screenWidth, screenHeight, 260, 260, rng);

            // Classify which zone was generated
            if (left > 1400 && top > 600) zonesHit[0] = true; // Bottom-Right
            else if (left < 200 && top > 600) zonesHit[1] = true; // Bottom-Left
            else if (left >= 600 && left <= 1100 && top > 600) zonesHit[2] = true; // Bottom-Center
            else if (left > 1400 && top <= 600) zonesHit[3] = true; // Right-Side
            else if (left < 200 && top <= 600) zonesHit[4] = true; // Left-Side
        }

        Assert.All(zonesHit, hit => Assert.True(hit));
    }

    [Fact]
    public void OverlayWindow_Instantiation_InitializesMascotControlAndCanvas()
    {
        RunInSta(() =>
        {
            var store = CreateTestSettingsStore();
            var scheduler = new RandomReminderScheduler();
            var videoLib = new VideoLibraryService(store);
            using var coordinator = new OverlayCoordinator(scheduler, videoLib, store);

            var window = new OverlayWindow(coordinator, store);

            Assert.NotNull(window.MascotCanvas);
            Assert.NotNull(window.Mascot);
            Assert.False(window.CanClose);
            Assert.False(window.IsClickHandled);
            Assert.Equal(Visibility.Visible, window.MascotCanvas.Visibility);
            Assert.Equal(Visibility.Collapsed, window.EmptyLibraryNotice.Visibility);

            window.SafeClose();
        });
    }

    [Fact]
    public void PositionMascot_SetsCanvasLeftAndTopWithinSafeBounds()
    {
        RunInSta(() =>
        {
            var store = CreateTestSettingsStore();
            var scheduler = new RandomReminderScheduler();
            var videoLib = new VideoLibraryService(store);
            using var coordinator = new OverlayCoordinator(scheduler, videoLib, store);

            var window = new OverlayWindow(coordinator, store)
            {
                Width = 1920,
                Height = 1080
            };

            window.PositionMascot(new Random(42));

            double left = Canvas.GetLeft(window.Mascot);
            double top = Canvas.GetTop(window.Mascot);

            Assert.False(double.IsNaN(left));
            Assert.False(double.IsNaN(top));
            Assert.True(left >= 60.0);
            Assert.True(top >= 80.0);
            Assert.True(left + 260.0 <= 1920.0 - 60.0);
            Assert.True(top + 260.0 <= 1080.0 - 120.0);

            window.SafeClose();
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
    public void ShowEmptyLibraryNotice_HidesMascotCanvas_StopsAnimations_AndShowsNotice()
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

            Assert.Equal(Visibility.Collapsed, window.MascotCanvas.Visibility);
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
