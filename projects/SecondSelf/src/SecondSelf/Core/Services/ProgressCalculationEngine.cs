using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SecondSelf.Core.Models;

namespace SecondSelf.Core.Services;

/// <summary>
/// Реализация математического ядра расчёта прогресса по формуле 30/70%, стандарту ISO-8601 и правилу «Честные 100%».
/// </summary>
public sealed class ProgressCalculationEngine : IProgressCalculationEngine
{
    public const double DefaultDailyWeight = 0.30;
    public const double DefaultWeeklyWeight = 0.70;

    /// <inheritdoc />
    public IReadOnlyList<IsoWeekInfo> GetIsoWeeksForMonth(int year, int month)
    {
        ValidateYearMonth(year, month);

        int totalDays = DateTime.DaysInMonth(year, month);
        var weeks = new List<IsoWeekInfo>(5);

        for (int day = 1; day <= totalDays; day++)
        {
            var date = new DateOnly(year, month, day);
            if (date.DayOfWeek == DayOfWeek.Thursday)
            {
                weeks.Add(CreateIsoWeekInfoFromThursday(date));
            }
        }

        return weeks;
    }

    /// <inheritdoc />
    public IsoWeekInfo GetIsoWeekForDate(DateOnly date)
    {
        int isoDay = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
        int diffToThursday = 4 - isoDay;
        var thursday = date.AddDays(diffToThursday);
        return CreateIsoWeekInfoFromThursday(thursday);
    }

    /// <inheritdoc />
    public MonthlyProgressResult CalculateMonthlyProgress(
        int year,
        int month,
        int completedDays,
        int completedWeeks,
        double dailyWeight = DefaultDailyWeight,
        double weeklyWeight = DefaultWeeklyWeight)
    {
        ValidateYearMonth(year, month);

        if (completedDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(completedDays), "Количество выполненных дней не может быть отрицательным.");
        }

        if (completedWeeks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(completedWeeks), "Количество выполненных недель не может быть отрицательным.");
        }

        if (dailyWeight < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(dailyWeight), "Вес ежедневных ритуалов не может быть отрицательным.");
        }

        if (weeklyWeight < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(weeklyWeight), "Вес недельных целей не может быть отрицательным.");
        }

        int totalDaysInMonth = DateTime.DaysInMonth(year, month);
        var isoWeeks = GetIsoWeeksForMonth(year, month);
        int totalWeeksInMonth = isoWeeks.Count;

        // Нормализация (не больше общего количества)
        int clampedCompletedDays = Math.Min(completedDays, totalDaysInMonth);
        int clampedCompletedWeeks = Math.Min(completedWeeks, totalWeeksInMonth);

        double dailyPct = totalDaysInMonth > 0
            ? ((double)clampedCompletedDays / totalDaysInMonth) * 100.0
            : 0.0;

        double weeklyPct = totalWeeksInMonth > 0
            ? ((double)clampedCompletedWeeks / totalWeeksInMonth) * 100.0
            : 0.0;

        double rawScore = (dailyWeight * dailyPct) + (weeklyWeight * weeklyPct);
        int roundedScore = (int)Math.Round(rawScore, MidpointRounding.AwayFromZero);

        bool allDaysCompleted = clampedCompletedDays == totalDaysInMonth;
        bool allWeeksCompleted = clampedCompletedWeeks == totalWeeksInMonth;

        int displayScore;
        bool isHonest100Capped;

        if (allDaysCompleted && allWeeksCompleted)
        {
            // 100% разрешено только при полном закрытии всех дней и всех недель
            displayScore = Math.Min(100, Math.Max(0, roundedScore));
            isHonest100Capped = false;
        }
        else
        {
            // Правило «Честные 100%»: при наличии хотя бы одного пропуска максимум 99%
            if (roundedScore >= 100)
            {
                displayScore = 99;
                isHonest100Capped = true;
            }
            else
            {
                displayScore = Math.Max(0, roundedScore);
                isHonest100Capped = false;
            }
        }

        return new MonthlyProgressResult
        {
            Year = year,
            Month = month,
            CompletedDays = clampedCompletedDays,
            TotalDaysInMonth = totalDaysInMonth,
            CompletedWeeks = clampedCompletedWeeks,
            TotalWeeksInMonth = totalWeeksInMonth,
            DailyWeight = dailyWeight,
            WeeklyWeight = weeklyWeight,
            RawScore = rawScore,
            DisplayScore = displayScore,
            IsHonest100Capped = isHonest100Capped,
            DailyRatio = $"{clampedCompletedDays}/{totalDaysInMonth}",
            WeeklyRatio = $"{clampedCompletedWeeks}/{totalWeeksInMonth}"
        };
    }

    /// <inheritdoc />
    public MonthlyProgressResult CalculateMonthlyProgress(
        int year,
        int month,
        IEnumerable<DateOnly> completedDates,
        IEnumerable<IsoWeekInfo> completedWeeks,
        double dailyWeight = DefaultDailyWeight,
        double weeklyWeight = DefaultWeeklyWeight)
    {
        ValidateYearMonth(year, month);
        ArgumentNullException.ThrowIfNull(completedDates);
        ArgumentNullException.ThrowIfNull(completedWeeks);

        int completedDaysCount = completedDates
            .Where(d => d.Year == year && d.Month == month)
            .Distinct()
            .Count();

        var monthWeeks = GetIsoWeeksForMonth(year, month);
        var monthWeekSet = monthWeeks.Select(w => (w.IsoYear, w.IsoWeek)).ToHashSet();

        int completedWeeksCount = completedWeeks
            .Where(w => monthWeekSet.Contains((w.IsoYear, w.IsoWeek)))
            .Select(w => (w.IsoYear, w.IsoWeek))
            .Distinct()
            .Count();

        return CalculateMonthlyProgress(year, month, completedDaysCount, completedWeeksCount, dailyWeight, weeklyWeight);
    }

    private static IsoWeekInfo CreateIsoWeekInfoFromThursday(DateOnly thursday)
    {
        var monday = thursday.AddDays(-3);
        var sunday = thursday.AddDays(3);
        var dt = thursday.ToDateTime(TimeOnly.MinValue);
        int isoYear = ISOWeek.GetYear(dt);
        int isoWeek = ISOWeek.GetWeekOfYear(dt);

        return new IsoWeekInfo
        {
            IsoYear = isoYear,
            IsoWeek = isoWeek,
            ThursdayDate = thursday,
            StartDateMonday = monday,
            EndDateSunday = sunday
        };
    }

    private static void ValidateYearMonth(int year, int month)
    {
        if (year < 1 || year > 9999)
        {
            throw new ArgumentOutOfRangeException(nameof(year), "Год должен быть в диапазоне от 1 до 9999.");
        }

        if (month < 1 || month > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), "Месяц должен быть в диапазоне от 1 до 12.");
        }
    }
}
