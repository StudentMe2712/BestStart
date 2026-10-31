using System;

namespace SecondSelf.Data.Entities;

/// <summary>
/// Сущность промежуточного этапа достижения Большой цели.
/// </summary>
public sealed class Milestone
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string GoalId { get; set; } = string.Empty;
    public int OrderIndex { get; set; } = 1;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = "Upcoming"; // 'Completed', 'InProgress', 'Upcoming', 'Future'
    public string? CompletedAt { get; set; }
}
