using System;
using Stepwise.Core.Models;

namespace Stepwise.Core.Interfaces;

/// <summary>
/// Сервис визуального индикатора активной записи экрана (периметральная рамка и информационный виджет).
/// Предоставляет мгновенную визуальную обратную связь при фиксации действий пользователя.
/// </summary>
public interface IScreenRecordingIndicator : IDisposable
{
    /// <summary>
    /// Отображает периметральную рамку записи и информационный виджет.
    /// </summary>
    void StartIndicator();

    /// <summary>
    /// Скрывает индикатор и освобождает экранные ресурсы.
    /// </summary>
    void StopIndicator();

    /// <summary>
    /// Обновляет количество записанных шагов на плавающем виджете.
    /// </summary>
    void UpdateStepCount(int stepCount);

    /// <summary>
    /// Выполняет подсветку захваченного элемента (ripple spotlight) и отображает уведомление о шаге.
    /// </summary>
    /// <param name="elementBounds">Экранные границы зафиксированного элемента.</param>
    /// <param name="elementName">Название зафиксированного элемента.</param>
    /// <param name="sequenceIndex">Порядковый номер шага.</param>
    void FlashCapture(BoundingBox elementBounds, string elementName, int sequenceIndex);
}
