using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SecondSelf.Data.Entities;

namespace SecondSelf.Core.Services;

/// <summary>
/// Сервис локальной библиотеки видео Daily Reset.
/// Сканирует папку Videos/, отслеживает доступность файлов на диске и управляет метаданными.
/// </summary>
public interface IVideoLibraryService
{
    /// <summary>
    /// Путь к каталогу локальной библиотеки видео.
    /// </summary>
    string VideosDirectory { get; }

    /// <summary>
    /// Выполняет сканирование папки видео: добавляет новые файлы в VideoLibrary
    /// и актуализирует флаг IsMissing для существующих записей.
    /// </summary>
    Task<IReadOnlyList<VideoLibraryItem>> ScanLibraryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает видео по уникальному идентификатору.
    /// </summary>
    Task<VideoLibraryItem?> GetVideoByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает все видео из библиотеки, упорядоченные по имени файла.
    /// </summary>
    Task<IReadOnlyList<VideoLibraryItem>> GetAllVideosAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Обновляет статус доступности видеофайла (IsMissing).
    /// </summary>
    Task UpdateMissingStatusAsync(string id, bool isMissing, CancellationToken cancellationToken = default);
}
