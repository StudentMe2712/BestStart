namespace UniversalMediaPlayer.Discovery;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UniversalMediaPlayer.Core.Enums;
using UniversalMediaPlayer.Core.Models;
using CoreConfidence = UniversalMediaPlayer.Core.Models.MatchConfidence;

public static class PreferredTrackResolver
{
    public static TrackResolutionResult<AudioTrack> ResolveAudioTrack(
        ShowPreferences? preferences,
        IReadOnlyList<AudioTrack> availableTracks)
    {
        if (availableTracks == null || availableTracks.Count == 0)
        {
            return new TrackResolutionResult<AudioTrack>
            {
                SelectedTrack = null,
                Reason = TrackSelectionReason.None,
                Confidence = CoreConfidence.None,
                Explanation = "No audio tracks available."
            };
        }

        var prefTrack = preferences?.PreferredAudioTrack;
        var preferredLang = preferences?.PreferredAudioLanguage ??
                            (prefTrack != null && !string.IsNullOrWhiteSpace(prefTrack.Language) && !prefTrack.Language.Equals("und", StringComparison.OrdinalIgnoreCase) ? prefTrack.Language : null);

        // 1. Exact track match
        if (prefTrack != null)
        {
            var exactMatch = FindExactAudioMatch(prefTrack, availableTracks);
            if (exactMatch != null)
            {
                var titleOrLang = !string.IsNullOrWhiteSpace(prefTrack.Title) ? prefTrack.Title : exactMatch.Language;
                return new TrackResolutionResult<AudioTrack>
                {
                    SelectedTrack = exactMatch,
                    Reason = TrackSelectionReason.ExactTrackMatch,
                    Confidence = CoreConfidence.High,
                    Explanation = $"Exact match found for preferred audio track '{titleOrLang}'."
                };
            }
        }

        // 2. Preferred language match with tie-breaking
        if (!string.IsNullOrWhiteSpace(preferredLang) && !preferredLang.Equals("und", StringComparison.OrdinalIgnoreCase))
        {
            var langMatches = availableTracks
                .Where(t => LanguagesMatch(t.Language, preferredLang))
                .ToList();

            if (langMatches.Count > 0)
            {
                var best = langMatches
                    .OrderByDescending(t => ScoreAudioTieBreak(t, prefTrack))
                    .First();

                return new TrackResolutionResult<AudioTrack>
                {
                    SelectedTrack = best,
                    Reason = TrackSelectionReason.PreferredLanguage,
                    Confidence = CoreConfidence.Medium,
                    Explanation = $"Selected {GetLanguageDisplayName(best.Language)} audio based on preferred language."
                };
            }
        }

        // 3. Fallback: Backend default or first available track
        var fallback = availableTracks.FirstOrDefault(t => t.IsSelected) ?? availableTracks[0];
        var reason = fallback.IsSelected ? TrackSelectionReason.BackendDefault : TrackSelectionReason.FallbackFirstAvailable;
        var confidence = CoreConfidence.Low;

        string explanation;
        if (!string.IsNullOrWhiteSpace(preferredLang) && !preferredLang.Equals("und", StringComparison.OrdinalIgnoreCase))
        {
            explanation = $"Preferred {GetLanguageDisplayName(preferredLang)} audio unavailable. Fallback: {GetLanguageDisplayName(fallback.Language)} ({fallback.Origin})";
        }
        else if (fallback.IsSelected)
        {
            explanation = $"Using backend default audio: {fallback.DisplaySummary}";
        }
        else
        {
            explanation = $"Falling back to first available audio: {fallback.DisplaySummary}";
        }

        return new TrackResolutionResult<AudioTrack>
        {
            SelectedTrack = fallback,
            Reason = reason,
            Confidence = confidence,
            Explanation = explanation
        };
    }

