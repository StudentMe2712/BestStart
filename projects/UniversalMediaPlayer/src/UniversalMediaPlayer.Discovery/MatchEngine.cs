using UniversalMediaPlayer.Core.Models;

namespace UniversalMediaPlayer.Discovery;

public static class MatchEngine
{
    public static MatchingResult Evaluate(
        MediaItem video,
        EpisodeInfo? videoEpisode,
        string candidateFilePath,
        bool isSameDirectory)
    {
        var candidateName = Path.GetFileName(candidateFilePath);
        var candidateEpisode = EpisodeParser.Parse(candidateName);
        var matchedFactors = new List<string>();
        var rejectedFactors = new List<string>();
        int score = 0;

        // 1. Episode Gating (Crucial Rule: hard reject on mismatch)
        if (videoEpisode != null && candidateEpisode != null)
        {
            if (videoEpisode.EpisodeNumber == candidateEpisode.EpisodeNumber)
            {
                score += 40;
                matchedFactors.Add($"Same Episode (E{videoEpisode.EpisodeNumber:D2})");
            }
            else
            {
                rejectedFactors.Add($"Episode mismatch (Video: E{videoEpisode.EpisodeNumber}, Candidate: E{candidateEpisode.EpisodeNumber})");
                return new MatchingResult
                {
                    CandidateFilePath = candidateFilePath,
                    CandidateFileName = candidateName,
                    Score = 0,
                    Confidence = MatchConfidence.Rejected,
                    MatchedFactors = matchedFactors,
                    RejectedFactors = rejectedFactors
                };
            }

            // Season Gating
            if (videoEpisode.SeasonNumber.HasValue && candidateEpisode.SeasonNumber.HasValue)
            {
                if (videoEpisode.SeasonNumber.Value == candidateEpisode.SeasonNumber.Value)
                {
                    score += 15;
                    matchedFactors.Add($"Same Season (S{videoEpisode.SeasonNumber.Value:D2})");
                }
                else
                {
                    rejectedFactors.Add($"Season mismatch (Video: S{videoEpisode.SeasonNumber}, Candidate: S{candidateEpisode.SeasonNumber})");
                    return new MatchingResult
                    {
                        CandidateFilePath = candidateFilePath,
                        CandidateFileName = candidateName,
                        Score = 0,
                        Confidence = MatchConfidence.Rejected,
                        MatchedFactors = matchedFactors,
                        RejectedFactors = rejectedFactors
                    };
                }
            }
        }
        else if (videoEpisode != null && candidateEpisode == null)
        {
            // Candidate has no episode number but video does
            score -= 20;
            rejectedFactors.Add("Candidate lacks episode identifier");
        }

        // 2. Directory proximity
        if (isSameDirectory)
        {
            score += 10;
            matchedFactors.Add("Same directory");
        }
        else
        {
            score += 8;
            matchedFactors.Add("Sibling media subfolder");
        }

        // 3. Language identification
        var lang = LanguageDetector.DetectLanguage(candidateName);
        if (lang != "und")
        {
            score += 5;
            matchedFactors.Add($"Identified language '{lang}'");
        }

        // 4. Title similarity
        var normVideo = FilenameParser.NormalizeTitle(video.FileName);
        var normCandidate = FilenameParser.NormalizeTitle(candidateName);
        var similarity = ComputeStringSimilarity(normVideo, normCandidate);
        var simScore = (int)(similarity * 30);
        score += simScore;
        matchedFactors.Add($"Title similarity {(int)(similarity * 100)}%");

        // Clamp score 0..100
        score = Math.Clamp(score, 0, 100);

        var confidence = score switch
        {
            >= 95 => MatchConfidence.HighConfidence,
            >= 80 => MatchConfidence.Likely,
            >= 50 => MatchConfidence.Possible,
            _ => MatchConfidence.Rejected
        };

        return new MatchingResult
        {
            CandidateFilePath = candidateFilePath,
            CandidateFileName = candidateName,
            Score = score,
            Confidence = confidence,
            MatchedFactors = matchedFactors,
            RejectedFactors = rejectedFactors
        };
    }

    private static double ComputeStringSimilarity(string s1, string s2)
    {
        if (string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase)) return 1.0;
        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0.0;

        var tokens1 = s1.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var tokens2 = s2.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var set1 = new HashSet<string>(tokens1, StringComparer.OrdinalIgnoreCase);
        var set2 = new HashSet<string>(tokens2, StringComparer.OrdinalIgnoreCase);

        var intersection = set1.Count(set2.Contains);
        var union = set1.Union(set2, StringComparer.OrdinalIgnoreCase).Count();

        return union == 0 ? 0.0 : (double)intersection / union;
    }
}
