using System;

namespace SecondSelf.Data.Entities;

/// <summary>
/// Сущность активной задачи дня из Today's Focus.
/// </summary>
public sealed class ActiveTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("O");
    public int OrderIndex { get; set; } = 0;
    public int PostponedDays { get; set; } = 0;
}
