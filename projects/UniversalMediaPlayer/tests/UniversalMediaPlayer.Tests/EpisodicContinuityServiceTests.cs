namespace UniversalMediaPlayer.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UniversalMediaPlayer.Core.Enums;
using UniversalMediaPlayer.Core.Models;
using UniversalMediaPlayer.Core.Persistence;
using UniversalMediaPlayer.Discovery;
using UniversalMediaPlayer.Persistence;
using Xunit;

public sealed class EpisodicContinuityServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _prefsPath;
    private readonly string _dbPath;

    public EpisodicContinuityServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "UMP_ContinuityTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _prefsPath = Path.Combine(_tempDir, "show_preferences.json");
        _dbPath = Path.Combine(_tempDir, "history.db");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    private (EpisodicContinuityService Service, JsonShowPreferencesStore PrefStore, SqliteWatchHistoryStore HistoryStore) CreateRealServices()
    {
        var prefStore = new JsonShowPreferencesStore(_prefsPath);
        var historyStore = new SqliteWatchHistoryStore(_dbPath);
        var service = new EpisodicContinuityService(prefStore, historyStore);
        return (service, prefStore, historyStore);
    }

    [Fact]
    public async Task PreparePlaybackAsync_CorrectlyResolvesPreferences_ForS01E02_AfterS01E01SetRuAudioAndSub()
    {
        var (service, prefStore, historyStore) = CreateRealServices();
        using (prefStore)
        using (historyStore)
        {
            // Episode 1 tracks
            var ep1RuAudio = new AudioTrack
            {
                Id = 1,
                Title = "[AniLibria] Russian Dub",
                Language = "ru",
                Codec = "FLAC",
                Channels = 6,
                Origin = TrackOrigin.External
            };
            var ep1JaAudio = new AudioTrack
            {
                Id = 2,
                Title = "Japanese Original",
                Language = "ja",
                Codec = "AAC",
                Channels = 2,
                Origin = TrackOrigin.Embedded
            };
            var ep1RuSub = new SubtitleTrack
            {
                Id = 10,
                Title = "Russian Full",
                Language = "ru",
                Format = SubtitleFormat.ASS,
                Origin = TrackOrigin.External
            };
            var ep1EnSub = new SubtitleTrack
            {
                Id = 11,
                Title = "English Subs",
                Language = "en",
                Format = SubtitleFormat.SRT,
                Origin = TrackOrigin.Embedded
            };

            var ep1 = new MediaPackage
            {
                PrimaryVideo = new MediaItem
                {
                    FilePath = @"C:\Anime\Sousou no Frieren\Sousou no Frieren - S01E01.mkv",
                    FileName = "Sousou no Frieren - S01E01.mkv",
                    Extension = "mkv"
                },
                Episode = new EpisodeInfo
                {
                    ShowTitle = "Sousou no Frieren",
                    SeasonNumber = 1,
                    EpisodeNumber = 1
                },
                AudioTracks = new List<AudioTrack> { ep1JaAudio, ep1RuAudio },
                SubtitleTracks = new List<SubtitleTrack> { ep1EnSub, ep1RuSub }
            };

            // Save user preferences during Episode 1 playback
            await service.SaveAudioPreferenceAsync(ep1, ep1RuAudio);
            await service.SaveSubtitlePreferenceAsync(ep1, ep1RuSub, subtitleEnabled: true);

            // Episode 2 package
            var ep2JaAudio = new AudioTrack
            {
                Id = 1,
                Title = "Japanese Original",
                Language = "ja",
                Codec = "AAC",
                Channels = 2,
                Origin = TrackOrigin.Embedded
            };
            var ep2RuAudio = new AudioTrack
            {
                Id = 2,
                Title = "[AniLibria] Russian Dub",
                Language = "ru",
                Codec = "FLAC",
                Channels = 6,
                Origin = TrackOrigin.External
            };
            var ep2EnSub = new SubtitleTrack
            {
                Id = 20,
                Title = "English Subs",
                Language = "en",
                Format = SubtitleFormat.SRT,
                Origin = TrackOrigin.Embedded
            };
            var ep2RuSub = new SubtitleTrack
            {
                Id = 21,
                Title = "Russian Full",
                Language = "ru",
                Format = SubtitleFormat.ASS,
                Origin = TrackOrigin.External
            };

            var ep2 = new MediaPackage
            {
                PrimaryVideo = new MediaItem
                {
                    FilePath = @"C:\Anime\Sousou no Frieren\Sousou no Frieren - S01E02.mkv",
                    FileName = "Sousou no Frieren - S01E02.mkv",
                    Extension = "mkv"
                },
                Episode = new EpisodeInfo
                {
                    ShowTitle = "Sousou no Frieren",
                    SeasonNumber = 1,
                    EpisodeNumber = 2
                },
                AudioTracks = new List<AudioTrack> { ep2JaAudio, ep2RuAudio },
                SubtitleTracks = new List<SubtitleTrack> { ep2EnSub, ep2RuSub }
            };

            // Prepare playback for Episode 2
            var plan = await service.PreparePlaybackAsync(ep2);

            Assert.NotNull(plan);
            Assert.Equal("sousou no frieren", plan.ShowId);
            Assert.NotNull(plan.Preferences);
            Assert.Equal("ru", plan.Preferences.PreferredAudioLanguage);
            Assert.Equal("ru", plan.Preferences.PreferredSubtitleLanguage);
            Assert.True(plan.SubtitleVisible);

            // Audio resolution should automatically select RU
            Assert.NotNull(plan.AudioResolution);
            Assert.NotNull(plan.AudioResolution.SelectedTrack);
            Assert.Equal("ru", plan.AudioResolution.SelectedTrack.Language);
            Assert.Equal(2, plan.AudioResolution.SelectedTrack.Id);

            // Subtitle resolution should automatically select RU
            Assert.NotNull(plan.SubtitleResolution);
            Assert.NotNull(plan.SubtitleResolution.SelectedTrack);
            Assert.Equal("ru", plan.SubtitleResolution.SelectedTrack.Language);
            Assert.Equal(21, plan.SubtitleResolution.SelectedTrack.Id);
        }
    }

    [Fact]
    public async Task PreparePlaybackAsync_SubtitleDisabledPreference_PersistsAndHidesSubtitles()
    {
        var (service, prefStore, historyStore) = CreateRealServices();
        using (prefStore)
        using (historyStore)
        {
            var ep1 = new MediaPackage
            {
                PrimaryVideo = new MediaItem
                {
                    FilePath = @"C:\Anime\Oshi no Ko\Oshi no Ko - S01E01.mkv",
                    FileName = "Oshi no Ko - S01E01.mkv",
                    Extension = "mkv"
                },
                Episode = new EpisodeInfo
                {
                    ShowTitle = "Oshi no Ko",
                    SeasonNumber = 1,
                    EpisodeNumber = 1
                }
            };

            // User explicitly disables subtitles
            await service.SaveSubtitlePreferenceAsync(ep1, null, subtitleEnabled: false);

            var ep2Subs = new List<SubtitleTrack>
            {
                new()
                {
                    Id = 1,
                    Title = "English Full",
                    Language = "en",
                    Format = SubtitleFormat.ASS,
                    Origin = TrackOrigin.External
                }
            };

            var ep2 = new MediaPackage
            {
                PrimaryVideo = new MediaItem
                {
                    FilePath = @"C:\Anime\Oshi no Ko\Oshi no Ko - S01E02.mkv",
                    FileName = "Oshi no Ko - S01E02.mkv",
                    Extension = "mkv"
                },
                Episode = new EpisodeInfo
                {
                    ShowTitle = "Oshi no Ko",
                    SeasonNumber = 1,
                    EpisodeNumber = 2
                },
                SubtitleTracks = ep2Subs
            };

            var plan = await service.PreparePlaybackAsync(ep2);

            Assert.NotNull(plan);
            Assert.NotNull(plan.Preferences);
            Assert.False(plan.Preferences.SubtitleEnabled);
            Assert.False(plan.SubtitleVisible);
            Assert.NotNull(plan.SubtitleResolution);
            Assert.Null(plan.SubtitleResolution.SelectedTrack);
            Assert.Equal(TrackSelectionReason.ExplicitlyDisabled, plan.SubtitleResolution.Reason);
        }
    }

    [Theory]
    [InlineData(10.0, 1000.0, false, false, "Position <= 15 seconds")]
    [InlineData(15.0, 1000.0, false, false, "Position == 15 seconds")]
    [InlineData(16.0, 1000.0, false, true, "Position > 15 seconds with plenty remaining")]
    [InlineData(100.0, 1000.0, false, true, "Normal watch progress")]
    [InlineData(990.0, 1000.0, false, false, "Remaining <= 15 seconds")]
    [InlineData(984.0, 1000.0, false, true, "Remaining > 15 seconds")]
    [InlineData(500.0, 1000.0, true, false, "Completed flag is true")]
    [InlineData(30.0, 0.0, false, true, "Zero duration stream with position > 15")]
    [InlineData(30.0, -1.0, false, true, "Negative duration with position > 15")]
    public void PlaybackPreparationPlan_CanResume_EvaluatesCorrectly(
        double position,
        double duration,
        bool completed,
        bool expectedCanResume,
        string explanation)
    {
        var history = new WatchHistoryItem
        {
            ShowId = "TestShow",
            FilePath = @"C:\Media\test.mkv",
            PositionSeconds = position,
            DurationSeconds = duration,
            Completed = completed
        };

        var plan = new PlaybackPreparationPlan
        {
            ShowId = "TestShow",
            ResumeHistory = history
        };

        Assert.True(plan.CanResume == expectedCanResume, explanation);
        Assert.Equal(position, plan.ResumePositionSeconds);
    }

    [Fact]
    public void PlaybackPreparationPlan_NullResumeHistory_CannotResume()
    {
        var plan = new PlaybackPreparationPlan
        {
            ShowId = "TestShow",
            ResumeHistory = null
        };

        Assert.False(plan.CanResume);
        Assert.Equal(0.0, plan.ResumePositionSeconds);
    }

    [Fact]
    public async Task SaveAudioAndSubtitlePreferencesAsync_WritesAtomicPreferencesToStore()
    {
        var (service, prefStore, historyStore) = CreateRealServices();
        using (prefStore)
        using (historyStore)
        {
            var package = new MediaPackage
            {
                PrimaryVideo = new MediaItem
                {
                    FilePath = @"C:\Anime\Dungeon Meshi\Dungeon Meshi - S01E01.mkv",
                    FileName = "Dungeon Meshi - S01E01.mkv",
                    Extension = "mkv"
                },
                Episode = new EpisodeInfo
                {
                    ShowTitle = "Dungeon Meshi",
                    SeasonNumber = 1,
                    EpisodeNumber = 1
                }
            };

            var audio = new AudioTrack
            {
                Id = 1,
                Title = "English Dub",
                Language = "en",
                Codec = "EAC3",
                Channels = 6,
                Origin = TrackOrigin.Embedded
            };

            // 1. Save audio preference
            await service.SaveAudioPreferenceAsync(package, audio);

            var retrieved1 = await prefStore.GetPreferencesAsync("dungeon meshi");
            Assert.NotNull(retrieved1);
            Assert.Equal("en", retrieved1.PreferredAudioLanguage);
            Assert.NotNull(retrieved1.PreferredAudioTrack);
            Assert.Equal("en", retrieved1.PreferredAudioTrack.Language);
            Assert.Equal("English Dub", retrieved1.PreferredAudioTrack.Title);
            Assert.Equal("EAC3", retrieved1.PreferredAudioTrack.Codec);
            Assert.Equal(6, retrieved1.PreferredAudioTrack.Channels);
            Assert.Equal(TrackOrigin.Embedded, retrieved1.PreferredAudioTrack.Origin);
            Assert.Null(retrieved1.PreferredSubtitleLanguage);
            Assert.True(retrieved1.AutoNextEpisode);

            // 2. Save subtitle preference - must preserve audio preference atomically
            var sub = new SubtitleTrack
            {
                Id = 2,
                Title = "English Signs & Songs",
                Language = "en",
                Format = SubtitleFormat.ASS,
                Origin = TrackOrigin.External
            };

            await service.SaveSubtitlePreferenceAsync(package, sub, subtitleEnabled: true);

            var retrieved2 = await prefStore.GetPreferencesAsync("dungeon meshi");
            Assert.NotNull(retrieved2);
            // Audio preferences are intact
            Assert.Equal("en", retrieved2.PreferredAudioLanguage);
            Assert.NotNull(retrieved2.PreferredAudioTrack);
            Assert.Equal("English Dub", retrieved2.PreferredAudioTrack.Title);
            // Subtitle preferences are set
            Assert.Equal("en", retrieved2.PreferredSubtitleLanguage);
            Assert.True(retrieved2.SubtitleEnabled);
            Assert.NotNull(retrieved2.PreferredSubtitleTrack);
            Assert.Equal("en", retrieved2.PreferredSubtitleTrack.Language);
            Assert.Equal("English Signs & Songs", retrieved2.PreferredSubtitleTrack.Title);
            Assert.Equal(SubtitleFormat.ASS, retrieved2.PreferredSubtitleTrack.Format);
            Assert.Equal(TrackOrigin.External, retrieved2.PreferredSubtitleTrack.Origin);

            // 3. Set auto-next episode preference
            await service.SetAutoNextEpisodePreferenceAsync(package, autoNext: false);

            var retrieved3 = await prefStore.GetPreferencesAsync("dungeon meshi");
            Assert.NotNull(retrieved3);
            Assert.False(retrieved3.AutoNextEpisode);
            Assert.Equal("en", retrieved3.PreferredAudioLanguage);
            Assert.Equal("en", retrieved3.PreferredSubtitleLanguage);
            Assert.True(retrieved3.SubtitleEnabled);
        }
    }
}
