using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SecondSelf.Core.Models;

namespace SecondSelf.Core.Services;

/// <summary>
/// Реализация сервиса управления пользовательскими настройками приложения с сохранением в JSON.
/// </summary>
public sealed class AppSettingsService : IAppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly SemaphoreSlim _lock = new(1, 1);
    private AppSettings _currentSettings = new();

    public string SettingsFilePath { get; }

    public AppSettings CurrentSettings => _currentSettings;

    public AppSettingsService(string? customSettingsPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customSettingsPath))
        {
            SettingsFilePath = Path.GetFullPath(customSettingsPath);
        }
        else
        {
            var envFilePath = Environment.GetEnvironmentVariable("SECONDSELF_APP_SETTINGS_PATH");
            if (!string.IsNullOrWhiteSpace(envFilePath))
            {
                SettingsFilePath = Path.GetFullPath(envFilePath);
            }
            else
            {
                var appDataRoot = Environment.GetEnvironmentVariable("SECONDSELF_APP_DATA");
                if (!string.IsNullOrWhiteSpace(appDataRoot))
                {
                    SettingsFilePath = Path.Combine(Path.GetFullPath(appDataRoot), "Settings", "app_settings.json");
                }
                else
                {
                    SettingsFilePath = Path.Combine(AppContext.BaseDirectory, "Settings", "app_settings.json");
                }
            }
        }
    }

    /// <inheritdoc />
    public async Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                _currentSettings = new AppSettings();
                return _currentSettings;
            }

            var json = await File.ReadAllTextAsync(SettingsFilePath, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                _currentSettings = new AppSettings();
                return _currentSettings;
            }

            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (settings is null)
            {
                _currentSettings = new AppSettings();
                return _currentSettings;
            }

            // Если веса выходят за допустимые границы, корректируем на дефолтные
            if (settings.DailyWeight < AppSettings.MinDailyWeight || settings.DailyWeight > AppSettings.MaxDailyWeight)
            {
                settings.DailyWeight = AppSettings.DefaultDailyWeight;
            }
            settings.WeeklyWeight = Math.Round(1.0 - settings.DailyWeight, 4);

            _currentSettings = settings;
            return _currentSettings;
        }
        catch
        {
            _currentSettings = new AppSettings();
            return _currentSettings;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ValidateDailyWeight(settings.DailyWeight);

        settings.WeeklyWeight = Math.Round(1.0 - settings.DailyWeight, 4);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            await File.WriteAllTextAsync(SettingsFilePath, json, cancellationToken).ConfigureAwait(false);
            _currentSettings = settings;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<AppSettings> UpdateWeightsAsync(double dailyWeight, CancellationToken cancellationToken = default)
    {
        ValidateDailyWeight(dailyWeight);

        var current = await GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        current.DailyWeight = dailyWeight;
        current.WeeklyWeight = Math.Round(1.0 - dailyWeight, 4);

        await SaveSettingsAsync(current, cancellationToken).ConfigureAwait(false);
        return current;
    }

    private static void ValidateDailyWeight(double dailyWeight)
    {
        if (dailyWeight < AppSettings.MinDailyWeight || dailyWeight > AppSettings.MaxDailyWeight)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dailyWeight),
                dailyWeight,
                $"Вес ежедневного ритуала ({dailyWeight:P0}) должен находиться в диапазоне от {AppSettings.MinDailyWeight:P0} (10%) до {AppSettings.MaxDailyWeight:P0} (50%).");
        }
    }
}
