using System.Diagnostics;
using Microsoft.Win32;

namespace PersonalProvocateur.App.WindowsInterop;

/// <summary>
/// Manages Windows startup configuration via the HKCU Run registry key.
/// Safe and operable without administrator privileges.
/// </summary>
public static class StartupManager
{
    public const string AppName = "PersonalProvocateur";
    public const string DefaultRunRegistrySubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// Gets the formatted executable path for the current process enclosed in quotes.
    /// </summary>
    public static string GetExecutablePath()
    {
        var rawPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            try
            {
                rawPath = Process.GetCurrentProcess().MainModule?.FileName;
            }
            catch
            {
                // Fallback
            }
        }

        return FormatExecutablePath(rawPath ?? string.Empty);
    }

    /// <summary>
    /// Normalizes and formats an executable path string, trimming existing outer quotes and wrapping in double quotes.
    /// </summary>
    public static string FormatExecutablePath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return string.Empty;
        }

        var trimmed = rawPath.Trim().Trim('\"');
        return $"\"{trimmed}\"";
    }

    /// <summary>
    /// Determines whether the application is configured to run at Windows startup.
    /// </summary>
    /// <param name="subKey">Registry subkey path under CurrentUser (defaults to standard Run key).</param>
    /// <returns>True if the run entry exists and is non-empty; otherwise false.</returns>
    public static bool IsRunOnStartupEnabled(string subKey = DefaultRunRegistrySubKey)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(subKey, false);
            if (key == null)
            {
                return false;
            }

            var value = key.GetValue(AppName) as string;
            return !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Enables or disables application automatic startup on Windows login for the current user.
    /// </summary>
    /// <param name="enable">Whether auto-startup should be enabled.</param>
    /// <param name="subKey">Registry subkey path under CurrentUser (defaults to standard Run key).</param>
    /// <param name="customExePath">Optional custom executable path (useful for testing).</param>
    /// <returns>True if registry was successfully updated; otherwise false.</returns>
    public static bool SetRunOnStartup(bool enable, string subKey = DefaultRunRegistrySubKey, string? customExePath = null)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(subKey, true);
            if (key == null)
            {
                using var createdKey = Registry.CurrentUser.CreateSubKey(subKey, true);
                if (createdKey == null)
                {
                    return false;
                }
                return UpdateRegistryValue(createdKey, enable, customExePath);
            }

            return UpdateRegistryValue(key, enable, customExePath);
        }
        catch
        {
            return false;
        }
    }

    private static bool UpdateRegistryValue(RegistryKey key, bool enable, string? customExePath)
    {
        if (enable)
        {
            var exePath = !string.IsNullOrWhiteSpace(customExePath)
                ? FormatExecutablePath(customExePath)
                : GetExecutablePath();

            if (string.IsNullOrWhiteSpace(exePath))
            {
                return false;
            }

            key.SetValue(AppName, exePath, RegistryValueKind.String);
            return true;
        }
        else
        {
            if (key.GetValue(AppName) != null)
            {
                key.DeleteValue(AppName, false);
            }
            return true;
        }
    }
}
