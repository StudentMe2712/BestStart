using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using PersonalProvocateur.App.Models;
using PersonalProvocateur.App.Services;
using PersonalProvocateur.App.Storage;
using PersonalProvocateur.App.WindowsInterop;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;
using TextBox = System.Windows.Controls.TextBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace PersonalProvocateur.App.UI;

/// <summary>
/// Full-featured frameless macOS-styled dashboard window for managing provocation intervals,
/// dimming, hotkey registration, auto-startup, and the video library.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsStore? _settingsStore;
    private readonly VideoLibraryService? _videoLibrary;
    private readonly OverlayCoordinator? _coordinator;
    private readonly RandomReminderScheduler? _scheduler;
    private readonly DispatcherTimer? _statusTimer;
    private DispatcherTimer? _toastTimer;
    private readonly ObservableCollection<VideoItemViewModel> _videos = new();

    private bool _isPaused;
    private string? _initialTriggerHotkey;
    private string? _initialPanicHotkey;

    /// <summary>
    /// Initializes a new instance of <see cref="SettingsWindow"/>.
    /// </summary>
    public SettingsWindow(
        SettingsStore settingsStore,
        VideoLibraryService videoLibrary,
        OverlayCoordinator coordinator,
        RandomReminderScheduler? scheduler = null)
    {
        InitializeComponent();

        _settingsStore = settingsStore;
        _videoLibrary = videoLibrary;
        _coordinator = coordinator;
        _scheduler = scheduler ?? coordinator?.Scheduler;

        VideosListBox.ItemsSource = _videos;

        Loaded += OnLoaded;

        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _statusTimer.Tick += (s, e) => RefreshStatusHeader();
    }

    /// <summary>
    /// Parameterless constructor for designer and testing support.
    /// </summary>
    public SettingsWindow() : this(null!, null!, null!, null)
    {
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadValues();
        _statusTimer?.Start();
    }

    private void LoadValues()
    {
        if (_settingsStore == null)
        {
            return;
        }

        var s = _settingsStore.CurrentSettings;
        _isPaused = s.IsPaused;

        MinIntervalSlider.Value = s.MinIntervalMinutes;
        MaxIntervalSlider.Value = s.MaxIntervalMinutes;
        OpacitySlider.Value = (int)Math.Round(s.DimmingOpacity * 100);
        VolumeSlider.Value = (int)Math.Round(s.VideoVolume * 100);

        _initialTriggerHotkey = s.TriggerHotkey;
        _initialPanicHotkey = s.PanicHotkey;

        TriggerHotkeyTextBox.Text = s.TriggerHotkey;
        PanicHotkeyTextBox.Text = s.PanicHotkey;

        RunOnStartupCheckBox.IsChecked = StartupManager.IsRunOnStartupEnabled();

        RefreshVideosList();
        RefreshStatusHeader();
        ValidateAllHotkeys();
    }

    private void RefreshStatusHeader()
    {
        if (StatusBadgeBorder == null || StatusBadgeText == null || QuickPauseText == null)
        {
            return;
        }

        if (_isPaused)
        {
            StatusBadgeBorder.Background = new SolidColorBrush(Color.FromArgb(0x1A, 0xF5, 0x9E, 0x0B));
            StatusBadgeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
            if (StatusBadgeDot != null)
            {
                StatusBadgeDot.Fill = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
            }
            StatusBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
            StatusBadgeText.Text = "На паузе";

            if (QuickPauseIcon != null && TryFindResource("IconPlay") is Geometry playGeom)
            {
                QuickPauseIcon.Data = playGeom;
                QuickPauseIcon.Fill = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6));
                QuickPauseIcon.Stroke = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6));
                QuickPauseIcon.StrokeThickness = 1;
            }
            QuickPauseText.Text = "Возобновить";
            NextTriggerTextBlock.Text = "Таймер остановлен (на паузе)";
        }
        else
        {
            StatusBadgeBorder.Background = new SolidColorBrush(Color.FromArgb(0x1A, 0x10, 0xB9, 0x81));
            StatusBadgeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
            if (StatusBadgeDot != null)
            {
                StatusBadgeDot.Fill = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
            }
            StatusBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
            StatusBadgeText.Text = "Активен";

            if (QuickPauseIcon != null && TryFindResource("IconPause") is Geometry pauseGeom)
            {
                QuickPauseIcon.Data = pauseGeom;
                QuickPauseIcon.Fill = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6));
                QuickPauseIcon.Stroke = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6));
                QuickPauseIcon.StrokeThickness = 1;
            }
            QuickPauseText.Text = "Пауза";

            var next = _scheduler?.NextTriggerTimeLocal ?? _coordinator?.Scheduler?.NextTriggerTimeLocal;
            if (next.HasValue)
            {
                var remaining = next.Value - DateTime.Now;
                int minutes = Math.Max(0, (int)Math.Ceiling(remaining.TotalMinutes));
                NextTriggerTextBlock.Text = $"Следующее появление: {next.Value:HH:mm} (через ~{minutes} мин)";
            }
            else
            {
                NextTriggerTextBlock.Text = "Ожидание запуска сессии...";
            }
        }
    }

    private void RefreshVideosList()
    {
        _videos.Clear();
        var rawVideos = _videoLibrary != null
            ? _videoLibrary.GetAllVideos()
            : _settingsStore?.CurrentSettings.Videos ?? (IEnumerable<VideoItem>)Array.Empty<VideoItem>();

        int activeCount = 0;
        int availableCount = 0;

        foreach (var v in rawVideos)
        {
            bool exists = !string.IsNullOrWhiteSpace(v.FilePath) && File.Exists(v.FilePath);
            if (exists)
            {
                availableCount++;
            }
            if (v.IsEnabled)
            {
                activeCount++;
            }

            _videos.Add(new VideoItemViewModel(v, exists));
        }

        if (_videos.Count == 0)
        {
            EmptyVideosBorder.Visibility = Visibility.Visible;
            VideosListBox.Visibility = Visibility.Collapsed;
        }
        else
        {
            EmptyVideosBorder.Visibility = Visibility.Collapsed;
            VideosListBox.Visibility = Visibility.Visible;
        }

        VideoCountTextBlock.Text = $"Всего роликов: {_videos.Count} (активно: {activeCount}, доступно на диске: {availableCount})";
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

    private void HotkeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ValidateAllHotkeys();
    }

    /// <summary>
    /// Interactive Hotkey Recorder: captures pressed modifier and character/function keys,
    /// formats them into a clean string (e.g. "Ctrl+Alt+P"), and automatically validates availability.
    /// </summary>
    private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var rawKey = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignore standalone modifier keypresses without a trailing key
        if (rawKey is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin)
        {
            return;
        }

        var modifiers = Keyboard.Modifiers;
        bool hasCtrl = (modifiers & ModifierKeys.Control) != 0;
        bool hasAlt = (modifiers & ModifierKeys.Alt) != 0;
        bool hasShift = (modifiers & ModifierKeys.Shift) != 0;
        bool hasWin = (modifiers & ModifierKeys.Windows) != 0
                      || Keyboard.IsKeyDown(Key.LWin)
                      || Keyboard.IsKeyDown(Key.RWin);

        string? keyName = rawKey switch
        {
            >= Key.A and <= Key.Z => rawKey.ToString().ToUpperInvariant(),
            >= Key.D0 and <= Key.D9 => ((int)(rawKey - Key.D0)).ToString(),
            >= Key.NumPad0 and <= Key.NumPad9 => ((int)(rawKey - Key.NumPad0)).ToString(),
            >= Key.F1 and <= Key.F12 => rawKey.ToString(),
            Key.Escape => "Esc",
            Key.Space => "Space",
            Key.Tab => "Tab",
            Key.Enter or Key.Return => "Enter",
            Key.Back => "Backspace",
            Key.Delete => "Delete",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            _ => null
        };

        if (keyName == null)
        {
            return;
        }

        var parts = new List<string>(4);
        if (hasCtrl) parts.Add("Ctrl");
        if (hasAlt) parts.Add("Alt");
        if (hasShift) parts.Add("Shift");
        if (hasWin) parts.Add("Win");
        parts.Add(keyName);

        string hotkeyStr = string.Join("+", parts);

        if (sender is TextBox textBox)
        {
            textBox.Text = hotkeyStr;
            textBox.Select(textBox.Text.Length, 0);
            ValidateAllHotkeys();
            Keyboard.ClearFocus();
        }

        e.Handled = true;
    }

    private void ValidateAllHotkeys()
    {
        if (TriggerHotkeyTextBox != null && TriggerHotkeyStatusText != null)
        {
            UpdateHotkeyStatusUi(TriggerHotkeyTextBox.Text, _initialTriggerHotkey, TriggerHotkeyStatusText);
        }

        if (PanicHotkeyTextBox != null && PanicHotkeyStatusText != null)
        {
            UpdateHotkeyStatusUi(PanicHotkeyTextBox.Text, _initialPanicHotkey, PanicHotkeyStatusText);
        }
    }

    private static void UpdateHotkeyStatusUi(string hotkeyText, string? activeKey, TextBlock statusBlock)
    {
        var status = GlobalHotkeyService.CheckHotkeyStatus(hotkeyText, activeKey);
        switch (status)
        {
            case HotkeyValidationState.Valid:
                statusBlock.Text = "Сочетание доступно";
                statusBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                break;
            case HotkeyValidationState.OccupiedByAnotherApp:
                statusBlock.Text = "Сочетание занято другой программой";
                statusBlock.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                break;
            case HotkeyValidationState.InvalidFormat:
                statusBlock.Text = "Некорректный формат (например: Ctrl+Alt+P)";
                statusBlock.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                break;
            case HotkeyValidationState.Empty:
                statusBlock.Text = "Введите сочетание клавиш";
                statusBlock.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                break;
        }
    }

    private void AddVideoButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите .mp4 видеофайлы для библиотеки",
            Filter = "MP4 Видео (*.mp4)|*.mp4|Все поддерживаемые (*.mp4;*.wmv;*.avi)|*.mp4;*.wmv;*.avi|Все файлы (*.*)|*.*",
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
            ShowToast("Видео успешно добавлены в библиотеку", isSuccess: true);
        }
    }

    private void RemoveVideoButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is VideoItemViewModel vm)
        {
            _videoLibrary?.RemoveVideo(vm.Id);
            RefreshVideosList();
        }
    }

    private void VideoCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is VideoItemViewModel vm)
        {
            _videoLibrary?.ToggleVideo(vm.Id, vm.IsEnabled);
            RefreshVideosList();
        }
    }

    private void TestSingleVideo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is VideoItemViewModel vm)
        {
            if (!File.Exists(vm.FilePath))
            {
                ShowToast($"Файл не найден на диске: {Path.GetFileName(vm.FilePath)}", isSuccess: false);
                return;
            }

            if (_coordinator != null && _settingsStore != null)
            {
                var videoWin = new VideoWindow(_coordinator, _settingsStore);
                videoWin.Owner = this;
                videoWin.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                videoWin.Show();
                videoWin.PlayVideo(vm.FilePath, vm.Title);
            }
        }
    }

    private void QuickTestButton_Click(object sender, RoutedEventArgs e)
    {
        _coordinator?.TriggerProvocation(isManualTest: true);
    }

    private void QuickPauseButton_Click(object sender, RoutedEventArgs e)
    {
        _isPaused = !_isPaused;
        _coordinator?.SetPaused(_isPaused);
        RefreshStatusHeader();
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

        // Apply startup configuration to Windows Registry
        StartupManager.SetRunOnStartup(runOnStartup);

        // Display floating macOS Toast HUD and smoothly close after delay
        ShowToast("Настройки сохранены", isSuccess: true, onCompleted: () =>
        {
            try
            {
                Close();
            }
            catch
            {
                // Window might already be closed by user
            }
        });
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void WindowMinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void WindowCloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Displays a floating macOS Toast HUD banner with icon and smooth fade/translate animations.
    /// </summary>
    /// <param name="message">Notification message text.</param>
    /// <param name="isSuccess">True for green success indicator; false for red warning indicator.</param>
    /// <param name="onCompleted">Optional callback invoked after the toast finishes its display cycle.</param>
    public void ShowToast(string message, bool isSuccess = true, Action? onCompleted = null)
    {
        if (ToastHudBorder == null || ToastMessageTextBlock == null)
        {
            onCompleted?.Invoke();
            return;
        }

        _toastTimer?.Stop();

        ToastMessageTextBlock.Text = message;

        if (isSuccess)
        {
            var successBrush = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
            ToastHudBorder.BorderBrush = successBrush;
            if (ToastIcon != null)
            {
                ToastIcon.Stroke = successBrush;
                if (TryFindResource("IconCheck") is Geometry checkGeom)
                {
                    ToastIcon.Data = checkGeom;
                }
            }
        }
        else
        {
            var dangerBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
            ToastHudBorder.BorderBrush = dangerBrush;
            if (ToastIcon != null)
            {
                ToastIcon.Stroke = dangerBrush;
                if (TryFindResource("IconWarning") is Geometry alertGeom)
                {
                    ToastIcon.Data = alertGeom;
                }
            }
        }

        ToastHudBorder.Visibility = Visibility.Visible;
        var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var slideIn = new DoubleAnimation(20.0, 0.0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        ToastHudBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        ToastTransform?.BeginAnimation(TranslateTransform.YProperty, slideIn);

        _toastTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.2)
        };
        _toastTimer.Tick += (s, e) =>
        {
            _toastTimer.Stop();
            var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            var slideOut = new DoubleAnimation(0.0, 20.0, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            fadeOut.Completed += (_, _) =>
            {
                ToastHudBorder.Visibility = Visibility.Collapsed;
                onCompleted?.Invoke();
            };
            ToastHudBorder.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            ToastTransform?.BeginAnimation(TranslateTransform.YProperty, slideOut);
        };
        _toastTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _statusTimer?.Stop();
        _toastTimer?.Stop();
        base.OnClosed(e);
    }
}

