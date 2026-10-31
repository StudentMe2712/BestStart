using System;
using Microsoft.Win32;
using SecondSelf.Core.Services;
using Xunit;

namespace SecondSelf.Tests;

public class AutostartServiceTests : IDisposable
{
    private readonly string _testRegistrySubKey;
    private readonly string _testValueName;
    private readonly string _testExePath;

    public AutostartServiceTests()
    {
        _testRegistrySubKey = $@"Software\SecondSelfTests_{Guid.NewGuid():N}\Run";
        _testValueName = "SecondSelfTest";
        _testExePath = @"C:\Program Files\SecondSelf\SecondSelf.exe";
    }

    public void Dispose()
    {
        try
        {
            var parentKey = _testRegistrySubKey.Split('\\')[0];
            Registry.CurrentUser.DeleteSubKeyTree(parentKey, throwOnMissingSubKey: false);
        }
        catch
        {
            // Очистка тестовых ключей реестра
        }
    }

    [Fact]
    public void GetAutostartCommandLine_WithCustomPath_FormatsCorrectQuotesAndFlag()
    {
        var service = new AutostartService(_testRegistrySubKey, _testValueName, _testExePath);
        var commandLine = service.GetAutostartCommandLine();

        Assert.Equal($"\"{_testExePath}\" --startup-background", commandLine);
    }

    [Fact]
    public void GetAutostartCommandLine_WithDefaultPath_ContainsFlag()
    {
        var service = new AutostartService(_testRegistrySubKey, _testValueName);
        var commandLine = service.GetAutostartCommandLine();

        Assert.Contains("--startup-background", commandLine);
        Assert.StartsWith("\"", commandLine);
    }

    [Fact]
    public void SetAutostart_EnablesAndDisablesInRegistry()
    {
        var service = new AutostartService(_testRegistrySubKey, _testValueName, _testExePath);

        // Изначально выключено
        Assert.False(service.IsAutostartEnabled());

        // Включаем
        service.SetAutostart(true);
        Assert.True(service.IsAutostartEnabled());

        // Проверяем сырое значение в реестре
        using (var key = Registry.CurrentUser.OpenSubKey(_testRegistrySubKey, writable: false))
        {
            Assert.NotNull(key);
            var rawValue = (string?)key.GetValue(_testValueName);
            Assert.Equal($"\"{_testExePath}\" --startup-background", rawValue);
        }

        // Отключаем
        service.SetAutostart(false);
        Assert.False(service.IsAutostartEnabled());
    }
}
