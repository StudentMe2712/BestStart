namespace Stepwise.Core.Interfaces;

/// <summary>
/// Предоставляет системные метрики для распознавания пользовательских взаимодействий (например, параметры двойного клика).
/// </summary>
public interface ISystemMetricsProvider
{
    /// <summary>
    /// Максимальный временной интервал в миллисекундах между кликами для распознавания двойного клика.
    /// </summary>
    int DoubleClickTimeMs { get; }

    /// <summary>
    /// Максимальная ширина прямоугольника в пикселях, в пределах которого должен произойти второй клик.
    /// </summary>
    int DoubleClickWidth { get; }

    /// <summary>
    /// Максимальная высота прямоугольника в пикселях, в пределах которого должен произойти второй клик.
    /// </summary>
    int DoubleClickHeight { get; }
}

/// <summary>
/// Реализация метрик по умолчанию со стандартными значениями Windows (500 мс, 4x4 пикселя) для тестов и резервной работы.
/// </summary>
public sealed class DefaultSystemMetricsProvider : ISystemMetricsProvider
{
    /// <summary>
    /// Стандартное время двойного клика по умолчанию в Windows (500 мс).
    /// </summary>
    public const int DefaultDoubleClickTimeMs = 500;

    /// <summary>
    /// Стандартная ширина зоны двойного клика по умолчанию в Windows (4 пикселя).
    /// </summary>
    public const int DefaultDoubleClickWidth = 4;

    /// <summary>
    /// Стандартная высота зоны двойного клика по умолчанию в Windows (4 пикселя).
    /// </summary>
    public const int DefaultDoubleClickHeight = 4;

    /// <summary>
    /// Экземпляр по умолчанию.
    /// </summary>
    public static DefaultSystemMetricsProvider Instance { get; } = new();

    /// <inheritdoc />
    public int DoubleClickTimeMs { get; }

    /// <inheritdoc />
    public int DoubleClickWidth { get; }

    /// <inheritdoc />
    public int DoubleClickHeight { get; }

    /// <summary>
    /// Создает экземпляр <see cref="DefaultSystemMetricsProvider"/> с указанными или стандартными значениями.
    /// </summary>
    public DefaultSystemMetricsProvider(
        int doubleClickTimeMs = DefaultDoubleClickTimeMs,
        int doubleClickWidth = DefaultDoubleClickWidth,
        int doubleClickHeight = DefaultDoubleClickHeight)
    {
        if (doubleClickTimeMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(doubleClickTimeMs), "DoubleClickTimeMs must be greater than zero.");
        }

        if (doubleClickWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(doubleClickWidth), "DoubleClickWidth must be greater than zero.");
        }

        if (doubleClickHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(doubleClickHeight), "DoubleClickHeight must be greater than zero.");
        }

        DoubleClickTimeMs = doubleClickTimeMs;
        DoubleClickWidth = doubleClickWidth;
        DoubleClickHeight = doubleClickHeight;
    }
}
