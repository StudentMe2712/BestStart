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
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;
using TextBox = System.Windows.Controls.TextBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace PersonalProvocateur.App.UI;

/// <summary>
/// Minimalist frameless macOS-styled dashboard window featuring Segmented Control tabs,
/// content management (MP4 videos and Spotlight text reminders), numeric interval steppers,
/// monitor targeting, instant debounced auto-saving, and skin selection (ADR-013, ADR-017, ADR-018).
/// </summary>
public partial class SettingsWindow : Window
{
    private static readonly Brush ActiveTabBg = CreateFrozenBrush(0xFF, 0x25, 0x29, 0x3A);
    private static readonly Brush ActiveTabFg = CreateFrozenBrush(0xFF, 0xF3, 0xF4, 0xF6);
    private static readonly Brush InactiveTabBg = Brushes.Transparent;
    private static readonly Brush InactiveTabFg = CreateFrozenBrush(0xFF, 0x9C, 0xA3, 0xAF);

    private static readonly Brush SelectedCardBg = CreateFrozenBrush(0x22, 0x8B, 0x5C, 0xF6);
    private static readonly Brush SelectedCardBorder = CreateFrozenBrush(0xFF, 0x8B, 0x5C, 0xF6);
    private static readonly Brush NormalCardBg = CreateFrozenBrush(0xFF, 0x13, 0x15, 0x1F);
    private static readonly Brush NormalCardBorder = CreateFrozenBrush(0xFF, 0x25, 0x29, 0x3A);

    private static Brush CreateFrozenBrush(byte a, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    private readonly SettingsStore? _settingsStore;
    private readonly VideoLibraryService? _videoLibrary;
    private readonly OverlayCoordinator? _coordinator;
    private readonly RandomReminderScheduler? _scheduler;
    private readonly DispatcherTimer? _statusTimer;
    private readonly DispatcherTimer _autoSaveDebounceTimer;
    private DispatcherTimer? _toastTimer;
    private readonly ObservableCollection<VideoItemViewModel> _videos = new();

    private bool _isPaused;
    private bool _isLoading = true;
    private string? _initialTriggerHotkey;
    private string? _initialPanicHotkey;
    private string _selectedSkin = "DynamicOrb";
    private string _customMascotPath = string.Empty;
    private int _selectedTabIndex = 0;

    /// <summary>
    /// Gets the currently selected tab index (0: Content, 1: Appearance &amp; Timer, 2: System).
    /// </summary>
    public int SelectedTabIndex => _selectedTabIndex;

    /// <summary>
    /// Gets the currently selected mascot skin ("DynamicOrb", "CyberVisor", "CustomImage").
    /// </summary>
    public string SelectedSkin => _selectedSkin;

    /// <summary>
    /// Gets the path to the custom mascot image if CustomImage skin is selected.
    /// </summary>
    public string CustomMascotPath => _customMascotPath;

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

        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _statusTimer.Tick += (s, e) => RefreshStatusHeader();

        _autoSaveDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _autoSaveDebounceTimer.Tick += (s, e) =>
        {
            _autoSaveDebounceTimer.Stop();
            ExecuteAutoSave();
        };

        LoadValues();

        Loaded += OnLoaded;
    }

    /// <summary>
    /// Parameterless constructor for designer and testing support.
    /// </summary>
    public SettingsWindow() : this(null!, null!, null!, null)
    {
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _statusTimer?.Start();
    }

