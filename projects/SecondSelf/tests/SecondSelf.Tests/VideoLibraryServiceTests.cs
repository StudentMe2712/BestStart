using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Core.Services;
using SecondSelf.Data;
using SecondSelf.Data.Migrations;
using Xunit;

namespace SecondSelf.Tests;

public class VideoLibraryServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _testVideosDir;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly VideoLibraryService _service;

    public VideoLibraryServiceTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"secondself_videolib_{Guid.NewGuid():N}.db");
        _testVideosDir = Path.Combine(Path.GetTempPath(), $"secondself_videos_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testVideosDir);

        _connectionFactory = new SqliteConnectionFactory(_testDbPath);
        new DatabaseMigrationService(_connectionFactory).MigrateAsync().GetAwaiter().GetResult();
        _service = new VideoLibraryService(_connectionFactory, _testVideosDir);
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
    public async Task ScanLibraryAsync_FindsSupportedExtensions_IgnoresOtherFiles()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testVideosDir, "clip1.mp4"), "dummy-content");
        await File.WriteAllTextAsync(Path.Combine(_testVideosDir, "clip2.mkv"), "dummy-content");
        await File.WriteAllTextAsync(Path.Combine(_testVideosDir, "clip3.webm"), "dummy-content");
        await File.WriteAllTextAsync(Path.Combine(_testVideosDir, "clip4.mov"), "dummy-content");
        await File.WriteAllTextAsync(Path.Combine(_testVideosDir, "notes.txt"), "ignored-text");
        await File.WriteAllTextAsync(Path.Combine(_testVideosDir, "thumb.jpg"), "ignored-image");

        // Act
        var result = await _service.ScanLibraryAsync();

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Contains(result, v => v.FileName == "clip1.mp4" && !v.IsMissing);
        Assert.Contains(result, v => v.FileName == "clip2.mkv" && !v.IsMissing);
        Assert.Contains(result, v => v.FileName == "clip3.webm" && !v.IsMissing);
        Assert.Contains(result, v => v.FileName == "clip4.mov" && !v.IsMissing);
        Assert.DoesNotContain(result, v => v.FileName == "notes.txt");
        Assert.DoesNotContain(result, v => v.FileName == "thumb.jpg");
    }

    [Fact]
    public async Task ScanLibraryAsync_WhenFileDeleted_SetsIsMissingToOne()
    {
        // Arrange
        string filePath = Path.Combine(_testVideosDir, "delete_me.mp4");
        await File.WriteAllTextAsync(filePath, "video-data");
        var initial = await _service.ScanLibraryAsync();
        Assert.Single(initial);
        Assert.False(initial[0].IsMissing);

        // Act: Delete file and scan again
        File.Delete(filePath);
        var afterDelete = await _service.ScanLibraryAsync();

        // Assert
        Assert.Single(afterDelete);
        Assert.True(afterDelete[0].IsMissing);

        var fromDb = await _service.GetVideoByIdAsync(afterDelete[0].Id);
        Assert.NotNull(fromDb);
        Assert.True(fromDb.IsMissing);
    }

    [Fact]
    public async Task ScanLibraryAsync_WhenFileReturns_ResetsIsMissingToZero()
    {
        // Arrange
        string filePath = Path.Combine(_testVideosDir, "return_me.mp4");
        await File.WriteAllTextAsync(filePath, "video-data");
        await _service.ScanLibraryAsync();

        File.Delete(filePath);
        await _service.ScanLibraryAsync();

        // Act: recreate file and rescan
        await File.WriteAllTextAsync(filePath, "restored-video-data");
        var afterRestore = await _service.ScanLibraryAsync();

        // Assert
        Assert.Single(afterRestore);
        Assert.False(afterRestore[0].IsMissing);
    }

    [Fact]
    public async Task GetVideoByIdAsync_ReturnsCorrectVideo()
    {
        string filePath = Path.Combine(_testVideosDir, "search.mp4");
        await File.WriteAllTextAsync(filePath, "searchable-video");
        var scanned = await _service.ScanLibraryAsync();
        var videoId = scanned[0].Id;

        var found = await _service.GetVideoByIdAsync(videoId);
        Assert.NotNull(found);
        Assert.Equal(videoId, found.Id);
        Assert.Equal("search.mp4", found.FileName);
    }
}