    public static TrackResolutionResult<SubtitleTrack> ResolveSubtitleTrack(
        ShowPreferences? preferences,
        IReadOnlyList<SubtitleTrack> availableTracks)
    {
        // 4. For subtitles: If preferences.SubtitleEnabled == false, explicitly return SelectedTrack = null, Reason = ExplicitlyDisabled
        if (preferences is { SubtitleEnabled: false })
        {
            return new TrackResolutionResult<SubtitleTrack>
            {
                SelectedTrack = null,
                Reason = TrackSelectionReason.ExplicitlyDisabled,
                Confidence = CoreConfidence.High,
                Explanation = "Subtitles explicitly disabled by user preference."
            };
        }

        if (availableTracks == null || availableTracks.Count == 0)
        {
            return new TrackResolutionResult<SubtitleTrack>
            {
                SelectedTrack = null,
                Reason = TrackSelectionReason.None,
                Confidence = CoreConfidence.None,
                Explanation = "No subtitle tracks available."
            };
        }

        var prefTrack = preferences?.PreferredSubtitleTrack;
        var preferredLang = preferences?.PreferredSubtitleLanguage ??
                            (prefTrack != null && !string.IsNullOrWhiteSpace(prefTrack.Language) && !prefTrack.Language.Equals("und", StringComparison.OrdinalIgnoreCase) ? prefTrack.Language : null);

        // 1. Exact track match
        if (prefTrack != null)
        {
            var exactMatch = FindExactSubtitleMatch(prefTrack, availableTracks);
            if (exactMatch != null)
            {
                var titleOrLang = !string.IsNullOrWhiteSpace(prefTrack.Title) ? prefTrack.Title : exactMatch.Language;
                return new TrackResolutionResult<SubtitleTrack>
                {
                    SelectedTrack = exactMatch,
                    Reason = TrackSelectionReason.ExactTrackMatch,
                    Confidence = CoreConfidence.High,
                    Explanation = $"Exact match found for preferred subtitle track '{titleOrLang}'."
                };
            }
        }

        // 2. Preferred language match with tie-breaking
        if (!string.IsNullOrWhiteSpace(preferredLang) && !preferredLang.Equals("und", StringComparison.OrdinalIgnoreCase))
        {
            var langMatches = availableTracks
                .Where(t => LanguagesMatch(t.Language, preferredLang))
                .ToList();

            if (langMatches.Count > 0)
            {
                var best = langMatches
                    .OrderByDescending(t => ScoreSubtitleTieBreak(t, prefTrack))
                    .First();

                return new TrackResolutionResult<SubtitleTrack>
                {
                    SelectedTrack = best,
                    Reason = TrackSelectionReason.PreferredLanguage,
                    Confidence = CoreConfidence.Medium,
                    Explanation = $"Selected {GetLanguageDisplayName(best.Language)} subtitle ({best.Format}) matching preferred language."
                };
            }

            // Fallback: preferred subtitle language is missing
            return new TrackResolutionResult<SubtitleTrack>
            {
                SelectedTrack = null,
                Reason = TrackSelectionReason.None,
                Confidence = CoreConfidence.None,
                Explanation = $"Preferred {GetLanguageDisplayName(preferredLang)} subtitle unavailable."
            };
        }

        // No preferred language: check for backend default
        var backendDefault = availableTracks.FirstOrDefault(t => t.IsSelected);
        if (backendDefault != null)
        {
            return new TrackResolutionResult<SubtitleTrack>
            {
                SelectedTrack = backendDefault,
                Reason = TrackSelectionReason.BackendDefault,
                Confidence = CoreConfidence.Low,
                Explanation = $"Using backend default subtitle: {backendDefault.DisplaySummary}"
            };
        }

        return new TrackResolutionResult<SubtitleTrack>
        {
            SelectedTrack = null,
            Reason = TrackSelectionReason.None,
            Confidence = CoreConfidence.None,
            Explanation = "No subtitle preference configured."
        };
    }

