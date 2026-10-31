namespace SecondSelf.Core.Models;

/// <summary>
/// Модель пользовательских настроек приложения Second Self.
/// </summary>
public sealed class AppSettings
{
    public const double DefaultDailyWeight = 0.30;
    public const double DefaultWeeklyWeight = 0.70;
    public const double MinDailyWeight = 0.10;
    public const double MaxDailyWeight = 0.50;

    /// <summary>
    /// Вес ежедневного ритуала в формуле месячного прогресса (по умолчанию 0.30, диапазон от 0.10 до 0.50).
    /// </summary>
    public double DailyWeight { get; set; } = DefaultDailyWeight;

    /// <summary>
    /// Вес недельных целей в формуле месячного прогресса (вычисляется как 1.0 - DailyWeight, по умолчанию 0.70).
    /// </summary>
    public double WeeklyWeight { get; set; } = DefaultWeeklyWeight;

    /// <summary>
    /// Флаг закрепления окна виджета поверх всех окон (Always on Top).
    /// </summary>
    public bool AlwaysOnTop { get; set; } = true;

    /// <summary>
    /// Автоматическое сворачивание виджета в компактную таблетку при потере фокуса.
    /// </summary>
    public bool AutoCollapse { get; set; } = true;

    /// <summary>
    /// Коэффициент прозрачности стекла виджета (0.0 .. 1.0).
    /// </summary>
    public double GlassOpacity { get; set; } = 0.85;

    /// <summary>
    /// Идентификатор целевого монитора для размещения виджета.
    /// </summary>
    public string PreferredMonitor { get; set; } = "Primary";
}
