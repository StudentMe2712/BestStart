using System.Diagnostics;
using UniversalMediaPlayer.Core.Enums;
using UniversalMediaPlayer.Core.Models;
using UniversalMediaPlayer.Discovery;
using UniversalMediaPlayer.Playback;
using UniversalMediaPlayer.UI.Helpers;
using UniversalMediaPlayer.UI.Services;
using UniversalMediaPlayer.UI.ViewModels;
using Xunit;

namespace UniversalMediaPlayer.Tests;

[Collection("MpvPlayback")]
public class UiWorkflowTests
{
    [Theory]
    [InlineData(KeyInput.Space, false, false, PlayerAction.PlayPause)]
    [InlineData(KeyInput.Left, false, false, PlayerAction.SeekBackwardSmall)]
    [InlineData(KeyInput.Left, true, false, PlayerAction.SeekBackwardLarge)]
    [InlineData(KeyInput.Right, false, false, PlayerAction.SeekForwardSmall)]
    [InlineData(KeyInput.Right, true, false, PlayerAction.SeekForwardLarge)]
    [InlineData(KeyInput.Up, false, false, PlayerAction.VolumeUp)]
    [InlineData(KeyInput.Down, false, false, PlayerAction.VolumeDown)]
    [InlineData(KeyInput.M, false, false, PlayerAction.ToggleMute)]
    [InlineData(KeyInput.F, false, false, PlayerAction.ToggleFullscreen)]
    [InlineData(KeyInput.Enter, false, true, PlayerAction.ToggleFullscreen)]
    [InlineData(KeyInput.Escape, false, false, PlayerAction.ExitFullscreen)]
    [InlineData(KeyInput.A, false, false, PlayerAction.CycleAudioTrack)]
    [InlineData(KeyInput.S, false, false, PlayerAction.CycleSubtitleTrack)]
    [InlineData(KeyInput.PageDown, false, false, PlayerAction.NextEpisode)]
    [InlineData(KeyInput.PageUp, false, false, PlayerAction.PreviousEpisode)]
    public void KeyboardCommandRouter_RoutesShortcutsCorrectly(KeyInput key, bool ctrl, bool alt, PlayerAction expected)
    {
        var action = KeyboardCommandRouter.Route(key, ctrl, alt);
        Assert.Equal(expected, action);
    }

    [Fact]
    public void FormatHelper_Timecodes_FormattedAccurately()
    {
        Assert.Equal("00:00", FormatHelper.FormatTimecode(0));
        Assert.Equal("00:45", FormatHelper.FormatTimecode(45));
        Assert.Equal("01:15", FormatHelper.FormatTimecode(75));
        Assert.Equal("01:01:05", FormatHelper.FormatTimecode(3665));
        Assert.Equal("00:00", FormatHelper.FormatTimecode(-10));
        Assert.Equal("00:00", FormatHelper.FormatTimecode(double.NaN));
    }

    [Fact]
    public void FormatHelper_AudioAndSubtitleBadges_FormattedAccurately()
    {
        var audioExternal = new AudioTrack
        {
            Id = 2,
            Title = "AniLibria",
            Language = "ru",
            Channels = 6,
            Codec = "flac",
            Origin = TrackOrigin.External
        };

        var audioLabel = FormatHelper.FormatAudioTrackLabel(audioExternal);
        Assert.Contains("🇷🇺 Russian", audioLabel);
        Assert.Contains("5.1", audioLabel);
        Assert.Contains("External", audioLabel);
        Assert.Contains("FLAC", audioLabel);

        var subExternal = new SubtitleTrack
        {
            Id = 1,
            Title = "Full Subs",
            Language = "ru",
            Format = SubtitleFormat.ASS,
            Origin = TrackOrigin.External
        };

        var subLabel = FormatHelper.FormatSubtitleTrackLabel(subExternal);
        Assert.Contains("🇷🇺 Russian", subLabel);
        Assert.Contains("ASS", subLabel);
        Assert.Contains("External", subLabel);
    }