    private void LoadValues()
    {
        _isLoading = true;
        try
        {
            try
            {
                Icon = TrayIconService.CreateTaskbarIconSource();
            }
            catch
            {
                // Ignore in testing / non-GDI+ environments
            }

            PopulateMonitors();

            if (_settingsStore != null)
            {
                var s = _settingsStore.CurrentSettings;
                _isPaused = s.IsPaused;

                MinIntervalTextBox.Text = s.MinIntervalMinutes.ToString();
                MaxIntervalTextBox.Text = s.MaxIntervalMinutes.ToString();
                OpacitySlider.Value = (int)Math.Round(s.DimmingOpacity * 100);
                VolumeSlider.Value = (int)Math.Round(s.VideoVolume * 100);

                if (s.TargetMonitorIndex >= 0 && s.TargetMonitorIndex < TargetMonitorComboBox.Items.Count)
                {
                    TargetMonitorComboBox.SelectedIndex = s.TargetMonitorIndex;
                }
                else if (TargetMonitorComboBox.Items.Count > 0)
                {
                    TargetMonitorComboBox.SelectedIndex = 0;
                }

                _initialTriggerHotkey = s.TriggerHotkey;
                _initialPanicHotkey = s.PanicHotkey;

                TriggerHotkeyTextBox.Text = s.TriggerHotkey;
                PanicHotkeyTextBox.Text = s.PanicHotkey;

                _selectedSkin = s.MascotSkin ?? "DynamicOrb";
                _customMascotPath = s.CustomMascotPath ?? string.Empty;

                try
                {
                    RunOnStartupCheckBox.IsChecked = StartupManager.IsRunOnStartupEnabled();
                }
                catch
                {
                    RunOnStartupCheckBox.IsChecked = s.RunOnStartup;
                }
            }
            else
            {
                MinIntervalTextBox.Text = "5";
                MaxIntervalTextBox.Text = "20";
                _selectedSkin = "DynamicOrb";
                _customMascotPath = string.Empty;
                if (TargetMonitorComboBox.Items.Count > 0)
                {
                    TargetMonitorComboBox.SelectedIndex = 0;
                }
            }

            UpdateSkinSelectionUi();
            SelectTab(0);
            RefreshVideosList();
            RefreshStatusHeader();
            ValidateAllHotkeys();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void PopulateMonitors()
    {
        if (TargetMonitorComboBox == null)
        {
            return;
        }

        TargetMonitorComboBox.Items.Clear();

        try
        {
            var screens = System.Windows.Forms.Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                var screen = screens[i];
                string primaryMarker = screen.Primary ? " (Основной)" : string.Empty;
                TargetMonitorComboBox.Items.Add($"Монитор {i + 1}: {screen.Bounds.Width}x{screen.Bounds.Height}{primaryMarker}");
            }
        }
        catch
        {
            TargetMonitorComboBox.Items.Add("Монитор 1 (Основной)");
        }
    }

    #region Auto-Save Engine (ADR-013, ADR-018)

    /// <summary>
    /// Triggers automatic saving of all settings to the store.
    /// When <paramref name="immediate"/> is false, debounces changes by 250 ms.
    /// </summary>
    /// <param name="immediate">True to save immediately without debouncing; false for slider debounce.</param>
    public void TriggerAutoSave(bool immediate = false)
    {
        if (_isLoading)
        {
            return;
        }

        if (immediate)
        {
            _autoSaveDebounceTimer.Stop();
            ExecuteAutoSave();
        }
        else
        {
            _autoSaveDebounceTimer.Stop();
            _autoSaveDebounceTimer.Start();
        }
    }

    private void ExecuteAutoSave()
    {
        int min = GetMinInterval();
        int max = GetMaxInterval();
        if (max < min)
        {
            max = min;
        }

        double opacity = OpacitySlider != null ? Math.Clamp(OpacitySlider.Value / 100.0, 0.10, 0.95) : 0.60;
        double volume = VolumeSlider != null ? Math.Clamp(VolumeSlider.Value / 100.0, 0.0, 1.0) : 0.85;

        int targetMonitorIndex = TargetMonitorComboBox?.SelectedIndex ?? 0;
        if (targetMonitorIndex < 0)
        {
            targetMonitorIndex = 0;
        }

        string triggerKey = TriggerHotkeyTextBox?.Text?.Trim() ?? "Ctrl+Alt+P";
        string panicKey = PanicHotkeyTextBox?.Text?.Trim() ?? "Ctrl+Alt+Shift+P";
        bool runOnStartup = RunOnStartupCheckBox?.IsChecked ?? false;
        string selectedSkin = _selectedSkin;
        string customPath = _customMascotPath;

        _settingsStore?.Update(s =>
        {
            s.MinIntervalMinutes = min;
            s.MaxIntervalMinutes = max;
            s.DimmingOpacity = opacity;
            s.VideoVolume = volume;
            s.TargetMonitorIndex = targetMonitorIndex;
            if (!string.IsNullOrWhiteSpace(triggerKey))
            {
                s.TriggerHotkey = triggerKey;
            }
            if (!string.IsNullOrWhiteSpace(panicKey))
            {
                s.PanicHotkey = panicKey;
            }
            s.RunOnStartup = runOnStartup;
            s.MascotSkin = selectedSkin;
            s.CustomMascotPath = customPath ?? string.Empty;
        });

        try
        {
            StartupManager.SetRunOnStartup(runOnStartup);
        }
        catch
        {
            // Ignore registry errors in test or restricted environments
        }

        FlashAutoSaveBadge();
    }

    private void FlashAutoSaveBadge()
    {
        if (AutoSaveBadge == null)
        {
            return;
        }

        var anim = new DoubleAnimationUsingKeyFrames();
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200))));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1000))));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1500))));
        AutoSaveBadge.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ExecuteAutoSave();

        int min = GetMinInterval();
        int max = GetMaxInterval();
        _scheduler?.UpdateIntervals(min, max);

        Close();
    }

    #endregion

    #region Segmented Control & Navigation

    /// <summary>
    /// Selects the active tab in the macOS Segmented Control (0: Content, 1: Appearance &amp; Timer, 2: System).
    /// </summary>
    /// <param name="index">Tab index.</param>
    public void SelectTab(int index)
    {
        _selectedTabIndex = Math.Clamp(index, 0, 2);

        if (TabVideosButton != null)
        {
            TabVideosButton.Background = _selectedTabIndex == 0 ? ActiveTabBg : InactiveTabBg;
            TabVideosButton.Foreground = _selectedTabIndex == 0 ? ActiveTabFg : InactiveTabFg;
            TabVideosButton.FontWeight = _selectedTabIndex == 0 ? FontWeights.SemiBold : FontWeights.Medium;
        }

        if (TabAppearanceButton != null)
        {
            TabAppearanceButton.Background = _selectedTabIndex == 1 ? ActiveTabBg : InactiveTabBg;
            TabAppearanceButton.Foreground = _selectedTabIndex == 1 ? ActiveTabFg : InactiveTabFg;
            TabAppearanceButton.FontWeight = _selectedTabIndex == 1 ? FontWeights.SemiBold : FontWeights.Medium;
        }

        if (TabSystemButton != null)
        {
            TabSystemButton.Background = _selectedTabIndex == 2 ? ActiveTabBg : InactiveTabBg;
            TabSystemButton.Foreground = _selectedTabIndex == 2 ? ActiveTabFg : InactiveTabFg;
            TabSystemButton.FontWeight = _selectedTabIndex == 2 ? FontWeights.SemiBold : FontWeights.Medium;
        }

        if (TabVideosContent != null)
        {
            TabVideosContent.Visibility = _selectedTabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        if (TabAppearanceContent != null)
        {
            TabAppearanceContent.Visibility = _selectedTabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        }

        if (TabSystemContent != null)
        {
            TabSystemContent.Visibility = _selectedTabIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void TabVideos_Click(object sender, RoutedEventArgs e) => SelectTab(0);

    private void TabAppearance_Click(object sender, RoutedEventArgs e) => SelectTab(1);

    private void TabSystem_Click(object sender, RoutedEventArgs e) => SelectTab(2);

    #endregion

    #region Mascot Skin Selection (ADR-012)

    /// <summary>
    /// Selects the mascot skin ("DynamicOrb", "CyberVisor", "CustomImage"),
    /// updates UI, applies the skin to the active overlay, and triggers auto-save.
    /// </summary>
    /// <param name="skin">Skin identifier.</param>
    /// <param name="customPath">Optional path to custom PNG image.</param>
    public void SelectSkin(string skin, string? customPath = null)
    {
        if (skin != "CyberVisor" && skin != "CustomImage")
        {
            skin = "DynamicOrb";
        }

        _selectedSkin = skin;
        if (customPath != null)
        {
            _customMascotPath = customPath;
        }

        UpdateSkinSelectionUi();

        _coordinator?.ActiveOverlayWindow?.Mascot?.ApplySkin(_selectedSkin, _customMascotPath);

        TriggerAutoSave(immediate: true);
    }

    private void UpdateSkinSelectionUi()
    {
        if (SkinDynamicOrbButton != null)
        {
            bool isOrb = _selectedSkin == "DynamicOrb";
            SkinDynamicOrbButton.Background = isOrb ? SelectedCardBg : NormalCardBg;
            SkinDynamicOrbButton.BorderBrush = isOrb ? SelectedCardBorder : NormalCardBorder;
            SkinDynamicOrbButton.BorderThickness = new Thickness(isOrb ? 1.5 : 1);
        }

        if (SkinCyberVisorButton != null)
        {
            bool isVisor = _selectedSkin == "CyberVisor";
            SkinCyberVisorButton.Background = isVisor ? SelectedCardBg : NormalCardBg;
            SkinCyberVisorButton.BorderBrush = isVisor ? SelectedCardBorder : NormalCardBorder;
            SkinCyberVisorButton.BorderThickness = new Thickness(isVisor ? 1.5 : 1);
        }

        if (SkinCustomImageButton != null)
        {
            bool isCustom = _selectedSkin == "CustomImage";
            SkinCustomImageButton.Background = isCustom ? SelectedCardBg : NormalCardBg;
            SkinCustomImageButton.BorderBrush = isCustom ? SelectedCardBorder : NormalCardBorder;
            SkinCustomImageButton.BorderThickness = new Thickness(isCustom ? 1.5 : 1);

            if (CustomSkinSubtitleText != null)
            {
                if (isCustom && !string.IsNullOrWhiteSpace(_customMascotPath))
                {
                    CustomSkinSubtitleText.Text = Path.GetFileName(_customMascotPath);
                }
                else
                {
                    CustomSkinSubtitleText.Text = "Выбрать файл";
                }
            }
        }
    }

    private void SkinDynamicOrbButton_Click(object sender, RoutedEventArgs e)
    {
        SelectSkin("DynamicOrb");
    }

    private void SkinCyberVisorButton_Click(object sender, RoutedEventArgs e)
    {
        SelectSkin("CyberVisor");
    }

    private void SkinCustomImageButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите изображение персонажа (PNG)",
            Filter = "PNG Изображения (*.png)|*.png|Все изображения (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|Все файлы (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true && File.Exists(dialog.FileName))
        {
            SelectSkin("CustomImage", dialog.FileName);
        }
    }

    #endregion

    #region Steppers, Controls & Monitor Selection (ADR-018)

    public int GetMinInterval()
    {
        if (MinIntervalTextBox != null && int.TryParse(MinIntervalTextBox.Text, out int val))
        {
            return Math.Clamp(val, 1, 120);
        }
        return _settingsStore?.CurrentSettings.MinIntervalMinutes ?? 5;
    }

    public int GetMaxInterval()
    {
        int min = GetMinInterval();
        if (MaxIntervalTextBox != null && int.TryParse(MaxIntervalTextBox.Text, out int val))
        {
            return Math.Clamp(val, min, 480);
        }
        return Math.Max(min, _settingsStore?.CurrentSettings.MaxIntervalMinutes ?? 20);
    }

    private void MinIntervalMinus_Click(object sender, RoutedEventArgs e)
    {
        int current = GetMinInterval();
        int next = Math.Max(1, current - 1);
        MinIntervalTextBox.Text = next.ToString();
        TriggerAutoSave(immediate: false);
    }

    private void MinIntervalPlus_Click(object sender, RoutedEventArgs e)
    {
        int current = GetMinInterval();
        int max = GetMaxInterval();
        int next = Math.Min(120, current + 1);
        if (next > max)
        {
            MaxIntervalTextBox.Text = next.ToString();
        }
        MinIntervalTextBox.Text = next.ToString();
        TriggerAutoSave(immediate: false);
    }

    private void MaxIntervalMinus_Click(object sender, RoutedEventArgs e)
    {
        int current = GetMaxInterval();
        int min = GetMinInterval();
        int next = Math.Max(min, current - 1);
        MaxIntervalTextBox.Text = next.ToString();
        TriggerAutoSave(immediate: false);
    }

    private void MaxIntervalPlus_Click(object sender, RoutedEventArgs e)
    {
        int current = GetMaxInterval();
        int next = Math.Min(480, current + 1);
        MaxIntervalTextBox.Text = next.ToString();
        TriggerAutoSave(immediate: false);
    }

    private void IntervalTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        TriggerAutoSave(immediate: false);
    }

    private void IntervalTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        int min = GetMinInterval();
        int max = GetMaxInterval();

        if (MinIntervalTextBox != null)
        {
            MinIntervalTextBox.Text = min.ToString();
        }

        if (MaxIntervalTextBox != null)
        {
            MaxIntervalTextBox.Text = max.ToString();
        }

        TriggerAutoSave(immediate: true);
    }

    private void NumericTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is >= Key.D0 and <= Key.D9
            or >= Key.NumPad0 and <= Key.NumPad9
            or Key.Back or Key.Delete or Key.Tab
            or Key.Left or Key.Right or Key.Enter)
        {
            return;
        }

        e.Handled = true;
    }

    private void TargetMonitorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        TriggerAutoSave(immediate: true);
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        TriggerAutoSave(immediate: false);
    }

    private void RunOnStartupCheckBox_Click(object sender, RoutedEventArgs e)
    {
        TriggerAutoSave(immediate: true);
    }

    #endregion

    #region Status Header & Controls

    private void RefreshStatusHeader()
    {
        if (_isPaused)
        {
            StatusBadgeDot.Fill = (Brush)FindResource("WarningBrush");
            StatusBadgeText.Text = "На паузе";
            StatusBadgeText.Foreground = (Brush)FindResource("WarningBrush");
            StatusBadgeBorder.Style = (Style)FindResource("WarningBadgeStyle");

            QuickPauseText.Text = "Продолжить";
            QuickPauseIcon.Data = (Geometry)FindResource("IconPlay");
            NextTriggerTextBlock.Text = "Таймер приостановлен";
        }
        else
        {
            StatusBadgeDot.Fill = (Brush)FindResource("SuccessBrush");
            StatusBadgeText.Text = "Активен";
            StatusBadgeText.Foreground = (Brush)FindResource("SuccessBrush");
            StatusBadgeBorder.Style = (Style)FindResource("SuccessBadgeStyle");

            QuickPauseText.Text = "Пауза";
            QuickPauseIcon.Data = (Geometry)FindResource("IconPause");

            var next = _scheduler?.NextTriggerTime;
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
            bool isAvail;
            if (v.ContentType == ProvocationContentType.Text)
            {
                isAvail = !string.IsNullOrWhiteSpace(v.TextContent);
            }
            else
            {
                isAvail = !string.IsNullOrWhiteSpace(v.FilePath) && File.Exists(v.FilePath);
            }

            if (isAvail)
            {
                availableCount++;
            }
            if (v.IsEnabled)
            {
                activeCount++;
            }

            _videos.Add(new VideoItemViewModel(v, isAvail));
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

        VideoCountTextBlock.Text = $"Всего элементов: {_videos.Count} (активно: {activeCount}, доступно: {availableCount})";
    }

    #endregion

    #region Hotkeys

    private void HotkeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ValidateAllHotkeys();
        if (!_isLoading && TriggerHotkeyTextBox != null && PanicHotkeyTextBox != null)
        {
            var triggerStatus = GlobalHotkeyService.CheckHotkeyStatus(TriggerHotkeyTextBox.Text, _initialTriggerHotkey);
            var panicStatus = GlobalHotkeyService.CheckHotkeyStatus(PanicHotkeyTextBox.Text, _initialPanicHotkey);
            if (triggerStatus == HotkeyValidationState.Valid && panicStatus == HotkeyValidationState.Valid)
            {
                TriggerAutoSave(immediate: true);
            }
        }
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

    #endregion

    #region Content Actions (Videos & Text Reminders)

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
            TriggerAutoSave(immediate: true);
            ShowToast("Видео успешно добавлены в библиотеку", isSuccess: true);
        }
    }

    private void AddTextButton_Click(object sender, RoutedEventArgs e)
    {
        if (InlineAddTextBorder != null)
        {
            InlineAddTextBorder.Visibility = Visibility.Visible;
            InlineAddTextBox?.Focus();
        }
    }

    private void CancelAddTextButton_Click(object sender, RoutedEventArgs e)
    {
        if (InlineAddTextBorder != null)
        {
            InlineAddTextBorder.Visibility = Visibility.Collapsed;
            InlineAddTextBox?.Clear();
        }
    }

    private void ConfirmAddTextButton_Click(object sender, RoutedEventArgs e)
    {
        var text = InlineAddTextBox?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            ShowToast("Введите текст напоминания", isSuccess: false);
            return;
        }

        if (_videoLibrary != null)
        {
            _videoLibrary.AddTextItem(text);
        }
        else if (_settingsStore != null)
        {
            var title = text.Length > 40 ? text.Substring(0, 37) + "..." : text;
            _settingsStore.Update(s =>
            {
                s.Videos.Add(new VideoItem
                {
                    Id = Guid.NewGuid(),
                    Title = title,
                    ContentType = ProvocationContentType.Text,
                    TextContent = text,
                    IsEnabled = true,
                    AddedAtUtc = DateTime.UtcNow
                });
            });
        }

        InlineAddTextBox?.Clear();
        if (InlineAddTextBorder != null)
        {
            InlineAddTextBorder.Visibility = Visibility.Collapsed;
        }

        RefreshVideosList();
        TriggerAutoSave(immediate: true);
        ShowToast("Текстовое напоминание добавлено", isSuccess: true);
    }

    private void RemoveVideoButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is VideoItemViewModel vm)
        {
            _videoLibrary?.RemoveVideo(vm.Id);
            RefreshVideosList();
            TriggerAutoSave(immediate: true);
        }
    }

    private void VideoCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is VideoItemViewModel vm)
        {
            _videoLibrary?.ToggleVideo(vm.Id, vm.IsEnabled);
            RefreshVideosList();
            TriggerAutoSave(immediate: true);
        }
    }

    private void TestSingleVideo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is VideoItemViewModel vm)
        {
            if (vm.IsText)
            {
                var spotlight = new TextSpotlightWindow(vm.Item.TextContent);
                spotlight.Owner = this;
                spotlight.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                spotlight.Show();
                return;
            }

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

    #endregion

    #region Quick Actions & Window Controls

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

    #endregion

    #region Toast Notifications

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
        var slideIn = new DoubleAnimation(16.0, 0.0, TimeSpan.FromMilliseconds(220))
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
            var slideOut = new DoubleAnimation(0.0, 16.0, TimeSpan.FromMilliseconds(220))
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

    #endregion

    protected override void OnClosed(EventArgs e)
    {
        _statusTimer?.Stop();
        _toastTimer?.Stop();
        _autoSaveDebounceTimer?.Stop();
        base.OnClosed(e);
    }
}

