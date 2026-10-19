using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Color = System.Windows.Media.Color;
using PersonalProvocateur.App.Services;
using PersonalProvocateur.App.Storage;

namespace PersonalProvocateur.App.UI;

/// <summary>
/// Window dedicated to hardware-accelerated video playback with dark theme framing.
/// </summary>
public partial class VideoWindow : Window
{
    private readonly OverlayCoordinator? _coordinator;
    private readonly SettingsStore? _settingsStore;
    private readonly DispatcherTimer _playbackWatchdogTimer;
    private bool _hasCompleted;

    /// <summary>
    /// Gets or sets whether the window is explicitly permitted to close.
    /// If false and <see cref="IsManualTest"/> is false, closing signals are blocked (Mandatory Watch Lock).
    /// </summary>
    public bool AllowClose { get; set; } = false;

    /// <summary>
    /// Gets or sets whether this video is being played in manual test mode (from settings/tray).
    /// </summary>
    public bool IsManualTest { get; set; } = false;

    /// <summary>
    /// Gets whether playback completion has already been triggered.
    /// </summary>
    public bool HasCompleted => _hasCompleted;

    /// <summary>
    /// Gets the playback watchdog timer instance.
    /// </summary>
    public DispatcherTimer PlaybackWatchdogTimer => _playbackWatchdogTimer;

    /// <summary>
    /// Initializes a new instance of <see cref="VideoWindow"/>.
    /// </summary>
    /// <param name="coordinator">The coordinator orchestrating sessions.</param>
    /// <param name="settingsStore">Application settings store.</param>
    public VideoWindow(OverlayCoordinator coordinator, SettingsStore settingsStore)
    {
        InitializeComponent();

        _coordinator = coordinator;
        _settingsStore = settingsStore;

        _playbackWatchdogTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _playbackWatchdogTimer.Tick += OnPlaybackWatchdogTick;

        Loaded += OnLoaded;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    /// <summary>
    /// Parameterless constructor for designer and testing support.
    /// </summary>
    public VideoWindow() : this(null!, null!)
    {
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateLayoutBounds();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.InvokeAsync(UpdateLayoutBounds);
        }
        else
        {
            UpdateLayoutBounds();
        }
    }

    private void UpdateLayoutBounds()
    {
        // 75% to 80% screen coverage
        double targetWidth = Math.Max(720, SystemParameters.PrimaryScreenWidth * 0.78);
        double targetHeight = Math.Max(480, SystemParameters.PrimaryScreenHeight * 0.78);

        Width = targetWidth;
        Height = targetHeight;
        Left = (SystemParameters.PrimaryScreenWidth - targetWidth) / 2;
        Top = (SystemParameters.PrimaryScreenHeight - targetHeight) / 2;
    }

    /// <summary>
    /// Loads and begins playback of the specified video file.
    /// </summary>
    /// <param name="filePath">Path to the video file.</param>
    /// <param name="title">Display title of the video.</param>
    /// <param name="isManualTest">True if invoked as a manual test from settings/tray; enables close button.</param>
    public void PlayVideo(string filePath, string title, bool isManualTest = false)
    {
        IsManualTest = isManualTest;
        CloseButton.Visibility = isManualTest ? Visibility.Visible : Visibility.Collapsed;

        VideoTitleTextBlock.Text = title;

        double volume = _settingsStore?.CurrentSettings?.VideoVolume ?? 0.85;
        Player.Volume = Math.Clamp(volume, 0.0, 1.0);

        PlaybackProgressBar.Value = 0;
        RemainingTimeTextBlock.Text = "--:--";
        _hasCompleted = false;
        _playbackWatchdogTimer.Start();

        try
        {
            var absolutePath = Path.GetFullPath(filePath);
            Player.Source = new Uri(absolutePath, UriKind.Absolute);
            Player.Play();
        }
        catch
        {
            _playbackWatchdogTimer.Stop();
            // If URI creation or initial play fails, complete session gracefully
            _coordinator?.OnVideoPlaybackFailed(filePath);
        }
    }

    /// <summary>
    /// Flashes the mandatory watch notice overlay to alert the user
    /// that closing the motivation video early is forbidden.
    /// Redirects to <see cref="PulseMandatoryWatchWarning"/>.
    /// </summary>
    public void FlashMandatoryWatchNotice()
    {
        PulseMandatoryWatchWarning();
    }

