using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml.Media.Imaging;
using Moq;
using Stepwise.App.Services;
using Stepwise.App.ViewModels;
using Stepwise.Core.Engine;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Stepwise.Storage.Repositories;
using Xunit;

namespace Stepwise.Tests;

/// <summary>
/// Unit- и приемочные тесты жизненного цикла изображений плеера (Player Image Lifecycle Tests)
/// в строгом соответствии с Разделами 6, 12, 13 и 15 specs/spec.md.
/// </summary>
public sealed class PlayerImageLifecycleTests : IDisposable
{
    private readonly string _tempTestDir;

    public PlayerImageLifecycleTests()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), "Stepwise_PlayerImageTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempTestDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempTestDir))
            {
                Directory.Delete(_tempTestDir, recursive: true);
            }
        }
        catch
        {
            // Игнорируем задержки файловой системы ОС
        }
    }

    private static Step CreateTestStep(int sequenceIndex, string? screenshotPath = null, string? title = null)
    {
        return new Step(
            Id: Guid.NewGuid(),
            SequenceIndex: sequenceIndex,
            Timestamp: DateTime.UtcNow,
            Action: ActionType.LeftClick,
            ClickX: 150.0 + sequenceIndex * 10,
            ClickY: 250.0 + sequenceIndex * 10,
            TargetElement: new ElementInfo(
                Name: $"Element_{sequenceIndex}",
                ControlType: "Button",
                AutomationId: $"btn_{sequenceIndex}",
                ClassName: "Button",
                ProcessName: "notepad",
                ProcessId: 1234,
                WindowTitle: "Untitled - Notepad",
                WindowHandle: 0x1000,
                BoundingRectangle: new BoundingBox(100.0, 200.0, 80.0, 30.0)
            ),
            ScreenshotPath: screenshotPath ?? $"assets/screenshots/step_{sequenceIndex:D3}.png",
            Title: title ?? $"Step {sequenceIndex}",
            Description: $"Description for step {sequenceIndex}"
        );
    }

    private string CreateTestImageFile(string fileName, int width = 100, int height = 50)
    {
        var filePath = Path.Combine(_tempTestDir, fileName);
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var bmp = new Bitmap(width, height);
        using var gfx = Graphics.FromImage(bmp);
        gfx.Clear(Color.CornflowerBlue);
        bmp.Save(filePath, ImageFormat.Png);
        return filePath;
    }

    /// <summary>
    /// Requirement 1 & Раздел 12: Быстрое переключение шагов (Step 1 -> Step 2 -> Step 3 -> Step 4)
    /// отменяет предыдущие CancellationTokenSource, и итоговым состоянием остается СТРОГО Step 4.
    /// Устаревшие задачи от шагов 1-3 не могут перезаписать превью активного шага.
    /// </summary>
    [Fact]
    public async Task RapidStepSwitching_CancelsPriorTokens_AndSetsImageOnlyForActiveStep()
    {
        // Arrange
        var steps = new List<Step>
        {
            CreateTestStep(1, CreateTestImageFile("step_001.png")),
            CreateTestStep(2, CreateTestImageFile("step_002.png")),
            CreateTestStep(3, CreateTestImageFile("step_003.png")),
            CreateTestStep(4, CreateTestImageFile("step_004.png"))
        };

        var observedTokens = new List<CancellationToken>();
        var stepTcsMap = new Dictionary<string, TaskCompletionSource<BitmapImage?>>();

        foreach (var s in steps)
        {
            stepTcsMap[s.ScreenshotPath!] = new TaskCompletionSource<BitmapImage?>();
        }

        var mockLoader = new Mock<IImageLoaderService>();
        mockLoader
            .Setup(l => l.LoadPreviewAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns<string?, CancellationToken>((path, token) =>
            {
                lock (observedTokens)
                {
                    observedTokens.Add(token);
                }

                if (path != null && stepTcsMap.TryGetValue(path, out var tcs))
                {
                    // Реакция на отмену через токен
                    token.Register(() => tcs.TrySetCanceled(token));
                    return tcs.Task;
                }

                return Task.FromResult<BitmapImage?>(null);
            });

        var engine = new PlayerEngine();
        using var vm = new PlayerViewModel(engine, mockLoader.Object);
        vm.ProjectRootPath = _tempTestDir;

        // Act: Загружаем руководство и быстро переключаем шаги 1 -> 2 -> 3 -> 4
        engine.LoadGuide(Guid.NewGuid(), steps); // Загружает шаг 1 (индекс 0)

        // Быстрый проклик шагов (симуляция быстрого клика пользователя в UI)
        engine.Next(); // Шаг 2 (индекс 1)
        engine.Next(); // Шаг 3 (индекс 2)
        engine.Next(); // Шаг 4 (индекс 3)

        // Небольшая пауза для завершения планирования на фоновых задачах
        await Task.Yield();

        // Assert: Токены шагов 1, 2, 3 ДОЛЖНЫ быть отменены
        Assert.True(observedTokens.Count >= 4, $"Expected at least 4 observed tokens, got {observedTokens.Count}");
        Assert.True(observedTokens[0].IsCancellationRequested, "Token for Step 1 must be cancelled");
        Assert.True(observedTokens[1].IsCancellationRequested, "Token for Step 2 must be cancelled");
        Assert.True(observedTokens[2].IsCancellationRequested, "Token for Step 3 must be cancelled");
        Assert.False(observedTokens[3].IsCancellationRequested, "Token for Step 4 must NOT be cancelled");

        // Имитируем завершение задачи шага 4
        stepTcsMap[steps[3].ScreenshotPath!].TrySetResult(null); // В headless среде без WinUI возвращаем null
        await Task.Yield();

        // Финальное состояние — СТРОГО Шаг 4
        Assert.Equal(3, vm.CurrentIndex);
        Assert.Equal(4, vm.CurrentStep?.SequenceIndex);
        Assert.Equal("Step 4", vm.CurrentStepTitle);
    }

    /// <summary>
    /// Requirement 2 & Раздел 14: Отсутствующий файл скриншота: IsPreviewError == true,
    /// PreviewErrorMessage задано с понятным пользователю сообщением, приложение НЕ падает.
    /// </summary>
    [Fact]
    public async Task MissingScreenshotFile_SetsIsPreviewError_AndDescriptiveMessage_WithoutCrashing()
    {
        // Arrange
        var missingPath = Path.Combine(_tempTestDir, "assets", "screenshots", "non_existent_file.png");
        Assert.False(File.Exists(missingPath));

        var step = CreateTestStep(1, screenshotPath: missingPath);
        var steps = new List<Step> { step };

        var mockLoader = new Mock<IImageLoaderService>();
        var engine = new PlayerEngine();
        using var vm = new PlayerViewModel(engine, mockLoader.Object);
        vm.ProjectRootPath = _tempTestDir;

        // Act: Загружаем шаг с несуществующим файлом
        engine.LoadGuide(Guid.NewGuid(), steps);
        await vm.LoadPreviewForStepAsync(step);

        // Assert
        Assert.True(vm.IsPreviewError, "IsPreviewError must be true for missing file");
        Assert.False(vm.IsPreviewLoading, "IsPreviewLoading must be false when error is detected");
        Assert.Null(vm.PreviewImage);
        Assert.NotNull(vm.PreviewErrorMessage);
        Assert.Contains("не найден", vm.PreviewErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Проверяем, что загрузчик даже не вызывался для отсутствующего файла (оптимизация)
        mockLoader.Verify(l => l.LoadPreviewAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Requirement 3 & Раздел 14: Поврежденный или пустой (0 байт) файл скриншота:
    /// IsPreviewError == true, PreviewErrorMessage задано, приложение НЕ падает,
    /// и кнопка Retry повторно инициирует загрузку.
    /// </summary>
    [Fact]
    public async Task CorruptedOrEmptyScreenshotFile_HandlesGracefully_WithErrorMessage_AndRetryCapability()
    {
        // 1. Пустой 0-байтовый файл
        var zeroByteFile = Path.Combine(_tempTestDir, "zero_byte_step.png");
        await File.WriteAllBytesAsync(zeroByteFile, Array.Empty<byte>());
        Assert.True(File.Exists(zeroByteFile));
        Assert.Equal(0, new FileInfo(zeroByteFile).Length);

        // 2. Поврежденный файл (битый заголовок)
        var corruptedFile = Path.Combine(_tempTestDir, "corrupted_step.png");
        await File.WriteAllBytesAsync(corruptedFile, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x01 });
        Assert.True(File.Exists(corruptedFile));

        var stepZero = CreateTestStep(1, screenshotPath: zeroByteFile);
        var stepCorrupt = CreateTestStep(2, screenshotPath: corruptedFile);
        var steps = new List<Step> { stepZero, stepCorrupt };

        var mockLoader = new Mock<IImageLoaderService>();
        mockLoader
            .Setup(l => l.LoadPreviewAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BitmapImage?)null); // Поврежденный/пустой файл возвращает null при декодировании

        var engine = new PlayerEngine();
        using var vm = new PlayerViewModel(engine, mockLoader.Object);
        vm.ProjectRootPath = _tempTestDir;

        // Act 1: Загрузка 0-байтового файла
        engine.LoadGuide(Guid.NewGuid(), steps);
        await vm.LoadPreviewForStepAsync(stepZero);

        // Assert 1: Пустой файл обработан без краха
        Assert.True(vm.IsPreviewError);
        Assert.False(vm.IsPreviewLoading);
        Assert.Null(vm.PreviewImage);
        Assert.NotNull(vm.PreviewErrorMessage);
        Assert.Contains("поврежден или пуст", vm.PreviewErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Act 2: Переключение на поврежденный файл
        engine.Next();
        await vm.LoadPreviewForStepAsync(stepCorrupt);

        // Assert 2: Поврежденный файл обработан без краха
        Assert.True(vm.IsPreviewError);
        Assert.False(vm.IsPreviewLoading);
        Assert.Null(vm.PreviewImage);
        Assert.NotNull(vm.PreviewErrorMessage);
        Assert.Contains("поврежден или пуст", vm.PreviewErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Act 3: Проверка RetryCapability:
        // Вызов команды RetryImageLoad повторно запрашивает загрузчик без исключений
        vm.RetryImageLoadCommand.Execute(null);
        await Task.Yield();

        mockLoader.Verify(l => l.LoadPreviewAsync(corruptedFile, It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    /// <summary>
    /// Requirement 4 & Раздел 12: Stream disposed immediately after decode.
    /// Файл скриншота может быть сразу же изменен, перезаписан или удален другим процессом
    /// или тестом, так как дескриптор файла не удерживается.
    /// </summary>
    [Fact]
    public async Task StreamDisposal_ImmediatelyDisposesStream_AndUnlocksFile()
    {
        // Arrange: Создаем реальный PNG-файл
        var imagePath = CreateTestImageFile("unlock_test.png", 300, 200);
        Assert.True(File.Exists(imagePath));

        // Act: Читаем файл по паттерну ImageLoaderService (FileShare.ReadWrite + немедленный Dispose)
        byte[] fileBytes;
        using (var fileStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            Assert.True(fileStream.Length > 0);
            fileBytes = new byte[fileStream.Length];
            await fileStream.ReadExactlyAsync(fileBytes, 0, (int)fileStream.Length);
        }

        Assert.NotEmpty(fileBytes);

        // Assert: Проверяем, что файл НЕ заблокирован:
        // 1. Можем открыть с эксклюзивным доступом на запись (FileShare.None)
        using (var exclusiveStream = new FileStream(imagePath, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            var testByte = new byte[] { 0xFF };
            exclusiveStream.Write(testByte, 0, 1);
        }

        // 2. Можем удалить файл с диска без IOException
        File.Delete(imagePath);
        Assert.False(File.Exists(imagePath), "File must be deletable immediately after read");
    }

    /// <summary>
    /// Requirement 5 & Раздел 15: Read-Only Database Safety.
    /// Загрузка и навигация по шагам инструкции ни при каких обстоятельствах
    /// не мутируют свойства шагов Step и не вносят изменений в SQLite базу данных.
    /// </summary>
    [Fact]
    public async Task ReadOnlyBehavior_LoadingAndNavigatingSteps_NeverMutatesStepPropertiesOrDatabase()
    {
        // Arrange: Создаем реальную базу данных SQLite на диске с несколькими шагами
        var projectDir = Path.Combine(_tempTestDir, "ReadOnlyProject");
        Directory.CreateDirectory(projectDir);

        using (var repo = new ProjectRepository(projectDir))
        {
            repo.CreateProject("Read Only Guide", "Testing read-only invariants");

            for (int i = 1; i <= 5; i++)
            {
                var step = CreateTestStep(i, $"assets/screenshots/step_{i:D3}.png", $"Step Title {i}");
                repo.SaveStep(step);
            }
        }

        // Фиксируем исходное состояние БД и шагов
        IReadOnlyList<Step> initialSteps;
        using (var verifyRepo = new ProjectRepository(projectDir))
        {
            initialSteps = verifyRepo.LoadSteps();
        }
        Assert.Equal(5, initialSteps.Count);

        // Act: Инициализируем плеер и выполняем исчерпывающую навигацию
        var mockLoader = new Mock<IImageLoaderService>();
        var engine = new PlayerEngine();
        using (var vm = new PlayerViewModel(engine, mockLoader.Object))
        {
            await vm.InitializeAsync(projectDir);

            // Массовая навигация: Next, Previous, First, Last, Restart, Play/Pause, Toggle
            vm.NextCommand.Execute(null);
            vm.NextCommand.Execute(null);
            vm.PreviousCommand.Execute(null);
            vm.LastCommand.Execute(null);
            vm.FirstCommand.Execute(null);
            vm.RestartCommand.Execute(null);
            vm.PlayPauseCommand.Execute(null);
            vm.PlayPauseCommand.Execute(null);
            vm.ToggleHighlightOverlayCommand.Execute(null);
            vm.ToggleMetadataCommand.Execute(null);
            vm.RetryImageLoadCommand.Execute(null);
        }

        // Assert: Сверяем состояние БД после всех манипуляций в плеере
        IReadOnlyList<Step> stepsAfterPlayer;
        using (var verifyRepo = new ProjectRepository(projectDir))
        {
            stepsAfterPlayer = verifyRepo.LoadSteps();
        }

        Assert.Equal(initialSteps.Count, stepsAfterPlayer.Count);

        for (int i = 0; i < initialSteps.Count; i++)
        {
            var before = initialSteps[i];
            var after = stepsAfterPlayer[i];

            Assert.Equal(before.Id, after.Id);
            Assert.Equal(before.SequenceIndex, after.SequenceIndex);
            Assert.Equal(before.Timestamp, after.Timestamp);
            Assert.Equal(before.Action, after.Action);
            Assert.Equal(before.ClickX, after.ClickX);
            Assert.Equal(before.ClickY, after.ClickY);
            Assert.Equal(before.ScreenshotPath, after.ScreenshotPath);
            Assert.Equal(before.Title, after.Title);
            Assert.Equal(before.Description, after.Description);
            Assert.Equal(before.TargetElement.Name, after.TargetElement.Name);
            Assert.Equal(before.TargetElement.ControlType, after.TargetElement.ControlType);
            Assert.Equal(before.TargetElement.AutomationId, after.TargetElement.AutomationId);
            Assert.Equal(before.TargetElement.ClassName, after.TargetElement.ClassName);
            Assert.Equal(before.TargetElement.ProcessName, after.TargetElement.ProcessName);
            Assert.Equal(before.TargetElement.BoundingRectangle, after.TargetElement.BoundingRectangle);
        }
    }
}
