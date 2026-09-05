namespace UniversalMediaPlayer.Tests;

using System;
using System.Collections.Generic;
using UniversalMediaPlayer.Core.Enums;
using UniversalMediaPlayer.Core.Models;
using UniversalMediaPlayer.Discovery;
using Xunit;
using CoreConfidence = UniversalMediaPlayer.Core.Models.MatchConfidence;

public class PreferredTrackResolverTests
{
    [Fact]
    public void ResolveAudioTrack_ExactTrackMatchWithReleaseGroupOrTitle_SelectsCorrectTrack()
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

        // 1. Preference for AniLibria
        var prefsAniLibria = new ShowPreferences
        {
            ShowId = "frieren",
            PreferredAudioTrack = new TrackPreference
            {
                Language = "ru",
                Title = "AniLibria"
            }
        };

        var resultAniLibria = PreferredTrackResolver.ResolveAudioTrack(prefsAniLibria, tracks);

        Assert.True(resultAniLibria.HasSelection);
        Assert.NotNull(resultAniLibria.SelectedTrack);
        Assert.Equal(1, resultAniLibria.SelectedTrack.Id);
        Assert.Equal(TrackSelectionReason.ExactTrackMatch, resultAniLibria.Reason);
        Assert.Equal(CoreConfidence.High, resultAniLibria.Confidence);
        Assert.Contains("AniLibria", resultAniLibria.Explanation);

        // 2. Preference for JAM
        var prefsJam = new ShowPreferences
        {
            ShowId = "frieren",
            PreferredAudioTrack = new TrackPreference
            {
                Language = "ru",
                Title = "JAM"
            }
        };

        var resultJam = PreferredTrackResolver.ResolveAudioTrack(prefsJam, tracks);

