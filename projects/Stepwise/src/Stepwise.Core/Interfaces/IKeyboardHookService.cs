using Stepwise.Core.Models;

namespace Stepwise.Core.Interfaces;

/// <summary>
/// Сервис низкоуровневого перехвата клавиатурных событий Windows (WH_KEYBOARD_LL).
/// </summary>
public interface IKeyboardHookService : IDisposable
{
    /// <summary>
    /// Событие возникновения необработанного клавиатурного ввода.
    /// </summary>
    event EventHandler<RawKeyboardEvent>? KeyboardEventReceived;

    /// <summary>
    /// Запускает перехват событий клавиатуры.
    /// </summary>
    void Start();

    /// <summary>
    /// Останавливает перехват событий клавиатуры.
    /// </summary>
    void Stop();

    /// <summary>
    /// Признак активности хука клавиатуры.
    /// </summary>
    bool IsRunning { get; }
}
