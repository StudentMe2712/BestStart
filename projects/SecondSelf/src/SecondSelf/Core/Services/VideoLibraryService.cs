using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Data;
using SecondSelf.Data.Entities;

namespace SecondSelf.Core.Services;

/// <summary>
/// Реализация сканера и менеджера локальной библиотеки видео Daily Reset.
/// </summary>
public sealed class VideoLibraryService : IVideoLibraryService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4",
        ".mkv",
        ".webm",
        ".mov"
    };

    private readonly ISqliteConnectionFactory _connectionFactory;

    public string VideosDirectory { get; }

    public VideoLibraryService(ISqliteConnectionFactory connectionFactory, string? videosDirectory = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

        if (!string.IsNullOrWhiteSpace(videosDirectory))
        {
            VideosDirectory = Path.GetFullPath(videosDirectory);
        }
        else
        {
            VideosDirectory = Path.Combine(AppContext.BaseDirectory, "Videos");
        }
    }

    public async Task<IReadOnlyList<VideoLibraryItem>> ScanLibraryAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(VideosDirectory))
        {
            Directory.CreateDirectory(VideosDirectory);
        }

        var diskFiles = Directory.GetFiles(VideosDirectory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)))
            .Select(Path.GetFullPath)
            .ToDictionary(f => f, Path.GetFileName, StringComparer.OrdinalIgnoreCase);

        var existingVideos = await GetAllVideosAsync(cancellationToken).ConfigureAwait(false);
        var existingByPath = new Dictionary<string, VideoLibraryItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in existingVideos)
        {
            existingByPath[Path.GetFullPath(v.FilePath)] = v;
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        string nowIso = DateTime.UtcNow.ToString("O");

        // 1. Проверяем существующие записи на наличие на диске
        foreach (var existing in existingVideos)
        {
            string fullPath = Path.GetFullPath(existing.FilePath);
            bool fileExistsOnDisk = File.Exists(fullPath);

            if (fileExistsOnDisk && existing.IsMissing)
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = "UPDATE VideoLibrary SET IsMissing = 0, LastVerifiedAt = @Now WHERE Id = @Id;";
                cmd.Parameters.AddWithValue("@Now", nowIso);
                cmd.Parameters.AddWithValue("@Id", existing.Id);
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                existing.IsMissing = false;
            }
            else if (!fileExistsOnDisk && !existing.IsMissing)
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = "UPDATE VideoLibrary SET IsMissing = 1, LastVerifiedAt = @Now WHERE Id = @Id;";
                cmd.Parameters.AddWithValue("@Now", nowIso);
                cmd.Parameters.AddWithValue("@Id", existing.Id);
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                existing.IsMissing = true;
            }
        }

        // 2. Добавляем новые файлы, которых еще нет в таблице
        foreach (var (filePath, fileName) in diskFiles)
        {
            if (!existingByPath.ContainsKey(filePath))
            {
                var newItem = new VideoLibraryItem
                {
                    Id = Guid.NewGuid().ToString(),
                    FilePath = filePath,
                    FileName = fileName ?? Path.GetFileName(filePath) ?? string.Empty,
                    DurationSeconds = 0,
                    ThumbnailCachePath = null,
                    PlaybackCount = 0,
                    IsMissing = false,
                    LastVerifiedAt = nowIso
                };

                await using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
INSERT INTO VideoLibrary (Id, FilePath, FileName, DurationSeconds, ThumbnailCachePath, PlaybackCount, IsMissing, LastVerifiedAt)
VALUES (@Id, @FilePath, @FileName, @DurationSeconds, @ThumbnailCachePath, @PlaybackCount, @IsMissing, @LastVerifiedAt);";
                cmd.Parameters.AddWithValue("@Id", newItem.Id);
                cmd.Parameters.AddWithValue("@FilePath", newItem.FilePath);
                cmd.Parameters.AddWithValue("@FileName", newItem.FileName);
                cmd.Parameters.AddWithValue("@DurationSeconds", newItem.DurationSeconds);
                cmd.Parameters.AddWithValue("@ThumbnailCachePath", (object?)newItem.ThumbnailCachePath ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PlaybackCount", newItem.PlaybackCount);
                cmd.Parameters.AddWithValue("@IsMissing", newItem.IsMissing ? 1 : 0);
                cmd.Parameters.AddWithValue("@LastVerifiedAt", newItem.LastVerifiedAt);

                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        return await GetAllVideosAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<VideoLibraryItem?> GetVideoByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        const string sql = @"
SELECT Id, FilePath, FileName, DurationSeconds, ThumbnailCachePath, PlaybackCount, IsMissing, LastVerifiedAt
FROM VideoLibrary
WHERE Id = @Id;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", id);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return ReadVideoLibraryItem(reader);
        }

        return null;
    }

    public async Task<IReadOnlyList<VideoLibraryItem>> GetAllVideosAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        const string sql = @"
SELECT Id, FilePath, FileName, DurationSeconds, ThumbnailCachePath, PlaybackCount, IsMissing, LastVerifiedAt
FROM VideoLibrary
ORDER BY FileName ASC;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        var list = new List<VideoLibraryItem>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ReadVideoLibraryItem(reader));
        }

        return list;
    }

    public async Task UpdateMissingStatusAsync(string id, bool isMissing, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        const string sql = @"
UPDATE VideoLibrary
SET IsMissing = @IsMissing,
    LastVerifiedAt = @Now
WHERE Id = @Id;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@IsMissing", isMissing ? 1 : 0);
        cmd.Parameters.AddWithValue("@Now", DateTime.UtcNow.ToString("O"));

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static VideoLibraryItem ReadVideoLibraryItem(DbDataReader reader)
    {
        return new VideoLibraryItem
        {
            Id = reader.GetString(0),
            FilePath = reader.GetString(1),
            FileName = reader.GetString(2),
            DurationSeconds = reader.GetDouble(3),
            ThumbnailCachePath = reader.IsDBNull(4) ? null : reader.GetString(4),
            PlaybackCount = reader.GetInt32(5),
            IsMissing = reader.GetInt32(6) == 1,
            LastVerifiedAt = reader.GetString(7)
        };
    }
}
