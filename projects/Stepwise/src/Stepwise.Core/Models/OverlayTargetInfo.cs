namespace Stepwise.Core.Models;

/// <summary>
/// Информация о целевом элементе интерфейса для позиционирования и визуализации подсветки оверлея.
/// </summary>
public sealed record OverlayTargetInfo(
    BoundingBox BoundingRectangle,
    long WindowHandle,
    string? Title,
    string? Description,
    double ClickX,
    double ClickY,
    bool IsTargetValid
)
{
    /// <summary>
    /// Экземпляр пустого/недействительного целевого элемента по умолчанию.
    /// </summary>
    public static OverlayTargetInfo Empty => new(
        BoundingRectangle: BoundingBox.Empty,
        WindowHandle: 0,
        Title: null,
        Description: null,
        ClickX: 0,
        ClickY: 0,
        IsTargetValid: false
    );

    /// <summary>
    /// Создает экземпляр <see cref="OverlayTargetInfo"/> на основе данных шага руководства (<see cref="Step"/>).
    /// </summary>
    /// <param name="step">Шаг интерактивного руководства.</param>
    /// <returns>Структурированная информация для позиционирования оверлея.</returns>
    public static OverlayTargetInfo FromStep(Step? step)
    {
        if (step == null)
        {
            return Empty;
        }

        var bounds = step.TargetElement?.BoundingRectangle ?? BoundingBox.Empty;
        var isValid = !bounds.IsEmpty;

        return new OverlayTargetInfo(
            BoundingRectangle: bounds,
            WindowHandle: step.TargetElement?.WindowHandle ?? 0,
            Title: step.Title ?? step.TargetElement?.Name,
            Description: step.Description,
            ClickX: step.ClickX,
            ClickY: step.ClickY,
            IsTargetValid: isValid
        );
    }
}
