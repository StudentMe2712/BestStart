using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Stepwise.WindowsIntegration.Capture;
using Xunit;

namespace Stepwise.Tests;

[Collection("GoldenGuiE2ETestsCollection")]
public class ScreenCaptureTests : IDisposable
{
    private readonly string _testTempDir;

    [DllImport("user32.dll")]
    private static extern uint GetGuiResources(nint hProcess, uint uiFlags);

    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentProcess();

    private const uint GR_GDIOBJECTS = 0;

    private readonly Xunit.Abstractions.ITestOutputHelper? _output;

    public ScreenCaptureTests(Xunit.Abstractions.ITestOutputHelper? output = null)
    {
        _output = output;
        _testTempDir = Path.Combine(Path.GetTempPath(), "StepwiseTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testTempDir);
    }

    [Fact]
    public void ScreenCaptureService_Capture_CreatesPngFileOnDiskAndReturnsRelativePath()
    {
        // Arrange
        var captureService = new ScreenCaptureService();
        var region = new BoundingBox(10, 10, 300, 200);

        // Act
        var relativePath = captureService.Capture(_testTempDir, sequenceIndex: 1, targetRegion: region);

        // Assert
        Assert.NotNull(relativePath);
        Assert.Equal(@"assets/screenshots/step_001.png".Replace('\\', '/'), relativePath.Replace('\\', '/'));

        var fullPath = Path.Combine(_testTempDir, relativePath);
        Assert.True(File.Exists(fullPath), $"Ожидалось, что файл скриншота существует по пути: {fullPath}");

        var fileInfo = new FileInfo(fullPath);
        Assert.True(fileInfo.Length > 0, "Размер скриншота должен быть больше 0 байт.");
    }

    [Fact]
    public void ScreenCaptureService_GdiResourceSafety_SeriesOf55Captures_DoesNotLeakGdiHandles()
    {
        var captureService = new ScreenCaptureService();
        var region = new BoundingBox(10, 10, 200, 150);

        // Warm up GDI+ subsystem and fonts
        captureService.Capture(_testTempDir, sequenceIndex: 0, targetRegion: region);
        GC.Collect();
        GC.WaitForPendingFinalizers();

        uint gdiBefore = GetGuiResources(GetCurrentProcess(), GR_GDIOBJECTS);

        const int iterations = 55;
        for (int i = 1; i <= iterations; i++)
        {
            var path = captureService.Capture(_testTempDir, sequenceIndex: i, targetRegion: region);
            Assert.NotNull(path);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        uint gdiAfter = GetGuiResources(GetCurrentProcess(), GR_GDIOBJECTS);
        _output?.WriteLine($"GDI before: {gdiBefore}, after: {gdiAfter}");

        // Allow at most 2 GDI objects difference due to ambient OS caching, verifying no per-iteration leak
        int gdiDifference = (int)gdiAfter - (int)gdiBefore;
        Assert.True(gdiDifference <= 2, $"Обнаружена утечка GDI дескрипторов после {iterations} захватов: до={gdiBefore}, после={gdiAfter}, дельта={gdiDifference}");
    }

    [Theory]
    [InlineData(-1920, 0, -1920, 0, 0, 0)]      // Верхний левый угол левого монитора
    [InlineData(-1920, 0, -960, 100, 960, 100)]  // Середина левого монитора
    [InlineData(-1920, 0, 0, 0, 1920, 0)]        // Начало основного монитора
    [InlineData(-1920, 0, 100, 200, 2020, 200)]  // Элемент на основном мониторе
    [InlineData(0, -1080, 50, -1080, 50, 0)]     // Верхний монитор с отрицательным Y
    public void ScreenCaptureService_TranslateToBitmapCoordinates_CorrectlyTranslatesNegativeCoordinates(
        int originX, int originY, double elementX, double elementY, double expectedRelX, double expectedRelY)
    {
        var absoluteBox = new BoundingBox(elementX, elementY, 200, 100);
        var translated = ScreenCaptureService.TranslateToBitmapCoordinates(absoluteBox, originX, originY);

        Assert.Equal(expectedRelX, translated.X);
        Assert.Equal(expectedRelY, translated.Y);
        Assert.Equal(200, translated.Width);
        Assert.Equal(100, translated.Height);
    }

    [Fact]
    public void ScreenCaptureService_TranslateToBitmapCoordinates_HandlesSpecialValuesWithoutOverflow()
    {
        var emptyBox = BoundingBox.Empty;
        var resultEmpty = ScreenCaptureService.TranslateToBitmapCoordinates(emptyBox, -1920, 0);
        Assert.True(resultEmpty.IsEmpty);

        var nanBox = new BoundingBox(double.NaN, 100, 200, 100);
        var resultNan = ScreenCaptureService.TranslateToBitmapCoordinates(nanBox, -1920, 0);
        Assert.True(resultNan.IsEmpty);

        var infinityBox = new BoundingBox(double.PositiveInfinity, 100, 200, 100);
        var resultInf = ScreenCaptureService.TranslateToBitmapCoordinates(infinityBox, -1920, 0);
        Assert.True(resultInf.IsEmpty);
    }

    [Fact]
    public void ScreenCaptureService_CaptureAnnotated_WithNegativeCoordinates_SucceedsWithoutCrashing()
    {
        var captureService = new ScreenCaptureService();
        var regionOnLeftMonitor = new BoundingBox(-500, 50, 150, 80);

        var relPath = captureService.CaptureAnnotated(_testTempDir, sequenceIndex: 99, targetRegion: regionOnLeftMonitor);

        Assert.NotNull(relPath);
        var fullPath = Path.Combine(_testTempDir, relPath);
        Assert.True(File.Exists(fullPath));
    }

    [Fact]
    public async Task CaptureCoordinator_FileLocking_DoesNotLockScreenshotFileOnDisk()
    {
        var captureMock = new Mock<IScreenCaptureService>();
        var repoMock = new Mock<IProjectRepository>();
        repoMock.SetupGet(r => r.ProjectRootPath).Returns(_testTempDir);

        var screenshotsDir = Path.Combine(_testTempDir, "assets", "screenshots");
        Directory.CreateDirectory(screenshotsDir);
        var testPng = Path.Combine(screenshotsDir, "step_042.png");
        using (var bmp = new Bitmap(640, 480))
        {
            bmp.Save(testPng, System.Drawing.Imaging.ImageFormat.Png);
        }

        captureMock
            .Setup(c => c.Capture(It.IsAny<string>(), 42, It.IsAny<BoundingBox?>(), It.IsAny<long>()))
            .Returns("assets/screenshots/step_042.png");

        var coordinator = new CaptureCoordinator(captureMock.Object, repoMock.Object);
        var target = new ElementInfo("Btn", "Button", "b1", "Button", "proc", 1, "Win", 0, new BoundingBox(10, 10, 50, 20));

        var result = await coordinator.CaptureStepWithResultAsync(42, target);

        Assert.True(result.Success);
        Assert.Equal(640, result.Width);
        Assert.Equal(480, result.Height);

        // Проверяем, что файл освобожден и может быть открыт с монопольным доступом на запись
        using (var writeStream = new FileStream(testPng, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.True(writeStream.CanWrite);
        }
    }

    [Fact]
    public async Task CaptureCoordinator_CaptureStepWithResultAsync_WhenCancelled_ReturnsFailedResultWithoutUnhandledException()
    {
        var captureMock = new Mock<IScreenCaptureService>();
        var repoMock = new Mock<IProjectRepository>();
        repoMock.SetupGet(r => r.ProjectRootPath).Returns(_testTempDir);

        var coordinator = new CaptureCoordinator(captureMock.Object, repoMock.Object);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var target = new ElementInfo("Btn", "Button", "b1", "Button", "proc", 1, "Win", 0, new BoundingBox(10, 10, 50, 20));
        var result = await coordinator.CaptureStepWithResultAsync(1, target, cts.Token);

        Assert.False(result.Success);
        Assert.Null(result.RelativePath);
        Assert.Contains("cancel", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        var path = await coordinator.CaptureStepAsync(1, target, cts.Token);
        Assert.Null(path);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testTempDir))
            {
                Directory.Delete(_testTempDir, true);
            }
        }
        catch
        {
            // Игнорируем ошибки очистки временных файлов
        }
    }
}
