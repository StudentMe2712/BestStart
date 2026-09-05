using UniversalMediaPlayer.Discovery;
using Xunit;

namespace UniversalMediaPlayer.Tests;

public class EpisodeParserTests
{
    [Theory]
    [InlineData("Show.S01E03.1080p.mkv", 1, 3)]
    [InlineData("Show.s1e3.720p.mkv", 1, 3)]
    [InlineData("Show_S02_E15.mkv", 2, 15)]
    [InlineData("Show - 1x04.avi", 1, 4)]
    [InlineData("Show.Episode.08.mkv", null, 8)]
    [InlineData("Show.Ep09.mkv", null, 9)]
    [InlineData("Show.E10.mkv", null, 10)]
    [InlineData("[SubsPlease] Frieren - 03 (1080p).mkv", 1, 3)]
    [InlineData("Show - 24v2.mkv", 1, 24)]
    public void Parse_CorrectlyIdentifiesSeasonAndEpisode(string fileName, int? expectedSeason, int expectedEpisode)
    {
        var result = EpisodeParser.Parse(fileName);
        Assert.NotNull(result);
        Assert.Equal(expectedSeason, result.SeasonNumber);
        Assert.Equal(expectedEpisode, result.EpisodeNumber);
    }

    [Fact]
    public void Parse_ReturnsNull_ForFilesWithoutEpisodeNumbers()
    {
        var result = EpisodeParser.Parse("The.Lord.of.the.Rings.The.Fellowship.of.the.Ring.Extended.2001.mkv");
        Assert.Null(result);
    }
}
