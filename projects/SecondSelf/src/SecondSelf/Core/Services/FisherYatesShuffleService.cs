using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Data;
using SecondSelf.Data.Entities;

namespace SecondSelf.Core.Services;

/// <summary>
/// Реализация циклической очереди воспроизведения без повторений на основе алгоритма Фишера — Йейтса.
/// </summary>
public sealed class FisherYatesShuffleService : IFisherYatesShuffleService
{
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly IVideoLibraryService _videoLibraryService;
    private readonly Random _random;

    public FisherYatesShuffleService(
        ISqliteConnectionFactory connectionFactory,
        IVideoLibraryService videoLibraryService,
        Random? random = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _videoLibraryService = videoLibraryService ?? throw new ArgumentNullException(nameof(videoLibraryService));
        _random = random ?? Random.Shared;
    }

    public async Task<VideoLibraryItem?> GetNextVideoInCycleAsync(CancellationToken cancellationToken = default)
    {
        int attempts = 0;
        const int maxAttempts = 1000;

        while (attempts++ < maxAttempts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

            // 1. Ищем следующий непроигранный ролик в очереди
            var nextItem = await GetNextUnplayedItemAsync(connection, cancellationToken).ConfigureAwait(false);

            if (nextItem == null)
            {
                // Очередь пуста или исчерпана — формируем новый цикл
                bool cycleCreated = await GenerateNewCycleAsync(connection, cancellationToken).ConfigureAwait(false);
                if (!cycleCreated)
                {
                    return null;
                }

                nextItem = await GetNextUnplayedItemAsync(connection, cancellationToken).ConfigureAwait(false);
                if (nextItem == null)
                {
                    return null;
                }
            }

            // 2. Получаем видео из VideoLibrary
            var video = await _videoLibraryService.GetVideoByIdAsync(nextItem.VideoId, cancellationToken).ConfigureAwait(false);

            // 3. Проверяем наличие файла на диске
            if (video == null || string.IsNullOrWhiteSpace(video.FilePath) || !File.Exists(video.FilePath))
            {
                if (video != null)
                {
                    await _videoLibraryService.UpdateMissingStatusAsync(video.Id, true, cancellationToken).ConfigureAwait(false);
                }

                // Помечаем элемент очереди как проигранный/пропущенный, чтобы не зацикливаться на битом файле
                await MarkItemPlayedAsync(connection, nextItem.QueueOrder, cancellationToken).ConfigureAwait(false);
                continue;
            }

            // 4. Файл доступен — помечаем как проигранный в очереди и возвращаем
            await MarkItemPlayedAsync(connection, nextItem.QueueOrder, cancellationToken).ConfigureAwait(false);
            return video;
        }

        return null;
    }

    public async Task<IReadOnlyList<VideoPlaybackQueueItem>> GetCurrentQueueAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        const string sql = @"
SELECT QueueOrder, VideoId, IsPlayed, PlayedAt
FROM VideoPlaybackQueue
ORDER BY QueueOrder ASC;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        var list = new List<VideoPlaybackQueueItem>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new VideoPlaybackQueueItem
            {
                QueueOrder = reader.GetInt64(0),
                VideoId = reader.GetString(1),
                IsPlayed = reader.GetInt32(2) == 1,
                PlayedAt = reader.IsDBNull(3) ? null : reader.GetString(3)
            });
        }

        return list;
    }

    public async Task ResetQueueAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM VideoPlaybackQueue;";
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> GenerateNewCycleAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        // 1. Ищем последний проигранный ролик из предыдущего цикла
        string? lastPlayedVideoId = null;
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT VideoId FROM VideoPlaybackQueue WHERE IsPlayed = 1 ORDER BY QueueOrder DESC LIMIT 1;";
            var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (result != null && result != DBNull.Value)
            {
                lastPlayedVideoId = result.ToString();
            }
        }

        // 2. Очищаем старые проигранные записи очереди
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM VideoPlaybackQueue WHERE IsPlayed = 1;";
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // 3. Получаем все видео из библиотеки и фильтруем доступные на диске
        var allVideos = await _videoLibraryService.GetAllVideosAsync(cancellationToken).ConfigureAwait(false);
        var validVideos = new List<VideoLibraryItem>();

        foreach (var v in allVideos)
        {
            if (!v.IsMissing && !string.IsNullOrWhiteSpace(v.FilePath) && File.Exists(v.FilePath))
            {
                validVideos.Add(v);
            }
            else if (!v.IsMissing)
            {
                await _videoLibraryService.UpdateMissingStatusAsync(v.Id, true, cancellationToken).ConfigureAwait(false);
            }
        }

        if (validVideos.Count == 0)
        {
            return false;
        }

        if (validVideos.Count == 1)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO VideoPlaybackQueue (VideoId, IsPlayed) VALUES (@VideoId, 0);";
            cmd.Parameters.AddWithValue("@VideoId", validVideos[0].Id);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        // Перемешивание по алгоритму Фишера — Йейтса
        for (int i = validVideos.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (validVideos[i], validVideos[j]) = (validVideos[j], validVideos[i]);
        }

        // Если первый ролик совпадает с последним проигранным из прошлого цикла,
        // меняем его местами, чтобы исключить показ одного и того же ролика подряд
        if (!string.IsNullOrEmpty(lastPlayedVideoId) && validVideos[0].Id == lastPlayedVideoId && validVideos.Count > 1)
        {
            int swapIndex = validVideos.Count == 2 ? 1 : _random.Next(1, validVideos.Count);
            (validVideos[0], validVideos[swapIndex]) = (validVideos[swapIndex], validVideos[0]);
        }

        // Сохраняем новый цикл в очередь
        await using (var tx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                foreach (var video in validVideos)
                {
                    await using var cmd = connection.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = "INSERT INTO VideoPlaybackQueue (VideoId, IsPlayed) VALUES (@VideoId, 0);";
                    cmd.Parameters.AddWithValue("@VideoId", video.Id);
                    await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        return true;
    }

    private static async Task<VideoPlaybackQueueItem?> GetNextUnplayedItemAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT QueueOrder, VideoId, IsPlayed, PlayedAt
FROM VideoPlaybackQueue
WHERE IsPlayed = 0
ORDER BY QueueOrder ASC
LIMIT 1;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new VideoPlaybackQueueItem
            {
                QueueOrder = reader.GetInt64(0),
                VideoId = reader.GetString(1),
                IsPlayed = reader.GetInt32(2) == 1,
                PlayedAt = reader.IsDBNull(3) ? null : reader.GetString(3)
            };
        }

        return null;
    }

    private static async Task MarkItemPlayedAsync(SqliteConnection connection, long queueOrder, CancellationToken cancellationToken)
    {
        const string sql = @"
UPDATE VideoPlaybackQueue
SET IsPlayed = 1,
    PlayedAt = @PlayedAt
WHERE QueueOrder = @QueueOrder;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@PlayedAt", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@QueueOrder", queueOrder);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
