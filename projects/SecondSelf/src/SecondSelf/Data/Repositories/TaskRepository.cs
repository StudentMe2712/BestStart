using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Data.Entities;

namespace SecondSelf.Data.Repositories;

/// <summary>
/// Репозиторий задач на базе локальной SQLite БД.
/// </summary>
public sealed class TaskRepository : ITaskRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public TaskRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IReadOnlyList<ActiveTask>> GetActiveTasksAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        const string query = @"
SELECT Id, Title, CreatedAt, OrderIndex, PostponedDays
FROM ActiveTasks
ORDER BY OrderIndex ASC;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = query;

        var list = new List<ActiveTask>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ReadActiveTask(reader));
        }

        return list;
    }

    public async Task AddActiveTaskAsync(ActiveTask task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (string.IsNullOrWhiteSpace(task.Id))
        {
            task.Id = Guid.NewGuid().ToString();
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        const string sql = @"
INSERT INTO ActiveTasks (Id, Title, CreatedAt, OrderIndex, PostponedDays)
VALUES (@Id, @Title, @CreatedAt, @OrderIndex, @PostponedDays);";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", task.Id);
        cmd.Parameters.AddWithValue("@Title", task.Title);
        cmd.Parameters.AddWithValue("@CreatedAt", task.CreatedAt);
        cmd.Parameters.AddWithValue("@OrderIndex", task.OrderIndex);
        cmd.Parameters.AddWithValue("@PostponedDays", task.PostponedDays);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateActiveTaskAsync(ActiveTask task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        const string sql = @"
UPDATE ActiveTasks
SET Title = @Title,
    OrderIndex = @OrderIndex,
    PostponedDays = @PostponedDays
WHERE Id = @Id;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", task.Id);
        cmd.Parameters.AddWithValue("@Title", task.Title);
        cmd.Parameters.AddWithValue("@OrderIndex", task.OrderIndex);
        cmd.Parameters.AddWithValue("@PostponedDays", task.PostponedDays);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteActiveTaskAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        const string sql = "DELETE FROM ActiveTasks WHERE Id = @Id;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", id);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ArchivedTask> ArchiveTaskAsync(string taskId, string logicalDate, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalDate);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // 1. Считываем активную задачу
            ActiveTask? activeTask = null;
            await using (var selectCmd = connection.CreateCommand())
            {
                selectCmd.Transaction = transaction;
                selectCmd.CommandText = @"
SELECT Id, Title, CreatedAt, OrderIndex, PostponedDays
FROM ActiveTasks
WHERE Id = @Id;";
                selectCmd.Parameters.AddWithValue("@Id", taskId);

                await using var reader = await selectCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    activeTask = ReadActiveTask(reader);
                }
            }

            if (activeTask is null)
            {
                throw new KeyNotFoundException($"Задача с Id '{taskId}' не найдена в ActiveTasks.");
            }

            // 2. Создаем архивную запись
            var archived = new ArchivedTask
            {
                Id = Guid.NewGuid().ToString(),
                OriginalTaskId = activeTask.Id,
                Title = activeTask.Title,
                TaskSource = "Today",
                CreatedAt = activeTask.CreatedAt,
                CompletedAt = DateTime.UtcNow.ToString("O"),
                LogicalDate = logicalDate
            };

            await using (var insertCmd = connection.CreateCommand())
            {
                insertCmd.Transaction = transaction;
                insertCmd.CommandText = @"
INSERT INTO ArchivedTasks (Id, OriginalTaskId, Title, TaskSource, CreatedAt, CompletedAt, LogicalDate)
VALUES (@Id, @OriginalTaskId, @Title, @TaskSource, @CreatedAt, @CompletedAt, @LogicalDate);";

                insertCmd.Parameters.AddWithValue("@Id", archived.Id);
                insertCmd.Parameters.AddWithValue("@OriginalTaskId", (object?)archived.OriginalTaskId ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("@Title", archived.Title);
                insertCmd.Parameters.AddWithValue("@TaskSource", archived.TaskSource);
                insertCmd.Parameters.AddWithValue("@CreatedAt", archived.CreatedAt);
                insertCmd.Parameters.AddWithValue("@CompletedAt", archived.CompletedAt);
                insertCmd.Parameters.AddWithValue("@LogicalDate", archived.LogicalDate);

                await insertCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            // 3. Удаляем из активных
            await using (var deleteCmd = connection.CreateCommand())
            {
                deleteCmd.Transaction = transaction;
                deleteCmd.CommandText = "DELETE FROM ActiveTasks WHERE Id = @Id;";
                deleteCmd.Parameters.AddWithValue("@Id", taskId);

                await deleteCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return archived;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<ActiveTask> RestoreArchivedTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ArchivedTask? archived = null;
            await using (var selectCmd = connection.CreateCommand())
            {
                selectCmd.Transaction = transaction;
                selectCmd.CommandText = @"
SELECT Id, OriginalTaskId, Title, TaskSource, CreatedAt, CompletedAt, LogicalDate
FROM ArchivedTasks
WHERE OriginalTaskId = @Id OR Id = @Id;";
                selectCmd.Parameters.AddWithValue("@Id", taskId);

                await using var reader = await selectCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    archived = ReadArchivedTask(reader);
                }
            }

            if (archived is null)
            {
                throw new KeyNotFoundException($"Архивная задача '{taskId}' не найдена.");
            }

            // Удаляем из ArchivedTasks
            await using (var deleteCmd = connection.CreateCommand())
            {
                deleteCmd.Transaction = transaction;
                deleteCmd.CommandText = "DELETE FROM ArchivedTasks WHERE Id = @Id;";
                deleteCmd.Parameters.AddWithValue("@Id", archived.Id);
                await deleteCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            // Восстанавливаем в ActiveTasks
            var active = new ActiveTask
            {
                Id = archived.OriginalTaskId ?? archived.Id,
                Title = archived.Title,
                CreatedAt = archived.CreatedAt,
                OrderIndex = 0,
                PostponedDays = 0
            };

            await using (var insertCmd = connection.CreateCommand())
            {
                insertCmd.Transaction = transaction;
                insertCmd.CommandText = @"
INSERT INTO ActiveTasks (Id, Title, CreatedAt, OrderIndex, PostponedDays)
VALUES (@Id, @Title, @CreatedAt, @OrderIndex, @PostponedDays);";
                insertCmd.Parameters.AddWithValue("@Id", active.Id);
                insertCmd.Parameters.AddWithValue("@Title", active.Title);
                insertCmd.Parameters.AddWithValue("@CreatedAt", active.CreatedAt);
                insertCmd.Parameters.AddWithValue("@OrderIndex", active.OrderIndex);
                insertCmd.Parameters.AddWithValue("@PostponedDays", active.PostponedDays);
                await insertCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return active;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<IReadOnlyList<ArchivedTask>> GetArchivedTasksAsync(string? logicalDate = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<ArchivedTask>();

        await using var cmd = connection.CreateCommand();
        if (string.IsNullOrWhiteSpace(logicalDate))
        {
            cmd.CommandText = @"
SELECT Id, OriginalTaskId, Title, TaskSource, CreatedAt, CompletedAt, LogicalDate
FROM ArchivedTasks
ORDER BY CompletedAt DESC;";
        }
        else
        {
            cmd.CommandText = @"
SELECT Id, OriginalTaskId, Title, TaskSource, CreatedAt, CompletedAt, LogicalDate
FROM ArchivedTasks
WHERE LogicalDate = @LogicalDate
ORDER BY CompletedAt DESC;";
            cmd.Parameters.AddWithValue("@LogicalDate", logicalDate);
        }

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ReadArchivedTask(reader));
        }

        return list;
    }

    public async Task ReorderTasksAsync(IEnumerable<(string Id, int NewOrder)> orders, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orders);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            foreach (var (id, newOrder) in orders)
            {
                await using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "UPDATE ActiveTasks SET OrderIndex = @OrderIndex WHERE Id = @Id;";
                cmd.Parameters.AddWithValue("@OrderIndex", newOrder);
                cmd.Parameters.AddWithValue("@Id", id);

                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static ActiveTask ReadActiveTask(DbDataReader reader)
    {
        return new ActiveTask
        {
            Id = reader.GetString(0),
            Title = reader.GetString(1),
            CreatedAt = reader.GetString(2),
            OrderIndex = reader.GetInt32(3),
            PostponedDays = reader.GetInt32(4)
        };
    }

    private static ArchivedTask ReadArchivedTask(DbDataReader reader)
    {
        return new ArchivedTask
        {
            Id = reader.GetString(0),
            OriginalTaskId = reader.IsDBNull(1) ? null : reader.GetString(1),
            Title = reader.GetString(2),
            TaskSource = reader.GetString(3),
            CreatedAt = reader.GetString(4),
            CompletedAt = reader.GetString(5),
            LogicalDate = reader.GetString(6)
        };
    }
}
