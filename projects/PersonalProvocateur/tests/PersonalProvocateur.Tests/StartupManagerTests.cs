using Microsoft.Win32;
using PersonalProvocateur.App.WindowsInterop;
using Xunit;

namespace PersonalProvocateur.Tests;

public class StartupManagerTests : IDisposable
{
    private const string TestSubKey = @"Software\PersonalProvocateurTests\Run";

    public StartupManagerTests()
    {
        CleanupTestRegistry();
    }

    public void Dispose()
    {
        CleanupTestRegistry();
        GC.SuppressFinalize(this);
    }

    private static void CleanupTestRegistry()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\PersonalProvocateurTests", false);
        }
        catch
        {
            // Ignore
        }
    }

    [Fact]
    public void FormatExecutablePath_WrapsUnquotedPathInQuotes()
    {
        string raw = @"C:\Program Files\PersonalProvocateur\app.exe";
        string formatted = StartupManager.FormatExecutablePath(raw);

        Assert.Equal("\"C:\\Program Files\\PersonalProvocateur\\app.exe\"", formatted);
    }

    [Fact]
    public void FormatExecutablePath_DoesNotDoubleQuote()
    {
        string raw = "\"C:\\Test\\app.exe\"";
        string formatted = StartupManager.FormatExecutablePath(raw);

        Assert.Equal("\"C:\\Test\\app.exe\"", formatted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FormatExecutablePath_EmptyOrNull_ReturnsEmptyString(string? raw)
    {
        string formatted = StartupManager.FormatExecutablePath(raw!);
        Assert.Equal(string.Empty, formatted);
    }

    [Fact]
    public void GetExecutablePath_ReturnsNonEmptyFormattedString()
    {
        string path = StartupManager.GetExecutablePath();

        Assert.False(string.IsNullOrWhiteSpace(path));
        Assert.StartsWith("\"", path);
        Assert.EndsWith("\"", path);
    }

    [Fact]
    public void SetRunOnStartup_And_IsRunOnStartupEnabled_LifecycleWorksCorrectly()
    {
        // 1. Initially disabled
        bool initial = StartupManager.IsRunOnStartupEnabled(TestSubKey);
        Assert.False(initial);

        // 2. Enable startup with test path
        string dummyPath = @"C:\MockPath\PersonalProvocateur.App.exe";
        bool setResult = StartupManager.SetRunOnStartup(true, TestSubKey, dummyPath);
        Assert.True(setResult);

        // 3. Verify it is enabled
        bool isEnabled = StartupManager.IsRunOnStartupEnabled(TestSubKey);
        Assert.True(isEnabled);

        // 4. Verify value in registry
        using (var key = Registry.CurrentUser.OpenSubKey(TestSubKey, false))
        {
            Assert.NotNull(key);
            var value = key.GetValue(StartupManager.AppName) as string;
            Assert.Equal($"\"{dummyPath}\"", value);
        }

        // 5. Disable startup
        bool disableResult = StartupManager.SetRunOnStartup(false, TestSubKey);
        Assert.True(disableResult);

        // 6. Verify disabled
        bool isNowDisabled = StartupManager.IsRunOnStartupEnabled(TestSubKey);
        Assert.False(isNowDisabled);
    }
}
