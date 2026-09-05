using System.Diagnostics;
using System.IO;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using Windows.System;
using UniversalMediaPlayer.App.Native;
using UniversalMediaPlayer.Core.Enums;
using UniversalMediaPlayer.Core.Models;
using UniversalMediaPlayer.Core.Persistence;
using UniversalMediaPlayer.Core.Services;
using UniversalMediaPlayer.Discovery;
using UniversalMediaPlayer.Persistence;
using UniversalMediaPlayer.Playback;
using UniversalMediaPlayer.UI.Helpers;
using UniversalMediaPlayer.UI.Resources;
using UniversalMediaPlayer.UI.Services;
using UniversalMediaPlayer.UI.ViewModels;

namespace UniversalMediaPlayer.App;

public sealed partial class MainWindow : Window
{
    private static readonly string LogFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UniversalMediaPlayer", "startup.log");
    public PlayerViewModel ViewModel { get; } = new();

    private readonly IPlaybackEngine _engine;
    private readonly IShowPreferencesStore _preferencesStore;
    private readonly IWatchHistoryStore _historyStore;
    private readonly EpisodicContinuityService _continuityService;
    private readonly PlaybackHistoryTracker _historyTracker;

    private readonly DispatcherTimer _autoHideTimer;
    private readonly DispatcherTimer _osdTimer;
    private readonly DispatcherTimer _resumePromptTimer;
    private DispatcherTimer? _autoNextTimer;

    private nint _hwnd;
    private nint _videoHwnd;
    private AppWindow? _appWindow;

    private bool _isDraggingSlider;
    private bool _isMuted;
    private int _currentVolume = 100;
    private double _durationSeconds;
    private double _currentPositionSeconds;
    private MediaPackage? _currentPackage;
    private PlaybackPreparationPlan? _currentPlan;
    private CancellationTokenSource? _openCts;

    private double _pendingResumePosition;
    private bool _isResumePromptVisible;
    private double _resumePromptPlaybackSeconds;
    private bool _autoNextPrompted;
    private int _autoNextCountdownSeconds = 5;
    private MediaItem? _nextEpisodeItem;

