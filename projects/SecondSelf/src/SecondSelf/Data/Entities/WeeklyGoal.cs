namespace SecondSelf.Data.Entities;

/// <summary>
/// Сущность цели календарной недели (Weekly Goal).
/// </summary>
public sealed class WeeklyGoal
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int IsoYear { get; set; }
    public int IsoWeek { get; set; }
    public string BelongsToMonth { get; set; } = string.Empty; // 'YYYY-MM'
    public string? MilestoneId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = "Active"; // 'Active', 'Locked', 'Completed', 'Postponed', 'Cancelled'
    public string? LockedAt { get; set; }
    public string? CompletedAt { get; set; }
    public string? CancellationReason { get; set; }
}
