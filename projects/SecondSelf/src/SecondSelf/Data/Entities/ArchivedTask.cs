using System;

namespace SecondSelf.Data.Entities;

/// <summary>
/// Сущность закрытой задачи дня или недели (Хроника побед).
/// </summary>
public sealed class ArchivedTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? OriginalTaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TaskSource { get; set; } = "Today";
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("O");
    public string CompletedAt { get; set; } = DateTime.UtcNow.ToString("O");
    public string LogicalDate { get; set; } = string.Empty;
}
