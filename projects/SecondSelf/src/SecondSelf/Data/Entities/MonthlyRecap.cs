using System;

namespace SecondSelf.Data.Entities;

/// <summary>
/// Сущность финализированного ежемесячного отчёта (Monthly Recap).
/// </summary>
public sealed class MonthlyRecap
{
    public string YearMonth { get; set; } = string.Empty; // 'YYYY-MM'
    public double FinalScore { get; set; } = 0;
    public string DailyRatio { get; set; } = "0/0";
    public string WeeklyRatio { get; set; } = "0/0";
    public double DailyWeight { get; set; } = 0.30;
    public double WeeklyWeight { get; set; } = 0.70;
    public string? ReflectionText { get; set; }
    public bool IsFinalized { get; set; } = false;
    public string CompletedAt { get; set; } = DateTime.UtcNow.ToString("O");
}
