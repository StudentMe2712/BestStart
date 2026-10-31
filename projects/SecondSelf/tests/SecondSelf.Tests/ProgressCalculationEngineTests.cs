using System;
using System.Linq;
using SecondSelf.Core.Models;
using SecondSelf.Core.Services;
using Xunit;

namespace SecondSelf.Tests;

public class ProgressCalculationEngineTests
{
    private readonly ProgressCalculationEngine _engine = new();

    [Fact]
    public void GetIsoWeeksForMonth_February_HasStrictly4Weeks()
    {
        // В феврале 2026 года (невисокосный): 28 дней, 4 четверга
        var weeks = _engine.GetIsoWeeksForMonth(2026, 2);

        Assert.Equal(4, weeks.Count);
        foreach (var week in weeks)
        {
            Assert.Equal(DayOfWeek.Thursday, week.ThursdayDate.DayOfWeek);
            Assert.Equal(DayOfWeek.Monday, week.StartDateMonday.DayOfWeek);
            Assert.Equal(DayOfWeek.Sunday, week.EndDateSunday.DayOfWeek);
            Assert.Equal(week.ThursdayDate.AddDays(-3), week.StartDateMonday);
            Assert.Equal(week.ThursdayDate.AddDays(3), week.EndDateSunday);
        }
    }

    [Fact]
    public void GetIsoWeeksForMonth_MonthWith5Thursdays_HasStrictly5Weeks()
    {
        // Октябрь 2026: 1-е число — четверг (четверги: 1, 8, 15, 22, 29) -> строго 5 недель
        var weeks = _engine.GetIsoWeeksForMonth(2026, 10);

        Assert.Equal(5, weeks.Count);
        Assert.Equal(new DateOnly(2026, 10, 1), weeks[0].ThursdayDate);
        Assert.Equal(new DateOnly(2026, 10, 29), weeks[4].ThursdayDate);
    }

    [Theory]
    [InlineData(2024)] // Високосный
    [InlineData(2025)]
    [InlineData(2026)]
    public void GetIsoWeeksForMonth_AllMonths_HaveStrictly4Or5Weeks(int year)
    {
        for (int month = 1; month <= 12; month++)
        {
            var weeks = _engine.GetIsoWeeksForMonth(year, month);
            Assert.True(weeks.Count is 4 or 5, $"Месяц {month}/{year} имеет {weeks.Count} недель вместо 4 или 5.");
        }
    }

    [Fact]
    public void GetIsoWeekForDate_CorrectlyDeterminesIsoWeekAndThursday()
    {
        // 31 октября 2026 (суббота): четверг этой недели — 29 октября 2026
        var saturday = new DateOnly(2026, 10, 31);
        var weekInfo = _engine.GetIsoWeekForDate(saturday);

        Assert.Equal(new DateOnly(2026, 10, 29), weekInfo.ThursdayDate);
        Assert.Equal(new DateOnly(2026, 10, 26), weekInfo.StartDateMonday);
        Assert.Equal(new DateOnly(2026, 11, 1), weekInfo.EndDateSunday);
        Assert.Equal(2026, weekInfo.IsoYear);
        Assert.Equal(44, weekInfo.IsoWeek);
    }

    [Fact]
    public void CalculateMonthlyProgress_AC09_Scenario1_Rounds82Point5To83()
    {
        // AC-09: 31 день, 4 недели (например, Март 2026: 31 день, четверги 5, 12, 19, 26 = 4 недели).
        // Закрыто 31 день (100%) и 3 из 4 недель (75%).
        // P = 0.30 * 100 + 0.70 * 75 = 30 + 52.5 = 82.5% -> DisplayScore = 83%.
        var result = _engine.CalculateMonthlyProgress(2026, 3, completedDays: 31, completedWeeks: 3);

        Assert.Equal(31, result.TotalDaysInMonth);
        Assert.Equal(4, result.TotalWeeksInMonth);
        Assert.Equal(31, result.CompletedDays);
        Assert.Equal(3, result.CompletedWeeks);
        Assert.Equal(82.5, result.RawScore, 2);
        Assert.Equal(83, result.DisplayScore);
        Assert.False(result.IsHonest100Capped);
        Assert.Equal("31/31", result.DailyRatio);
        Assert.Equal("3/4", result.WeeklyRatio);
    }

    [Fact]
    public void CalculateMonthlyProgress_AC09_Scenario2_30Of31DaysYields99()
    {
        // AC-09: 30 из 31 дня и 4 из 4 недель.
        // P = 0.30 * (30/31 * 100) + 0.70 * 100 = 29.032... + 70 = 99.032...%
        // DisplayScore = 99%.
        var result = _engine.CalculateMonthlyProgress(2026, 3, completedDays: 30, completedWeeks: 4);

        Assert.Equal(99, result.DisplayScore);
        Assert.Equal("30/31", result.DailyRatio);
        Assert.Equal("4/4", result.WeeklyRatio);
    }

    [Fact]
    public void CalculateMonthlyProgress_Honest100Rule_CapsRoundingTo99WhenMissExists()
    {
        // Допустим, веса 0.99 daily и 0.01 weekly.
        // Дни: 31 из 31 (100%), недели: 3 из 4 (75%).
        // P_raw = 0.99 * 100 + 0.01 * 75 = 99 + 0.75 = 99.75% -> обычное округление дало бы 100!
        // Но так как одна неделя пропущена, правило «Честные 100%» строго ограничивает DisplayScore = 99
        var result = _engine.CalculateMonthlyProgress(
            2026, 3, completedDays: 31, completedWeeks: 3, dailyWeight: 0.99, weeklyWeight: 0.01);

        Assert.True(result.RawScore >= 99.5);
        Assert.Equal(99, result.DisplayScore);
        Assert.True(result.IsHonest100Capped);
    }

    [Fact]
    public void CalculateMonthlyProgress_Honest100Rule_Allows100OnlyWhenAllDaysAndWeeksCompleted()
    {
        // 31 из 31 дней и 4 из 4 недель закрыты -> ровно 100%
        var result = _engine.CalculateMonthlyProgress(2026, 3, completedDays: 31, completedWeeks: 4);

        Assert.Equal(100.0, result.RawScore, 2);
        Assert.Equal(100, result.DisplayScore);
        Assert.False(result.IsHonest100Capped);
    }

    [Fact]
    public void CalculateMonthlyProgress_WithCollections_CalculatesCorrectly()
    {
        var completedDates = new[]
        {
            new DateOnly(2026, 3, 1),
            new DateOnly(2026, 3, 2),
            new DateOnly(2026, 3, 3)
        };

        var allMonthWeeks = _engine.GetIsoWeeksForMonth(2026, 3);
        var completedWeeks = allMonthWeeks.Take(2).ToList();

        var result = _engine.CalculateMonthlyProgress(2026, 3, completedDates, completedWeeks);

        Assert.Equal(3, result.CompletedDays);
        Assert.Equal(2, result.CompletedWeeks);
        Assert.Equal("3/31", result.DailyRatio);
        Assert.Equal("2/4", result.WeeklyRatio);
    }

    [Fact]
    public void CalculateMonthlyProgress_ValidatesNegativeArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.CalculateMonthlyProgress(2026, 3, -1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.CalculateMonthlyProgress(2026, 3, 1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.CalculateMonthlyProgress(2026, 3, 1, 1, -0.1, 0.7));
        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.CalculateMonthlyProgress(2026, 3, 1, 1, 0.3, -0.1));
    }
}
