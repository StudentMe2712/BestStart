using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SecondSelf.Core.Messages;
using SecondSelf.Data.Entities;
using SecondSelf.Data.Repositories;

namespace SecondSelf.Core.Services;

/// <summary>
/// Реализация сервиса честного воспроизведения Daily Reset, 2-секундного автосохранения
/// и фиксации выполнения ежедневного ритуала.
/// </summary>
public sealed class VideoPlaybackService : IVideoPlaybackService
{
    private readonly IDailyRitualRepository _dailyRitualRepository;
    private readonly IDayTransitionService _dayTransitionService;
    private readonly IVideoLibraryService _videoLibraryService;
    private readonly IProgressCalculationEngine _progressEngine;
    private readonly INotificationBus _notificationBus;

    private readonly Timer? _autoSaveTimer;
    private readonly object _stateLock = new();
    private bool _disposed;

    public PlaybackState State { get; private set; } = PlaybackState.Idle;
    public string StatusText { get; private set; } = "Ready";
    public VideoLibraryItem? CurrentVideo { get; private set; }
    public DailyRitualLog? CurrentLog { get; private set; }
    public int CurrentPositionMs { get; private set; }
    public int DurationMs { get; private set; }

    public double Volume { get; set; } = 1.0;
    public bool IsMuted { get; set; } = false;

    public bool IsCompletedPlayback { get; private set; }
    public bool IsCountButtonVisible { get; private set; }
    public bool IsRitualCounted { get; private set; }

    public int AutoSaveIntervalMs { get; set; } = 2000;

    public double ProgressRatio =>
        DurationMs > 0 ? Math.Clamp((double)CurrentPositionMs / DurationMs, 0.0, 1.0) : 0.0;

    public string FormattedTimecode
    {
        get
        {
            var cur = FormatTime(CurrentPositionMs);
            var dur = FormatTime(DurationMs);
            return $"{cur} / {dur}";
        }
    }

    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler<int>? PositionChanged;
    public event EventHandler? PlaybackCompleted;

