using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SecondSelf.Data.Entities;

namespace SecondSelf.Data.Repositories;

/// <summary>
/// Репозиторий задач (Today's Focus и Хроника закрытых дел).
/// </summary>
public interface ITaskRepository
{
    /// <summary>
    /// Получает список всех активных задач дня, отсортированных по OrderIndex.
    /// </summary>
    Task<IReadOnlyList<ActiveTask>> GetActiveTasksAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавляет новую активную задачу дня.
    /// </summary>
    Task AddActiveTaskAsync(ActiveTask task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Обновляет параметры активной задачи (название, порядок, число дней переноса).
    /// </summary>
    Task UpdateActiveTaskAsync(ActiveTask task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет активную задачу по идентификатору.
    /// </summary>
    Task DeleteActiveTaskAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Переносит активную задачу в архив выполненных дел с фиксацией логической даты.
    /// </summary>
    Task<ArchivedTask> ArchiveTaskAsync(string taskId, string logicalDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Восстанавливает архивную задачу обратно в активный список Today's Focus.
    /// </summary>
    Task<ActiveTask> RestoreArchivedTaskAsync(string taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает список архивных задач за всё время либо за конкретную логическую дату.
    /// </summary>
    Task<IReadOnlyList<ArchivedTask>> GetArchivedTasksAsync(string? logicalDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Массово обновляет порядковые номера задач в списке (Drag-and-Drop или сортировка).
    /// </summary>
    Task ReorderTasksAsync(IEnumerable<(string Id, int NewOrder)> orders, CancellationToken cancellationToken = default);
}
