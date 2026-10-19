using System.IO;
using System.Windows;
using PersonalProvocateur.App.Models;
using PersonalProvocateur.App.Services;
using PersonalProvocateur.App.Storage;
using PersonalProvocateur.App.UI;
using PersonalProvocateur.App.WindowsInterop;
using Xunit;

namespace PersonalProvocateur.Tests;

public class SettingsWindowTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;

    public SettingsWindowTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "pp_settings_tests_" + Guid.NewGuid().ToString("N"));
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
        }
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
            throw caught;
        }
    }

    [Fact]
    public void VideoItemViewModel_WhenAvailable_SetsProperTextAndBrushes()
    {
        var item = new VideoItem
        {
            Title = "Test Clip",
            FilePath = "C:\\test\\clip.mp4",
            IsEnabled = true
        };

        var vm = new VideoItemViewModel(item, isAvailable: true);

        Assert.Equal("Test Clip", vm.Title);
        Assert.Equal("C:\\test\\clip.mp4", vm.FilePath);
        Assert.True(vm.IsEnabled);
        Assert.True(vm.IsAvailable);
        Assert.Equal("Доступен", vm.AvailabilityText);
        Assert.NotNull(vm.AvailabilityBackground);
        Assert.NotNull(vm.AvailabilityForeground);
    }

    [Fact]
    public void VideoItemViewModel_WhenMissing_SetsProperTextAndBrushes()
    {
        var item = new VideoItem
        {
            Title = "Missing Clip",
            FilePath = "C:\\missing\\clip.mp4",
            IsEnabled = false
        };

        var vm = new VideoItemViewModel(item, isAvailable: false);

        Assert.Equal("Missing Clip", vm.Title);
        Assert.False(vm.IsEnabled);
        Assert.False(vm.IsAvailable);
        Assert.Equal("Не найден", vm.AvailabilityText);
        Assert.NotNull(vm.AvailabilityBackground);
        Assert.NotNull(vm.AvailabilityForeground);
    }

    [Theory]
    [InlineData("Ctrl+Alt+P", HotkeyValidationState.Valid)]
    [InlineData("Ctrl+Alt+Shift+P", HotkeyValidationState.Valid)]
    [InlineData("Win+Shift+F5", HotkeyValidationState.Valid)]
    [InlineData("", HotkeyValidationState.Empty)]
    [InlineData("   ", HotkeyValidationState.Empty)]
    [InlineData(null, HotkeyValidationState.Empty)]
    [InlineData("Ctrl+", HotkeyValidationState.InvalidFormat)]
    [InlineData("InvalidString", HotkeyValidationState.InvalidFormat)]
    public void CheckHotkeyStatus_ReturnsExpectedStatus(string? hotkeyStr, HotkeyValidationState expected)
    {
        var status = GlobalHotkeyService.CheckHotkeyStatus(hotkeyStr);
        Assert.Equal(expected, status);
    }

    [Fact]
    public void CheckHotkeyStatus_WhenMatchesActiveKey_ReturnsValid()
    {
        var status = GlobalHotkeyService.CheckHotkeyStatus("Ctrl+Alt+P", currentActiveHotkey: "Ctrl+Alt+P");
        Assert.Equal(HotkeyValidationState.Valid, status);
    }

    [Fact]
    public void SettingsWindow_InstantiatesAndLoadsDefaultValuesInSta()
    {
        RunInSta(() =>
        {
            var store = new SettingsStore(_settingsPath);
            var videoLib = new VideoLibraryService(store, fileExistsChecker: _ => false);
            var scheduler = new RandomReminderScheduler();
            using var coordinator = new OverlayCoordinator(scheduler, videoLib, store);

            var window = new SettingsWindow(store, videoLib, coordinator, scheduler);
            Assert.NotNull(window);

            // Trigger window load
            window.Show();
            Assert.Equal(15, window.MinIntervalSlider.Value);
            Assert.Equal(45, window.MaxIntervalSlider.Value);
            Assert.Equal(60, window.OpacitySlider.Value);
            Assert.Equal(85, window.VolumeSlider.Value);

            window.Close();
        });
    }

    [Fact]
    public void SettingsWindow_SliderValidation_KeepsMinLessThanOrEqualToMax()
    {
        RunInSta(() =>
        {
            var store = new SettingsStore(_settingsPath);
            var videoLib = new VideoLibraryService(store, fileExistsChecker: _ => false);
            var scheduler = new RandomReminderScheduler();
            using var coordinator = new OverlayCoordinator(scheduler, videoLib, store);

            var window = new SettingsWindow(store, videoLib, coordinator, scheduler);
            window.Show();

            // Set Min higher than Max
            window.MinIntervalSlider.Value = 60;
            Assert.True(window.MaxIntervalSlider.Value >= window.MinIntervalSlider.Value);

            window.Close();
        });
    }

    [Fact]
    public void SettingsWindow_EmptyVideosState_VisibilityReflectsLibraryContent()
    {
        RunInSta(() =>
        {
            var store = new SettingsStore(_settingsPath);
            var videoLib = new VideoLibraryService(store, fileExistsChecker: _ => true);
            var scheduler = new RandomReminderScheduler();
            using var coordinator = new OverlayCoordinator(scheduler, videoLib, store);

            var window = new SettingsWindow(store, videoLib, coordinator, scheduler);
            window.Show();

            // Initially empty
            Assert.Equal(Visibility.Visible, window.EmptyVideosBorder.Visibility);
            Assert.Equal(Visibility.Collapsed, window.VideosListBox.Visibility);

            // Add video
            videoLib.AddVideo(Path.Combine(_tempDir, "sample.mp4"), "Sample");
            window.Close();

            // Re-open with 1 video
            var windowWithVideos = new SettingsWindow(store, videoLib, coordinator, scheduler);
            windowWithVideos.Show();

            Assert.Equal(Visibility.Collapsed, windowWithVideos.EmptyVideosBorder.Visibility);
            Assert.Equal(Visibility.Visible, windowWithVideos.VideosListBox.Visibility);

            windowWithVideos.Close();
        });
    }

    [Fact]
    public void SettingsWindow_Footer_HasSaveAndCloseButtonsAndNoDuplicateTestButton()
    {
        RunInSta(() =>
        {
            var store = new SettingsStore(_settingsPath);
            var videoLib = new VideoLibraryService(store, fileExistsChecker: _ => false);
            var scheduler = new RandomReminderScheduler();
            using var coordinator = new OverlayCoordinator(scheduler, videoLib, store);

            var window = new SettingsWindow(store, videoLib, coordinator, scheduler);
            window.Show();

            Assert.NotNull(window.SaveButton);
            Assert.NotNull(window.CloseButton);

            // Verify BottomTestButton is no longer in the window
            var bottomTest = window.FindName("BottomTestButton");
            Assert.Null(bottomTest);

            window.Close();
        });
    }

    [Fact]
    public void Icons_AllRequiredSprint5VectorIconsExistInResourceDictionary()
    {
        RunInSta(() =>
        {
            var dict = new ResourceDictionary
            {
                Source = new Uri("/PersonalProvocateur.App;component/UI/Theme/Icons.xaml", UriKind.RelativeOrAbsolute)
            };

            string[] requiredIcons =
            [
                "IconBolt",
                "IconPlay",
                "IconPause",
                "IconTrash",
                "IconPlus",
                "IconVideo",
                "IconClock",
                "IconSettings",
                "IconEyeOff",
                "IconLogOut",
                "IconCheck",
                "IconWarning",
                "IconClose",
                "IconMinimize"
            ];

            foreach (var iconKey in requiredIcons)
            {
                Assert.True(dict.Contains(iconKey), $"Icon key '{iconKey}' must exist in Icons.xaml");
                var geom = dict[iconKey] as System.Windows.Media.StreamGeometry;
                Assert.NotNull(geom);
            }
        });
    }

    [Fact]
    public void DarkTheme_MacOsToggleSwitchStyle_ExistsAndCanBeInstantiated()
    {
        RunInSta(() =>
        {
            var dict = new ResourceDictionary
            {
                Source = new Uri("/PersonalProvocateur.App;component/UI/Theme/DarkTheme.xaml", UriKind.RelativeOrAbsolute)
            };

            Assert.True(dict.Contains("MacOsToggleSwitchStyle"));
            var style = dict["MacOsToggleSwitchStyle"] as Style;
            Assert.NotNull(style);
            Assert.Equal(typeof(System.Windows.Controls.CheckBox), style.TargetType);
        });
    }

    [Fact]
    public void TrayPopupMenu_InstantiatesAndLoadsWithoutErrorsInSta()
    {
        RunInSta(() =>
        {
            var store = new SettingsStore(_settingsPath);
            var videoLib = new VideoLibraryService(store, fileExistsChecker: _ => false);
            var scheduler = new RandomReminderScheduler();
            using var coordinator = new OverlayCoordinator(scheduler, videoLib, store);

            var menu = new TrayPopupMenu(coordinator, scheduler, store, () => { });
            Assert.NotNull(menu);
            menu.Show();

            Assert.NotNull(menu.TriggerNowButton);
            Assert.NotNull(menu.PauseResumeButton);
            Assert.NotNull(menu.SettingsButton);
            Assert.NotNull(menu.DismissOverlayButton);
            Assert.NotNull(menu.ExitButton);

            menu.Close();
        });
    }

    [Fact]
    public void SettingsWindow_ShowToast_ShowsToastBannerInSta()
    {
        RunInSta(() =>
        {
            var store = new SettingsStore(_settingsPath);
            var videoLib = new VideoLibraryService(store, fileExistsChecker: _ => false);
            var scheduler = new RandomReminderScheduler();
            using var coordinator = new OverlayCoordinator(scheduler, videoLib, store);

            var window = new SettingsWindow(store, videoLib, coordinator, scheduler);
            window.Show();

            window.ShowToast("Тестовое уведомление", isSuccess: true);

            Assert.Equal(Visibility.Visible, window.ToastHudBorder.Visibility);
            Assert.Equal("Тестовое уведомление", window.ToastMessageTextBlock.Text);

            window.Close();
        });
    }
}
