using System.Text.RegularExpressions;
using UniversalMediaPlayer.Core.Models;

namespace UniversalMediaPlayer.Discovery;

public static class EpisodeParser
{
    // S01E03 / s1e3
    private static readonly Regex SeasonEpisodeRegex = new(
        @"[Ss](\d{1,2})[._ -]?[Ee](\d{1,3})", RegexOptions.Compiled);

    // 1x03
    private static readonly Regex CrossFormatRegex = new(
        @"\b(\d{1,2})x(\d{1,3})\b", RegexOptions.Compiled);

    // Anime format: " - 03 (1080p)" or " - 03v2" or " - 24v2"
    private static readonly Regex AnimeAbsoluteRegex = new(
        @"(?:^|\s)-\s*(\d{2,3})(?:v\d)?(?:\s|\(|$|[._-])", RegexOptions.Compiled);

    // Episode 03 / Ep 03 / E03 / Ep.09 / E10
    private static readonly Regex ExplicitEpisodeRegex = new(
        @"(?:^|[._ -])(?:Episode|Ep|[Ee])[._ -]?(\d{1,3})(?:[._ -]|$|\b)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static EpisodeInfo? Parse(string fileName, string? parentFolder = null)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

        // 1. Check Standard SxxExx
        var match = SeasonEpisodeRegex.Match(nameWithoutExt);
        if (match.Success)
        {
            var season = int.Parse(match.Groups[1].Value);
            var episode = int.Parse(match.Groups[2].Value);
            var title = ExtractTitle(nameWithoutExt, match.Index);
            return new EpisodeInfo
            {
                ShowTitle = CleanTitle(title, parentFolder),
                SeasonNumber = season,
                EpisodeNumber = episode,
                RawToken = match.Value
            };
        }

        // 2. Check Cross format 1x03
        match = CrossFormatRegex.Match(nameWithoutExt);
        if (match.Success)
        {
            var season = int.Parse(match.Groups[1].Value);
            var episode = int.Parse(match.Groups[2].Value);
            var title = ExtractTitle(nameWithoutExt, match.Index);
            return new EpisodeInfo
            {
                ShowTitle = CleanTitle(title, parentFolder),
                SeasonNumber = season,
                EpisodeNumber = episode,
                RawToken = match.Value
            };
        }

        // 3. Check Anime absolute numbering "Show - 03"
        match = AnimeAbsoluteRegex.Match(nameWithoutExt);
        if (match.Success)
        {
            var episode = int.Parse(match.Groups[1].Value);
            var title = ExtractTitle(nameWithoutExt, match.Index);
            return new EpisodeInfo
            {
                ShowTitle = CleanTitle(title, parentFolder),
                SeasonNumber = 1, // Default anime season 1
                EpisodeNumber = episode,
                RawToken = match.Value
            };
        }

        // 4. Check Explicit Episode (Ep 03 / E03 / Episode.08)
        match = ExplicitEpisodeRegex.Match(nameWithoutExt);
        if (match.Success)
        {
            var episode = int.Parse(match.Groups[1].Value);
            var title = ExtractTitle(nameWithoutExt, match.Index);
            return new EpisodeInfo
            {
                ShowTitle = CleanTitle(title, parentFolder),
                SeasonNumber = null,
                EpisodeNumber = episode,
                RawToken = match.Value
            };
        }

        return null;
    }

    private static string ExtractTitle(string text, int index)
    {
        if (index > 0)
        {
            var prefix = text[..index].Trim(" -_.".ToCharArray());
            return FilenameParser.NormalizeTitle(prefix);
        }
        return string.Empty;
    }

    private static string CleanTitle(string parsedTitle, string? parentFolder)
    {
        if (!string.IsNullOrWhiteSpace(parsedTitle))
        {
            return parsedTitle;
        }

        if (!string.IsNullOrWhiteSpace(parentFolder))
        {
            return FilenameParser.NormalizeTitle(parentFolder);
        }

        return "Unknown Show";
    }
}
