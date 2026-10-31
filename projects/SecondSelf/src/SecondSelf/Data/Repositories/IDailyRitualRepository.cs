using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SecondSelf.Data.Entities;

namespace SecondSelf.Data.Repositories;

/// <summary>
/// Репозиторий записей ежедневного ритуала Daily Reset.
/// </summary>
public interface IDailyRitualRepository
{
    /// <summary>
    /// Получает запись ритуала для указанной логической даты ('YYYY-MM-DD').
    /// </summary>
    Task<DailyRitualLog?> GetDailyRitualLogAsync(string logicalDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Создает или обновляет запись ритуала (Upsert).
    /// </summary>
    Task UpsertDailyRitualLogAsync(DailyRitualLog log, CancellationToken cancellationToken = default);

    /// <summary>
    /// Фиксирует завершение ритуала с отметкой точного ISO-штампа времени.
    /// Активирует триггер TR_DailyRitualLogs_IncrementPlayback.
    /// </summary>
    Task MarkCompletedAsync(string logicalDate, string completedAtIso, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает все записи ритуалов за календарный месяц ('YYYY-MM').
    /// </summary>
    Task<IReadOnlyList<DailyRitualLog>> GetLogsForMonthAsync(string yearMonth, CancellationToken cancellationToken = default);

    /// <summary>
    /// Обновляет текущую позицию воспроизведения LastPositionMs.
    /// </summary>
    Task UpdateLastPositionAsync(string logicalDate, int lastPositionMs, CancellationToken cancellationToken = default);
}
