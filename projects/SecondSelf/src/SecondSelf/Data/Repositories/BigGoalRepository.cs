using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Data.Entities;

namespace SecondSelf.Data.Repositories;

/// <summary>
/// Репозиторий Больших целей и их промежуточных этапов (вех) на базе SQLite.
/// </summary>
public sealed class BigGoalRepository : IBigGoalRepository
{
    private const int MaxActiveGoalsLimit = 4;
    private readonly ISqliteConnectionFactory _connectionFactory;

    public BigGoalRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BigGoal>> GetActiveGoalsAsync(CancellationToken ct = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);

        // 1. Получаем все активные цели
        const string selectGoalsSql = @"
SELECT Id, Title, Motivation, TargetResult, TargetPeriod, IconKey, Status, CreatedAt
FROM BigGoals
WHERE Status = 'Active'
ORDER BY CreatedAt ASC;";

        var goals = new List<BigGoal>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = selectGoalsSql;
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                goals.Add(ReadBigGoal(reader));
            }
        }

        if (goals.Count == 0)
        {
            return goals;
        }

        // 2. Загружаем все этапы для активных целей
        const string selectMilestonesSql = @"
SELECT m.Id, m.GoalId, m.OrderIndex, m.Title, m.Status, m.CompletedAt
FROM Milestones m
JOIN BigGoals g ON m.GoalId = g.Id
WHERE g.Status = 'Active'
ORDER BY m.GoalId ASC, m.OrderIndex ASC;";

        var milestonesByGoal = new Dictionary<string, List<Milestone>>(StringComparer.OrdinalIgnoreCase);
        await using (var msCmd = connection.CreateCommand())
        {
            msCmd.CommandText = selectMilestonesSql;
            await using var msReader = await msCmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await msReader.ReadAsync(ct).ConfigureAwait(false))
            {
                var ms = ReadMilestone(msReader);
                if (!milestonesByGoal.TryGetValue(ms.GoalId, out var list))
                {
                    list = new List<Milestone>();
                    milestonesByGoal[ms.GoalId] = list;
                }
                list.Add(ms);
            }
        }

        foreach (var goal in goals)
        {
            if (milestonesByGoal.TryGetValue(goal.Id, out var msList))
            {
                goal.Milestones = msList;
            }
        }

        return goals;
    }

    /// <inheritdoc />
    public async Task<BigGoal?> GetGoalByIdAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await using var connection = await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);

        BigGoal? goal = null;
        const string selectGoalSql = @"
SELECT Id, Title, Motivation, TargetResult, TargetPeriod, IconKey, Status, CreatedAt
FROM BigGoals
WHERE Id = @Id;";

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = selectGoalSql;
            cmd.Parameters.AddWithValue("@Id", id);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                goal = ReadBigGoal(reader);
            }
        }

        if (goal is null)
        {
            return null;
        }

        const string selectMilestonesSql = @"
