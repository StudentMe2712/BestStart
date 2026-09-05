using Stepwise.Core.Models;

namespace Stepwise.Core.Interfaces;

/// <summary>
/// Разрешитель целевого UI-элемента для семантического действия пользователя.
/// </summary>
public interface ITargetResolver
{
    /// <summary>
    /// Асинхронно определяет UI-элемент, над которым или в контексте которого произошло семантическое действие.
    /// </summary>
    /// <param name="action">Скоррелированное семантическое действие.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Информация о найденном элементе пользовательского интерфейса.</returns>
    Task<ElementInfo> ResolveTargetAsync(SemanticAction action, CancellationToken cancellationToken = default);
}
