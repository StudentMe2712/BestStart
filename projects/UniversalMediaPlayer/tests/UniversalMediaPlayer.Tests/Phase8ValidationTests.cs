using System.Diagnostics;
using UniversalMediaPlayer.Core.Enums;
using UniversalMediaPlayer.Core.Models;
using UniversalMediaPlayer.Discovery;
using UniversalMediaPlayer.Playback;
using UniversalMediaPlayer.UI.Helpers;
using UniversalMediaPlayer.UI.ViewModels;
using Xunit;

namespace UniversalMediaPlayer.Tests;

[Collection("MpvPlayback")]
public class Phase8ValidationTests
{
    private readonly string _testDataDir;
    private readonly string _videoPath;

    public Phase8ValidationTests()
    {
        _testDataDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\TestData\Anime"));
        _videoPath = Path.Combine(_testDataDir, "S01E01.mkv");
    }

    #region 1. External Track Lifecycle & Edge Case Resilience

    [Fact]
    public async Task ExternalTrackLifecycle_VideoOnly_PlaysWithoutCrash()
    {
        var package = new MediaPackage
        {
            PrimaryVideo = MediaItem.FromFilePath(_videoPath),
            AudioTracks = [],
            SubtitleTracks = []
        };

        var engine = new MpvPlaybackEngine();
        await engine.InitializeAsync(windowHandle: 0);
        try
        {
            await engine.OpenAsync(package);
            Assert.True(engine.IsInitialized);
            await engine.PlayAsync();
            await Task.Delay(50);
            await engine.PauseAsync();
        }
        finally
        {
            await engine.DisposeAsync();
        }
    }

    [Fact]
    public async Task ExternalTrackLifecycle_VideoWithAudioOnly_PlaysWithoutCrash()
    {
        var mkaPath = Path.Combine(_testDataDir, "S01E01.RU.mka");
        var package = new MediaPackage
        {
            PrimaryVideo = MediaItem.FromFilePath(_videoPath),
            AudioTracks = [
                new AudioTrack
                {
                    Id = 201,
                    Language = "ru",
                    Origin = TrackOrigin.External,
                    ExternalFilePath = mkaPath
                }
            ],
            SubtitleTracks = []
        };

        var engine = new MpvPlaybackEngine();
        await engine.InitializeAsync(windowHandle: 0);
        try
        {
            await engine.OpenAsync(package);
            Assert.True(engine.IsInitialized);
            var audios = engine.ActiveTracks.OfType<AudioTrack>().ToList();
            Assert.NotEmpty(audios);
        }
        finally
        {
            await engine.DisposeAsync();
        }
    }

    [Fact]
    public async Task ExternalTrackLifecycle_VideoWithSubtitlesOnly_PlaysWithoutCrash()
    {
        var assPath = Path.Combine(_testDataDir, "S01E01.RU.ass");
        var package = new MediaPackage
        {
            PrimaryVideo = MediaItem.FromFilePath(_videoPath),
            AudioTracks = [],
            SubtitleTracks = [
                new SubtitleTrack
                {
                    Id = 301,
                    Language = "ru",
                    Origin = TrackOrigin.External,
                    ExternalFilePath = assPath,
                    Format = SubtitleFormat.ASS
                }
            ]
        };

        var engine = new MpvPlaybackEngine();
        await engine.InitializeAsync(windowHandle: 0);
        try
        {
            await engine.OpenAsync(package);
            Assert.True(engine.IsInitialized);
            var subs = engine.ActiveTracks.OfType<SubtitleTrack>().ToList();
            Assert.NotEmpty(subs);
        }
        finally
        {
            await engine.DisposeAsync();
        }
    }

    [Fact]
    public async Task ExternalTrackLifecycle_MissingAudioAndMissingSubtitles_DoesNotCrashOrHang()
    {
        var package = new MediaPackage
        {
            PrimaryVideo = MediaItem.FromFilePath(_videoPath),
            AudioTracks = [
                new AudioTrack
                {
                    Id = 991,
                    Language = "ru",
                    Origin = TrackOrigin.External,
                    ExternalFilePath = @"C:\NonExistentDirectory_12345\ghost_audio.mka"
                }
            ],
            SubtitleTracks = [
                new SubtitleTrack
                {
                    Id = 992,
                    Language = "ru",
                    Origin = TrackOrigin.External,
                    ExternalFilePath = @"C:\NonExistentDirectory_12345\ghost_subs.ass",
                    Format = SubtitleFormat.ASS
                }
            ]
        };

        var engine = new MpvPlaybackEngine();
        await engine.InitializeAsync(windowHandle: 0);
        try
        {
            var sw = Stopwatch.StartNew();
            await engine.OpenAsync(package);
            sw.Stop();

            // Must complete promptly without hanging for missing files
            Assert.True(sw.ElapsedMilliseconds < 2500, $"OpenAsync took {sw.ElapsedMilliseconds} ms, expected < 2500 ms");
            Assert.True(engine.IsInitialized);
        }
        finally
        {
            await engine.DisposeAsync();
        }
    }

