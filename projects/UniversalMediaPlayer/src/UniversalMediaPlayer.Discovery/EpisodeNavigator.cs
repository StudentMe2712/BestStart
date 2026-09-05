namespace UniversalMediaPlayer.Discovery;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UniversalMediaPlayer.Core.Models;

public static class EpisodeNavigator
{
    public static MediaItem? FindNextEpisode(MediaPackage currentPackage)
    {
        ArgumentNullException.ThrowIfNull(currentPackage);
        if (currentPackage.PrimaryVideo == null) return null;

        var ordered = GetOrderedEpisodes(currentPackage);
        if (ordered.Count <= 1) return null;

        int currentIndex = FindCurrentIndex(ordered, currentPackage.PrimaryVideo);
        if (currentIndex >= 0 && currentIndex + 1 < ordered.Count)
        {
            return ordered[currentIndex + 1];
        }

        return null;
    }

    public static MediaItem? FindPreviousEpisode(MediaPackage currentPackage)
    {
        ArgumentNullException.ThrowIfNull(currentPackage);
        if (currentPackage.PrimaryVideo == null) return null;

        var ordered = GetOrderedEpisodes(currentPackage);
        if (ordered.Count <= 1) return null;

        int currentIndex = FindCurrentIndex(ordered, currentPackage.PrimaryVideo);
        if (currentIndex > 0)
        {
            return ordered[currentIndex - 1];
        }

        return null;
    }

    public static IReadOnlyList<MediaItem> GetOrderedEpisodes(MediaPackage currentPackage)
    {
        if (currentPackage == null || currentPackage.PrimaryVideo == null)
        {
            return [];
        }

        var allItems = new List<MediaItem> { currentPackage.PrimaryVideo };
        if (currentPackage.SiblingEpisodes != null && currentPackage.SiblingEpisodes.Count > 0)
        {
            allItems.AddRange(currentPackage.SiblingEpisodes);
        }

        var distinctItems = allItems
            .GroupBy(i => i.FilePath, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var primaryParentDir = Path.GetDirectoryName(currentPackage.PrimaryVideo.FilePath);
        var primaryParentFolder = !string.IsNullOrEmpty(primaryParentDir) ? Path.GetFileName(primaryParentDir) : null;
        var primaryEpisode = currentPackage.Episode ?? EpisodeParser.Parse(currentPackage.PrimaryVideo.FileName, primaryParentFolder);
        var targetShowId = ShowIdentityResolver.ResolveShowId(currentPackage.PrimaryVideo.FilePath, primaryEpisode);

        var parsedItems = new List<(MediaItem Item, int Season, int Episode)>();

        foreach (var item in distinctItems)
        {
            var parentDir = Path.GetDirectoryName(item.FilePath);
            var parentFolder = !string.IsNullOrEmpty(parentDir) ? Path.GetFileName(parentDir) : null;
            var epInfo = EpisodeParser.Parse(item.FileName, parentFolder);
            if (epInfo == null)
            {
                continue;
            }

            var itemShowId = ShowIdentityResolver.ResolveShowId(item.FilePath, epInfo);
            if (!string.Equals(targetShowId, itemShowId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            parsedItems.Add((item, epInfo.SeasonNumber ?? 1, epInfo.EpisodeNumber));
        }

        return parsedItems
            .OrderBy(x => x.Season)
            .ThenBy(x => x.Episode)
            .ThenBy(x => x.Item.FileName, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Item)
            .ToList();
    }

    private static int FindCurrentIndex(IReadOnlyList<MediaItem> items, MediaItem primaryVideo)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (string.Equals(items[i].FilePath, primaryVideo.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        for (int i = 0; i < items.Count; i++)
        {
            if (string.Equals(items[i].FileName, primaryVideo.FileName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
