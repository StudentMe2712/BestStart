using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Data.Entities;

namespace SecondSelf.Data.Repositories;

/// <summary>
/// Репозиторий ежедневных ритуалов на базе SQLite.
/// </summary>
public sealed class DailyRitualRepository : IDailyRitualRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public DailyRitualRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<DailyRitualLog?> GetDailyRitualLogAsync(string logicalDate, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalDate);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        const string sql = @"
SELECT LogicalDate, VideoId, LastPositionMs, IsCompleted, CompletedAt
FROM DailyRitualLogs
WHERE LogicalDate = @LogicalDate;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@LogicalDate", logicalDate);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return ReadDailyRitualLog(reader);
        }

        return null;
    }

    public async Task UpsertDailyRitualLogAsync(DailyRitualLog log, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentException.ThrowIfNullOrWhiteSpace(log.LogicalDate);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        const string sql = @"
INSERT INTO DailyRitualLogs (LogicalDate, VideoId, LastPositionMs, IsCompleted, CompletedAt)
VALUES (@LogicalDate, @VideoId, @LastPositionMs, @IsCompleted, @CompletedAt)
ON CONFLICT(LogicalDate) DO UPDATE SET
    VideoId = excluded.VideoId,
    LastPositionMs = excluded.LastPositionMs,
    IsCompleted = excluded.IsCompleted,
    CompletedAt = excluded.CompletedAt;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@LogicalDate", log.LogicalDate);
        cmd.Parameters.AddWithValue("@VideoId", log.VideoId);
        cmd.Parameters.AddWithValue("@LastPositionMs", log.LastPositionMs);
        cmd.Parameters.AddWithValue("@IsCompleted", log.IsCompleted ? 1 : 0);
        cmd.Parameters.AddWithValue("@CompletedAt", (object?)log.CompletedAt ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkCompletedAsync(string logicalDate, string completedAtIso, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalDate);
        ArgumentException.ThrowIfNullOrWhiteSpace(completedAtIso);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        const string sql = @"
UPDATE DailyRitualLogs
SET IsCompleted = 1,
    CompletedAt = @CompletedAt
WHERE LogicalDate = @LogicalDate;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@LogicalDate", logicalDate);
        cmd.Parameters.AddWithValue("@CompletedAt", completedAtIso);

        int rows = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (rows == 0)
        {
            throw new KeyNotFoundException($"Запись ежедневного ритуала для логической даты '{logicalDate}' не найдена.");
        }
    }

    public async Task<IReadOnlyList<DailyRitualLog>> GetLogsForMonthAsync(string yearMonth, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yearMonth);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        const string sql = @"
SELECT LogicalDate, VideoId, LastPositionMs, IsCompleted, CompletedAt
FROM DailyRitualLogs
WHERE LogicalDate LIKE @Pattern
ORDER BY LogicalDate ASC;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Pattern", yearMonth + "%");

        var list = new List<DailyRitualLog>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ReadDailyRitualLog(reader));
        }

        return list;
    }

    public async Task UpdateLastPositionAsync(string logicalDate, int lastPositionMs, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalDate);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        const string sql = @"
UPDATE DailyRitualLogs
SET LastPositionMs = @LastPositionMs
WHERE LogicalDate = @LogicalDate;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@LogicalDate", logicalDate);
        cmd.Parameters.AddWithValue("@LastPositionMs", lastPositionMs);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static DailyRitualLog ReadDailyRitualLog(DbDataReader reader)
    {
        return new DailyRitualLog
        {
            LogicalDate = reader.GetString(0),
            VideoId = reader.GetString(1),
            LastPositionMs = reader.GetInt32(2),
            IsCompleted = reader.GetInt32(3) == 1,
            CompletedAt = reader.IsDBNull(4) ? null : reader.GetString(4)
        };
    }
}
