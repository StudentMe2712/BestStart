using System.IO;
using System.Text.Json;
using PersonalProvocateur.App.Models;

namespace PersonalProvocateur.App.Storage;

/// <summary>
/// Provides thread-safe persistence and retrieval of application settings using System.Text.Json.
/// Implements offline-first fallback on missing or corrupted configuration files.
/// </summary>
public class SettingsStore
{
    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _settingsFilePath;
    private readonly object _lock = new();

    /// <summary>
    /// Occurs when settings have been updated and saved.
    /// </summary>
    public event EventHandler<AppSettings>? SettingsChanged;

    /// <summary>
    /// Gets the current loaded in-memory application settings.
    /// </summary>
    public AppSettings CurrentSettings { get; private set; }

    /// <summary>
    /// Alias for <see cref="CurrentSettings"/> for backward compatibility and concise access.
    /// </summary>
    public AppSettings Settings => CurrentSettings;

    /// <summary>
    /// Initializes a new instance of <see cref="SettingsStore"/>.
    /// </summary>
    /// <param name="settingsFilePath">Custom settings file path. If null, defaults to <see cref="AppPaths.SettingsFilePath"/>.</param>
    public SettingsStore(string? settingsFilePath = null)
    {
        _settingsFilePath = string.IsNullOrWhiteSpace(settingsFilePath)
            ? AppPaths.SettingsFilePath
            : settingsFilePath;

        CurrentSettings = Load();
    }

    /// <summary>
    /// Loads settings from disk. Returns default sanitized settings if file is absent or corrupted.
    /// </summary>
    public AppSettings Load()
    {
        lock (_lock)
        {
            if (!File.Exists(_settingsFilePath))
            {
                var defaultSettings = new AppSettings();
                defaultSettings.Sanitize();
                CurrentSettings = defaultSettings;
                return defaultSettings;
            }

            try
            {
                var json = File.ReadAllText(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, DefaultJsonOptions);

                if (loaded == null)
                {
                    loaded = new AppSettings();
                }

                loaded.Sanitize();
                CurrentSettings = loaded;
                return loaded;
            }
            catch (Exception)
            {
                // Resilient fallback on damaged or invalid JSON as required by spec
                var fallback = new AppSettings();
                fallback.Sanitize();
                CurrentSettings = fallback;
                return fallback;
            }
        }
    }

    /// <summary>
    /// Saves application settings to disk in a thread-safe manner.
    /// </summary>
    /// <param name="settings">The settings to save. If null, saves <see cref="CurrentSettings"/>.</param>
    public void Save(AppSettings? settings = null)
    {
        AppSettings settingsToSave;

        lock (_lock)
        {
            if (settings != null)
            {
                CurrentSettings = settings;
            }

            CurrentSettings.Sanitize();
            settingsToSave = CurrentSettings;

            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settingsToSave, DefaultJsonOptions);
            File.WriteAllText(_settingsFilePath, json);
        }

        SettingsChanged?.Invoke(this, settingsToSave);
    }

    /// <summary>
    /// Atomically executes an update action on the current settings and saves the result to disk.
    /// </summary>
    /// <param name="updateAction">The action to apply to settings.</param>
    public void Update(Action<AppSettings> updateAction)
    {
        ArgumentNullException.ThrowIfNull(updateAction);

        AppSettings settingsToSave;

        lock (_lock)
        {
            updateAction(CurrentSettings);
            CurrentSettings.Sanitize();
            settingsToSave = CurrentSettings;

            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settingsToSave, DefaultJsonOptions);
            File.WriteAllText(_settingsFilePath, json);
        }

        SettingsChanged?.Invoke(this, settingsToSave);
    }
}
