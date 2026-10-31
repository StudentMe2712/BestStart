using System;
using System.Collections.Generic;
using SecondSelf.Core.Models;

namespace SecondSelf.Core.Services;

/// <summary>
/// Движок расчёта прогресса месяца по стандарту ISO-8601 и правилу «Честные 100%».
/// </summary>
public interface IProgressCalculationEngine
{
    /// <summary>
    /// Возвращает список всех ISO-8601 недель, чей четверг попадает в указанный месяц года.
    /// В любом месяце гарантированно возвращается строго 4 или 5 недель.
    /// </summary>
    IReadOnlyList<IsoWeekInfo> GetIsoWeeksForMonth(int year, int month);

    /// <summary>
    /// Возвращает информацию об ISO-неделе для произвольной даты.
    /// </summary>
    IsoWeekInfo GetIsoWeekForDate(DateOnly date);

    /// <summary>
    /// Рассчитывает прогресс месяца на основе закрытых дней и закрытых недель.
    /// По формуле: RawScore = DailyWeight * D + WeeklyWeight * W, где веса по умолчанию 0.30 и 0.70.
    /// Правило «Честные 100%»: если не все дни или недели закрыты, результат не может превышать 99%.
    /// </summary>
    MonthlyProgressResult CalculateMonthlyProgress(
        int year,
        int month,
        int completedDays,
        int completedWeeks,
        double dailyWeight = 0.30,
        double weeklyWeight = 0.70);

    /// <summary>
    /// Удобная перегрузка для расчёта прогресса по наборам выполненных дат и недель.
    /// </summary>
    MonthlyProgressResult CalculateMonthlyProgress(
        int year,
        int month,
        IEnumerable<DateOnly> completedDates,
        IEnumerable<IsoWeekInfo> completedWeeks,
        double dailyWeight = 0.30,
        double weeklyWeight = 0.70);
}
