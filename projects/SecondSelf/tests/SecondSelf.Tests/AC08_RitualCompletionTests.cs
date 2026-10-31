using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Core.Messages;
using SecondSelf.Core.Services;
using SecondSelf.Core.ViewModels;
using SecondSelf.Data;
using SecondSelf.Data.Entities;
using SecondSelf.Data.Migrations;
using SecondSelf.Data.Repositories;
using Xunit;

namespace SecondSelf.Tests;

/// <summary>
/// BDD-тесты приёмочного критерия AC-08: Фиксация завершения ритуала кнопкой «Засчитать»,
/// инвариант неначисления баллов до клика, защита от повторов и отказоустойчивость (Этап 4).
/// </summary>
public sealed class AC08_RitualCompletionTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _testVideosDir;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DailyRitualRepository _dailyRitualRepository;
    private readonly VideoLibraryService _videoLibraryService;
    private readonly FisherYatesShuffleService _shuffleService;
    private readonly NotificationBus _notificationBus;
    private readonly DayTransitionService _dayTransitionService;
    private readonly ProgressCalculationEngine _progressEngine;

    private sealed class InMemoryTaskRepository : ITaskRepository
    {
        public Task<IReadOnlyList<ActiveTask>> GetActiveTasksAsync(System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ActiveTask>>(Array.Empty<ActiveTask>());
        public Task AddActiveTaskAsync(ActiveTask task, System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateActiveTaskAsync(ActiveTask task, System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteActiveTaskAsync(string id, System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ArchivedTask> ArchiveTaskAsync(string taskId, string logicalDate, System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult(new ArchivedTask { Id = taskId, Title = "Test" });
        public Task<ActiveTask> RestoreArchivedTaskAsync(string taskId, System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult(new ActiveTask { Id = taskId, Title = "Test" });
        public Task<IReadOnlyList<ArchivedTask>> GetArchivedTasksAsync(string? logicalDate = null, System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ArchivedTask>>(Array.Empty<ArchivedTask>());
        public Task ReorderTasksAsync(System.Collections.Generic.IEnumerable<(string Id, int NewOrder)> orders, System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public AC08_RitualCompletionTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"secondself_ac08_{Guid.NewGuid():N}.db");
        _testVideosDir = Path.Combine(Path.GetTempPath(), $"secondself_ac08_vids_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testVideosDir);

        _connectionFactory = new SqliteConnectionFactory(_testDbPath);
        new DatabaseMigrationService(_connectionFactory).MigrateAsync().GetAwaiter().GetResult();

        _dailyRitualRepository = new DailyRitualRepository(_connectionFactory);
        _videoLibraryService = new VideoLibraryService(_connectionFactory, _testVideosDir);
        _shuffleService = new FisherYatesShuffleService(_connectionFactory, _videoLibraryService);
        _notificationBus = new NotificationBus();
        _progressEngine = new ProgressCalculationEngine();

        _dayTransitionService = new DayTransitionService(
            _dailyRitualRepository,
            _videoLibraryService,
            _shuffleService,
            _notificationBus,
            autoStartTimer: false);
    }

    public void Dispose()
    {
        _dayTransitionService.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
        if (Directory.Exists(_testVideosDir))
        {
            try { Directory.Delete(_testVideosDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task AC08_MediaEnded_ActivatesCompletedStatusAndCountButton_WithoutCountingMonthScore()
    {
        // Given: видео дня запущено
        var videoPath = Path.Combine(_testVideosDir, "ritual_vid.mp4");
        await File.WriteAllTextAsync(videoPath, "video-data");
        await _videoLibraryService.ScanLibraryAsync();

        var playbackService = new VideoPlaybackService(
            _dailyRitualRepository,
            _dayTransitionService,
            _videoLibraryService,
            _progressEngine,
            _notificationBus,
            enableTimer: false);

        var now = new DateTime(2026, 11, 1, 10, 0, 0);
        await playbackService.InitializePlaybackAsync(now);
        await playbackService.PlayAsync();

        // When: видео доигрывает до конца (120000 / 120000)
        playbackService.UpdatePosition(120000, 120000);

        // Then 1: Статус меняется на Completed playback
        Assert.Equal("Completed playback", playbackService.StatusText);
        Assert.Equal(PlaybackState.Completed, playbackService.State);

        // Then 2: Кнопка «Засчитать выполнение» становится видимой
        Assert.True(playbackService.IsCountButtonVisible);

        // Then 3: До клика по кнопке ритуал в зачёт месяца НЕ начисляется (IsCompleted = 0)
        Assert.False(playbackService.IsRitualCounted);
        var log = await _dailyRitualRepository.GetDailyRitualLogAsync("2026-11-01");
        Assert.NotNull(log);
        Assert.False(log.IsCompleted);
        Assert.Null(log.CompletedAt);

        playbackService.Dispose();
    }

    [Fact]
    public async Task AC08_ClickCountButton_RecordsCompletion_UpdatesProgress_IncrementsPlayback_AndBlocksDuplicates()
    {
        // Given: видео в библиотеке
        var videoPath = Path.Combine(_testVideosDir, "ritual_complete.mp4");
        await File.WriteAllTextAsync(videoPath, "video-data");
        await _videoLibraryService.ScanLibraryAsync();

        var playbackService = new VideoPlaybackService(
            _dailyRitualRepository,
            _dayTransitionService,
            _videoLibraryService,
            _progressEngine,
            _notificationBus,
            enableTimer: false);

        var widgetViewModel = new WidgetViewModel(
            new InMemoryTaskRepository(),
            _progressEngine,
            null,
            _dailyRitualRepository,
            playbackService,
            _dayTransitionService,
            _notificationBus);

        var now = new DateTime(2026, 11, 1, 10, 0, 0);
        await playbackService.InitializePlaybackAsync(now);
        await playbackService.PlayAsync();

        // Симулируем окончание видео
        await playbackService.HandleMediaEndedAsync();
        Assert.True(playbackService.IsCountButtonVisible);

        // Подписываемся на шину сообщений
        DailyRitualCountedMessage? receivedMessage = null;
        _notificationBus.Subscribe<DailyRitualCountedMessage>(this, msg => receivedMessage = msg);

        // When: пользователь нажимает кнопку «Засчитать выполнение»
        var completedAt = new DateTime(2026, 11, 1, 10, 2, 0, DateTimeKind.Utc);
        bool counted = await playbackService.CountRitualCompletionAsync(completedAt);

        // Then 1: Зачёт успешен
        Assert.True(counted);
        Assert.True(playbackService.IsRitualCounted);
        Assert.False(playbackService.IsCountButtonVisible);
        Assert.Equal("Done for Today", playbackService.StatusText);

        // Then 2: В SQLite записано IsCompleted = 1 и точный штамп времени
        var log = await _dailyRitualRepository.GetDailyRitualLogAsync("2026-11-01");
        Assert.NotNull(log);
        Assert.True(log.IsCompleted);
        Assert.Equal(completedAt.ToString("O"), log.CompletedAt);

        // Then 3: Опубликовано сообщение в шину
        Assert.NotNull(receivedMessage);
        Assert.Equal("2026-11-01", receivedMessage.LogicalDate);

        // Then 4: Триггер TR_DailyRitualLogs_IncrementPlayback инкрементировал PlaybackCount в VideoLibrary
        var video = await _videoLibraryService.GetVideoByIdAsync(playbackService.CurrentVideo!.Id);
        Assert.NotNull(video);
        Assert.Equal(1, video.PlaybackCount);

        // Then 5: Виджет обновляет статус на Done for Today и пересчитывает прогресс
        await widgetViewModel.RefreshMonthlyProgressAsync(now);
        Assert.Equal("Daily 1/30", widgetViewModel.DailyRatio);

        // Then 6: Защита от повторного начисления при повторном клике / запуске в тот же день
        bool duplicateCount = await playbackService.CountRitualCompletionAsync(completedAt);
        Assert.False(duplicateCount);

        // Проверяем, что PlaybackCount не увеличился повторно
        var videoAfterDuplicate = await _videoLibraryService.GetVideoByIdAsync(playbackService.CurrentVideo!.Id);
        Assert.NotNull(videoAfterDuplicate);
        Assert.Equal(1, videoAfterDuplicate.PlaybackCount);

        playbackService.Dispose();
    }

    [Fact]
    public async Task AC08_FaultTolerance_SilentReplacement_WhenFileDeletedOrMediaFailed()
    {
        // Given: 2 видео в библиотеке, ролик 1 назначен на день
        var vid1Path = Path.Combine(_testVideosDir, "video_1.mp4");
        var vid2Path = Path.Combine(_testVideosDir, "video_2.mp4");
        await File.WriteAllTextAsync(vid1Path, "vid1-bytes");
        await File.WriteAllTextAsync(vid2Path, "vid2-bytes");
        await _videoLibraryService.ScanLibraryAsync();

        var playbackService = new VideoPlaybackService(
            _dailyRitualRepository,
            _dayTransitionService,
            _videoLibraryService,
            _progressEngine,
            _notificationBus,
            enableTimer: false);

        var now = new DateTime(2026, 11, 1, 10, 0, 0);
        await playbackService.InitializePlaybackAsync(now);
        Assert.NotNull(playbackService.CurrentVideo);

        var initialVideo = playbackService.CurrentVideo;
        var initialVideoId = initialVideo.Id;
        var initialFilePath = initialVideo.FilePath;

        // When 1: Файл текущего назначенного ролика физически удаляется с диска и генерируется ошибка декодирования MediaFailed
        File.Delete(initialFilePath);

        bool replaced = await playbackService.HandleMediaFailedAsync();

        // Then 1: Система производит тихую замену на другой валидный ролик из очереди
        Assert.True(replaced);
        Assert.NotNull(playbackService.CurrentVideo);
        Assert.NotEqual(initialVideoId, playbackService.CurrentVideo.Id);
        Assert.True(File.Exists(playbackService.CurrentVideo.FilePath));
        Assert.Equal(PlaybackState.Ready, playbackService.State);

        // Then 2: Битый файл в VideoLibrary помечен IsMissing = 1
        var brokenVideo = await _videoLibraryService.GetVideoByIdAsync(initialVideoId);
        Assert.NotNull(brokenVideo);
        Assert.True(brokenVideo.IsMissing);

        // Then 3: В суточном логе логическая дата привязана к новому валидному ролику
        var updatedLog = await _dailyRitualRepository.GetDailyRitualLogAsync("2026-11-01");
        Assert.NotNull(updatedLog);
        Assert.Equal(playbackService.CurrentVideo.Id, updatedLog.VideoId);

        playbackService.Dispose();
    }
}