    private static AudioTrack? FindExactAudioMatch(TrackPreference pref, IReadOnlyList<AudioTrack> tracks)
    {
        if (!string.IsNullOrWhiteSpace(pref.Title))
        {
            var candidates = tracks.Where(t =>
            {
                if (!string.IsNullOrWhiteSpace(pref.Language) && !pref.Language.Equals("und", StringComparison.OrdinalIgnoreCase))
                {
                    if (!LanguagesMatch(t.Language, pref.Language)) return false;
                }

                return MatchTitle(t.Title, pref.Title) ||
                       (t.ExternalFilePath != null && MatchTitle(Path.GetFileName(t.ExternalFilePath), pref.Title));
            }).ToList();

            if (candidates.Count > 0)
            {
                return candidates.OrderByDescending(t => ScoreExactAudioCandidate(t, pref)).First();
            }

            return null;
        }

        bool hasSpecifics = !string.IsNullOrWhiteSpace(pref.Codec) ||
                            pref.Channels.HasValue ||
                            pref.Origin.HasValue;

        if (hasSpecifics && !string.IsNullOrWhiteSpace(pref.Language) && !pref.Language.Equals("und", StringComparison.OrdinalIgnoreCase))
        {
            var candidates = tracks.Where(t =>
            {
                if (!LanguagesMatch(t.Language, pref.Language)) return false;
                if (!string.IsNullOrWhiteSpace(pref.Codec) && !string.Equals(t.Codec, pref.Codec, StringComparison.OrdinalIgnoreCase)) return false;
                if (pref.Channels.HasValue && t.Channels != pref.Channels.Value) return false;
                if (pref.Origin.HasValue && t.Origin != pref.Origin.Value) return false;
                return true;
            }).ToList();

            if (candidates.Count > 0)
            {
                return candidates.First();
            }
        }

        return null;
    }

    private static SubtitleTrack? FindExactSubtitleMatch(TrackPreference pref, IReadOnlyList<SubtitleTrack> tracks)
    {
        if (!string.IsNullOrWhiteSpace(pref.Title))
        {
            var candidates = tracks.Where(t =>
            {
                if (!string.IsNullOrWhiteSpace(pref.Language) && !pref.Language.Equals("und", StringComparison.OrdinalIgnoreCase))
                {
                    if (!LanguagesMatch(t.Language, pref.Language)) return false;
                }

                return MatchTitle(t.Title, pref.Title) ||
                       (t.ExternalFilePath != null && MatchTitle(Path.GetFileName(t.ExternalFilePath), pref.Title));
            }).ToList();

            if (candidates.Count > 0)
            {
                return candidates.OrderByDescending(t => ScoreExactSubtitleCandidate(t, pref)).First();
            }

            return null;
        }

        bool hasSpecifics = (pref.Format.HasValue && pref.Format.Value != SubtitleFormat.Unknown) ||
                            pref.Origin.HasValue;

        if (hasSpecifics && !string.IsNullOrWhiteSpace(pref.Language) && !pref.Language.Equals("und", StringComparison.OrdinalIgnoreCase))
        {
            var candidates = tracks.Where(t =>
            {
                if (!LanguagesMatch(t.Language, pref.Language)) return false;
                if (pref.Format.HasValue && pref.Format.Value != SubtitleFormat.Unknown && t.Format != pref.Format.Value) return false;
                if (pref.Origin.HasValue && t.Origin != pref.Origin.Value) return false;
                return true;
            }).ToList();

            if (candidates.Count > 0)
            {
                return candidates.First();
            }
        }

        return null;
    }

    private static int ScoreExactAudioCandidate(AudioTrack track, TrackPreference pref)
    {
        int score = 100;
        if (pref.Origin.HasValue && track.Origin == pref.Origin.Value) score += 20;
        if (!string.IsNullOrWhiteSpace(pref.Codec) && string.Equals(track.Codec, pref.Codec, StringComparison.OrdinalIgnoreCase)) score += 15;
        if (pref.Channels.HasValue && track.Channels == pref.Channels.Value) score += 15;
        return score;
    }

    private static int ScoreExactSubtitleCandidate(SubtitleTrack track, TrackPreference pref)
    {
        int score = 100;
        if (pref.Format.HasValue && track.Format == pref.Format.Value) score += 30;
        if (pref.Origin.HasValue && track.Origin == pref.Origin.Value) score += 20;
        return score;
    }

