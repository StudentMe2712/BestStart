using PersonalProvocateur.App.Models;
using Xunit;

namespace PersonalProvocateur.Tests;

public class AppSettingsTests
{
    [Fact]
    public void AppSettings_DefaultValues_AreCorrect()
    {
        var settings = new AppSettings();

        Assert.Equal(5, settings.MinIntervalMinutes);
        Assert.Equal(20, settings.MaxIntervalMinutes);
        Assert.Equal(0, settings.TargetMonitorIndex);
        Assert.Equal(0.60, settings.DimmingOpacity, 2);
        Assert.Equal("Ctrl+Alt+P", settings.TriggerHotkey);
        Assert.Equal("Ctrl+Alt+Shift+P", settings.PanicHotkey);
        Assert.False(settings.RunOnStartup);
        Assert.False(settings.IsPaused);
        Assert.Equal(0.85, settings.VideoVolume, 2);
        Assert.NotNull(settings.Videos);
        Assert.Empty(settings.Videos);
    }

    [Fact]
    public void Sanitize_ClampsIntervalsAndOpacity()
    {
        var settings = new AppSettings
        {
            MinIntervalMinutes = -10,
            MaxIntervalMinutes = 600,
            TargetMonitorIndex = -5,
            DimmingOpacity = 1.5,
            VideoVolume = -0.5,
            TriggerHotkey = "",
            PanicHotkey = "   "
        };

        settings.Sanitize();

        Assert.Equal(1, settings.MinIntervalMinutes);
        Assert.Equal(480, settings.MaxIntervalMinutes);
        Assert.Equal(0, settings.TargetMonitorIndex);
        Assert.Equal(0.95, settings.DimmingOpacity, 2);
        Assert.Equal(0.0, settings.VideoVolume, 2);
        Assert.Equal("Ctrl+Alt+P", settings.TriggerHotkey);
        Assert.Equal("Ctrl+Alt+Shift+P", settings.PanicHotkey);
    }

    [Fact]
    public void Sanitize_EnsuresMinDoesNotExceedMax()
    {
        var settings = new AppSettings
        {
            MinIntervalMinutes = 50,
            MaxIntervalMinutes = 30
        };

        settings.Sanitize();

        Assert.True(settings.MinIntervalMinutes <= settings.MaxIntervalMinutes);
        Assert.Equal(30, settings.MinIntervalMinutes);
        Assert.Equal(30, settings.MaxIntervalMinutes);
    }

    [Fact]
    public void VideoItem_DefaultValues_AreInitialized()
    {
        var video = new VideoItem
        {
            Title = "Test Video",
            FilePath = @"C:\Videos\test.mp4"
        };

        Assert.NotEqual(Guid.Empty, video.Id);
        Assert.True(video.IsEnabled);
        Assert.Equal(ProvocationContentType.Video, video.ContentType);
        Assert.Equal(string.Empty, video.TextContent);
        Assert.Equal("Test Video", video.Title);
        Assert.Equal(@"C:\Videos\test.mp4", video.FilePath);
        Assert.True(video.AddedAtUtc <= DateTime.UtcNow);
    }

    [Fact]
    public void Enums_HaveExpectedMembers()
    {
        Assert.Equal(5, Enum.GetValues<MascotState>().Length);
        Assert.Equal(4, Enum.GetValues<SessionState>().Length);
        Assert.Equal(2, Enum.GetValues<ProvocationContentType>().Length);
    }
}
