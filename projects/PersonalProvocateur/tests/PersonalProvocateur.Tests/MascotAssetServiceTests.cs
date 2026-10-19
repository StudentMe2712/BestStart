using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Media.Imaging;
using PersonalProvocateur.App.Models;
using PersonalProvocateur.App.Services;
using Xunit;

namespace PersonalProvocateur.Tests;

public class MascotAssetServiceTests : IDisposable
{
    private readonly string _tempDir;

    public MascotAssetServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "pp_mascot_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Suppress cleanup exceptions in tests
        }
    }

    private static void RunInSta(Action action)
    {
        Exception? caught = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (caught != null)
        {
            ExceptionDispatchInfo.Capture(caught).Throw();
        }
    }

    [Fact]
    public void Constructor_WithCustomDirectory_SetsAssetsDirectory()
    {
        var service = new MascotAssetService(_tempDir);
        Assert.Equal(_tempDir, service.AssetsDirectory);
    }

    [Fact]
    public void Constructor_Default_ResolvesNonEmptyDirectory()
    {
        var service = new MascotAssetService();
        Assert.False(string.IsNullOrWhiteSpace(service.AssetsDirectory));
    }

    [Fact]
    public void EnsureAssetsExist_GeneratesAllFourValidPngFiles()
    {
        RunInSta(() =>
        {
            var service = new MascotAssetService(_tempDir);
            service.EnsureAssetsExist();

            var expectedFiles = new[] { "idle.png", "peek.png", "attention.png", "clicked.png" };
            foreach (var filename in expectedFiles)
            {
                var filePath = Path.Combine(_tempDir, filename);
                Assert.True(File.Exists(filePath), $"File {filename} was not generated.");

                var fileInfo = new FileInfo(filePath);
                Assert.True(fileInfo.Length > 0, $"File {filename} is 0 bytes.");

                // Validate PNG image header and dimensions
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var frame = decoder.Frames[0];
                Assert.Equal(320, frame.PixelWidth);
                Assert.Equal(320, frame.PixelHeight);
            }
        });
    }

    [Fact]
    public void EnsureAssetsExist_DoesNotOverwriteExistingFiles()
    {
        RunInSta(() =>
        {
            var service = new MascotAssetService(_tempDir);
            var idlePath = Path.Combine(_tempDir, "idle.png");

            // Write custom content
            File.WriteAllText(idlePath, "custom-content");

            service.EnsureAssetsExist();

            // Should remain untouched
            Assert.Equal("custom-content", File.ReadAllText(idlePath));
        });
    }

    [Fact]
    public void GetSprite_ReturnsValidFrozenBitmaps_AndCachesResults()
    {
        RunInSta(() =>
        {
            var service = new MascotAssetService(_tempDir);
            service.EnsureAssetsExist();

            var states = new[]
            {
                MascotState.Idle,
                MascotState.Peek,
                MascotState.Attention,
                MascotState.Clicked,
                MascotState.Hidden
            };

            foreach (var state in states)
            {
                var sprite1 = service.GetSprite(state);
                Assert.NotNull(sprite1);
                Assert.True(sprite1.IsFrozen);

                var sprite2 = service.GetSprite(state);
                Assert.Same(sprite1, sprite2); // Cached instance
            }
        });
    }

    [Fact]
    public void GetSprite_DoesNotLockFileOnDisk_AllowsOverwriting()
    {
        RunInSta(() =>
        {
            var service = new MascotAssetService(_tempDir);
            service.EnsureAssetsExist();

            var sprite = service.GetSprite(MascotState.Idle);
            Assert.NotNull(sprite);

            // Verify file on disk can be opened with exclusive write access and modified
            var idlePath = Path.Combine(_tempDir, "idle.png");
            using (var stream = new FileStream(idlePath, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                stream.WriteByte(0xFF);
            }

            // File modification succeeded without locking exception
            Assert.True(File.Exists(idlePath));
        });
    }

    [Fact]
    public void GetSprite_WhenFileIsCorrupted_FallsBackToVectorRenderingWithoutThrowing()
    {
        RunInSta(() =>
        {
            var service = new MascotAssetService(_tempDir);
            var idlePath = Path.Combine(_tempDir, "idle.png");

            // Write corrupted non-image bytes
            File.WriteAllText(idlePath, "corrupted-file-content-not-a-valid-png");

            var sprite = service.GetSprite(MascotState.Idle);
            Assert.NotNull(sprite);
            Assert.Equal(320, sprite.PixelWidth);
            Assert.Equal(320, sprite.PixelHeight);
            Assert.True(sprite.IsFrozen);
        });
    }

    [Fact]
    public void ClearCache_RemovesCachedInstances()
    {
        RunInSta(() =>
        {
            var service = new MascotAssetService(_tempDir);
            service.EnsureAssetsExist();

            var sprite1 = service.GetSprite(MascotState.Idle);
            service.ClearCache();
            var sprite2 = service.GetSprite(MascotState.Idle);

            Assert.NotSame(sprite1, sprite2); // Reloaded after clearing cache
        });
    }

    [Fact]
    public void RenderSprite_DirectCall_ReturnsFrozen320x320Bitmap()
    {
        RunInSta(() =>
        {
            var service = new MascotAssetService(_tempDir);
            var bitmap = service.RenderSprite(MascotState.Attention);

            Assert.NotNull(bitmap);
            Assert.Equal(320, bitmap.PixelWidth);
            Assert.Equal(320, bitmap.PixelHeight);
            Assert.True(bitmap.IsFrozen);
        });
    }
}