/// <summary>
/// ViewModel wrapping <see cref="VideoItem"/> for rich databinding with type badges and disk availability indicators.
/// </summary>
public class VideoItemViewModel
{
    private static readonly Brush AvailableBg = CreateFrozenBrush(0x1A, 0x10, 0xB9, 0x81);
    private static readonly Brush AvailableFg = CreateFrozenBrush(0xFF, 0x10, 0xB9, 0x81);
    private static readonly Brush MissingBg = CreateFrozenBrush(0x1A, 0xEF, 0x44, 0x44);
    private static readonly Brush MissingFg = CreateFrozenBrush(0xFF, 0xEF, 0x44, 0x44);

    private static readonly Brush VideoBadgeBg = CreateFrozenBrush(0x25, 0x8B, 0x5C, 0xF6);
    private static readonly Brush VideoBadgeFg = CreateFrozenBrush(0xFF, 0xA7, 0x8B, 0xFA);
    private static readonly Brush TextBadgeBg = CreateFrozenBrush(0x25, 0x06, 0xB6, 0xD4);
    private static readonly Brush TextBadgeFg = CreateFrozenBrush(0xFF, 0x38, 0xBD, 0xF8);

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
    public bool IsText => Item.ContentType == ProvocationContentType.Text;

    public bool IsEnabled
    {
        get => Item.IsEnabled;
        set => Item.IsEnabled = value;
    }

    public string ContentTypeBadgeText => IsText ? "ТЕКСТ" : "ВИДЕО";
    public Brush ContentTypeBadgeBackground => IsText ? TextBadgeBg : VideoBadgeBg;
    public Brush ContentTypeBadgeForeground => IsText ? TextBadgeFg : VideoBadgeFg;

    public string SecondaryText => IsText
        ? (string.IsNullOrWhiteSpace(Item.TextContent) ? Item.Title : Item.TextContent)
        : Item.FilePath;

    public string AvailabilityText => IsText ? "Готов к показу" : (IsAvailable ? "Доступен" : "Не найден");
    public Brush AvailabilityBackground => IsText ? AvailableBg : (IsAvailable ? AvailableBg : MissingBg);
    public Brush AvailabilityForeground => IsText ? AvailableFg : (IsAvailable ? AvailableFg : MissingFg);

    public VideoItemViewModel(VideoItem item, bool isAvailable)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
        IsAvailable = isAvailable;
    }
}
