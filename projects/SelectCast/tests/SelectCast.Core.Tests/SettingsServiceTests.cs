using System.IO;
using SelectCast.Core.Settings;
using Xunit;

namespace SelectCast.Core.Tests;

public class SettingsServiceTests
{
    [Fact]
    public void Round_trips_settings()
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"selectcast_settings_{Guid.NewGuid():N}.json");
        try
        {
            var saved = new AppSettings { HotkeyModifiers = 0x0006, HotkeyVk = 0x42, Autostart = false };
            new SettingsService(tmp).Save(saved);

            AppSettings loaded = new SettingsService(tmp).Load();

            Assert.Equal(0x0006u, loaded.HotkeyModifiers);
            Assert.Equal(0x42u, loaded.HotkeyVk);
            Assert.False(loaded.Autostart);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public void Missing_file_returns_defaults()
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"selectcast_missing_{Guid.NewGuid():N}.json");

        AppSettings s = new SettingsService(tmp).Load();

        Assert.Equal(0x43u, s.HotkeyVk);          // 'C'
        Assert.Equal(0x0002u | 0x0001u, s.HotkeyModifiers); // Ctrl+Alt
        Assert.True(s.Autostart);
    }
}
