using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecondSelf.Core.Services;

namespace SecondSelf.Core.ViewModels;

/// <summary>
/// ViewModel фокус-плеера Daily Reset Player (размеры 380×680px, Direct3D 11 декодирование,
/// блокировка перемотки, 2-секундное автосохранение, кнопка фиксации ритуала).
/// </summary>
public partial class VideoPlayerViewModel : ObservableObject
{
    private readonly IVideoPlaybackService _playbackService;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _statusForeground = "#36D399";

    [ObservableProperty]
    private string _statusBackground = "#2E26A65B";

    [ObservableProperty]
    private string _videoTitle = "Daily Reset";

    [ObservableProperty]
    private string _videoSubtitle = "A moment to refocus";

    [ObservableProperty]
    private string _videoSourceTag = "local";

    [ObservableProperty]
    private string _videoFilePath = string.Empty;

    [ObservableProperty]
    private int _resumePositionMs = 0;

    [ObservableProperty]
    private string _formattedTimecode = "00:00 / 00:00";

    [ObservableProperty]
    private double _playbackProgressRatio = 0.0;

    [ObservableProperty]
    private double _progressBarWidth = 0.0;

    [ObservableProperty]
    private double _volume = 1.0;

    [ObservableProperty]
    private bool _isMuted = false;

    [ObservableProperty]
    private bool _isPlaying = false;

    [ObservableProperty]
    private bool _isCountButtonVisible = false;

    [ObservableProperty]
    private bool _isCompleted = false;

    [ObservableProperty]
    private bool _isPlayerOpen = false;

    [ObservableProperty]
    private bool _isPlaceholderVisible = false;

    [ObservableProperty]
    private string _placeholderText = "Видео ритуала не назначено";

    public IVideoPlaybackService PlaybackService => _playbackService;

    public event Action? RequestClosePlayer;
    public event Action<string, int>? RequestStartMedia; // (filePath, startPositionMs)
    public event Action? RequestPauseMedia;
    public event Action? RequestResumeMedia;

    public VideoPlayerViewModel(IVideoPlaybackService playbackService)
    {
        _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));

        _playbackService.StateChanged += OnPlaybackStateChanged;
        _playbackService.PositionChanged += OnPositionChanged;
        _playbackService.PlaybackCompleted += OnPlaybackCompleted;
    }

    public async Task<bool> InitializeAsync(DateTime? currentTime = null, CancellationToken cancellationToken = default)
    {
        bool success = await _playbackService.InitializePlaybackAsync(currentTime, cancellationToken).ConfigureAwait(false);

        if (!success || _playbackService.CurrentVideo == null)
        {
            IsPlaceholderVisible = true;
            PlaceholderText = "Локальная видеотека пуста или файлы недоступны";
            VideoFilePath = string.Empty;
            UpdatePropertiesFromService();
            return false;
        }

        IsPlaceholderVisible = false;
        VideoFilePath = _playbackService.CurrentVideo.FilePath;
        VideoTitle = _playbackService.CurrentVideo.FileName;
        ResumePositionMs = _playbackService.CurrentPositionMs;

        UpdatePropertiesFromService();

        // Запуск воспроизведения в плеере с позиции возобновления
        RequestStartMedia?.Invoke(VideoFilePath, ResumePositionMs);
        return true;
    }

    [RelayCommand]
    public async Task PlayPauseAsync()
    {
        if (IsPlaying)
        {
            await _playbackService.PauseAsync().ConfigureAwait(false);
            RequestPauseMedia?.Invoke();
        }
        else
        {
            await _playbackService.PlayAsync().ConfigureAwait(false);
            RequestResumeMedia?.Invoke();
        }

        UpdatePropertiesFromService();
    }

    [RelayCommand]
    public async Task ClosePlayerAsync(CancellationToken cancellationToken = default)
    {
        await _playbackService.StopAndSaveAsync(cancellationToken).ConfigureAwait(false);
        RequestPauseMedia?.Invoke();
        IsPlayerOpen = false;
        UpdatePropertiesFromService();
        RequestClosePlayer?.Invoke();
    }

    [RelayCommand]
    public async Task CountRitualAsync(CancellationToken cancellationToken = default)
    {
        bool counted = await _playbackService.CountRitualCompletionAsync(null, cancellationToken).ConfigureAwait(false);
        if (counted)
        {
            UpdatePropertiesFromService();
            await ClosePlayerAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    public void ToggleMute()
    {
        IsMuted = !IsMuted;
        _playbackService.IsMuted = IsMuted;
    }

    public void SetVolume(double newVolume)
    {
        Volume = Math.Clamp(newVolume, 0.0, 1.0);
        _playbackService.Volume = Volume;
    }

    public void UpdatePosition(int positionMs, int durationMs)
    {
        _playbackService.UpdatePosition(positionMs, durationMs);
        UpdatePropertiesFromService();
    }

    public async Task OnMediaEndedAsync(CancellationToken cancellationToken = default)
    {
        await _playbackService.HandleMediaEndedAsync(cancellationToken).ConfigureAwait(false);
        UpdatePropertiesFromService();
    }

    public async Task<bool> OnMediaFailedAsync(CancellationToken cancellationToken = default)
    {
        bool replaced = await _playbackService.HandleMediaFailedAsync(cancellationToken).ConfigureAwait(false);
        if (replaced && _playbackService.CurrentVideo != null)
        {
            IsPlaceholderVisible = false;
            VideoFilePath = _playbackService.CurrentVideo.FilePath;
            VideoTitle = _playbackService.CurrentVideo.FileName;
            ResumePositionMs = 0;
            UpdatePropertiesFromService();
            RequestStartMedia?.Invoke(VideoFilePath, 0);
            return true;
        }
        else
        {
            IsPlaceholderVisible = true;
            PlaceholderText = "Файл поврежден, доступные ролики в очереди отсутствуют";
            VideoFilePath = string.Empty;
            UpdatePropertiesFromService();
            return false;
        }
    }

    private void UpdatePropertiesFromService()
    {
        StatusText = _playbackService.StatusText;
        FormattedTimecode = _playbackService.FormattedTimecode;
        PlaybackProgressRatio = _playbackService.ProgressRatio;
        ProgressBarWidth = Math.Round(340.0 * PlaybackProgressRatio, 1);
        IsCountButtonVisible = _playbackService.IsCountButtonVisible;
        IsCompleted = _playbackService.IsRitualCounted;
        IsPlaying = _playbackService.State == PlaybackState.Playing;

        switch (_playbackService.State)
        {
            case PlaybackState.Paused:
                StatusForeground = "#F59E0B";
                StatusBackground = "#2EF59E0B";
                break;
            case PlaybackState.Completed:
                StatusForeground = "#36D399";
                StatusBackground = "#2E26A65B";
                break;
            default:
                StatusForeground = "#36D399";
                StatusBackground = "#2E26A65B";
                break;
        }
    }

    private void OnPlaybackStateChanged(object? sender, PlaybackState state)
    {
        UpdatePropertiesFromService();
    }

    private void OnPositionChanged(object? sender, int pos)
    {
        UpdatePropertiesFromService();
    }

    private void OnPlaybackCompleted(object? sender, EventArgs e)
    {
        UpdatePropertiesFromService();
    }
}