    /// <summary>
    /// Pulses the window border with accent warning colors (#EF4444 / #8B5CF6) and displays
    /// a stylish mandatory watch capsule notice at the top of the video player for 1.8 seconds.
    /// Enforces Strict Mandatory Watch Lock (ADR-010).
    /// </summary>
    public void PulseMandatoryWatchWarning()
    {
        if (MandatoryWatchNotice != null)
        {
            MandatoryWatchNotice.BeginAnimation(UIElement.OpacityProperty, null);

            var noticeAnimation = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(1800)
            };
            noticeAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            noticeAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250))));
            noticeAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1350))));
            noticeAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1800))));

            MandatoryWatchNotice.BeginAnimation(UIElement.OpacityProperty, noticeAnimation);
        }

        if (WindowBorder != null)
        {
            var brush = WindowBorder.BorderBrush as SolidColorBrush;
            if (brush == null || brush.IsFrozen)
            {
                brush = new SolidColorBrush(Color.FromRgb(0x34, 0x38, 0x4E));
                WindowBorder.BorderBrush = brush;
            }

            brush.BeginAnimation(SolidColorBrush.ColorProperty, null);

            var borderPulse = new ColorAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(1800)
            };
            borderPulse.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(0x34, 0x38, 0x4E), KeyTime.FromTimeSpan(TimeSpan.Zero)));
            borderPulse.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(0xEF, 0x44, 0x44), KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250))));
            borderPulse.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(0x8B, 0x5C, 0xF6), KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(800))));
            borderPulse.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(0xEF, 0x44, 0x44), KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1350))));
            borderPulse.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(0x34, 0x38, 0x4E), KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1800))));

            brush.BeginAnimation(SolidColorBrush.ColorProperty, borderPulse);
        }

        if (WindowBorder?.Effect is DropShadowEffect shadow && !shadow.IsFrozen)
        {
            shadow.BeginAnimation(DropShadowEffect.ColorProperty, null);

            var shadowPulse = new ColorAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(1800)
            };
            shadowPulse.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(0x00, 0x00, 0x00), KeyTime.FromTimeSpan(TimeSpan.Zero)));
            shadowPulse.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(0xEF, 0x44, 0x44), KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250))));
            shadowPulse.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(0x8B, 0x5C, 0xF6), KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(800))));
            shadowPulse.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(0xEF, 0x44, 0x44), KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1350))));
            shadowPulse.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(0x00, 0x00, 0x00), KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1800))));

            shadow.BeginAnimation(DropShadowEffect.ColorProperty, shadowPulse);
        }
    }

    /// <summary>
    /// Checks playback progress, updates the progress bar and remaining time, and enforces
    /// 100% reliable auto-completion when the video reaches its natural duration (ADR-008).
    /// </summary>
    /// <param name="overridePosition">Optional simulated playback position for unit testing.</param>
    /// <param name="overrideDuration">Optional simulated natural duration for unit testing.</param>
    public void CheckPlaybackProgress(TimeSpan? overridePosition = null, TimeSpan? overrideDuration = null)
    {
        bool hasNaturalDuration = overrideDuration.HasValue || (Player.NaturalDuration.HasTimeSpan && Player.NaturalDuration.TimeSpan.TotalSeconds > 0);
        TimeSpan natural = overrideDuration ?? (Player.NaturalDuration.HasTimeSpan ? Player.NaturalDuration.TimeSpan : TimeSpan.Zero);
        TimeSpan position = overridePosition ?? Player.Position;

        if (hasNaturalDuration && natural.TotalSeconds > 0)
        {
            var remaining = natural > position ? natural - position : TimeSpan.Zero;
            if (RemainingTimeTextBlock != null)
            {
                RemainingTimeTextBlock.Text = remaining.ToString(@"m\:ss");
            }

            double progress = Math.Clamp((position.TotalSeconds / natural.TotalSeconds) * 100.0, 0.0, 100.0);
            if (PlaybackProgressBar != null)
            {
                PlaybackProgressBar.Value = progress;
            }

            // Reliable completion check at natural duration - 120ms
            if (position >= natural - TimeSpan.FromMilliseconds(120))
            {
                CompleteVideoPlayback();
            }
        }
    }

    /// <summary>
    /// Completes video playback with fade-out animation and informs the coordinator.
    /// Uses <see cref="_hasCompleted"/> guard to prevent duplicate invocations.
    /// </summary>
    /// <param name="immediate">True to bypass animation for testability or manual closing.</param>
    public void CompleteVideoPlayback(bool immediate = false)
    {
        if (_hasCompleted)
        {
            return;
        }

        _hasCompleted = true;
        _playbackWatchdogTimer.Stop();

        if (immediate || !IsLoaded)
        {
            _coordinator?.CompleteSession();
            return;
        }

        try
        {
            var fadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            fadeOut.Completed += (s, e) =>
            {
                _coordinator?.CompleteSession();
            };
            BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }
        catch
        {
            _coordinator?.CompleteSession();
        }
    }

    private void OnPlaybackWatchdogTick(object? sender, EventArgs e)
    {
        CheckPlaybackProgress();
    }

    private void Player_MediaOpened(object sender, RoutedEventArgs e)
    {
        _playbackWatchdogTimer.Start();
        Player.Play();
    }

    private void Player_MediaEnded(object sender, RoutedEventArgs e)
    {
        CompleteVideoPlayback();
    }

    private void Player_MediaFailed(object? sender, ExceptionRoutedEventArgs e)
    {
        _playbackWatchdogTimer.Stop();
        // Automatically switch to next video or cleanly complete without crashing
        string failedPath = Player.Source?.LocalPath ?? string.Empty;
        if (_coordinator != null)
        {
            _coordinator.OnVideoPlaybackFailed(failedPath);
        }
        else
        {
            Close();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (!AllowClose && !IsManualTest)
        {
            PulseMandatoryWatchWarning();
            return;
        }

        CompleteVideoPlayback(immediate: true);
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (!AllowClose && !IsManualTest)
            {
                e.Handled = true;
                PulseMandatoryWatchWarning();
                return;
            }

            CompleteVideoPlayback(immediate: true);
            e.Handled = true;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose && !IsManualTest)
        {
            e.Cancel = true;
            PulseMandatoryWatchWarning();
            return;
        }

        _playbackWatchdogTimer.Stop();
        try
        {
            Player.Stop();
            Player.Close();
        }
        catch
        {
            // Suppress cleanup exceptions
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _playbackWatchdogTimer.Stop();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        base.OnClosed(e);
    }
}
