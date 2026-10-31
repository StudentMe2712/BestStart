namespace SecondSelf.Data.Entities;

/// <summary>
/// Сущность записи журнала ежедневных ритуалов Daily Reset.
/// </summary>
public sealed class DailyRitualLog
{
    public string LogicalDate { get; set; } = string.Empty; // 'YYYY-MM-DD'
    public string VideoId { get; set; } = string.Empty;
    public int LastPositionMs { get; set; } = 0;
    public bool IsCompleted { get; set; } = false;
    public string? CompletedAt { get; set; }
}
