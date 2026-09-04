namespace Stepwise.Core.Models;

/// <summary>
/// Прямоугольные координаты и габариты экранного элемента.
/// </summary>
public readonly record struct BoundingBox(double X, double Y, double Width, double Height)
{
    public static BoundingBox Empty => new(0, 0, 0, 0);

    public bool IsEmpty => Width <= 0 || Height <= 0;
}
