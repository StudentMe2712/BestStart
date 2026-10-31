using System;
using System.Threading;
using System.Threading.Tasks;
using SecondSelf.Data.Entities;

namespace SecondSelf.Core.Services;

/// <summary>
/// Состояния воспроизведения плеера Daily Reset.
/// </summary>
public enum PlaybackState
{
    Idle,
    Ready,
    Playing,
    Paused,
    Completed
}

/// <summary>
/// Сервис воспроизведения видео, автосохранения позиции и фиксации выполнения ежедневного ритуала.
/// </summary>
public interface IVideoPlaybackService : IDisposable
{
    PlaybackState State { get; }
    string StatusText { get; }
    VideoLibraryItem? CurrentVideo { get; }
    DailyRitualLog? CurrentLog { get; }
    int CurrentPositionMs { get; }
    int DurationMs { get; }
    double Volume { get; set; }
    bool IsMuted { get; set; }
    bool IsCompletedPlayback { get; }
    bool IsCountButtonVisible { get; }
    bool IsRitualCounted { get; }
    string FormattedTimecode { get; }
    double ProgressRatio { get; }
    int AutoSaveIntervalMs { get; set; }

    event EventHandler<PlaybackState>? StateChanged;
    event EventHandler<int>? PositionChanged;
    event EventHandler? PlaybackCompleted;

    Task<bool> InitializePlaybackAsync(DateTime? currentTime = null, CancellationToken cancellationToken = default);
    Task PlayAsync();
    Task PauseAsync();
    Task StopAndSaveAsync(CancellationToken cancellationToken = default);
    Task SavePositionAsync(CancellationToken cancellationToken = default);
    void UpdatePosition(int positionMs, int durationMs);
    Task HandleMediaEndedAsync(CancellationToken cancellationToken = default);
    Task<bool> HandleMediaFailedAsync(CancellationToken cancellationToken = default);
    Task<bool> CountRitualCompletionAsync(DateTime? completedAt = null, CancellationToken cancellationToken = default);
}
