using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecondSelf.Core.Services;
using SecondSelf.Data.Entities;
using SecondSelf.Data.Repositories;

namespace SecondSelf.Core.ViewModels;

/// <summary>
/// ViewModel второго раздела Dashboard — Goals (Большие цели и дорожная карта этапов).
/// Реализует концепт THE BIG PICTURE · 2026—2028, баннер Next Milestone,
/// жесткое ограничение на максимум 4 активные Большие цели с тултипом предупреждения,
/// экран детального просмотра цели и завершение этапов (Milestones).
/// </summary>
public partial class GoalsTabViewModel : ObservableObject
{
    public const string HeaderCategoryText = "THE BIG PICTURE · 2026—2028";
    public const string HeaderTitleText = "Build the life you want.";
    public const string HeaderSubtitleText = "Small decisions. Meaningful progress. One step at a time.";
    public const string MaxGoalsWarningMessage = "Одновременно можно вести до 4 Больших целей для удержания фокуса. Завершите или архивируйте одну из текущих целей.";

    private readonly IBigGoalRepository _bigGoalRepository;
    private readonly INotificationBus? _notificationBus;

    public string HeaderCategory => HeaderCategoryText;
    public string HeaderTitle => HeaderTitleText;
    public string HeaderSubtitle => HeaderSubtitleText;
    public string LimitWarningMessage => MaxGoalsWarningMessage;

    // --- Active Goals List ---
    public ObservableCollection<BigGoalViewModel> ActiveGoals { get; } = new();

    [ObservableProperty]
    private bool _canAddGoal = true;

    [ObservableProperty]
    private bool _isLimitWarningVisible;

    // --- Next Milestone Banner ---
    [ObservableProperty]
    private string _nextMilestoneTitle = string.Empty;

    [ObservableProperty]
    private string _nextMilestoneLabel = "Your next milestone · 01 / 04";

    [ObservableProperty]
    private bool _hasNextMilestone;

    // --- Goal Detail View ---
    [ObservableProperty]
    private BigGoalViewModel? _selectedGoal;

    [ObservableProperty]
    private bool _isGoalDetailOpen;

    // --- Add Goal Modal Fields ---
    [ObservableProperty]
    private bool _isAddGoalModalOpen;

    [ObservableProperty]
    private string _newGoalTitle = string.Empty;

    [ObservableProperty]
    private string _newGoalMotivation = string.Empty;

    [ObservableProperty]
    private string _newGoalTargetResult = string.Empty;

    [ObservableProperty]
    private string _newGoalTargetPeriod = string.Empty;

    [ObservableProperty]
    private string _newGoalIconKey = "Compass";

    public ObservableCollection<string> NewGoalInitialMilestones { get; } = new();

    [ObservableProperty]
    private string _newMilestoneTitleInput = string.Empty;

    public GoalsTabViewModel(
        IBigGoalRepository bigGoalRepository,
        INotificationBus? notificationBus = null)
    {
        _bigGoalRepository = bigGoalRepository ?? throw new ArgumentNullException(nameof(bigGoalRepository));
        _notificationBus = notificationBus;
    }

    [RelayCommand]
    public async Task LoadGoalsAsync()
    {
        var entities = await _bigGoalRepository.GetActiveGoalsAsync();
        ActiveGoals.Clear();

        foreach (var entity in entities)
        {
            ActiveGoals.Add(new BigGoalViewModel(entity));
        }

        CanAddGoal = ActiveGoals.Count < 4;
        if (CanAddGoal)
        {
            IsLimitWarningVisible = false;
        }

        UpdateNextMilestoneBanner();

        // Если была открыта цель, обновляем ссылку
        if (SelectedGoal != null)
        {
            SelectedGoal = ActiveGoals.FirstOrDefault(g => g.Id == SelectedGoal.Id);
            if (SelectedGoal == null)
            {
                IsGoalDetailOpen = false;
            }
        }
    }

    private void UpdateNextMilestoneBanner()
    {
        foreach (var goal in ActiveGoals)
        {
            var upcomingMs = goal.Milestones.FirstOrDefault(m => !m.IsCompleted);
            if (upcomingMs != null)
            {
                NextMilestoneTitle = upcomingMs.Title;
                NextMilestoneLabel = $"Your next milestone · {upcomingMs.OrderIndex:D2} / {goal.Milestones.Count:D2}";
                HasNextMilestone = true;
                return;
            }
        }

        NextMilestoneTitle = "Все вехи текущих целей достигнуты";
        NextMilestoneLabel = "Next milestone";
        HasNextMilestone = false;
    }

