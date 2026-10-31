using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SecondSelf.Data.Entities;

namespace SecondSelf.Core.Services;

/// <summary>
/// Сервис циклической очереди воспроизведения видео без повторений на основе алгоритма Фишера — Йейтса (CycleQueue).
/// </summary>
public interface IFisherYatesShuffleService
{
    /// <summary>
    /// Извлекает следующий непроигранный ролик из очереди.
    /// Если очередь пуста или исчерпана, формирует новый цикл перемешивания.
    /// Бесшовно пропускает отсутствующие на диске файлы, помечая их IsMissing = 1.
    /// </summary>
    Task<VideoLibraryItem?> GetNextVideoInCycleAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает текущее состояние очереди воспроизведения.
    /// </summary>
    Task<IReadOnlyList<VideoPlaybackQueueItem>> GetCurrentQueueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Очищает текущую очередь воспроизведения.
    /// </summary>
    Task ResetQueueAsync(CancellationToken cancellationToken = default);
}
