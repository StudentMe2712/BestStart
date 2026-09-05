namespace UniversalMediaPlayer.Core.Models;

using UniversalMediaPlayer.Core.Enums;

public record TrackPreference
{
    public string Language { get; init; } = "und";
    public string? Title { get; init; }
    public string? Codec { get; init; }
    public int? Channels { get; init; }
    public TrackOrigin? Origin { get; init; }
    public SubtitleFormat? Format { get; init; }
}
