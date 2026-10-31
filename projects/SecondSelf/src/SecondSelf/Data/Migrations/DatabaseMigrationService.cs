using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace SecondSelf.Data.Migrations;

/// <summary>
/// Сервис встроенного наката миграций схемы SQLite.
/// Считывает миграции из внедренных ресурсов сборки и файлов,
/// гарантирует атомарность выполнения каждой миграции в транзакции.
/// </summary>
public sealed class DatabaseMigrationService : IDatabaseMigrationService
{
    private static readonly Regex MigrationRegex = new(
        @"(?:^|\.)(?<version>\d+)_(?<name>[^.]+)\.sql$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly Assembly _migrationAssembly;

    public DatabaseMigrationService(
        ISqliteConnectionFactory connectionFactory,
        Assembly? migrationAssembly = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _migrationAssembly = migrationAssembly ?? typeof(DatabaseMigrationService).Assembly;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        // 1. Создаем служебную таблицу истории миграций
        const string createMigrationsTableSql = @"
CREATE TABLE IF NOT EXISTS __SchemaMigrations (
    Version INTEGER PRIMARY KEY,
    AppliedAt TEXT NOT NULL,
    ScriptName TEXT NOT NULL
);";

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = createMigrationsTableSql;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // 2. Считываем уже примененные версии
        var appliedVersions = new HashSet<int>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Version FROM __SchemaMigrations ORDER BY Version ASC;";
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                appliedVersions.Add(reader.GetInt32(0));
            }
        }

        // 3. Собираем доступные миграции
        var discoveredMigrations = DiscoverMigrations();

        // 4. Последовательно накатываем новые миграции
        foreach (var migration in discoveredMigrations)
        {
            if (appliedVersions.Contains(migration.Version))
            {
                continue;
            }

            await ApplyMigrationAsync(connection, migration, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ApplyMigrationAsync(
        SqliteConnection connection,
        MigrationItem migration,
        CancellationToken cancellationToken)
    {
        string sqlContent = await migration.LoadSqlAsync().ConfigureAwait(false);

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = sqlContent;
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var logCmd = connection.CreateCommand())
            {
                logCmd.Transaction = transaction;
                logCmd.CommandText = @"
INSERT INTO __SchemaMigrations (Version, AppliedAt, ScriptName)
VALUES (@version, @appliedAt, @scriptName);";

                logCmd.Parameters.AddWithValue("@version", migration.Version);
                logCmd.Parameters.AddWithValue("@appliedAt", DateTime.UtcNow.ToString("O"));
                logCmd.Parameters.AddWithValue("@scriptName", migration.ScriptName);

                await logCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            try
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Игнорируем вторичную ошибку отката
            }

            LogError(migration, ex);
            throw;
        }
    }

    private void LogError(MigrationItem migration, Exception exception)
    {
        try
        {
            string dbPath = _connectionFactory.DatabasePath;
            string targetDir;

            if (dbPath == ":memory:" || dbPath.StartsWith("Data Source=:memory:", StringComparison.OrdinalIgnoreCase))
            {
                targetDir = Path.Combine(AppContext.BaseDirectory, "Data");
            }
            else
            {
                targetDir = Path.GetDirectoryName(Path.GetFullPath(dbPath)) ?? AppContext.BaseDirectory;
            }

            Directory.CreateDirectory(targetDir);
            string logPath = Path.Combine(targetDir, "migrations.log");

            string entry = $"[{DateTime.UtcNow:O}] ERROR in migration {migration.Version} ({migration.ScriptName}):{Environment.NewLine}{exception}{Environment.NewLine}{new string('-', 80)}{Environment.NewLine}";
            File.AppendAllText(logPath, entry);
        }
        catch
        {
            // Ошибка логирования не должна маскировать основное исключение
        }
    }

    private List<MigrationItem> DiscoverMigrations()
    {
        var migrations = new Dictionary<int, MigrationItem>();

        // 1. Поиск во внедренных ресурсах сборки (EmbeddedResource)
        foreach (var resourceName in _migrationAssembly.GetManifestResourceNames())
        {
            var match = MigrationRegex.Match(resourceName);
            if (match.Success && int.TryParse(match.Groups["version"].Value, out int version))
            {
                string scriptName = resourceName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)
                    ? resourceName[(resourceName.LastIndexOf('.', resourceName.Length - 5) + 1)..]
                    : resourceName;

                migrations[version] = new MigrationItem(
                    version,
                    scriptName,
                    () =>
                    {
                        using var stream = _migrationAssembly.GetManifestResourceStream(resourceName)
                            ?? throw new InvalidOperationException($"Не удалось загрузить ресурс {resourceName}");
                        using var reader = new StreamReader(stream);
                        return Task.FromResult(reader.ReadToEnd());
                    });
            }
        }

        // 2. Fallback: поиск на диске в Data/Migrations
        var searchDirectories = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Data", "Migrations"),
            Path.Combine(Directory.GetCurrentDirectory(), "Data", "Migrations"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "SecondSelf", "Data", "Migrations")
        };

        foreach (var dir in searchDirectories)
        {
            if (Directory.Exists(dir))
            {
                foreach (var file in Directory.GetFiles(dir, "*.sql"))
                {
                    var fileName = Path.GetFileName(file);
                    var match = MigrationRegex.Match(fileName);
                    if (match.Success && int.TryParse(match.Groups["version"].Value, out int version))
                    {
                        if (!migrations.ContainsKey(version))
                        {
                            migrations[version] = new MigrationItem(
                                version,
                                fileName,
                                async () => await File.ReadAllTextAsync(file).ConfigureAwait(false));
                        }
                    }
                }
            }
        }

        return migrations.Values.OrderBy(m => m.Version).ToList();
    }

    private sealed record MigrationItem(
        int Version,
        string ScriptName,
        Func<Task<string>> LoadSqlAsync);
}
