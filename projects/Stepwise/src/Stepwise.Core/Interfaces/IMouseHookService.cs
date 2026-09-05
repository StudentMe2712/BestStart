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
    /// Событие сырого ввода мыши (нажатие, отпускание, колесо, перемещение).
    /// </summary>
    event EventHandler<RawMouseEvent>? RawMouseEventReceived;

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
