namespace Stepwise.Core.Models;

/// <summary>
/// Прямоугольные координаты, габариты и выбранное размещение информационной подсказки (Callout)
/// на виртуальном экране.
/// </summary>
public sealed record CalloutPosition(
    double X,
    double Y,
    double Width,
    double Height,
    CalloutPlacement Placement
);
