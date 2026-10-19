using System.IO;
using PersonalProvocateur.App.Models;

namespace PersonalProvocateur.App.Services;

/// <summary>
/// Implements a Fisher-Yates shuffle bag for video playback without consecutive repeats across cycles.
/// </summary>
public class VideoShuffleBag
{
    private readonly Random _random;
    private readonly Func<string, bool> _fileExistsChecker;
    private readonly List<VideoItem> _pool = new();
    private readonly Queue<VideoItem> _queue = new();
    private readonly object _lock = new();

    private VideoItem? _lastServedVideo;

    /// <summary>
    /// Gets the number of available videos in the current synchronized pool.
    /// </summary>
    public int PoolCount
    {
        get
        {
            lock (_lock)
            {
                return _pool.Count;
            }
        }
    }

    /// <summary>
    /// Gets the number of remaining videos in the active shuffle cycle.
    /// </summary>
    public int RemainingInCurrentCycle
    {
        get
        {
            lock (_lock)
            {
                return _queue.Count;
            }
        }
    }

    /// <summary>
    /// Gets the last served video item.
    /// </summary>
    public VideoItem? LastServedVideo
    {
        get
        {
            lock (_lock)
            {
                return _lastServedVideo;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of <see cref="VideoShuffleBag"/>.
    /// </summary>
    /// <param name="random">Optional Random generator for deterministic unit testing.</param>
    /// <param name="fileExistsChecker">Optional file existence checker for test decoupling from physical disk.</param>
    public VideoShuffleBag(Random? random = null, Func<string, bool>? fileExistsChecker = null)
    {
        _random = random ?? new Random();
        _fileExistsChecker = fileExistsChecker ?? File.Exists;
    }

    /// <summary>
    /// Synchronizes the pool with available videos, filtering out disabled or nonexistent files.
    /// </summary>
    /// <param name="availableVideos">Collection of candidate video items.</param>
    public void SyncPool(IEnumerable<VideoItem>? availableVideos)
    {
        lock (_lock)
        {
            _pool.Clear();

            if (availableVideos != null)
            {
                foreach (var video in availableVideos)
                {
                    if (video != null && video.IsEnabled &&
                        ((video.ContentType == ProvocationContentType.Video && !string.IsNullOrWhiteSpace(video.FilePath) && _fileExistsChecker(video.FilePath)) ||
                         (video.ContentType == ProvocationContentType.Text && !string.IsNullOrWhiteSpace(video.TextContent))))
                    {
                        _pool.Add(video);
                    }
                }
            }

            // Prune any items from the active queue that are no longer present or enabled in the pool
            if (_queue.Count > 0)
            {
                var validIds = _pool.Select(v => v.Id).ToHashSet();
                var remaining = _queue.Where(v => validIds.Contains(v.Id)).ToList();
                _queue.Clear();
                foreach (var item in remaining)
                {
                    _queue.Enqueue(item);
                }
            }
        }
    }

    /// <summary>
    /// Returns the next video item from the shuffled cycle. Reshuffles pool when cycle completes.
    /// Guarantees that across cycle boundaries, the first video of a new cycle is not the same as the last of previous cycle.
    /// </summary>
    /// <returns>Next video item to play, or null if pool is empty.</returns>
    public VideoItem? Next()
    {
        lock (_lock)
        {
            if (_pool.Count == 0)
            {
                return null;
            }

            if (_pool.Count == 1)
            {
                _lastServedVideo = _pool[0];
                return _pool[0];
            }

            if (_queue.Count == 0)
            {
                RefillAndShuffleQueue();
            }

            if (_queue.Count == 0)
            {
                return null;
            }

            var next = _queue.Dequeue();
            _lastServedVideo = next;
            return next;
        }
    }

    private void RefillAndShuffleQueue()
    {
        var shuffled = new List<VideoItem>(_pool);

        // Knuth Fisher-Yates shuffle
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        // Boundary constraint: if pool has >= 2 items, the first video of the new cycle
        // must not match the last served video of the preceding cycle.
        if (_lastServedVideo != null && shuffled.Count >= 2 && shuffled[0].Id == _lastServedVideo.Id)
        {
            int swapIndex = _random.Next(1, shuffled.Count);
            (shuffled[0], shuffled[swapIndex]) = (shuffled[swapIndex], shuffled[0]);
        }

        foreach (var item in shuffled)
        {
            _queue.Enqueue(item);
        }
    }
}
