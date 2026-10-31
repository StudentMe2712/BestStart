using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SecondSelf.Core.Models;

namespace SecondSelf.Core.Services;

/// <summary>
/// Сервис создания и ротации резервных копий базы данных SQLite.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Создаёт резервную копию базы данных SQLite через нативный Online Backup API.
    /// Сохраняет файл в папку Backups/ с форматом имени secondself_yyyyMMdd_HHmmss.db.bak.
    /// </summary>
    /// <returns>Абсолютный путь к созданному файлу бэкапа.</returns>
    Task<string> CreateBackupAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет устаревшие бэкапы, оставляя только указанное количество самых свежих копий (по умолчанию 14).
    /// </summary>
    /// <param name="maxCopies">Максимальное количество сохраняемых бэкапов.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Количество фактически удалённых файлов.</returns>
    Task<int> CleanupOldBackupsAsync(int maxCopies = 14, CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает список всех существующих резервных копий, отсортированных от новых к старым.
    /// </summary>
    Task<IReadOnlyList<BackupFileInfo>> GetBackupsAsync(CancellationToken cancellationToken = default);
}
