using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SecondSelf.Core.ViewModels;

/// <summary>
/// ViewModel промежуточного этапа (Milestone) Большой цели.
/// Статусы: 'Completed', 'InProgress', 'Upcoming', 'Future'.
/// </summary>
public partial class MilestoneItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _goalId = string.Empty;

    [ObservableProperty]
    private int _orderIndex = 1;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCompleted))]
    [NotifyPropertyChangedFor(nameof(IsInProgress))]
    private string _status = "Upcoming";

    [ObservableProperty]
    private string? _completedAt;

    public bool IsCompleted => string.Equals(Status, "Completed", StringComparison.OrdinalIgnoreCase);
    public bool IsInProgress => string.Equals(Status, "InProgress", StringComparison.OrdinalIgnoreCase);
    public string DisplayTitle => $"Этап {OrderIndex}: {Title}";

    public MilestoneItemViewModel()
    {
    }

    public MilestoneItemViewModel(string id, string goalId, int orderIndex, string title, string status = "Upcoming", string? completedAt = null)
    {
        Id = id;
        GoalId = goalId;
        OrderIndex = orderIndex;
        Title = title;
        Status = status;
        CompletedAt = completedAt;
    }
}