    [Fact]
    public void TrackSelectorViewModel_UpdatesAndNotifiesSelections()
    {
        var vm = new TrackSelectorViewModel();
        int selectedAudioId = -1;
        int selectedSubId = -1;
        bool? subVisibility = null;

        vm.AudioTrackSelected += id => selectedAudioId = id;
        vm.SubtitleTrackSelected += id => selectedSubId = id;
        vm.SubtitleVisibilityChanged += v => subVisibility = v;

        var tracks = new List<MediaTrack>
        {
            new AudioTrack { Id = 1, Language = "ja", Channels = 2, Origin = TrackOrigin.Embedded, IsSelected = true },
            new AudioTrack { Id = 2, Language = "ru", Channels = 6, Origin = TrackOrigin.External, IsSelected = false },
            new SubtitleTrack { Id = 1, Language = "ru", Format = SubtitleFormat.ASS, Origin = TrackOrigin.External, IsSelected = true }
        };

        vm.UpdateTracks(tracks);

        Assert.Equal(2, vm.AudioTracks.Count);
        Assert.Single(vm.SubtitleTracks);
        Assert.True(vm.IsSubtitlesEnabled);

        // Select external audio
        vm.SelectAudio(2);
        Assert.Equal(2, selectedAudioId);
        Assert.True(vm.AudioTracks.First(a => a.Id == 2).IsSelected);

        // Turn off subtitles
        vm.DisableSubtitles();
        Assert.False(vm.IsSubtitlesEnabled);
        Assert.False(subVisibility);

        // Re-enable subtitle
        vm.SelectSubtitle(1);
        Assert.Equal(1, selectedSubId);
        Assert.True(vm.IsSubtitlesEnabled);
        Assert.True(subVisibility);
    }

    [Fact]
    public void PlayerViewModel_UpdatesTelemetryAndEpisodeIdentity()
    {
        var vm = new PlayerViewModel();
        vm.UpdateTime(125.5, 300.0);
        Assert.Equal(125.5, vm.CurrentPosition);
        Assert.Equal(300.0, vm.Duration);
        Assert.Equal("02:05 / 05:00", vm.FormattedTimecode);

        var package = new MediaPackage
        {
            PrimaryVideo = MediaItem.FromFilePath(@"C:\Shows\Attack on Titan\S01E03.mkv"),
            Episode = new EpisodeInfo
            {
                ShowTitle = "Attack on Titan",
                SeasonNumber = 1,
                EpisodeNumber = 3
            },
            AudioTracks = [new AudioTrack { Id = 1, Language = "ru" }],
            SubtitleTracks = [new SubtitleTrack { Id = 1, Language = "ru" }]
        };

        vm.UpdateMediaPackage(package);
        Assert.True(vm.HasMedia);
        Assert.Equal("Attack on Titan S01E03", vm.EpisodeTitle);
        Assert.Contains("1 video", vm.PackageSummary);
    }

