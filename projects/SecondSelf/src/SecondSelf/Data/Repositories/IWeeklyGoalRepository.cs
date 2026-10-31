using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SecondSelf.Data.Entities;

namespace SecondSelf.Data.Repositories;

/// <summary>
/// Репозиторий недельных целей (Weekly Goals).
/// </summary>
public interface IWeeklyGoalRepository
{
    /// <summary>
    /// Получает цель на конкретную ISO-неделю года.
    /// </summary>
    Task<WeeklyGoal?> GetGoalForWeekAsync(int isoYear, int isoWeek, CancellationToken ct = default);

    /// <summary>
    /// Получает список всех целей, относящихся к указанному месяцу ('YYYY-MM').
    /// </summary>
    Task<IReadOnlyList<WeeklyGoal>> GetGoalsForMonthAsync(string yearMonth, CancellationToken ct = default);

    /// <summary>
    /// Получает цель по идентификатору.
    /// </summary>
    Task<WeeklyGoal?> GetGoalByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Добавляет или обновляет недельную цель.
    /// </summary>
    Task AddOrUpdateGoalAsync(WeeklyGoal goal, CancellationToken ct = default);

    /// <summary>
    /// Блокирует цель на неделю при подтверждении коммитмента (Status = 'Locked', LockedAt = UtcNow).
    /// </summary>
    Task LockGoalAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Отмечает недельную цель выполненной (Status = 'Completed', CompletedAt = UtcNow).
    /// </summary>
    Task CompleteGoalAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Отменяет недельную цель с обязательным указанием причины (Status = 'Cancelled', CancellationReason).
    /// </summary>
    Task CancelGoalAsync(string id, string reason, CancellationToken ct = default);

    /// <summary>
    /// Переносит цель на следующую неделю (текущая Status = 'Postponed', создается новая цель на nextYear/nextWeek).
    /// </summary>
    Task<WeeklyGoal> PostponeGoalAsync(string id, string reason, int nextYear, int nextWeek, CancellationToken ct = default);
}
