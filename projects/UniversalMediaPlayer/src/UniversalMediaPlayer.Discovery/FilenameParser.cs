using System.Text.RegularExpressions;

namespace UniversalMediaPlayer.Discovery;

public static class FilenameParser
{
    private static readonly Regex ReleaseGroupPrefixRegex = new(
        @"^\[([^\]]+)\]\s*", RegexOptions.Compiled);

    private static readonly Regex ReleaseGroupSuffixRegex = new(
        @"\s*\[[0-9A-Fa-f]{8}\]$", RegexOptions.Compiled);

    private static readonly Regex MetadataTagsRegex = new(
        @"(?:\[|\b)(2160p|1080p|720p|480p|4k|uhd|web-dl|webrip|bluray|bdrip|hdtv|dvdrip|remux|" +
        @"x264|x265|hevc|av1|h\.?264|h\.?265|10bit|hi10p|8bit|" +
        @"aac|ac3|eac3|flac|dts(-hd)?|truehd|opus|mp3|ddp5\.1|atmos)(?:\]|\b)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string ExtractReleaseGroup(string fileName)
    {
        var match = ReleaseGroupPrefixRegex.Match(fileName);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    public static string NormalizeTitle(string rawName)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(rawName);

        // Strip release group prefix [Group]
        nameWithoutExt = ReleaseGroupPrefixRegex.Replace(nameWithoutExt, string.Empty);

        // Strip CRC suffix [12345678]
        nameWithoutExt = ReleaseGroupSuffixRegex.Replace(nameWithoutExt, string.Empty);

        // Strip metadata tags (1080p, BluRay, HEVC, etc.)
        nameWithoutExt = MetadataTagsRegex.Replace(nameWithoutExt, string.Empty);

        // Standardize separators
        nameWithoutExt = nameWithoutExt.Replace('.', ' ').Replace('_', ' ');

        // Clean extra whitespace
        var cleaned = Regex.Replace(nameWithoutExt, @"\s+", " ").Trim();

        return string.IsNullOrWhiteSpace(cleaned) ? Path.GetFileNameWithoutExtension(rawName) : cleaned;
    }
}
