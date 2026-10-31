using System;

namespace SecondSelf.Core.Models;

/// <summary>
/// Результат расчёта прогресса за месяц по формуле 30% Daily / 70% Weekly и правилу «Честные 100%».
/// </summary>
public sealed record MonthlyProgressResult
{
    public required int Year { get; init; }
    public required int Month { get; init; }
    public required int CompletedDays { get; init; }
    public required int TotalDaysInMonth { get; init; }
    public required int CompletedWeeks { get; init; }
    public required int TotalWeeksInMonth { get; init; }
    public required double DailyWeight { get; init; }
    public required double WeeklyWeight { get; init; }
    public required double RawScore { get; init; }
    public required int DisplayScore { get; init; }
    public required bool IsHonest100Capped { get; init; }
    public required string DailyRatio { get; init; }
    public required string WeeklyRatio { get; init; }
}
