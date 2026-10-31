using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SecondSelf.Data.Entities;

namespace SecondSelf.Data.Repositories;

/// <summary>
/// Репозиторий Больших целей (горизонт 1–3 года) и их промежуточных этапов (вех).
/// </summary>
public interface IBigGoalRepository
{
    /// <summary>
    /// Возвращает список всех активных Больших целей с загруженными этапами, отсортированными по OrderIndex.
    /// </summary>
    Task<IReadOnlyList<BigGoal>> GetActiveGoalsAsync(CancellationToken ct = default);

    /// <summary>
    /// Возвращает цель по идентификатору с загруженными этапами, отсортированными по OrderIndex.
    /// </summary>
    Task<BigGoal?> GetGoalByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Добавляет новую Большую цель и её начальные этапы в рамках единой транзакции.
    /// Перед вставкой валидирует лимит активных целей (&lt; 4).
    /// </summary>
    Task AddGoalAsync(BigGoal goal, IEnumerable<Milestone> initialMilestones, CancellationToken ct = default);

    /// <summary>
    /// Обновляет параметры Большой цели (название, мотивация, целевой результат, период, иконка, статус).
    /// </summary>
    Task UpdateGoalAsync(BigGoal goal, CancellationToken ct = default);

    /// <summary>
    /// Добавляет новый промежуточный этап (веху) к цели.
    /// </summary>
    Task AddMilestoneAsync(Milestone milestone, CancellationToken ct = default);

    /// <summary>
    /// Обновляет существующий промежуточный этап.
    /// </summary>
    Task UpdateMilestoneAsync(Milestone milestone, CancellationToken ct = default);

    /// <summary>
    /// Отмечает промежуточный этап как выполненный (Status = 'Completed', CompletedAt = UtcNow ISO-8601).
    /// </summary>
    Task CompleteMilestoneAsync(string milestoneId, CancellationToken ct = default);

    /// <summary>
    /// Переводит Большую цель в архив (Status = 'Archived').
    /// </summary>
    Task ArchiveGoalAsync(string goalId, CancellationToken ct = default);

    /// <summary>
    /// Удаляет Большую цель и все связанные с ней этапы.
    /// </summary>
    Task DeleteGoalAsync(string goalId, CancellationToken ct = default);
}
