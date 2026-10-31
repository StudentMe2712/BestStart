using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SecondSelf.Data.Entities;

namespace SecondSelf.Core.ViewModels;

/// <summary>
/// ViewModel Большой цели (Big Goal, горизонт 1–3 года).
/// Включает коллекцию этапов (Milestones), расчет прогресса завершения этапов
/// и текстовые метрики ("1 of 4 milestones").
/// ВАЖНО: прогресс продвигается ТОЛЬКО при явном завершении этапа (Milestone completed)!
/// </summary>
public partial class BigGoalViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string? _motivation;

    [ObservableProperty]
    private string? _targetResult;

    [ObservableProperty]
    private string? _targetPeriod;

    [ObservableProperty]
    private string _iconKey = "Compass";

    [ObservableProperty]
    private string _status = "Active";

    [ObservableProperty]
    private string _createdAt = DateTime.UtcNow.ToString("O");

    public ObservableCollection<MilestoneItemViewModel> Milestones { get; } = new();

    public int CompletedMilestonesCount => Milestones.Count(m => m.IsCompleted);

    public int TotalMilestonesCount => Milestones.Count;

    public string MilestonesProgressText => $"{CompletedMilestonesCount} of {TotalMilestonesCount} milestones";

    public double ProgressPercentage => TotalMilestonesCount > 0
        ? ((double)CompletedMilestonesCount / TotalMilestonesCount) * 100.0
        : 0.0;

    public BigGoalViewModel()
    {
        Milestones.CollectionChanged += OnMilestonesCollectionChanged;
    }

    public BigGoalViewModel(BigGoal entity) : this()
    {
        ArgumentNullException.ThrowIfNull(entity);
        Id = entity.Id;
        Title = entity.Title;
        Motivation = entity.Motivation;
        TargetResult = entity.TargetResult;
        TargetPeriod = entity.TargetPeriod;
        IconKey = string.IsNullOrWhiteSpace(entity.IconKey) ? "Compass" : entity.IconKey;
        Status = entity.Status;
        CreatedAt = entity.CreatedAt;

        if (entity.Milestones != null)
        {
            foreach (var m in entity.Milestones.OrderBy(x => x.OrderIndex))
            {
                var mvm = new MilestoneItemViewModel(m.Id, m.GoalId, m.OrderIndex, m.Title, m.Status, m.CompletedAt);
                mvm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName is nameof(MilestoneItemViewModel.Status) or nameof(MilestoneItemViewModel.IsCompleted))
                    {
                        RefreshMilestoneProgress();
                    }
                };
                Milestones.Add(mvm);
            }
        }

        RefreshMilestoneProgress();
    }

    private void OnMilestonesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (MilestoneItemViewModel item in e.NewItems)
            {
                item.PropertyChanged += (_, ev) =>
                {
                    if (ev.PropertyName is nameof(MilestoneItemViewModel.Status) or nameof(MilestoneItemViewModel.IsCompleted))
                    {
                        RefreshMilestoneProgress();
                    }
                };
            }
        }
        RefreshMilestoneProgress();
    }

    public void RefreshMilestoneProgress()
    {
        OnPropertyChanged(nameof(CompletedMilestonesCount));
        OnPropertyChanged(nameof(TotalMilestonesCount));
        OnPropertyChanged(nameof(MilestonesProgressText));
        OnPropertyChanged(nameof(ProgressPercentage));
    }
}
