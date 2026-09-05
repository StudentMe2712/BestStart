using Stepwise.Core.Models;

namespace Stepwise.Core.Interfaces;

/// <summary>
/// Агрегирующий сервис мониторинга глобального низкоуровневого пользовательского ввода (мышь и клавиатура).
/// </summary>
public interface IInputMonitoringService : IDisposable
{
    /// <summary>
    /// Событие сырого ввода мыши (нажатия, отпускания, перемещения, колесо).
    /// </summary>
    event EventHandler<RawMouseEvent>? MouseEventReceived;

    /// <summary>
    /// Событие сырого ввода клавиатуры (нажатия, отпускания, символы, шорткаты).
    /// </summary>
    event EventHandler<RawKeyboardEvent>? KeyboardEventReceived;

    /// <summary>
    /// Запускает мониторинг ввода.
    /// </summary>
    void Start();

    /// <summary>
    /// Останавливает мониторинг ввода.
    /// </summary>
    void Stop();

    /// <summary>
    /// Признак активности мониторинга ввода.
    /// </summary>
    bool IsRunning { get; }
}
