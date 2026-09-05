namespace UniversalMediaPlayer.Core.Models;

public record PlaybackPreparationPlan
{
    public required string ShowId { get; init; }
    public ShowPreferences? Preferences { get; init; }
    public TrackResolutionResult<AudioTrack>? AudioResolution { get; init; }
    public TrackResolutionResult<SubtitleTrack>? SubtitleResolution { get; init; }
    public bool SubtitleVisible { get; init; }
    public WatchHistoryItem? ResumeHistory { get; init; }
    public bool CanResume => ResumeHistory != null && 
                             !ResumeHistory.Completed && 
                             ResumeHistory.PositionSeconds > 15 && 
                             (ResumeHistory.DurationSeconds <= 0 || (ResumeHistory.DurationSeconds - ResumeHistory.PositionSeconds) > 15);
    public double ResumePositionSeconds => ResumeHistory?.PositionSeconds ?? 0;
}
