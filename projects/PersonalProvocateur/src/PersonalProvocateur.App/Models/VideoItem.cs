namespace PersonalProvocateur.App.Models;

/// <summary>
/// Represents a video item in the user's provocation library.
/// </summary>
public class VideoItem
{
    /// <summary>
    /// Unique identifier for the video item.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// User-friendly title or file name of the video.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Absolute or relative file path to the video file on disk.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp of when the video was added to the library.
    /// </summary>
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this video is active and eligible for playback.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
