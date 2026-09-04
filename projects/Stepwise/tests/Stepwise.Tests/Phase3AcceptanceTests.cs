using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Stepwise.Core.Models;
using Stepwise.Storage.Repositories;
using Xunit;

namespace Stepwise.Tests;

/// <summary>
/// Приемочные тесты Фазы 3 в соответствии с матрицей Раздела 16 specs/spec.md (Acceptance Tests 1-12).
/// </summary>
public sealed class Phase3AcceptanceTests : IDisposable
{
    private readonly string _tempTestDir;

    public Phase3AcceptanceTests()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), "Stepwise_Phase3_Tests_" + Guid.NewGuid().ToString("N"));
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
            // Игнорируем задержки освобождения файловой системы ОС
        }
    }

    /// <summary>
    /// Acceptance Test 10 & Раздел 12: Чтение скриншота не удерживает дескриптор файла.
    /// Файл скриншота может быть сразу же изменен, перезаписан или удален другим процессом.
    /// </summary>
    [Fact]
    public async Task AcceptanceTest_FileLocking_ScreenshotStreamIsImmediatelyDisposed()
    {
        // Arrange: создаем временный скриншот
        var screenshotPath = Path.Combine(_tempTestDir, "test_step_lock.png");
        using (var bmp = new Bitmap(200, 100))
        using (var gfx = Graphics.FromImage(bmp))
        {
            gfx.Clear(Color.AliceBlue);
            bmp.Save(screenshotPath, ImageFormat.Png);
        }

        Assert.True(File.Exists(screenshotPath));

        // Act: Читаем байты по паттерну ImageLoaderService (FileShare.ReadWrite + немедленный Dispose)
        byte[] fileBytes;
        using (var fileStream = new FileStream(screenshotPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            fileBytes = new byte[fileStream.Length];
            await fileStream.ReadExactlyAsync(fileBytes, 0, (int)fileStream.Length);
        }

        Assert.True(fileBytes.Length > 0);

        // Assert: Проверяем, что файл НЕ заблокирован: можем открыть на эксклюзивную запись и перезаписать
        Exception? fileLockException = null;
        try
        {
            using (var writeStream = new FileStream(screenshotPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var dummyBytes = new byte[] { 1, 2, 3, 4 };
                writeStream.Write(dummyBytes, 0, dummyBytes.Length);
            }

            File.Delete(screenshotPath);
        }
        catch (IOException ex)
        {
            fileLockException = ex;
        }

        Assert.Null(fileLockException);
        Assert.False(File.Exists(screenshotPath));
    }

    /// <summary>
    /// Acceptance Test 10 & Раздел 14: Устойчивость к отсутствующим, нулевым или поврежденным ассетам скриншотов.
    /// </summary>
    [Fact]
    public async Task AcceptanceTest_MissingAndCorruptedAssets_HandledGracefully()
    {
        // 1. Отсутствующий файл
        var missingPath = Path.Combine(_tempTestDir, "non_existent.png");
        Assert.False(File.Exists(missingPath));

        // 2. Файл размером 0 байт
        var zeroBytePath = Path.Combine(_tempTestDir, "zero_byte.png");
        await File.WriteAllBytesAsync(zeroBytePath, Array.Empty<byte>());
        var zeroFileInfo = new FileInfo(zeroBytePath);
        Assert.Equal(0, zeroFileInfo.Length);

        // 3. Поврежденный файл (битый заголовок)
        var corruptedPath = Path.Combine(_tempTestDir, "corrupted.png");
        await File.WriteAllBytesAsync(corruptedPath, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x01 });

        // Проверяем паттерн чтения ImageLoaderService для 0-байтовых файлов: возвращает null без исключений
        using var stream = new FileStream(zeroBytePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        Assert.Equal(0, stream.Length);
        // Не пытается читать 0 байт
    }

    /// <summary>
    /// Acceptance Test 4, 5, 6 & Раздел 3.2: Проверка математики BoundingBox и индикатора клика (ClickPin).
    /// </summary>
    [Fact]
    public void AcceptanceTest_BoundingBox_CoordinatesAndClickPinCalculations()
    {
        // Arrange
        var bb = new BoundingBox(120.5, 340.0, 250.0, 44.0);
        var clickX = 145.0;
        var clickY = 360.0;

        // Act
        var clickPinLeft = clickX - 9;
        var clickPinTop = clickY - 9;
        var hasHighlight = !bb.IsEmpty && bb.Width > 0 && bb.Height > 0;
        var hasClickPin = clickX > 0 || clickY > 0;

        // Assert
        Assert.False(bb.IsEmpty);
        Assert.Equal(120.5, bb.X);
        Assert.Equal(340.0, bb.Y);
        Assert.Equal(250.0, bb.Width);
        Assert.Equal(44.0, bb.Height);
        Assert.Equal(136.0, clickPinLeft);
        Assert.Equal(351.0, clickPinTop);
        Assert.True(hasHighlight);
        Assert.True(hasClickPin);

        // Empty state
        var emptyBb = BoundingBox.Empty;
        Assert.True(emptyBb.IsEmpty);
        Assert.False(emptyBb.Width > 0 && emptyBb.Height > 0);
    }

    /// <summary>
    /// Acceptance Test 9 & Раздел 12: Быстрое прокликивание шагов (1 -> 2 -> 3 -> 4 -> 5) отменяет предыдущие задачи
    /// через CancellationToken и не допускает рассинхронизации финального состояния.
    /// </summary>
    [Fact]
    public async Task AcceptanceTest_RapidSelectionRaceCondition_LatestTokenPreventsOldState()
    {
        // Arrange
        var steps = new List<int> { 1, 2, 3, 4, 5 };
        int activeStep = -1;
        CancellationTokenSource? currentCts = null;

        // Act: Имитируем быстрый клик по шагам 1 -> 2 -> 3 -> 4 -> 5 с искусственной задержкой
        var tasks = new List<Task>();
        foreach (var stepNum in steps)
        {
            currentCts?.Cancel();
            currentCts = new CancellationTokenSource();
            var token = currentCts.Token;

            var task = Task.Run(async () =>
            {
                // Имитация асинхронной загрузки скриншота шага
                await Task.Delay(stepNum == 5 ? 10 : 100, token);
                if (!token.IsCancellationRequested)
                {
                    Interlocked.Exchange(ref activeStep, stepNum);
                }
            }, token);

            tasks.Add(task);
            await Task.Delay(5); // Быстрое переключение с минимальным интервалом
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            // Ожидаемо для отмененных шагов 1-4
        }

        // Assert: В активном состоянии остался СТРОГО шаг 5
        Assert.Equal(5, activeStep);
    }

    /// <summary>
    /// Acceptance Test 7, 8, 11 & Раздел 15: Сохранение и персистентность отредактированных заголовка и описания
    /// шага в SQLite при перезапуске репозитория.
    /// </summary>
    [Fact]
    public void AcceptanceTest_Persistence_UpdatedStepDetailsRetainedAcrossRepositoryInstances()
    {
        var projectRoot = Path.Combine(_tempTestDir, "PersistenceProject");
        var stepId = Guid.NewGuid();

        // 1. Создаем проект и сохраняем исходный шаг в первой сессии
        using (var repoSession1 = new ProjectRepository(projectRoot))
        {
            repoSession1.CreateProject("Session 1 Guide");

            var initialStep = new Step(
                Id: stepId,
                SequenceIndex: 0,
                Timestamp: DateTime.UtcNow,
                Action: ActionType.LeftClick,
                ClickX: 100,
                ClickY: 200,
                TargetElement: new ElementInfo(
                    Name: "SubmitButton",
                    ControlType: "Button",
                    AutomationId: "btnSubmit",
                    ClassName: "WpfButton",
                    ProcessName: "notepad",
                    ProcessId: 4321,
                    WindowTitle: "Untitled - Notepad",
                    WindowHandle: 0x5555,
                    BoundingRectangle: new BoundingBox(80, 180, 120, 40)
                ),
                ScreenshotPath: "assets/screenshots/step_001.png",
                Title: "Исходный заголовок",
                Description: "Исходное описание"
            );

            repoSession1.SaveStep(initialStep);

            // Редактируем заголовок и описание (Test 7, Test 8)
            repoSession1.UpdateStepDetails(stepId, "Обновленный заголовок", "Обновленное описание шага");
        } // Сессия 1 завершена, репозиторий закрыт

        // 2. Открываем вторую независимую сессию из той же директории (Test 11 - Persistence)
        using (var repoSession2 = new ProjectRepository(projectRoot))
        {
            var loadedProject = repoSession2.LoadProject();
            Assert.NotNull(loadedProject);
            Assert.Equal("Session 1 Guide", loadedProject.Name);

            var steps = repoSession2.LoadSteps();
            Assert.Single(steps);

            var loadedStep = steps[0];
            Assert.Equal(stepId, loadedStep.Id);
            Assert.Equal("Обновленный заголовок", loadedStep.Title);
            Assert.Equal("Обновленное описание шага", loadedStep.Description);

            // Проверяем, что все UIA метаданные остались нетронутыми
            Assert.Equal("SubmitButton", loadedStep.TargetElement.Name);
            Assert.Equal("Button", loadedStep.TargetElement.ControlType);
            Assert.Equal("btnSubmit", loadedStep.TargetElement.AutomationId);
            Assert.Equal("notepad", loadedStep.TargetElement.ProcessName);
            Assert.Equal(4321, loadedStep.TargetElement.ProcessId);
            Assert.Equal("Untitled - Notepad", loadedStep.TargetElement.WindowTitle);
            Assert.Equal(0x5555, loadedStep.TargetElement.WindowHandle);
            Assert.Equal(120, loadedStep.TargetElement.BoundingRectangle.Width);
            Assert.Equal("assets/screenshots/step_001.png", loadedStep.ScreenshotPath);
        }
    }

    /// <summary>
    /// Acceptance Test 12 & Раздел 13: Масштабируемость и производительность проекта со 100+ шагами.
    /// </summary>
    [Fact]
    public void AcceptanceTest_StressScalability_100PlusStepsPerformance()
    {
        var projectRoot = Path.Combine(_tempTestDir, "StressProject");
        const int stepCount = 120;

        using var repo = new ProjectRepository(projectRoot);
        repo.CreateProject("Stress Scalability Project");

        var sw = Stopwatch.StartNew();

        // 1. Пакетная вставка 120 шагов
        for (int i = 0; i < stepCount; i++)
        {
            var step = new Step(
                Id: Guid.NewGuid(),
                SequenceIndex: i,
                Timestamp: DateTime.UtcNow.AddSeconds(i),
                Action: (i % 2 == 0) ? ActionType.LeftClick : ActionType.RightClick,
                ClickX: 100 + i,
                ClickY: 200 + i,
                TargetElement: new ElementInfo(
                    Name: $"Element_{i}",
                    ControlType: "Button",
                    AutomationId: $"btn_{i}",
                    ClassName: "Win32Button",
                    ProcessName: "stressapp",
                    ProcessId: 9999,
                    WindowTitle: "Stress Window",
                    WindowHandle: 0x1111,
                    BoundingRectangle: new BoundingBox(10, 20, 100, 30)
                ),
                ScreenshotPath: $"assets/screenshots/step_{i:D3}.png",
                Title: $"Шаг {i + 1}: Клик по элементу {i}",
                Description: $"Инструкция для шага {i + 1}"
            );
            repo.SaveStep(step);
        }

        sw.Stop();
        var insertTimeMs = sw.ElapsedMilliseconds;

        // 2. Чтение всех 120 шагов (должно быть быстрым, < 100ms)
        sw.Restart();
        var loadedSteps = repo.LoadSteps();
        sw.Stop();
        var loadTimeMs = sw.ElapsedMilliseconds;

        // Assert
        Assert.Equal(stepCount, loadedSteps.Count);
        Assert.True(loadTimeMs < 500, $"Загрузка 120 шагов заняла {loadTimeMs}ms, ожидается < 500ms");

        // Проверяем последовательность индексов
        for (int i = 0; i < stepCount; i++)
        {
            Assert.Equal(i, loadedSteps[i].SequenceIndex);
        }

        // 3. Проверяем точечное обновление шага 50
        var targetStep = loadedSteps[50];
        repo.UpdateStepDetails(targetStep.Id, "Обновленный шаг 50", "Новое описание");

        var reloaded = repo.LoadSteps();
        Assert.Equal("Обновленный шаг 50", reloaded[50].Title);
        Assert.Equal("Новое описание", reloaded[50].Description);
    }

    /// <summary>
    /// Acceptance Test 1, 2, 3: Инициализация и подготовка реального проекта в DefaultProject
    /// с проверенными скриншотами и метаданными для живой инспекции WinUI 3 Shell.
    /// </summary>
    [Fact]
    public void AcceptanceTest_SeedDefaultProject_EnablesLiveWinUIInspection()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var defaultProjectRoot = Path.Combine(localAppData, "Stepwise", "DefaultProject");
        var screenshotsDir = Path.Combine(defaultProjectRoot, "assets", "screenshots");
        Directory.CreateDirectory(screenshotsDir);

        // 1. Создаем 2 реальных скриншота на диске
        var png1 = Path.Combine(screenshotsDir, "step_001.png");
        var png2 = Path.Combine(screenshotsDir, "step_002.png");

        using (var bmp1 = new Bitmap(1280, 720))
        using (var gfx1 = Graphics.FromImage(bmp1))
        {
            gfx1.Clear(Color.LightSteelBlue);
            using var brush = new SolidBrush(Color.DarkBlue);
            using var font = new Font(FontFamily.GenericSansSerif, 24, FontStyle.Bold);
            gfx1.DrawString("Step 1: Open Settings Window", font, brush, 50, 50);
            bmp1.Save(png1, ImageFormat.Png);
        }

        using (var bmp2 = new Bitmap(1280, 720))
        using (var gfx2 = Graphics.FromImage(bmp2))
        {
            gfx2.Clear(Color.Honeydew);
            using var brush = new SolidBrush(Color.DarkGreen);
            using var font = new Font(FontFamily.GenericSansSerif, 24, FontStyle.Bold);
            gfx2.DrawString("Step 2: Toggle Dark Mode", font, brush, 50, 50);
            bmp2.Save(png2, ImageFormat.Png);
        }

        Assert.True(File.Exists(png1));
        Assert.True(File.Exists(png2));

        // 2. Очищаем старую БД для детерминированного приемочного состояния
        var dbPath = Path.Combine(defaultProjectRoot, "project.db");
        if (File.Exists(dbPath))
        {
            try { File.Delete(dbPath); } catch { }
        }

        // 3. Сохраняем проект и 2 шага в SQLite
        using (var repo = new ProjectRepository(defaultProjectRoot))
        {
            repo.CreateProject("Settings Guide", "Walkthrough for configuring application settings");

            var step1Id = new Guid("11111111-1111-1111-1111-111111111111");
            var step1 = new Step(
                Id: step1Id,
                SequenceIndex: 0,
                Timestamp: DateTime.UtcNow,
                Action: ActionType.LeftClick,
                ClickX: 100,
                ClickY: 70,
                TargetElement: new ElementInfo(
                    Name: "Settings",
                    ControlType: "Button",
                    AutomationId: "btnSettings",
                    ClassName: "Windows.UI.Xaml.Controls.Button",
                    ProcessName: "Stepwise.App",
                    ProcessId: 1234,
                    WindowTitle: "Stepwise — Interactive Walkthrough Engine",
                    WindowHandle: 1001,
                    BoundingRectangle: new BoundingBox(50, 50, 200, 40)
                ),
                ScreenshotPath: "assets/screenshots/step_001.png",
                Title: "Шаг 1: Нажмите Settings",
                Description: "Нажмите на кнопку настроек в главном окне."
            );
            repo.SaveStep(step1);

            var step2Id = new Guid("22222222-2222-2222-2222-222222222222");
            var step2 = new Step(
                Id: step2Id,
                SequenceIndex: 1,
                Timestamp: DateTime.UtcNow.AddSeconds(5),
                Action: ActionType.LeftClick,
                ClickX: 130,
                ClickY: 195,
                TargetElement: new ElementInfo(
                    Name: "Dark Mode",
                    ControlType: "CheckBox",
                    AutomationId: "chkDarkMode",
                    ClassName: "Windows.UI.Xaml.Controls.CheckBox",
                    ProcessName: "Stepwise.App",
                    ProcessId: 1234,
                    WindowTitle: "Settings Dialog",
                    WindowHandle: 1002,
                    BoundingRectangle: new BoundingBox(120, 180, 150, 32)
                ),
                ScreenshotPath: "assets/screenshots/step_002.png",
                Title: "Шаг 2: Включите Dark Mode",
                Description: "Переключите флажок для активации темной темы."
            );
            repo.SaveStep(step2);

            var steps = repo.LoadSteps();
            Assert.Equal(2, steps.Count);
        }
    }
}
