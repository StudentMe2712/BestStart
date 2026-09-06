using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;

namespace Stepwise.Core.AI;

/// <summary>
/// Реализация <see cref="IGroqAIService"/> для интеграции с Groq Cloud API.
/// Предоставляет генерацию заголовков и описаний шагов на базе моделей openai/gpt-oss-120b и qwen/qwen3.8-27b.
/// Работает безопасно: при отсутствии ключа или сбое сети не прерывает работу приложения.
/// </summary>
public sealed class GroqAIService : IGroqAIService
{
    public const string DefaultModel = "openai/gpt-oss-120b";
    public const string QwenModel = "qwen/qwen3.8-27b";
    public const string LlamaModel = "llama-3.3-70b-versatile";

    private const string GroqEndpoint = "https://api.groq.com/openai/v1/chat/completions";

    private readonly HttpClient _httpClient;
    private readonly string _settingsFilePath;
    private readonly object _settingsLock = new();

    private string? _storedApiKey;
    private string _selectedModel = DefaultModel;

    public string? StoredApiKey
    {
        get
        {
            lock (_settingsLock) return _storedApiKey;
        }
        set
        {
            lock (_settingsLock)
            {
                _storedApiKey = value;
                SaveSettingsLocked();
            }
        }
    }

    public string SelectedModel
    {
        get
        {
            lock (_settingsLock) return _selectedModel;
        }
        set
        {
            lock (_settingsLock)
            {
                _selectedModel = string.IsNullOrWhiteSpace(value) ? DefaultModel : value;
                SaveSettingsLocked();
            }
        }
    }

    public GroqAIService(HttpClient? httpClient = null, string? customSettingsDir = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(25) };

        var settingsDir = !string.IsNullOrWhiteSpace(customSettingsDir)
            ? customSettingsDir
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Stepwise");

        Directory.CreateDirectory(settingsDir);
        _settingsFilePath = Path.Combine(settingsDir, "ai_settings.json");

        LoadSettings();
    }

    public async Task<GroqEnhanceResult> EnhanceStepAsync(
        Step step,
        string? apiKey = null,
        string? model = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(step);

        string effectiveKey = !string.IsNullOrWhiteSpace(apiKey)
            ? apiKey
            : StoredApiKey ?? Environment.GetEnvironmentVariable("GROQ_API_KEY") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(effectiveKey))
        {
            return new GroqEnhanceResult(
                Title: step.Title ?? $"Шаг {step.SequenceIndex}",
                Description: step.Description ?? string.Empty,
                Success: false,
                ErrorMessage: "API-ключ Groq не настроен. Укажите ключ в настройках или в диалоге."
            );
        }

        string effectiveModel = !string.IsNullOrWhiteSpace(model) ? model : SelectedModel;

        try
        {
            var systemPrompt =
                "Ты — профессиональный технический писатель и эксперт по регламентам и пользовательским инструкциям к корпоративному ПО (1С:Предприятие, ERP, CRM, офисные пакеты). " +
                "Твоя задача — на основе сырых метаданных действия пользователя в Windows сформировать лаконичный, понятный заголовок шага (до 7 слов в повелительном наклонении, например: «Нажмите на раздел «Администрирование»») и подробное понятное пояснение для сотрудника. " +
                "Ответ верни СТРОГО В ФОРМАТЕ JSON с двумя полями: " +
                "{\"title\": \"Заголовок шага\", \"description\": \"Подробное описание действия пользователя\"}";

            var userPrompt =
                $"Действие: {step.Action}\n" +
                $"Элемент управления: {(string.IsNullOrWhiteSpace(step.TargetElement.Name) ? "Без имени" : step.TargetElement.Name)} (Тип: {step.TargetElement.ControlType})\n" +
                $"Окно: {(string.IsNullOrWhiteSpace(step.TargetElement.WindowTitle) ? "Не определено" : step.TargetElement.WindowTitle)}\n" +
                $"Процесс: {step.TargetElement.ProcessName}\n" +
                $"Координаты клика: X={step.ClickX}, Y={step.ClickY}\n" +
                $"Текущий черновик заголовка: {step.Title}\n" +
                $"Текущий черновик описания: {step.Description}";

            var payload = new
            {
                model = effectiveModel,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.2
            };

            var jsonContent = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, GroqEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", effectiveKey);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return new GroqEnhanceResult(
                    Title: step.Title ?? $"Шаг {step.SequenceIndex}",
                    Description: step.Description ?? string.Empty,
                    Success: false,
                    ErrorMessage: $"Ошибка Groq API (HTTP {(int)response.StatusCode}): {errorBody}"
                );
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var jsonNode = JsonNode.Parse(responseBody);
            var contentStr = jsonNode?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(contentStr))
            {
                return new GroqEnhanceResult(
                    Title: step.Title ?? $"Шаг {step.SequenceIndex}",
                    Description: step.Description ?? string.Empty,
                    Success: false,
                    ErrorMessage: "Пустой ответ от нейросети Groq."
                );
            }

            var parsed = JsonNode.Parse(contentStr);
            string title = parsed?["title"]?.GetValue<string>() ?? step.Title ?? $"Шаг {step.SequenceIndex}";
            string description = parsed?["description"]?.GetValue<string>() ?? step.Description ?? string.Empty;

            return new GroqEnhanceResult(title, description, Success: true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GroqAIService] Ошибка генерации: {ex.Message}");
            return new GroqEnhanceResult(
                Title: step.Title ?? $"Шаг {step.SequenceIndex}",
                Description: step.Description ?? string.Empty,
                Success: false,
                ErrorMessage: $"Исключение при обращении к Groq: {ex.Message}"
            );
        }
    }

    public async Task<IReadOnlyList<Step>> EnhanceAllStepsAsync(
        IReadOnlyList<Step> steps,
        string? apiKey = null,
        string? model = null,
        IProgress<(int Current, int Total)>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var resultList = new List<Step>(steps.Count);
        for (int i = 0; i < steps.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var step = steps[i];

            var enhanced = await EnhanceStepAsync(step, apiKey, model, ct).ConfigureAwait(false);
            if (enhanced.Success)
            {
                resultList.Add(step with
                {
                    Title = enhanced.Title,
                    Description = enhanced.Description
                });
            }
            else
            {
                resultList.Add(step);
            }

            progress?.Report((i + 1, steps.Count));
        }

        return resultList;
    }

    private void LoadSettings()
    {
        lock (_settingsLock)
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var text = File.ReadAllText(_settingsFilePath);
                    var node = JsonNode.Parse(text);
                    _storedApiKey = node?["apiKey"]?.GetValue<string>();
                    var savedModel = node?["model"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(savedModel))
                    {
                        _selectedModel = savedModel;
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки повреждения файла настроек
            }
        }
    }

    private void SaveSettingsLocked()
    {
        try
        {
            var node = new JsonObject
            {
                ["apiKey"] = _storedApiKey,
                ["model"] = _selectedModel
            };
            File.WriteAllText(_settingsFilePath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GroqAIService] Не удалось сохранить настройки: {ex.Message}");
        }
    }
}
