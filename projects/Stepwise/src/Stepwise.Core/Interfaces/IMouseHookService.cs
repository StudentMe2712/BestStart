using Stepwise.Core.Models;

namespace Stepwise.Core.Interfaces;

/// <summary>
/// Сервис перехвата глобальных событий мыши Windows.
/// </summary>
public interface IMouseHookService : IDisposable
{
    /// <summary>
    /// Событие клика мыши.
    /// </summary>
    event EventHandler<MouseClickEvent>? MouseClicked;

    /// <summary>
    /// Запускает перехват событий мыши.
    /// </summary>
    void Start();

    /// <summary>
    /// Останавливает перехват событий мыши.
    /// </summary>
    void Stop();

    /// <summary>
    /// Признак активности хука.
    /// </summary>
    bool IsRunning { get; }
}
