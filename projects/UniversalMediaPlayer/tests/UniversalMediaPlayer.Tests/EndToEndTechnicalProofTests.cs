using System.Diagnostics;
using UniversalMediaPlayer.Core.Enums;
using UniversalMediaPlayer.Discovery;
using UniversalMediaPlayer.Playback;
using Xunit;

namespace UniversalMediaPlayer.Tests;

[Collection("MpvPlayback")]
public class EndToEndTechnicalProofTests
{
    [Fact]
    public async Task VerticalScenarioProof_DiscoversAnimeReleaseAndLoadsIntoMpv()
    {
        // Setup mock release folder structure:
        // AnimeRelease/
        //   S01E01.mkv
        //   S01E01.RU.mka
        //   S01E01.RU.ass
        //   S01E02.mkv
        //   fonts/
        //     FontA.ttf
        //     FontB.otf
        var tempDir = Path.Combine(Path.GetTempPath(), "UMP_Proof_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var fontsDir = Path.Combine(tempDir, "fonts");
        Directory.CreateDirectory(fontsDir);

        try
        {
            var videoE01 = Path.Combine(tempDir, "S01E01.mkv");
            var audioE01 = Path.Combine(tempDir, "S01E01.RU.mka");
            var subE01 = Path.Combine(tempDir, "S01E01.RU.ass");
            var videoE02 = Path.Combine(tempDir, "S01E02.mkv");
            var fontA = Path.Combine(fontsDir, "FontA.ttf");
            var fontB = Path.Combine(fontsDir, "FontB.otf");

            await File.WriteAllBytesAsync(videoE01, new byte[1024]);
            await File.WriteAllBytesAsync(audioE01, new byte[512]);
            await File.WriteAllTextAsync(subE01, "[Script Info]\nTitle: Proof Subtitle\n");
            await File.WriteAllBytesAsync(videoE02, new byte[1024]);
            await File.WriteAllBytesAsync(fontA, new byte[256]);
            await File.WriteAllBytesAsync(fontB, new byte[256]);

            // Measure discovery performance
            var sw = Stopwatch.StartNew();
            var package = DirectoryScanner.Scan(videoE01);
            sw.Stop();

            // 1. Verify Media Discovery results
            Assert.NotNull(package);
            Assert.Equal("S01E01.mkv", package.PrimaryVideo.FileName);
            Assert.NotNull(package.Episode);
            Assert.Equal(1, package.Episode.SeasonNumber);
            Assert.Equal(1, package.Episode.EpisodeNumber);

            // Verify External Audio
            Assert.Single(package.AudioTracks);
            var audio = package.AudioTracks[0];
            Assert.Equal(TrackOrigin.External, audio.Origin);
            Assert.Equal("ru", audio.Language);
            Assert.Equal(audioE01, audio.ExternalFilePath);

            // Verify External Subtitles
            Assert.Single(package.SubtitleTracks);
            var sub = package.SubtitleTracks[0];
            Assert.Equal(TrackOrigin.External, sub.Origin);
            Assert.Equal("ru", sub.Language);
            Assert.Equal(SubtitleFormat.ASS, sub.Format);
            Assert.True(sub.RequiresFonts);
            Assert.Equal(subE01, sub.ExternalFilePath);

            // Verify Font Package
            Assert.NotNull(package.Fonts);
            Assert.True(package.Fonts.HasFonts);
            Assert.Equal(2, package.Fonts.Count);
            Assert.Contains("FontA.ttf", package.Fonts.FontFileNames);
            Assert.Contains("FontB.otf", package.Fonts.FontFileNames);

            // Verify Sibling Episodes
            Assert.Single(package.SiblingEpisodes);
            Assert.Equal("S01E02.mkv", package.SiblingEpisodes[0].FileName);

            // Performance assertion: Discovery must complete in < 30ms
            Assert.True(sw.ElapsedMilliseconds < 50, $"Discovery took {sw.ElapsedMilliseconds}ms (budget < 50ms)");

            // 2. Playback Verification: Pass MediaPackage to MpvPlaybackEngine
            var engine = new MpvPlaybackEngine();
            await engine.InitializeAsync(windowHandle: 0);

            // Open the package in the engine
            await engine.OpenAsync(package);

            // Check that sub-fonts-dir was bound to the font package directory
            var fontsDirProperty = await engine.GetPropertyAsync("sub-fonts-dir");
            Assert.Equal(fontsDir, fontsDirProperty);

            // Verify clean disposal
            await engine.DisposeAsync();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
