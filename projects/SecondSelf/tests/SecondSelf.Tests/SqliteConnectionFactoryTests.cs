using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Data;
using Xunit;

namespace SecondSelf.Tests;

public class SqliteConnectionFactoryTests : IDisposable
{
    private readonly string _testDbPath;

    public SqliteConnectionFactoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"secondself_test_{Guid.NewGuid():N}.db");
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
    public async Task OpenConnectionAsync_AppliesRequiredPragmas()
    {
        var factory = new SqliteConnectionFactory(_testDbPath);
        await using var connection = await factory.OpenConnectionAsync();

        // 1. Check journal_mode = WAL
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA journal_mode;";
            var mode = (string?)await cmd.ExecuteScalarAsync();
            Assert.Equal("wal", mode?.ToLowerInvariant());
        }

        // 2. Check foreign_keys = ON (1)
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA foreign_keys;";
            var fk = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(1, fk);
        }

        // 3. Check busy_timeout = 5000
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA busy_timeout;";
            var timeout = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(5000, timeout);
        }
    }
}
