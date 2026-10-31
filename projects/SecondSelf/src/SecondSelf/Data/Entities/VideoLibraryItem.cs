using System;

namespace SecondSelf.Data.Entities;

/// <summary>
/// Сущность видеофайла локальной видеотеки Daily Reset.
/// </summary>
public sealed class VideoLibraryItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public double DurationSeconds { get; set; } = 0;
    public string? ThumbnailCachePath { get; set; }
    public int PlaybackCount { get; set; } = 0;
    public bool IsMissing { get; set; } = false;
    public string LastVerifiedAt { get; set; } = DateTime.UtcNow.ToString("O");
}
