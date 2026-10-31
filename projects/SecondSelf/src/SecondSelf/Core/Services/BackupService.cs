using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Core.Models;
using SecondSelf.Data;

namespace SecondSelf.Core.Services;

/// <summary>
/// Реализация сервиса резервного копирования базы данных с использованием SQLite Online Backup API и ротацией архивов.
/// </summary>
public sealed class BackupService : IBackupService
{
    private const string BackupFilePrefix = "secondself_";
    private const string BackupFileExtension = ".db.bak";
    private const string TimestampFormat = "yyyyMMdd_HHmmss";

    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly string _backupDirectory;

    public BackupService(ISqliteConnectionFactory connectionFactory, string? backupDirectory = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

        if (!string.IsNullOrWhiteSpace(backupDirectory))
        {
            _backupDirectory = Path.GetFullPath(backupDirectory);
        }
        else
        {
            var baseDir = AppContext.BaseDirectory;
            _backupDirectory = Path.Combine(baseDir, "Backups");
        }
    }

    /// <inheritdoc />
    public async Task<string> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_backupDirectory);

        var now = DateTime.UtcNow;
        var timestamp = now.ToString(TimestampFormat, CultureInfo.InvariantCulture);
        var fileName = $"{BackupFilePrefix}{timestamp}{BackupFileExtension}";
        var backupFilePath = Path.Combine(_backupDirectory, fileName);

        // Если в ту же секунду уже есть файл, добавляем числовой суффикс
        int suffix = 1;
        while (File.Exists(backupFilePath))
        {
            fileName = $"{BackupFilePrefix}{timestamp}_{suffix++}{BackupFileExtension}";
            backupFilePath = Path.Combine(_backupDirectory, fileName);
        }

        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var source = _connectionFactory.OpenConnection();
            using var destination = new SqliteConnection($"Data Source={backupFilePath}");
            destination.Open();

            source.BackupDatabase(destination);
        }, cancellationToken).ConfigureAwait(false);

        return backupFilePath;
    }

    /// <inheritdoc />
    public async Task<int> CleanupOldBackupsAsync(int maxCopies = 14, CancellationToken cancellationToken = default)
    {
        if (maxCopies < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCopies), "Количество копий для сохранения не может быть отрицательным.");
        }

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(_backupDirectory))
            {
                return 0;
            }

            var backups = GetBackupsInternal();
            if (backups.Count <= maxCopies)
            {
                return 0;
            }

            var toDelete = backups.Skip(maxCopies).ToList();
            int deletedCount = 0;

            foreach (var backup in toDelete)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (File.Exists(backup.FilePath))
                    {
                        File.Delete(backup.FilePath);
                        deletedCount++;
                    }
                }
                catch (IOException)
                {
                    // Файл может быть временно заблокирован внешним процессом
                }
                catch (UnauthorizedAccessException)
                {
                    // Игнорируем ошибки доступа при очистке
                }
            }

            return deletedCount;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BackupFileInfo>> GetBackupsAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (IReadOnlyList<BackupFileInfo>)GetBackupsInternal();
        }, cancellationToken).ConfigureAwait(false);
    }

    private List<BackupFileInfo> GetBackupsInternal()
    {
        if (!Directory.Exists(_backupDirectory))
        {
            return new List<BackupFileInfo>();
        }

        var dirInfo = new DirectoryInfo(_backupDirectory);
        var files = dirInfo.GetFiles($"{BackupFilePrefix}*{BackupFileExtension}");

        return files
            .Select(f => new BackupFileInfo
            {
                FilePath = f.FullName,
                FileName = f.Name,
                CreatedAtUtc = ParseTimestampOrFallback(f),
                FileSizeBytes = f.Length
            })
            .OrderByDescending(b => b.CreatedAtUtc)
            .ToList();
    }

    private static DateTime ParseTimestampOrFallback(FileInfo file)
    {
        // Ожидаемый формат имени: secondself_yyyyMMdd_HHmmss.db.bak или secondself_yyyyMMdd_HHmmss_1.db.bak
        var nameWithoutExt = Path.GetFileNameWithoutExtension(file.Name); // secondself_yyyyMMdd_HHmmss[.db]
        if (nameWithoutExt.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
        {
            nameWithoutExt = Path.GetFileNameWithoutExtension(nameWithoutExt);
        }

        if (nameWithoutExt.StartsWith(BackupFilePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var datePart = nameWithoutExt[BackupFilePrefix.Length..];
            // Если есть суффикс _1
            var underscoreParts = datePart.Split('_');
            if (underscoreParts.Length >= 2)
            {
                var stampStr = $"{underscoreParts[0]}_{underscoreParts[1]}";
                if (DateTime.TryParseExact(stampStr, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
                {
                    return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
                }
            }
        }

        var fallback = file.CreationTimeUtc > file.LastWriteTimeUtc ? file.CreationTimeUtc : file.LastWriteTimeUtc;
        return DateTime.SpecifyKind(fallback, DateTimeKind.Utc);
    }
}
