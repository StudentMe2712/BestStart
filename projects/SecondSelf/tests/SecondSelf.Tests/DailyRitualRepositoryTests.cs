using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Data;
using SecondSelf.Data.Entities;
using SecondSelf.Data.Migrations;
using SecondSelf.Data.Repositories;
using Xunit;

namespace SecondSelf.Tests;

public class DailyRitualRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _factory;
    private readonly DailyRitualRepository _repository;

    public DailyRitualRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"secondself_rituals_{Guid.NewGuid():N}.db");
        _factory = new SqliteConnectionFactory(_testDbPath);
        new DatabaseMigrationService(_factory).MigrateAsync().GetAwaiter().GetResult();
        _repository = new DailyRitualRepository(_factory);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }

    [Fact]
    public async Task UpsertAndGetDailyRitualLog_SavesAndRetrievesLog()
    {
        const string videoId = "vid-1";
        await InsertVideoAsync(videoId, "path/video1.mp4");

        var log = new DailyRitualLog
        {
            LogicalDate = "2026-10-31",
            VideoId = videoId,
            LastPositionMs = 45000,
            IsCompleted = false
        };

        await _repository.UpsertDailyRitualLogAsync(log);

        var retrieved = await _repository.GetDailyRitualLogAsync("2026-10-31");
        Assert.NotNull(retrieved);
        Assert.Equal("2026-10-31", retrieved.LogicalDate);
        Assert.Equal(videoId, retrieved.VideoId);
        Assert.Equal(45000, retrieved.LastPositionMs);
        Assert.False(retrieved.IsCompleted);

        // Update position (Upsert update)
        log.LastPositionMs = 50000;
        await _repository.UpsertDailyRitualLogAsync(log);

        var updated = await _repository.GetDailyRitualLogAsync("2026-10-31");
        Assert.NotNull(updated);
        Assert.Equal(50000, updated.LastPositionMs);

        // Update position via dedicated UpdateLastPositionAsync method
        await _repository.UpdateLastPositionAsync("2026-10-31", 65000);
        var positionUpdated = await _repository.GetDailyRitualLogAsync("2026-10-31");
        Assert.NotNull(positionUpdated);
        Assert.Equal(65000, positionUpdated.LastPositionMs);
    }

    [Fact]
    public async Task MarkCompleted_FiresTriggerAndIncrementsPlaybackCount()
    {
        const string videoId = "vid-trigger";
        await InsertVideoAsync(videoId, "path/video_trigger.mp4");

        var log = new DailyRitualLog
        {
            LogicalDate = "2026-10-31",
            VideoId = videoId,
            LastPositionMs = 120000,
            IsCompleted = false
        };
        await _repository.UpsertDailyRitualLogAsync(log);

        // Mark completed
        string completedAt = "2026-10-31T15:30:00Z";
        await _repository.MarkCompletedAsync("2026-10-31", completedAt);

        var updatedLog = await _repository.GetDailyRitualLogAsync("2026-10-31");
        Assert.NotNull(updatedLog);
        Assert.True(updatedLog.IsCompleted);
        Assert.Equal(completedAt, updatedLog.CompletedAt);

        // Verify trigger TR_DailyRitualLogs_IncrementPlayback incremented PlaybackCount in VideoLibrary
        await using var conn = await _factory.OpenConnectionAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT PlaybackCount FROM VideoLibrary WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", videoId);
        int playbackCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        Assert.Equal(1, playbackCount);
    }

    [Fact]
    public async Task GetLogsForMonth_ReturnsAllDaysInMonth()
    {
        await InsertVideoAsync("vid-m", "path/vid_m.mp4");

        await _repository.UpsertDailyRitualLogAsync(new DailyRitualLog { LogicalDate = "2026-10-01", VideoId = "vid-m" });
        await _repository.UpsertDailyRitualLogAsync(new DailyRitualLog { LogicalDate = "2026-10-15", VideoId = "vid-m" });
        await _repository.UpsertDailyRitualLogAsync(new DailyRitualLog { LogicalDate = "2026-11-01", VideoId = "vid-m" });

        var octLogs = await _repository.GetLogsForMonthAsync("2026-10");
        Assert.Equal(2, octLogs.Count);
        Assert.Equal("2026-10-01", octLogs[0].LogicalDate);
        Assert.Equal("2026-10-15", octLogs[1].LogicalDate);
    }

    private async Task InsertVideoAsync(string id, string path)
    {
        await using var conn = await _factory.OpenConnectionAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO VideoLibrary (Id, FilePath, FileName, DurationSeconds, PlaybackCount, IsMissing, LastVerifiedAt)
VALUES (@Id, @FilePath, @FileName, 120.0, 0, 0, @LastVerifiedAt);";
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@FilePath", path);
        cmd.Parameters.AddWithValue("@FileName", Path.GetFileName(path));
        cmd.Parameters.AddWithValue("@LastVerifiedAt", DateTime.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }
}
