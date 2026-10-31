using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Core.Messages;
using SecondSelf.Core.Services;
using SecondSelf.Data;
using SecondSelf.Data.Migrations;
using SecondSelf.Data.Repositories;
using Xunit;

namespace SecondSelf.Tests;

/// <summary>
/// BDD-тесты приёмочного критерия AC-06: Назначение видео на сутки и Shuffle без повторений.
/// Given: в библиотеку добавлено 5 видеофайлов.
/// When: наступают новые сутки (переход через 04:00 утра), и пользователь открывает виджет.
/// Then:
///   1. Система берёт следующее видео из очереди Фишера — Йейтса (CycleQueue).
///   2. Видео фиксируется за текущими логическими сутками (YYYY-MM-DD).
///   3. При перезапуске приложения в эти же сутки видео не меняется.
///   4. Ролик не повторится до тех пор, пока не будут просмотрены все 5 роликов из библиотеки.
/// </summary>
public sealed class AC06_DailyVideoAssignmentTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _testVideosDir;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DailyRitualRepository _dailyRitualRepository;
    private readonly VideoLibraryService _videoLibraryService;
    private readonly FisherYatesShuffleService _shuffleService;
    private readonly NotificationBus _notificationBus;
    private readonly DayTransitionService _dayTransitionService;

    public AC06_DailyVideoAssignmentTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"secondself_ac06_{Guid.NewGuid():N}.db");
        _testVideosDir = Path.Combine(Path.GetTempPath(), $"secondself_ac06_vids_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testVideosDir);

        _connectionFactory = new SqliteConnectionFactory(_testDbPath);
        new DatabaseMigrationService(_connectionFactory).MigrateAsync().GetAwaiter().GetResult();

        _dailyRitualRepository = new DailyRitualRepository(_connectionFactory);
        _videoLibraryService = new VideoLibraryService(_connectionFactory, _testVideosDir);
        _shuffleService = new FisherYatesShuffleService(_connectionFactory, _videoLibraryService);
        _notificationBus = new NotificationBus();

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
    public async Task AC06_FiveVideos_AssignedPerDay_PersistsAcrossRestarts_AndCompletesFullCycleWithoutRepetition()
    {
        // Given: в библиотеку добавлено 5 видеофайлов
        var fileNames = new[] { "video_1.mp4", "video_2.mp4", "video_3.mp4", "video_4.mp4", "video_5.mp4" };
        foreach (var fn in fileNames)
        {
            await File.WriteAllTextAsync(Path.Combine(_testVideosDir, fn), $"content of {fn}");
        }
        await _videoLibraryService.ScanLibraryAsync();

        var libraryItems = await _videoLibraryService.GetAllVideosAsync();
        Assert.Equal(5, libraryItems.Count);

        // When/Then 1: Назначение видео на сутки
        var day1Time = new DateTime(2026, 11, 1, 10, 0, 0); // День 1 (логическая дата 2026-11-01)
        var ritualDay1 = await _dayTransitionService.GetOrAssignDailyRitualAsync(day1Time);
        Assert.NotNull(ritualDay1);
        Assert.Equal("2026-11-01", ritualDay1.LogicalDate);
        Assert.False(string.IsNullOrEmpty(ritualDay1.VideoId));

        // When/Then 2: Неизменность ролика при перезапусках приложения в те же логические сутки
        // Симулируем повторные запуски приложения в 14:00, 20:00 и в 03:30 ночи следующих суток (до 04:00)
        var restart1 = await _dayTransitionService.GetOrAssignDailyRitualAsync(new DateTime(2026, 11, 1, 14, 0, 0));
        var restart2 = await _dayTransitionService.GetOrAssignDailyRitualAsync(new DateTime(2026, 11, 1, 20, 0, 0));
        var restart3 = await _dayTransitionService.GetOrAssignDailyRitualAsync(new DateTime(2026, 11, 2, 3, 30, 0)); // Еще принадлежат суткам 2026-11-01

        Assert.Equal(ritualDay1.VideoId, restart1!.VideoId);
        Assert.Equal(ritualDay1.VideoId, restart2!.VideoId);
        Assert.Equal(ritualDay1.VideoId, restart3!.VideoId);

        // When/Then 3: Прохождение полного цикла без повторений до исчерпания всех 5 роликов
        var assignedVideoIds = new List<string> { ritualDay1.VideoId };

        for (int day = 2; day <= 5; day++)
        {
            var nextDayTime = new DateTime(2026, 11, day, 10, 0, 0);
            var ritualNext = await _dayTransitionService.GetOrAssignDailyRitualAsync(nextDayTime);
            Assert.NotNull(ritualNext);
            Assert.Equal($"2026-11-{day:D2}", ritualNext.LogicalDate);

            // Видео еще не должно было встречаться в текущем цикле
            Assert.DoesNotContain(ritualNext.VideoId, assignedVideoIds);
            assignedVideoIds.Add(ritualNext.VideoId);
        }

        // В цикле из 5 дней были назначены ровно все 5 уникальных видеофайлов
        Assert.Equal(5, assignedVideoIds.Distinct().Count());
        Assert.Equal(5, assignedVideoIds.Count);

        // When/Then 4: На 6-й день (новый цикл) выбирается видео, при этом исключается повторение подряд с днем 5
        var day6Time = new DateTime(2026, 11, 6, 10, 0, 0);
        var ritualDay6 = await _dayTransitionService.GetOrAssignDailyRitualAsync(day6Time);
        Assert.NotNull(ritualDay6);
        Assert.Equal("2026-11-06", ritualDay6.LogicalDate);
        Assert.NotEqual(assignedVideoIds[4], ritualDay6.VideoId); // Никаких повторений подряд на границе циклов!
    }
}
