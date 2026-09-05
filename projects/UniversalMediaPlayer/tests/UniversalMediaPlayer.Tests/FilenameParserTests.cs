using UniversalMediaPlayer.Discovery;
using Xunit;

namespace UniversalMediaPlayer.Tests;

public class FilenameParserTests
{
    [Theory]
    [InlineData("[SubsPlease] Sousou no Frieren - 03 (1080p) [9A1B2C3D].mkv", "Sousou no Frieren - 03")]
    [InlineData("Attack.on.Titan.S01E01.1080p.BluRay.x265.HEVC.10bit.DDP5.1.mkv", "Attack on Titan S01E01")]
    [InlineData("[Erai-raws] Jujutsu Kaisen - 12 [1080p][Multiple Subtitle].mkv", "Jujutsu Kaisen - 12 [Multiple Subtitle]")]
    [InlineData("Movie.2024.2160p.UHD.Remux.AV1.TrueHD.Atmos.mkv", "Movie 2024")]
    [InlineData("Legacy.Show.S02E05.DVDRip.xvid.avi", "Legacy Show S02E05")]
    public void NormalizeTitle_StripsReleaseTagsConservatively(string input, string expectedSubstring)
    {
        var normalized = FilenameParser.NormalizeTitle(input);
        Assert.Contains(expectedSubstring, normalized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExtractReleaseGroup_ExtractsSquareBracketPrefix()
    {
        var group = FilenameParser.ExtractReleaseGroup("[SubsPlease] Show - 01.mkv");
        Assert.Equal("SubsPlease", group);
    }
}
