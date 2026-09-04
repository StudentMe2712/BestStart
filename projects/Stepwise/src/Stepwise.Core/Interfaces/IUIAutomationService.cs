using Stepwise.Core.Models;

namespace Stepwise.Core.Interfaces;

/// <summary>
/// Сервис инспекции элементов интерфейса через Microsoft UI Automation.
/// </summary>
public interface IUIAutomationService
{
    /// <summary>
    /// Извлекает метаданные элемента UI по экранным координатам.
    /// </summary>
    /// <param name="x">Координата X на экране.</param>
    /// <param name="y">Координата Y на экране.</param>
    /// <returns>Метаданные элемента <see cref="ElementInfo"/>.</returns>
    ElementInfo InspectElementAt(int x, int y);
}
