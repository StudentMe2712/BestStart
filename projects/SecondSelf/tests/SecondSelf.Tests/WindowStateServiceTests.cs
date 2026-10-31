using System;
using System.IO;
using System.Threading.Tasks;
using SecondSelf.Core.Services;
using Xunit;

namespace SecondSelf.Tests;

public sealed class WindowStateServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _settingsFilePath;
    private readonly WindowStateService _service;

    public WindowStateServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "SecondSelfTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _settingsFilePath = Path.Combine(_tempDirectory, "Settings", "window_state.json");
        _service = new WindowStateService(_settingsFilePath);
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
    public async Task LoadStateAsync_WhenFileDoesNotExist_ReturnsDefaultState()
    {
        var state = await _service.LoadStateAsync();

        Assert.NotNull(state);
        Assert.Equal(0.85, state.NormalizedX, 2);
        Assert.Equal(0.05, state.NormalizedY, 2);
        Assert.Equal("Primary", state.DisplayId);
        Assert.Equal(1.0, state.DpiScale);
        Assert.False(state.IsExpanded);
    }

    [Fact]
    public async Task SaveStateAsync_AndLoadStateAsync_RoundTripsSuccessfully()
    {
        var originalState = new WindowState
        {
            NormalizedX = 0.72,
            NormalizedY = 0.15,
            DisplayId = "MONITOR-2",
            DpiScale = 1.25,
            IsExpanded = true
        };

        await _service.SaveStateAsync(originalState);
        Assert.True(File.Exists(_settingsFilePath));

        var loadedState = await _service.LoadStateAsync();

        Assert.NotNull(loadedState);
        Assert.Equal(0.72, loadedState.NormalizedX, 2);
        Assert.Equal(0.15, loadedState.NormalizedY, 2);
        Assert.Equal("MONITOR-2", loadedState.DisplayId);
        Assert.Equal(1.25, loadedState.DpiScale, 2);
        Assert.True(loadedState.IsExpanded);
    }

    [Fact]
    public async Task LoadStateAsync_WhenFileIsCorrupted_ReturnsDefaultStateGracefully()
    {
        var dir = Path.GetDirectoryName(_settingsFilePath)!;
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(_settingsFilePath, "{ invalid json text }");

        var state = await _service.LoadStateAsync();

        Assert.NotNull(state);
        Assert.Equal(0.85, state.NormalizedX, 2);
        Assert.Equal(0.05, state.NormalizedY, 2);
    }

    [Fact]
    public void CalculateWindowPosition_WithinWorkArea_ComputesCorrectCoordinates()
    {
        var state = new WindowState
        {
            NormalizedX = 0.5,
            NormalizedY = 0.5
        };

        // Work area 1920x1080 at (0, 0), window 360x240
        // maxOffsetX = 1920 - 360 = 1560 -> 0.5 * 1560 = 780
        // maxOffsetY = 1080 - 240 = 840 -> 0.5 * 840 = 420
        var (x, y) = _service.CalculateWindowPosition(
            state,
            windowWidth: 360,
            windowHeight: 240,
            workAreaX: 0,
            workAreaY: 0,
            workAreaWidth: 1920,
            workAreaHeight: 1080);

        Assert.Equal(780, x);
        Assert.Equal(420, y);
    }

    [Fact]
    public void CalculateWindowPosition_WithNonZeroWorkAreaOrigin_OffSetsCorrectly()
    {
        var state = new WindowState
        {
            NormalizedX = 1.0,
            NormalizedY = 0.0
        };

        // Second monitor at (1920, 0), work area 1920x1080, window 360x240
        var (x, y) = _service.CalculateWindowPosition(
            state,
            windowWidth: 360,
            windowHeight: 240,
            workAreaX: 1920,
            workAreaY: 40,
            workAreaWidth: 1920,
            workAreaHeight: 1040);

        // Top right: X should be 1920 + (1920 - 360) = 3480
        // Y should be 40 + 0 = 40
        Assert.Equal(3480, x);
        Assert.Equal(40, y);
    }

    [Fact]
    public void CalculateWindowPosition_WhenWindowLargerThanWorkArea_ClampsToWorkAreaOrigin()
    {
        var state = new WindowState
        {
            NormalizedX = 0.9,
            NormalizedY = 0.9
        };

        var (x, y) = _service.CalculateWindowPosition(
            state,
            windowWidth: 1000,
            windowHeight: 800,
            workAreaX: 100,
            workAreaY: 50,
            workAreaWidth: 800,
            workAreaHeight: 600);

        Assert.Equal(100, x);
        Assert.Equal(50, y);
    }

    [Fact]
    public void RoundTrip_CoordinatesAndNormalization_PreservesPosition()
    {
        int workAreaX = 0;
        int workAreaY = 0;
        int workAreaW = 2560;
        int workAreaH = 1440;
        int winW = 380;
        int winH = 680;

        var originalState = new WindowState { NormalizedX = 0.85, NormalizedY = 0.10 };

        var (x, y) = _service.CalculateWindowPosition(originalState, winW, winH, workAreaX, workAreaY, workAreaW, workAreaH);
        var (normX, normY) = _service.CalculateNormalizedCoordinates(x, y, winW, winH, workAreaX, workAreaY, workAreaW, workAreaH);

        Assert.Equal(originalState.NormalizedX, normX, 2);
        Assert.Equal(originalState.NormalizedY, normY, 2);
    }
}
