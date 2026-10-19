using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
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
            Interval = TimeSpan.FromMilliseconds(200)
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
    public void PlayVideo(string filePath, string title)
    {
        VideoTitleTextBlock.Text = title;

        double volume = _settingsStore?.CurrentSettings?.VideoVolume ?? 0.85;
        Player.Volume = Math.Clamp(volume, 0.0, 1.0);

        PlaybackProgressBar.Value = 0;
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
    /// Checks playback progress, updates the progress bar, and enforces 100% reliable
    /// auto-completion when the video reaches its natural duration (ADR-008).
    /// </summary>
    public void CheckPlaybackProgress()
    {
        if (Player.NaturalDuration.HasTimeSpan && Player.NaturalDuration.TimeSpan.TotalSeconds > 0)
        {
            double totalSeconds = Player.NaturalDuration.TimeSpan.TotalSeconds;
            double currentSeconds = Player.Position.TotalSeconds;
            double progress = Math.Clamp((currentSeconds / totalSeconds) * 100.0, 0.0, 100.0);
            PlaybackProgressBar.Value = progress;

            if (Player.Position >= Player.NaturalDuration.TimeSpan - TimeSpan.FromMilliseconds(150))
            {
                _playbackWatchdogTimer.Stop();
                _coordinator?.CompleteSession();
            }
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
        _playbackWatchdogTimer.Stop();
        _coordinator?.CompleteSession();
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
        _playbackWatchdogTimer.Stop();
        _coordinator?.CompleteSession();
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _playbackWatchdogTimer.Stop();
            _coordinator?.CompleteSession();
            e.Handled = true;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
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
