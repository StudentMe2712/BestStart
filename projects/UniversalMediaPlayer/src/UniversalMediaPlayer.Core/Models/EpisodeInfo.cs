namespace UniversalMediaPlayer.Core.Models;

public record EpisodeInfo
{
    public required string ShowTitle { get; init; }
    public int? SeasonNumber { get; init; }
    public required int EpisodeNumber { get; init; }
    public string RawToken { get; init; } = string.Empty;

    public string FormattedEpisode => SeasonNumber.HasValue
        ? $"S{SeasonNumber.Value:D2}E{EpisodeNumber:D2}"
        : $"E{EpisodeNumber:D2}";

    public override string ToString() => $"{ShowTitle} - {FormattedEpisode}";
}
