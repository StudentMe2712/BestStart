using UniversalMediaPlayer.Core.Models;
using UniversalMediaPlayer.Discovery;
using Xunit;
using MatchConfidence = UniversalMediaPlayer.Discovery.MatchConfidence;

namespace UniversalMediaPlayer.Tests;

public class MatchEngineTests
{
    [Fact]
    public void Evaluate_SameEpisodeAndSeason_ReturnsHighConfidenceMatch()
    {
        var video = MediaItem.FromFilePath(@"C:\Anime\Show.S01E01.mkv");
        var ep = EpisodeParser.Parse(video.FileName);

        var result = MatchEngine.Evaluate(video, ep, @"C:\Anime\Show.S01E01.RU.mka", isSameDirectory: true);

        Assert.True(result.IsAccepted);
        Assert.True(result.Score >= 80, $"Expected score >= 80, got {result.Score}");
        Assert.Contains(result.MatchedFactors, f => f.Contains("Same Episode"));
        Assert.Contains(result.MatchedFactors, f => f.Contains("Same Season"));
    }

    [Fact]
    public void Evaluate_EpisodeMismatch_HardRejectsWithZeroScore()
    {
        var video = MediaItem.FromFilePath(@"C:\Anime\Show.S01E01.mkv");
        var ep = EpisodeParser.Parse(video.FileName);

        // Subtitle is for Episode 2!
        var result = MatchEngine.Evaluate(video, ep, @"C:\Anime\Show.S01E02.RU.ass", isSameDirectory: true);

        Assert.False(result.IsAccepted);
        Assert.Equal(0, result.Score);
        Assert.Equal(MatchConfidence.Rejected, result.Confidence);
        Assert.Contains(result.RejectedFactors, f => f.Contains("Episode mismatch"));
    }

    [Fact]
    public void Evaluate_SeasonMismatch_HardRejectsWithZeroScore()
    {
        var video = MediaItem.FromFilePath(@"C:\Anime\Show.S01E01.mkv");
        var ep = EpisodeParser.Parse(video.FileName);

        // Audio is for Season 2 Episode 1!
        var result = MatchEngine.Evaluate(video, ep, @"C:\Anime\Show.S02E01.RU.mka", isSameDirectory: true);

        Assert.False(result.IsAccepted);
        Assert.Equal(0, result.Score);
        Assert.Equal(MatchConfidence.Rejected, result.Confidence);
        Assert.Contains(result.RejectedFactors, f => f.Contains("Season mismatch"));
    }
}
