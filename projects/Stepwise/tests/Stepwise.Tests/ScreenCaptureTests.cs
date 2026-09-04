using Stepwise.Core.Models;
using Stepwise.WindowsIntegration.Capture;
using Xunit;

namespace Stepwise.Tests;

public class ScreenCaptureTests : IDisposable
{
    private readonly string _testTempDir;

    public ScreenCaptureTests()
    {
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
