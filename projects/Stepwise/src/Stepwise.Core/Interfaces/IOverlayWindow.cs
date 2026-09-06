namespace Stepwise.Core.Interfaces;

using Stepwise.Core.Models;

/// <summary>
/// Платформенно-независимая абстракция нативного окна оверлея на рабочем столе.
/// Реализуется уровнем интеграции с Windows (Win32 / Windows App SDK / Composition).
/// Изолирует ядро от низкоуровневых типов окон (HWND, Window, XAML, Composition, DispatcherQueue).
/// </summary>
public interface IOverlayWindow
{
    /// <summary>
    /// Устанавливает границы окна оверлея в координатах виртуального экрана.
    /// </summary>
    /// <param name="x">Координата X левого верхнего угла.</param>
    /// <param name="y">Координата Y левого верхнего угла.</param>
    /// <param name="width">Ширина окна.</param>
    /// <param name="height">Высота окна.</param>
    void SetBounds(int x, int y, int width, int height);

    /// <summary>
    /// Отображает окно оверлея на рабочем столе.
    /// </summary>
    void ShowWindow();

    /// <summary>
    /// Скрывает окно оверлея.
    /// </summary>
    void HideWindow();

    /// <summary>
    /// Закрывает окно оверлея и освобождает занятые графические и оконные ресурсы.
    /// </summary>
    void CloseWindow();

    /// <summary>
    /// Признак того, что окно оверлея отображается на экране.
    /// </summary>
    bool IsWindowVisible { get; }

    /// <summary>
    /// Обновляет визуальное содержимое оверлея: целевую подсветку и позиционирование подсказки.
    /// </summary>
    /// <param name="target">Информация о подсвечиваемом элементе или null.</param>
    /// <param name="callout">Рассчитанная позиция информационной плашки подсказки или null.</param>
    /// <param name="isMissingTarget">Флаг отсутствия целевого элемента на экране.</param>
    void UpdateVisuals(OverlayTargetInfo? target, CalloutPosition? callout, bool isMissingTarget);
}
