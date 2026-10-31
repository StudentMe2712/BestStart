using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Core.Services;

namespace SecondSelf.Data;

/// <summary>
/// Реализация сервиса самовосстановления базы данных SQLite (AC-14).
/// Проверяет целостность через PRAGMA quick_check; при сбое архивирует поврежденные файлы
/// в .corrupted и накатывает самый свежий бэкап.
/// </summary>
public sealed class DatabaseRecoveryService : IDatabaseRecoveryService
{
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly IBackupService _backupService;

    public DatabaseRecoveryService(ISqliteConnectionFactory connectionFactory, IBackupService backupService)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
    }

    /// <inheritdoc />
    public async Task<DatabaseRecoveryResult> CheckAndRecoverAsync(CancellationToken cancellationToken = default)
    {
        var dbPath = _connectionFactory.DatabasePath;

        // Память или пустой путь проверять на файловую коррупцию не требуется
        if (string.IsNullOrWhiteSpace(dbPath) || dbPath == ":memory:" || dbPath.StartsWith("Data Source=:memory:", StringComparison.OrdinalIgnoreCase))
        {
            return DatabaseRecoveryResult.Healthy();
        }

        // Если файл базы данных еще не создан (первый запуск), повреждений нет
        if (!File.Exists(dbPath))
        {
            return DatabaseRecoveryResult.Healthy();
        }

        bool isCorrupted = false;
        string corruptionReason = string.Empty;

        try
        {
            using var connection = _connectionFactory.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check;";
            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var quickCheckResult = result?.ToString()?.Trim();
            if (!string.Equals(quickCheckResult, "ok", StringComparison.OrdinalIgnoreCase))
            {
                isCorrupted = true;
                corruptionReason = quickCheckResult ?? "PRAGMA quick_check returned non-ok";
            }
        }
        catch (SqliteException ex)
        {
            isCorrupted = true;
            corruptionReason = ex.Message;
        }
        catch (Exception ex)
        {
            isCorrupted = true;
            corruptionReason = ex.Message;
        }

        if (!isCorrupted)
        {
            return DatabaseRecoveryResult.Healthy();
        }

        // 1. Безопасно освобождаем пул соединений для этой базы перед файловыми операциями
        ClearConnectionPool(dbPath);

        // 2. Копируем поврежденные файлы в ${DatabasePath}.corrupted, -wal.corrupted, -shm.corrupted
        var corruptedDbPath = $"{dbPath}.corrupted";
        try
        {
            if (File.Exists(dbPath))
            {
                File.Copy(dbPath, corruptedDbPath, overwrite: true);
            }

            var walPath = $"{dbPath}-wal";
            if (File.Exists(walPath))
            {
                File.Copy(walPath, $"{walPath}.corrupted", overwrite: true);
                try { File.Delete(walPath); } catch { }
            }

            var shmPath = $"{dbPath}-shm";
            if (File.Exists(shmPath))
            {
                File.Copy(shmPath, $"{shmPath}.corrupted", overwrite: true);
                try { File.Delete(shmPath); } catch { }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DatabaseRecovery] Failed to archive corrupted files: {ex.Message}");
        }

        // 3. Запрашиваем список существующих бэкапов для выбора самого свежего
        var backups = await _backupService.GetBackupsAsync(cancellationToken).ConfigureAwait(false);
        var latestBackup = backups.OrderByDescending(b => b.CreatedAtUtc).FirstOrDefault();

        if (latestBackup == null || string.IsNullOrWhiteSpace(latestBackup.FilePath) || !File.Exists(latestBackup.FilePath))
        {
            return DatabaseRecoveryResult.CorruptedNotRestored(
                $"База данных повреждена ({corruptionReason}), но резервные копии для восстановления не найдены.");
        }

        // 4. Восстанавливаем базу данных из файла бэкапа
        try
        {
            ClearConnectionPool(dbPath);

            // Копируем файл бэкапа поверх поврежденной базы данных
            File.Copy(latestBackup.FilePath, dbPath, overwrite: true);

            // Очищаем оставшиеся журналы -wal и -shm от старой поврежденной базы
            var walPath = $"{dbPath}-wal";
            if (File.Exists(walPath))
            {
                try { File.Delete(walPath); } catch { }
            }

            var shmPath = $"{dbPath}-shm";
            if (File.Exists(shmPath))
            {
                try { File.Delete(shmPath); } catch { }
            }

            // 5. Проверяем целостность восстановленной базы данных
            using (var connection = _connectionFactory.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA quick_check;";
                var verifyResult = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                var checkStr = verifyResult?.ToString()?.Trim();
                if (!string.Equals(checkStr, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    return DatabaseRecoveryResult.CorruptedNotRestored(
                        $"Восстановленная база данных не прошла проверку целостности: {checkStr}");
                }
            }

            return DatabaseRecoveryResult.Restored("Данные восстановлены из резервной копии");
        }
        catch (Exception ex)
        {
            return DatabaseRecoveryResult.CorruptedNotRestored(
                $"Ошибка при восстановлении базы данных из резервной копии: {ex.Message}");
        }
    }

    private static void ClearConnectionPool(string dbPath)
    {
        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            };
            using var cleanupConn = new SqliteConnection(builder.ToString());
            SqliteConnection.ClearPool(cleanupConn);
        }
        catch { }

        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}
