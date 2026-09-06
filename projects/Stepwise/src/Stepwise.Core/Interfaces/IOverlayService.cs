namespace Stepwise.Core.Interfaces;

using Stepwise.Core.Models;

/// <summary>
/// Сервис управления визуальным оверлеем подсказок на рабочем столе.
/// Предоставляет абстракцию управления показом, скрытием и привязкой к целевым элементам для слоя воспроизведения (Player).
/// Изолирует ядро от Win32, XAML, Composition и DispatcherQueue.
/// </summary>
public interface IOverlayService
{
    /// <summary>
    /// Текущее состояние жизненного цикла оверлея.
    /// </summary>
    OverlayState State { get; }

    /// <summary>
    /// Признак фактической видимости оверлея на экране.
    /// </summary>
    bool IsVisible { get; }

    /// <summary>
    /// Признак доступности и включенности механизма оверлея.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Отображает окно оверлея на экране.
    /// </summary>
    void Show();

    /// <summary>
    /// Скрывает окно оверлея.
    /// </summary>
    void Hide();

    /// <summary>
    /// Переключает видимость оверлея (Show / Hide).
    /// </summary>
    void Toggle();

    /// <summary>
    /// Обновляет активный целевой элемент на основе данных шага инструкции.
    /// </summary>
    /// <param name="step">Текущий шаг инструкции или null.</param>
    void UpdateTarget(Step? step);

    /// <summary>
    /// Обновляет активный целевой элемент на основе структурированной информации о цели.
    /// </summary>
    /// <param name="target">Информация о целевом элементе или null.</param>
    void UpdateTarget(OverlayTargetInfo? target);

    /// <summary>
    /// Событие изменения состояния оверлея (старое состояние, новое состояние).
    /// </summary>
    event Action<OverlayState, OverlayState>? StateChanged;
}