    private static int ScoreAudioTieBreak(AudioTrack track, TrackPreference? pref)
    {
        int score = 0;

        if (pref != null)
        {
            if (pref.Origin.HasValue && track.Origin == pref.Origin.Value) score += 30;
            else if (track.Origin == TrackOrigin.External) score += 20;

            if (!string.IsNullOrWhiteSpace(pref.Codec) && string.Equals(track.Codec, pref.Codec, StringComparison.OrdinalIgnoreCase)) score += 15;
            if (pref.Channels.HasValue && track.Channels == pref.Channels.Value) score += 15;
        }
        else
        {
            if (track.Origin == TrackOrigin.External) score += 20;
        }

        score += track.Channels * 2;
        if (IsLosslessAudio(track.Codec)) score += 5;

        return score;
    }

    private static int ScoreSubtitleTieBreak(SubtitleTrack track, TrackPreference? pref)
    {
        int score = 0;

        if (pref?.Format.HasValue == true && pref.Format.Value != SubtitleFormat.Unknown)
        {
            if (track.Format == pref.Format.Value) score += 60;
        }
        else
        {
            if (track.Format is SubtitleFormat.ASS or SubtitleFormat.SSA) score += 40;
            else if (track.Format == SubtitleFormat.SRT) score += 20;
            else if (track.Format == SubtitleFormat.VTT) score += 10;
        }

        if (pref?.Origin.HasValue == true && track.Origin == pref.Origin.Value) score += 25;
        else if (track.Origin == TrackOrigin.External) score += 15;

        if (!string.IsNullOrWhiteSpace(pref?.Title) && MatchTitle(track.Title, pref.Title)) score += 30;

        return score;
    }

    private static bool LanguagesMatch(string? lang1, string? lang2)
    {
        if (string.IsNullOrWhiteSpace(lang1) || string.IsNullOrWhiteSpace(lang2)) return false;
        if (string.Equals(lang1, lang2, StringComparison.OrdinalIgnoreCase)) return true;
        return LanguageDetector.AreLanguagesEqual(lang1, lang2);
    }

    private static bool MatchTitle(string actual, string expected)
    {
        if (string.IsNullOrWhiteSpace(actual) || string.IsNullOrWhiteSpace(expected)) return false;
        if (actual.Contains(expected, StringComparison.OrdinalIgnoreCase) ||
            expected.Contains(actual, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var grpActual = FilenameParser.ExtractReleaseGroup(actual);
        var grpExpected = FilenameParser.ExtractReleaseGroup(expected);
        if (!string.IsNullOrEmpty(grpActual) && !string.IsNullOrEmpty(grpExpected))
        {
            return string.Equals(grpActual, grpExpected, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static string GetLanguageDisplayName(string langCode)
    {
        var canonical = LanguageDetector.DetectLanguage("file." + langCode);
        if (canonical == "und") canonical = langCode.Trim().ToLowerInvariant();

        return canonical switch
        {
            "ru" => "Russian",
            "en" => "English",
            "ja" => "Japanese",
            "de" => "German",
            "fr" => "French",
            "es" => "Spanish",
            "it" => "Italian",
            "zh" => "Chinese",
            "ko" => "Korean",
            "uk" => "Ukrainian",
            _ => char.ToUpperInvariant(canonical[0]) + (canonical.Length > 1 ? canonical[1..] : string.Empty)
        };
    }

    private static bool IsLosslessAudio(string codec)
    {
        return codec.Equals("flac", StringComparison.OrdinalIgnoreCase) ||
               codec.Equals("alac", StringComparison.OrdinalIgnoreCase) ||
               codec.Equals("truehd", StringComparison.OrdinalIgnoreCase) ||
               codec.Contains("dtshd", StringComparison.OrdinalIgnoreCase) ||
               codec.Equals("pcm", StringComparison.OrdinalIgnoreCase) ||
               codec.Equals("wav", StringComparison.OrdinalIgnoreCase);
    }
}
