namespace UniversalMediaPlayer.Tests;

using System.Text.Json;
using UniversalMediaPlayer.Core.Enums;
using UniversalMediaPlayer.Core.Models;
using UniversalMediaPlayer.Persistence;

public sealed class PersistenceTests : IDisposable
{
    private readonly string _tempDirectory;

    public PersistenceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "UMP_PersistenceTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
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
            // Best effort cleanup in test disposal
        }
    }

    [Fact]
    public async Task JsonShowPreferencesStore_NonExistentFile_ReturnsNullAndEmpty()
    {
        var filePath = Path.Combine(_tempDirectory, "non_existent.json");
        using var store = new JsonShowPreferencesStore(filePath);

        var pref = await store.GetPreferencesAsync("TestShow");
        var all = await store.GetAllPreferencesAsync();

        Assert.Null(pref);
        Assert.Empty(all);
    }

    [Fact]
    public async Task JsonShowPreferencesStore_SaveAndRetrieve_PreservesValues()
    {
        var filePath = Path.Combine(_tempDirectory, "prefs.json");
        using var store = new JsonShowPreferencesStore(filePath);

        var pref = new ShowPreferences
        {
            ShowId = "Frieren",
            PreferredAudioLanguage = "ja",
            PreferredSubtitleLanguage = "en",
            AutoNextEpisode = true,
            SubtitleEnabled = true,
            PreferredAudioTrack = new TrackPreference
            {
                Language = "ja",
                Title = "Original Japanese",
                Codec = "FLAC",
                Channels = 6,
                Origin = TrackOrigin.External
            },
            PreferredSubtitleTrack = new TrackPreference
            {
                Language = "en",
                Title = "English Full",
                Format = SubtitleFormat.ASS,
                Origin = TrackOrigin.External
            }
        };

        await store.SavePreferencesAsync(pref);

        var retrieved = await store.GetPreferencesAsync("Frieren");
        Assert.NotNull(retrieved);
        Assert.Equal("Frieren", retrieved.ShowId);
        Assert.Equal("ja", retrieved.PreferredAudioLanguage);
        Assert.Equal("en", retrieved.PreferredSubtitleLanguage);
        Assert.True(retrieved.AutoNextEpisode);
        Assert.True(retrieved.SubtitleEnabled);

        Assert.NotNull(retrieved.PreferredAudioTrack);
        Assert.Equal("ja", retrieved.PreferredAudioTrack.Language);
        Assert.Equal("Original Japanese", retrieved.PreferredAudioTrack.Title);
        Assert.Equal("FLAC", retrieved.PreferredAudioTrack.Codec);
        Assert.Equal(6, retrieved.PreferredAudioTrack.Channels);
        Assert.Equal(TrackOrigin.External, retrieved.PreferredAudioTrack.Origin);

        Assert.NotNull(retrieved.PreferredSubtitleTrack);
        Assert.Equal("en", retrieved.PreferredSubtitleTrack.Language);
        Assert.Equal("English Full", retrieved.PreferredSubtitleTrack.Title);
        Assert.Equal(SubtitleFormat.ASS, retrieved.PreferredSubtitleTrack.Format);
        Assert.Equal(TrackOrigin.External, retrieved.PreferredSubtitleTrack.Origin);
    }

    [Fact]
    public async Task JsonShowPreferencesStore_CaseInsensitiveShowId_MatchesAndUpdates()
    {
        var filePath = Path.Combine(_tempDirectory, "case_test.json");
        using var store = new JsonShowPreferencesStore(filePath);

        var pref1 = new ShowPreferences
        {
            ShowId = "AttackOnTitan",
            PreferredAudioLanguage = "ja"
        };
        await store.SavePreferencesAsync(pref1);

        // Retrieve with lowercase
        var retrieved = await store.GetPreferencesAsync("attackontitan");
        Assert.NotNull(retrieved);
        Assert.Equal("ja", retrieved.PreferredAudioLanguage);

        // Retrieve with uppercase
        var retrievedUpper = await store.GetPreferencesAsync("ATTACKONTITAN");
        Assert.NotNull(retrievedUpper);
        Assert.Equal("ja", retrievedUpper.PreferredAudioLanguage);

        // Overwrite with different case
        var pref2 = new ShowPreferences
        {
            ShowId = "attackontitan",
            PreferredAudioLanguage = "ru"
        };
        await store.SavePreferencesAsync(pref2);

        var all = await store.GetAllPreferencesAsync();
        Assert.Single(all);

        var updated = await store.GetPreferencesAsync("AttackOnTitan");
        Assert.NotNull(updated);
        Assert.Equal("ru", updated.PreferredAudioLanguage);
    }

    [Fact]
    public async Task JsonShowPreferencesStore_WritesVersionedSchemaAndLeavesNoTmpFiles()
    {
        var filePath = Path.Combine(_tempDirectory, "schema_test.json");
        using var store = new JsonShowPreferencesStore(filePath);

        await store.SavePreferencesAsync(new ShowPreferences
        {
            ShowId = "SteinsGate",
            PreferredAudioLanguage = "ja"
        });

        Assert.True(File.Exists(filePath));

        // Check no leftover .tmp files
        var tmpFiles = Directory.GetFiles(_tempDirectory, "*.tmp");
        Assert.Empty(tmpFiles);

        // Verify JSON root schema structure
        var json = await File.ReadAllTextAsync(filePath);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("version", out var versionProp));
        Assert.Equal(1, versionProp.GetInt32());

        Assert.True(doc.RootElement.TryGetProperty("showPreferences", out var showPrefsProp));
        Assert.True(showPrefsProp.TryGetProperty("SteinsGate", out var steinsGateProp));
        Assert.Equal("ja", steinsGateProp.GetProperty("preferredAudioLanguage").GetString());
    }

    [Fact]
    public async Task JsonShowPreferencesStore_ConcurrentSaves_DoNotCorruptFile()
    {
        var filePath = Path.Combine(_tempDirectory, "concurrent.json");
        using var store = new JsonShowPreferencesStore(filePath);

        var tasks = Enumerable.Range(1, 20).Select(i =>
            store.SavePreferencesAsync(new ShowPreferences
            {
                ShowId = $"Show_{i}",
                PreferredAudioLanguage = $"lang_{i}"
            }));

        await Task.WhenAll(tasks);

        var all = await store.GetAllPreferencesAsync();
        Assert.Equal(20, all.Count);
        for (int i = 1; i <= 20; i++)
        {
            var item = await store.GetPreferencesAsync($"show_{i}");
            Assert.NotNull(item);
            Assert.Equal($"lang_{i}", item.PreferredAudioLanguage);
        }
    }

    [Fact]
    public async Task SqliteWatchHistoryStore_RecordAndRetrieveByFilePath_Works()
    {
        var dbPath = Path.Combine(_tempDirectory, "history.db");
        using var store = new SqliteWatchHistoryStore(dbPath);

        var item = new WatchHistoryItem
        {
            ShowId = "ShowA",
            SeasonNumber = 1,
            EpisodeNumber = 2,
            FilePath = "C:\\Media\\ShowA_S01E02.mkv",
            PositionSeconds = 120.5,
            DurationSeconds = 1400.0,
            LastPlayedUtc = DateTime.UtcNow,
            Completed = false
        };

        await store.RecordPositionAsync(item);

        var retrieved = await store.GetByFilePathAsync("C:\\Media\\ShowA_S01E02.mkv");
        Assert.NotNull(retrieved);
        Assert.True(retrieved.Id > 0);
        Assert.Equal("ShowA", retrieved.ShowId);
        Assert.Equal(1, retrieved.SeasonNumber);
        Assert.Equal(2, retrieved.EpisodeNumber);
        Assert.Equal("C:\\Media\\ShowA_S01E02.mkv", retrieved.FilePath);
        Assert.Equal(120.5, retrieved.PositionSeconds, precision: 2);
        Assert.Equal(1400.0, retrieved.DurationSeconds, precision: 2);
        Assert.False(retrieved.Completed);
    }

    [Fact]
    public async Task SqliteWatchHistoryStore_RecordPosition_UpdatesExistingRecord()
    {
        var dbPath = Path.Combine(_tempDirectory, "history.db");
        using var store = new SqliteWatchHistoryStore(dbPath);

        var item1 = new WatchHistoryItem
        {
            ShowId = "ShowA",
            SeasonNumber = 1,
            EpisodeNumber = 1,
            FilePath = "C:\\Media\\ShowA_S01E01.mkv",
            PositionSeconds = 100.0,
            DurationSeconds = 1000.0,
            LastPlayedUtc = DateTime.UtcNow.AddMinutes(-10),
            Completed = false
        };
        await store.RecordPositionAsync(item1);

        var item2 = new WatchHistoryItem
        {
            ShowId = "ShowA",
            SeasonNumber = 1,
            EpisodeNumber = 1,
            FilePath = "C:\\Media\\ShowA_S01E01.mkv",
            PositionSeconds = 500.0,
            DurationSeconds = 1000.0,
            LastPlayedUtc = DateTime.UtcNow,
            Completed = false
        };
        await store.RecordPositionAsync(item2);

        var retrieved = await store.GetByFilePathAsync("C:\\Media\\ShowA_S01E01.mkv");
        Assert.NotNull(retrieved);
        Assert.Equal(500.0, retrieved.PositionSeconds, precision: 2);
        Assert.False(retrieved.Completed);

        // Verify only 1 record exists in history
        var continueWatching = await store.GetContinueWatchingAsync(100);
        Assert.Single(continueWatching);
    }

    [Fact]
    public async Task SqliteWatchHistoryStore_AutoCompletesAtNinetyPercent()
    {
        var dbPath = Path.Combine(_tempDirectory, "history.db");
        using var store = new SqliteWatchHistoryStore(dbPath);

        // 89% - not completed
        var item89 = new WatchHistoryItem
        {
            ShowId = "ShowB",
            FilePath = "C:\\Media\\ShowB_89.mkv",
            PositionSeconds = 890.0,
            DurationSeconds = 1000.0,
            Completed = false
        };
        await store.RecordPositionAsync(item89);
        var res89 = await store.GetByFilePathAsync("C:\\Media\\ShowB_89.mkv");
        Assert.NotNull(res89);
        Assert.False(res89.Completed);

        // 90% - automatically completed
        var item90 = new WatchHistoryItem
        {
            ShowId = "ShowB",
            FilePath = "C:\\Media\\ShowB_90.mkv",
            PositionSeconds = 900.0,
            DurationSeconds = 1000.0,
            Completed = false
        };
        await store.RecordPositionAsync(item90);
        var res90 = await store.GetByFilePathAsync("C:\\Media\\ShowB_90.mkv");
        Assert.NotNull(res90);
        Assert.True(res90.Completed);

        // 95% - automatically completed
        var item95 = new WatchHistoryItem
        {
            ShowId = "ShowB",
            FilePath = "C:\\Media\\ShowB_95.mkv",
            PositionSeconds = 950.0,
            DurationSeconds = 1000.0,
            Completed = false
        };
        await store.RecordPositionAsync(item95);
        var res95 = await store.GetByFilePathAsync("C:\\Media\\ShowB_95.mkv");
        Assert.NotNull(res95);
        Assert.True(res95.Completed);
    }

    [Fact]
    public async Task SqliteWatchHistoryStore_GetLatestByShowId_ReturnsNewest()
    {
        var dbPath = Path.Combine(_tempDirectory, "history.db");
        using var store = new SqliteWatchHistoryStore(dbPath);

        await store.RecordPositionAsync(new WatchHistoryItem
        {
            ShowId = "MultiEpShow",
            EpisodeNumber = 1,
            FilePath = "C:\\Media\\ep1.mkv",
            PositionSeconds = 300,
            DurationSeconds = 1200,
            LastPlayedUtc = DateTime.UtcNow.AddHours(-2)
        });

        await store.RecordPositionAsync(new WatchHistoryItem
        {
            ShowId = "MultiEpShow",
            EpisodeNumber = 2,
            FilePath = "C:\\Media\\ep2.mkv",
            PositionSeconds = 200,
            DurationSeconds = 1200,
            LastPlayedUtc = DateTime.UtcNow.AddHours(-1)
        });

        var latest = await store.GetLatestByShowIdAsync("MultiEpShow");
        Assert.NotNull(latest);
        Assert.Equal(2, latest.EpisodeNumber);
        Assert.Equal("C:\\Media\\ep2.mkv", latest.FilePath);
    }

    [Fact]
    public async Task SqliteWatchHistoryStore_GetContinueWatching_FiltersAndOrdersCorrectly()
    {
        var dbPath = Path.Combine(_tempDirectory, "history.db");
        using var store = new SqliteWatchHistoryStore(dbPath);

        // 1. Valid continue watching candidate (played 100s, 900s remaining, played 1 hour ago)
        await store.RecordPositionAsync(new WatchHistoryItem
        {
            ShowId = "Show1",
            FilePath = "C:\\Media\\valid1.mkv",
            PositionSeconds = 100,
            DurationSeconds = 1000,
            Completed = false,
            LastPlayedUtc = DateTime.UtcNow.AddHours(-1)
        });

        // 2. Valid candidate, newer (played 200s, 800s remaining, played 10 mins ago)
        await store.RecordPositionAsync(new WatchHistoryItem
        {
            ShowId = "Show2",
            FilePath = "C:\\Media\\valid2.mkv",
            PositionSeconds = 200,
            DurationSeconds = 1000,
            Completed = false,
            LastPlayedUtc = DateTime.UtcNow.AddMinutes(-10)
        });

        // 3. Invalid: position <= 15s
        await store.RecordPositionAsync(new WatchHistoryItem
        {
            ShowId = "Show3",
            FilePath = "C:\\Media\\too_short_pos.mkv",
            PositionSeconds = 15,
            DurationSeconds = 1000,
            Completed = false,
            LastPlayedUtc = DateTime.UtcNow
        });

        // 4. Invalid: remaining <= 15s (Duration - Position <= 15)
        await store.RecordPositionAsync(new WatchHistoryItem
        {
            ShowId = "Show4",
            FilePath = "C:\\Media\\too_short_remaining.mkv",
            PositionSeconds = 990,
            DurationSeconds = 1000,
            Completed = false,
            LastPlayedUtc = DateTime.UtcNow
        });

        // 5. Invalid: completed = true
        await store.RecordPositionAsync(new WatchHistoryItem
        {
            ShowId = "Show5",
            FilePath = "C:\\Media\\already_completed.mkv",
            PositionSeconds = 500,
            DurationSeconds = 1000,
            Completed = true,
            LastPlayedUtc = DateTime.UtcNow
        });

        var continueWatching = await store.GetContinueWatchingAsync(10);
        Assert.Equal(2, continueWatching.Count);
        // Ordered by LastPlayedUtc DESC: valid2 (10 mins ago) should be first, then valid1 (1 hr ago)
        Assert.Equal("C:\\Media\\valid2.mkv", continueWatching[0].FilePath);
        Assert.Equal("C:\\Media\\valid1.mkv", continueWatching[1].FilePath);

        // Test limit
        var limited = await store.GetContinueWatchingAsync(1);
        Assert.Single(limited);
        Assert.Equal("C:\\Media\\valid2.mkv", limited[0].FilePath);
    }

    [Fact]
    public async Task SqliteWatchHistoryStore_MarkCompletedAndClearHistory_Work()
    {
        var dbPath = Path.Combine(_tempDirectory, "history.db");
        using var store = new SqliteWatchHistoryStore(dbPath);

        await store.RecordPositionAsync(new WatchHistoryItem
        {
            ShowId = "Show1",
            FilePath = "C:\\Media\\item.mkv",
            PositionSeconds = 100,
            DurationSeconds = 1000,
            Completed = false
        });

        await store.MarkCompletedAsync("C:\\Media\\item.mkv");
        var item = await store.GetByFilePathAsync("C:\\Media\\item.mkv");
        Assert.NotNull(item);
        Assert.True(item.Completed);

        await store.ClearHistoryAsync();
        var clearedItem = await store.GetByFilePathAsync("C:\\Media\\item.mkv");
        Assert.Null(clearedItem);

        var continueWatching = await store.GetContinueWatchingAsync();
        Assert.Empty(continueWatching);
    }
}
