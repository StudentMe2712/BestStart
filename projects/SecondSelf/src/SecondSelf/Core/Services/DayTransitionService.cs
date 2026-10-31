using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SecondSelf.Core.Messages;
using SecondSelf.Data.Entities;
using SecondSelf.Data.Repositories;

namespace SecondSelf.Core.Services;

/// <summary>
/// Реализация сервиса суточного перехода в 04:00 утра и фиксации видео-ритуала дня.
/// </summary>
public sealed class DayTransitionService : IDayTransitionService
{
    private readonly IDailyRitualRepository _dailyRitualRepository;
    private readonly IVideoLibraryService _videoLibraryService;
    private readonly IFisherYatesShuffleService _shuffleService;
    private readonly INotificationBus _notificationBus;

    private readonly Timer? _timer;
    private bool _disposed;

    public string CurrentLogicalDate { get; private set; }

    public static string GetLogicalDate(DateTime localTime) =>
        (localTime - TimeSpan.FromHours(4)).Date.ToString("yyyy-MM-dd");

    public DayTransitionService(
        IDailyRitualRepository dailyRitualRepository,
        IVideoLibraryService videoLibraryService,
        IFisherYatesShuffleService shuffleService,
        INotificationBus notificationBus,
        bool autoStartTimer = true)
    {
        _dailyRitualRepository = dailyRitualRepository ?? throw new ArgumentNullException(nameof(dailyRitualRepository));
        _videoLibraryService = videoLibraryService ?? throw new ArgumentNullException(nameof(videoLibraryService));
        _shuffleService = shuffleService ?? throw new ArgumentNullException(nameof(shuffleService));
        _notificationBus = notificationBus ?? throw new ArgumentNullException(nameof(notificationBus));

        CurrentLogicalDate = GetLogicalDate(DateTime.Now);

        if (autoStartTimer)
        {
            _timer = new Timer(OnTimerFired, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            StartTimer();
        }
    }

    public async Task<DailyRitualLog?> GetOrAssignDailyRitualAsync(DateTime? currentTime = null, CancellationToken cancellationToken = default)
    {
        var time = currentTime ?? DateTime.Now;
        var logicalDate = GetLogicalDate(time);

        var existingLog = await _dailyRitualRepository.GetDailyRitualLogAsync(logicalDate, cancellationToken).ConfigureAwait(false);

        if (existingLog != null)
        {
            var video = await _videoLibraryService.GetVideoByIdAsync(existingLog.VideoId, cancellationToken).ConfigureAwait(false);
            if (video != null && !video.IsMissing && !string.IsNullOrWhiteSpace(video.FilePath) && File.Exists(video.FilePath))
            {
                // Видео уже назначено и физически присутствует на диске
                return existingLog;
            }

            // Файл назначенного видео был удален с диска
            if (video != null)
            {
                await _videoLibraryService.UpdateMissingStatusAsync(video.Id, true, cancellationToken).ConfigureAwait(false);
            }
        }

        // Запрашиваем следующий доступный ролик из очереди
        var nextVideo = await _shuffleService.GetNextVideoInCycleAsync(cancellationToken).ConfigureAwait(false);
        if (nextVideo == null)
        {
            return null;
        }

        var newLog = new DailyRitualLog
        {
            LogicalDate = logicalDate,
            VideoId = nextVideo.Id,
            LastPositionMs = 0,
            IsCompleted = false,
            CompletedAt = null
        };

        await _dailyRitualRepository.UpsertDailyRitualLogAsync(newLog, cancellationToken).ConfigureAwait(false);
        return newLog;
    }

    public TimeSpan CalculateDelayUntilNextTransition(DateTime currentTime)
    {
        var next4Am = currentTime.Date.AddHours(4);
        if (currentTime >= next4Am)
        {
            next4Am = next4Am.AddDays(1);
        }

        return next4Am - currentTime;
    }

    public void StartTimer()
    {
        if (_disposed || _timer == null) return;

        var delay = CalculateDelayUntilNextTransition(DateTime.Now);
        _timer.Change(delay, Timeout.InfiniteTimeSpan);
    }

    public void StopTimer()
    {
        if (_disposed || _timer == null) return;

        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public async Task TriggerDayTransitionAsync(DateTime transitionTime, CancellationToken cancellationToken = default)
    {
        string previousDate = CurrentLogicalDate;
        string newDate = GetLogicalDate(transitionTime);

        CurrentLogicalDate = newDate;

        // Обновляем назначенное видео на новую логическую дату
        await GetOrAssignDailyRitualAsync(transitionTime, cancellationToken).ConfigureAwait(false);

        // Уведомляем систему о переходе суток
        _notificationBus.Publish(new DayChangedMessage(previousDate, newDate));
    }

    public async Task<DailyRitualLog?> ReplaceCorruptedVideoAsync(string videoId, DateTime? currentTime = null, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(videoId))
        {
            await _videoLibraryService.UpdateMissingStatusAsync(videoId, true, cancellationToken).ConfigureAwait(false);
        }

        var time = currentTime ?? DateTime.Now;
        var logicalDate = GetLogicalDate(time);

        var nextVideo = await _shuffleService.GetNextVideoInCycleAsync(cancellationToken).ConfigureAwait(false);
        if (nextVideo == null)
        {
            return null;
        }

        var newLog = new DailyRitualLog
        {
            LogicalDate = logicalDate,
            VideoId = nextVideo.Id,
            LastPositionMs = 0,
            IsCompleted = false,
            CompletedAt = null
        };

        await _dailyRitualRepository.UpsertDailyRitualLogAsync(newLog, cancellationToken).ConfigureAwait(false);
        return newLog;
    }

    private async void OnTimerFired(object? state)
    {
        if (_disposed) return;

        try
        {
            await TriggerDayTransitionAsync(DateTime.Now).ConfigureAwait(false);
        }
        catch
        {
            // Подавляем исключения периодического таймера
        }
        finally
        {
            if (!_disposed)
            {
                StartTimer();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _timer?.Dispose();
    }
}
