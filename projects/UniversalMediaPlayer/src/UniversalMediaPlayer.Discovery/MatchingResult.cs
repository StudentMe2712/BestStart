namespace UniversalMediaPlayer.Discovery;

public enum MatchConfidence
{
    Rejected,
    Possible,
    Likely,
    HighConfidence
}

public record MatchingResult
{
    public required string CandidateFilePath { get; init; }
    public required string CandidateFileName { get; init; }
    public int Score { get; init; }
    public MatchConfidence Confidence { get; init; }
    public IReadOnlyList<string> MatchedFactors { get; init; } = [];
    public IReadOnlyList<string> RejectedFactors { get; init; } = [];

    public bool IsAccepted => Confidence is MatchConfidence.HighConfidence or MatchConfidence.Likely;

    public string Explanation =>
        $"{CandidateFileName} -> Score {Score} ({Confidence})\n" +
        $"  Matched:  {string.Join(", ", MatchedFactors)}\n" +
        (RejectedFactors.Count > 0 ? $"  Rejected: {string.Join(", ", RejectedFactors)}" : "");
}
