using PersonalProvocateur.App.Models;
using PersonalProvocateur.App.Storage;
using PersonalProvocateur.App.Services;
using Xunit;

namespace PersonalProvocateur.Tests;

public class VideoLibraryServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _settingsFile;
    private readonly SettingsStore _settingsStore;

    public VideoLibraryServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "VideoLibraryTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _settingsFile = Path.Combine(_tempDirectory, "settings.json");
        _settingsStore = new SettingsStore(_settingsFile);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore test cleanup exceptions
        }
    }

    [Fact]
    public void AddVideo_AddsToSettingsAndReturnsItem()
    {
        var service = new VideoLibraryService(_settingsStore, fileExistsChecker: _ => true);

        var video = service.AddVideo(@"C:\Videos\Motivation_1.mp4", "First Clip");

        Assert.NotNull(video);
        Assert.Equal("First Clip", video.Title);
        Assert.Equal(@"C:\Videos\Motivation_1.mp4", video.FilePath);
        Assert.True(video.IsEnabled);

        var all = service.GetAllVideos();
        Assert.Single(all);
        Assert.Equal(video.Id, all[0].Id);
        Assert.Equal(1, service.ShuffleBag.PoolCount);
    }

    [Fact]
    public void AddVideo_DefaultsTitleToFilenameWithoutExtension()
    {
        var service = new VideoLibraryService(_settingsStore, fileExistsChecker: _ => true);

        var video = service.AddVideo(@"C:\Videos\awesome_routine.mp4");

        Assert.Equal("awesome_routine", video.Title);
    }

    [Fact]
    public void RemoveVideo_RemovesFromSettingsAndPool()
    {
        var service = new VideoLibraryService(_settingsStore, fileExistsChecker: _ => true);

        var video = service.AddVideo(@"C:\Videos\video1.mp4");
        Assert.Single(service.GetAllVideos());

        var removed = service.RemoveVideo(video.Id);
        Assert.True(removed);
        Assert.Empty(service.GetAllVideos());
        Assert.Equal(0, service.ShuffleBag.PoolCount);
    }

    [Fact]
    public void ToggleVideo_EnablesAndDisablesVideoInPool()
    {
        var service = new VideoLibraryService(_settingsStore, fileExistsChecker: _ => true);

        var video = service.AddVideo(@"C:\Videos\video1.mp4");
        Assert.Equal(1, service.ShuffleBag.PoolCount);

        var toggledOff = service.ToggleVideo(video.Id, false);
        Assert.True(toggledOff);
        Assert.False(service.GetAllVideos()[0].IsEnabled);
        Assert.Equal(0, service.ShuffleBag.PoolCount);
        Assert.Empty(service.GetAvailableVideos());

        var toggledOn = service.ToggleVideo(video.Id, true);
        Assert.True(toggledOn);
        Assert.True(service.GetAllVideos()[0].IsEnabled);
        Assert.Equal(1, service.ShuffleBag.PoolCount);
        Assert.Single(service.GetAvailableVideos());
    }

    [Fact]
    public void GetNextVideo_DelegatesToShuffleBag()
    {
        var service = new VideoLibraryService(_settingsStore, fileExistsChecker: _ => true);
        service.AddVideo(@"C:\Videos\video1.mp4", "V1");

        var next = service.GetNextVideo();
        Assert.NotNull(next);
        Assert.Equal("V1", next.Title);
    }

    [Fact]
    public void AddTextItem_ValidText_AddsToSettingsAndPool()
    {
        var service = new VideoLibraryService(_settingsStore, fileExistsChecker: _ => true);

        var item = service.AddTextItem("  Сделай разминку для глаз!  ");

        Assert.NotNull(item);
        Assert.Equal("Сделай разминку для глаз!", item.Title);
        Assert.Equal("Сделай разминку для глаз!", item.TextContent);
        Assert.Equal(ProvocationContentType.Text, item.ContentType);
        Assert.Equal(string.Empty, item.FilePath);
        Assert.True(item.IsEnabled);

        var all = service.GetAllVideos();
        Assert.Single(all);
        Assert.Equal(item.Id, all[0].Id);
        Assert.Equal(1, service.ShuffleBag.PoolCount);

        var available = service.GetAvailableVideos();
        Assert.Single(available);
        Assert.Equal(item.Id, available[0].Id);
    }

    [Fact]
    public void AddTextItem_LongText_TruncatesTitleTo37CharsWithEllipsis()
    {
        var service = new VideoLibraryService(_settingsStore, fileExistsChecker: _ => true);
        var longText = "Это очень длинный текст напоминания, который превышает сорок символов значительно";

        var item = service.AddTextItem(longText);

        Assert.NotNull(item);
        Assert.Equal(longText.Substring(0, 37) + "...", item.Title);
        Assert.Equal(40, item.Title.Length);
        Assert.Equal(longText, item.TextContent);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddTextItem_NullOrWhitespace_ThrowsArgumentException(string? invalidText)
    {
        var service = new VideoLibraryService(_settingsStore, fileExistsChecker: _ => true);

        Assert.Throws<ArgumentException>(() => service.AddTextItem(invalidText!));
    }
}
