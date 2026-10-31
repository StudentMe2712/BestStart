using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Data.Entities;

namespace SecondSelf.Data.Repositories;

/// <summary>
/// Репозиторий недельных целей на базе SQLite.
/// </summary>
public sealed class WeeklyGoalRepository : IWeeklyGoalRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public WeeklyGoalRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task<WeeklyGoal?> GetGoalForWeekAsync(int isoYear, int isoWeek, CancellationToken ct = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        const string sql = @"
SELECT Id, IsoYear, IsoWeek, BelongsToMonth, MilestoneId, Title, Status, LockedAt, CompletedAt, CancellationReason, CreatedAt
FROM WeeklyGoals
WHERE IsoYear = @IsoYear AND IsoWeek = @IsoWeek
LIMIT 1;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@IsoYear", isoYear);
        cmd.Parameters.AddWithValue("@IsoWeek", isoWeek);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return ReadWeeklyGoal(reader);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WeeklyGoal>> GetGoalsForMonthAsync(string yearMonth, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yearMonth);

        await using var connection = await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        const string sql = @"
SELECT Id, IsoYear, IsoWeek, BelongsToMonth, MilestoneId, Title, Status, LockedAt, CompletedAt, CancellationReason, CreatedAt
FROM WeeklyGoals
WHERE BelongsToMonth = @BelongsToMonth
ORDER BY IsoWeek ASC;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@BelongsToMonth", yearMonth);

        var list = new List<WeeklyGoal>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(ReadWeeklyGoal(reader));
        }

        return list;
    }

    /// <inheritdoc />
    public async Task<WeeklyGoal?> GetGoalByIdAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await using var connection = await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        const string sql = @"
