using Stepwise.Core.Models;

namespace Stepwise.Core.Interfaces;

/// <summary>
/// Сервис отслеживания активности окон Windows на базе WinEvent hooks (без циклов опроса).
/// </summary>
public interface IActiveWindowTracker : IDisposable
{
    /// <summary>
    /// Событие смены активного (переднего) окна в операционной системе.
    /// </summary>
    event EventHandler<ActiveWindowInfo>? ActiveWindowChanged;

    /// <summary>
    /// Получает текущую информацию об активном окне верхнего уровня.
    /// </summary>
    ActiveWindowInfo? GetActiveWindow();

    /// <summary>
    /// Запускает отслеживание активности окон.
    /// </summary>
    void Start();

    /// <summary>
    /// Останавливает отслеживание активности окон.
    /// </summary>
    void Stop();

    /// <summary>
    /// Признак активности трекера.
    /// </summary>
    bool IsRunning { get; }
}
