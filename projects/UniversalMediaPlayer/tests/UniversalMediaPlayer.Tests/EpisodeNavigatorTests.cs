namespace UniversalMediaPlayer.Tests;

using System;
using System.Collections.Generic;
using UniversalMediaPlayer.Core.Models;
using UniversalMediaPlayer.Discovery;
using Xunit;

public class EpisodeNavigatorTests
{
    [Fact]
    public void FindNextEpisode_StandardS01E01ToS01E02_ReturnsNextEpisode()
    {
        var ep1 = MediaItem.FromFilePath(@"C:\Anime\Show.S01E01.mkv");
        var ep2 = MediaItem.FromFilePath(@"C:\Anime\Show.S01E02.mkv");
        var ep3 = MediaItem.FromFilePath(@"C:\Anime\Show.S01E03.mkv");

        var package = new MediaPackage
        {
            PrimaryVideo = ep1,
            SiblingEpisodes = [ep2, ep3]
        };

        var next = EpisodeNavigator.FindNextEpisode(package);

        Assert.NotNull(next);
        Assert.Equal(ep2.FilePath, next.FilePath);
    }

    [Fact]
    public void FindPreviousEpisode_StandardS01E02ToS01E01_ReturnsPreviousEpisode()
    {
        var ep1 = MediaItem.FromFilePath(@"C:\Anime\Show.S01E01.mkv");
        var ep2 = MediaItem.FromFilePath(@"C:\Anime\Show.S01E02.mkv");
        var ep3 = MediaItem.FromFilePath(@"C:\Anime\Show.S01E03.mkv");

        var package = new MediaPackage
        {
            PrimaryVideo = ep2,
            SiblingEpisodes = [ep1, ep3]
        };

        var prev = EpisodeNavigator.FindPreviousEpisode(package);

        Assert.NotNull(prev);
        Assert.Equal(ep1.FilePath, prev.FilePath);
    }

    [Fact]
    public void FindNextEpisode_LastEpisode_ReturnsNull()
    {
        var ep1 = MediaItem.FromFilePath(@"C:\Anime\Show.S01E01.mkv");
        var ep2 = MediaItem.FromFilePath(@"C:\Anime\Show.S01E02.mkv");
        var ep3 = MediaItem.FromFilePath(@"C:\Anime\Show.S01E03.mkv");

        var package = new MediaPackage
        {
            PrimaryVideo = ep3,
            SiblingEpisodes = [ep1, ep2]
        };

        var next = EpisodeNavigator.FindNextEpisode(package);

        Assert.Null(next);
    }

    [Fact]
    public void FindPreviousEpisode_FirstEpisode_ReturnsNull()
    {
        var ep1 = MediaItem.FromFilePath(@"C:\Anime\Show.S01E01.mkv");
        var ep2 = MediaItem.FromFilePath(@"C:\Anime\Show.S01E02.mkv");

        var package = new MediaPackage
        {
            PrimaryVideo = ep1,
            SiblingEpisodes = [ep2]
        };

        var prev = EpisodeNavigator.FindPreviousEpisode(package);

        Assert.Null(prev);
    }

    [Fact]
    public void SeasonTransition_S01E12ToS02E01_HandlesTransitionBothWays()
    {
        var s1e11 = MediaItem.FromFilePath(@"C:\Anime\Show.S01E11.mkv");
        var s1e12 = MediaItem.FromFilePath(@"C:\Anime\Show.S01E12.mkv");
        var s2e01 = MediaItem.FromFilePath(@"C:\Anime\Show.S02E01.mkv");
        var s2e02 = MediaItem.FromFilePath(@"C:\Anime\Show.S02E02.mkv");

        // Test forward transition across seasons: S01E12 -> S02E01
        var packageS1E12 = new MediaPackage
        {
            PrimaryVideo = s1e12,
            SiblingEpisodes = [s1e11, s2e01, s2e02]
        };

        var next = EpisodeNavigator.FindNextEpisode(packageS1E12);
        Assert.NotNull(next);
        Assert.Equal(s2e01.FilePath, next.FilePath);

        // Test backward transition across seasons: S02E01 -> S01E12
        var packageS2E01 = new MediaPackage
        {
            PrimaryVideo = s2e01,
            SiblingEpisodes = [s1e11, s1e12, s2e02]
        };

        var prev = EpisodeNavigator.FindPreviousEpisode(packageS2E01);
        Assert.NotNull(prev);
        Assert.Equal(s1e12.FilePath, prev.FilePath);
    }

    [Fact]
    public void AnimeAbsoluteNumbering_Show01ToShow02_NavigatesCorrectly()
    {
        var ep1 = MediaItem.FromFilePath(@"C:\Anime\Frieren - 01 (1080p).mkv");
        var ep2 = MediaItem.FromFilePath(@"C:\Anime\Frieren - 02 (1080p).mkv");
        var ep3 = MediaItem.FromFilePath(@"C:\Anime\Frieren - 03 (1080p).mkv");

        var package = new MediaPackage
        {
            PrimaryVideo = ep1,
            SiblingEpisodes = [ep2, ep3]
        };

        var next = EpisodeNavigator.FindNextEpisode(package);
        Assert.NotNull(next);
        Assert.Equal(ep2.FilePath, next.FilePath);

        var package2 = new MediaPackage
        {
            PrimaryVideo = ep2,
            SiblingEpisodes = [ep1, ep3]
        };

        var prev = EpisodeNavigator.FindPreviousEpisode(package2);
        Assert.NotNull(prev);
        Assert.Equal(ep1.FilePath, prev.FilePath);
    }

    [Fact]
    public void Filtering_UnrelatedFilesInSameDirectory_FiltersOutUnrelatedShows()
    {
        var ep1 = MediaItem.FromFilePath(@"C:\Anime\ShowA - 01.mkv");
        var ep2 = MediaItem.FromFilePath(@"C:\Anime\ShowA - 02.mkv");
        var otherShow = MediaItem.FromFilePath(@"C:\Anime\ShowB - 01.mkv");
        var randomMovie = MediaItem.FromFilePath(@"C:\Anime\Movie.mkv");

        var package = new MediaPackage
        {
            PrimaryVideo = ep1,
            SiblingEpisodes = [ep2, otherShow, randomMovie]
        };

        var ordered = EpisodeNavigator.GetOrderedEpisodes(package);

        Assert.Equal(2, ordered.Count);
        Assert.Equal(ep1.FilePath, ordered[0].FilePath);
        Assert.Equal(ep2.FilePath, ordered[1].FilePath);

        var next = EpisodeNavigator.FindNextEpisode(package);
        Assert.NotNull(next);
        Assert.Equal(ep2.FilePath, next.FilePath);
    }
}
