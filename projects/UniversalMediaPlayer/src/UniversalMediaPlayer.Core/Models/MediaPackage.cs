namespace UniversalMediaPlayer.Core.Models;

public record MediaPackage
{
    public required MediaItem PrimaryVideo { get; init; }
    public EpisodeInfo? Episode { get; init; }
    public IReadOnlyList<AudioTrack> AudioTracks { get; init; } = [];
    public IReadOnlyList<SubtitleTrack> SubtitleTracks { get; init; } = [];
    public FontPackage? Fonts { get; init; }
    public IReadOnlyList<MediaItem> SiblingEpisodes { get; init; } = [];

    public bool HasExternalAudio => AudioTracks.Any(t => t.IsExternal);
    public bool HasExternalSubtitles => SubtitleTracks.Any(t => t.IsExternal);
    public bool HasFonts => Fonts is { HasFonts: true };
}
