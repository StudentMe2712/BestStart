using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Stepwise.Core.Engine;
using Stepwise.Core.Models;
using Stepwise.Storage.Repositories;
using Stepwise.WindowsIntegration.Automation;
using Stepwise.WindowsIntegration.Capture;
using Stepwise.WindowsIntegration.Hooks;

namespace Stepwise.App;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        PrintHeader();

        // Инициализируем локальный каталог проекта: [ProjectRoot]/project.db + [ProjectRoot]/assets/screenshots/
        var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var projectRoot = Path.Combine(appDataDir, "Stepwise", "DefaultProject");
        Directory.CreateDirectory(projectRoot);

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"[Storage] Каталог проекта: {projectRoot}");
        Console.WriteLine($"[Storage] База данных:    {Path.Combine(projectRoot, "project.db")}");
        Console.WriteLine($"[Storage] Скриншоты:       {Path.Combine(projectRoot, "assets", "screenshots")}\n");
        Console.ResetColor();

        using var repository = new ProjectRepository(projectRoot);
        var currentProject = repository.LoadProject();
        if (currentProject == null)
        {
            currentProject = repository.CreateProject(
                "QuickStart Walkthrough",
                "Интерактивное руководство, записанное с помощью Stepwise Engine"
            );
            Console.WriteLine($">> Создан новый проект: '{currentProject.Name}' ({currentProject.Id})\n");
        }
        else
        {
            var existingSteps = repository.LoadSteps();
            Console.WriteLine($">> Загружен существующий проект: '{currentProject.Name}' (уже сохранено шагов: {existingSteps.Count})\n");
        }

        using var mouseHookService = new LowLevelMouseHookService();
        var uiaService = new UIAutomationService();
        var captureService = new ScreenCaptureService();

        using var recordingEngine = new RecordingPipelineEngine(
            mouseHookService,
            uiaService,
            captureService,
            repository
        );

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        recordingEngine.StepRecorded += OnStepRecorded;

        try
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(">> Инициализация полного конвейера (Hook -> UIA -> ScreenCapture -> SQLite)...");
            recordingEngine.StartRecording();
            Console.WriteLine(">> Конвейер активен! Кликайте по элементам любых окон Windows.");
            Console.WriteLine(">> Для выхода нажмите Ctrl+C или клавишу 'Q'.\n");
            Console.ResetColor();

            var keyListener = Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(intercept: true);
                        if (key.Key == ConsoleKey.Q || key.Key == ConsoleKey.Escape)
                        {
                            cts.Cancel();
                            break;
                        }
                    }
                    Thread.Sleep(50);
                }
            }, cts.Token);

            await Task.Delay(Timeout.Infinite, cts.Token).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            // Корректный выход
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[ERROR] Непредвиденная ошибка: {ex.Message}");
            Console.ResetColor();
        }
        finally
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n>> Остановка конвейера записи...");
            recordingEngine.StopRecording();

            var allSteps = repository.LoadSteps();
            Console.WriteLine($">> Запись остановлена. Всего шагов в SQLite: {allSteps.Count}.");
            Console.ResetColor();
        }
    }

    private static void OnStepRecorded(object? sender, Step step)
    {
        var json = JsonSerializer.Serialize(step, JsonOptions);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(new string('=', 80));
        Console.WriteLine($"[ШАГ #{step.SequenceIndex}] {step.Timestamp:HH:mm:ss.fff} | Действие: {step.Action} | Точка: ({step.ClickX}, {step.ClickY})");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"Заголовок:  {step.Title}");
        Console.WriteLine($"Процесс:    {step.TargetElement.ProcessName} (PID: {step.TargetElement.ProcessId})");
        Console.WriteLine($"Окно:       {step.TargetElement.WindowTitle}");
        Console.WriteLine($"Элемент:    Name='{step.TargetElement.Name}', Type='{step.TargetElement.ControlType}', Id='{step.TargetElement.AutomationId}'");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Скриншот:   {step.ScreenshotPath ?? "(не создан)"}");
        Console.WriteLine("Статус БД:  Успешно записано в project.db");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("--- JSON Payload ---");
        Console.ResetColor();
        Console.WriteLine(json);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(new string('=', 80) + "\n");
        Console.ResetColor();
    }

    private static void PrintHeader()
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("================================================================================");
        Console.WriteLine("   ____  _                                       __  ____     ____     __ ");
        Console.WriteLine("  / __/ / /_ ___  ___ _    __ (_)___ ___        /  |/  / |   / / _ \\   / / ");
        Console.WriteLine(" _\\ \\  / __// -_)/ _ \\ |/|/ // /(_-</ -_)      / /|_/ /| |  / / ___/  /_/  ");
        Console.WriteLine("/___/  \\__/ \\__// .__/__,__//_//___/\\__/      /_/  /_/ |___/_/_/     (_)   ");
        Console.WriteLine("               /_/                                                         ");
        Console.WriteLine("   Stepwise Interactive Walkthrough Engine — Visual & Storage (MVP 0.2)        ");
        Console.WriteLine("   Pipeline: Hook (Win32) -> UIA -> ScreenCapture (GDI) -> SQLite (DB)         ");
        Console.WriteLine("================================================================================");
        Console.ResetColor();
    }
}
