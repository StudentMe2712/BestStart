namespace SecondSelf.Core.ViewModels;

/// <summary>
/// Элемент выпадающего списка этапов Больших целей для привязки к недельной цели (Weekly Goal).
/// </summary>
public sealed class MilestoneLookupItem
{
    public required string MilestoneId { get; init; }
    public required string GoalId { get; init; }
    public required string GoalTitle { get; init; }
    public required string MilestoneTitle { get; init; }
    public int OrderIndex { get; init; }

    public string DisplayName => $"[{GoalTitle} · {MilestoneTitle}]";

    public override string ToString() => DisplayName;
}
