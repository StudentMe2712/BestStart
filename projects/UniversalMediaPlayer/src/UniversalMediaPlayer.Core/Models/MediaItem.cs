namespace UniversalMediaPlayer.Core.Models;

public record MediaItem
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required string Extension { get; init; }
    public long FileSizeBytes { get; init; }

    public static MediaItem FromFilePath(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        return new MediaItem
        {
            FilePath = fileInfo.FullName,
            FileName = fileInfo.Name,
            Extension = fileInfo.Extension.TrimStart('.').ToLowerInvariant(),
            FileSizeBytes = fileInfo.Exists ? fileInfo.Length : 0
        };
    }
}
