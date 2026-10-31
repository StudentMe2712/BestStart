using System;
using CommunityToolkit.Mvvm.ComponentModel;
using SecondSelf.Data.Entities;

namespace SecondSelf.Core.ViewModels;

/// <summary>
/// ViewModel строки видеофайла в таблице библиотеки видео (Settings).
/// </summary>
public partial class VideoLibraryItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private double _durationSeconds;

    [ObservableProperty]
    private string? _thumbnailCachePath;

    [ObservableProperty]
    private int _playbackCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _isMissing;

    [ObservableProperty]
    private string _lastVerifiedAt = string.Empty;

    public string StatusText => IsMissing ? "Файл не найден" : "OK";

    public string PlaybackCountText => $"Показов: {PlaybackCount}";

    public string FormattedDuration
    {
        get
        {
            var ts = TimeSpan.FromSeconds(DurationSeconds);
            return $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}";
        }
    }

    public VideoLibraryItemViewModel()
    {
    }

    public VideoLibraryItemViewModel(VideoLibraryItem entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Id = entity.Id;
        FilePath = entity.FilePath;
        FileName = entity.FileName;
        DurationSeconds = entity.DurationSeconds;
        ThumbnailCachePath = entity.ThumbnailCachePath;
        PlaybackCount = entity.PlaybackCount;
        IsMissing = entity.IsMissing;
        LastVerifiedAt = entity.LastVerifiedAt;
    }
}