/// <summary>
/// ViewModel wrapping <see cref="VideoItem"/> for rich databinding with disk availability indicators.
/// </summary>
public class VideoItemViewModel
{
    private static readonly Brush AvailableBg = CreateFrozenBrush(0x1A, 0x10, 0xB9, 0x81);
    private static readonly Brush AvailableFg = CreateFrozenBrush(0xFF, 0x10, 0xB9, 0x81);
    private static readonly Brush MissingBg = CreateFrozenBrush(0x1A, 0xEF, 0x44, 0x44);
    private static readonly Brush MissingFg = CreateFrozenBrush(0xFF, 0xEF, 0x44, 0x44);

    private static Brush CreateFrozenBrush(byte a, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    public VideoItem Item { get; }
    public bool IsAvailable { get; }

    public Guid Id => Item.Id;
    public string Title => Item.Title;
    public string FilePath => Item.FilePath;

    public bool IsEnabled
    {
        get => Item.IsEnabled;
        set => Item.IsEnabled = value;
    }

    public string AvailabilityText => IsAvailable ? "Доступен" : "Не найден";
    public Brush AvailabilityBackground => IsAvailable ? AvailableBg : MissingBg;
    public Brush AvailabilityForeground => IsAvailable ? AvailableFg : MissingFg;

    public VideoItemViewModel(VideoItem item, bool isAvailable)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
        IsAvailable = isAvailable;
    }
}
