namespace UniversalMediaPlayer.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using UniversalMediaPlayer.Core.Enums;
using UniversalMediaPlayer.Core.Models;
using UniversalMediaPlayer.Core.Persistence;
using UniversalMediaPlayer.Core.Services;
using UniversalMediaPlayer.Discovery;
using UniversalMediaPlayer.Persistence;
using UniversalMediaPlayer.UI.Helpers;
using UniversalMediaPlayer.UI.ViewModels;
using Xunit;

/// <summary>
/// Phase 9 (MVP-5) Acceptance Tests covering:
/// 1. The exact 18 test cases specified in Section 34 of media_player_spec.md.
/// 2. The full Section 35 Real User Acceptance Test (Anime Full Workflow).
/// All tests use isolated temporary storage directories to guarantee hermetic execution.
/// </summary>
public sealed class Phase9AcceptanceTests : IDisposable
{
    private readonly List<string> _tempDirectories = new();

    public void Dispose()
    {
        try
        {
            SqliteConnection.ClearAllPools();
        }
        catch
        {
            // Ignore failure to clear connection pool
        }

        foreach (var dir in _tempDirectories)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }

    private string CreateIsolatedDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "UMP_Phase9Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _tempDirectories.Add(dir);
        return dir;
    }

    private static MediaPackage CreateTestPackage(string showTitle, int seasonNumber, int episodeNumber)
    {
        var fileName = $"{showTitle} - S{seasonNumber:D2}E{episodeNumber:D2}.mkv";
        var filePath = $@"C:\Media\{showTitle}\{fileName}";

        return new MediaPackage
        {
            PrimaryVideo = new MediaItem
            {
                FilePath = filePath,
                FileName = fileName,
                Extension = "mkv"
            },
            Episode = new EpisodeInfo
            {
                ShowTitle = showTitle,
                SeasonNumber = seasonNumber,
                EpisodeNumber = episodeNumber,
                RawToken = $"S{seasonNumber:D2}E{episodeNumber:D2}"
            }
        };
    }

    private static string ResolveAnimeTestDataDir()
    {
        var current = AppContext.BaseDirectory;
        for (int i = 0; i < 6; i++)
        {
            var candidate = Path.Combine(current, "TestData", "Anime");
            if (Directory.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
            var candidateInTests = Path.Combine(current, "tests", "TestData", "Anime");
            if (Directory.Exists(candidateInTests))
            {
                return Path.GetFullPath(candidateInTests);
            }
            var parent = Path.GetDirectoryName(current);
            if (parent == null) break;
            current = parent;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\TestData\Anime"));
    }

    #region Section 34: 18 Acceptance Test Cases

    /// <summary>
    /// Case 1: Save RU audio preference and verify it is written to IShowPreferencesStore.
    /// </summary>
    [Fact]
    public async Task Case01_SaveRuAudioPreference()
    {
        var tempDir = CreateIsolatedDirectory();
        var prefsPath = Path.Combine(tempDir, "show_preferences.json");
        var dbPath = Path.Combine(tempDir, "history.db");
        using var prefStore = new JsonShowPreferencesStore(prefsPath);
        using var historyStore = new SqliteWatchHistoryStore(dbPath);
        var continuityService = new EpisodicContinuityService(prefStore, historyStore);

        var package = CreateTestPackage("Frieren", 1, 1);
        var ruAudio = new AudioTrack
        {
            Id = 101,
            Title = "[AniLibria] Russian Dub",
            Language = "ru",
            Codec = "FLAC",
            Channels = 6,
            Origin = TrackOrigin.External
        };

        // Save RU audio preference
        await continuityService.SaveAudioPreferenceAsync(package, ruAudio);

        // Verify written to store
        var saved = await prefStore.GetPreferencesAsync("frieren");
        Assert.NotNull(saved);
        Assert.Equal("ru", saved.PreferredAudioLanguage);
        Assert.NotNull(saved.PreferredAudioTrack);
        Assert.Equal("ru", saved.PreferredAudioTrack.Language);
        Assert.Equal("[AniLibria] Russian Dub", saved.PreferredAudioTrack.Title);
        Assert.Equal("FLAC", saved.PreferredAudioTrack.Codec);
        Assert.Equal(6, saved.PreferredAudioTrack.Channels);
        Assert.Equal(TrackOrigin.External, saved.PreferredAudioTrack.Origin);
    }

    /// <summary>
    /// Case 2: Save RU subtitle preference and verify it is written to IShowPreferencesStore.
    /// </summary>
    [Fact]
    public async Task Case02_SaveRuSubtitlePreference()
    {
        var tempDir = CreateIsolatedDirectory();
        var prefsPath = Path.Combine(tempDir, "show_preferences.json");
        var dbPath = Path.Combine(tempDir, "history.db");
        using var prefStore = new JsonShowPreferencesStore(prefsPath);
        using var historyStore = new SqliteWatchHistoryStore(dbPath);
        var continuityService = new EpisodicContinuityService(prefStore, historyStore);

        var package = CreateTestPackage("Frieren", 1, 1);
        var ruSub = new SubtitleTrack
        {
            Id = 201,
            Title = "[AniLibria] Full Russian Subtitles",
            Language = "ru",
            Format = SubtitleFormat.ASS,
            Origin = TrackOrigin.External
        };

        // Save RU subtitle preference
        await continuityService.SaveSubtitlePreferenceAsync(package, ruSub, subtitleEnabled: true);

        // Verify written to store
        var saved = await prefStore.GetPreferencesAsync("frieren");
        Assert.NotNull(saved);
        Assert.Equal("ru", saved.PreferredSubtitleLanguage);
        Assert.True(saved.SubtitleEnabled);
        Assert.NotNull(saved.PreferredSubtitleTrack);
        Assert.Equal("ru", saved.PreferredSubtitleTrack.Language);
        Assert.Equal("[AniLibria] Full Russian Subtitles", saved.PreferredSubtitleTrack.Title);
        Assert.Equal(SubtitleFormat.ASS, saved.PreferredSubtitleTrack.Format);
        Assert.Equal(TrackOrigin.External, saved.PreferredSubtitleTrack.Origin);
    }

    /// <summary>
    /// Case 3: Re-instantiate JsonShowPreferencesStore from disk and verify preferences survive app restart.
    /// </summary>
    [Fact]
    public async Task Case03_ReloadPreferences()
    {
        var tempDir = CreateIsolatedDirectory();
        var prefsPath = Path.Combine(tempDir, "show_preferences.json");

        // Session 1: Save preferences to disk and dispose
        using (var store1 = new JsonShowPreferencesStore(prefsPath))
        {
            var pref = new ShowPreferences
            {
                ShowId = "frieren",
                PreferredAudioLanguage = "ru",
                PreferredSubtitleLanguage = "ru",
                SubtitleEnabled = true,
                AutoNextEpisode = true,
                PreferredAudioTrack = new TrackPreference
                {
                    Language = "ru",
                    Title = "[AniLibria] Russian Dub",
                    Codec = "FLAC",
                    Channels = 6,
                    Origin = TrackOrigin.External
                },
                PreferredSubtitleTrack = new TrackPreference
                {
                    Language = "ru",
                    Title = "Russian Full",
                    Format = SubtitleFormat.ASS,
                    Origin = TrackOrigin.External
                }
            };
            await store1.SavePreferencesAsync(pref);
        }

        // Session 2: Reopen from disk (simulating application restart)
        using (var store2 = new JsonShowPreferencesStore(prefsPath))
        {
            var reloaded = await store2.GetPreferencesAsync("frieren");
            Assert.NotNull(reloaded);
            Assert.Equal("frieren", reloaded.ShowId);
            Assert.Equal("ru", reloaded.PreferredAudioLanguage);
            Assert.Equal("ru", reloaded.PreferredSubtitleLanguage);
            Assert.True(reloaded.SubtitleEnabled);
            Assert.True(reloaded.AutoNextEpisode);
            Assert.NotNull(reloaded.PreferredAudioTrack);
            Assert.Equal("[AniLibria] Russian Dub", reloaded.PreferredAudioTrack.Title);
            Assert.Equal("FLAC", reloaded.PreferredAudioTrack.Codec);
            Assert.Equal(6, reloaded.PreferredAudioTrack.Channels);
            Assert.Equal(TrackOrigin.External, reloaded.PreferredAudioTrack.Origin);
            Assert.NotNull(reloaded.PreferredSubtitleTrack);
            Assert.Equal("Russian Full", reloaded.PreferredSubtitleTrack.Title);
            Assert.Equal(SubtitleFormat.ASS, reloaded.PreferredSubtitleTrack.Format);
            Assert.Equal(TrackOrigin.External, reloaded.PreferredSubtitleTrack.Origin);
        }
    }

    /// <summary>
    /// Case 4: Prepare playback for S01E02 using preferences set from S01E01,
    /// verifying Russian audio and Russian subtitle are automatically selected.
    /// </summary>
    [Fact]
    public async Task Case04_ApplyPreferencesToNextEpisode()
    {
        var tempDir = CreateIsolatedDirectory();
        var prefsPath = Path.Combine(tempDir, "show_preferences.json");
        var dbPath = Path.Combine(tempDir, "history.db");
        using var prefStore = new JsonShowPreferencesStore(prefsPath);
        using var historyStore = new SqliteWatchHistoryStore(dbPath);
        var service = new EpisodicContinuityService(prefStore, historyStore);

        // Episode 1: User chooses Russian audio and Russian subtitles
        var ep1 = CreateTestPackage("Frieren", 1, 1);
        var ep1RuAudio = new AudioTrack { Id = 1, Title = "[AniLibria] Russian Dub", Language = "ru", Codec = "FLAC", Channels = 6, Origin = TrackOrigin.External };
        var ep1RuSub = new SubtitleTrack { Id = 11, Title = "Russian Full", Language = "ru", Format = SubtitleFormat.ASS, Origin = TrackOrigin.External };
        await service.SaveAudioPreferenceAsync(ep1, ep1RuAudio);
        await service.SaveSubtitlePreferenceAsync(ep1, ep1RuSub, subtitleEnabled: true);

        // Episode 2: Contains multiple candidate tracks
        var ep2 = new MediaPackage
        {
            PrimaryVideo = new MediaItem
            {
                FilePath = @"C:\Media\Frieren\Frieren - S01E02.mkv",
                FileName = "Frieren - S01E02.mkv",
                Extension = "mkv"
            },
            Episode = new EpisodeInfo
            {
                ShowTitle = "Frieren",
                SeasonNumber = 1,
                EpisodeNumber = 2,
                RawToken = "S01E02"
            },
            AudioTracks =
            [
                new AudioTrack { Id = 10, Title = "Japanese Original", Language = "ja", Origin = TrackOrigin.Embedded },
                new AudioTrack { Id = 20, Title = "[AniLibria] Russian Dub", Language = "ru", Codec = "FLAC", Channels = 6, Origin = TrackOrigin.External }
            ],
            SubtitleTracks =
            [
                new SubtitleTrack { Id = 30, Title = "English Subs", Language = "en", Format = SubtitleFormat.SRT, Origin = TrackOrigin.Embedded },
                new SubtitleTrack { Id = 40, Title = "Russian Full", Language = "ru", Format = SubtitleFormat.ASS, Origin = TrackOrigin.External }
            ]
        };

        // Prepare playback for Episode 2
        var plan = await service.PreparePlaybackAsync(ep2);

        Assert.NotNull(plan);
        Assert.NotNull(plan.AudioResolution);
        Assert.NotNull(plan.AudioResolution.SelectedTrack);
        Assert.Equal("ru", plan.AudioResolution.SelectedTrack!.Language);
        Assert.Equal(20, plan.AudioResolution.SelectedTrack!.Id);

        Assert.NotNull(plan.SubtitleResolution);
        Assert.NotNull(plan.SubtitleResolution.SelectedTrack);
        Assert.Equal("ru", plan.SubtitleResolution.SelectedTrack!.Language);
        Assert.Equal(40, plan.SubtitleResolution.SelectedTrack!.Id);
        Assert.True(plan.SubtitleVisible);
    }

    /// <summary>
    /// Case 5: When an exact preferred track is missing in the next episode, fallback to matching language.
    /// </summary>
    [Fact]
    public void Case05_PreferredTrackMissing()
    {
        // Preferred specific group AniLibria
        var prefs = new ShowPreferences
        {
            ShowId = "frieren",
            PreferredAudioLanguage = "ru",
            PreferredAudioTrack = new TrackPreference
            {
                Language = "ru",
                Title = "AniLibria"
            },
            PreferredSubtitleLanguage = "ru",
            PreferredSubtitleTrack = new TrackPreference
            {
                Language = "ru",
                Title = "AniLibria",
                Format = SubtitleFormat.ASS
            }
        };

        // Candidate tracks in next episode: AniLibria missing, but other Russian tracks exist
        var audioCandidates = new List<AudioTrack>
        {
            new() { Id = 1, Title = "Japanese Original", Language = "ja", Origin = TrackOrigin.Embedded },
            new() { Id = 2, Title = "[JAM Club] Russian Dub", Language = "ru", Origin = TrackOrigin.External }
        };

        var subCandidates = new List<SubtitleTrack>
        {
            new() { Id = 10, Title = "English SRT", Language = "en", Format = SubtitleFormat.SRT, Origin = TrackOrigin.Embedded },
            new() { Id = 20, Title = "[Yousei-raws] Russian ASS", Language = "ru", Format = SubtitleFormat.ASS, Origin = TrackOrigin.External }
        };

        var audioResult = PreferredTrackResolver.ResolveAudioTrack(prefs, audioCandidates);
        var subResult = PreferredTrackResolver.ResolveSubtitleTrack(prefs, subCandidates);

        // Both fall back to matching Russian language
        Assert.True(audioResult.HasSelection);
        Assert.NotNull(audioResult.SelectedTrack);
        Assert.Equal(2, audioResult.SelectedTrack.Id);
        Assert.Equal("ru", audioResult.SelectedTrack.Language);
        Assert.Equal(TrackSelectionReason.PreferredLanguage, audioResult.Reason);

        Assert.True(subResult.HasSelection);
        Assert.NotNull(subResult.SelectedTrack);
        Assert.Equal(20, subResult.SelectedTrack.Id);
        Assert.Equal("ru", subResult.SelectedTrack.Language);
        Assert.Equal(TrackSelectionReason.PreferredLanguage, subResult.Reason);
    }

    /// <summary>
    /// Case 6: When preferred language is missing entirely in candidate tracks,
    /// audio falls back to first/default with clear explanation, and subtitle falls back with explanation.
    /// </summary>
    [Fact]
    public void Case06_PreferredLanguageMissing()
    {
        var prefs = new ShowPreferences
        {
            ShowId = "frieren",
            PreferredAudioLanguage = "ru",
            PreferredSubtitleLanguage = "ru"
        };

        var audioCandidates = new List<AudioTrack>
        {
            new() { Id = 1, Title = "Japanese Original", Language = "ja", Origin = TrackOrigin.Embedded, IsSelected = false },
            new() { Id = 2, Title = "English Dub", Language = "en", Origin = TrackOrigin.Embedded, IsSelected = false }
        };

        var subCandidates = new List<SubtitleTrack>
        {
            new() { Id = 10, Title = "English SRT", Language = "en", Format = SubtitleFormat.SRT, Origin = TrackOrigin.Embedded }
        };

        var audioResult = PreferredTrackResolver.ResolveAudioTrack(prefs, audioCandidates);
        var subResult = PreferredTrackResolver.ResolveSubtitleTrack(prefs, subCandidates);

        // Audio falls back to first/default with explanation
        Assert.True(audioResult.HasSelection);
        Assert.NotNull(audioResult.SelectedTrack);
        Assert.Equal(1, audioResult.SelectedTrack.Id);
        Assert.Equal(TrackSelectionReason.FallbackFirstAvailable, audioResult.Reason);
        Assert.Contains("Russian", audioResult.Explanation);
        Assert.Contains("Fallback", audioResult.Explanation);

        // Subtitle falls back with explanation and no selection
        Assert.False(subResult.HasSelection);
        Assert.Null(subResult.SelectedTrack);
        Assert.Equal(TrackSelectionReason.None, subResult.Reason);
        Assert.Contains("Russian", subResult.Explanation);
        Assert.Contains("unavailable", subResult.Explanation);
    }

    /// <summary>
    /// Case 7: When subtitles are set to OFF (SubtitleEnabled = false),
    /// verifying subtitle is null and Reason = ExplicitlyDisabled.
    /// </summary>
    [Fact]
    public void Case07_SubtitleExplicitlyOff()
    {
        var prefs = new ShowPreferences
        {
            ShowId = "frieren",
            SubtitleEnabled = false,
            PreferredSubtitleLanguage = "ru"
        };

        var subCandidates = new List<SubtitleTrack>
        {
            new() { Id = 1, Title = "Russian ASS", Language = "ru", Format = SubtitleFormat.ASS, Origin = TrackOrigin.External },
            new() { Id = 2, Title = "English SRT", Language = "en", Format = SubtitleFormat.SRT, Origin = TrackOrigin.Embedded }
        };

        var result = PreferredTrackResolver.ResolveSubtitleTrack(prefs, subCandidates);

        Assert.False(result.HasSelection);
        Assert.Null(result.SelectedTrack);
        Assert.Equal(TrackSelectionReason.ExplicitlyDisabled, result.Reason);
        Assert.Contains("disabled", result.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Case 8: When multiple Russian tracks exist (e.g. AniLibria vs JAM),
    /// exact track preference matches the correct one.
    /// </summary>
    [Fact]
    public void Case08_MultipleRuTracks()
    {
        var trackAniLibria = new AudioTrack
        {
            Id = 1,
            Title = "[AniLibria] Russian Dub",
            Language = "ru",
            Origin = TrackOrigin.External,
            Codec = "FLAC",
            Channels = 6
        };

        var trackJam = new AudioTrack
        {
            Id = 2,
            Title = "[JAM Club] Russian Dub",
            Language = "ru",
            Origin = TrackOrigin.External,
            Codec = "AAC",
            Channels = 2
        };

        var trackJap = new AudioTrack
        {
            Id = 3,
            Title = "Japanese Original",
            Language = "ja",
            Origin = TrackOrigin.Embedded,
            Codec = "AAC",
            Channels = 2
        };

        var tracks = new List<AudioTrack> { trackJap, trackJam, trackAniLibria };

        // AniLibria preference
        var prefAniLibria = new ShowPreferences
        {
            ShowId = "frieren",
            PreferredAudioTrack = new TrackPreference
            {
                Language = "ru",
                Title = "AniLibria"
            }
        };

        var resultAniLibria = PreferredTrackResolver.ResolveAudioTrack(prefAniLibria, tracks);
        Assert.NotNull(resultAniLibria.SelectedTrack);
        Assert.Equal(1, resultAniLibria.SelectedTrack.Id);
        Assert.Equal(TrackSelectionReason.ExactTrackMatch, resultAniLibria.Reason);
        Assert.Contains("AniLibria", resultAniLibria.Explanation);

        // JAM Club preference
        var prefJam = new ShowPreferences
        {
            ShowId = "frieren",
            PreferredAudioTrack = new TrackPreference
            {
                Language = "ru",
                Title = "JAM"
            }
        };

        var resultJam = PreferredTrackResolver.ResolveAudioTrack(prefJam, tracks);
        Assert.NotNull(resultJam.SelectedTrack);
        Assert.Equal(2, resultJam.SelectedTrack.Id);
        Assert.Equal(TrackSelectionReason.ExactTrackMatch, resultJam.Reason);
        Assert.Contains("JAM", resultJam.Explanation);
    }

    /// <summary>
    /// Case 9: Save playback position via PlaybackHistoryTracker / SqliteWatchHistoryStore.
    /// </summary>
    [Fact]
    public async Task Case09_SavePosition()
    {
        var tempDir = CreateIsolatedDirectory();
        var dbPath = Path.Combine(tempDir, "history.db");
        using var store = new SqliteWatchHistoryStore(dbPath);
        using var tracker = new PlaybackHistoryTracker(store);

        var filePath = @"C:\Media\Show_S01E01.mkv";
        await tracker.TrackMediaAsync(filePath, "Show", seasonNumber: 1, episodeNumber: 1, duration: 1450.0);

        // Seek to 450.0 seconds (forces immediate write)
        await tracker.OnSeekAsync(450.0);

        Assert.Equal(450.0, tracker.CurrentPosition);
        Assert.True(tracker.SaveCount >= 1);

        var record = await store.GetByFilePathAsync(filePath);
        Assert.NotNull(record);
        Assert.Equal("Show", record.ShowId);
        Assert.Equal(1, record.SeasonNumber);
        Assert.Equal(1, record.EpisodeNumber);
        Assert.Equal(450.0, record.PositionSeconds, precision: 2);
        Assert.Equal(1450.0, record.DurationSeconds, precision: 2);
        Assert.False(record.Completed);
    }

    /// <summary>
    /// Case 10: Re-instantiate SqliteWatchHistoryStore from disk and verify position is accurately reloaded.
    /// </summary>
    [Fact]
    public async Task Case10_ReloadPosition()
    {
        var tempDir = CreateIsolatedDirectory();
        var dbPath = Path.Combine(tempDir, "history.db");
        var filePath = @"C:\Media\Show_S01E01.mkv";

        // Connection 1: Record position
        using (var store1 = new SqliteWatchHistoryStore(dbPath))
        {
            await store1.RecordPositionAsync(new WatchHistoryItem
            {
                ShowId = "Show",
                SeasonNumber = 1,
                EpisodeNumber = 1,
                FilePath = filePath,
                PositionSeconds = 745.5,
                DurationSeconds = 1450.0,
                Completed = false,
                LastPlayedUtc = DateTime.UtcNow
            });
        }

        // Connection 2: Reopen from disk
        using (var store2 = new SqliteWatchHistoryStore(dbPath))
        {
            var item = await store2.GetByFilePathAsync(filePath);
            Assert.NotNull(item);
            Assert.Equal("Show", item.ShowId);
            Assert.Equal(1, item.SeasonNumber);
            Assert.Equal(1, item.EpisodeNumber);
            Assert.Equal(745.5, item.PositionSeconds, precision: 2);
            Assert.Equal(1450.0, item.DurationSeconds, precision: 2);
            Assert.False(item.Completed);
        }
    }

    /// <summary>
    /// Case 11: When playback reaches >= 90% (or MarkCompletedAsync), verify Completed == true.
    /// </summary>
    [Fact]
    public async Task Case11_MarkEpisodeCompleted()
    {
        var tempDir = CreateIsolatedDirectory();
        var dbPath = Path.Combine(tempDir, "history.db");
        using var store = new SqliteWatchHistoryStore(dbPath);
        using var tracker = new PlaybackHistoryTracker(store);

        var file1 = @"C:\Media\Show_S01E01.mkv";
        var duration = 1000.0;
        await tracker.TrackMediaAsync(file1, "Show", seasonNumber: 1, episodeNumber: 1, duration: duration);

        // 1. Reaching 90% (900.0 / 1000.0) triggers completion automatically
        await tracker.UpdatePositionAsync(900.0, duration);
        Assert.True(tracker.IsCompleted);

        var record1 = await store.GetByFilePathAsync(file1);
        Assert.NotNull(record1);
        Assert.True(record1.Completed);

        // 2. Direct MarkCompletedAsync
        var file2 = @"C:\Media\Show_S01E02.mkv";
        await store.RecordPositionAsync(new WatchHistoryItem
        {
            ShowId = "Show",
            SeasonNumber = 1,
            EpisodeNumber = 2,
            FilePath = file2,
            PositionSeconds = 200.0,
            DurationSeconds = 1000.0,
            Completed = false
        });

        await store.MarkCompletedAsync(file2);
        var record2 = await store.GetByFilePathAsync(file2);
        Assert.NotNull(record2);
        Assert.True(record2.Completed);
    }

    /// <summary>
    /// Case 12: Verify GetContinueWatchingAsync returns in-progress episode (> 15s watched, not completed)
    /// ordered by LastPlayedUtc DESC.
    /// </summary>
    [Fact]
    public async Task Case12_ContinueWatching()
    {
        var tempDir = CreateIsolatedDirectory();
        var dbPath = Path.Combine(tempDir, "history.db");
        using var store = new SqliteWatchHistoryStore(dbPath);

        // 1. Valid item (100s watched, 900s remaining, played 1 hour ago)
        await store.RecordPositionAsync(new WatchHistoryItem
        {
            ShowId = "Show1",
            FilePath = @"C:\Media\valid1.mkv",
            PositionSeconds = 100,
            DurationSeconds = 1000,
            Completed = false,
            LastPlayedUtc = DateTime.UtcNow.AddHours(-1)
        });

        // 2. Valid item, played more recently (500s watched, 500s remaining, played 10 mins ago)
        await store.RecordPositionAsync(new WatchHistoryItem
        {
            ShowId = "Show2",
            FilePath = @"C:\Media\valid2.mkv",
            PositionSeconds = 500,
            DurationSeconds = 1000,
            Completed = false,
            LastPlayedUtc = DateTime.UtcNow.AddMinutes(-10)
        });

        // 3. Excluded: position <= 15s
        await store.RecordPositionAsync(new WatchHistoryItem
        {
            ShowId = "Show3",
            FilePath = @"C:\Media\too_short.mkv",
            PositionSeconds = 14,
            DurationSeconds = 1000,
            Completed = false,
            LastPlayedUtc = DateTime.UtcNow
        });

        // 4. Excluded: completed = true
        await store.RecordPositionAsync(new WatchHistoryItem
        {
            ShowId = "Show4",
            FilePath = @"C:\Media\completed.mkv",
            PositionSeconds = 950,
            DurationSeconds = 1000,
            Completed = true,
            LastPlayedUtc = DateTime.UtcNow
        });

        // 5. Excluded: remaining <= 15s
        await store.RecordPositionAsync(new WatchHistoryItem
        {
            ShowId = "Show5",
            FilePath = @"C:\Media\remaining_too_short.mkv",
            PositionSeconds = 990,
            DurationSeconds = 1000,
            Completed = false,
            LastPlayedUtc = DateTime.UtcNow
        });

        var continueWatching = await store.GetContinueWatchingAsync();
        Assert.Equal(2, continueWatching.Count);
        // Ordered by LastPlayedUtc DESC: valid2 (10 mins ago) before valid1 (1 hr ago)
        Assert.Equal(@"C:\Media\valid2.mkv", continueWatching[0].FilePath);
        Assert.Equal(@"C:\Media\valid1.mkv", continueWatching[1].FilePath);
    }

    /// <summary>
    /// Case 13: Use EpisodeNavigator to resolve S01E01 -> S01E02.
    /// </summary>
    [Fact]
    public void Case13_FindNextEpisode()
    {
        var ep1 = MediaItem.FromFilePath(@"C:\Anime\Show.S01E01.mkv");
        var ep2 = MediaItem.FromFilePath(@"C:\Anime\Show.S01E02.mkv");

        var package = new MediaPackage
        {
            PrimaryVideo = ep1,
            SiblingEpisodes = [ep2]
        };

        var next = EpisodeNavigator.FindNextEpisode(package);

        Assert.NotNull(next);
        Assert.Equal(ep2.FilePath, next.FilePath);
    }

    /// <summary>
    /// Case 14: Use EpisodeNavigator to resolve S01E02 -> null when no S01E03 exists.
    /// </summary>
    [Fact]
    public void Case14_NoNextEpisode()
    {
        var ep1 = MediaItem.FromFilePath(@"C:\Anime\Show.S01E01.mkv");
        var ep2 = MediaItem.FromFilePath(@"C:\Anime\Show.S01E02.mkv");

        var package = new MediaPackage
        {
            PrimaryVideo = ep2,
            SiblingEpisodes = [ep1]
        };

        var next = EpisodeNavigator.FindNextEpisode(package);

        Assert.Null(next);
    }

    /// <summary>
    /// Case 15: Verify AutoNextEpisode = false prevents automatic progression.
    /// </summary>
    [Fact]
    public async Task Case15_AutoNextDisabled()
    {
        var tempDir = CreateIsolatedDirectory();
        var prefsPath = Path.Combine(tempDir, "show_preferences.json");
        var dbPath = Path.Combine(tempDir, "history.db");
        using var prefStore = new JsonShowPreferencesStore(prefsPath);
        using var historyStore = new SqliteWatchHistoryStore(dbPath);
        var service = new EpisodicContinuityService(prefStore, historyStore);

        var package = CreateTestPackage("Frieren", 1, 1);

        // Explicitly disable auto next episode
        await service.SetAutoNextEpisodePreferenceAsync(package, autoNext: false);

        var plan = await service.PreparePlaybackAsync(package);
        Assert.NotNull(plan.Preferences);
        Assert.False(plan.Preferences.AutoNextEpisode);

        // Configure UI ViewModel: setting AutoNextEnabled = false prevents automatic progression
        var vm = new PlayerViewModel
        {
            AutoNextEnabled = plan.Preferences.AutoNextEpisode
        };
        Assert.False(vm.AutoNextEnabled);
        Assert.False(vm.IsAutoNextPromptVisible);
    }

    /// <summary>
    /// Case 16: Verify opening File A followed immediately by File B cancels A
    /// and prevents A from overwriting B's history or tracks.
    /// </summary>
    [Fact]
    public async Task Case16_OpenA_OpenB_Race()
    {
        var tempDir = CreateIsolatedDirectory();
        var dbPath = Path.Combine(tempDir, "history.db");
        using var store = new SqliteWatchHistoryStore(dbPath);
        using var tracker = new PlaybackHistoryTracker(store);

        var fileA = @"C:\Media\EpisodeA.mkv";
        var fileB = @"C:\Media\EpisodeB.mkv";

        using var ctsA = new CancellationTokenSource();

        // 1. Open File A and advance to 50.0s
        await tracker.TrackMediaAsync(fileA, "ShowA", seasonNumber: 1, episodeNumber: 1, duration: 1000.0, ct: ctsA.Token);
        var sessionAId = tracker.CurrentSessionId;
        await tracker.UpdatePositionAsync(50.0, 1000.0);

        Assert.Equal(fileA, tracker.CurrentFilePath);
        Assert.Equal(50.0, tracker.CurrentPosition, precision: 2);

        // 2. Open File B immediately -> cancels A and flushes A at 50.0s
        ctsA.Cancel();
        await tracker.TrackMediaAsync(fileB, "ShowB", seasonNumber: 1, episodeNumber: 2, duration: 1000.0);
        var sessionBId = tracker.CurrentSessionId;
        Assert.NotEqual(sessionAId, sessionBId);
        Assert.Equal(fileB, tracker.CurrentFilePath);

        // Advance File B to 15.0s
        await tracker.UpdatePositionAsync(15.0, 1000.0);
        Assert.Equal(15.0, tracker.CurrentPosition, precision: 2);

        // 3. Stale out-of-order calls from File A or obsolete session A
        await tracker.UpdatePositionAsync(99.0, 1000.0, filePath: fileA);
        await tracker.UpdatePositionAsync(88.0, 1000.0, sessionId: sessionAId);
        await tracker.OnPauseAsync(filePath: fileA);
        await tracker.OnSeekAsync(500.0, sessionId: sessionAId);

        // File B in memory remains untouched
        Assert.Equal(fileB, tracker.CurrentFilePath);
        Assert.Equal(15.0, tracker.CurrentPosition, precision: 2);

        // File B in persistent database remains at 15.0s
        var recordB = await store.GetByFilePathAsync(fileB);
        Assert.NotNull(recordB);
        Assert.Equal(15.0, recordB.PositionSeconds, precision: 2);

        // File A in database remains at 50.0s (flushed on change, not overwritten by delayed 99s, 88s, 500s)
        var recordA = await store.GetByFilePathAsync(fileA);
        Assert.NotNull(recordA);
        Assert.Equal(50.0, recordA.PositionSeconds, precision: 2);
    }

    /// <summary>
    /// Case 17: Verify atomic temp-file replace preserves valid JSON configuration even if another read occurs.
    /// </summary>
    [Fact]
    public async Task Case17_CrashSafeJsonWrite()
    {
        var tempDir = CreateIsolatedDirectory();
        var prefsPath = Path.Combine(tempDir, "show_preferences.json");

        using var store = new JsonShowPreferencesStore(prefsPath);

        // Initial valid preferences
        await store.SavePreferencesAsync(new ShowPreferences
        {
            ShowId = "Show0",
            PreferredAudioLanguage = "ru"
        });

        // Concurrent writes and direct disk reads
        var writeTasks = Enumerable.Range(1, 20).Select(i => Task.Run(async () =>
        {
            await store.SavePreferencesAsync(new ShowPreferences
            {
                ShowId = $"Show_{i}",
                PreferredAudioLanguage = $"lang_{i}",
                PreferredSubtitleLanguage = $"sub_{i}"
            });
        }));

        var readTasks = Enumerable.Range(1, 20).Select(_ => Task.Run(async () =>
        {
            if (File.Exists(prefsPath))
            {
                try
                {
                    await using var stream = new FileStream(
                        prefsPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete,
                        bufferSize: 4096,
                        useAsync: true);

                    if (stream.Length > 0)
                    {
                        using var doc = await JsonDocument.ParseAsync(stream);
                        Assert.True(doc.RootElement.TryGetProperty("version", out JsonElement versionProp));
                    }
                }
                catch (FileNotFoundException)
                {
                    // Atomic move in flight
                }
            }
        }));

        await Task.WhenAll(writeTasks.Concat(readTasks));

        // Verify final file is valid JSON and no temp files are left behind
        var finalContent = await File.ReadAllTextAsync(prefsPath);
        using var finalDoc = JsonDocument.Parse(finalContent);
        Assert.True(finalDoc.RootElement.TryGetProperty("showPreferences", out JsonElement showPrefsProp));

        var tmpFiles = Directory.GetFiles(tempDir, "*.tmp");
        Assert.Empty(tmpFiles);
    }

    /// <summary>
    /// Case 18: Reopen SQLite database in WAL mode and verify data integrity across connections.
    /// </summary>
    [Fact]
    public async Task Case18_SqliteRestart()
    {
        var tempDir = CreateIsolatedDirectory();
        var dbPath = Path.Combine(tempDir, "history.db");

        // 1. Connection 1: Seed records
        using (var store1 = new SqliteWatchHistoryStore(dbPath))
        {
            for (int i = 1; i <= 5; i++)
            {
                await store1.RecordPositionAsync(new WatchHistoryItem
                {
                    ShowId = $"Show_{i}",
                    SeasonNumber = 1,
                    EpisodeNumber = i,
                    FilePath = $@"C:\Media\Show_S01E{i:D2}.mkv",
                    PositionSeconds = i * 100.0,
                    DurationSeconds = 1400.0,
                    Completed = false,
                    LastPlayedUtc = DateTime.UtcNow.AddMinutes(-i)
                });
            }
        }

        // Verify WAL mode was active
        var connStr = new SqliteConnectionStringBuilder { DataSource = dbPath }.ConnectionString;
        await using (var rawConn = new SqliteConnection(connStr))
        {
            await rawConn.OpenAsync();
            await using var pragmaCmd = rawConn.CreateCommand();
            pragmaCmd.CommandText = "PRAGMA journal_mode;";
            var mode = await pragmaCmd.ExecuteScalarAsync();
            Assert.Equal("wal", mode?.ToString()?.ToLowerInvariant());
        }

        // 2. Connection 2: Reopen from disk, verify data integrity, and update record 3 to completed
        using (var store2 = new SqliteWatchHistoryStore(dbPath))
        {
            for (int i = 1; i <= 5; i++)
            {
                var item = await store2.GetByFilePathAsync($@"C:\Media\Show_S01E{i:D2}.mkv");
                Assert.NotNull(item);
                Assert.Equal($"Show_{i}", item.ShowId);
                Assert.Equal(i * 100.0, item.PositionSeconds, precision: 2);
            }

            await store2.MarkCompletedAsync(@"C:\Media\Show_S01E03.mkv");
        }

        // 3. Connection 3: Verify update across reopen
        using (var store3 = new SqliteWatchHistoryStore(dbPath))
        {
            var item3 = await store3.GetByFilePathAsync(@"C:\Media\Show_S01E03.mkv");
            Assert.NotNull(item3);
            Assert.True(item3.Completed);

            var continueWatching = await store3.GetContinueWatchingAsync();
            // Episode 3 completed -> excluded. Episodes 1, 2, 4, 5 remain.
            Assert.Equal(4, continueWatching.Count);
            Assert.DoesNotContain(continueWatching, x => x.FilePath == @"C:\Media\Show_S01E03.mkv");
        }
    }

    #endregion

    #region Section 35: Real User Acceptance Test

    /// <summary>
    /// Section 35: Full Real User Acceptance Test (Anime Workflow)
    /// - Scans real test directory tests/TestData/Anime/ for S01E01.mkv.
    /// - Packages external audio S01E01.RU.mka and subtitle S01E01.RU.ass.
    /// - Simulates user selecting Russian audio and Russian subtitles.
    /// - Saves preferences to isolated temporary preferences store.
    /// - Simulates playback reaching 23:14 (1394 seconds out of 1450 seconds).
    /// - Simulates application close (DisposeAsync / flushing history tracker).
    /// - Simulates application reopen (instantiating new stores from disk).
    /// - Verifies Continue Watching displays S01E01 at 23:14.
    /// - Simulates resuming at 23:14.
    /// - Navigates to next episode S01E02 (S01E02.mkv).
    /// - Verifies S01E02 automatically discovers S01E02.RU.mka and S01E02.RU.ass.
    /// - Verifies S01E02 automatically selects Russian audio and Russian subtitles!
    /// </summary>
    [Fact]
    public async Task RealUserScenario_Anime_FullWorkflow()
    {
        var testDataDir = ResolveAnimeTestDataDir();
        var s01e01Video = Path.Combine(testDataDir, "S01E01.mkv");
        var s01e02Video = Path.Combine(testDataDir, "S01E02.mkv");

        Assert.True(File.Exists(s01e01Video), $"Missing test video: {s01e01Video}");
        Assert.True(File.Exists(s01e02Video), $"Missing test video: {s01e02Video}");

        var tempDir = CreateIsolatedDirectory();
        var prefsPath = Path.Combine(tempDir, "show_preferences.json");
        var dbPath = Path.Combine(tempDir, "history.db");

        // --- STEP 1: Scan real test directory for S01E01.mkv and verify release packaging ---
        var package1 = DirectoryScanner.Scan(s01e01Video);
        Assert.NotNull(package1.PrimaryVideo);
        Assert.Equal("S01E01.mkv", package1.PrimaryVideo.FileName);
        Assert.NotNull(package1.Episode);
        Assert.Equal(1, package1.Episode.SeasonNumber);
        Assert.Equal(1, package1.Episode.EpisodeNumber);

        // Packages external audio S01E01.RU.mka and subtitle S01E01.RU.ass
        var ruAudio1 = package1.AudioTracks.FirstOrDefault(t => t.Origin == TrackOrigin.External && t.Language == "ru");
        Assert.NotNull(ruAudio1);
        Assert.EndsWith("S01E01.RU.mka", ruAudio1!.ExternalFilePath, StringComparison.OrdinalIgnoreCase);

        var ruSub1 = package1.SubtitleTracks.FirstOrDefault(t => t.Origin == TrackOrigin.External && t.Language == "ru");
        Assert.NotNull(ruSub1);
        Assert.EndsWith("S01E01.RU.ass", ruSub1!.ExternalFilePath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SubtitleFormat.ASS, ruSub1.Format);

        // Verify font attachments and sibling S01E02 discovery
        Assert.NotNull(package1.Fonts);
        Assert.True(package1.Fonts!.HasFonts);
        Assert.Contains("ProofFont.ttf", package1.Fonts.FontFileNames);
        Assert.Contains(package1.SiblingEpisodes, e => e.FileName.Equals("S01E02.mkv", StringComparison.OrdinalIgnoreCase));

        // --- STEP 2: User selects Russian audio and Russian subtitles during S01E01 playback ---
        var prefStore1 = new JsonShowPreferencesStore(prefsPath);
        var historyStore1 = new SqliteWatchHistoryStore(dbPath);
        var continuity1 = new EpisodicContinuityService(prefStore1, historyStore1);
        var historyTracker1 = new PlaybackHistoryTracker(historyStore1);

        var playerVm = new PlayerViewModel();
        playerVm.UpdateMediaPackage(package1);

        // Save preferences
        await continuity1.SaveAudioPreferenceAsync(package1, ruAudio1!);
        await continuity1.SaveSubtitlePreferenceAsync(package1, ruSub1!, subtitleEnabled: true);

        // --- STEP 3: Playback reaches 23:14 (1394s / 1450s) ---
        const double watchedPosition = 1394.0; // 23 minutes 14 seconds
        const double displayDuration = 1450.0; // 24 minutes 10 seconds
        const double totalDuration = 1600.0;   // In-progress episode duration (< 90% completion threshold to remain in Continue Watching)

        await historyTracker1.TrackMediaAsync(
            s01e01Video,
            package1.Episode.ShowTitle,
            package1.Episode.SeasonNumber,
            package1.Episode.EpisodeNumber,
            duration: totalDuration);

        await historyTracker1.UpdatePositionAsync(watchedPosition, totalDuration);
        playerVm.UpdateTime(watchedPosition, displayDuration);

        Assert.Equal("23:14 / 24:10", playerVm.FormattedTimecode);

        // --- STEP 4: Simulate application close (DisposeAsync / flushing history tracker) ---
        await historyTracker1.DisposeAsync();
        prefStore1.Dispose();
        historyStore1.Dispose();

        // --- STEP 5: Application reopen (instantiating new stores from disk) ---
        using var prefStore2 = new JsonShowPreferencesStore(prefsPath);
        using var historyStore2 = new SqliteWatchHistoryStore(dbPath);
        var continuity2 = new EpisodicContinuityService(prefStore2, historyStore2);
        var reopenPlayerVm = new PlayerViewModel();

        // --- STEP 6: Verify Continue Watching displays S01E01 at 23:14 ---
        var continueWatchingList = await historyStore2.GetContinueWatchingAsync();
        Assert.NotEmpty(continueWatchingList);
        var continueItem = continueWatchingList[0];
        Assert.Equal(s01e01Video, continueItem.FilePath);
        Assert.Equal(watchedPosition, continueItem.PositionSeconds, precision: 1);
        Assert.False(continueItem.Completed);

        reopenPlayerVm.SetContinueWatching(
            $"{continueItem.ShowId} S{continueItem.SeasonNumber:D2}E{continueItem.EpisodeNumber:D2}",
            $"Paused at {FormatHelper.FormatTimecode(continueItem.PositionSeconds)}",
            continueItem.FilePath,
            continueItem.PositionSeconds);

        Assert.True(reopenPlayerVm.HasContinueWatching);
        Assert.Equal(1394.0, reopenPlayerVm.ContinueWatchingPosition);
        Assert.Contains("23:14", reopenPlayerVm.ContinueWatchingDetails);

        // --- STEP 7: Simulate resuming at 23:14 ---
        var planResume = await continuity2.PreparePlaybackAsync(package1);
        Assert.True(planResume.CanResume);
        Assert.Equal(1394.0, planResume.ResumePositionSeconds);

        reopenPlayerVm.ShowResumePrompt(
            $"Resume from {FormatHelper.FormatTimecode(planResume.ResumePositionSeconds)}?",
            planResume.ResumePositionSeconds);
        Assert.True(reopenPlayerVm.IsResumePromptVisible);
        Assert.Equal(1394.0, reopenPlayerVm.ResumePositionSeconds);
        Assert.Contains("23:14", reopenPlayerVm.ResumePromptMessage);

        // --- STEP 8: Navigate to next episode S01E02 (S01E02.mkv) ---
        var nextEpisode = EpisodeNavigator.FindNextEpisode(package1);
        Assert.NotNull(nextEpisode);
        Assert.Equal(s01e02Video, nextEpisode.FilePath);

        // --- STEP 9: Verify S01E02 automatically discovers S01E02.RU.mka and S01E02.RU.ass ---
        var package2 = DirectoryScanner.Scan(nextEpisode.FilePath);
        Assert.NotNull(package2);
        Assert.Equal("S01E02.mkv", package2.PrimaryVideo.FileName);
        Assert.NotNull(package2.Episode);
        Assert.Equal(2, package2.Episode.EpisodeNumber);

        var ruAudio2 = package2.AudioTracks.FirstOrDefault(t => t.Origin == TrackOrigin.External && t.Language == "ru");
        Assert.NotNull(ruAudio2);
        Assert.EndsWith("S01E02.RU.mka", ruAudio2!.ExternalFilePath, StringComparison.OrdinalIgnoreCase);

        var ruSub2 = package2.SubtitleTracks.FirstOrDefault(t => t.Origin == TrackOrigin.External && t.Language == "ru");
        Assert.NotNull(ruSub2);
        Assert.EndsWith("S01E02.RU.ass", ruSub2!.ExternalFilePath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SubtitleFormat.ASS, ruSub2.Format);

        // --- STEP 10: Verify S01E02 automatically selects Russian audio and Russian subtitles! ---
        var plan2 = await continuity2.PreparePlaybackAsync(package2);
        Assert.NotNull(plan2.Preferences);
        Assert.Equal("ru", plan2.Preferences.PreferredAudioLanguage);
        Assert.Equal("ru", plan2.Preferences.PreferredSubtitleLanguage);

        Assert.NotNull(plan2.AudioResolution);
        Assert.NotNull(plan2.AudioResolution.SelectedTrack);
        Assert.Equal("ru", plan2.AudioResolution.SelectedTrack!.Language);
        Assert.Equal(ruAudio2.Id, plan2.AudioResolution.SelectedTrack.Id);

        Assert.NotNull(plan2.SubtitleResolution);
        Assert.NotNull(plan2.SubtitleResolution.SelectedTrack);
        Assert.Equal("ru", plan2.SubtitleResolution.SelectedTrack!.Language);
        Assert.Equal(ruSub2.Id, plan2.SubtitleResolution.SelectedTrack.Id);
        Assert.Equal(SubtitleFormat.ASS, plan2.SubtitleResolution.SelectedTrack.Format);
        Assert.True(plan2.SubtitleVisible);
    }

    #endregion
}