SELECT Id, IsoYear, IsoWeek, BelongsToMonth, MilestoneId, Title, Status, LockedAt, CompletedAt, CancellationReason, CreatedAt
FROM WeeklyGoals
WHERE Id = @Id
LIMIT 1;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", id);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return ReadWeeklyGoal(reader);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task AddOrUpdateGoalAsync(WeeklyGoal goal, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(goal);

        if (string.IsNullOrWhiteSpace(goal.Id))
        {
            goal.Id = Guid.NewGuid().ToString();
        }

        if (string.IsNullOrWhiteSpace(goal.BelongsToMonth))
        {
            var thursday = ISOWeek.ToDateTime(goal.IsoYear, goal.IsoWeek, DayOfWeek.Thursday);
            goal.BelongsToMonth = $"{thursday.Year:D4}-{thursday.Month:D2}";
        }

        if (string.IsNullOrWhiteSpace(goal.Status))
        {
            goal.Status = "Active";
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);

        // Проверяем, существует ли уже запись с таким Id
        bool existsById = false;
        await using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM WeeklyGoals WHERE Id = @Id;";
            checkCmd.Parameters.AddWithValue("@Id", goal.Id);
            existsById = Convert.ToInt32(await checkCmd.ExecuteScalarAsync(ct).ConfigureAwait(false)) > 0;
        }

        if (existsById)
        {
            const string updateSql = @"
UPDATE WeeklyGoals
SET IsoYear = @IsoYear,
    IsoWeek = @IsoWeek,
    BelongsToMonth = @BelongsToMonth,
    MilestoneId = @MilestoneId,
    Title = @Title,
    Status = @Status,
    LockedAt = @LockedAt,
    CompletedAt = @CompletedAt,
    CancellationReason = @CancellationReason
WHERE Id = @Id;";

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = updateSql;
            AddWeeklyGoalParameters(cmd, goal);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        else
        {
            const string insertSql = @"
INSERT INTO WeeklyGoals (Id, IsoYear, IsoWeek, BelongsToMonth, MilestoneId, Title, Status, LockedAt, CompletedAt, CancellationReason, CreatedAt)
VALUES (@Id, @IsoYear, @IsoWeek, @BelongsToMonth, @MilestoneId, @Title, @Status, @LockedAt, @CompletedAt, @CancellationReason, @CreatedAt)
ON CONFLICT(IsoYear, IsoWeek) DO UPDATE SET
    BelongsToMonth = excluded.BelongsToMonth,
    MilestoneId = excluded.MilestoneId,
    Title = excluded.Title,
    Status = excluded.Status,
    LockedAt = excluded.LockedAt,
    CompletedAt = excluded.CompletedAt,
    CancellationReason = excluded.CancellationReason,
    CreatedAt = COALESCE(WeeklyGoals.CreatedAt, excluded.CreatedAt);";

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = insertSql;
            AddWeeklyGoalParameters(cmd, goal);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task LockGoalAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var nowIso = DateTime.UtcNow.ToString("O");
        await using var connection = await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        const string sql = @"
UPDATE WeeklyGoals
SET Status = 'Locked',
    LockedAt = @LockedAt
WHERE Id = @Id;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@LockedAt", nowIso);

        var affected = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        if (affected == 0)
        {
            throw new KeyNotFoundException($"Недельная цель с Id '{id}' не найдена.");
        }
    }

    /// <inheritdoc />
    public async Task CompleteGoalAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var nowIso = DateTime.UtcNow.ToString("O");
        await using var connection = await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        const string sql = @"
UPDATE WeeklyGoals
SET Status = 'Completed',
    CompletedAt = @CompletedAt
WHERE Id = @Id;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@CompletedAt", nowIso);

        var affected = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        if (affected == 0)
        {
            throw new KeyNotFoundException($"Недельная цель с Id '{id}' не найдена.");
        }
    }

    /// <inheritdoc />
    public async Task CancelGoalAsync(string id, string reason, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await using var connection = await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        const string sql = @"
UPDATE WeeklyGoals
SET Status = 'Cancelled',
    CancellationReason = @Reason
WHERE Id = @Id;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Reason", (object?)reason ?? DBNull.Value);

        var affected = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        if (affected == 0)
        {
            throw new KeyNotFoundException($"Недельная цель с Id '{id}' не найдена.");
        }
    }

    /// <inheritdoc />
    public async Task<WeeklyGoal> PostponeGoalAsync(string id, string reason, int nextYear, int nextWeek, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await using var connection = await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        try
        {
            // 1. Находим текущую цель
            WeeklyGoal? currentGoal = null;
            const string selectSql = @"
SELECT Id, IsoYear, IsoWeek, BelongsToMonth, MilestoneId, Title, Status, LockedAt, CompletedAt, CancellationReason, CreatedAt
FROM WeeklyGoals
WHERE Id = @Id;";

            await using (var selectCmd = connection.CreateCommand())
            {
                selectCmd.Transaction = transaction;
                selectCmd.CommandText = selectSql;
                selectCmd.Parameters.AddWithValue("@Id", id);

                await using var reader = await selectCmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
                if (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    currentGoal = ReadWeeklyGoal(reader);
                }
            }

            if (currentGoal is null)
            {
                throw new KeyNotFoundException($"Недельная цель с Id '{id}' не найдена.");
            }

            // 2. Помечаем текущую цель как перенесённую ('Postponed') с указанием причины
            const string postponeSql = @"
UPDATE WeeklyGoals
SET Status = 'Postponed',
    CancellationReason = @Reason
WHERE Id = @Id;";

            await using (var updateCmd = connection.CreateCommand())
            {
                updateCmd.Transaction = transaction;
                updateCmd.CommandText = postponeSql;
                updateCmd.Parameters.AddWithValue("@Id", id);
                updateCmd.Parameters.AddWithValue("@Reason", (object?)reason ?? DBNull.Value);

                await updateCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            // 3. Проверяем, существует ли уже цель на целевую неделю
            WeeklyGoal? existingNextGoal = null;
            const string selectNextSql = @"
SELECT Id, IsoYear, IsoWeek, BelongsToMonth, MilestoneId, Title, Status, LockedAt, CompletedAt, CancellationReason, CreatedAt
FROM WeeklyGoals
WHERE IsoYear = @IsoYear AND IsoWeek = @IsoWeek;";

            await using (var checkNextCmd = connection.CreateCommand())
            {
                checkNextCmd.Transaction = transaction;
                checkNextCmd.CommandText = selectNextSql;
                checkNextCmd.Parameters.AddWithValue("@IsoYear", nextYear);
                checkNextCmd.Parameters.AddWithValue("@IsoWeek", nextWeek);

                await using var reader = await checkNextCmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
                if (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    existingNextGoal = ReadWeeklyGoal(reader);
                }
            }

            WeeklyGoal resultGoal;
            if (existingNextGoal is not null)
            {
                resultGoal = existingNextGoal;
            }
            else
            {
                var thursday = ISOWeek.ToDateTime(nextYear, nextWeek, DayOfWeek.Thursday);
                var belongsToMonth = $"{thursday.Year:D4}-{thursday.Month:D2}";

                resultGoal = new WeeklyGoal
                {
                    Id = Guid.NewGuid().ToString(),
                    IsoYear = nextYear,
                    IsoWeek = nextWeek,
                    BelongsToMonth = belongsToMonth,
                    MilestoneId = currentGoal.MilestoneId,
                    Title = currentGoal.Title,
                    Status = "Active"
                };

                const string insertNextSql = @"
INSERT INTO WeeklyGoals (Id, IsoYear, IsoWeek, BelongsToMonth, MilestoneId, Title, Status, LockedAt, CompletedAt, CancellationReason, CreatedAt)
VALUES (@Id, @IsoYear, @IsoWeek, @BelongsToMonth, @MilestoneId, @Title, @Status, @LockedAt, @CompletedAt, @CancellationReason, @CreatedAt);";

                await using (var insertCmd = connection.CreateCommand())
                {
                    insertCmd.Transaction = transaction;
                    insertCmd.CommandText = insertNextSql;
                    AddWeeklyGoalParameters(insertCmd, resultGoal);
                    await insertCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
            }

            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return resultGoal;
        }
        catch
        {
            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
    }

    private static void AddWeeklyGoalParameters(SqliteCommand cmd, WeeklyGoal goal)
    {
        cmd.Parameters.AddWithValue("@Id", goal.Id);
        cmd.Parameters.AddWithValue("@IsoYear", goal.IsoYear);
        cmd.Parameters.AddWithValue("@IsoWeek", goal.IsoWeek);
        cmd.Parameters.AddWithValue("@BelongsToMonth", goal.BelongsToMonth);
        cmd.Parameters.AddWithValue("@MilestoneId", (object?)goal.MilestoneId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Title", goal.Title);
        cmd.Parameters.AddWithValue("@Status", goal.Status);
        cmd.Parameters.AddWithValue("@LockedAt", (object?)goal.LockedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CompletedAt", (object?)goal.CompletedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CancellationReason", (object?)goal.CancellationReason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt", (object?)goal.CreatedAt ?? DateTime.UtcNow.ToString("O"));
    }

    private static WeeklyGoal ReadWeeklyGoal(DbDataReader reader)
    {
        int createdAtOrdinal = -1;
        try { createdAtOrdinal = reader.GetOrdinal("CreatedAt"); } catch { }

        return new WeeklyGoal
        {
            Id = reader.GetString(reader.GetOrdinal("Id")),
            IsoYear = reader.GetInt32(reader.GetOrdinal("IsoYear")),
            IsoWeek = reader.GetInt32(reader.GetOrdinal("IsoWeek")),
            BelongsToMonth = reader.GetString(reader.GetOrdinal("BelongsToMonth")),
            MilestoneId = reader.IsDBNull(reader.GetOrdinal("MilestoneId")) ? null : reader.GetString(reader.GetOrdinal("MilestoneId")),
            Title = reader.GetString(reader.GetOrdinal("Title")),
            Status = reader.GetString(reader.GetOrdinal("Status")),
            LockedAt = reader.IsDBNull(reader.GetOrdinal("LockedAt")) ? null : reader.GetString(reader.GetOrdinal("LockedAt")),
            CompletedAt = reader.IsDBNull(reader.GetOrdinal("CompletedAt")) ? null : reader.GetString(reader.GetOrdinal("CompletedAt")),
            CancellationReason = reader.IsDBNull(reader.GetOrdinal("CancellationReason")) ? null : reader.GetString(reader.GetOrdinal("CancellationReason")),
            CreatedAt = (createdAtOrdinal >= 0 && !reader.IsDBNull(createdAtOrdinal)) ? reader.GetString(createdAtOrdinal) : null
        };
    }
}
