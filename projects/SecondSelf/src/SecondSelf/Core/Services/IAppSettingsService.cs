using System.Threading;
using System.Threading.Tasks;
using SecondSelf.Core.Models;

namespace SecondSelf.Core.Services;

/// <summary>
/// Сервис управления и сохранения пользовательских настроек приложения (Settings/app_settings.json).
/// </summary>
public interface IAppSettingsService
{
    /// <summary>
    /// Полный путь к файлу конфигурации (app_settings.json).
    /// </summary>
    string SettingsFilePath { get; }

    /// <summary>
    /// Текущие кэшированные настройки приложения.
    /// </summary>
    AppSettings CurrentSettings { get; }

    /// <summary>
    /// Загружает настройки из JSON-файла. При отсутствии или повреждении возвращает настройки по умолчанию.
    /// </summary>
    Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Сохраняет настройки приложения в JSON-файл. Выполняет валидацию весов (DailyWeight от 0.10 до 0.50).
    /// </summary>
    Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Обновляет веса прогресса (DailyWeight и WeeklyWeight = 1.0 - DailyWeight) и сохраняет их в хранилище.
    /// </summary>
    Task<AppSettings> UpdateWeightsAsync(double dailyWeight, CancellationToken cancellationToken = default);
}
