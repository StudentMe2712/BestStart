using PersonalProvocateur.App.Models;
using PersonalProvocateur.App.Storage;
using Xunit;

namespace PersonalProvocateur.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _testSettingsFile;

    public SettingsStoreTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "PersonalProvocateur_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _testSettingsFile = Path.Combine(_tempDirectory, "test_settings.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup exceptions in tests
        }
    }

    [Fact]
    public void Load_NonExistentFile_ReturnsDefaultSettings()
    {
        var store = new SettingsStore(_testSettingsFile);
        var settings = store.Load();

        Assert.NotNull(settings);
        Assert.Equal(15, settings.MinIntervalMinutes);
        Assert.Equal(45, settings.MaxIntervalMinutes);
        Assert.Equal(0.60, settings.DimmingOpacity, 2);
    }

    [Fact]
    public void Load_CorruptedJsonFile_FallsBackToDefaultWithoutThrowing()
    {
        File.WriteAllText(_testSettingsFile, "{ broken json content: invalid !!!");

        var store = new SettingsStore(_testSettingsFile);
        var settings = store.Load();

        Assert.NotNull(settings);
        Assert.Equal(15, settings.MinIntervalMinutes);
        Assert.Equal(45, settings.MaxIntervalMinutes);
    }

    [Fact]
    public void Save_And_Load_PersistsSettingsCorrectly()
    {
        var store = new SettingsStore(_testSettingsFile);
        var custom = new AppSettings
        {
            MinIntervalMinutes = 20,
            MaxIntervalMinutes = 60,
            DimmingOpacity = 0.75,
            VideoVolume = 0.50,
            IsPaused = true,
            RunOnStartup = true,
            TriggerHotkey = "Ctrl+Shift+F1",
            PanicHotkey = "Ctrl+Alt+Escape"
        };
        custom.Videos.Add(new VideoItem { Title = "Clip 1", FilePath = @"C:\test\clip1.mp4" });

        store.Save(custom);

        var reloadedStore = new SettingsStore(_testSettingsFile);
        var loaded = reloadedStore.CurrentSettings;

        Assert.Equal(20, loaded.MinIntervalMinutes);
        Assert.Equal(60, loaded.MaxIntervalMinutes);
        Assert.Equal(0.75, loaded.DimmingOpacity, 2);
        Assert.Equal(0.50, loaded.VideoVolume, 2);
        Assert.True(loaded.IsPaused);
        Assert.True(loaded.RunOnStartup);
        Assert.Equal("Ctrl+Shift+F1", loaded.TriggerHotkey);
        Assert.Equal("Ctrl+Alt+Escape", loaded.PanicHotkey);
        Assert.Single(loaded.Videos);
        Assert.Equal("Clip 1", loaded.Videos[0].Title);
    }

    [Fact]
    public void Update_ModifiesAndSavesSettings_AndRaisesEvent()
    {
        var store = new SettingsStore(_testSettingsFile);
        var eventRaised = false;
        AppSettings? eventPayload = null;

        store.SettingsChanged += (_, s) =>
        {
            eventRaised = true;
            eventPayload = s;
        };

        store.Update(s =>
        {
            s.MinIntervalMinutes = 25;
            s.MaxIntervalMinutes = 55;
        });

        Assert.True(eventRaised);
        Assert.NotNull(eventPayload);
        Assert.Equal(25, eventPayload.MinIntervalMinutes);
        Assert.Equal(55, eventPayload.MaxIntervalMinutes);

        // Verify written to disk
        var reloadedStore = new SettingsStore(_testSettingsFile);
        Assert.Equal(25, reloadedStore.CurrentSettings.MinIntervalMinutes);
    }

    [Fact]
    public void AppPaths_PropertiesAreWellFormed()
    {
        Assert.False(string.IsNullOrWhiteSpace(AppPaths.AppDataDirectory));
        Assert.False(string.IsNullOrWhiteSpace(AppPaths.SettingsFilePath));
        Assert.EndsWith("settings.json", AppPaths.SettingsFilePath, StringComparison.OrdinalIgnoreCase);
    }
}