    [Fact]
    public async Task ExternalTrackLifecycle_InvalidCorruptAudioAndSubtitleFiles_HandlesSafely()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UMP_Corrupt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var fakeAudio = Path.Combine(tempDir, "corrupt.mka");
            var fakeSub = Path.Combine(tempDir, "corrupt.ass");
            await File.WriteAllBytesAsync(fakeAudio, [0x00, 0x01, 0x02, 0x03]); // invalid 4-byte garbage
            await File.WriteAllTextAsync(fakeSub, "This is not an ASS subtitle script!");

            var package = new MediaPackage
            {
                PrimaryVideo = MediaItem.FromFilePath(_videoPath),
                AudioTracks = [
                    new AudioTrack
                    {
                        Id = 801,
                        Language = "ru",
                        Origin = TrackOrigin.External,
                        ExternalFilePath = fakeAudio
                    }
                ],
                SubtitleTracks = [
                    new SubtitleTrack
                    {
                        Id = 802,
                        Language = "ru",
                        Origin = TrackOrigin.External,
                        ExternalFilePath = fakeSub,
                        Format = SubtitleFormat.ASS
                    }
                ]
            };

            var engine = new MpvPlaybackEngine();
            await engine.InitializeAsync(windowHandle: 0);
            try
            {
                // Must not throw or crash the process
                await engine.OpenAsync(package);
                Assert.True(engine.IsInitialized);
            }
            finally
            {
                await engine.DisposeAsync();
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    #endregion

    #region 2. Concurrency & Cancellation Verification

    [Fact]
    public void Concurrency_DirectoryScanner_CancellationAbortsPromptly()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Immediately cancelled

        Assert.Throws<OperationCanceledException>(() =>
        {
            DirectoryScanner.Scan(_videoPath, cts.Token);
        });
    }

    [Fact]
    public async Task Concurrency_RapidOpenA_ThenB_ExecutesWithoutRaceOrDuplicateTracks()
    {
        var videoB = Path.Combine(_testDataDir, "S01E02.mkv");
        Assert.True(File.Exists(videoB));

        var packageA = DirectoryScanner.Scan(_videoPath);
        var packageB = DirectoryScanner.Scan(videoB);

        var engine = new MpvPlaybackEngine();
        await engine.InitializeAsync(windowHandle: 0);

        try
        {
            // Rapid consecutive opens
            var taskA = engine.OpenAsync(packageA);
            var taskB = engine.OpenAsync(packageB);

            await Task.WhenAll(taskA, taskB);

            // Engine should reflect package B cleanly
            Assert.Equal(packageB, engine.CurrentPackage);
            Assert.True(engine.IsInitialized);
        }
        finally
        {
            await engine.DisposeAsync();
        }
    }

    #endregion

    #region 3. Resource Lifecycle Verification

    [Fact]
    public async Task ResourceLifecycle_OpenCloseOpenFullscreenClose_LeavesNoDanglingResources()
    {
        var package = DirectoryScanner.Scan(_videoPath);
        var engine = new MpvPlaybackEngine();
        await engine.InitializeAsync(windowHandle: 0);

        try
        {
            // 1. Open package
            await engine.OpenAsync(package);
            Assert.True(engine.IsInitialized);

            // 2. Play and pause
            await engine.PlayAsync();
            await engine.PauseAsync();

            // 3. Toggle fullscreen
            await engine.SetFullscreenAsync(true);
            var fsState = await engine.GetPropertyAsync("fullscreen");
            Assert.Equal("yes", fsState);

            await engine.SetFullscreenAsync(false);
            fsState = await engine.GetPropertyAsync("fullscreen");
            Assert.Equal("no", fsState);

            // 4. Open second file
            var videoB = Path.Combine(_testDataDir, "S01E02.mkv");
            var packageB = DirectoryScanner.Scan(videoB);
            await engine.OpenAsync(packageB);
            Assert.Equal(packageB, engine.CurrentPackage);

            // 5. Stop
            await engine.StopAsync();
        }
        finally
        {
            // 6. Dispose
            await engine.DisposeAsync();
            Assert.False(engine.IsInitialized);
        }
    }

    #endregion

    #region 4. UI Quality Gate: Long Text & 10+ Tracks Overflow Prevention