    [RelayCommand]
    public void OpenAddGoalModal()
    {
        if (ActiveGoals.Count >= 4)
        {
            CanAddGoal = false;
            IsLimitWarningVisible = true;
            return;
        }

        IsLimitWarningVisible = false;
        NewGoalTitle = string.Empty;
        NewGoalMotivation = string.Empty;
        NewGoalTargetResult = string.Empty;
        NewGoalTargetPeriod = string.Empty;
        NewGoalIconKey = "Compass";
        NewGoalInitialMilestones.Clear();
        NewMilestoneTitleInput = string.Empty;
        IsAddGoalModalOpen = true;
    }

    [RelayCommand]
    public void CloseAddGoalModal()
    {
        IsAddGoalModalOpen = false;
        IsLimitWarningVisible = false;
    }

    [RelayCommand]
    public void AddMilestoneToNewGoal()
    {
        var text = NewMilestoneTitleInput?.Trim();
        if (!string.IsNullOrWhiteSpace(text))
        {
            NewGoalInitialMilestones.Add(text);
            NewMilestoneTitleInput = string.Empty;
        }
    }

    [RelayCommand]
    public void RemoveMilestoneFromNewGoal(string title)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            NewGoalInitialMilestones.Remove(title);
        }
    }

    [RelayCommand]
    public async Task SaveNewGoalAsync()
    {
        if (ActiveGoals.Count >= 4)
        {
            CanAddGoal = false;
            IsLimitWarningVisible = true;
            throw new InvalidOperationException("Превышен лимит активных целей: одновременно можно вести не более 4 Больших целей.");
        }

        if (string.IsNullOrWhiteSpace(NewGoalTitle))
        {
            return;
        }

        var goal = new BigGoal
        {
            Id = Guid.NewGuid().ToString(),
            Title = NewGoalTitle.Trim(),
            Motivation = string.IsNullOrWhiteSpace(NewGoalMotivation) ? null : NewGoalMotivation.Trim(),
            TargetResult = string.IsNullOrWhiteSpace(NewGoalTargetResult) ? null : NewGoalTargetResult.Trim(),
            TargetPeriod = string.IsNullOrWhiteSpace(NewGoalTargetPeriod) ? null : NewGoalTargetPeriod.Trim(),
            IconKey = string.IsNullOrWhiteSpace(NewGoalIconKey) ? "Compass" : NewGoalIconKey,
            Status = "Active",
            CreatedAt = DateTime.UtcNow.ToString("O")
        };

        var initialMilestones = new List<Milestone>();
        int order = 1;
        foreach (var msTitle in NewGoalInitialMilestones)
        {
            initialMilestones.Add(new Milestone
            {
                Id = Guid.NewGuid().ToString(),
                GoalId = goal.Id,
                OrderIndex = order++,
                Title = msTitle,
                Status = order == 2 ? "InProgress" : "Upcoming"
            });
        }

        await _bigGoalRepository.AddGoalAsync(goal, initialMilestones);

        IsAddGoalModalOpen = false;
        await LoadGoalsAsync();
    }

    [RelayCommand]
    public void SelectGoal(BigGoalViewModel goal)
    {
        SelectedGoal = goal;
        IsGoalDetailOpen = goal != null;
    }

    [RelayCommand]
    public void CloseGoalDetail()
    {
        SelectedGoal = null;
        IsGoalDetailOpen = false;
    }

    [RelayCommand]
    public async Task CompleteMilestoneAsync(string milestoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(milestoneId);

        await _bigGoalRepository.CompleteMilestoneAsync(milestoneId);

        // Обновляем локальную модель
        if (SelectedGoal != null)
        {
            var ms = SelectedGoal.Milestones.FirstOrDefault(m => m.Id == milestoneId);
            if (ms != null)
            {
                ms.Status = "Completed";
                ms.CompletedAt = DateTime.UtcNow.ToString("O");
                SelectedGoal.RefreshMilestoneProgress();
            }
        }
        else
        {
            foreach (var goal in ActiveGoals)
            {
                var ms = goal.Milestones.FirstOrDefault(m => m.Id == milestoneId);
                if (ms != null)
                {
                    ms.Status = "Completed";
                    ms.CompletedAt = DateTime.UtcNow.ToString("O");
                    goal.RefreshMilestoneProgress();
                    break;
                }
            }
        }

        UpdateNextMilestoneBanner();
    }

    [RelayCommand]
    public async Task ArchiveGoalAsync(string goalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goalId);

        await _bigGoalRepository.ArchiveGoalAsync(goalId);

        if (SelectedGoal?.Id == goalId)
        {
            CloseGoalDetail();
        }

        await LoadGoalsAsync();
    }
}
