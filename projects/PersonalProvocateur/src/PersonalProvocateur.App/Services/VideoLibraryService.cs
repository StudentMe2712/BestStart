using System.IO;
using PersonalProvocateur.App.Models;
using PersonalProvocateur.App.Storage;

namespace PersonalProvocateur.App.Services;

/// <summary>
/// Service managing the local video library and coordinating with the shuffle bag and settings store.
/// </summary>
public class VideoLibraryService
{
    private readonly SettingsStore _settingsStore;
    private readonly VideoShuffleBag _shuffleBag;
    private readonly Func<string, bool> _fileExistsChecker;

    /// <summary>
    /// Gets the underlying video shuffle bag instance.
    /// </summary>
    public VideoShuffleBag ShuffleBag => _shuffleBag;

    /// <summary>
    /// Initializes a new instance of <see cref="VideoLibraryService"/>.
    /// </summary>
    /// <param name="settingsStore">Application settings store.</param>
    /// <param name="shuffleBag">Optional shuffle bag. If null, a new instance is created.</param>
    /// <param name="fileExistsChecker">Optional file existence checker function.</param>
    public VideoLibraryService(
        SettingsStore settingsStore,
        VideoShuffleBag? shuffleBag = null,
        Func<string, bool>? fileExistsChecker = null)
    {
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _fileExistsChecker = fileExistsChecker ?? File.Exists;
        _shuffleBag = shuffleBag ?? new VideoShuffleBag(fileExistsChecker: _fileExistsChecker);

        RefreshPool();
    }

    /// <summary>
    /// Returns all registered video items from current settings.
    /// </summary>
    public IReadOnlyList<VideoItem> GetAllVideos()
    {
        return _settingsStore.CurrentSettings.Videos.ToList().AsReadOnly();
    }

    /// <summary>
    /// Returns only active and existing video items.
    /// </summary>
    public IReadOnlyList<VideoItem> GetAvailableVideos()
    {
        return _settingsStore.CurrentSettings.Videos
            .Where(v => v != null && v.IsEnabled && !string.IsNullOrWhiteSpace(v.FilePath) && _fileExistsChecker(v.FilePath))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Adds a new video file to the library, updates settings, and refreshes the shuffle pool.
    /// </summary>
    /// <param name="filePath">Absolute or relative path to the video file.</param>
    /// <param name="title">Optional display title. Defaults to filename without extension.</param>
    /// <returns>The created <see cref="VideoItem"/>.</returns>
    public VideoItem AddVideo(string filePath, string? title = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Video file path cannot be null or empty.", nameof(filePath));
        }

        var resolvedTitle = string.IsNullOrWhiteSpace(title)
            ? Path.GetFileNameWithoutExtension(filePath)
            : title.Trim();

        if (string.IsNullOrWhiteSpace(resolvedTitle))
        {
            resolvedTitle = Path.GetFileName(filePath);
        }

        if (string.IsNullOrWhiteSpace(resolvedTitle))
        {
            resolvedTitle = "Provocation Video";
        }

        var videoItem = new VideoItem
        {
            Id = Guid.NewGuid(),
            Title = resolvedTitle,
            FilePath = filePath,
            AddedAtUtc = DateTime.UtcNow,
            IsEnabled = true
        };

        _settingsStore.Update(settings =>
        {
            settings.Videos.Add(videoItem);
        });

        RefreshPool();
        return videoItem;
    }

    /// <summary>
    /// Removes a video from the library by its ID.
    /// </summary>
    /// <param name="id">Unique ID of the video.</param>
    /// <returns>True if the video was found and removed; otherwise, false.</returns>
    public bool RemoveVideo(Guid id)
    {
        var removed = false;

        _settingsStore.Update(settings =>
        {
            removed = settings.Videos.RemoveAll(v => v.Id == id) > 0;
        });

        if (removed)
        {
            RefreshPool();
        }

        return removed;
    }

    /// <summary>
    /// Toggles the enabled state of a video.
    /// </summary>
    /// <param name="id">Unique ID of the video.</param>
    /// <param name="isEnabled">Target enabled state.</param>
    /// <returns>True if the video was found and updated; otherwise, false.</returns>
    public bool ToggleVideo(Guid id, bool isEnabled)
    {
        var updated = false;

        _settingsStore.Update(settings =>
        {
            var item = settings.Videos.FirstOrDefault(v => v.Id == id);
            if (item != null)
            {
                item.IsEnabled = isEnabled;
                updated = true;
            }
        });

        if (updated)
        {
            RefreshPool();
        }

        return updated;
    }

    /// <summary>
    /// Retrieves the next video from the shuffle bag.
    /// </summary>
    public VideoItem? GetNextVideo()
    {
        return _shuffleBag.Next();
    }

    /// <summary>
    /// Synchronizes the shuffle bag pool with the latest videos in settings.
    /// </summary>
    public void RefreshPool()
    {
        _shuffleBag.SyncPool(_settingsStore.CurrentSettings.Videos);
    }
}