SELECT Id, GoalId, OrderIndex, Title, Status, CompletedAt
FROM Milestones
WHERE GoalId = @GoalId
ORDER BY OrderIndex ASC;";

        await using (var msCmd = connection.CreateCommand())
        {
            msCmd.CommandText = selectMilestonesSql;
            msCmd.Parameters.AddWithValue("@GoalId", id);

            await using var msReader = await msCmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await msReader.ReadAsync(ct).ConfigureAwait(false))
            {
                goal.Milestones.Add(ReadMilestone(msReader));
            }
        }

        return goal;
    }

    /// <inheritdoc />
    public async Task AddGoalAsync(BigGoal goal, IEnumerable<Milestone> initialMilestones, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(goal);

        if (string.IsNullOrWhiteSpace(goal.Id))
        {
            goal.Id = Guid.NewGuid().ToString();
        }

        if (string.IsNullOrWhiteSpace(goal.CreatedAt))
        {
            goal.CreatedAt = DateTime.UtcNow.ToString("O");
        }

        if (string.IsNullOrWhiteSpace(goal.Status))
        {
            goal.Status = "Active";
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        try
        {
            // Валидация лимита активных целей
            if (string.Equals(goal.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                await using var countCmd = connection.CreateCommand();
                countCmd.Transaction = transaction;
                countCmd.CommandText = "SELECT COUNT(*) FROM BigGoals WHERE Status = 'Active';";
                var count = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct).ConfigureAwait(false));

                if (count >= MaxActiveGoalsLimit)
                {
                    throw new InvalidOperationException("Превышен лимит активных целей: одновременно можно вести не более 4 Больших целей.");
                }
            }

            // Вставка BigGoal
            const string insertGoalSql = @"
INSERT INTO BigGoals (Id, Title, Motivation, TargetResult, TargetPeriod, IconKey, Status, CreatedAt)
VALUES (@Id, @Title, @Motivation, @TargetResult, @TargetPeriod, @IconKey, @Status, @CreatedAt);";

            await using (var insertGoalCmd = connection.CreateCommand())
            {
                insertGoalCmd.Transaction = transaction;
                insertGoalCmd.CommandText = insertGoalSql;
                insertGoalCmd.Parameters.AddWithValue("@Id", goal.Id);
                insertGoalCmd.Parameters.AddWithValue("@Title", goal.Title);
                insertGoalCmd.Parameters.AddWithValue("@Motivation", (object?)goal.Motivation ?? DBNull.Value);
                insertGoalCmd.Parameters.AddWithValue("@TargetResult", (object?)goal.TargetResult ?? DBNull.Value);
                insertGoalCmd.Parameters.AddWithValue("@TargetPeriod", (object?)goal.TargetPeriod ?? DBNull.Value);
                insertGoalCmd.Parameters.AddWithValue("@IconKey", string.IsNullOrWhiteSpace(goal.IconKey) ? "Compass" : goal.IconKey);
                insertGoalCmd.Parameters.AddWithValue("@Status", goal.Status);
                insertGoalCmd.Parameters.AddWithValue("@CreatedAt", goal.CreatedAt);

                await insertGoalCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            // Вставка начальных вех
            if (initialMilestones != null)
            {
                var addedList = new List<Milestone>();
                int defaultIndex = 1;
                foreach (var milestone in initialMilestones)
                {
                    if (string.IsNullOrWhiteSpace(milestone.Id))
                    {
                        milestone.Id = Guid.NewGuid().ToString();
                    }
                    milestone.GoalId = goal.Id;
                    if (milestone.OrderIndex <= 0)
                    {
                        milestone.OrderIndex = defaultIndex;
                    }
                    if (string.IsNullOrWhiteSpace(milestone.Status))
                    {
                        milestone.Status = "Upcoming";
                    }

                    const string insertMsSql = @"
INSERT INTO Milestones (Id, GoalId, OrderIndex, Title, Status, CompletedAt)
VALUES (@Id, @GoalId, @OrderIndex, @Title, @Status, @CompletedAt);";

                    await using (var insertMsCmd = connection.CreateCommand())
                    {
                        insertMsCmd.Transaction = transaction;
                        insertMsCmd.CommandText = insertMsSql;
                        insertMsCmd.Parameters.AddWithValue("@Id", milestone.Id);
                        insertMsCmd.Parameters.AddWithValue("@GoalId", milestone.GoalId);
                        insertMsCmd.Parameters.AddWithValue("@OrderIndex", milestone.OrderIndex);
                        insertMsCmd.Parameters.AddWithValue("@Title", milestone.Title);
                        insertMsCmd.Parameters.AddWithValue("@Status", milestone.Status);
                        insertMsCmd.Parameters.AddWithValue("@CompletedAt", (object?)milestone.CompletedAt ?? DBNull.Value);

                        await insertMsCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }

                    addedList.Add(milestone);
                    defaultIndex++;
                }

                goal.Milestones = addedList;
            }

            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdateGoalAsync(BigGoal goal, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentException.ThrowIfNullOrWhiteSpace(goal.Id);

        await using var connection = await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        try
        {
            if (string.Equals(goal.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                await using var checkCmd = connection.CreateCommand();
                checkCmd.Transaction = transaction;
                checkCmd.CommandText = "SELECT Status FROM BigGoals WHERE Id = @Id;";
                checkCmd.Parameters.AddWithValue("@Id", goal.Id);
                var currentStatus = (string?)await checkCmd.ExecuteScalarAsync(ct).ConfigureAwait(false);

                if (!string.Equals(currentStatus, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    await using var countCmd = connection.CreateCommand();
                    countCmd.Transaction = transaction;
                    countCmd.CommandText = "SELECT COUNT(*) FROM BigGoals WHERE Status = 'Active' AND Id != @Id;";
                    countCmd.Parameters.AddWithValue("@Id", goal.Id);
                    var count = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct).ConfigureAwait(false));

                    if (count >= MaxActiveGoalsLimit)
                    {
                        throw new InvalidOperationException("Превышен лимит активных целей: одновременно можно вести не более 4 Больших целей.");
                    }
                }
            }

            const string updateSql = @"
UPDATE BigGoals
SET Title = @Title,
    Motivation = @Motivation,
    TargetResult = @TargetResult,
    TargetPeriod = @TargetPeriod,
    IconKey = @IconKey,
    Status = @Status
WHERE Id = @Id;";

            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = updateSql;
            cmd.Parameters.AddWithValue("@Id", goal.Id);
            cmd.Parameters.AddWithValue("@Title", goal.Title);
            cmd.Parameters.AddWithValue("@Motivation", (object?)goal.Motivation ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TargetResult", (object?)goal.TargetResult ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TargetPeriod", (object?)goal.TargetPeriod ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IconKey", string.IsNullOrWhiteSpace(goal.IconKey) ? "Compass" : goal.IconKey);
            cmd.Parameters.AddWithValue("@Status", goal.Status);

            var affected = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            if (affected == 0)
            {
                throw new KeyNotFoundException($"Большая цель с Id '{goal.Id}' не найдена.");
            }

            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task AddMilestoneAsync(Milestone milestone, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(milestone);
        ArgumentException.ThrowIfNullOrWhiteSpace(milestone.GoalId);

        if (string.IsNullOrWhiteSpace(milestone.Id))
        {
            milestone.Id = Guid.NewGuid().ToString();
        }

        if (string.IsNullOrWhiteSpace(milestone.Status))
        {
            milestone.Status = "Upcoming";
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        const string insertSql = @"
INSERT INTO Milestones (Id, GoalId, OrderIndex, Title, Status, CompletedAt)
VALUES (@Id, @GoalId, @OrderIndex, @Title, @Status, @CompletedAt);";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = insertSql;
        cmd.Parameters.AddWithValue("@Id", milestone.Id);
        cmd.Parameters.AddWithValue("@GoalId", milestone.GoalId);
        cmd.Parameters.AddWithValue("@OrderIndex", milestone.OrderIndex);
        cmd.Parameters.AddWithValue("@Title", milestone.Title);
        cmd.Parameters.AddWithValue("@Status", milestone.Status);
        cmd.Parameters.AddWithValue("@CompletedAt", (object?)milestone.CompletedAt ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateMilestoneAsync(Milestone milestone, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(milestone);
        ArgumentException.ThrowIfNullOrWhiteSpace(milestone.Id);

        await using var connection = await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        const string updateSql = @"
UPDATE Milestones
SET OrderIndex = @OrderIndex,
    Title = @Title,
    Status = @Status,
    CompletedAt = @CompletedAt
WHERE Id = @Id;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = updateSql;
        cmd.Parameters.AddWithValue("@Id", milestone.Id);
        cmd.Parameters.AddWithValue("@OrderIndex", milestone.OrderIndex);
        cmd.Parameters.AddWithValue("@Title", milestone.Title);
        cmd.Parameters.AddWithValue("@Status", milestone.Status);
        cmd.Parameters.AddWithValue("@CompletedAt", (object?)milestone.CompletedAt ?? DBNull.Value);

        var affected = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        if (affected == 0)
        {
            throw new KeyNotFoundException($"Этап с Id '{milestone.Id}' не найден.");
        }
    }

    /// <inheritdoc />
    public async Task CompleteMilestoneAsync(string milestoneId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(milestoneId);

        var nowIso = DateTime.UtcNow.ToString("O");
        await using var connection = await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        const string sql = @"
UPDATE Milestones
SET Status = 'Completed',
    CompletedAt = @CompletedAt
WHERE Id = @Id;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", milestoneId);
        cmd.Parameters.AddWithValue("@CompletedAt", nowIso);

        var affected = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        if (affected == 0)
        {
            throw new KeyNotFoundException($"Этап с Id '{milestoneId}' не найден.");
        }
    }

    /// <inheritdoc />
    public async Task ArchiveGoalAsync(string goalId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goalId);

        await using var connection = await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        const string sql = @"
UPDATE BigGoals
SET Status = 'Archived'
WHERE Id = @Id;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", goalId);

        var affected = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        if (affected == 0)
        {
            throw new KeyNotFoundException($"Большая цель с Id '{goalId}' не найдена.");
        }
    }

    /// <inheritdoc />
    public async Task DeleteGoalAsync(string goalId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goalId);

        await using var connection = await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        const string sql = "DELETE FROM BigGoals WHERE Id = @Id;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", goalId);

        var affected = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        if (affected == 0)
        {
            throw new KeyNotFoundException($"Большая цель с Id '{goalId}' не найдена.");
        }
    }

    private static BigGoal ReadBigGoal(DbDataReader reader)
    {
        return new BigGoal
        {
            Id = reader.GetString(reader.GetOrdinal("Id")),
            Title = reader.GetString(reader.GetOrdinal("Title")),
            Motivation = reader.IsDBNull(reader.GetOrdinal("Motivation")) ? null : reader.GetString(reader.GetOrdinal("Motivation")),
            TargetResult = reader.IsDBNull(reader.GetOrdinal("TargetResult")) ? null : reader.GetString(reader.GetOrdinal("TargetResult")),
            TargetPeriod = reader.IsDBNull(reader.GetOrdinal("TargetPeriod")) ? null : reader.GetString(reader.GetOrdinal("TargetPeriod")),
            IconKey = reader.GetString(reader.GetOrdinal("IconKey")),
            Status = reader.GetString(reader.GetOrdinal("Status")),
            CreatedAt = reader.GetString(reader.GetOrdinal("CreatedAt"))
        };
    }

    private static Milestone ReadMilestone(DbDataReader reader)
    {
        return new Milestone
        {
            Id = reader.GetString(reader.GetOrdinal("Id")),
            GoalId = reader.GetString(reader.GetOrdinal("GoalId")),
            OrderIndex = reader.GetInt32(reader.GetOrdinal("OrderIndex")),
            Title = reader.GetString(reader.GetOrdinal("Title")),
            Status = reader.GetString(reader.GetOrdinal("Status")),
            CompletedAt = reader.IsDBNull(reader.GetOrdinal("CompletedAt")) ? null : reader.GetString(reader.GetOrdinal("CompletedAt"))
        };
    }
}
