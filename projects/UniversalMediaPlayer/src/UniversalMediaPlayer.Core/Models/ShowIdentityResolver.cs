namespace UniversalMediaPlayer.Core.Models;

using System.Text.RegularExpressions;

public static class ShowIdentityResolver
{
    private static readonly Regex ReleaseGroupPrefixRegex = new(
        @"^\[([^\]]+)\]\s*", RegexOptions.Compiled);

    private static readonly Regex ReleaseGroupSuffixRegex = new(
        @"\s*\[[0-9A-Fa-f]{8}\]$", RegexOptions.Compiled);

    private static readonly Regex SeasonOrExtraFolderRegex = new(
        @"^([Ss]eason\s*\d+|[Ss]\d+|[Ss]pecials?|[Ee]xtras?|[Ss][Pp])$", RegexOptions.Compiled);

    private static readonly Regex EpisodeTokenRegex = new(
        @"(?:\s-\s*\d+|\b[Ss]\d+[._ -]?[Ee]\d+|\b\d+x\d+\b|\b(?:Episode|Ep|[Ee])[._ -]?\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AlphanumericOnlyRegex = new(
        @"[^\p{L}\p{Nd}\s]+", RegexOptions.Compiled);

    private static readonly Regex MultipleWhitespaceRegex = new(
        @"\s+", RegexOptions.Compiled);

    public static string ResolveShowId(MediaPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        if (package.Episode != null && !string.IsNullOrWhiteSpace(package.Episode.ShowTitle))
        {
            return ResolveShowId(package.PrimaryVideo?.FilePath ?? string.Empty, package.Episode);
        }

        if (package.PrimaryVideo != null && !string.IsNullOrWhiteSpace(package.PrimaryVideo.FilePath))
        {
            return ResolveShowId(package.PrimaryVideo.FilePath, null);
        }

        return "unknown";
    }

    public static string ResolveShowId(string filePath, EpisodeInfo? episode = null)
    {
        if (episode != null && !string.IsNullOrWhiteSpace(episode.ShowTitle) &&
            !string.Equals(episode.ShowTitle, "Unknown Show", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeShowTitle(episode.ShowTitle);
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return "unknown";
        }

        var trimmed = filePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "unknown";
        }

        string rawTitle;
        if (Path.HasExtension(trimmed))
        {
            var fileName = Path.GetFileNameWithoutExtension(trimmed);
            var match = EpisodeTokenRegex.Match(fileName);
            if (match.Success && match.Index > 0)
            {
                rawTitle = fileName[..match.Index].Trim(" -_.".ToCharArray());
            }
            else
            {
                var dir = Path.GetDirectoryName(trimmed);
                if (!string.IsNullOrEmpty(dir))
                {
                    var folderName = Path.GetFileName(dir);
                    if (SeasonOrExtraFolderRegex.IsMatch(folderName))
                    {
                        var parentDir = Path.GetDirectoryName(dir);
                        if (!string.IsNullOrEmpty(parentDir))
                        {
                            folderName = Path.GetFileName(parentDir);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(folderName) && !folderName.EndsWith(':'))
                    {
                        rawTitle = folderName;
                    }
                    else
                    {
                        rawTitle = fileName;
                    }
                }
                else
                {
                    rawTitle = fileName;
                }
            }
        }
        else
        {
            var folderName = Path.GetFileName(trimmed);
            if (SeasonOrExtraFolderRegex.IsMatch(folderName))
            {
                var parentDir = Path.GetDirectoryName(trimmed);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    folderName = Path.GetFileName(parentDir);
                }
            }

            rawTitle = !string.IsNullOrWhiteSpace(folderName) && !folderName.EndsWith(':')
                ? folderName
                : trimmed;
        }

        return NormalizeShowTitle(rawTitle);
    }

    public static string NormalizeShowTitle(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "unknown";
        }

        var cleaned = ReleaseGroupPrefixRegex.Replace(raw, string.Empty);
        cleaned = ReleaseGroupSuffixRegex.Replace(cleaned, string.Empty);

        cleaned = cleaned.Replace('.', ' ').Replace('_', ' ').Replace('-', ' ');
        cleaned = AlphanumericOnlyRegex.Replace(cleaned, " ");
        cleaned = MultipleWhitespaceRegex.Replace(cleaned, " ").Trim();

        return string.IsNullOrWhiteSpace(cleaned) ? "unknown" : cleaned.ToLowerInvariant();
    }
}
