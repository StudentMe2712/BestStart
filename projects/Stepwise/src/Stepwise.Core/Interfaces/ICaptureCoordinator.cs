using Stepwise.Core.Models;

namespace Stepwise.Core.Interfaces;

/// <summary>
/// Координатор захвата экранных снимков для формируемых шагов инструкции.
/// </summary>
public interface ICaptureCoordinator
{
    /// <summary>
    /// Асинхронно выполняет захват экрана для указанного шага и целевого элемента.
    /// </summary>
    /// <param name="sequenceIndex">Порядковый номер шага.</param>
    /// <param name="target">Целевой элемент пользовательского интерфейса.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Относительный путь к сохраненному файлу скриншота или <c>null</c>, если захват не производился или не удался.</returns>
    Task<string?> CaptureStepAsync(int sequenceIndex, ElementInfo target, CancellationToken cancellationToken = default);
}
