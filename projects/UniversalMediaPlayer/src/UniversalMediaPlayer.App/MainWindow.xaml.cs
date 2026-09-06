using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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

    private nint _origVideoWndProc;
    private Win32.WndProcDelegate? _videoWndProcDelegate;
    private long _lastClickTick;

    private bool _isDraggingSlider;
    private bool _isDraggingVolume;
    private bool _isPointerOverControls;
    private bool _isFlyoutOpen;
    private volatile bool _isControlsHidden;

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

        _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5.0) };
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
            if (_origVideoWndProc != 0)
            {
                Win32.SetWindowLongPtr(_videoHwnd, Win32.GWLP_WNDPROC, _origVideoWndProc);
            }
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
                SetApplicationIcon();
            }

            CreateVideoChildWindow();
            File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] CreateVideoChildWindow created child HWND: {_videoHwnd:X}\n");

            // Hook engine events
            _engine.TimeUpdated += OnTimeUpdated;
            _engine.PlaybackStateChanged += OnPlaybackStateChanged;
            _engine.StateChanged += OnEngineStateChanged;
            _engine.TracksChanged += OnTracksChanged;

            // Initial control states for empty playback
            PlayPauseButton.IsEnabled = false;
            TimelineSlider.IsEnabled = false;
            TracksButton.IsEnabled = false;
            PrevEpisodeButton.IsEnabled = false;
            NextEpisodeButton.IsEnabled = false;

            // Flyout events for auto-hide tracking
            TracksMenuFlyout.Opened += (s, e) =>
            {
                _isFlyoutOpen = true;
                _autoHideTimer.Stop();
            };
            TracksMenuFlyout.Closed += (s, e) =>
            {
                _isFlyoutOpen = false;
                ResetAutoHideTimer();
            };

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

    private void SetApplicationIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app_icon.ico");
            if (!File.Exists(iconPath))
            {
                var candidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico");
                if (File.Exists(candidate)) iconPath = candidate;
            }

            if (File.Exists(iconPath))
            {
                _appWindow?.SetIcon(iconPath);

                if (_hwnd != 0)
                {
                    var hIconBig = Win32.LoadImage(0, iconPath, Win32.IMAGE_ICON, 32, 32, Win32.LR_LOADFROMFILE);
                    var hIconSmall = Win32.LoadImage(0, iconPath, Win32.IMAGE_ICON, 16, 16, Win32.LR_LOADFROMFILE);
                    if (hIconBig != 0) Win32.SendMessage(_hwnd, Win32.WM_SETICON, Win32.ICON_BIG, hIconBig);
                    if (hIconSmall != 0) Win32.SendMessage(_hwnd, Win32.WM_SETICON, Win32.ICON_SMALL, hIconSmall);
                }
            }
        }
        catch (Exception ex)
        {
            File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] SetApplicationIcon EXCEPTION: {ex.Message}\n");
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
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_CLIPSIBLINGS | Win32.WS_CLIPCHILDREN | Win32.SS_NOTIFY,
            0, 0, width, height,
            _hwnd,
            nint.Zero,
            Win32.GetModuleHandleW(null),
            nint.Zero);

        if (_videoHwnd != 0)
        {
            _videoWndProcDelegate = new Win32.WndProcDelegate(VideoHostWndProc);
            _origVideoWndProc = Win32.SetWindowLongPtr(_videoHwnd, Win32.GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_videoWndProcDelegate));
        }

        // Initially hide video window until media is opened
        Win32.ShowWindow(_videoHwnd, Win32.SW_HIDE);
    }

    private nint VideoHostWndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case Win32.WM_ERASEBKGND:
                return (nint)1;

            case Win32.WM_SETCURSOR:
                if (_isControlsHidden)
                {
                    Win32.SetCursor(nint.Zero);
                    return (nint)1;
                }
                break;

            case Win32.WM_SIZE:
                int w = (int)(lParam.ToInt64() & 0xFFFF);
                int h = (int)((lParam.ToInt64() >> 16) & 0xFFFF);
                if (w > 0 && h > 0)
                {
                    Win32.EnumChildWindows(hWnd, (childHwnd, _) =>
                    {
                        Win32.MoveWindow(childHwnd, 0, 0, w, h, true);
                        return false;
                    }, 0);
                }
                break;

            case Win32.WM_LBUTTONDBLCLK:
                DispatcherQueue.TryEnqueue(() => ToggleFullscreen());
                return nint.Zero;

            case Win32.WM_LBUTTONDOWN:
                Win32.SetFocus(_hwnd);
                var now = Environment.TickCount64;
                var dblTime = (long)Win32.GetDoubleClickTime();
                if (now - _lastClickTick <= dblTime)
                {
                    _lastClickTick = 0;
                    DispatcherQueue.TryEnqueue(() => ToggleFullscreen());
                    return nint.Zero;
                }
                _lastClickTick = now;
                DispatcherQueue.TryEnqueue(() =>
                {
                    ShowControls();
                    ResetAutoHideTimer();
                });
                break;

            case Win32.WM_MOUSEMOVE:
                if (_isControlsHidden)
                {
                    _isControlsHidden = false;
                    Win32.SetCursor(Win32.LoadCursor(nint.Zero, Win32.IDC_ARROW));
                }
                int yPixels = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                DispatcherQueue.TryEnqueue(() => OnVideoMouseMove(yPixels));
                break;

            case Win32.WM_MOUSEWHEEL:
                short wheelDelta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                DispatcherQueue.TryEnqueue(() => HandleMouseWheel(wheelDelta));
                return nint.Zero;

            case Win32.WM_KEYDOWN:
            case Win32.WM_SYSKEYDOWN:
                Win32.PostMessage(_hwnd, msg, wParam, lParam);
                return nint.Zero;
        }

        return Win32.CallWindowProc(_origVideoWndProc, hWnd, msg, wParam, lParam);
    }

    private void OnVideoMouseMove(int yInPixels)
    {
        ShowControls();
        ResetAutoHideTimer();
    }

    private void HandleMouseWheel(int delta)
    {
        if (_engine == null || !_engine.IsInitialized) return;
        int step = delta > 0 ? 5 : -5;
        var newVol = Math.Clamp(VolumeSlider.Value + step, 0, 150);
        VolumeSlider.Value = newVol;
    }

    private void RootGrid_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var prop = e.GetCurrentPoint(RootGrid).Properties;
        var delta = prop.MouseWheelDelta;
        HandleMouseWheel(delta);
        e.Handled = true;
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
                if (_currentPackage != null)
                {
                    Win32.SetWindowPos(_videoHwnd, Win32.HWND_TOP, x, y, width, height, Win32.SWP_SHOWWINDOW | Win32.SWP_NOACTIVATE);

                    Win32.EnumChildWindows(_videoHwnd, (childHwnd, _) =>
                    {
                        Win32.MoveWindow(childHwnd, 0, 0, width, height, true);
                        return false;
                    }, 0);

                    Win32.InvalidateRect(_videoHwnd, 0, false);
                    Win32.UpdateWindow(_videoHwnd);
                }
                else
                {
                    // Media is not loaded: hide Win32 video canvas so XAML empty state is visible
                    Win32.SetWindowPos(_videoHwnd, Win32.HWND_TOP, x, y, width, height, Win32.SWP_HIDEWINDOW | Win32.SWP_NOACTIVATE);
                }
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

            // Background non-blocking scan with cancellation support
            MediaPackage package = await Task.Run(() => DirectoryScanner.Scan(filePath, ct), ct);
            ct.ThrowIfCancellationRequested();
            _currentPackage = package;

            EmptyStatePanel.Visibility = Visibility.Collapsed;
            ContinueWatchingBorder.Visibility = Visibility.Collapsed;

            // Make sure video HWND is visible and sized correctly
            Win32.ShowWindow(_videoHwnd, Win32.SW_SHOW);
            SyncVideoHostSize();

            // Display series / episodic identity if recognized
            var audioCount = $"{package.AudioTracks.Count} {AppStrings.Audio.ToLowerInvariant()}";
            var subCount = $"{package.SubtitleTracks.Count} {AppStrings.Subtitles.ToLowerInvariant()}";
            var fontsCount = package.Fonts?.HasFonts == true ? $" · {package.Fonts.Count} шрифтов" : "";

            if (package.Episode != null)
            {
                var s = package.Episode.SeasonNumber.HasValue ? $"S{package.Episode.SeasonNumber:D2}" : "";
                var epStr = $"{package.Episode.ShowTitle} {s}E{package.Episode.EpisodeNumber:D2}".Trim();
                TitleTextBlock.Text = epStr;
                TechnicalMetadataTextBlock.Text = $"· 1 видео · {audioCount} · {subCount}{fontsCount}";
            }
            else
            {
                TitleTextBlock.Text = FormatHelper.CleanTitle(filePath);
                TechnicalMetadataTextBlock.Text = $"· 1 видео · {audioCount} · {subCount}";
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

            // Update technical metadata with decoded stream resolution and codec
            var videoW = await _engine.GetPropertyAsync("video-params/w");
            var videoH = await _engine.GetPropertyAsync("video-params/h");
            var videoCodec = await _engine.GetPropertyAsync("video-codec");
            var cleanCodec = !string.IsNullOrEmpty(videoCodec) ? videoCodec.Split('/')[0].Trim() : "";
            var ext = package.PrimaryVideo.Extension.ToUpperInvariant();
            var resStr = (!string.IsNullOrEmpty(videoW) && !string.IsNullOrEmpty(videoH)) ? $" · {videoW}×{videoH}" : "";
            var codecStr = !string.IsNullOrEmpty(cleanCodec) ? $" · {cleanCodec.ToUpperInvariant()}" : "";
            TechnicalMetadataTextBlock.Text = $"· {ext}{resStr}{codecStr} · {audioCount} · {subCount}{fontsCount}";

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

    private void OnEngineStateChanged(PlaybackState state)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ViewModel.UpdatePlaybackState(state);
            PlayPauseIcon.Glyph = ViewModel.PlayPauseGlyph;
            ToolTipService.SetToolTip(PlayPauseButton, ViewModel.PlayPauseToolTip);
            PlayPauseButton.IsEnabled = ViewModel.IsPlayPauseEnabled;
            TimelineSlider.IsEnabled = state == PlaybackState.Playing || state == PlaybackState.Paused;
            TracksButton.IsEnabled = state == PlaybackState.Playing || state == PlaybackState.Paused;
            PrevEpisodeButton.IsEnabled = _currentPackage?.Episode != null;
            NextEpisodeButton.IsEnabled = _currentPackage?.Episode != null;

            if (state == PlaybackState.Playing)
            {
                _autoHideTimer.Start();
                SyncVideoHostSize();
            }
            else
            {
                _autoHideTimer.Stop();
                ShowControls();
                if (state == PlaybackState.Paused)
                {
                    _historyTracker.OnPause();
                }
            }
        });
    }

    private void OnPlaybackStateChanged(bool isPlaying)
    {
        // State updates are primarily processed by OnEngineStateChanged
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
        if (_engine.State == PlaybackState.Playing)
        {
            await _engine.PauseAsync();
            ShowOsd(AppStrings.Pause);
        }
        else if (_engine.State == PlaybackState.Paused || _engine.State == PlaybackState.Stopped)
        {
            await _engine.PlayAsync();
            ShowOsd(AppStrings.Play);
        }
    }

    private void TimelineSlider_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isDraggingSlider = true;
        _autoHideTimer.Stop();
    }

    private void TimelineSlider_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isDraggingSlider = false;
        ResetAutoHideTimer();
    }

    private void TimelineSlider_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isDraggingSlider = false;
        ResetAutoHideTimer();
    }

    private async void TimelineSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isDraggingSlider && _engine != null && _engine.IsInitialized)
        {
            await _engine.SeekAsync(e.NewValue, relative: false);
            _historyTracker.OnSeek(e.NewValue);
            _currentPositionSeconds = e.NewValue;
            TimecodeTextBlock.Text = $"{FormatHelper.FormatTimecode(e.NewValue)} / {FormatHelper.FormatTimecode(_durationSeconds)}";
        }
    }

    private void VolumeSlider_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isDraggingVolume = true;
        _autoHideTimer.Stop();
    }

    private void VolumeSlider_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isDraggingVolume = false;
        ResetAutoHideTimer();
    }

    private void VolumeSlider_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isDraggingVolume = false;
        ResetAutoHideTimer();
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
        ResetAutoHideTimer();
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
        ResetAutoHideTimer();
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
            ShowControls();
            ShowOsd(AppStrings.Windowed);
        }
        else
        {
            _appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
            FullscreenIcon.Glyph = "\uE73F";
            ToolTipService.SetToolTip(FullscreenButton, AppStrings.Windowed);
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
            ShowControls();
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

    private async Task SeekRelativeWithBoundsAsync(double seconds)
    {
        if (_engine == null || !_engine.IsInitialized) return;
        DismissResumePrompt();

        var current = _currentPositionSeconds;
        var duration = _durationSeconds > 0 ? _durationSeconds : double.MaxValue;
        var target = Math.Clamp(current + seconds, 0, duration);

        await _engine.SeekAsync(target, relative: false);
        _historyTracker.OnSeek(target);
        _currentPositionSeconds = target;

        if (_durationSeconds > 0)
        {
            TimelineSlider.Maximum = _durationSeconds;
            TimelineSlider.Value = target;
        }
        TimecodeTextBlock.Text = $"{FormatHelper.FormatTimecode(target)} / {FormatHelper.FormatTimecode(_durationSeconds)}";

        var sign = seconds >= 0 ? "+" : "−";
        ShowOsd($"{sign}{Math.Abs((int)seconds)} секунд");
        ResetAutoHideTimer();
    }

    private async void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var focused = FocusManager.GetFocusedElement(this.Content.XamlRoot);
        if (focused is TextBox || focused is PasswordBox || focused is RichEditBox) return;

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
                await SeekRelativeWithBoundsAsync(10);
                break;
            case PlayerAction.SeekBackwardSmall:
                await SeekRelativeWithBoundsAsync(-10);
                break;
            case PlayerAction.SeekForwardLarge:
                await SeekRelativeWithBoundsAsync(30);
                break;
            case PlayerAction.SeekBackwardLarge:
                await SeekRelativeWithBoundsAsync(-30);
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
        ResetAutoHideTimer();
    }

    private async Task CycleSubtitleTrackAsync()
    {
        var subs = _engine.ActiveTracks.OfType<SubtitleTrack>().ToList();
        if (subs.Count == 0) return;

        var currentIndex = subs.FindIndex(s => s.IsSelected);
        if (currentIndex == -1)
        {
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
        ResetAutoHideTimer();
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
        if (_isControlsHidden)
        {
            _isControlsHidden = false;
            Win32.SetCursor(Win32.LoadCursor(nint.Zero, Win32.IDC_ARROW));
        }
        ShowControls();
        ResetAutoHideTimer();
    }

    private void TopBarBorder_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOverControls = true;
        _autoHideTimer.Stop();
    }

    private void TopBarBorder_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOverControls = false;
        ResetAutoHideTimer();
    }

    private void ControlBarBorder_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOverControls = true;
        _autoHideTimer.Stop();
    }

    private void ControlBarBorder_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOverControls = false;
        ResetAutoHideTimer();
    }

    private void ResetAutoHideTimer()
    {
        _autoHideTimer.Stop();
        if (_engine.IsPlaying && !_isPointerOverControls && !_isDraggingSlider && !_isDraggingVolume && !_isFlyoutOpen)
        {
            _autoHideTimer.Start();
        }
    }

    private void AutoHideTimer_Tick(object? sender, object e)
    {
        if (_engine.IsPlaying && !_isPointerOverControls && !_isDraggingSlider && !_isDraggingVolume && !_isFlyoutOpen && !_isResumePromptVisible && !_autoNextPrompted)
        {
            HideControls();
        }
    }

    private void ShowControls()
    {
        _isControlsHidden = false;
        Win32.SetCursor(Win32.LoadCursor(nint.Zero, Win32.IDC_ARROW));

        bool changed = false;
        if (ControlBarBorder.Visibility != Visibility.Visible)
        {
            ControlBarBorder.Visibility = Visibility.Visible;
            ControlsRow.Height = GridLength.Auto;
            changed = true;
        }
        if (TopBarBorder.Visibility != Visibility.Visible)
        {
            TopBarBorder.Visibility = Visibility.Visible;
            TopRow.Height = GridLength.Auto;
            changed = true;
        }
        if (changed)
        {
            SyncVideoHostSize();
        }
    }

    private void HideControls()
    {
        _isControlsHidden = true;
        Win32.SetCursor(nint.Zero);

        bool changed = false;
        if (ControlBarBorder.Visibility != Visibility.Collapsed)
        {
            ControlBarBorder.Visibility = Visibility.Collapsed;
            ControlsRow.Height = new GridLength(0);
            changed = true;
        }
        if (TopBarBorder.Visibility != Visibility.Collapsed)
        {
            TopBarBorder.Visibility = Visibility.Collapsed;
            TopRow.Height = new GridLength(0);
            changed = true;
        }
        if (changed)
        {
            SyncVideoHostSize();
        }
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
        ResetAutoHideTimer();
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
        ResetAutoHideTimer();
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

    public async Task CaptureUiStateAsync(string filename)
    {
        try
        {
            var rtb = new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap();
            await rtb.RenderAsync(RootGrid);
            var pixelBuffer = await rtb.GetPixelsAsync();
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UniversalMediaPlayer", "Screenshots");
            Directory.CreateDirectory(dir);
            var localPath = Path.Combine(dir, filename);

            byte[] pixels = new byte[pixelBuffer.Length];
            using (var reader = Windows.Storage.Streams.DataReader.FromBuffer(pixelBuffer))
            {
                reader.ReadBytes(pixels);
            }

            var localFolder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(dir);
            var file = await localFolder.CreateFileAsync(filename, Windows.Storage.CreationCollisionOption.ReplaceExisting);
            using (var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite))
            {
                var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);
                encoder.SetPixelData(
                    Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                    Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                    (uint)rtb.PixelWidth,
                    (uint)rtb.PixelHeight,
                    96, 96,
                    pixels);
                await encoder.FlushAsync();
            }

            var artifactDir = @"C:\Users\Mila\.gemini\antigravity-cli\brain\01c6a22d-9ac2-4840-9a37-52e1e8c0e3ca";
            if (Directory.Exists(artifactDir))
            {
                File.Copy(localPath, Path.Combine(artifactDir, filename), true);
            }

            File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] CaptureUiStateAsync saved {filename} ({rtb.PixelWidth}x{rtb.PixelHeight})\n");
        }
        catch (Exception ex)
        {
            File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] CaptureUiStateAsync EXCEPTION: {ex}\n");
        }
    }

    public async Task RunUiVerificationSuiteAsync()
    {
        File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] UI Verification Suite started\n");
        await Task.Delay(2500);

        // 1. Initial playback state
        await CaptureUiStateAsync("1_initial_playback.png");
        File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] Step 1: Initial playback captured. Position={_currentPositionSeconds:F2}, Duration={_durationSeconds:F2}, TimecodeText='{TimecodeTextBlock.Text}'\n");

        // 2. Seek forward +10s
        await SeekRelativeWithBoundsAsync(10);
        await Task.Delay(300);
        await CaptureUiStateAsync("2_seek_forward_osd.png");
        File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] Step 2: Seek +10s captured. Position={_currentPositionSeconds:F2}, OsdText='{OsdTextBlock.Text}'\n");

        // 3. Seek backward -10s
        await SeekRelativeWithBoundsAsync(-10);
        await Task.Delay(300);
        await CaptureUiStateAsync("3_seek_backward_osd.png");
        File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] Step 3: Seek -10s captured. Position={_currentPositionSeconds:F2}, OsdText='{OsdTextBlock.Text}'\n");

        // 4. Mouse wheel volume adjustment
        var preVol = VolumeSlider.Value;
        HandleMouseWheel(-120);
        await Task.Delay(200);
        File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] Step 4: Mouse wheel volume adjusted. PreVol={preVol}, PostVol={VolumeSlider.Value}\n");

        // 5. Toggle Fullscreen
        ToggleFullscreen();
        await Task.Delay(500);
        var isFullscreen = _appWindow?.Presenter.Kind == AppWindowPresenterKind.FullScreen;
        await CaptureUiStateAsync("4_fullscreen.png");
        File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] Step 5: Fullscreen toggled. IsFullscreen={isFullscreen}\n");

        // 6. Return to Windowed
        ToggleFullscreen();
        await Task.Delay(500);
        var isWindowed = _appWindow?.Presenter.Kind == AppWindowPresenterKind.Default;
        await CaptureUiStateAsync("5_windowed.png");
        File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] Step 6: Returned to windowed. IsWindowed={isWindowed}\n");

        // 7. AutoHide controls test
        HideControls();
        await Task.Delay(300);
        var isControlsHidden = ControlBarBorder.Visibility == Visibility.Collapsed;
        await CaptureUiStateAsync("6_controls_hidden.png");
        File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] Step 7: Controls hidden. IsHidden={isControlsHidden}\n");

        // 8. Restore controls
        ShowControls();
        await Task.Delay(300);
        var isControlsShown = ControlBarBorder.Visibility == Visibility.Visible;
        await CaptureUiStateAsync("7_controls_shown.png");
        File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] Step 8: Controls shown. IsShown={isControlsShown}\n");

        File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] UI_VERIFICATION_COMPLETE_SUCCESS\n");
    }
}
