using System.IO;
using PersonalProvocateur.App.Services;
using Xunit;

namespace PersonalProvocateur.Tests;

public class VideoThumbnailServiceTests : IDisposable
{
    private readonly string _tempThumbDir;

    public VideoThumbnailServiceTests()
    {
        _tempThumbDir = Path.Combine(Path.GetTempPath(), "pp_thumb_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempThumbDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempThumbDir))
            {
                Directory.Delete(_tempThumbDir, true);
            }
        }
        catch
        {
            // Suppress cleanup exceptions
        }
    }

    [Fact]
    public void ProcessVideo_GeneratesThumbnail_InSpecifiedDirectory()
    {
        var service = new VideoThumbnailService(
            thumbnailsDirectory: _tempThumbDir,
            durationExtractor: _ => TimeSpan.FromMinutes(2).Add(TimeSpan.FromSeconds(15)),
            fileExistsChecker: _ => true);

        var videoId = Guid.NewGuid();
        var metadata = service.ProcessVideo(@"C:\Videos\demo_presentation.mp4", videoId);

        Assert.NotNull(metadata);
        Assert.NotEmpty(metadata.ThumbnailPath);
        Assert.True(File.Exists(metadata.ThumbnailPath), "Thumbnail file must exist on disk.");
        Assert.True(new FileInfo(metadata.ThumbnailPath).Length > 0, "Thumbnail file must not be empty.");
        Assert.Equal("02:15", metadata.DurationText);
        Assert.Contains(videoId.ToString(), metadata.ThumbnailPath);
    }

    [Fact]
    public void ProcessVideo_UsesCachedThumbnail_WhenFileAlreadyExists()
    {
        var service = new VideoThumbnailService(
            thumbnailsDirectory: _tempThumbDir,
            durationExtractor: _ => TimeSpan.FromSeconds(90),
            fileExistsChecker: _ => true);

        var videoId = Guid.NewGuid();

        // 1. First run generates thumbnail
        var firstResult = service.ProcessVideo(@"C:\Videos\first.mp4", videoId);
        Assert.True(File.Exists(firstResult.ThumbnailPath));

        var initialWriteTime = File.GetLastWriteTimeUtc(firstResult.ThumbnailPath);

        // 2. Second run should reuse existing thumbnail
        var secondResult = service.ProcessVideo(@"C:\Videos\first.mp4", videoId);

        Assert.Equal(firstResult.ThumbnailPath, secondResult.ThumbnailPath);
        Assert.Equal(initialWriteTime, File.GetLastWriteTimeUtc(secondResult.ThumbnailPath));
    }

    [Fact]
    public void ProcessVideo_GracefulFallback_OnMissingOrCorruptFile()
    {
        var service = new VideoThumbnailService(
            thumbnailsDirectory: _tempThumbDir,
            durationExtractor: null,
            fileExistsChecker: _ => false); // Simulates missing file

        var videoId = Guid.NewGuid();
        var metadata = service.ProcessVideo(@"C:\NonExistent\broken.mp4", videoId);

        Assert.NotNull(metadata);
        Assert.True(File.Exists(metadata.ThumbnailPath), "Procedural fallback should create thumbnail even for missing files.");
        Assert.True(new FileInfo(metadata.ThumbnailPath).Length > 0);
        Assert.Equal("01:30", metadata.DurationText); // Fallback duration
    }

    [Theory]
    [InlineData(0, "01:30")]
    [InlineData(-5, "01:30")]
    [InlineData(45, "00:45")]
    [InlineData(90, "01:30")]
    [InlineData(125, "02:05")]
    [InlineData(3665, "01:01:05")]
    public void FormatDuration_FormatsExpectedStrings(int totalSeconds, string expectedText)
    {
        var duration = TimeSpan.FromSeconds(totalSeconds);
        var formatted = VideoThumbnailService.FormatDuration(duration);

        Assert.Equal(expectedText, formatted);
    }

    [Fact]
    public async Task ProcessVideoAsync_ExecutesAsynchronouslyAndReturnsValidMetadata()
    {
        var service = new VideoThumbnailService(
            thumbnailsDirectory: _tempThumbDir,
            durationExtractor: _ => TimeSpan.FromMinutes(3),
            fileExistsChecker: _ => true);

        var videoId = Guid.NewGuid();
        var metadata = await service.ProcessVideoAsync(@"C:\Videos\async_test.mp4", videoId);

        Assert.NotNull(metadata);
        Assert.True(File.Exists(metadata.ThumbnailPath));
        Assert.Equal("03:00", metadata.DurationText);
    }
}
