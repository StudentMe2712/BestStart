using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Core.Services;
using SecondSelf.Data;
using Xunit;

namespace SecondSelf.Tests;

public class BackupServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _dbPath;
    private readonly string _backupsDir;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly BackupService _backupService;

    public BackupServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"secondself_backup_test_{Guid.NewGuid():N}");
        _dbPath = Path.Combine(_testDir, "data", "test.db");
        _backupsDir = Path.Combine(_testDir, "backups");

        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        Directory.CreateDirectory(_backupsDir);

        _connectionFactory = new SqliteConnectionFactory(_dbPath);
        _backupService = new BackupService(_connectionFactory, _backupsDir);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch
        {
            // Игнорируем задержки освобождения файлов в ОС
        }
    }

    [Fact]
    public async Task CreateBackupAsync_CreatesValidSqliteBackupViaOnlineBackupApi()
    {
        // 1. Создаём исходную таблицу и данные в базе
        await using (var conn = await _connectionFactory.OpenConnectionAsync())
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE TestData (Id INTEGER PRIMARY KEY, Val TEXT); INSERT INTO TestData (Val) VALUES ('SecondSelfBackupCheck');";
            await cmd.ExecuteNonQueryAsync();
        }

        // 2. Создаем бэкап через сервис
        var backupPath = await _backupService.CreateBackupAsync();

        // 3. Проверяем наличие файла и размер
        Assert.True(File.Exists(backupPath));
        var fileInfo = new FileInfo(backupPath);
        Assert.True(fileInfo.Length > 0);

        // 4. Проверяем, что созданный бэкап — валидная SQLite база с нашими данными
        await using (var backupConn = new SqliteConnection($"Data Source={backupPath}"))
        {
            await backupConn.OpenAsync();
            using var readCmd = backupConn.CreateCommand();
            readCmd.CommandText = "SELECT Val FROM TestData WHERE Id = 1;";
            var val = (string?)await readCmd.ExecuteScalarAsync();
            Assert.Equal("SecondSelfBackupCheck", val);
        }
    }

    [Fact]
    public async Task CleanupOldBackupsAsync_With16Backups_RetainsExact14NewestCopies()
    {
        // Создаем 16 фиктивных файлов бэкапов с возрастающими метками дат
        for (int i = 1; i <= 16; i++)
        {
            var dayStr = i.ToString("D2");
            var fileName = $"secondself_202610{dayStr}_120000.db.bak";
            var filePath = Path.Combine(_backupsDir, fileName);
            await File.WriteAllTextAsync(filePath, $"Backup payload day {dayStr}");
        }

        var initialList = await _backupService.GetBackupsAsync();
        Assert.Equal(16, initialList.Count);

        // Запускаем ротацию с сохранением 14 копий
        int deletedCount = await _backupService.CleanupOldBackupsAsync(maxCopies: 14);

        Assert.Equal(2, deletedCount);

        var remainingBackups = await _backupService.GetBackupsAsync();
        Assert.Equal(14, remainingBackups.Count);

        // Самые старые (01 и 02) должны быть удалены
        Assert.False(File.Exists(Path.Combine(_backupsDir, "secondself_20261001_120000.db.bak")));
        Assert.False(File.Exists(Path.Combine(_backupsDir, "secondself_20261002_120000.db.bak")));

        // Более свежие (с 03 по 16) должны остаться
        Assert.True(File.Exists(Path.Combine(_backupsDir, "secondself_20261003_120000.db.bak")));
        Assert.True(File.Exists(Path.Combine(_backupsDir, "secondself_20261016_120000.db.bak")));
    }

    [Fact]
    public async Task GetBackupsAsync_ReturnsBackupsOrderedByDateDescending()
    {
        var dates = new[] { "20261005_100000", "20261015_120000", "20261010_110000" };
        foreach (var d in dates)
        {
            var filePath = Path.Combine(_backupsDir, $"secondself_{d}.db.bak");
            await File.WriteAllTextAsync(filePath, "dummy data");
        }

        var backups = await _backupService.GetBackupsAsync();

        Assert.Equal(3, backups.Count);
        Assert.Equal("secondself_20261015_120000.db.bak", backups[0].FileName);
        Assert.Equal("secondself_20261010_110000.db.bak", backups[1].FileName);
        Assert.Equal("secondself_20261005_100000.db.bak", backups[2].FileName);
    }
}
