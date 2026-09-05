using UniversalMediaPlayer.Core.Enums;
using UniversalMediaPlayer.Core.Models;
using UniversalMediaPlayer.UI.Resources;

namespace UniversalMediaPlayer.UI.Helpers;

public static class FormatHelper
{
    public static string FormatTimecode(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
        {
            seconds = 0;
        }

        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    public static string FormatAudioTrackLabel(AudioTrack track, bool isPreferred = false)
    {
        var langName = GetLanguageDisplayName(track.Language);
        var pref = isPreferred ? " (Preferred)" : "";
        var titlePart = string.IsNullOrWhiteSpace(track.Title) || track.Title.StartsWith("Audio Track")
            ? $"{langName}{pref}"
            : $"{langName} ({track.Title}){pref}";

        var originStr = track.Origin == TrackOrigin.External ? "External" : "Embedded";
        var channelsStr = track.Channels switch
        {
            6 => "5.1",
            8 => "7.1",
            2 => "2.0",
            1 => "1.0",
            _ => $"{track.Channels} ch"
        };

        var codecStr = string.IsNullOrWhiteSpace(track.Codec) ? "" : $"{track.Codec.ToUpperInvariant()} · ";
        return $"{titlePart}\n{codecStr}{channelsStr} · {originStr}";
    }

    public static string FormatSubtitleTrackLabel(SubtitleTrack track, bool isPreferred = false)
    {
        var langName = GetLanguageDisplayName(track.Language);
        var pref = isPreferred ? " (Preferred)" : "";
        var originStr = track.Origin == TrackOrigin.External ? "External" : "Embedded";
        var formatStr = track.Format != SubtitleFormat.Unknown ? track.Format.ToString() : track.Codec.ToUpperInvariant();

        return $"{langName}{pref} {formatStr} · {originStr}";
    }

    public static string FormatAudioTrackLabelRu(AudioTrack track, bool isPreferred = false)
    {
        var langName = AppStrings.GetLanguageNameRu(track.Language);
        var pref = isPreferred ? " · " + AppStrings.Preferred : "";
        var titlePart = string.IsNullOrWhiteSpace(track.Title) || track.Title.StartsWith("Audio Track")
            ? langName
            : $"{langName} ({track.Title})";

        var originStr = track.Origin == TrackOrigin.External ? AppStrings.External : AppStrings.Embedded;
        var channelsStr = track.Channels switch
        {
            6 => "5.1",
            8 => "7.1",
            2 => "2.0",
            1 => "1.0",
            _ => $"{track.Channels} ch"
        };

        var codecStr = string.IsNullOrWhiteSpace(track.Codec) ? "" : $"{track.Codec.ToUpperInvariant()} · ";
        return $"{titlePart}{pref}\n{codecStr}{channelsStr} · {originStr}";
    }

    public static string FormatSubtitleTrackLabelRu(SubtitleTrack track, bool isPreferred = false)
    {
        var langName = AppStrings.GetLanguageNameRu(track.Language);
        var pref = isPreferred ? " · " + AppStrings.Preferred : "";
        var originStr = track.Origin == TrackOrigin.External ? AppStrings.External : AppStrings.Embedded;
        var formatStr = track.Format != SubtitleFormat.Unknown ? track.Format.ToString() : track.Codec.ToUpperInvariant();

        return $"{langName}{pref}\n{formatStr} · {originStr}";
    }

    /// <summary>
    /// Cleans technical release groups, tags, and brackets from a filename to produce a human-readable title.
    /// E.g. "[Beatrice-Raws] Kimi no Na wa [BDRip 1920x1080 x264].mkv" -> "Kimi no Na wa"
    /// </summary>
    public static string CleanTitle(string rawFileName)
    {
        if (string.IsNullOrWhiteSpace(rawFileName)) return string.Empty;

        var name = System.IO.Path.GetFileNameWithoutExtension(rawFileName);

        // Remove release group prefixes: [Group] Name or (Group) Name
        name = System.Text.RegularExpressions.Regex.Replace(name, @"^\s*(\[[^\]]+\]|\([^\)]+\))\s*", "");

        // Remove trailing bracket tags: Name [1080p BDRip x264...] or Name (1080p...)
        name = System.Text.RegularExpressions.Regex.Replace(name, @"\s*(\[[^\]]+\]|\([^\)]+\))\s*$", "");

        // Trim dots, underscores, hyphens from edges
        name = name.Replace('_', ' ').Trim(' ', '-', '.');

        return string.IsNullOrWhiteSpace(name) ? System.IO.Path.GetFileNameWithoutExtension(rawFileName) : name;
    }

    public static string GetLanguageDisplayName(string langCode)
    {
        return langCode.ToLowerInvariant() switch
        {
            "ru" or "rus" => "🇷🇺 Russian",
            "en" or "eng" => "🇬🇧 English",
            "ja" or "jpn" => "🇯🇵 Japanese",
            "de" or "ger" or "deu" => "🇩🇪 German",
            "fr" or "fra" or "fre" => "🇫🇷 French",
            "es" or "spa" => "🇪🇸 Spanish",
            "it" or "ita" => "🇮🇹 Italian",
            "zh" or "chi" or "zho" => "🇨🇳 Chinese",
            "ko" or "kor" => "🇰🇷 Korean",
            "uk" or "ukr" => "🇺🇦 Ukrainian",
            "orig" or "original" => "Original",
            _ => string.IsNullOrWhiteSpace(langCode) || langCode == "und" ? "Original" : langCode.ToUpperInvariant()
        };
    }
}
