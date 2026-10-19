using System.IO;

namespace PersonalProvocateur.App.Storage;

/// <summary>
/// Provides application file paths and directories according to the offline-first specification.
/// </summary>
public static class AppPaths
{
    /// <summary>
    /// Gets the application data directory under %LOCALAPPDATA%\PersonalProvocateur.
    /// </summary>
    public static string AppDataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PersonalProvocateur");

    /// <summary>
    /// Gets the full path to the settings JSON file.
    /// </summary>
    public static string SettingsFilePath { get; } =
        Path.Combine(AppDataDirectory, "settings.json");

    /// <summary>
    /// Ensures that the application data directory exists on disk.
    /// </summary>
    public static void EnsureAppDataDirectory()
    {
        if (!Directory.Exists(AppDataDirectory))
        {
            Directory.CreateDirectory(AppDataDirectory);
        }
    }
}
