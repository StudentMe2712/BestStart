using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SecondSelf.Core.Services;

/// <summary>
/// Прямоугольная позиция и размеры окна.
/// </summary>
public sealed record WindowPosition(int X, int Y, int Width, int Height);

/// <summary>
/// Информация о рабочей области монитора для многомониторных конфигураций.
/// </summary>
public sealed record DisplayArea
{
    public Windows.Graphics.RectInt32 WorkArea { get; init; }
    public bool IsPrimary { get; init; }

    public DisplayArea() { }

    public DisplayArea(Windows.Graphics.RectInt32 workArea, bool isPrimary = false)
    {
        WorkArea = workArea;
        IsPrimary = isPrimary;
    }

    public DisplayArea(int x, int y, int width, int height, bool isPrimary = false)
        : this(new Windows.Graphics.RectInt32(x, y, width, height), isPrimary)
    {
    }

    public static DisplayArea FromDisplayArea(Microsoft.UI.Windowing.DisplayArea area) =>
        new(area.WorkArea, area.IsPrimary);

    public static implicit operator DisplayArea(Microsoft.UI.Windowing.DisplayArea area) =>
        FromDisplayArea(area);
}

/// <summary>
/// Модель сохранённого состояния и положения окна виджета.
/// </summary>
public sealed record WindowState
{
    public double NormalizedX { get; init; } = 0.85;
    public double NormalizedY { get; init; } = 0.0;
    public string DisplayId { get; init; } = "Primary";
    public double DpiScale { get; init; } = 1.0;
    public bool IsExpanded { get; init; } = false;
}

/// <summary>
/// Сервис управления состоянием, координатами и сохранением геометрии окна виджета.
/// </summary>
public interface IWindowStateService
{
    /// <summary>
    /// Полный путь к файлу конфигурации (window_state.json).
    /// </summary>
    string SettingsFilePath { get; }

    /// <summary>
    /// Асинхронно загружает сохраненное состояние окна из JSON-файла.
    /// Если файл отсутствует или поврежден, возвращает состояние по умолчанию.
    /// </summary>
    Task<WindowState> LoadStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Асинхронно сохраняет текущее состояние окна в JSON-файл.
    /// </summary>
    Task SaveStateAsync(WindowState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Рассчитывает абсолютные экранные координаты X и Y с проверкой границ рабочего стола (WorkArea).
    /// Гарантирует, что окно не окажется за пределами монитора.
    /// </summary>
    (int X, int Y) CalculateWindowPosition(
        WindowState state,
        int windowWidth,
        int windowHeight,
        int workAreaX,
        int workAreaY,
        int workAreaWidth,
        int workAreaHeight);

    /// <summary>
    /// Рассчитывает нормализованные координаты (0.0 .. 1.0) на основе текущей экранной позиции окна.
    /// </summary>
    (double NormalizedX, double NormalizedY) CalculateNormalizedCoordinates(
        int currentX,
        int currentY,
        int windowWidth,
        int windowHeight,
        int workAreaX,
        int workAreaY,
        int workAreaWidth,
        int workAreaHeight);

    /// <summary>
    /// Проверяет, пересекается ли прямоугольник виджета с рабочей областью хотя бы одного активного монитора.
    /// Если монитор был отключен и виджет оказался вне экрана, перемещает или ограничивает виджет
    /// с безопасным отступом (24px) внутри рабочей области основного (Primary) монитора.
    /// </summary>
    WindowPosition EnsureSafePosition(WindowPosition pos, IReadOnlyList<DisplayArea> activeMonitors);

    /// <summary>
    /// Перегрузка EnsureSafePosition для нативных мониторов WinUI Microsoft.UI.Windowing.DisplayArea.
    /// </summary>
    WindowPosition EnsureSafePosition(WindowPosition pos, IReadOnlyList<Microsoft.UI.Windowing.DisplayArea> activeMonitors);
}
