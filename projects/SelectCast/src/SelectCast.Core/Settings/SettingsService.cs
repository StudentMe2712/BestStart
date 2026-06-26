using System.IO;
using System.Text.Json;

namespace SelectCast.Core.Settings;

/// <summary>Loads/saves <see cref="AppSettings"/> as JSON in the user profile (same dir as rates cache).</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly string _path;

    public SettingsService(string? path = null) => _path = path ?? DefaultPath();

    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SelectCast",
        "settings.json");

    /// <summary>Returns saved settings, or defaults if the file is missing/corrupt.</summary>
    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new AppSettings();
        }
        catch
        {
            // Corrupt/unreadable settings — defaults rather than a crash on startup.
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, Json));
        }
        catch
        {
            // Best-effort; a failed save must not take the app down.
        }
    }
}
