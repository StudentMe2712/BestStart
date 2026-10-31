using System.Threading;
using System.Threading.Tasks;

namespace SecondSelf.Core.Services;

/// <summary>
/// Модель сохранённого состояния и положения окна виджета.
/// </summary>
public sealed record WindowState
{
    public double NormalizedX { get; init; } = 0.85;
    public double NormalizedY { get; init; } = 0.05;
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
}