    [Fact]
    public async Task UiPlaybackWorkflow_EndToEndMediaSessionVerification()
    {
        var testDataDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\TestData\Anime"));
        var videoPath = Path.Combine(testDataDir, "S01E01.mkv");
        Assert.True(File.Exists(videoPath), $"Test video file not found at {videoPath}");

        // 1. Discover MediaPackage
        var package = DirectoryScanner.Scan(videoPath);
        Assert.NotNull(package);
        Assert.Single(package.AudioTracks);
        Assert.Single(package.SubtitleTracks);
        Assert.NotNull(package.Fonts);
        Assert.True(package.Fonts.HasFonts);

        // 2. Initialize Playback Engine in headless mode
        var engine = new MpvPlaybackEngine();
        await engine.InitializeAsync(windowHandle: 0);

        try
        {
            // 3. Open Package
            await engine.OpenAsync(package);

            // 4. Verify Active Tracks
            var tracks = engine.ActiveTracks;
            Assert.NotEmpty(tracks);

            var extAudio = tracks.OfType<AudioTrack>().FirstOrDefault(a => a.Origin == TrackOrigin.External);
            var extSub = tracks.OfType<SubtitleTrack>().FirstOrDefault(s => s.Origin == TrackOrigin.External);

            Assert.NotNull(extAudio);
            Assert.NotNull(extSub);

            // 5. Wire TrackSelectorViewModel
            var trackVm = new TrackSelectorViewModel();
            trackVm.UpdateTracks(tracks);

            trackVm.AudioTrackSelected += async id => await engine.SelectAudioTrackAsync(id);
            trackVm.SubtitleTrackSelected += async id => await engine.SelectSubtitleTrackAsync(id);
            trackVm.SubtitleVisibilityChanged += async v => await engine.SetSubtitleVisibilityAsync(v);

            // Switch to external Russian audio
            trackVm.SelectAudio(extAudio.Id);
            var activeAid = await engine.GetPropertyAsync("aid");
            Assert.Equal(extAudio.Id.ToString(), activeAid);

            // Switch to external Russian subtitle
            trackVm.SelectSubtitle(extSub.Id);
            var activeSid = await engine.GetPropertyAsync("sid");
            Assert.Equal(extSub.Id.ToString(), activeSid);

            // 6. Test Transport Controls (Play, Pause, Seek, Volume)
            await engine.PlayAsync();
            await Task.Delay(50);

            await engine.PauseAsync();
            var paused = await engine.GetPropertyAsync("pause");
            Assert.Equal("yes", paused);

            await engine.SeekAsync(0.5, relative: false);
            var pos = await engine.GetPropertyAsync("playback-time");
            Assert.NotNull(pos);

            await engine.SetVolumeAsync(85);
            var vol = await engine.GetPropertyAsync("volume");
            Assert.Equal("85.000000", vol);

            // 7. Test Fullscreen Toggle
            await engine.SetFullscreenAsync(true);
            var fs = await engine.GetPropertyAsync("fullscreen");
            Assert.Equal("yes", fs);

            await engine.SetFullscreenAsync(false);
            fs = await engine.GetPropertyAsync("fullscreen");
            Assert.Equal("no", fs);
        }
        finally
        {
            await engine.DisposeAsync();
        }
    }

    [Fact]
    public void FormatHelper_PreferredTrackBadges_FormattedCorrectly()
    {
        var audio = new AudioTrack
        {
            Id = 1,
            Language = "ru",
            Title = "AniLibria",
            Channels = 6,
            Codec = "flac",
            Origin = TrackOrigin.External
        };

        var normalLabel = FormatHelper.FormatAudioTrackLabel(audio, isPreferred: false);
        var prefLabel = FormatHelper.FormatAudioTrackLabel(audio, isPreferred: true);

        Assert.DoesNotContain("(Preferred)", normalLabel);
        Assert.Contains("(Preferred)", prefLabel);
        Assert.Contains("🇷🇺 Russian", prefLabel);
        Assert.Contains("5.1", prefLabel);

        var sub = new SubtitleTrack
        {
            Id = 1,
            Language = "ru",
            Format = SubtitleFormat.ASS,
            Origin = TrackOrigin.External
        };

        var normalSubLabel = FormatHelper.FormatSubtitleTrackLabel(sub, isPreferred: false);
        var prefSubLabel = FormatHelper.FormatSubtitleTrackLabel(sub, isPreferred: true);

        Assert.DoesNotContain("(Preferred)", normalSubLabel);
        Assert.Contains("(Preferred)", prefSubLabel);
        Assert.Contains("🇷🇺 Russian", prefSubLabel);
        Assert.Contains("ASS", prefSubLabel);
    }

