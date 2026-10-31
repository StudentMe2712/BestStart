using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace SecondSelf.Data;

/// <summary>
/// Реализация фабрики подключений к SQLite с PRAGMA настройками для высокой надежности и производительности.
/// </summary>
public sealed class SqliteConnectionFactory : ISqliteConnectionFactory
{
    private const string PragmaScript = @"
PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;
PRAGMA foreign_keys = ON;
PRAGMA busy_timeout = 5000;
PRAGMA cache_size = -8000;
PRAGMA encoding = 'UTF-8';
";

    public string DatabasePath { get; }
    private readonly string _connectionString;

    public SqliteConnectionFactory(string? databasePath = null)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            var envPath = Environment.GetEnvironmentVariable("SECONDSELF_DATABASE_PATH");
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                DatabasePath = envPath;
            }
            else
            {
                DatabasePath = Path.Combine(AppContext.BaseDirectory, "Data", "secondself.db");
            }
        }
        else
        {
            DatabasePath = databasePath;
        }

        if (DatabasePath != ":memory:" && !DatabasePath.StartsWith("Data Source=:memory:", StringComparison.OrdinalIgnoreCase))
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(DatabasePath));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };
        _connectionString = builder.ToString();
    }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        ApplyPragmas(connection);
        return connection;
    }

    public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ApplyPragmasAsync(connection, cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static void ApplyPragmas(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = PragmaScript;
        command.ExecuteNonQuery();
    }

    private static async Task ApplyPragmasAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = PragmaScript;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
