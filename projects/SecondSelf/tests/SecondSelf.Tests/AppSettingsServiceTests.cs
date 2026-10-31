using System;
using System.IO;
using System.Threading.Tasks;
using SecondSelf.Core.Models;
using SecondSelf.Core.Services;
using Xunit;

namespace SecondSelf.Tests;

public sealed class AppSettingsServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _settingsFilePath;
    private readonly AppSettingsService _service;

    public AppSettingsServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "SecondSelfSettingsTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _settingsFilePath = Path.Combine(_tempDirectory, "Settings", "app_settings.json");
        _service = new AppSettingsService(_settingsFilePath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public async Task GetSettingsAsync_WhenFileDoesNotExist_ReturnsDefaults()
    {
        var settings = await _service.GetSettingsAsync();

        Assert.NotNull(settings);
        Assert.Equal(0.30, settings.DailyWeight, 2);
        Assert.Equal(0.70, settings.WeeklyWeight, 2);
        Assert.True(settings.AlwaysOnTop);
        Assert.True(settings.AutoCollapse);
        Assert.Equal(0.85, settings.GlassOpacity, 2);
        Assert.Equal("Primary", settings.PreferredMonitor);
    }

    [Theory]
    [InlineData(0.10, 0.90)]
    [InlineData(0.25, 0.75)]
    [InlineData(0.30, 0.70)]
    [InlineData(0.50, 0.50)]
    public async Task SaveSettingsAsync_ValidWeights_CalculatesWeeklyWeightAndSaves(double dailyWeight, double expectedWeeklyWeight)
    {
        var settings = new AppSettings
        {
            DailyWeight = dailyWeight,
            AlwaysOnTop = false,
            AutoCollapse = false,
            GlassOpacity = 0.90,
            PreferredMonitor = "Display_2"
        };

        await _service.SaveSettingsAsync(settings);

        var loaded = await _service.GetSettingsAsync();
        Assert.Equal(dailyWeight, loaded.DailyWeight, 2);
        Assert.Equal(expectedWeeklyWeight, loaded.WeeklyWeight, 2);
        Assert.False(loaded.AlwaysOnTop);
        Assert.False(loaded.AutoCollapse);
        Assert.Equal(0.90, loaded.GlassOpacity, 2);
        Assert.Equal("Display_2", loaded.PreferredMonitor);
    }

    [Theory]
    [InlineData(0.09)]
    [InlineData(0.00)]
    [InlineData(-0.10)]
    [InlineData(0.51)]
    [InlineData(0.80)]
    [InlineData(1.00)]
    public async Task SaveSettingsAsync_InvalidDailyWeight_ThrowsArgumentOutOfRangeException(double invalidDailyWeight)
    {
        var settings = new AppSettings
        {
            DailyWeight = invalidDailyWeight
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.SaveSettingsAsync(settings));
    }

    [Fact]
    public async Task UpdateWeightsAsync_UpdatesDailyAndWeeklyWeights()
    {
        var result = await _service.UpdateWeightsAsync(0.40);

        Assert.Equal(0.40, result.DailyWeight, 2);
        Assert.Equal(0.60, result.WeeklyWeight, 2);

        var current = _service.CurrentSettings;
        Assert.Equal(0.40, current.DailyWeight, 2);
        Assert.Equal(0.60, current.WeeklyWeight, 2);
    }

    [Fact]
    public async Task Settings_PersistAcrossServiceInstances()
    {
        var initialSettings = new AppSettings
        {
            DailyWeight = 0.35,
            AlwaysOnTop = false,
            GlassOpacity = 0.75,
            PreferredMonitor = "Secondary_01"
        };
        await _service.SaveSettingsAsync(initialSettings);

        // Создаем новый экземпляр сервиса с тем же файлом настроек
        var newServiceInstance = new AppSettingsService(_settingsFilePath);
        var loaded = await newServiceInstance.GetSettingsAsync();

        Assert.NotNull(loaded);
        Assert.Equal(0.35, loaded.DailyWeight, 2);
        Assert.Equal(0.65, loaded.WeeklyWeight, 2);
        Assert.False(loaded.AlwaysOnTop);
        Assert.Equal(0.75, loaded.GlassOpacity, 2);
        Assert.Equal("Secondary_01", loaded.PreferredMonitor);
    }
}
