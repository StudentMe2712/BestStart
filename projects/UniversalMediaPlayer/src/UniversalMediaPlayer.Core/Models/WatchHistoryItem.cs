namespace UniversalMediaPlayer.Core.Models;

public record WatchHistoryItem
{
    public long Id { get; init; }
    public required string ShowId { get; init; }
    public int? SeasonNumber { get; init; }
    public int? EpisodeNumber { get; init; }
    public required string FilePath { get; init; }
    public double PositionSeconds { get; init; }
    public double DurationSeconds { get; init; }
    public DateTime LastPlayedUtc { get; init; } = DateTime.UtcNow;
    public bool Completed { get; init; }
}
