using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Data;
using SecondSelf.Data.Migrations;
using Xunit;

namespace SecondSelf.Tests;

public class DatabaseTriggersTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _factory;

    public DatabaseTriggersTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"secondself_triggers_{Guid.NewGuid():N}.db");
        _factory = new SqliteConnectionFactory(_testDbPath);
        new DatabaseMigrationService(_factory).MigrateAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }

    [Fact]
    public async Task Trigger_BigGoals_MaxActiveCheck_BlocksMoreThan4ActiveGoals()
    {
        await using var conn = await _factory.OpenConnectionAsync();

        // Добавляем 4 активные цели
        for (int i = 1; i <= 4; i++)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO BigGoals (Id, Title, Status, CreatedAt)
VALUES (@Id, @Title, 'Active', @CreatedAt);";
            cmd.Parameters.AddWithValue("@Id", $"goal-{i}");
            cmd.Parameters.AddWithValue("@Title", $"Goal {i}");
            cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
        }

        // Попытка добавить 5-ю активную цель должна вызвать SqliteException с сообщением триггера
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
INSERT INTO BigGoals (Id, Title, Status, CreatedAt)
VALUES ('goal-5', 'Goal 5', 'Active', @CreatedAt);";
            cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow.ToString("O"));

            var ex = await Assert.ThrowsAsync<SqliteException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Contains("Превышен лимит активных целей", ex.Message);
        }

        // Но добавление неактивной (Completed/Archived) цели разрешено
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
INSERT INTO BigGoals (Id, Title, Status, CreatedAt)
VALUES ('goal-archived', 'Goal Archived', 'Archived', @CreatedAt);";
            cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow.ToString("O"));

            int rows = await cmd.ExecuteNonQueryAsync();
            Assert.Equal(1, rows);
        }
    }
}
