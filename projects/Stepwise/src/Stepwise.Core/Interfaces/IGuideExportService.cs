using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stepwise.Core.Models;

namespace Stepwise.Core.Interfaces;

/// <summary>
/// Сервис экспорта интерактивных руководств в документированные форматы (Word, PDF, HTML).
/// Формирует структурированные документы с оглавлением, карточками шагов, контекстной информацией и скриншотами.
/// </summary>
public interface IGuideExportService
{
    /// <summary>
    /// Экспортирует руководство в документ Microsoft Word (.docx).
    /// </summary>
    Task ExportToDocxAsync(Project project, IReadOnlyList<Step> steps, string outputPath, CancellationToken ct = default);

    /// <summary>
    /// Экспортирует руководство в документ PDF (.pdf).
    /// </summary>
    Task ExportToPdfAsync(Project project, IReadOnlyList<Step> steps, string outputPath, CancellationToken ct = default);

    /// <summary>
    /// Экспортирует руководство в автономный HTML-документ (.html) с встроенными изображениями, готовый к печати.
    /// </summary>
    Task ExportToHtmlAsync(Project project, IReadOnlyList<Step> steps, string outputPath, CancellationToken ct = default);
}