    [Fact]
    public void PlayerViewModel_ResumePrompt_LifecycleState()
    {
        var vm = new PlayerViewModel();
        Assert.False(vm.IsResumePromptVisible);
        Assert.Equal(0, vm.ResumePositionSeconds);
        Assert.Empty(vm.ResumePromptMessage);

        vm.ShowResumePrompt("Continue watching from 23:14?", 1394.0);
        Assert.True(vm.IsResumePromptVisible);
        Assert.Equal(1394.0, vm.ResumePositionSeconds);
        Assert.Equal("Continue watching from 23:14?", vm.ResumePromptMessage);

        vm.HideResumePrompt();
        Assert.False(vm.IsResumePromptVisible);
        Assert.Equal(0, vm.ResumePositionSeconds);
        Assert.Empty(vm.ResumePromptMessage);
    }

    [Fact]
    public void PlayerViewModel_AutoNextPrompt_LifecycleState()
    {
        var vm = new PlayerViewModel();
        Assert.False(vm.IsAutoNextPromptVisible);
        Assert.True(vm.AutoNextEnabled);
        Assert.Equal(5, vm.AutoNextCountdownSeconds);

        vm.ShowAutoNextPrompt("Next Episode: Attack on Titan S01E04", 5);
        Assert.True(vm.IsAutoNextPromptVisible);
        Assert.Equal("Next Episode: Attack on Titan S01E04", vm.AutoNextMessage);
        Assert.Equal(5, vm.AutoNextCountdownSeconds);

        vm.AutoNextCountdownSeconds = 3;
        Assert.Equal(3, vm.AutoNextCountdownSeconds);

        vm.HideAutoNextPrompt();
        Assert.False(vm.IsAutoNextPromptVisible);
        Assert.Empty(vm.AutoNextMessage);
    }

    [Fact]
    public void PlayerViewModel_ContinueWatching_LifecycleState()
    {
        var vm = new PlayerViewModel();
        Assert.False(vm.HasContinueWatching);
        Assert.Empty(vm.ContinueWatchingTitle);
        Assert.Empty(vm.ContinueWatchingDetails);
        Assert.Empty(vm.ContinueWatchingFilePath);
        Assert.Equal(0, vm.ContinueWatchingPosition);

        vm.SetContinueWatching(
            "Attack on Titan S01E03",
            "Paused at 15:30 / 24:00",
            @"C:\Shows\Attack on Titan\S01E03.mkv",
            930.0);

        Assert.True(vm.HasContinueWatching);
        Assert.Equal("Attack on Titan S01E03", vm.ContinueWatchingTitle);
        Assert.Equal("Paused at 15:30 / 24:00", vm.ContinueWatchingDetails);
        Assert.Equal(@"C:\Shows\Attack on Titan\S01E03.mkv", vm.ContinueWatchingFilePath);
        Assert.Equal(930.0, vm.ContinueWatchingPosition);

        vm.ClearContinueWatching();
        Assert.False(vm.HasContinueWatching);
        Assert.Empty(vm.ContinueWatchingTitle);
        Assert.Empty(vm.ContinueWatchingDetails);
        Assert.Empty(vm.ContinueWatchingFilePath);
        Assert.Equal(0, vm.ContinueWatchingPosition);
    }

    [Fact]
    public void EpisodeNavigator_IntegrationWithMediaPackage_FindsNextAndPrevious()
    {
        var ep1 = MediaItem.FromFilePath(@"C:\Shows\Attack on Titan\S01E01.mkv");
        var ep2 = MediaItem.FromFilePath(@"C:\Shows\Attack on Titan\S01E02.mkv");
        var ep3 = MediaItem.FromFilePath(@"C:\Shows\Attack on Titan\S01E03.mkv");

        var package2 = new MediaPackage
        {
            PrimaryVideo = ep2,
            Episode = new EpisodeInfo
            {
                ShowTitle = "Attack on Titan",
                SeasonNumber = 1,
                EpisodeNumber = 2
            },
            SiblingEpisodes = [ep1, ep3]
        };

        var prev = EpisodeNavigator.FindPreviousEpisode(package2);
        var next = EpisodeNavigator.FindNextEpisode(package2);

        Assert.NotNull(prev);
        Assert.Equal(ep1.FilePath, prev.FilePath);
        Assert.NotNull(next);
        Assert.Equal(ep3.FilePath, next.FilePath);
    }
}
