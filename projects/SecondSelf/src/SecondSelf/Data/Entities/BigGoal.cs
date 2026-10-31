using System;

namespace SecondSelf.Data.Entities;

/// <summary>
/// Сущность Большой цели горизонта 1–3 года (максимум 4 активных).
/// </summary>
public sealed class BigGoal
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string? Motivation { get; set; }
    public string? TargetResult { get; set; }
    public string? TargetPeriod { get; set; }
    public string IconKey { get; set; } = "Compass";
    public string Status { get; set; } = "Active";
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("O");
}
