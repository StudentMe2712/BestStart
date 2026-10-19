using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using PersonalProvocateur.App.Models;
using PersonalProvocateur.App.Services;
using PersonalProvocateur.App.Storage;
using PersonalProvocateur.App.WindowsInterop;

namespace PersonalProvocateur.App.UI;

/// <summary>
/// Settings window for configuring intervals, dimming, hotkeys, and video assets.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsStore? _settingsStore;
    private readonly VideoLibraryService? _videoLibrary;
    private readonly OverlayCoordinator? _coordinator;
    private readonly ObservableCollection<VideoItem> _videos = new();

    /// <summary>
    /// Initializes a new instance of <see cref="SettingsWindow"/>.
    /// </summary>
    public SettingsWindow(
        SettingsStore settingsStore,
        VideoLibraryService videoLibrary,
        OverlayCoordinator coordinator)
    {
        InitializeComponent();

        _settingsStore = settingsStore;
        _videoLibrary = videoLibrary;
        _coordinator = coordinator;

        VideosListBox.ItemsSource = _videos;

        Loaded += OnLoaded;
    }

    /// <summary>
    /// Parameterless constructor for designer and testing support.
    /// </summary>
    public SettingsWindow() : this(null!, null!, null!)
    {
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadValues();
    }

    private void LoadValues()
    {
        if (_settingsStore == null)
        {
            return;
        }

        var s = _settingsStore.CurrentSettings;

        MinIntervalSlider.Value = s.MinIntervalMinutes;
        MaxIntervalSlider.Value = s.MaxIntervalMinutes;
        OpacitySlider.Value = (int)Math.Round(s.DimmingOpacity * 100);
        VolumeSlider.Value = (int)Math.Round(s.VideoVolume * 100);

        TriggerHotkeyTextBox.Text = s.TriggerHotkey;
        PanicHotkeyTextBox.Text = s.PanicHotkey;

        RunOnStartupCheckBox.IsChecked = s.RunOnStartup;

        RefreshVideosList();
    }

    private void RefreshVideosList()
    {
        _videos.Clear();
        if (_videoLibrary != null)
        {
            foreach (var v in _videoLibrary.GetAllVideos())
            {
                _videos.Add(v);
            }
        }
        else if (_settingsStore != null)
        {
            foreach (var v in _settingsStore.CurrentSettings.Videos)
            {
                _videos.Add(v);
            }
        }

        VideoCountTextBlock.Text = $"Всего роликов: {_videos.Count} (активно: {_videos.Count(v => v.IsEnabled)})";
    }

    private void IntervalSliders_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MinIntervalSlider != null && MaxIntervalSlider != null)
        {
            if (MinIntervalSlider.Value > MaxIntervalSlider.Value)
            {
                MaxIntervalSlider.Value = MinIntervalSlider.Value;
            }
        }
    }

    private void AddVideoButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Выберите видеофайл для провокатора",
            Filter = "Видео MP4 (*.mp4)|*.mp4|Все поддерживаемые (*.mp4;*.wmv;*.avi)|*.mp4;*.wmv;*.avi|Все файлы (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            foreach (var file in dialog.FileNames)
            {
                if (File.Exists(file))
                {
                    _videoLibrary?.AddVideo(file);
                }
            }

            RefreshVideosList();
        }
    }

    private void RemoveVideoButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is VideoItem item)
        {
            _videoLibrary?.RemoveVideo(item.Id);
            RefreshVideosList();
        }
    }

    private void VideoCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is VideoItem item)
        {
            _videoLibrary?.ToggleVideo(item.Id, item.IsEnabled);
            RefreshVideosList();
        }
    }

    private void TestOverlayButton_Click(object sender, RoutedEventArgs e)
    {
        _coordinator?.TriggerProvocation(true);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsStore == null)
        {
            Close();
            return;
        }

        int min = (int)MinIntervalSlider.Value;
        int max = (int)MaxIntervalSlider.Value;
        if (max < min)
        {
            max = min;
        }

        double opacity = Math.Clamp(OpacitySlider.Value / 100.0, 0.10, 0.95);
        double volume = Math.Clamp(VolumeSlider.Value / 100.0, 0.0, 1.0);

        string triggerKey = TriggerHotkeyTextBox.Text.Trim();
        string panicKey = PanicHotkeyTextBox.Text.Trim();
        bool runOnStartup = RunOnStartupCheckBox.IsChecked ?? false;

        _settingsStore.Update(s =>
        {
            s.MinIntervalMinutes = min;
            s.MaxIntervalMinutes = max;
            s.DimmingOpacity = opacity;
            s.VideoVolume = volume;
            if (!string.IsNullOrWhiteSpace(triggerKey))
            {
                s.TriggerHotkey = triggerKey;
            }
            if (!string.IsNullOrWhiteSpace(panicKey))
            {
                s.PanicHotkey = panicKey;
            }
            s.RunOnStartup = runOnStartup;
        });

        // Apply startup configuration
        StartupManager.SetRunOnStartup(runOnStartup);

        System.Windows.MessageBox.Show(
            this,
            "Настройки успешно сохранены!",
            "Personal Provocateur",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
