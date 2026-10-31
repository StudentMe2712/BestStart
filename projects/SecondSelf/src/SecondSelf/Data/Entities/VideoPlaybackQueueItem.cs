namespace SecondSelf.Data.Entities;

/// <summary>
/// Сущность элемента очереди циклического воспроизведения видео Daily Reset.
/// </summary>
public sealed class VideoPlaybackQueueItem
{
    public long QueueOrder { get; set; }
    public string VideoId { get; set; } = string.Empty;
    public bool IsPlayed { get; set; }
    public string? PlayedAt { get; set; }
}
