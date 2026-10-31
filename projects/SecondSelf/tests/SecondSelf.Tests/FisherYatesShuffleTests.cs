using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Core.Services;
using SecondSelf.Data;
using SecondSelf.Data.Migrations;
using Xunit;

namespace SecondSelf.Tests;

public class FisherYatesShuffleTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _testVideosDir;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly VideoLibraryService _videoLibraryService;
    private readonly FisherYatesShuffleService _shuffleService;

    public FisherYatesShuffleTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"secondself_shuffle_{Guid.NewGuid():N}.db");
        _testVideosDir = Path.Combine(Path.GetTempPath(), $"secondself_shufflevideos_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testVideosDir);

        _connectionFactory = new SqliteConnectionFactory(_testDbPath);
        new DatabaseMigrationService(_connectionFactory).MigrateAsync().GetAwaiter().GetResult();
        _videoLibraryService = new VideoLibraryService(_connectionFactory, _testVideosDir);
        _shuffleService = new FisherYatesShuffleService(_connectionFactory, _videoLibraryService);
    }

    public void Dispose()
    {
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
    public async Task GetNextVideoInCycleAsync_WhenZeroVideos_ReturnsNullWithoutCrashing()
    {
        // Act
        var result = await _shuffleService.GetNextVideoInCycleAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetNextVideoInCycleAsync_WhenOneVideo_ReturnsSameVideoAcrossMultipleCycles()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testVideosDir, "single.mp4"), "data");
        await _videoLibraryService.ScanLibraryAsync();

        // Act & Assert
        // Cycle 1
        var v1 = await _shuffleService.GetNextVideoInCycleAsync();
        Assert.NotNull(v1);
        Assert.Equal("single.mp4", v1.FileName);

        // Cycle 2
        var v2 = await _shuffleService.GetNextVideoInCycleAsync();
        Assert.NotNull(v2);
        Assert.Equal("single.mp4", v2.FileName);

        // Cycle 3
        var v3 = await _shuffleService.GetNextVideoInCycleAsync();
        Assert.NotNull(v3);
        Assert.Equal("single.mp4", v3.FileName);
    }

    [Fact]
    public async Task GetNextVideoInCycleAsync_WhenTwoVideos_AlternatesWithoutConsecutiveRepetition()
    {
        // Arrange: 2 videos
        await File.WriteAllTextAsync(Path.Combine(_testVideosDir, "video_A.mp4"), "dataA");
        await File.WriteAllTextAsync(Path.Combine(_testVideosDir, "video_B.mp4"), "dataB");
        await _videoLibraryService.ScanLibraryAsync();

        var history = new List<string>();

        // Act: Run through 10 iterations (5 cycles of 2 videos)
        for (int i = 0; i < 10; i++)
        {
            var v = await _shuffleService.GetNextVideoInCycleAsync();
            Assert.NotNull(v);
            history.Add(v.FileName);
        }

        // Assert: Never consecutive duplicates!
        for (int i = 0; i < history.Count - 1; i++)
        {
            Assert.NotEqual(history[i], history[i + 1]);
        }
    }

    [Fact]
    public async Task GetNextVideoInCycleAsync_WhenFiveVideos_PlaysEachExactlyOncePerCycle()
    {
        // Arrange: 5 videos
        for (int i = 1; i <= 5; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(_testVideosDir, $"v{i}.mp4"), $"content-{i}");
        }
        await _videoLibraryService.ScanLibraryAsync();

        // Cycle 1: 5 calls must return each of the 5 videos exactly once
        var cycle1 = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            var v = await _shuffleService.GetNextVideoInCycleAsync();
            Assert.NotNull(v);
            cycle1.Add(v.FileName);
        }

        Assert.Equal(5, cycle1.Distinct().Count());

        // Cycle 2: Next 5 calls must also return each of the 5 videos exactly once
        var cycle2 = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            var v = await _shuffleService.GetNextVideoInCycleAsync();
            Assert.NotNull(v);
            cycle2.Add(v.FileName);
        }

        Assert.Equal(5, cycle2.Distinct().Count());

        // The first video of Cycle 2 should not be the same as the last video of Cycle 1
        Assert.NotEqual(cycle1[^1], cycle2[0]);
    }

    [Fact]
    public async Task GetNextVideoInCycleAsync_WhenFileMissingOnDisk_MarksIsMissingAndSeamlesslyReturnsNext()
    {
        // Arrange: 2 videos
        string path1 = Path.Combine(_testVideosDir, "broken.mp4");
        string path2 = Path.Combine(_testVideosDir, "valid.mp4");
        await File.WriteAllTextAsync(path1, "content1");
        await File.WriteAllTextAsync(path2, "content2");
        await _videoLibraryService.ScanLibraryAsync();

        // Delete broken.mp4 directly on disk
        File.Delete(path1);

        // Act
        var chosen = await _shuffleService.GetNextVideoInCycleAsync();

        // Assert: valid.mp4 is returned
        Assert.NotNull(chosen);
        Assert.Equal("valid.mp4", chosen.FileName);

        // broken.mp4 is marked IsMissing in the database
        var allVideos = await _videoLibraryService.GetAllVideosAsync();
        var brokenVideo = allVideos.FirstOrDefault(v => v.FileName == "broken.mp4");
        Assert.NotNull(brokenVideo);
        Assert.True(brokenVideo.IsMissing);
    }
}
