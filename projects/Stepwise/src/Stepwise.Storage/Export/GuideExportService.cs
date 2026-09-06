using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;

namespace Stepwise.Storage.Export;

/// <summary>
/// Реализация сервиса экспорта руководств <see cref="IGuideExportService"/>.
/// Формирует профессиональные документы в форматах Word (.docx), PDF (.pdf) и HTML (.html)
/// без внешних COM/Office зависимостей.
/// </summary>
public sealed class GuideExportService : IGuideExportService
{
    private readonly string? _projectRootPath;

    public GuideExportService(string? projectRootPath = null)
    {
        _projectRootPath = projectRootPath;
    }

    /// <inheritdoc />
    public async Task ExportToDocxAsync(Project project, IReadOnlyList<Step> steps, string outputPath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // Если файл уже существует, перезаписываем его
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        await Task.Run(() =>
        {
            using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            using var zip = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: false);

            var imageEntries = new List<(string RelId, string MediaPath, string TargetPath, int WidthEmus, int HeightEmus)>();
            int imageIndex = 1;

            // 1. Сохраняем медиа-файлы скриншотов внутри zip
            for (int i = 0; i < steps.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var step = steps[i];
                var fullScreenshotPath = ResolveScreenshotPath(step.ScreenshotPath);

                if (!string.IsNullOrEmpty(fullScreenshotPath) && File.Exists(fullScreenshotPath))
                {
                    try
                    {
                        var bytes = File.ReadAllBytes(fullScreenshotPath);
                        if (bytes.Length > 0)
                        {
                            string relId = $"rIdImg{imageIndex}";
                            string mediaPath = $"word/media/image{imageIndex}.png";
                            string targetPath = $"media/image{imageIndex}.png";

                            var entry = zip.CreateEntry(mediaPath, CompressionLevel.Optimal);
                            using (var entryStream = entry.Open())
                            {
                                entryStream.Write(bytes, 0, bytes.Length);
                            }

                            // Стандартная ширина 5.8 дюймов (5303520 EMUs), соотношение 16:9 по умолчанию
                            int widthEmus = 5303520;
                            int heightEmus = 2983230;

                            imageEntries.Add((relId, mediaPath, targetPath, widthEmus, heightEmus));
                            imageIndex++;
                            continue;
                        }
                    }
                    catch
                    {
                        // Игнорируем поврежденный файл изображения
                    }
                }

                imageEntries.Add((string.Empty, string.Empty, string.Empty, 0, 0));
            }

            // 2. [Content_Types].xml
            WriteZipEntry(zip, "[Content_Types].xml", BuildContentTypesXml(imageEntries.Count > 0));

            // 3. _rels/.rels
            WriteZipEntry(zip, "_rels/.rels", BuildRootRelsXml());

            // 4. word/_rels/document.xml.rels
            WriteZipEntry(zip, "word/_rels/document.xml.rels", BuildDocumentRelsXml(imageEntries));

            // 5. word/styles.xml
            WriteZipEntry(zip, "word/styles.xml", BuildStylesXml());

            // 6. word/document.xml
            WriteZipEntry(zip, "word/document.xml", BuildDocumentXml(project, steps, imageEntries));

        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ExportToHtmlAsync(Project project, IReadOnlyList<Step> steps, string outputPath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var html = await Task.Run(() => BuildHtmlDocument(project, steps, ct), ct).ConfigureAwait(false);
        await File.WriteAllTextAsync(outputPath, html, Encoding.UTF8, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ExportToPdfAsync(Project project, IReadOnlyList<Step> steps, string outputPath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // Генерируем валидный стандартизированный PDF-документ со встроенными текстовыми блоками и скриншотами
        await Task.Run(() =>
        {
            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            GenerateStandardPdf(fs, project, steps, ct);
        }, ct).ConfigureAwait(false);
    }

    private string? ResolveScreenshotPath(string? relativeOrAbsolutePath)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath))
        {
            return null;
        }

        if (Path.IsPathRooted(relativeOrAbsolutePath))
        {
            return relativeOrAbsolutePath;
        }

        if (!string.IsNullOrWhiteSpace(_projectRootPath))
        {
            return Path.Combine(_projectRootPath, relativeOrAbsolutePath);
        }

        return null;
    }

    private static void WriteZipEntry(ZipArchive zip, string entryName, string content)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string BuildContentTypesXml(bool hasImages)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n");
        sb.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">\n");
        sb.Append("  <Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>\n");
        sb.Append("  <Default Extension=\"xml\" ContentType=\"application/xml\"/>\n");
        if (hasImages)
        {
            sb.Append("  <Default Extension=\"png\" ContentType=\"image/png\"/>\n");
            sb.Append("  <Default Extension=\"jpeg\" ContentType=\"image/jpeg\"/>\n");
            sb.Append("  <Default Extension=\"jpg\" ContentType=\"image/jpeg\"/>\n");
        }
        sb.Append("  <Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>\n");
        sb.Append("  <Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/>\n");
        sb.Append("</Types>");
        return sb.ToString();
    }

    private static string BuildRootRelsXml()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
               "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">\n" +
               "  <Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>\n" +
               "</Relationships>";
    }

    private static string BuildDocumentRelsXml(List<(string RelId, string MediaPath, string TargetPath, int WidthEmus, int HeightEmus)> imageEntries)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n");
        sb.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">\n");
        sb.Append("  <Relationship Id=\"rIdStyles\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>\n");

        foreach (var img in imageEntries)
        {
            if (!string.IsNullOrEmpty(img.RelId))
            {
                sb.Append($"  <Relationship Id=\"{img.RelId}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" Target=\"{img.TargetPath}\"/>\n");
            }
        }

        sb.Append("</Relationships>");
        return sb.ToString();
    }

    private static string BuildStylesXml()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
               "<w:styles xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">\n" +
               "  <w:docDefaults>\n" +
               "    <w:rPrDefault>\n" +
               "      <w:rPr>\n" +
               "        <w:rFonts w:ascii=\"Segoe UI\" w:hAnsi=\"Segoe UI\" w:cs=\"Segoe UI\"/>\n" +
               "        <w:sz w:val=\"22\"/>\n" +
               "        <w:color w:val=\"242424\"/>\n" +
               "      </w:rPr>\n" +
               "    </w:rPrDefault>\n" +
               "  </w:docDefaults>\n" +
               "</w:styles>";
    }

    private static string BuildDocumentXml(
        Project project,
        IReadOnlyList<Step> steps,
        List<(string RelId, string MediaPath, string TargetPath, int WidthEmus, int HeightEmus)> imageEntries)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n");
        sb.Append("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" ");
        sb.Append("xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" ");
        sb.Append("xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" ");
        sb.Append("xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" ");
        sb.Append("xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\">\n");
        sb.Append("  <w:body>\n");

        // --- Заголовок документа ---
        string docTitle = SecurityElement.Escape(string.IsNullOrWhiteSpace(project.Name) ? "Пошаговое руководство" : project.Name) ?? "Руководство";
        string docDesc = SecurityElement.Escape(project.Description ?? "Инструкция сформирована автоматически платформой Stepwise.") ?? "";

        sb.Append("    <w:p>\n");
        sb.Append("      <w:pPr><w:jc w:val=\"left\"/><w:spacing w:before=\"200\" w:after=\"100\"/></w:pPr>\n");
        sb.Append("      <w:r><w:rPr><w:b/><w:sz w:val=\"48\"/><w:color w:val=\"0F6CBD\"/></w:rPr>\n");
        sb.Append($"        <w:t>{docTitle}</w:t>\n");
        sb.Append("      </w:r>\n");
        sb.Append("    </w:p>\n");

        // Описание документа
        if (!string.IsNullOrWhiteSpace(docDesc))
        {
            sb.Append("    <w:p>\n");
            sb.Append("      <w:pPr><w:spacing w:after=\"200\"/></w:pPr>\n");
            sb.Append("      <w:r><w:rPr><w:sz w:val=\"24\"/><w:color w:val=\"555555\"/></w:rPr>\n");
            sb.Append($"        <w:t>{docDesc}</w:t>\n");
            sb.Append("      </w:r>\n");
            sb.Append("    </w:p>\n");
        }

        // Метаданные (Дата, количество шагов)
        sb.Append("    <w:p>\n");
        sb.Append("      <w:pPr><w:spacing w:after=\"300\"/></w:pPr>\n");
        sb.Append("      <w:r><w:rPr><w:i/><w:sz w:val=\"20\"/><w:color w:val=\"777777\"/></w:rPr>\n");
        sb.Append($"        <w:t>Количество шагов: {steps.Count}  |  Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}</w:t>\n");
        sb.Append("      </w:r>\n");
        sb.Append("    </w:p>\n");

        // Разделительная линия
        sb.Append("    <w:p><w:pPr><w:pBdr><w:bottom w:val=\"single\" w:sz=\"8\" w:space=\"4\" w:color=\"CCCCCC\"/></w:pBdr><w:spacing w:after=\"300\"/></w:pPr></w:p>\n");

        // --- Карточки шагов ---
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            string stepTitle = SecurityElement.Escape(string.IsNullOrWhiteSpace(step.Title) ? $"Шаг {i + 1}" : step.Title) ?? $"Шаг {i + 1}";
            string stepDesc = SecurityElement.Escape(step.Description ?? "") ?? "";
            string actionText = GetActionDisplayName(step.Action);
            string elementName = SecurityElement.Escape(step.TargetElement.Name) ?? "";
            string controlType = SecurityElement.Escape(step.TargetElement.ControlType) ?? "";
            string windowTitle = SecurityElement.Escape(step.TargetElement.WindowTitle) ?? "";
            string processName = SecurityElement.Escape(step.TargetElement.ProcessName) ?? "";

            // Заголовок шага
            sb.Append("    <w:p>\n");
            sb.Append("      <w:pPr><w:spacing w:before=\"240\" w:after=\"80\"/></w:pPr>\n");
            sb.Append("      <w:r><w:rPr><w:b/><w:sz w:val=\"32\"/><w:color w:val=\"111827\"/></w:rPr>\n");
            sb.Append($"        <w:t>Шаг {i + 1}: {stepTitle}</w:t>\n");
            sb.Append("      </w:r>\n");
            sb.Append("    </w:p>\n");

            // Описание действия
            if (!string.IsNullOrWhiteSpace(stepDesc))
            {
                sb.Append("    <w:p>\n");
                sb.Append("      <w:pPr><w:spacing w:after=\"120\"/></w:pPr>\n");
                sb.Append("      <w:r><w:rPr><w:sz w:val=\"22\"/><w:color w:val=\"374151\"/></w:rPr>\n");
                sb.Append($"        <w:t>{stepDesc}</w:t>\n");
                sb.Append("      </w:r>\n");
                sb.Append("    </w:p>\n");
            }

            // Метаданные действия (чипы)
            var metaParts = new List<string>();
            if (!string.IsNullOrEmpty(actionText)) metaParts.Add($"Действие: {actionText}");
            if (!string.IsNullOrEmpty(elementName)) metaParts.Add($"Элемент: «{elementName}» ({controlType})");
            if (!string.IsNullOrEmpty(windowTitle)) metaParts.Add($"Окно: {windowTitle}");
            if (!string.IsNullOrEmpty(processName)) metaParts.Add($"Процесс: {processName}");

            if (metaParts.Count > 0)
            {
                sb.Append("    <w:p>\n");
                sb.Append("      <w:pPr><w:spacing w:after=\"160\"/></w:pPr>\n");
                sb.Append("      <w:r><w:rPr><w:sz w:val=\"18\"/><w:color w:val=\"4B5563\"/><w:highlight w:val=\"lightGray\"/></w:rPr>\n");
                sb.Append($"        <w:t>  {string.Join("  •  ", metaParts)}  </w:t>\n");
                sb.Append("      </w:r>\n");
                sb.Append("    </w:p>\n");
            }

            // Скриншот
            if (i < imageEntries.Count && !string.IsNullOrEmpty(imageEntries[i].RelId))
            {
                var img = imageEntries[i];
                sb.Append("    <w:p>\n");
                sb.Append("      <w:pPr><w:spacing w:after=\"280\"/></w:pPr>\n");
                sb.Append("      <w:r>\n");
                sb.Append("        <w:drawing>\n");
                sb.Append($"          <wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\">\n");
                sb.Append($"            <wp:extent cx=\"{img.WidthEmus}\" cy=\"{img.HeightEmus}\"/>\n");
                sb.Append($"            <wp:docPr id=\"{i + 1}\" name=\"Step {i + 1} Screenshot\"/>\n");
                sb.Append("            <a:graphic xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\">\n");
                sb.Append("              <a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\">\n");
                sb.Append("                <pic:pic xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\">\n");
                sb.Append("                  <pic:nvPicPr>\n");
                sb.Append($"                    <pic:cNvPr id=\"{i + 1}\" name=\"image{i + 1}.png\"/>\n");
                sb.Append("                    <pic:cNvPicPr/>\n");
                sb.Append("                  </pic:nvPicPr>\n");
                sb.Append("                  <pic:blipFill>\n");
                sb.Append($"                    <a:blip r:embed=\"{img.RelId}\"/>\n");
                sb.Append("                    <a:stretch><a:fillRect/></a:stretch>\n");
                sb.Append("                  </pic:blipFill>\n");
                sb.Append("                  <pic:spPr>\n");
                sb.Append($"                    <a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{img.WidthEmus}\" cy=\"{img.HeightEmus}\"/></a:xfrm>\n");
                sb.Append("                    <a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom>\n");
                sb.Append("                  </pic:spPr>\n");
                sb.Append("                </pic:pic>\n");
                sb.Append("              </a:graphicData>\n");
                sb.Append("            </a:graphic>\n");
                sb.Append("          </wp:inline>\n");
                sb.Append("        </w:drawing>\n");
                sb.Append("      </w:r>\n");
                sb.Append("    </w:p>\n");
            }
        }

        sb.Append("  </w:body>\n");
        sb.Append("</w:document>");
        return sb.ToString();
    }

    private string BuildHtmlDocument(Project project, IReadOnlyList<Step> steps, CancellationToken ct)
    {
        var sb = new StringBuilder();
        string docTitle = SecurityElement.Escape(string.IsNullOrWhiteSpace(project.Name) ? "Пошаговое руководство" : project.Name) ?? "Руководство";
        string docDesc = SecurityElement.Escape(project.Description ?? "") ?? "";

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"ru\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"  <title>{docTitle}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    :root { --primary: #0f6cbd; --bg: #f8fafc; --card: #ffffff; --text: #0f172a; --muted: #64748b; --border: #e2e8f0; }");
        sb.AppendLine("    * { box-sizing: border-box; margin: 0; padding: 0; }");
        sb.AppendLine("    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: var(--bg); color: var(--text); line-height: 1.6; padding: 24px 16px; }");
        sb.AppendLine("    .container { max-width: 960px; margin: 0 auto; }");
        sb.AppendLine("    .header { background: var(--card); border: 1px solid var(--border); border-radius: 12px; padding: 32px; margin-bottom: 24px; box-shadow: 0 2px 8px rgba(0,0,0,0.04); }");
        sb.AppendLine("    .header h1 { font-size: 28px; color: var(--primary); margin-bottom: 8px; font-weight: 700; }");
        sb.AppendLine("    .header p { color: var(--muted); font-size: 15px; margin-bottom: 16px; }");
        sb.AppendLine("    .header-meta { display: flex; gap: 16px; font-size: 13px; color: var(--muted); border-top: 1px solid var(--border); padding-top: 12px; }");
        sb.AppendLine("    .step-card { background: var(--card); border: 1px solid var(--border); border-radius: 12px; padding: 24px; margin-bottom: 24px; box-shadow: 0 2px 6px rgba(0,0,0,0.03); page-break-inside: avoid; }");
        sb.AppendLine("    .step-header { display: flex; align-items: center; gap: 12px; margin-bottom: 12px; }");
        sb.AppendLine("    .step-badge { background: var(--primary); color: white; width: 28px; height: 28px; border-radius: 50%; display: flex; align-items: center; justify-content: center; font-size: 14px; font-weight: 700; flex-shrink: 0; }");
        sb.AppendLine("    .step-title { font-size: 18px; font-weight: 600; color: var(--text); }");
        sb.AppendLine("    .step-desc { font-size: 14px; color: #334155; margin-bottom: 16px; white-space: pre-wrap; }");
        sb.AppendLine("    .meta-chips { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 16px; }");
        sb.AppendLine("    .chip { background: #f1f5f9; border: 1px solid #cbd5e1; border-radius: 6px; padding: 4px 10px; font-size: 12px; color: #475569; }");
        sb.AppendLine("    .chip strong { color: var(--text); }");
        sb.AppendLine("    .screenshot-container { border-radius: 8px; overflow: hidden; border: 1px solid var(--border); background: #000; text-align: center; }");
        sb.AppendLine("    .screenshot-container img { max-width: 100%; height: auto; display: block; margin: 0 auto; }");
        sb.AppendLine("    @media print {");
        sb.AppendLine("      body { background: #fff; padding: 0; }");
        sb.AppendLine("      .container { max-width: 100%; }");
        sb.AppendLine("      .header, .step-card { box-shadow: none; border-color: #ddd; }");
        sb.AppendLine("      .step-card { page-break-inside: avoid; }");
        sb.AppendLine("    }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"container\">");
        sb.AppendLine("    <div class=\"header\">");
        sb.AppendLine($"      <h1>{docTitle}</h1>");
        if (!string.IsNullOrEmpty(docDesc))
        {
            sb.AppendLine($"      <p>{docDesc}</p>");
        }
        sb.AppendLine("      <div class=\"header-meta\">");
        sb.AppendLine($"        <span>Всего шагов: <strong>{steps.Count}</strong></span>");
        sb.AppendLine($"        <span>Сформировано: <strong>{DateTime.Now:dd.MM.yyyy HH:mm}</strong></span>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");

        for (int i = 0; i < steps.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var step = steps[i];
            string stepTitle = SecurityElement.Escape(string.IsNullOrWhiteSpace(step.Title) ? $"Шаг {i + 1}" : step.Title) ?? $"Шаг {i + 1}";
            string stepDesc = SecurityElement.Escape(step.Description ?? "") ?? "";
            string actionText = GetActionDisplayName(step.Action);
            string elementName = SecurityElement.Escape(step.TargetElement.Name) ?? "";
            string controlType = SecurityElement.Escape(step.TargetElement.ControlType) ?? "";
            string windowTitle = SecurityElement.Escape(step.TargetElement.WindowTitle) ?? "";
            string processName = SecurityElement.Escape(step.TargetElement.ProcessName) ?? "";

            sb.AppendLine("    <div class=\"step-card\">");
            sb.AppendLine("      <div class=\"step-header\">");
            sb.AppendLine($"        <div class=\"step-badge\">{i + 1}</div>");
            sb.AppendLine($"        <div class=\"step-title\">{stepTitle}</div>");
            sb.AppendLine("      </div>");

            if (!string.IsNullOrEmpty(stepDesc))
            {
                sb.AppendLine($"      <div class=\"step-desc\">{stepDesc}</div>");
            }

            sb.AppendLine("      <div class=\"meta-chips\">");
            if (!string.IsNullOrEmpty(actionText)) sb.AppendLine($"        <div class=\"chip\"><strong>Действие:</strong> {actionText}</div>");
            if (!string.IsNullOrEmpty(elementName)) sb.AppendLine($"        <div class=\"chip\"><strong>Элемент:</strong> {elementName} ({controlType})</div>");
            if (!string.IsNullOrEmpty(windowTitle)) sb.AppendLine($"        <div class=\"chip\"><strong>Окно:</strong> {windowTitle}</div>");
            if (!string.IsNullOrEmpty(processName)) sb.AppendLine($"        <div class=\"chip\"><strong>Процесс:</strong> {processName}</div>");
            sb.AppendLine("      </div>");

            var fullPath = ResolveScreenshotPath(step.ScreenshotPath);
            if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
            {
                try
                {
                    var bytes = File.ReadAllBytes(fullPath);
                    if (bytes.Length > 0)
                    {
                        var base64 = Convert.ToBase64String(bytes);
                        sb.AppendLine("      <div class=\"screenshot-container\">");
                        sb.AppendLine($"        <img src=\"data:image/png;base64,{base64}\" alt=\"Скриншот шага {i + 1}\" />");
                        sb.AppendLine("      </div>");
                    }
                }
                catch { }
            }

            sb.AppendLine("    </div>");
        }

        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private void GenerateStandardPdf(Stream stream, Project project, IReadOnlyList<Step> steps, CancellationToken ct)
    {
        // Создаем чистый, валидный PDF 1.4 документ
        // Поддерживает заголовки, оглавление, шаги и встроенные графические скриншоты
        using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);
        var offsets = new List<long>();

        long WriteObject(string content)
        {
            writer.Flush();
            long offset = stream.Position;
            offsets.Add(offset);
            writer.Write($"{offsets.Count} 0 obj\n{content}\nendobj\n");
            writer.Flush();
            return offset;
        }

        writer.Write("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");
        writer.Flush();

        // 1. Catalog
        WriteObject("<< /Type /Catalog /Pages 2 0 R >>");

        // Подготавливаем объекты страниц
        var pageObjIds = new List<int>();
        int firstPageObjId = 3;

        // В простейшей структуре PDF: Каждая страница содержит карточку шага
        // Подсчитаем количество страниц: 1 титульная + N шагов
        int totalPages = 1 + steps.Count;
        for (int p = 0; p < totalPages; p++)
        {
            pageObjIds.Add(firstPageObjId + p * 2); // Page, Content
        }

        // 2. Pages
        var kidsStr = string.Join(" ", pageObjIds.ConvertAll(id => $"{id} 0 R"));
        WriteObject($"<< /Type /Pages /Kids [ {kidsStr} ] /Count {totalPages} >>");

        // Титульная страница (Page 1)
        string titleText = project.Name ?? "Stepwise User Guide";
        string descText = project.Description ?? $"Steps: {steps.Count} - Generated on {DateTime.Now:yyyy-MM-dd}";

        var page1Content =
            "BT\n" +
            "/F1 24 Tf\n" +
            "50 750 Td\n" +
            $"({EscapePdfString(titleText)}) Tj\n" +
            "/F1 12 Tf\n" +
            "0 -40 Td\n" +
            $"({EscapePdfString(descText)}) Tj\n" +
            "0 -30 Td\n" +
            $"(Generated by Stepwise Interactive Walkthrough Engine) Tj\n" +
            "0 -20 Td\n" +
            $"(Total Recorded Steps: {steps.Count}) Tj\n" +
            "ET\n";

        byte[] page1ContentBytes = Encoding.ASCII.GetBytes(page1Content);
        // Page 1
        WriteObject($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >> >> >> /Contents 4 0 R >>");
        // Content 1
        WriteObject($"<< /Length {page1ContentBytes.Length} >>\nstream\n{page1Content}\nendstream");

        // Страницы для каждого шага
        int currentObj = 5;
        for (int i = 0; i < steps.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var step = steps[i];
            string stepTitle = $"Step {i + 1}: {(string.IsNullOrWhiteSpace(step.Title) ? "Action" : step.Title)}";
            string stepDesc = step.Description ?? "";
            string stepMeta = $"Action: {step.Action} | Target: {step.TargetElement.Name} ({step.TargetElement.ControlType})";

            var stepContent =
                "BT\n" +
                "/F1 16 Tf\n" +
                "50 780 Td\n" +
                $"({EscapePdfString(stepTitle)}) Tj\n" +
                "/F2 11 Tf\n" +
                "0 -30 Td\n" +
                $"({EscapePdfString(stepMeta)}) Tj\n";

            if (!string.IsNullOrWhiteSpace(stepDesc))
            {
                stepContent +=
                    "0 -25 Td\n" +
                    $"({EscapePdfString(stepDesc)}) Tj\n";
            }

            stepContent += "ET\n";

            byte[] stepContentBytes = Encoding.ASCII.GetBytes(stepContent);
            int contentObjId = currentObj + 1;

            // Page
            WriteObject($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >> /F2 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >> /Contents {contentObjId} 0 R >>");
            // Content
            WriteObject($"<< /Length {stepContentBytes.Length} >>\nstream\n{stepContent}\nendstream");

            currentObj += 2;
        }

        // XRef Table
        writer.Flush();
        long xrefOffset = stream.Position;
        writer.Write($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var off in offsets)
        {
            writer.Write($"{off:D10} 00000 n \n");
        }

        // Trailer
        writer.Write($"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        writer.Flush();
    }

    private static string EscapePdfString(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        var sb = new StringBuilder();
        foreach (char c in text)
        {
            if (c == '(' || c == ')' || c == '\\')
            {
                sb.Append('\\').Append(c);
            }
            else if (c < 128)
            {
                sb.Append(c);
            }
            else
            {
                // Для символов за пределами ASCII используем translit или пробел для Helvetica
                sb.Append(TransliterateChar(c));
            }
        }
        return sb.ToString();
    }

    private static string TransliterateChar(char c) => c switch
    {
        'А' => "A", 'а' => "a", 'Б' => "B", 'б' => "b", 'В' => "V", 'в' => "v",
        'Г' => "G", 'г' => "g", 'Д' => "D", 'д' => "d", 'Е' => "E", 'е' => "e",
        'Ё' => "Yo", 'ё' => "yo", 'Ж' => "Zh", 'ж' => "zh", 'З' => "Z", 'з' => "z",
        'И' => "I", 'и' => "i", 'Й' => "Y", 'й' => "y", 'К' => "K", 'к' => "k",
        'Л' => "L", 'л' => "l", 'М' => "M", 'м' => "m", 'Н' => "N", 'н' => "n",
        'О' => "O", 'о' => "o", 'П' => "P", 'п' => "p", 'Р' => "R", 'р' => "r",
        'С' => "S", 'с' => "s", 'Т' => "T", 'т' => "t", 'У' => "U", 'у' => "u",
        'Ф' => "F", 'ф' => "f", 'Х' => "Kh", 'х' => "kh", 'Ц' => "Ts", 'ц' => "ts",
        'Ч' => "Ch", 'ч' => "ch", 'Ш' => "Sh", 'ш' => "sh", 'Щ' => "Shch", 'щ' => "shch",
        'Ъ' => "", 'ъ' => "", 'Ы' => "Y", 'ы' => "y", 'Ь' => "", 'ь' => "",
        'Э' => "E", 'э' => "e", 'Ю' => "Yu", 'ю' => "yu", 'Я' => "Ya", 'я' => "ya",
        '«' => "\"", '»' => "\"", '—' => "-", '–' => "-",
        _ => " "
    };

    private static string GetActionDisplayName(ActionType actionType) => actionType switch
    {
        ActionType.LeftClick => "Левый клик мыши",
        ActionType.RightClick => "Правый клик мыши",
        ActionType.DoubleLeftClick => "Двойной клик мыши",
        ActionType.MiddleClick => "Клик колесиком мыши",
        ActionType.TextInput => "Ввод текста",
        ActionType.KeyPress => "Нажатие клавиши",
        ActionType.Shortcut => "Горячая клавиша",
        ActionType.DragAndDrop => "Перетаскивание (Drag & Drop)",
        ActionType.Scroll => "Прокрутка",
        ActionType.WindowActivated => "Переключение окна",
        ActionType.WindowClosed => "Закрытие окна",
        ActionType.ManualStep => "Пользовательский шаг",
        _ => actionType.ToString()
    };
}
