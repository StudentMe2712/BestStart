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

    private static void EnsureWpfInitialized()
    {
        if (!UriParser.IsKnownScheme("pack"))
        {
            _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
        }
        if (Application.ResourceAssembly == null)
        {
            Application.ResourceAssembly = typeof(SettingsWindow).Assembly;
        }
    }

    private static void RunInSta(Action action)
    {
        Exception? caught = null;
        var thread = new Thread(() =>
        {
            try
            {
                EnsureWpfInitialized();
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
        Assert.False(vm.IsText);
        Assert.Equal("ВИДЕО", vm.ContentTypeBadgeText);
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
        Assert.False(vm.IsText);
        Assert.Equal("ВИДЕО", vm.ContentTypeBadgeText);
        Assert.Equal("Не найден", vm.AvailabilityText);
        Assert.NotNull(vm.AvailabilityBackground);
        Assert.NotNull(vm.AvailabilityForeground);
    }

    [Fact]
    public void VideoItemViewModel_WhenTextItem_SetsProperBadgesAndAvailability()
    {
        var item = new VideoItem
        {
            Title = "Сделай разминку",
            ContentType = ProvocationContentType.Text,
            TextContent = "Сделай разминку для глаз прямо сейчас!",
            IsEnabled = true
        };

        var vm = new VideoItemViewModel(item, isAvailable: true);

        Assert.True(vm.IsText);
        Assert.Equal("ТЕКСТ", vm.ContentTypeBadgeText);
        Assert.Equal("Готов к показу", vm.AvailabilityText);
        Assert.Equal("Сделай разминку для глаз прямо сейчас!", vm.SecondaryText);
        Assert.NotNull(vm.ContentTypeBadgeBackground);
        Assert.NotNull(vm.ContentTypeBadgeForeground);
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

            Assert.Equal("Provocateur", window.Title);
            Assert.Equal(860, window.Width);
            Assert.Equal(600, window.Height);
            Assert.True(window.ShowInTaskbar);

            Assert.Equal("5", window.MinIntervalTextBox.Text);
            Assert.Equal("20", window.MaxIntervalTextBox.Text);
            Assert.Equal(60, window.OpacitySlider.Value);
            Assert.Equal(85, window.VolumeSlider.Value);
            Assert.NotNull(window.TargetMonitorComboBox);
            Assert.NotNull(window.SaveButton);

            window.Close();
        });
    }

    [Fact]
    public void SettingsWindow_NumericInputs_ManualInputValidatesAndSaves()
    {
        RunInSta(() =>
        {
            var store = new SettingsStore(_settingsPath);
            var videoLib = new VideoLibraryService(store, fileExistsChecker: _ => false);
            var scheduler = new RandomReminderScheduler();
            using var coordinator = new OverlayCoordinator(scheduler, videoLib, store);

            var window = new SettingsWindow(store, videoLib, coordinator, scheduler);
            window.Show();

            // Initial: 5 and 20
            Assert.Equal("5", window.MinIntervalTextBox.Text);
            Assert.Equal("20", window.MaxIntervalTextBox.Text);

            // Manual input
            window.MinIntervalTextBox.Text = "8";
            window.MaxIntervalTextBox.Text = "35";

            Assert.Equal(8, window.GetMinInterval());
            Assert.Equal(35, window.GetMaxInterval());

            // Trigger auto-save immediately
            window.TriggerAutoSave(immediate: true);

            Assert.Equal(8, store.CurrentSettings.MinIntervalMinutes);
            Assert.Equal(35, store.CurrentSettings.MaxIntervalMinutes);

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
    public void SettingsWindow_SidebarNavigation_HasThreeSectionsAndSwitchesContent()
    {
        RunInSta(() =>
        {
            var store = new SettingsStore(_settingsPath);
            var videoLib = new VideoLibraryService(store, fileExistsChecker: _ => false);
            var scheduler = new RandomReminderScheduler();
            using var coordinator = new OverlayCoordinator(scheduler, videoLib, store);

            var window = new SettingsWindow(store, videoLib, coordinator, scheduler);
            window.Show();

            // Verify three sidebar navigation buttons exist
            Assert.NotNull(window.NavContentButton);
            Assert.NotNull(window.NavScheduleButton);
            Assert.NotNull(window.NavSystemButton);

            // Verify backward compatibility aliases
            Assert.Same(window.NavContentButton, window.TabVideosButton);
            Assert.Same(window.NavScheduleButton, window.TabAppearanceButton);
            Assert.Same(window.NavSystemButton, window.TabSystemButton);

            Assert.NotNull(window.TabVideosContent);
            Assert.NotNull(window.TabAppearanceContent);
            Assert.NotNull(window.TabSystemContent);

            // Verify SaveButton is present (ADR-018)
            var saveBtn = window.FindName("SaveButton");
            Assert.NotNull(saveBtn);
            Assert.Same(window.SaveButton, saveBtn);

            // Verify old footer buttons are absent
            var closeBtn = window.FindName("CloseButton");
            Assert.Null(closeBtn);

            var bottomTest = window.FindName("BottomTestButton");
            Assert.Null(bottomTest);

            // Verify tab switching transitions
            Assert.Equal(0, window.SelectedTabIndex);
            Assert.Equal(Visibility.Visible, window.TabVideosContent.Visibility);
            Assert.Equal(Visibility.Collapsed, window.TabAppearanceContent.Visibility);
            Assert.Equal(Visibility.Collapsed, window.TabSystemContent.Visibility);

            // Switch to Schedule & Mascot tab
            window.NavScheduleButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal(1, window.SelectedTabIndex);
            Assert.Equal(Visibility.Collapsed, window.TabVideosContent.Visibility);
            Assert.Equal(Visibility.Visible, window.TabAppearanceContent.Visibility);
            Assert.Equal(Visibility.Collapsed, window.TabSystemContent.Visibility);

            // Switch to System tab
            window.NavSystemButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal(2, window.SelectedTabIndex);
            Assert.Equal(Visibility.Collapsed, window.TabVideosContent.Visibility);
            Assert.Equal(Visibility.Collapsed, window.TabAppearanceContent.Visibility);
            Assert.Equal(Visibility.Visible, window.TabSystemContent.Visibility);

            // Switch back to Content tab
            window.NavContentButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal(0, window.SelectedTabIndex);
            Assert.Equal(Visibility.Visible, window.TabVideosContent.Visibility);
            Assert.Equal(Visibility.Collapsed, window.TabAppearanceContent.Visibility);
            Assert.Equal(Visibility.Collapsed, window.TabSystemContent.Visibility);

            window.Close();
        });
    }

    [Fact]
    public void SettingsWindow_SingleAddButton_OpensModalOverlay()
    {
        RunInSta(() =>
        {
            var store = new SettingsStore(_settingsPath);
            var videoLib = new VideoLibraryService(store, fileExistsChecker: _ => false);
            var scheduler = new RandomReminderScheduler();
            using var coordinator = new OverlayCoordinator(scheduler, videoLib, store);

            var window = new SettingsWindow(store, videoLib, coordinator, scheduler);
            window.Show();

            Assert.NotNull(window.OpenAddModalButton);
            Assert.NotNull(window.AddContentModalOverlay);
            Assert.NotNull(window.CloseAddModalButton);
            Assert.NotNull(window.AddVideoModalCardButton);
            Assert.NotNull(window.AddTextModalCardButton);

            // Initially modal is collapsed
            Assert.Equal(Visibility.Collapsed, window.AddContentModalOverlay.Visibility);

            // Click OpenAddModalButton
            window.OpenAddModalButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal(Visibility.Visible, window.AddContentModalOverlay.Visibility);
            Assert.Equal(Visibility.Visible, window.ModalSelectionView.Visibility);
            Assert.Equal(Visibility.Collapsed, window.ModalTextInputView.Visibility);

            // Click AddTextModalCardButton to switch to text input
            window.AddTextModalCardButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal(Visibility.Collapsed, window.ModalSelectionView.Visibility);
            Assert.Equal(Visibility.Visible, window.ModalTextInputView.Visibility);

            // Click CancelAddTextButton to go back to selection view
            window.CancelAddTextButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal(Visibility.Visible, window.ModalSelectionView.Visibility);
            Assert.Equal(Visibility.Collapsed, window.ModalTextInputView.Visibility);

            // Click CloseAddModalButton to close overlay
            window.CloseAddModalButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal(Visibility.Collapsed, window.AddContentModalOverlay.Visibility);

            window.Close();
        });
    }

    [Fact]
    public void SettingsWindow_AutoSave_SavesSettingsToStoreAndTriggersBadge()
    {
        RunInSta(() =>
        {
            var store = new SettingsStore(_settingsPath);
            var videoLib = new VideoLibraryService(store, fileExistsChecker: _ => false);
            var scheduler = new RandomReminderScheduler();
            using var coordinator = new OverlayCoordinator(scheduler, videoLib, store);

            var window = new SettingsWindow(store, videoLib, coordinator, scheduler);
            window.Show();

            Assert.NotNull(window.AutoSaveBadge);

            window.MinIntervalTextBox.Text = "22";
            window.MaxIntervalTextBox.Text = "55";
            window.OpacitySlider.Value = 75;
            window.VolumeSlider.Value = 90;

            window.TriggerAutoSave(immediate: true);

            Assert.Equal(22, store.CurrentSettings.MinIntervalMinutes);
            Assert.Equal(55, store.CurrentSettings.MaxIntervalMinutes);
            Assert.Equal(0.75, store.CurrentSettings.DimmingOpacity, precision: 2);
            Assert.Equal(0.90, store.CurrentSettings.VideoVolume, precision: 2);

            window.Close();
        });
    }

    [Fact]
    public void SettingsWindow_SaveButton_SavesSettingsAndClosesWindow()
    {
        RunInSta(() =>
        {
            var store = new SettingsStore(_settingsPath);
            var videoLib = new VideoLibraryService(store, fileExistsChecker: _ => false);
            var scheduler = new RandomReminderScheduler();
            using var coordinator = new OverlayCoordinator(scheduler, videoLib, store);

            var window = new SettingsWindow(store, videoLib, coordinator, scheduler);
            window.Show();

            window.MinIntervalTextBox.Text = "15";
            window.MaxIntervalTextBox.Text = "45";

            window.SaveButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

            Assert.Equal(15, store.CurrentSettings.MinIntervalMinutes);
            Assert.Equal(45, store.CurrentSettings.MaxIntervalMinutes);
        });
    }

    [Fact]
    public void SettingsWindow_SkinSelection_UpdatesMascotSkin()
    {
        RunInSta(() =>
        {
            var store = new SettingsStore(_settingsPath);
            var videoLib = new VideoLibraryService(store, fileExistsChecker: _ => false);
            var scheduler = new RandomReminderScheduler();
            using var coordinator = new OverlayCoordinator(scheduler, videoLib, store);

            var window = new SettingsWindow(store, videoLib, coordinator, scheduler);
            window.Show();

            Assert.NotNull(window.SkinDynamicOrbButton);
            Assert.NotNull(window.SkinCyberVisorButton);
            Assert.NotNull(window.SkinCustomImageButton);

            // Initially DynamicOrb
            Assert.Equal("DynamicOrb", window.SelectedSkin);

            // Switch to CyberVisor
            window.SelectSkin("CyberVisor");
            Assert.Equal("CyberVisor", window.SelectedSkin);
            Assert.Equal("CyberVisor", store.CurrentSettings.MascotSkin);

            // Switch back to DynamicOrb
            window.SelectSkin("DynamicOrb");
            Assert.Equal("DynamicOrb", window.SelectedSkin);
            Assert.Equal("DynamicOrb", store.CurrentSettings.MascotSkin);

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
                "IconMinimize",
                "IconBrandPrism"
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
