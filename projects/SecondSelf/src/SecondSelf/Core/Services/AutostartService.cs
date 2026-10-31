using System;
using System.IO;
using Microsoft.Win32;

namespace SecondSelf.Core.Services;

/// <summary>
/// Реализация управления автозапуском приложения через ветку реестра Windows HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
/// </summary>
public sealed class AutostartService : IAutostartService
{
    public const string DefaultRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string DefaultValueName = "SecondSelf";
    public const string StartupBackgroundArgument = "--startup-background";

    private readonly string _registryPath;
    private readonly string _valueName;
    private readonly string? _customExecutablePath;

    public AutostartService(
        string registryPath = DefaultRegistryPath,
        string valueName = DefaultValueName,
        string? customExecutablePath = null)
    {
        _registryPath = registryPath;
        _valueName = valueName;
        _customExecutablePath = customExecutablePath;
    }

    /// <inheritdoc />
    public bool IsAutostartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_registryPath, writable: false);
            if (key == null)
            {
                return false;
            }

            var value = key.GetValue(_valueName) as string;
            return !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public void SetAutostart(bool enable)
    {
        if (enable)
        {
            using var key = Registry.CurrentUser.CreateSubKey(_registryPath, writable: true);
            if (key != null)
            {
                key.SetValue(_valueName, GetAutostartCommandLine(), RegistryValueKind.String);
            }
        }
        else
        {
            using var key = Registry.CurrentUser.OpenSubKey(_registryPath, writable: true);
            key?.DeleteValue(_valueName, throwOnMissingValue: false);
        }
    }

    /// <inheritdoc />
    public string GetAutostartCommandLine()
    {
        var exePath = _customExecutablePath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            exePath = Environment.ProcessPath;
        }

        if (string.IsNullOrWhiteSpace(exePath))
        {
            exePath = Path.Combine(AppContext.BaseDirectory, "SecondSelf.exe");
        }

        return $"\"{exePath}\" {StartupBackgroundArgument}";
    }
}
