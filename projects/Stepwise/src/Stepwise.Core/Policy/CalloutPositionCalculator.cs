namespace Stepwise.Core.Policy;

using Stepwise.Core.Models;

/// <summary>
/// Детерминированный калькулятор позиционирования информационной подсказки (Callout)
/// относительно целевого элемента с учетом границ виртуального экрана и приоритетов размещения.
/// </summary>
public static class CalloutPositionCalculator
{
    /// <summary>
    /// Стандартный отступ (в пикселях) между целевым элементом и информационной подсказкой.
    /// </summary>
    public const double DefaultMargin = 12.0;

    /// <summary>
    /// Рассчитывает координаты и ориентацию размещения подсказки <see cref="CalloutPosition"/> на основе
    /// границ целевого элемента, размеров подсказки и границ виртуального экрана.
    /// </summary>
    /// <param name="target">Прямоугольные границы целевого элемента на виртуальном экране.</param>
    /// <param name="calloutWidth">Ширина блока подсказки.</param>
    /// <param name="calloutHeight">Высота блока подсказки.</param>
    /// <param name="virtualScreenBounds">Границы виртуального экрана (поддерживаются отрицательные координаты мультимониторов).</param>
    /// <param name="margin">Зазор между целевым элементом и подсказкой (по умолчанию 12 пикселей).</param>
    /// <returns>Вычисленные координаты и ориентация <see cref="CalloutPosition"/>.</returns>
    public static CalloutPosition Calculate(
        BoundingBox target,
        double calloutWidth,
        double calloutHeight,
        BoundingBox virtualScreenBounds,
        double margin = DefaultMargin)
    {
        var width = Math.Max(0, calloutWidth);
        var height = Math.Max(0, calloutHeight);

        var virtualX = virtualScreenBounds.X;
        var virtualY = virtualScreenBounds.Y;
        var virtualWidth = virtualScreenBounds.Width;
        var virtualHeight = virtualScreenBounds.Height;
        var virtualRight = virtualX + virtualWidth;
        var virtualBottom = virtualY + virtualHeight;

        // Если цель отсутствует или некорректна, центрируем подсказку на виртуальном экране
        if (target.IsEmpty)
        {
            var centerX = virtualX + Math.Max(0, (virtualWidth - width) / 2.0);
            var centerY = virtualY + Math.Max(0, (virtualHeight - height) / 2.0);
            var clampedCenterX = Clamp(centerX, virtualX, virtualRight - width);
            var clampedCenterY = Clamp(centerY, virtualY, virtualBottom - height);
            return new CalloutPosition(clampedCenterX, clampedCenterY, width, height, CalloutPlacement.Below);
        }

        double candidateX;
        double candidateY;
        CalloutPlacement placement;

        // Priority 1: Right of target (target.X + target.Width + margin).
        // If fits in virtual screen (X + width <= virtualX + virtualWidth), use Right.
        var rightX = target.X + target.Width + margin;
        if (rightX + width <= virtualRight)
        {
            placement = CalloutPlacement.Right;
            candidateX = rightX;
            candidateY = target.Y;
        }
        else
        {
            // Priority 2: Left of target (target.X - width - margin).
            // If fits (X >= virtualX), use Left.
            var leftX = target.X - width - margin;
            if (leftX >= virtualX)
            {
                placement = CalloutPlacement.Left;
                candidateX = leftX;
                candidateY = target.Y;
            }
            else
            {
                // Priority 3: Below target (target.Y + target.Height + margin).
                // If fits (Y + height <= virtualY + virtualHeight), use Below.
                var belowY = target.Y + target.Height + margin;
                if (belowY + height <= virtualBottom)
                {
                    placement = CalloutPlacement.Below;
                    candidateX = target.X;
                    candidateY = belowY;
                }
                else
                {
                    // Priority 4: Above target (target.Y - height - margin).
                    // If fits (Y >= virtualY), use Above.
                    var aboveY = target.Y - height - margin;
                    if (aboveY >= virtualY)
                    {
                        placement = CalloutPlacement.Above;
                        candidateX = target.X;
                        candidateY = aboveY;
                    }
                    else
                    {
                        // Fallback: ни в одну из 4 сторон подсказка полностью не помещается без выхода за экран;
                        // берем дефолтный приоритет 1 (Right) и сдвигаем клампингом.
                        placement = CalloutPlacement.Right;
                        candidateX = rightX;
                        candidateY = target.Y;
                    }
                }
            }
        }

        // Clamping: Never place callout outside virtual screen bounds:
        // clamp X between virtualX and virtualX + virtualWidth - width;
        // clamp Y between virtualY and virtualY + virtualHeight - height.
        var clampedX = Clamp(candidateX, virtualX, virtualRight - width);
        var clampedY = Clamp(candidateY, virtualY, virtualBottom - height);

        return new CalloutPosition(clampedX, clampedY, width, height, placement);
    }

    /// <summary>
    /// Вычисляет позицию подсказки на основе информации <see cref="OverlayTargetInfo"/>.
    /// </summary>
    public static CalloutPosition Calculate(
        OverlayTargetInfo? target,
        double calloutWidth,
        double calloutHeight,
        BoundingBox virtualScreenBounds,
        double margin = DefaultMargin)
    {
        if (target == null || !target.IsTargetValid)
        {
            return Calculate(BoundingBox.Empty, calloutWidth, calloutHeight, virtualScreenBounds, margin);
        }

        return Calculate(target.BoundingRectangle, calloutWidth, calloutHeight, virtualScreenBounds, margin);
    }

    /// <summary>
    /// Вычисляет позицию подсказки на основе данных шага <see cref="Step"/>.
    /// </summary>
    public static CalloutPosition Calculate(
        Step? step,
        double calloutWidth,
        double calloutHeight,
        BoundingBox virtualScreenBounds,
        double margin = DefaultMargin)
    {
        var targetInfo = OverlayTargetInfo.FromStep(step);
        return Calculate(targetInfo, calloutWidth, calloutHeight, virtualScreenBounds, margin);
    }

    private static double Clamp(double value, double min, double max)
    {
        if (min > max)
        {
            return min;
        }

        return Math.Clamp(value, min, max);
    }
}
