using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Core.Services;
using SecondSelf.Data;
using SecondSelf.Data.Migrations;
using Xunit;

namespace SecondSelf.Tests;

/// <summary>
/// BDD Acceptance Tests для критерия AC-14: SQLite Self-Healing & Crash Recovery.
/// Given: валидная база данных с примененными миграциями и данными, плюс сохраненная резервная копия в Backups/.
/// When: файл базы данных поврежден (записан некорректный заголовок или мусорные байты).
/// Then:
/// 1. recoveryService.CheckAndRecoverAsync() обнаруживает повреждение (WasCorrupted = true).
/// 2. Поврежденный файл сохранен как secondself.db.corrupted на диске.
/// 3. secondself.db успешно восстановлен из резервной копии (RestoredSuccessfully = true).
/// 4. PRAGMA quick_check; на восстановленной БД возвращает "ok" и данные сохранены.
/// 5. Результат содержит сообщение "Данные восстановлены из резервной копии".
/// </summary>
public sealed class AC14_DatabaseCorruptionSelfHealingTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _testDbPath;
    private readonly string _backupsDir;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly BackupService _backupService;
    private readonly DatabaseRecoveryService _recoveryService;

    public AC14_DatabaseCorruptionSelfHealingTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"secondself_ac14_{Guid.NewGuid():N}");
        _testDbPath = Path.Combine(_tempDir, "Data", "secondself.db");
        _backupsDir = Path.Combine(_tempDir, "Backups");

        Directory.CreateDirectory(Path.GetDirectoryName(_testDbPath)!);
        Directory.CreateDirectory(_backupsDir);

        _connectionFactory = new SqliteConnectionFactory(_testDbPath);
        _backupService = new BackupService(_connectionFactory, _backupsDir);
        _recoveryService = new DatabaseRecoveryService(_connectionFactory, _backupService);
    }

    public void Dispose()
    {
        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = _testDbPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            };
            using var conn = new SqliteConnection(builder.ToString());
            SqliteConnection.ClearPool(conn);
        }
        catch { }

        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Игнорируем задержки файловой системы в тестах
        }
    }

    [Fact]
    public async Task AC14_WhenDatabaseIsHealthy_CheckAndRecover_ReturnsHealthyResult()
    {
        // Given: создана валидная база с миграциями
        var migrationService = new DatabaseMigrationService(_connectionFactory);
        await migrationService.MigrateAsync();

        // When: вызывается проверка целостности
        var result = await _recoveryService.CheckAndRecoverAsync();

        // Then: база признана целостной, восстановление не требуется
        Assert.NotNull(result);
        Assert.False(result.WasCorrupted);
        Assert.False(result.RestoredSuccessfully);
        Assert.Equal("База данных целостна", result.Message);
    }

    [Fact]
    public async Task AC14_WhenDatabaseIsCorrupted_Detects_ArchivesCorrupted_AndRestoresFromBackup()
    {
        // Given:
        // 1. Создаем валидную базу данных и накатываем схему
        var migrationService = new DatabaseMigrationService(_connectionFactory);
        await migrationService.MigrateAsync();

        // 2. Записываем контрольные данные
        await using (var conn = await _connectionFactory.OpenConnectionAsync())
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO ActiveTasks (Id, Title, CreatedAt, OrderIndex) 
                VALUES ('ac14-task-test', 'Задача перед сбоем диска', '2026-11-01T00:00:00Z', 0);
            ";
            await cmd.ExecuteNonQueryAsync();
        }

        // 3. Создаем валидный снапшот бэкапа в Backups/
        var backupPath = await _backupService.CreateBackupAsync();
        Assert.True(File.Exists(backupPath));

        // Освобождаем подключения для модификации файла
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _testDbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };
        using (var c = new SqliteConnection(builder.ToString()))
        {
            SqliteConnection.ClearPool(c);
        }
        GC.Collect();
        GC.WaitForPendingFinalizers();

        // When: база данных повреждается (записываем мусорные байты вместо валидного заголовка SQLite)
        var corruptedData = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88 };
        await File.WriteAllBytesAsync(_testDbPath, corruptedData);

        // Then:
        // 1. CheckAndRecoverAsync() обнаруживает коррупцию и восстанавливает базу
        var recoveryResult = await _recoveryService.CheckAndRecoverAsync();

        Assert.NotNull(recoveryResult);
        Assert.True(recoveryResult.WasCorrupted, "Сервис должен зафиксировать факт повреждения БД.");
        Assert.True(recoveryResult.RestoredSuccessfully, "Сервис должен успешно восстановить БД из бэкапа.");

        // 2. secondself.db.corrupted существует на диске
        var corruptedDbPath = $"{_testDbPath}.corrupted";
        Assert.True(File.Exists(corruptedDbPath), $"Поврежденный файл должен быть скопирован в {corruptedDbPath}");
        var savedCorruptedBytes = await File.ReadAllBytesAsync(corruptedDbPath);
        Assert.Equal(corruptedData, savedCorruptedBytes);

        // 3. secondself.db восстановлен и PRAGMA quick_check возвращает 'ok'
        Assert.True(File.Exists(_testDbPath), "Восстановленный файл базы данных должен существовать.");
        await using (var restoredConn = await _connectionFactory.OpenConnectionAsync())
        {
            using var checkCmd = restoredConn.CreateCommand();
            checkCmd.CommandText = "PRAGMA quick_check;";
            var checkResult = (await checkCmd.ExecuteScalarAsync())?.ToString();
            Assert.Equal("ok", checkResult);

            // 4. Проверяем сохранность контрольных данных
            using var selectCmd = restoredConn.CreateCommand();
            selectCmd.CommandText = "SELECT Title FROM ActiveTasks WHERE Id = 'ac14-task-test';";
            var retrievedTitle = (await selectCmd.ExecuteScalarAsync())?.ToString();
            Assert.Equal("Задача перед сбоем диска", retrievedTitle);
        }

        // 5. Сообщение содержит "Данные восстановлены из резервной копии"
        Assert.Contains("Данные восстановлены из резервной копии", recoveryResult.Message);
    }

    [Fact]
    public async Task AC14_WhenDatabaseCorrupted_AndNoBackupsExist_ReportsCorruptionWithoutCrash()
    {
        // Given: поврежденный файл базы данных без существующих бэкапов
        var corruptedData = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 };
        await File.WriteAllBytesAsync(_testDbPath, corruptedData);

        // When: запускаем самовосстановление
        var recoveryResult = await _recoveryService.CheckAndRecoverAsync();

        // Then: фиксирует коррупцию, но указывает на отсутствие бэкапов без аварийного завершения
        Assert.NotNull(recoveryResult);
        Assert.True(recoveryResult.WasCorrupted);
        Assert.False(recoveryResult.RestoredSuccessfully);
        Assert.Contains("резервные копии для восстановления не найдены", recoveryResult.Message);
        Assert.True(File.Exists($"{_testDbPath}.corrupted"));
    }
}
