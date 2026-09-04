using Stepwise.Core.Models;

namespace Stepwise.Core.Interfaces;

/// <summary>
/// Сервис захвата экрана и создания скриншотов шагов инструкции.
/// </summary>
public interface IScreenCaptureService
{
    /// <summary>
    /// Захватывает экран или целевую область и сохраняет файл в каталог проекта ([ProjectName]/assets/screenshots/).
    /// </summary>
    /// <param name="projectRootPath">Корневой путь к проекту.</param>
    /// <param name="sequenceIndex">Порядковый номер шага.</param>
    /// <param name="targetRegion">Опциональные координаты элемента для кадрирования или подсветки.</param>
    /// <param name="windowHandle">Опциональный дескриптор окна для привязки захвата.</param>
    /// <returns>Относительный путь к скриншоту (например, "assets/screenshots/step_001.png") или null.</returns>
    string? Capture(string projectRootPath, int sequenceIndex, BoundingBox? targetRegion = null, long windowHandle = 0);
}