    public MainWindow()
    {
        _engine = new MpvPlaybackEngine();

        _preferencesStore = new JsonShowPreferencesStore();
        _historyStore = new SqliteWatchHistoryStore();
        _continuityService = new EpisodicContinuityService(_preferencesStore, _historyStore);
        _historyTracker = new PlaybackHistoryTracker(_historyStore);

        InitializeComponent();

        _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _autoHideTimer.Tick += AutoHideTimer_Tick;

        _osdTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _osdTimer.Tick += (s, e) =>
        {
            _osdTimer.Stop();
            OsdBorder.Visibility = Visibility.Collapsed;
        };

        _resumePromptTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _resumePromptTimer.Tick += (s, e) =>
        {
            DismissResumePrompt();
        };

        this.Closed += MainWindow_Closed;

        File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] MainWindow constructor calling InitializeWindowInterop\n");
        InitializeWindowInterop();
        File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] MainWindow constructor finished\n");
    }

    private async void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] MainWindow_Closed triggered!\n");
        _autoHideTimer.Stop();
        _osdTimer.Stop();
        _resumePromptTimer.Stop();
        _autoNextTimer?.Stop();

        _historyTracker.OnMediaChanging();

        _openCts?.Cancel();
        _openCts?.Dispose();
        _openCts = null;

        try
        {
            await _engine.DisposeAsync();
        }
        catch { }

        _historyTracker.Dispose();
        if (_historyStore is IDisposable hDisp) hDisp.Dispose();
        if (_preferencesStore is IDisposable pDisp) pDisp.Dispose();

        if (_videoHwnd != 0)
        {
            Win32.DestroyWindow(_videoHwnd);
            _videoHwnd = 0;
        }
    }

    private async void InitializeWindowInterop()
    {
        try
        {
            _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] WindowNative.GetWindowHandle: {_hwnd:X}\n");

            // Set WS_CLIPCHILDREN on MainWindow HWND so it doesn't overdraw child HWNDs
            var style = Win32.GetWindowLongPtr(_hwnd, Win32.GWL_STYLE);
            Win32.SetWindowLongPtr(_hwnd, Win32.GWL_STYLE, style | Win32.WS_CLIPCHILDREN);

            var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
            File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] Win32Interop.GetWindowIdFromWindow: {windowId.Value:X}\n");
            _appWindow = AppWindow.GetFromWindowId(windowId);
            File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] AppWindow.GetFromWindowId: {(_appWindow != null ? "Success" : "NULL")}\n");

            if (_appWindow != null)
            {
                _appWindow.Title = AppStrings.AppTitle;
                _appWindow.Resize(new Windows.Graphics.SizeInt32(1100, 680));
                _appWindow.Changed += AppWindow_Changed;
            }

            CreateVideoChildWindow();
            File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] CreateVideoChildWindow created child HWND: {_videoHwnd:X}\n");

            // Hook engine events
            _engine.TimeUpdated += OnTimeUpdated;
            _engine.PlaybackStateChanged += OnPlaybackStateChanged;
            _engine.TracksChanged += OnTracksChanged;

            try
            {
                File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] Initializing engine with _videoHwnd: {_videoHwnd:X}\n");
                await _engine.InitializeAsync(_videoHwnd);
                File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] Engine InitializeAsync completed successfully\n");
            }
            catch (Exception ex)
            {
                File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] EXCEPTION initializing playback engine: {ex}\n");
                ShowError(AppStrings.UnableToPlay, ex.Message);
            }

            _ = CheckContinueWatchingAsync();
        }
        catch (Exception ex)
        {
            File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] EXCEPTION in InitializeWindowInterop: {ex}\n");
        }
    }

    private async Task CheckContinueWatchingAsync()
    {
        try
        {
            var items = await _historyStore.GetContinueWatchingAsync(1);
            if (items.Count > 0)
            {
                var item = items[0];
                if (File.Exists(item.FilePath))
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (_currentPackage == null)
                        {
                            var title = !string.IsNullOrWhiteSpace(item.ShowId) ? item.ShowId : FormatHelper.CleanTitle(item.FilePath);
                            if (item.SeasonNumber.HasValue || item.EpisodeNumber.HasValue)
                            {
                                var s = item.SeasonNumber.HasValue ? $"S{item.SeasonNumber.Value:D2}" : "";
                                var ep = item.EpisodeNumber.HasValue ? $"E{item.EpisodeNumber.Value:D2}" : "";
                                title = $"{item.ShowId} {s}{ep}".Trim();
                            }

                            var posStr = FormatHelper.FormatTimecode(item.PositionSeconds);
                            var durStr = item.DurationSeconds > 0 ? FormatHelper.FormatTimecode(item.DurationSeconds) : "--:--";
                            var details = string.Format(AppStrings.PausedAt, posStr, durStr);

                            ViewModel.SetContinueWatching(title, details, item.FilePath, item.PositionSeconds);

                            ContinueWatchingTitleTextBlock.Text = title;
                            ContinueWatchingDetailsTextBlock.Text = details;
                            ContinueWatchingBorder.Visibility = Visibility.Visible;
                        }
                    });
                }
            }
        }
        catch
        {
            // Ignore history load errors on startup
        }
    }

    private async void ContinueWatchingButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(ViewModel.ContinueWatchingFilePath))
        {
            var filePath = ViewModel.ContinueWatchingFilePath;
            var pos = ViewModel.ContinueWatchingPosition;
            await OpenMediaFileAsync(filePath);
            if (_engine.IsInitialized && pos > 0)
            {
                await _engine.SeekAsync(pos, relative: false);
                _historyTracker.OnSeek(pos);
                DismissResumePrompt();
            }
        }
    }

    private void CreateVideoChildWindow()
    {
        if (_hwnd == 0) return;

        var scale = (float)Win32.GetDpiForWindow(_hwnd) / 96f;
        var width = Math.Max(100, (int)(VideoHostBorder.ActualWidth * scale));
        var height = Math.Max(100, (int)(VideoHostBorder.ActualHeight * scale));

        _videoHwnd = Win32.CreateWindowExW(
            0,
            "static",
            "VideoSurface",
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_CLIPSIBLINGS,
            0, 0, width, height,
            _hwnd,
            nint.Zero,
            Win32.GetModuleHandleW(null),
            nint.Zero);

        // Initially hide video window until media is opened
        Win32.ShowWindow(_videoHwnd, Win32.SW_HIDE);
    }

    private void SyncVideoHostSize()
    {
        if (_videoHwnd == 0 || _hwnd == 0) return;
        if (Win32.IsIconic(_hwnd)) return;

        try
        {
            var transform = VideoHostBorder.TransformToVisual(RootGrid);
            var pt = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
            var scale = (float)Win32.GetDpiForWindow(_hwnd) / 96f;
            int x = (int)Math.Round(pt.X * scale);
            int y = (int)Math.Round(pt.Y * scale);
            int width = (int)Math.Round(VideoHostBorder.ActualWidth * scale);
            int height = (int)Math.Round(VideoHostBorder.ActualHeight * scale);

            if (width > 0 && height > 0)
            {
                Win32.SetWindowPos(_videoHwnd, Win32.HWND_TOP, x, y, width, height, Win32.SWP_SHOWWINDOW | Win32.SWP_NOACTIVATE);
                Win32.InvalidateRect(_videoHwnd, 0, true);
                Win32.UpdateWindow(_videoHwnd);
            }
        }
        catch
        {
            // RootGrid layout might not be ready during early initialization
        }
    }

    private void VideoHostBorder_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        SyncVideoHostSize();
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidSizeChange)
        {
            SyncVideoHostSize();
        }
    }

    public async Task OpenMediaFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            ShowError(AppStrings.FileNotFound, $"Файл '{filePath}' не найден или недоступен.");
            return;
        }

        _openCts?.Cancel();
        _openCts?.Dispose();
        _openCts = new CancellationTokenSource();
        var ct = _openCts.Token;

        try
        {
            // Pass cancellation token to history tracker
            await _historyTracker.OnMediaChangingAsync(ct);

            // Dismiss any existing resume prompt or auto-next prompt
            DismissResumePrompt();
            CancelAutoNextPrompt();
            _autoNextPrompted = false;

            ErrorNotificationBar.IsOpen = false;
            EmptyStatePanel.Visibility = Visibility.Collapsed;
            ContinueWatchingBorder.Visibility = Visibility.Collapsed;

            // Make sure video HWND is visible and sized correctly
            Win32.ShowWindow(_videoHwnd, Win32.SW_SHOW);
            SyncVideoHostSize();

            // Background non-blocking scan with cancellation support
            MediaPackage package = await Task.Run(() => DirectoryScanner.Scan(filePath, ct), ct);
            ct.ThrowIfCancellationRequested();
            _currentPackage = package;

            // Display series / episodic identity if recognized
            if (package.Episode != null)
            {
                var s = package.Episode.SeasonNumber.HasValue ? $"S{package.Episode.SeasonNumber:D2}" : "";
                var epStr = $"{package.Episode.ShowTitle} {s}E{package.Episode.EpisodeNumber:D2}".Trim();
                TitleTextBlock.Text = epStr;
                var audioCount = $"{package.AudioTracks.Count} {AppStrings.Audio.ToLowerInvariant()}";
                var subCount = $"{package.SubtitleTracks.Count} {AppStrings.Subtitles.ToLowerInvariant()}";
                var fontsCount = package.Fonts?.HasFonts == true ? $" · {package.Fonts.Count} шрифтов" : "";
                TechnicalMetadataTextBlock.Text = $"· 1 видео · {audioCount} · {subCount}{fontsCount}";
                TopBarBorder.Visibility = Visibility.Visible;
            }
            else
            {
                TitleTextBlock.Text = FormatHelper.CleanTitle(filePath);
                var audioCount = $"{package.AudioTracks.Count} {AppStrings.Audio.ToLowerInvariant()}";
                var subCount = $"{package.SubtitleTracks.Count} {AppStrings.Subtitles.ToLowerInvariant()}";
                TechnicalMetadataTextBlock.Text = $"· 1 видео · {audioCount} · {subCount}";
                TopBarBorder.Visibility = Visibility.Visible;
            }

            if (_appWindow != null)
            {
                _appWindow.Title = $"{TitleTextBlock.Text} — {AppStrings.AppTitle}";
            }

            // Prepare playback via EpisodicContinuityService
            var plan = await _continuityService.PreparePlaybackAsync(package, ct);
            ct.ThrowIfCancellationRequested();
            _currentPlan = plan;

            // Load into engine
            await _engine.OpenAsync(package, ct);
            ct.ThrowIfCancellationRequested();

            // After engine loads file and registers external tracks:
            // 1. Select resolved audio track if different from default
            AudioTrack? targetAudio = null;
            if (plan.AudioResolution?.SelectedTrack != null)
            {
                var resolved = plan.AudioResolution.SelectedTrack;
                var activeAudios = _engine.ActiveTracks.OfType<AudioTrack>().ToList();
                if (resolved.IsExternal && !string.IsNullOrEmpty(resolved.ExternalFilePath))
                {
                    targetAudio = activeAudios.FirstOrDefault(a => a.IsExternal && string.Equals(a.ExternalFilePath, resolved.ExternalFilePath, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    targetAudio = activeAudios.FirstOrDefault(a => a.Id == resolved.Id && !a.IsExternal)
                               ?? activeAudios.FirstOrDefault(a => !a.IsExternal && string.Equals(a.Language, resolved.Language, StringComparison.OrdinalIgnoreCase));
                }

                if (targetAudio != null && !targetAudio.IsSelected)
                {
                    await _engine.SelectAudioTrackAsync(targetAudio.Id);
                }
            }

            // 2. Select resolved subtitle track and set visibility based on plan.SubtitleVisible
            SubtitleTrack? targetSub = null;
            if (plan.SubtitleResolution?.SelectedTrack != null)
            {
                var resolved = plan.SubtitleResolution.SelectedTrack;
                var activeSubs = _engine.ActiveTracks.OfType<SubtitleTrack>().ToList();
                if (resolved.IsExternal && !string.IsNullOrEmpty(resolved.ExternalFilePath))
                {
                    targetSub = activeSubs.FirstOrDefault(s => s.IsExternal && string.Equals(s.ExternalFilePath, resolved.ExternalFilePath, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    targetSub = activeSubs.FirstOrDefault(s => s.Id == resolved.Id && !s.IsExternal)
                             ?? activeSubs.FirstOrDefault(s => !s.IsExternal && string.Equals(s.Language, resolved.Language, StringComparison.OrdinalIgnoreCase));
                }

                if (targetSub != null)
                {
                    await _engine.SelectSubtitleTrackAsync(targetSub.Id);
                }
            }
            await _engine.SetSubtitleVisibilityAsync(plan.SubtitleVisible);

            // 3. Show compact OSD summary: e.g. "Аудио: Русский (Предпочтительно) · Субтитры: Русский (Предпочтительно)"
            var audioPref = plan.AudioResolution?.Reason == TrackSelectionReason.ExactTrackMatch || plan.AudioResolution?.Reason == TrackSelectionReason.PreferredLanguage ? $" ({AppStrings.Preferred})" : "";
            var audioLang = targetAudio != null ? AppStrings.GetLanguageNameRu(targetAudio.Language) : "—";
            var audioStr = $"{AppStrings.Audio}: {audioLang}{audioPref}";

            var subPref = plan.SubtitleResolution?.Reason == TrackSelectionReason.ExactTrackMatch || plan.SubtitleResolution?.Reason == TrackSelectionReason.PreferredLanguage ? $" ({AppStrings.Preferred})" : "";
            var subStr = !plan.SubtitleVisible || targetSub == null
                ? AppStrings.SubtitlesOff
                : $"{AppStrings.Subtitles}: {AppStrings.GetLanguageNameRu(targetSub.Language)}{subPref}";

            ShowOsd($"{audioStr} · {subStr}");

            // If plan.CanResume: Show Resume Prompt ("Продолжить просмотр с XX:XX?")
            if (plan.CanResume)
            {
                var timeStr = FormatHelper.FormatTimecode(plan.ResumePositionSeconds);
                ShowResumePrompt(string.Format(AppStrings.ResumePromptQuestion, timeStr), plan.ResumePositionSeconds);
            }

            // Start playback
            await _engine.PlayAsync();
            SyncVideoHostSize();
            _autoHideTimer.Start();

            // Start tracking history with _historyTracker.OnMediaOpened
            var showId = plan.ShowId;
            var season = package.Episode?.SeasonNumber;
            var episode = package.Episode?.EpisodeNumber;
            _historyTracker.OnMediaOpened(package.PrimaryVideo.FilePath, showId, season, episode, _durationSeconds);
        }
        catch (OperationCanceledException)
        {
            // Rapid file switching or open cancelled; ignore cleanly
        }
        catch (Exception ex)
        {
            ShowError(AppStrings.UnableToPlay, ex.Message);
        }
    }

    private void ShowResumePrompt(string message, double resumePosition)
    {
        _pendingResumePosition = resumePosition;
        _isResumePromptVisible = true;
        _resumePromptPlaybackSeconds = 0;
        ResumePromptTextBlock.Text = message;
        ResumePromptBorder.Visibility = Visibility.Visible;
        ViewModel.ShowResumePrompt(message, resumePosition);

        _resumePromptTimer.Stop();
        _resumePromptTimer.Start();
    }

    private void DismissResumePrompt()
    {
        _resumePromptTimer.Stop();
        _isResumePromptVisible = false;
        ResumePromptBorder.Visibility = Visibility.Collapsed;
        ViewModel.HideResumePrompt();
    }

    private async void ResumePromptResumeButton_Click(object sender, RoutedEventArgs e)
    {
        var pos = _pendingResumePosition;
        DismissResumePrompt();
        if (_engine.IsInitialized && pos > 0)
        {
            await _engine.SeekAsync(pos, relative: false);
            _historyTracker.OnSeek(pos);
            ShowOsd($"{AppStrings.ResumeButton}: {FormatHelper.FormatTimecode(pos)}");
        }
    }

    private async void ResumePromptStartBeginningButton_Click(object sender, RoutedEventArgs e)
    {
        DismissResumePrompt();
        if (_engine.IsInitialized)
        {
            await _engine.SeekAsync(0, relative: false);
            _historyTracker.OnSeek(0);
            ShowOsd(AppStrings.StartFromBeginningButton);
        }
    }

    private void TriggerAutoNext(MediaItem nextEpisode)
    {
        _autoNextPrompted = true;
        _nextEpisodeItem = nextEpisode;
        _autoNextCountdownSeconds = 5;

        var parentDir = Path.GetDirectoryName(nextEpisode.FilePath);
        var parentFolder = !string.IsNullOrEmpty(parentDir) ? Path.GetFileName(parentDir) : null;
        var epInfo = EpisodeParser.Parse(nextEpisode.FileName, parentFolder);
        string nextEpTitle;
        if (epInfo != null)
        {
            var s = epInfo.SeasonNumber.HasValue ? $"S{epInfo.SeasonNumber:D2}" : "";
            nextEpTitle = $"{epInfo.ShowTitle} {s}E{epInfo.EpisodeNumber:D2}".Trim();
        }
        else
        {
            nextEpTitle = FormatHelper.CleanTitle(nextEpisode.FileName);
        }

        var message = string.Format(AppStrings.NextEpisodePrompt, nextEpTitle);
        AutoNextTitleTextBlock.Text = message;
        AutoNextCountdownTextBlock.Text = string.Format(AppStrings.PlayingInSeconds, _autoNextCountdownSeconds);
        AutoNextPromptBorder.Visibility = Visibility.Visible;
        ViewModel.ShowAutoNextPrompt(message, _autoNextCountdownSeconds);

        _autoNextTimer?.Stop();
        _autoNextTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _autoNextTimer.Tick += async (s, e) =>
        {
            _autoNextCountdownSeconds--;
            if (_autoNextCountdownSeconds > 0)
            {
                AutoNextCountdownTextBlock.Text = string.Format(AppStrings.PlayingInSeconds, _autoNextCountdownSeconds);
                ViewModel.AutoNextCountdownSeconds = _autoNextCountdownSeconds;
            }
            else
            {
                _autoNextTimer.Stop();
                CancelAutoNextPrompt();
                if (_nextEpisodeItem != null)
                {
                    await OpenMediaFileAsync(_nextEpisodeItem.FilePath);
                }
            }
        };
        _autoNextTimer.Start();
    }

    private void CancelAutoNextPrompt()
    {
        _autoNextTimer?.Stop();
        AutoNextPromptBorder.Visibility = Visibility.Collapsed;
        ViewModel.HideAutoNextPrompt();
    }

    private void AutoNextCancelButton_Click(object sender, RoutedEventArgs e)
    {
        CancelAutoNextPrompt();
    }

    private async void AutoNextPlayNowButton_Click(object sender, RoutedEventArgs e)
    {
        CancelAutoNextPrompt();
        if (_nextEpisodeItem != null)
        {
            await OpenMediaFileAsync(_nextEpisodeItem.FilePath);
        }
    }

    private void OnTimeUpdated(double position, double duration)
    {
        _historyTracker.OnPositionUpdate(position, duration);

        DispatcherQueue.TryEnqueue(() =>
        {
            _currentPositionSeconds = position;
            _durationSeconds = duration;

            if (!_isDraggingSlider && duration > 0)
            {
                TimelineSlider.Maximum = duration;
                TimelineSlider.Value = position;
            }

            TimecodeTextBlock.Text = $"{FormatHelper.FormatTimecode(position)} / {FormatHelper.FormatTimecode(duration)}";

            if (_isResumePromptVisible)
            {
                _resumePromptPlaybackSeconds += 0.25;
                if (_resumePromptPlaybackSeconds >= 10.0)
                {
                    DismissResumePrompt();
                }
            }

            // Check for Auto-Next: If duration > 0 && position / duration >= 0.95, and AutoNext is enabled, and not already prompted, and EpisodeNavigator.FindNextEpisode(_currentPackage) is not null:
            // Trigger Auto-Next 5-second countdown!
            if (duration > 0 && (position / duration) >= 0.95 &&
                ViewModel.AutoNextEnabled && !_autoNextPrompted && _currentPackage != null)
            {
                var nextEp = EpisodeNavigator.FindNextEpisode(_currentPackage);
                if (nextEp != null)
                {
                    TriggerAutoNext(nextEp);
                }
            }
        });
    }

    private void OnPlaybackStateChanged(bool isPlaying)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            PlayPauseIcon.Glyph = isPlaying ? "\uE769" : "\uE768";
            ToolTipService.SetToolTip(PlayPauseButton, isPlaying ? $"{AppStrings.Pause} (Space)" : $"{AppStrings.Play} (Space)");
            if (isPlaying)
            {
                _autoHideTimer.Start();
            }
            else
            {
                _autoHideTimer.Stop();
                ShowControls();
                _historyTracker.OnPause();
            }
        });
    }

    private void OnTracksChanged(IReadOnlyList<MediaTrack> tracks)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            PopulateTrackMenu(tracks);
        });
    }

    private void PopulateTrackMenu(IReadOnlyList<MediaTrack> tracks)
    {
        // 1. Audio tracks
        AudioTracksSubItem.Items.Clear();
        var audioTracks = tracks.OfType<AudioTrack>().ToList();
        if (audioTracks.Count == 0)
        {
            AudioTracksSubItem.Items.Add(new MenuFlyoutItem { Text = AppStrings.NoAudioTracks, IsEnabled = false });
        }
        else
        {
            foreach (var audio in audioTracks)
            {
                bool isPref = _currentPlan?.AudioResolution?.SelectedTrack?.Id == audio.Id ||
                              string.Equals(_currentPlan?.Preferences?.PreferredAudioLanguage, audio.Language, StringComparison.OrdinalIgnoreCase);

                var item = new RadioMenuFlyoutItem
                {
                    Text = FormatHelper.FormatAudioTrackLabelRu(audio, isPref),
                    IsChecked = audio.IsSelected,
                    GroupName = "AudioTracksGroup"
                };
                var trackId = audio.Id;
                item.Click += async (s, e) =>
                {
                    await _engine.SelectAudioTrackAsync(trackId);
                    if (_currentPackage != null)
                    {
                        await _continuityService.SaveAudioPreferenceAsync(_currentPackage, audio);
                    }
                    ShowOsd($"{AppStrings.Audio}: {AppStrings.GetLanguageNameRu(audio.Language)} ({AppStrings.SavedAsPreference})");
                };
                AudioTracksSubItem.Items.Add(item);
            }
        }

        // 2. Subtitle tracks
        SubtitleTracksSubItem.Items.Clear();
        var subTracks = tracks.OfType<SubtitleTrack>().ToList();

        var offItem = new RadioMenuFlyoutItem
        {
            Text = AppStrings.SubtitlesOff,
            IsChecked = !subTracks.Any(s => s.IsSelected),
            GroupName = "SubtitleTracksGroup"
        };
        offItem.Click += async (s, e) =>
        {
            await _engine.SetSubtitleVisibilityAsync(false);
            if (_currentPackage != null)
            {
                await _continuityService.SaveSubtitlePreferenceAsync(_currentPackage, null, false);
            }
            ShowOsd($"{AppStrings.SubtitlesOff} ({AppStrings.SavedAsPreference})");
        };
        SubtitleTracksSubItem.Items.Add(offItem);

        foreach (var sub in subTracks)
        {
            bool isPref = _currentPlan?.SubtitleResolution?.SelectedTrack?.Id == sub.Id ||
                          string.Equals(_currentPlan?.Preferences?.PreferredSubtitleLanguage, sub.Language, StringComparison.OrdinalIgnoreCase);

            var item = new RadioMenuFlyoutItem
            {
                Text = FormatHelper.FormatSubtitleTrackLabelRu(sub, isPref),
                IsChecked = sub.IsSelected,
                GroupName = "SubtitleTracksGroup"
            };
            var trackId = sub.Id;
            item.Click += async (s, e) =>
            {
                await _engine.SelectSubtitleTrackAsync(trackId);
                await _engine.SetSubtitleVisibilityAsync(true);
                if (_currentPackage != null)
                {
                    await _continuityService.SaveSubtitlePreferenceAsync(_currentPackage, sub, true);
                }
                ShowOsd($"{AppStrings.Subtitles}: {AppStrings.GetLanguageNameRu(sub.Language)} ({AppStrings.SavedAsPreference})");
            };
            SubtitleTracksSubItem.Items.Add(item);
        }
    }

    private async void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_engine == null || !_engine.IsInitialized) return;
        if (_engine.IsPlaying)
        {
            await _engine.PauseAsync();
            ShowOsd(AppStrings.Pause);
        }
        else
        {
            await _engine.PlayAsync();
            ShowOsd(AppStrings.Play);
        }
    }

    private async void TimelineSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isDraggingSlider && _engine != null && _engine.IsInitialized)
        {
            await _engine.SeekAsync(e.NewValue, relative: false);
        }
    }

    private void TimelineSlider_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isDraggingSlider = false;
    }

    private async void VolumeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_engine == null || !_engine.IsInitialized) return;
        _currentVolume = (int)e.NewValue;
        if (!_isMuted)
        {
            VolumeIcon.Glyph = _currentVolume == 0 ? "\uE74F" : "\uE767";
            await _engine.SetVolumeAsync(_currentVolume);
            ShowOsd($"{AppStrings.Volume}: {_currentVolume}%");
        }
    }

    private async void MuteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_engine == null || !_engine.IsInitialized) return;
        _isMuted = !_isMuted;
        if (_isMuted)
        {
            VolumeIcon.Glyph = "\uE74F";
            await _engine.SetVolumeAsync(0);
            ShowOsd(AppStrings.Muted);
        }
        else
        {
            VolumeIcon.Glyph = "\uE767";
            await _engine.SetVolumeAsync(_currentVolume);
            ShowOsd($"{AppStrings.Volume}: {_currentVolume}%");
        }
    }

    private async void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.VideosLibrary,
            ViewMode = PickerViewMode.Thumbnail
        };

        picker.FileTypeFilter.Add(".mkv");
        picker.FileTypeFilter.Add(".mp4");
        picker.FileTypeFilter.Add(".avi");
        picker.FileTypeFilter.Add(".mov");
        picker.FileTypeFilter.Add(".wmv");
        picker.FileTypeFilter.Add(".flv");
        picker.FileTypeFilter.Add(".webm");
        picker.FileTypeFilter.Add(".mka");
        picker.FileTypeFilter.Add(".mp3");
        picker.FileTypeFilter.Add(".flac");
        picker.FileTypeFilter.Add("*");

        WinRT.Interop.InitializeWithWindow.Initialize(picker, _hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            await OpenMediaFileAsync(file.Path);
        }
    }

    private void FullscreenButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private void ToggleFullscreen()
    {
        if (_appWindow == null) return;

        if (_appWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen)
        {
            _appWindow.SetPresenter(AppWindowPresenterKind.Default);
            FullscreenIcon.Glyph = "\uE740";
            ToolTipService.SetToolTip(FullscreenButton, AppStrings.FullscreenShortcut);
            TopBarBorder.Visibility = Visibility.Visible;
            TopBarRow.Height = GridLength.Auto;
            ShowOsd(AppStrings.Windowed);
        }
        else
        {
            _appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
            FullscreenIcon.Glyph = "\uE73F";
            ToolTipService.SetToolTip(FullscreenButton, AppStrings.Windowed);
            TopBarBorder.Visibility = Visibility.Collapsed;
            TopBarRow.Height = new GridLength(0);
            ShowOsd(AppStrings.Fullscreen);
        }
        SyncVideoHostSize();
    }

    private void ExitFullscreen()
    {
        if (_appWindow != null && _appWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen)
        {
            _appWindow.SetPresenter(AppWindowPresenterKind.Default);
            FullscreenIcon.Glyph = "\uE740";
            ToolTipService.SetToolTip(FullscreenButton, AppStrings.FullscreenShortcut);
            TopBarBorder.Visibility = Visibility.Visible;
            TopBarRow.Height = GridLength.Auto;
            ShowOsd(AppStrings.Windowed);
            SyncVideoHostSize();
        }
    }

    private static KeyInput ToKeyInput(VirtualKey key)
    {
        return key switch
        {
            VirtualKey.Space => KeyInput.Space,
            VirtualKey.Left => KeyInput.Left,
            VirtualKey.Right => KeyInput.Right,
            VirtualKey.Up => KeyInput.Up,
            VirtualKey.Down => KeyInput.Down,
            VirtualKey.M => KeyInput.M,
            VirtualKey.F => KeyInput.F,
            VirtualKey.Enter => KeyInput.Enter,
            VirtualKey.Escape => KeyInput.Escape,
            VirtualKey.A => KeyInput.A,
            VirtualKey.S => KeyInput.S,
            VirtualKey.PageUp => KeyInput.PageUp,
            VirtualKey.PageDown => KeyInput.PageDown,
            _ => KeyInput.None
        };
    }

    private async void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
        var alt = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu) & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

        var keyInput = ToKeyInput(e.Key);
        var action = KeyboardCommandRouter.Route(keyInput, ctrl, alt);
        if (action == PlayerAction.None) return;

        e.Handled = true;

        switch (action)
        {
            case PlayerAction.PlayPause:
                PlayPauseButton_Click(this, new RoutedEventArgs());
                break;
            case PlayerAction.SeekForwardSmall:
                DismissResumePrompt();
                await _engine.SeekAsync(5, relative: true);
                _historyTracker.OnSeek();
                ShowOsd("+00:05");
                break;
            case PlayerAction.SeekBackwardSmall:
                DismissResumePrompt();
                await _engine.SeekAsync(-5, relative: true);
                _historyTracker.OnSeek();
                ShowOsd("-00:05");
                break;
            case PlayerAction.SeekForwardLarge:
                DismissResumePrompt();
                await _engine.SeekAsync(30, relative: true);
                _historyTracker.OnSeek();
                ShowOsd("+00:30");
                break;
            case PlayerAction.SeekBackwardLarge:
                DismissResumePrompt();
                await _engine.SeekAsync(-30, relative: true);
                _historyTracker.OnSeek();
                ShowOsd("-00:30");
                break;
            case PlayerAction.VolumeUp:
                VolumeSlider.Value = Math.Min(150, VolumeSlider.Value + 5);
                break;
            case PlayerAction.VolumeDown:
                VolumeSlider.Value = Math.Max(0, VolumeSlider.Value - 5);
                break;
            case PlayerAction.ToggleMute:
                MuteButton_Click(this, new RoutedEventArgs());
                break;
            case PlayerAction.ToggleFullscreen:
                ToggleFullscreen();
                break;
            case PlayerAction.ExitFullscreen:
                if (AutoNextPromptBorder.Visibility == Visibility.Visible)
                {
                    CancelAutoNextPrompt();
                }
                else if (ResumePromptBorder.Visibility == Visibility.Visible)
                {
                    DismissResumePrompt();
                }
                else
                {
                    ExitFullscreen();
                }
                break;
            case PlayerAction.CycleAudioTrack:
                await CycleAudioTrackAsync();
                break;
            case PlayerAction.CycleSubtitleTrack:
                await CycleSubtitleTrackAsync();
                break;
            case PlayerAction.NextEpisode:
                NextEpisodeButton_Click(this, new RoutedEventArgs());
                break;
            case PlayerAction.PreviousEpisode:
                PrevEpisodeButton_Click(this, new RoutedEventArgs());
                break;
        }
    }

    private async Task CycleAudioTrackAsync()
    {
        var audios = _engine.ActiveTracks.OfType<AudioTrack>().ToList();
        if (audios.Count <= 1) return;

        var currentIndex = audios.FindIndex(a => a.IsSelected);
        var nextIndex = (currentIndex + 1) % audios.Count;
        var nextTrack = audios[nextIndex];

        await _engine.SelectAudioTrackAsync(nextTrack.Id);
        if (_currentPackage != null)
        {
            await _continuityService.SaveAudioPreferenceAsync(_currentPackage, nextTrack);
        }
        ShowOsd($"{AppStrings.Audio}: {AppStrings.GetLanguageNameRu(nextTrack.Language)} ({AppStrings.SavedAsPreference})");
    }

    private async Task CycleSubtitleTrackAsync()
    {
        var subs = _engine.ActiveTracks.OfType<SubtitleTrack>().ToList();
        if (subs.Count == 0) return;

        var currentIndex = subs.FindIndex(s => s.IsSelected);
        if (currentIndex == -1)
        {
            // Currently off, select first
            var first = subs[0];
            await _engine.SelectSubtitleTrackAsync(first.Id);
            await _engine.SetSubtitleVisibilityAsync(true);
            if (_currentPackage != null)
            {
                await _continuityService.SaveSubtitlePreferenceAsync(_currentPackage, first, true);
            }
            ShowOsd($"{AppStrings.Subtitles}: {AppStrings.GetLanguageNameRu(first.Language)} ({AppStrings.SavedAsPreference})");
        }
        else if (currentIndex == subs.Count - 1)
        {
            // Turn off
            await _engine.SetSubtitleVisibilityAsync(false);
            if (_currentPackage != null)
            {
                await _continuityService.SaveSubtitlePreferenceAsync(_currentPackage, null, false);
            }
            ShowOsd($"{AppStrings.SubtitlesOff} ({AppStrings.SavedAsPreference})");
        }
        else
        {
            var nextTrack = subs[currentIndex + 1];
            await _engine.SelectSubtitleTrackAsync(nextTrack.Id);
            await _engine.SetSubtitleVisibilityAsync(true);
            if (_currentPackage != null)
            {
                await _continuityService.SaveSubtitlePreferenceAsync(_currentPackage, nextTrack, true);
            }
            ShowOsd($"{AppStrings.Subtitles}: {AppStrings.GetLanguageNameRu(nextTrack.Language)} ({AppStrings.SavedAsPreference})");
        }
    }

    private void RootGrid_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private async void RootGrid_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;

        var items = await e.DataView.GetStorageItemsAsync();
        if (items.Count == 0) return;

        var first = items[0];
        if (first is Windows.Storage.StorageFile file)
        {
            await OpenMediaFileAsync(file.Path);
        }
        else if (first is Windows.Storage.StorageFolder folder)
        {
            var folderFiles = await folder.GetFilesAsync();
            var playable = folderFiles.FirstOrDefault(f =>
                f.FileType.Equals(".mkv", StringComparison.OrdinalIgnoreCase) ||
                f.FileType.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
                f.FileType.Equals(".avi", StringComparison.OrdinalIgnoreCase));

            if (playable != null)
            {
                await OpenMediaFileAsync(playable.Path);
            }
        }
    }

    private void RootGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        ShowControls();
        if (_engine.IsPlaying)
        {
            _autoHideTimer.Stop();
            _autoHideTimer.Start();
        }
    }

    private void AutoHideTimer_Tick(object? sender, object e)
    {
        if (_engine.IsPlaying)
        {
            HideControls();
        }
    }

    private void ShowControls()
    {
        ControlBarBorder.Visibility = Visibility.Visible;
        ControlsRow.Height = GridLength.Auto;
        if (_appWindow?.Presenter.Kind != AppWindowPresenterKind.FullScreen && _currentPackage != null)
        {
            TopBarBorder.Visibility = Visibility.Visible;
            TopBarRow.Height = GridLength.Auto;
        }
        SyncVideoHostSize();
    }

    private void HideControls()
    {
        ControlBarBorder.Visibility = Visibility.Collapsed;
        ControlsRow.Height = new GridLength(0);
        if (_appWindow?.Presenter.Kind == AppWindowPresenterKind.FullScreen)
        {
            TopBarBorder.Visibility = Visibility.Collapsed;
            TopBarRow.Height = new GridLength(0);
        }
        SyncVideoHostSize();
    }

    private void VideoHostBorder_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private async void PrevEpisodeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPackage == null) return;
        var prev = EpisodeNavigator.FindPreviousEpisode(_currentPackage);
        if (prev != null)
        {
            await OpenMediaFileAsync(prev.FilePath);
        }
        else
        {
            ShowOsd("Предыдущая серия отсутствует");
        }
    }

    private async void NextEpisodeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPackage == null) return;
        var next = EpisodeNavigator.FindNextEpisode(_currentPackage);
        if (next != null)
        {
            await OpenMediaFileAsync(next.FilePath);
        }
        else
        {
            ShowOsd("Следующая серия отсутствует");
        }
    }

    private void ShowOsd(string text)
    {
        OsdTextBlock.Text = text;
        OsdBorder.Visibility = Visibility.Visible;
        _osdTimer.Stop();
        _osdTimer.Start();
    }

    private void ShowError(string title, string details)
    {
        ErrorNotificationBar.Title = title;
        ErrorNotificationBar.Message = details;
        ErrorNotificationBar.IsOpen = true;
    }
}
