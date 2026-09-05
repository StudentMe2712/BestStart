using System.Diagnostics;
using UniversalMediaPlayer.Core.Enums;
using UniversalMediaPlayer.Core.Models;
using UniversalMediaPlayer.Discovery;
using UniversalMediaPlayer.Playback;
using Xunit;
using Xunit.Abstractions;

namespace UniversalMediaPlayer.Tests;

public class RealMediaSmokeTests
{
    private readonly ITestOutputHelper _output;

    public RealMediaSmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task RealMediaSmokeTest_FullScenarioValidationAndPerformance()
    {
        var testDataDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\TestData\Anime"));
        var videoPath = Path.Combine(testDataDir, "S01E01.mkv");

        Assert.True(File.Exists(videoPath), $"Test video does not exist at {videoPath}");

        // 1. Measure Media Discovery Performance (Cold run includes JIT compilation)
        var discoverySw = Stopwatch.StartNew();
        var package = DirectoryScanner.Scan(videoPath);
        discoverySw.Stop();

        _output.WriteLine($"[Benchmark] Directory discovery (cold) took: {discoverySw.ElapsedMilliseconds} ms");
        Assert.True(discoverySw.ElapsedMilliseconds < 150, $"Cold discovery budget exceeded: {discoverySw.ElapsedMilliseconds} ms");

        // Warm run measurement (pure algorithmic execution)
        var warmSw = Stopwatch.StartNew();
        DirectoryScanner.Scan(videoPath);
        warmSw.Stop();
        _output.WriteLine($"[Benchmark] Directory discovery (warm) took: {warmSw.ElapsedMilliseconds} ms");
        Assert.True(warmSw.ElapsedMilliseconds < 30, $"Warm discovery budget exceeded: {warmSw.ElapsedMilliseconds} ms");

        // Verify MediaPackage
        Assert.Equal("S01E01.mkv", package.PrimaryVideo.FileName);
        Assert.NotNull(package.Episode);
        Assert.Equal(1, package.Episode.SeasonNumber);
        Assert.Equal(1, package.Episode.EpisodeNumber);

        Assert.Single(package.AudioTracks);
        Assert.Equal("ru", package.AudioTracks[0].Language);
        Assert.Equal(TrackOrigin.External, package.AudioTracks[0].Origin);

        Assert.Single(package.SubtitleTracks);
        Assert.Equal("ru", package.SubtitleTracks[0].Language);
        Assert.Equal(SubtitleFormat.ASS, package.SubtitleTracks[0].Format);
        Assert.True(package.SubtitleTracks[0].RequiresFonts);

        Assert.NotNull(package.Fonts);
        Assert.True(package.Fonts.HasFonts);
        Assert.Contains("ProofFont.ttf", package.Fonts.FontFileNames);

        Assert.Single(package.SiblingEpisodes);
        Assert.Equal("S01E02.mkv", package.SiblingEpisodes[0].FileName);

        // 2. Measure Engine Cold Launch
        var initSw = Stopwatch.StartNew();
        var engine = new MpvPlaybackEngine();
        await engine.InitializeAsync(windowHandle: 0);
        initSw.Stop();

        _output.WriteLine($"[Benchmark] Engine initialization took: {initSw.ElapsedMilliseconds} ms");
        Assert.True(engine.IsInitialized);

        // 3. Measure Media Open to First Frame / Metadata
        var openSw = Stopwatch.StartNew();
        await engine.OpenAsync(package);
        openSw.Stop();

        _output.WriteLine($"[Benchmark] MediaPackage Open took: {openSw.ElapsedMilliseconds} ms");

        // Allow mpv worker thread to process file demuxing
        await Task.Delay(200);
        await engine.RefreshTrackListAsync();

        // 4. Verify Active Tracks in Engine
        var tracks = engine.ActiveTracks;
        _output.WriteLine($"[Playback] Active Tracks count: {tracks.Count}");
        foreach (var t in tracks)
        {
            _output.WriteLine($"  - Track #{t.Id}: {t.Title} [{t.Codec}] ({t.Language}) Origin={t.Origin}");
        }

        // Verify external tracks are registered in engine
        var hasExternalAudio = tracks.Any(t => t is AudioTrack && t.Origin == TrackOrigin.External);
        var hasExternalSub = tracks.Any(t => t is SubtitleTrack && t.Origin == TrackOrigin.External);

        Assert.True(hasExternalAudio, "Engine should report registered external audio track.");
        Assert.True(hasExternalSub, "Engine should report registered external subtitle track.");

        // 5. Test Track Selection
        var extAudio = tracks.First(t => t is AudioTrack && t.Origin == TrackOrigin.External);
        var extSub = tracks.First(t => t is SubtitleTrack && t.Origin == TrackOrigin.External);

        await engine.SelectAudioTrackAsync(extAudio.Id);
        await engine.SelectSubtitleTrackAsync(extSub.Id);

        var aid = await engine.GetPropertyAsync("aid");
        var sid = await engine.GetPropertyAsync("sid");
        Assert.Equal(extAudio.Id.ToString(), aid);
        Assert.Equal(extSub.Id.ToString(), sid);

        // 6. Test Playback & Seeking
        await engine.PlayAsync();
        await Task.Delay(100);

        await engine.SeekAsync(0.5, relative: false);
        await Task.Delay(50);

        // 7. Measure Memory Footprint
        var process = Process.GetCurrentProcess();
        process.Refresh();
        var workingSetMb = process.WorkingSet64 / (1024.0 * 1024.0);
        _output.WriteLine($"[Benchmark] Working Set RAM: {workingSetMb:F2} MB");
        Assert.True(workingSetMb < 150.0, $"Working set exceeded budget: {workingSetMb:F2} MB");

        // 8. Clean Disposal
        await engine.DisposeAsync();
        Assert.False(engine.IsInitialized);
    }
}
