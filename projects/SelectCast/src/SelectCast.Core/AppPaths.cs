using System.IO;

namespace SelectCast.Core;

/// <summary>
/// Where SelectCast keeps its per-user data (settings.json, rates.json). Defaults to
/// <c>%AppData%\SelectCast</c>, but honours the <c>SELECTCAST_DATA_DIR</c> environment variable
/// so an E2E test (or a portable setup) can redirect it to an isolated folder and never touch
/// the real user's settings, rates cache, or registry.
/// </summary>
public static class AppPaths
{
    public static string DataDir()
    {
        string? overrideDir = Environment.GetEnvironmentVariable("SELECTCAST_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(overrideDir))
            return overrideDir;

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SelectCast");
    }
}
