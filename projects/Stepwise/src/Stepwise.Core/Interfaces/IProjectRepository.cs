using Stepwise.Core.Models;

namespace Stepwise.Core.Interfaces;

/// <summary>
/// Репозиторий локального хранилища проекта и шагов (SQLite).
/// </summary>
public interface IProjectRepository : IDisposable
{
    /// <summary>
    /// Корневой путь к каталогу проекта на диске.
    /// </summary>
    string ProjectRootPath { get; }

    /// <summary>
    /// Создает новый проект и инициализирует базу данных.
    /// </summary>
    Project CreateProject(string projectName, string? description = null);

    /// <summary>
    /// Сохраняет шаг инструкции в базу данных.
    /// </summary>
    void SaveStep(Step step);

    /// <summary>
    /// Загружает проект со всеми привязанными шагами.
    /// </summary>
    Project? LoadProject();

    /// <summary>
    /// Загружает упорядоченный список шагов инструкции.
    /// </summary>
    IReadOnlyList<Step> LoadSteps();

    /// <summary>
    /// Обновляет заголовок и описание указанного шага инструкции.
    /// </summary>
    void UpdateStepDetails(Guid stepId, string? title, string? description);
}
