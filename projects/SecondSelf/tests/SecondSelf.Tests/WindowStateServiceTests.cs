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

    [Fact]
    public void EnsureSafePosition_WhenMonitorDisconnected_MovesToPrimaryWorkArea()
    {
        // Given: Активен только один Primary монитор 1920x1080
        var activeMonitors = new[]
        {
            new DisplayArea(new Windows.Graphics.RectInt32(0, 0, 1920, 1080), isPrimary: true)
        };

        // When: Виджет находился на втором (ныне отключенном) мониторе справа: X=2500, Y=100
        var disconnectedPosition = new WindowPosition(2500, 100, 380, 680);
        var safePosition = _service.EnsureSafePosition(disconnectedPosition, activeMonitors);

        // Then: Виджет перемещен в границы Primary монитора с безопасным отступом 24px от правого края:
        // MaxX = 1920 - 380 - 24 = 1516
        // Y = 100 находится в безопасных пределах [24 .. 1080 - 680 - 24 = 376]
        Assert.Equal(1516, safePosition.X);
        Assert.Equal(100, safePosition.Y);
        Assert.Equal(380, safePosition.Width);
        Assert.Equal(680, safePosition.Height);
    }

    [Fact]
    public void EnsureSafePosition_WhenDisconnectedMonitorOnLeft_MovesToPrimaryLeftWorkArea()
    {
        // Given: Primary монитор 1920x1080
        var activeMonitors = new[]
        {
            new DisplayArea(new Windows.Graphics.RectInt32(0, 0, 1920, 1080), isPrimary: true)
        };

        // When: Виджет был на отключенном левом мониторе X=-1200, Y=-50
        var disconnectedPosition = new WindowPosition(-1200, -50, 360, 240);
        var safePosition = _service.EnsureSafePosition(disconnectedPosition, activeMonitors);

        // Then: Виджет прижат к безопасному отступу 24px слева и сверху
        Assert.Equal(24, safePosition.X);
        Assert.Equal(24, safePosition.Y);
        Assert.Equal(360, safePosition.Width);
        Assert.Equal(240, safePosition.Height);
    }

    [Fact]
    public void EnsureSafePosition_WhenIntersectingActiveMonitor_PreservesPosition()
    {
        // Given: Два активных монитора (Primary 1920x1080 и Secondary справа 1920x1080)
        var activeMonitors = new[]
        {
            new DisplayArea(new Windows.Graphics.RectInt32(0, 0, 1920, 1080), isPrimary: true),
            new DisplayArea(new Windows.Graphics.RectInt32(1920, 0, 1920, 1080), isPrimary: false)
        };

        // When: Виджет находится на втором активном мониторе (X=2000, Y=100)
        var currentPosition = new WindowPosition(2000, 100, 380, 680);
        var safePosition = _service.EnsureSafePosition(currentPosition, activeMonitors);

        // Then: Позиция не изменяется, так как виджет пересекается с активным дисплеем
        Assert.Equal(2000, safePosition.X);
        Assert.Equal(100, safePosition.Y);
        Assert.Equal(380, safePosition.Width);
        Assert.Equal(680, safePosition.Height);
    }
}
