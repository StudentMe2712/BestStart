using UniversalMediaPlayer.Core.Enums;

namespace UniversalMediaPlayer.Core.Models;

public abstract record MediaTrack
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Language { get; init; } = "und";
    public TrackOrigin Origin { get; init; } = TrackOrigin.Embedded;
    public string? ExternalFilePath { get; init; }
    public string Codec { get; init; } = string.Empty;
    public bool IsSelected { get; init; }

    public bool IsExternal => Origin == TrackOrigin.External;
}
