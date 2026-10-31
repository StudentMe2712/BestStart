using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Core.Messages;
using SecondSelf.Core.Services;
using SecondSelf.Data;
using SecondSelf.Data.Migrations;
using SecondSelf.Data.Repositories;
using Xunit;

namespace SecondSelf.Tests;

public class DayTransitionServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _testVideosDir;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DailyRitualRepository _dailyRitualRepository;
    private readonly VideoLibraryService _videoLibraryService;
    private readonly FisherYatesShuffleService _shuffleService;
    private readonly NotificationBus _notificationBus;
    private readonly DayTransitionService _service;

    public DayTransitionServiceTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"secondself_daytrans_{Guid.NewGuid():N}.db");
        _testVideosDir = Path.Combine(Path.GetTempPath(), $"secondself_dayvideos_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testVideosDir);

        _connectionFactory = new SqliteConnectionFactory(_testDbPath);
        new DatabaseMigrationService(_connectionFactory).MigrateAsync().GetAwaiter().GetResult();

        _dailyRitualRepository = new DailyRitualRepository(_connectionFactory);
        _videoLibraryService = new VideoLibraryService(_connectionFactory, _testVideosDir);
        _shuffleService = new FisherYatesShuffleService(_connectionFactory, _videoLibraryService);
        _notificationBus = new NotificationBus();

        _service = new DayTransitionService(
            _dailyRitualRepository,
            _videoLibraryService,
            _shuffleService,
            _notificationBus,
            autoStartTimer: false);
    }

    public void Dispose()
    {
        _service.Dispose();
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
    public void GetLogicalDate_SwitchesAtFourAmExact()
    {
        // 03:59:59 belongs to previous day
        var before4Am = new DateTime(2026, 10, 31, 3, 59, 59);
        Assert.Equal("2026-10-30", DayTransitionService.GetLogicalDate(before4Am));

        // 04:00:00 marks the start of the new logical day
        var exact4Am = new DateTime(2026, 10, 31, 4, 0, 0);
        Assert.Equal("2026-10-31", DayTransitionService.GetLogicalDate(exact4Am));

        // 04:01:00 belongs to the new day
        var after4Am = new DateTime(2026, 10, 31, 4, 1, 0);
        Assert.Equal("2026-10-31", DayTransitionService.GetLogicalDate(after4Am));

        // 23:59:59 belongs to the same day
        var lateNight = new DateTime(2026, 10, 31, 23, 59, 59);
        Assert.Equal("2026-10-31", DayTransitionService.GetLogicalDate(lateNight));
    }

    [Fact]
    public void CalculateDelayUntilNextTransition_CalculatesExactSpan()
    {
        // Before 04:00 AM (03:50:00 -> delay is 10 minutes)
        var earlyTime = new DateTime(2026, 10, 31, 3, 50, 0);
        var delayEarly = _service.CalculateDelayUntilNextTransition(earlyTime);
        Assert.Equal(TimeSpan.FromMinutes(10), delayEarly);

        // After 04:00 AM (04:00:00 -> delay is exactly 24 hours until tomorrow 04:00)
        var exact4Am = new DateTime(2026, 10, 31, 4, 0, 0);
        var delay4Am = _service.CalculateDelayUntilNextTransition(exact4Am);
        Assert.Equal(TimeSpan.FromHours(24), delay4Am);

        // Daytime (10:00:00 -> delay is 18 hours until tomorrow 04:00)
        var dayTime = new DateTime(2026, 10, 31, 10, 0, 0);
        var delayDay = _service.CalculateDelayUntilNextTransition(dayTime);
        Assert.Equal(TimeSpan.FromHours(18), delayDay);
    }

    [Fact]
    public async Task GetOrAssignDailyRitualAsync_IsIdempotent_PreservesAssignedVideoOnRestarts()
    {
        // Arrange: 3 videos in library
        for (int i = 1; i <= 3; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(_testVideosDir, $"video_{i}.mp4"), $"content-{i}");
        }
        await _videoLibraryService.ScanLibraryAsync();

        var today = new DateTime(2026, 10, 31, 12, 0, 0);

        // Act: First call assigns ritual
        var ritual1 = await _service.GetOrAssignDailyRitualAsync(today);
        Assert.NotNull(ritual1);
        Assert.Equal("2026-10-31", ritual1.LogicalDate);
        var initialVideoId = ritual1.VideoId;

        // Simulate app restart: call again for the same date
        var ritual2 = await _service.GetOrAssignDailyRitualAsync(today);
        Assert.NotNull(ritual2);
        Assert.Equal("2026-10-31", ritual2.LogicalDate);
        Assert.Equal(initialVideoId, ritual2.VideoId);

        // Third call at 23:30 same logical date
        var laterToday = new DateTime(2026, 10, 31, 23, 30, 0);
        var ritual3 = await _service.GetOrAssignDailyRitualAsync(laterToday);
        Assert.NotNull(ritual3);
        Assert.Equal(initialVideoId, ritual3.VideoId);
    }

    [Fact]
    public async Task GetOrAssignDailyRitualAsync_WhenAssignedFileDeleted_ReplacesWithNextVideoFromQueue()
    {
        // Arrange: 2 videos
        string file1 = Path.Combine(_testVideosDir, "video_first.mp4");
        string file2 = Path.Combine(_testVideosDir, "video_second.mp4");
        await File.WriteAllTextAsync(file1, "content-first");
        await File.WriteAllTextAsync(file2, "content-second");
        await _videoLibraryService.ScanLibraryAsync();

        var date = new DateTime(2026, 10, 31, 15, 0, 0);

        // 1. Assign video for today
        var initialRitual = await _service.GetOrAssignDailyRitualAsync(date);
        Assert.NotNull(initialRitual);
        var initialVideo = await _videoLibraryService.GetVideoByIdAsync(initialRitual.VideoId);
        Assert.NotNull(initialVideo);

        // 2. Delete the assigned video file from disk
        File.Delete(initialVideo.FilePath);

        // 3. Act: Request daily ritual again on the same date
        var replacedRitual = await _service.GetOrAssignDailyRitualAsync(date);

        // Assert: Replaced seamlessly with the remaining valid video
        Assert.NotNull(replacedRitual);
        Assert.NotEqual(initialVideo.Id, replacedRitual.VideoId);
        Assert.Equal("2026-10-31", replacedRitual.LogicalDate);
        Assert.Equal(0, replacedRitual.LastPositionMs);
        Assert.False(replacedRitual.IsCompleted);

        // The deleted video is marked IsMissing
        var refreshedOldVideo = await _videoLibraryService.GetVideoByIdAsync(initialVideo.Id);
        Assert.NotNull(refreshedOldVideo);
        Assert.True(refreshedOldVideo.IsMissing);
    }

    [Fact]
    public async Task TriggerDayTransitionAsync_PublishesMessageAndAssignsNewRitual()
    {
        // Arrange: 2 videos
        await File.WriteAllTextAsync(Path.Combine(_testVideosDir, "v1.mp4"), "data1");
        await File.WriteAllTextAsync(Path.Combine(_testVideosDir, "v2.mp4"), "data2");
        await _videoLibraryService.ScanLibraryAsync();

        var receivedMessages = new List<DayChangedMessage>();
        _notificationBus.Subscribe<DayChangedMessage>(this, msg => receivedMessages.Add(msg));

        var day1 = new DateTime(2026, 10, 31, 12, 0, 0);
        await _service.GetOrAssignDailyRitualAsync(day1);

        // Act: Transition to next day at 04:00 AM on Nov 1
        var day2 = new DateTime(2026, 11, 1, 4, 0, 1);
        await _service.TriggerDayTransitionAsync(day2);

        // Assert
        Assert.Single(receivedMessages);
        Assert.Equal("2026-10-31", receivedMessages[0].PreviousLogicalDate);
        Assert.Equal("2026-11-01", receivedMessages[0].NewLogicalDate);

        var assignedNov1 = await _dailyRitualRepository.GetDailyRitualLogAsync("2026-11-01");
        Assert.NotNull(assignedNov1);
        Assert.Equal("2026-11-01", assignedNov1.LogicalDate);
    }
}
