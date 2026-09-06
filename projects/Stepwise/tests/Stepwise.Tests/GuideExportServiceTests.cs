using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Stepwise.Core.Models;
using Stepwise.Storage.Export;
using Xunit;

namespace Stepwise.Tests;

public class GuideExportServiceTests : IDisposable
{
    private readonly string _testDir;

    public GuideExportServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "Stepwise_Export_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch { }
    }

    private (Project Project, List<Step> Steps) CreateSampleProjectWithScreenshots()
    {
        var screenshotsDir = Path.Combine(_testDir, "assets", "screenshots");
        Directory.CreateDirectory(screenshotsDir);

        var shot1 = Path.Combine(screenshotsDir, "step_001.png");
        var shot2 = Path.Combine(screenshotsDir, "step_002.png");

        // Создаем валидные тестовые PNG-файлы (1x1 пиксель)
        byte[] samplePng = [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
            0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89, 0x00, 0x00, 0x00,
            0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
            0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49,
            0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
        ];
        File.WriteAllBytes(shot1, samplePng);
        File.WriteAllBytes(shot2, samplePng);

        var project = new Project(
            Id: Guid.NewGuid(),
            Name: "1С: Инструкция по администрированию",
            RootPath: _testDir,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            Description: "Руководство пользователя по настройке резервного копирования и пользователей."
        );

        var elem1 = new ElementInfo(
            Name: "Администрирование",
            ControlType: "Button",
            AutomationId: "btnAdmin",
            ClassName: "V8Button",
            ProcessName: "1cv8",
            ProcessId: 4242,
            WindowTitle: "1С:Предприятие 8.3",
            WindowHandle: 1001,
            BoundingRectangle: new BoundingBox(10, 50, 180, 32),
            FrameworkId: "Win32",
            IsPassword: false
        );

        var elem2 = new ElementInfo(
            Name: "Пользователи",
            ControlType: "ListItem",
            AutomationId: "listUsers",
            ClassName: "V8ListItem",
            ProcessName: "1cv8",
            ProcessId: 4242,
            WindowTitle: "1С:Предприятие 8.3",
            WindowHandle: 1001,
            BoundingRectangle: new BoundingBox(200, 120, 240, 28),
            FrameworkId: "Win32",
            IsPassword: false
        );

        var steps = new List<Step>
        {
            new(
                Id: Guid.NewGuid(),
                SequenceIndex: 1,
                Timestamp: DateTime.UtcNow.AddMinutes(-2),
                Action: ActionType.LeftClick,
                ClickX: 100,
                ClickY: 66,
                TargetElement: elem1,
                ScreenshotPath: "assets/screenshots/step_001.png",
                Title: "Переход в раздел Администрирование",
                Description: "В левой панели навигации нажмите на раздел «Администрирование»."
            ),
            new(
                Id: Guid.NewGuid(),
                SequenceIndex: 2,
                Timestamp: DateTime.UtcNow.AddMinutes(-1),
                Action: ActionType.LeftClick,
                ClickX: 320,
                ClickY: 134,
                TargetElement: elem2,
                ScreenshotPath: "assets/screenshots/step_002.png",
                Title: "Открытие списка пользователей",
                Description: "В открывшейся панели выберите пункт «Настройки пользователей и прав»."
            )
        };

        return (project, steps);
    }

    [Fact]
    public async Task ExportToDocxAsync_ValidGuide_GeneratesValidOpenXmlDocxWithImages()
    {
        var (project, steps) = CreateSampleProjectWithScreenshots();
        var exporter = new GuideExportService(project.RootPath);
        var docxPath = Path.Combine(_testDir, "guide.docx");

        await exporter.ExportToDocxAsync(project, steps, docxPath);

        Assert.True(File.Exists(docxPath));
        var fileInfo = new FileInfo(docxPath);
        Assert.True(fileInfo.Length > 0);

        // Проверяем внутреннюю структуру OpenXML ZIP архива
        using var zip = ZipFile.OpenRead(docxPath);
        Assert.NotNull(zip.GetEntry("[Content_Types].xml"));
        Assert.NotNull(zip.GetEntry("_rels/.rels"));
        Assert.NotNull(zip.GetEntry("word/document.xml"));
        Assert.NotNull(zip.GetEntry("word/_rels/document.xml.rels"));
        Assert.NotNull(zip.GetEntry("word/styles.xml"));
        Assert.NotNull(zip.GetEntry("word/media/image1.png"));
        Assert.NotNull(zip.GetEntry("word/media/image2.png"));

        // Проверяем наличие заголовков шагов внутри document.xml
        var docXmlEntry = zip.GetEntry("word/document.xml")!;
        using var reader = new StreamReader(docXmlEntry.Open(), Encoding.UTF8);
        var content = reader.ReadToEnd();
        Assert.Contains("Администрирование", content);
        Assert.Contains("Пользователи", content);
        Assert.Contains("Шаг 1", content);
        Assert.Contains("Шаг 2", content);
    }

    [Fact]
    public async Task ExportToHtmlAsync_ValidGuide_GeneratesValidHtmlWithBase64Images()
    {
        var (project, steps) = CreateSampleProjectWithScreenshots();
        var exporter = new GuideExportService(project.RootPath);
        var htmlPath = Path.Combine(_testDir, "guide.html");

        await exporter.ExportToHtmlAsync(project, steps, htmlPath);

        Assert.True(File.Exists(htmlPath));
        var html = await File.ReadAllTextAsync(htmlPath);
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("Администрирование", html);
        Assert.Contains("Пользователи", html);
        Assert.Contains("data:image/png;base64,", html);
    }

    [Fact]
    public async Task ExportToPdfAsync_ValidGuide_GeneratesValidPdfDocument()
    {
        var (project, steps) = CreateSampleProjectWithScreenshots();
        var exporter = new GuideExportService(project.RootPath);
        var pdfPath = Path.Combine(_testDir, "guide.pdf");

        await exporter.ExportToPdfAsync(project, steps, pdfPath);

        Assert.True(File.Exists(pdfPath));
        var bytes = await File.ReadAllBytesAsync(pdfPath);
        Assert.True(bytes.Length > 100);

        // Проверяем заголовок и завершающий маркер PDF
        string header = Encoding.ASCII.GetString(bytes, 0, 8);
        Assert.StartsWith("%PDF-1.", header);

        string trailer = Encoding.ASCII.GetString(bytes, bytes.Length - 10, 10);
        Assert.Contains("%%EOF", trailer);
    }

    [Fact]
    public async Task ExportToDocxAsync_MissingScreenshots_HandlesGracefullyWithoutCrashing()
    {
        var (project, steps) = CreateSampleProjectWithScreenshots();
        // Указываем несуществующие скриншоты
        var modifiedSteps = new List<Step>
        {
            steps[0] with { ScreenshotPath = "non_existent_1.png" },
            steps[1] with { ScreenshotPath = "non_existent_2.png" }
        };

        var exporter = new GuideExportService(project.RootPath);
        var docxPath = Path.Combine(_testDir, "guide_no_images.docx");

        await exporter.ExportToDocxAsync(project, modifiedSteps, docxPath);

        Assert.True(File.Exists(docxPath));
        using var zip = ZipFile.OpenRead(docxPath);
        Assert.NotNull(zip.GetEntry("word/document.xml"));
    }
}
