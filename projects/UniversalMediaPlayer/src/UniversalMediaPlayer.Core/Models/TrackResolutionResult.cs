namespace UniversalMediaPlayer.Core.Models;

public enum TrackSelectionReason
{
    None,
    ExactTrackMatch,
    PreferredLanguage,
    BackendDefault,
    FallbackFirstAvailable,
    ExplicitlyDisabled
}

public enum MatchConfidence
{
    None,
    Low,
    Medium,
    High
}

public record TrackResolutionResult<TTrack> where TTrack : MediaTrack
{
    public TTrack? SelectedTrack { get; init; }
    public TrackSelectionReason Reason { get; init; } = TrackSelectionReason.None;
    public MatchConfidence Confidence { get; init; } = MatchConfidence.None;
    public string Explanation { get; init; } = string.Empty;
    public bool HasSelection => SelectedTrack != null;
}
