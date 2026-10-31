using System;

namespace SecondSelf.Core.Models;

/// <summary>
/// Метаданные резервной копии базы данных SQLite.
/// </summary>
public sealed record BackupFileInfo
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required long FileSizeBytes { get; init; }
}
