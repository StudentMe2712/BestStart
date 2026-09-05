using Stepwise.Core.Models;

namespace Stepwise.Core.Interfaces;

/// <summary>
/// Коррелятор событий ввода, преобразующий низкоуровневые события мыши и клавиатуры в высокоуровневые семантические действия.
/// </summary>
public interface IEventCorrelator : IDisposable
{
    /// <summary>
    /// Событие формирования скоррелированного семантического действия.
    /// </summary>
    event EventHandler<SemanticAction>? ActionCorrelated;

    /// <summary>
    /// Обрабатывает низкоуровневое событие мыши.
    /// </summary>
    /// <param name="mouseEvent">Событие мыши.</param>
    /// <param name="context">Контекст активного окна на момент события.</param>
    void ProcessMouseEvent(RawMouseEvent mouseEvent, WindowContext? context = null);

    /// <summary>
    /// Обрабатывает низкоуровневое событие клавиатуры.
    /// </summary>
    /// <param name="keyboardEvent">Событие клавиатуры.</param>
    /// <param name="context">Контекст активного окна на момент события.</param>
    void ProcessKeyboardEvent(RawKeyboardEvent keyboardEvent, WindowContext? context = null);

    /// <summary>
    /// Принудительно сбрасывает и формирует ожидающие буферизованные события (например, накопленный текст или отложенный одиночный клик).
    /// </summary>
    void FlushPending();

    /// <summary>
    /// Сбрасывает внутреннее состояние коррелятора и очищает буферы без генерации действий.
    /// </summary>
    void Reset();
}
