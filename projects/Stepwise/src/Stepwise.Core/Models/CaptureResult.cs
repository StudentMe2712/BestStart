namespace Stepwise.Core.Models;

/// <summary>
/// Результат выполнения операции захвата экрана с метаданными выравнивания и геометрии.
/// </summary>
/// <param name="Success">Признак успешности создания скриншота.</param>
/// <param name="RelativePath">Относительный путь к скриншоту в структуре проекта (например, assets/screenshots/step_001.png).</param>
/// <param name="Width">Ширина сохраненного изображения в пикселях.</param>
/// <param name="Height">Высота сохраненного изображения в пикселях.</param>
/// <param name="HighlightBounds">Координаты подсветки целевого элемента.</param>
/// <param name="ErrorMessage">Сообщение об ошибке в случае сбоя.</param>
public sealed record CaptureResult(
    bool Success,
    string? RelativePath,
    int Width,
    int Height,
    BoundingBox HighlightBounds,
    string? ErrorMessage = null
);