        Assert.True(resultJam.HasSelection);
        Assert.NotNull(resultJam.SelectedTrack);
        Assert.Equal(2, resultJam.SelectedTrack.Id);
        Assert.Equal(TrackSelectionReason.ExactTrackMatch, resultJam.Reason);
        Assert.Equal(CoreConfidence.High, resultJam.Confidence);
        Assert.Contains("JAM", resultJam.Explanation);
    }

    [Fact]
    public void ResolveAudioTrack_LanguageOnlyMatch_SelectsTrackWithMatchingLanguage()
    {
        var trackEng = new AudioTrack
        {
            Id = 1,
            Title = "English Dub",
            Language = "en",
            Origin = TrackOrigin.Embedded
        };

        var trackRus = new AudioTrack
        {
            Id = 2,
            Title = "Russian Dub",
            Language = "ru",
            Origin = TrackOrigin.Embedded
        };

        var trackJap = new AudioTrack
        {
            Id = 3,
            Title = "Japanese Original",
            Language = "ja",
            Origin = TrackOrigin.Embedded
        };

        var prefs = new ShowPreferences
        {
            ShowId = "frieren",
            PreferredAudioLanguage = "ru"
        };

        var result = PreferredTrackResolver.ResolveAudioTrack(prefs, [trackEng, trackRus, trackJap]);

        Assert.True(result.HasSelection);
        Assert.NotNull(result.SelectedTrack);
        Assert.Equal(2, result.SelectedTrack.Id);
        Assert.Equal("ru", result.SelectedTrack.Language);
        Assert.Equal(TrackSelectionReason.PreferredLanguage, result.Reason);
        Assert.Equal(CoreConfidence.Medium, result.Confidence);
    }

    [Fact]
    public void ResolveAudioTrack_FallbackWhenPreferredLanguageUnavailable_FallsBackWithExplicitMessage()
    {
        var trackJap = new AudioTrack
        {
            Id = 1,
            Title = "Japanese Original",
            Language = "ja",
            Origin = TrackOrigin.Embedded,
            IsSelected = false
        };

        var trackEng = new AudioTrack
        {
            Id = 2,
            Title = "English Dub",
            Language = "en",
            Origin = TrackOrigin.Embedded,
            IsSelected = false
        };

        var prefs = new ShowPreferences
        {
            ShowId = "frieren",
            PreferredAudioLanguage = "ru"
        };

        var result = PreferredTrackResolver.ResolveAudioTrack(prefs, [trackJap, trackEng]);

        Assert.True(result.HasSelection);
        Assert.NotNull(result.SelectedTrack);
        Assert.Equal(1, result.SelectedTrack.Id);
        Assert.Equal(TrackSelectionReason.FallbackFirstAvailable, result.Reason);
        Assert.Equal(CoreConfidence.Low, result.Confidence);
        Assert.Equal("Preferred Russian audio unavailable. Fallback: Japanese (Embedded)", result.Explanation);
    }

    [Fact]
    public void ResolveSubtitleTrack_FallbackWhenPreferredLanguageUnavailable_ReturnsNoSelectionAndExplanation()
    {
        var trackEng = new SubtitleTrack
        {
            Id = 1,
            Title = "English",
            Language = "en",
            Format = SubtitleFormat.SRT,
            Origin = TrackOrigin.Embedded
        };

        var prefs = new ShowPreferences
        {
            ShowId = "frieren",
            PreferredSubtitleLanguage = "ru"
        };

        var result = PreferredTrackResolver.ResolveSubtitleTrack(prefs, [trackEng]);

        Assert.False(result.HasSelection);
        Assert.Null(result.SelectedTrack);
        Assert.Equal(TrackSelectionReason.None, result.Reason);
        Assert.Equal(CoreConfidence.None, result.Confidence);
        Assert.Equal("Preferred Russian subtitle unavailable.", result.Explanation);
    }

    [Fact]
    public void ResolveSubtitleTrack_SubtitlesExplicitlyDisabled_ReturnsExplicitlyDisabledReason()
    {
        var trackRus = new SubtitleTrack
        {
            Id = 1,
            Title = "Russian Full",
            Language = "ru",
            Format = SubtitleFormat.ASS,
            Origin = TrackOrigin.External
        };

        var trackEng = new SubtitleTrack
        {
            Id = 2,
            Title = "English",
            Language = "en",
            Format = SubtitleFormat.SRT,
            Origin = TrackOrigin.Embedded
        };

        var prefs = new ShowPreferences
        {
            ShowId = "frieren",
            SubtitleEnabled = false,
            PreferredSubtitleLanguage = "ru"
        };

        var result = PreferredTrackResolver.ResolveSubtitleTrack(prefs, [trackRus, trackEng]);

        Assert.False(result.HasSelection);
        Assert.Null(result.SelectedTrack);
        Assert.Equal(TrackSelectionReason.ExplicitlyDisabled, result.Reason);
        Assert.Equal(CoreConfidence.High, result.Confidence);
        Assert.Contains("disabled", result.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveSubtitleTrack_SubtitleFormatPreference_PrefersAssOverSrt()
    {
        var trackSrt = new SubtitleTrack
        {
            Id = 1,
            Title = "Russian Basic",
            Language = "ru",
            Format = SubtitleFormat.SRT,
            Origin = TrackOrigin.Embedded
        };

        var trackAss = new SubtitleTrack
        {
            Id = 2,
            Title = "Russian Styled",
            Language = "ru",
            Format = SubtitleFormat.ASS,
            Origin = TrackOrigin.External
        };

        var prefs = new ShowPreferences
        {
            ShowId = "frieren",
            PreferredSubtitleLanguage = "ru"
        };

        var result = PreferredTrackResolver.ResolveSubtitleTrack(prefs, [trackSrt, trackAss]);

        Assert.True(result.HasSelection);
        Assert.NotNull(result.SelectedTrack);
        Assert.Equal(2, result.SelectedTrack.Id);
        Assert.Equal(SubtitleFormat.ASS, result.SelectedTrack.Format);
        Assert.Equal(TrackSelectionReason.PreferredLanguage, result.Reason);
        Assert.Equal(CoreConfidence.Medium, result.Confidence);
    }

    [Fact]
    public void ResolveTracks_EmptyTrackList_ReturnsNullSelectionWithNoneReason()
    {
        var audioResult = PreferredTrackResolver.ResolveAudioTrack(null, []);
        Assert.False(audioResult.HasSelection);
        Assert.Null(audioResult.SelectedTrack);
        Assert.Equal(TrackSelectionReason.None, audioResult.Reason);
        Assert.Equal(CoreConfidence.None, audioResult.Confidence);

        var subResult = PreferredTrackResolver.ResolveSubtitleTrack(null, []);
        Assert.False(subResult.HasSelection);
        Assert.Null(subResult.SelectedTrack);
        Assert.Equal(TrackSelectionReason.None, subResult.Reason);
        Assert.Equal(CoreConfidence.None, subResult.Confidence);
    }

    [Fact]
    public void ShowIdentityResolver_NormalizesIdentityAcrossDrivesAndFormats()
    {
        var show1 = ShowIdentityResolver.ResolveShowId(@"D:\Anime\Show\");
        var show2 = ShowIdentityResolver.ResolveShowId(@"E:\Media\Show\");
        Assert.Equal("show", show1);
        Assert.Equal("show", show2);
        Assert.Equal(show1, show2);

        var aot1 = ShowIdentityResolver.ResolveShowId(@"D:\Anime\Attack on Titan\");
        var aot2 = ShowIdentityResolver.ResolveShowId(@"E:\Media\Attack on Titan\");
        Assert.Equal("attack on titan", aot1);
        Assert.Equal("attack on titan", aot2);

        var aotFile = ShowIdentityResolver.ResolveShowId(@"D:\Anime\Attack_on_Titan\Season 1\S01E01.mkv");
        Assert.Equal("attack on titan", aotFile);
    }
}