    public VideoPlaybackService(
        IDailyRitualRepository dailyRitualRepository,
        IDayTransitionService dayTransitionService,
        IVideoLibraryService videoLibraryService,
        IProgressCalculationEngine progressEngine,
        INotificationBus notificationBus,
        bool enableTimer = true)
    {
        _dailyRitualRepository = dailyRitualRepository ?? throw new ArgumentNullException(nameof(dailyRitualRepository));
        _dayTransitionService = dayTransitionService ?? throw new ArgumentNullException(nameof(dayTransitionService));
        _videoLibraryService = videoLibraryService ?? throw new ArgumentNullException(nameof(videoLibraryService));
        _progressEngine = progressEngine ?? throw new ArgumentNullException(nameof(progressEngine));
        _notificationBus = notificationBus ?? throw new ArgumentNullException(nameof(notificationBus));

        if (enableTimer)
        {
            _autoSaveTimer = new Timer(OnAutoSaveTimerTick, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    public async Task<bool> InitializePlaybackAsync(DateTime? currentTime = null, CancellationToken cancellationToken = default)
    {
        var time = currentTime ?? DateTime.Now;
        var log = await _dayTransitionService.GetOrAssignDailyRitualAsync(time, cancellationToken).ConfigureAwait(false);

        if (log == null)
        {
            SetState(PlaybackState.Idle, "No video available");
            return false;
        }

        var video = await _videoLibraryService.GetVideoByIdAsync(log.VideoId, cancellationToken).ConfigureAwait(false);

        // Проверка физической доступности файла на диске перед запуском
        if (video == null || string.IsNullOrWhiteSpace(video.FilePath) || !File.Exists(video.FilePath))
        {
            // Файл удален или поврежден — производим тихую автозамену
            log = await _dayTransitionService.ReplaceCorruptedVideoAsync(log.VideoId, time, cancellationToken).ConfigureAwait(false);
            if (log == null)
            {
                SetState(PlaybackState.Idle, "No video available");
                return false;
            }

            video = await _videoLibraryService.GetVideoByIdAsync(log.VideoId, cancellationToken).ConfigureAwait(false);
            if (video == null || !File.Exists(video.FilePath))
            {
                SetState(PlaybackState.Idle, "No video available");
                return false;
            }
        }

        lock (_stateLock)
        {
            CurrentLog = log;
            CurrentVideo = video;
            DurationMs = video.DurationSeconds > 0
                ? (int)(video.DurationSeconds * 1000.0)
                : 120000; // 120 секунд (2 минуты согласно спецификации Daily Reset)
            IsRitualCounted = log.IsCompleted;

            // Возобновление с позиции LastPositionMs - 1000 мс (1 секунда контекста)
            int resumePos = Math.Max(0, log.LastPositionMs - 1000);
            CurrentPositionMs = resumePos;

            if (log.IsCompleted)
            {
                IsCompletedPlayback = true;
                IsCountButtonVisible = false;
                SetState(PlaybackState.Completed, "Done for Today");
            }
            else if (CurrentPositionMs > 0)
            {
                IsCompletedPlayback = false;
                IsCountButtonVisible = false;
                SetState(PlaybackState.Paused, "Paused");
            }
            else
            {
                IsCompletedPlayback = false;
                IsCountButtonVisible = false;
                SetState(PlaybackState.Ready, "Ready");
            }
        }

        PositionChanged?.Invoke(this, CurrentPositionMs);
        return true;
    }

    public Task PlayAsync()
    {
        lock (_stateLock)
        {
            if (State == PlaybackState.Completed && IsCompletedPlayback)
            {
                // Повторный просмотр завершенного видео
                SetState(PlaybackState.Playing, IsRitualCounted ? "Done for Today" : "Playing");
            }
            else
            {
                SetState(PlaybackState.Playing, "Playing");
            }

            StartAutoSaveTimer();
        }

        return Task.CompletedTask;
    }

    public async Task PauseAsync()
    {
        lock (_stateLock)
        {
            StopAutoSaveTimer();
            if (State != PlaybackState.Completed)
            {
                SetState(PlaybackState.Paused, "Paused");
            }
        }

        // Мгновенное сохранение позиции при постановке на паузу
        await SavePositionAsync().ConfigureAwait(false);
    }

    public async Task StopAndSaveAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            StopAutoSaveTimer();
            if (State != PlaybackState.Completed)
            {
                SetState(PlaybackState.Paused, "Paused");
            }
        }

        // Сохранение позиции при закрытии плеера
        await SavePositionAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SavePositionAsync(CancellationToken cancellationToken = default)
    {
        string? date;
        int pos;

        lock (_stateLock)
        {
            if (CurrentLog == null) return;
            date = CurrentLog.LogicalDate;
            pos = CurrentPositionMs;
            CurrentLog.LastPositionMs = pos;
        }

        if (!string.IsNullOrEmpty(date))
        {
            try
            {
                await _dailyRitualRepository.UpdateLastPositionAsync(date, pos, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Подавляем сбои при сохранении позиции
            }
        }
    }

    public void UpdatePosition(int positionMs, int durationMs)
    {
        bool reachedEnd = false;

        lock (_stateLock)
        {
            CurrentPositionMs = Math.Max(0, positionMs);
            if (durationMs > 0)
            {
                DurationMs = durationMs;
            }

            if (DurationMs > 0 && CurrentPositionMs >= DurationMs && !IsCompletedPlayback)
            {
                reachedEnd = true;
            }
        }

        PositionChanged?.Invoke(this, CurrentPositionMs);

        if (reachedEnd)
        {
            _ = HandleMediaEndedAsync();
        }
    }

    public async Task HandleMediaEndedAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            StopAutoSaveTimer();
            IsCompletedPlayback = true;

            if (DurationMs > 0)
            {
                CurrentPositionMs = DurationMs;
            }

            SetState(PlaybackState.Completed, IsRitualCounted ? "Done for Today" : "Completed playback");

            // Кнопка фиксации ритуала появляется ТОЛЬКО если ритуал еще не был засчитан за эти сутки
            IsCountButtonVisible = !IsRitualCounted;
        }

        // Сохраняем финальную позицию
        await SavePositionAsync(cancellationToken).ConfigureAwait(false);

        PlaybackCompleted?.Invoke(this, EventArgs.Empty);
    }

    public async Task<bool> HandleMediaFailedAsync(CancellationToken cancellationToken = default)
    {
        string? brokenVideoId;
        DateTime logTime;

        lock (_stateLock)
        {
            StopAutoSaveTimer();
            brokenVideoId = CurrentVideo?.Id;
            logTime = CurrentLog != null && DateOnly.TryParse(CurrentLog.LogicalDate, out var d)
                ? d.ToDateTime(new TimeOnly(12, 0))
                : DateTime.Now;
        }

        if (string.IsNullOrEmpty(brokenVideoId))
        {
            return false;
        }

        // Помечаем битый файл IsMissing = 1 и запрашиваем следующий валидный из очереди
        var newLog = await _dayTransitionService.ReplaceCorruptedVideoAsync(brokenVideoId, logTime, cancellationToken).ConfigureAwait(false);
        if (newLog == null)
        {
            lock (_stateLock)
            {
                CurrentVideo = null;
                CurrentLog = null;
                SetState(PlaybackState.Idle, "No video available");
            }
            return false;
        }

        var newVideo = await _videoLibraryService.GetVideoByIdAsync(newLog.VideoId, cancellationToken).ConfigureAwait(false);
        if (newVideo == null || !File.Exists(newVideo.FilePath))
        {
            lock (_stateLock)
            {
                CurrentVideo = null;
                CurrentLog = null;
                SetState(PlaybackState.Idle, "No video available");
            }
            return false;
        }

        lock (_stateLock)
        {
            CurrentLog = newLog;
            CurrentVideo = newVideo;
            DurationMs = newVideo.DurationSeconds > 0
                ? (int)(newVideo.DurationSeconds * 1000.0)
                : 120000;
            CurrentPositionMs = 0;
            IsCompletedPlayback = false;
            IsCountButtonVisible = false;
            IsRitualCounted = false;

            SetState(PlaybackState.Ready, "Ready");
        }

        PositionChanged?.Invoke(this, CurrentPositionMs);
        return true;
    }

    public async Task<bool> CountRitualCompletionAsync(DateTime? completedAt = null, CancellationToken cancellationToken = default)
    {
        string logicalDate;
        string videoId;

        lock (_stateLock)
        {
            // Защита от повторного начисления
            if (IsRitualCounted || CurrentLog == null)
            {
                return false;
            }

            logicalDate = CurrentLog.LogicalDate;
            videoId = CurrentLog.VideoId;
        }

        // Проверка в базе данных на случай параллельных обновлений
        var existingLog = await _dailyRitualRepository.GetDailyRitualLogAsync(logicalDate, cancellationToken).ConfigureAwait(false);
        if (existingLog != null && existingLog.IsCompleted)
        {
            lock (_stateLock)
            {
                IsRitualCounted = true;
                IsCountButtonVisible = false;
                SetState(PlaybackState.Completed, "Done for Today");
            }
            return false;
        }

        var timestamp = completedAt ?? DateTime.UtcNow;
        var completedAtIso = timestamp.ToString("O");

        // 1. Запись в SQLite DailyRitualLogs (триггер TR_DailyRitualLogs_IncrementPlayback инкрементирует PlaybackCount в VideoLibrary)
        await _dailyRitualRepository.MarkCompletedAsync(logicalDate, completedAtIso, cancellationToken).ConfigureAwait(false);

        lock (_stateLock)
        {
            if (CurrentLog != null)
            {
                CurrentLog.IsCompleted = true;
                CurrentLog.CompletedAt = completedAtIso;
            }

            IsRitualCounted = true;
            IsCountButtonVisible = false;
            SetState(PlaybackState.Completed, "Done for Today");
        }

        // 2. Публикация сообщения в INotificationBus
        _notificationBus.Publish(new DailyRitualCountedMessage(logicalDate, videoId, completedAtIso));

        return true;
    }

    private void SetState(PlaybackState newState, string statusText)
    {
        State = newState;
        StatusText = statusText;
        StateChanged?.Invoke(this, newState);
    }

    private void StartAutoSaveTimer()
    {
        if (_disposed || _autoSaveTimer == null) return;
        _autoSaveTimer.Change(TimeSpan.FromMilliseconds(AutoSaveIntervalMs), TimeSpan.FromMilliseconds(AutoSaveIntervalMs));
    }

    private void StopAutoSaveTimer()
    {
        if (_disposed || _autoSaveTimer == null) return;
        _autoSaveTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    private async void OnAutoSaveTimerTick(object? state)
    {
        if (_disposed) return;

        try
        {
            await SavePositionAsync().ConfigureAwait(false);
        }
        catch
        {
            // Подавляем сбои таймера
        }
    }

    public static string FormatTime(int ms)
    {
        int totalSeconds = Math.Max(0, ms / 1000);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes:D2}:{seconds:D2}";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _autoSaveTimer?.Dispose();
    }
}

/// <summary>
/// Заглушка сервиса воспроизведения для автономных тестов без медиа-подсистемы.
/// </summary>
public sealed class NullVideoPlaybackService : IVideoPlaybackService
{
    public PlaybackState State => PlaybackState.Idle;
    public string StatusText => "Ready";
    public VideoLibraryItem? CurrentVideo => null;
    public DailyRitualLog? CurrentLog => null;
    public int CurrentPositionMs => 0;
    public int DurationMs => 0;
    public double Volume { get; set; } = 1.0;
    public bool IsMuted { get; set; } = false;
    public bool IsCompletedPlayback => false;
    public bool IsCountButtonVisible => false;
    public bool IsRitualCounted => false;
    public string FormattedTimecode => "00:00 / 00:00";
    public double ProgressRatio => 0.0;
    public int AutoSaveIntervalMs { get; set; } = 2000;

#pragma warning disable CS0067
    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler<int>? PositionChanged;
    public event EventHandler? PlaybackCompleted;
#pragma warning restore CS0067

    public Task<bool> InitializePlaybackAsync(DateTime? currentTime = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task PlayAsync() => Task.CompletedTask;
    public Task PauseAsync() => Task.CompletedTask;
    public Task StopAndSaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SavePositionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void UpdatePosition(int positionMs, int durationMs) { }
    public Task HandleMediaEndedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<bool> HandleMediaFailedAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<bool> CountRitualCompletionAsync(DateTime? completedAt = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public void Dispose() { }
}

