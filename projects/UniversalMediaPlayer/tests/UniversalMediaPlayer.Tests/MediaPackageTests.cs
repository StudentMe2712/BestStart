using UniversalMediaPlayer.Core.Enums;
using UniversalMediaPlayer.Core.Models;
using Xunit;

namespace UniversalMediaPlayer.Tests;

public class MediaPackageTests
{
    [Fact]
    public void MediaPackage_CorrectlyIdentifiesExternalTracksAndFonts()
    {
        var pkg = new MediaPackage
        {
            PrimaryVideo = MediaItem.FromFilePath(@"C:\Mediaideo.mkv"),
            AudioTracks = new List<AudioTrack>
            {
                new() { Id = 1, Title = "Embedded", Origin = TrackOrigin.Embedded },
                new() { Id = 2, Title = "External", Origin = TrackOrigin.External, ExternalFilePath = @"C:\Mediaudio.mka" }
            },
            SubtitleTracks = new List<SubtitleTrack>
            {
                new() { Id = 1, Title = "External ASS", Origin = TrackOrigin.External, Format = SubtitleFormat.ASS }
            },
            Fonts = new FontPackage(@"C:\Mediaonts", new[] { "font1.ttf", "font2.otf" })
        };

        Assert.True(pkg.HasExternalAudio);
        Assert.True(pkg.HasExternalSubtitles);
        Assert.True(pkg.HasFonts);
        Assert.Equal(2, pkg.Fonts!.Count);
        Assert.True(pkg.SubtitleTracks[0].RequiresFonts);
    }
}
