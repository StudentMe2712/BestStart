using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Data;
using SecondSelf.Data.Migrations;
using Xunit;

namespace SecondSelf.Tests;

public class DatabaseMigrationServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _factory;

    public DatabaseMigrationServiceTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"secondself_migrate_{Guid.NewGuid():N}.db");
        _factory = new SqliteConnectionFactory(_testDbPath);
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
    public async Task MigrateAsync_CreatesSchemaAndIsIdempotent()
    {
        var migrationService = new DatabaseMigrationService(_factory);

        // Первый накат
        await migrationService.MigrateAsync();

        // Проверяем наличие таблицы __SchemaMigrations
        await using (var conn = await _factory.OpenConnectionAsync())
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM __SchemaMigrations WHERE Version = 1;";
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(1, count);
        }

        // Проверяем наличие всех ключевых таблиц
        string[] requiredTables =
        [
            "ActiveTasks",
            "ArchivedTasks",
            "BigGoals",
            "Milestones",
            "WeeklyGoals",
            "VideoLibrary",
            "DailyRitualLogs",
            "MonthlyRecaps"
        ];

        await using (var conn = await _factory.OpenConnectionAsync())
        {
            foreach (var table in requiredTables)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name=@name;";
                cmd.Parameters.AddWithValue("@name", table);
                var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
                Assert.True(exists, $"Таблица {table} должна существовать после миграции.");
            }
        }

        // Повторный накат не должен вызывать ошибок (идемпотентность)
        await migrationService.MigrateAsync();
    }
}
