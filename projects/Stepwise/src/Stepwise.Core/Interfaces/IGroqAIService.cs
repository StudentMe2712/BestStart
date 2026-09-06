using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stepwise.Core.Models;

namespace Stepwise.Core.Interfaces;

/// <summary>
/// Результат генерации заголовка и описания шага через Groq AI.
/// </summary>
public sealed record GroqEnhanceResult(
    string Title,
    string Description,
    bool Success,
    string? ErrorMessage = null
);

/// <summary>
/// Сервис интеграции с бесплатным API Groq Cloud (openai/gpt-oss-120b, qwen/qwen3.8-27b, llama-3.3-70b-versatile)
/// для автоматического и точного синтеза профессиональных заголовков и описаний шагов на основе телеметрии UI.
/// </summary>
public interface IGroqAIService
{
    /// <summary>
    /// Сохраненный локально API-ключ Groq.
    /// </summary>
    string? StoredApiKey { get; set; }

    /// <summary>
    /// Выбранная модель нейросети (по умолчанию openai/gpt-oss-120b).
    /// </summary>
    string SelectedModel { get; set; }

    /// <summary>
    /// Улучшает заголовок и описание одного выбранного шага.
    /// </summary>
    Task<GroqEnhanceResult> EnhanceStepAsync(Step step, string? apiKey = null, string? model = null, CancellationToken ct = default);

    /// <summary>
    /// Выполняет пакетное улучшение всех шагов руководства с отчетом о прогрессе.
    /// </summary>
    Task<IReadOnlyList<Step>> EnhanceAllStepsAsync(
        IReadOnlyList<Step> steps,
        string? apiKey = null,
        string? model = null,
        IProgress<(int Current, int Total)>? progress = null,
        CancellationToken ct = default);
}
