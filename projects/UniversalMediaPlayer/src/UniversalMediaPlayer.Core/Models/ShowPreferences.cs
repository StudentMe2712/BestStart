namespace UniversalMediaPlayer.Core.Models;

public record ShowPreferences
{
    public required string ShowId { get; init; }
    public string? PreferredAudioLanguage { get; init; }
    public string? PreferredSubtitleLanguage { get; init; }
    public TrackPreference? PreferredAudioTrack { get; init; }
    public TrackPreference? PreferredSubtitleTrack { get; init; }
    public bool AutoNextEpisode { get; init; } = true;
    public bool SubtitleEnabled { get; init; } = true;
}