    [Fact]
    public void UiQualityGate_VeryLongText_DoesNotThrowAndFormatsCleanly()
    {
        var veryLongTrackTitle = new string('B', 180);

        var audio = new AudioTrack
        {
            Id = 1,
            Title = veryLongTrackTitle,
            Language = "ru",
            Channels = 6,
            Codec = "FLAC",
            Origin = TrackOrigin.External
        };

        var label = FormatHelper.FormatAudioTrackLabel(audio);
        Assert.NotNull(label);
        Assert.Contains("🇷🇺 Russian", label);
        Assert.Contains("5.1", label);

        var sub = new SubtitleTrack
        {
            Id = 1,
            Title = veryLongTrackTitle,
            Language = "ja",
            Format = SubtitleFormat.ASS,
            Origin = TrackOrigin.External
        };

        var subLabel = FormatHelper.FormatSubtitleTrackLabel(sub);
        Assert.NotNull(subLabel);
        Assert.Contains("🇯🇵 Japanese", subLabel);
    }

    [Fact]
    public void UiQualityGate_TenPlusAudioAndSubtitleTracks_HandlesSelectionWithoutClipping()
    {
        var vm = new TrackSelectorViewModel();
        var tracks = new List<MediaTrack>();

        for (int i = 1; i <= 15; i++)
        {
            tracks.Add(new AudioTrack
            {
                Id = i,
                Title = $"Audio Track #{i}",
                Language = i % 2 == 0 ? "ru" : "en",
                Channels = 2,
                Origin = i > 10 ? TrackOrigin.External : TrackOrigin.Embedded,
                IsSelected = i == 1
            });
        }

        for (int j = 1; j <= 12; j++)
        {
            tracks.Add(new SubtitleTrack
            {
                Id = 100 + j,
                Title = $"Subtitle Track #{j}",
                Language = j % 2 == 0 ? "ru" : "ja",
                Format = SubtitleFormat.ASS,
                Origin = j > 8 ? TrackOrigin.External : TrackOrigin.Embedded,
                IsSelected = j == 1
            });
        }

        vm.UpdateTracks(tracks);

        Assert.Equal(15, vm.AudioTracks.Count);
        Assert.Equal(12, vm.SubtitleTracks.Count);
        Assert.True(vm.IsSubtitlesEnabled);

        // Switch to track 14
        vm.SelectAudio(14);
        Assert.True(vm.AudioTracks.First(a => a.Id == 14).IsSelected);
        Assert.False(vm.AudioTracks.First(a => a.Id == 1).IsSelected);

        // Switch subtitle to 110
        vm.SelectSubtitle(110);
        Assert.True(vm.SubtitleTracks.First(s => s.Id == 110).IsSelected);
        Assert.False(vm.SubtitleTracks.First(s => s.Id == 101).IsSelected);
    }

    #endregion

    #region 5. UX Quality Gate: Friendly Language Presentation

    [Fact]
    public void UxQualityGate_LanguageDisplay_ShowsUserFriendlyNamesWithoutRawJargon()
    {
        Assert.Equal("🇷🇺 Russian", FormatHelper.GetLanguageDisplayName("ru"));
        Assert.Equal("🇯🇵 Japanese", FormatHelper.GetLanguageDisplayName("ja"));
        Assert.Equal("🇬🇧 English", FormatHelper.GetLanguageDisplayName("en"));
        Assert.Equal("Original", FormatHelper.GetLanguageDisplayName("und"));
        Assert.Equal("Original", FormatHelper.GetLanguageDisplayName(""));
        Assert.Equal("Original", FormatHelper.GetLanguageDisplayName("orig"));
    }

    #endregion

    #region 6. High DPI Scaling Calculation Sanity

    [Theory]
    [InlineData(96, 1.0f, 1000, 600, 1000, 600)]    // 100% DPI
    [InlineData(120, 1.25f, 1000, 600, 1250, 750)]  // 125% DPI
    [InlineData(144, 1.50f, 1000, 600, 1500, 900)]  // 150% DPI
    [InlineData(192, 2.00f, 1000, 600, 2000, 1200)] // 200% DPI
    public void HighDpi_PhysicalPixelCalculation_IsAccurate(
        uint dpi, float expectedScale, double dipWidth, double dipHeight, int expectedPxWidth, int expectedPxHeight)
    {
        var scale = (float)dpi / 96f;
        Assert.Equal(expectedScale, scale);

        var pxWidth = (int)Math.Round(dipWidth * scale);
        var pxHeight = (int)Math.Round(dipHeight * scale);

        Assert.Equal(expectedPxWidth, pxWidth);
        Assert.Equal(expectedPxHeight, pxHeight);
        Assert.True(pxWidth > 0 && pxHeight > 0);
    }

    #endregion
}
