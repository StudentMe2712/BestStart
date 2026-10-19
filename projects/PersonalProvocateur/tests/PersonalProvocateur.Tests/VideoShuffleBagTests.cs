using PersonalProvocateur.App.Models;
using PersonalProvocateur.App.Services;
using Xunit;

namespace PersonalProvocateur.Tests;

public class VideoShuffleBagTests
{
    private static VideoItem CreateVideo(string name, bool isEnabled = true)
    {
        return new VideoItem
        {
            Id = Guid.NewGuid(),
            Title = name,
            FilePath = $@"C:\Videos\{name}.mp4",
            IsEnabled = isEnabled
        };
    }

    [Fact]
    public void EmptyPool_ReturnsNull()
    {
        var bag = new VideoShuffleBag();
        bag.SyncPool(Array.Empty<VideoItem>());

        Assert.Equal(0, bag.PoolCount);
        Assert.Null(bag.Next());
    }

    [Fact]
    public void SingleVideo_ReturnsSameVideoRepeatedly()
    {
        var video = CreateVideo("Single");
        var bag = new VideoShuffleBag(fileExistsChecker: _ => true);
        bag.SyncPool(new[] { video });

        Assert.Equal(1, bag.PoolCount);

        for (int i = 0; i < 5; i++)
        {
            var next = bag.Next();
            Assert.NotNull(next);
            Assert.Equal(video.Id, next.Id);
        }
    }

    [Fact]
    public void SyncPool_FiltersDisabledAndNonexistentVideos()
    {
        var v1 = CreateVideo("ExistsAndEnabled", isEnabled: true);
        var v2 = CreateVideo("Disabled", isEnabled: false);
        var v3 = CreateVideo("MissingOnDisk", isEnabled: true);

        // fileExistsChecker returns true only for v1 and v2
        var bag = new VideoShuffleBag(fileExistsChecker: path => path.Contains("ExistsAndEnabled") || path.Contains("Disabled"));
        bag.SyncPool(new[] { v1, v2, v3 });

        Assert.Equal(1, bag.PoolCount);

        var served = bag.Next();
        Assert.NotNull(served);
        Assert.Equal(v1.Id, served.Id);
    }

    [Fact]
    public void Cycle_ContainsAllVideosWithoutDuplicatesWithinCycle()
    {
        var videos = Enumerable.Range(1, 5)
            .Select(i => CreateVideo($"Video{i}"))
            .ToList();

        var bag = new VideoShuffleBag(fileExistsChecker: _ => true);
        bag.SyncPool(videos);

        Assert.Equal(5, bag.PoolCount);

        var firstCycleIds = new HashSet<Guid>();
        for (int i = 0; i < 5; i++)
        {
            var next = bag.Next();
            Assert.NotNull(next);
            firstCycleIds.Add(next.Id);
        }

        Assert.Equal(5, firstCycleIds.Count);
    }

    [Fact]
    public void BoundaryCondition_FirstVideoOfNewCycleNeverMatchesLastVideoOfPreviousCycle()
    {
        var videos = Enumerable.Range(1, 4)
            .Select(i => CreateVideo($"Clip{i}"))
            .ToList();

        var random = new Random(42);
        var bag = new VideoShuffleBag(random, fileExistsChecker: _ => true);
        bag.SyncPool(videos);

        VideoItem? lastOfPrevCycle = null;

        // Run 50 complete cycles to ensure the boundary invariant strictly holds
        for (int cycle = 0; cycle < 50; cycle++)
        {
            var firstOfNewCycle = bag.Next();
            Assert.NotNull(firstOfNewCycle);

            if (lastOfPrevCycle != null)
            {
                Assert.NotEqual(lastOfPrevCycle.Id, firstOfNewCycle.Id);
            }

            // Consume remainder of the cycle (pool count is 4, so 3 remaining)
            VideoItem? current = firstOfNewCycle;
            for (int i = 1; i < 4; i++)
            {
                current = bag.Next();
                Assert.NotNull(current);
            }

            lastOfPrevCycle = current;
        }
    }

    [Fact]
    public void BoundaryCondition_TwoVideos_AlternateProperly()
    {
        var v1 = CreateVideo("Clip1");
        var v2 = CreateVideo("Clip2");

        var bag = new VideoShuffleBag(fileExistsChecker: _ => true);
        bag.SyncPool(new[] { v1, v2 });

        VideoItem? last = null;
        for (int i = 0; i < 20; i++)
        {
            var next = bag.Next();
            Assert.NotNull(next);
            if (last != null)
            {
                // With 2 videos, the boundary check guarantees alternating videos across cycles
                Assert.NotEqual(last.Id, next.Id);
            }
            last = next;
        }
    }
}
