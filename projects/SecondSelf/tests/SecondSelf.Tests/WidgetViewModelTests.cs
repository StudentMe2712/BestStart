using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SecondSelf.Core.Models;
using SecondSelf.Core.Services;
using SecondSelf.Core.ViewModels;
using SecondSelf.Data.Entities;
using SecondSelf.Data.Repositories;
using Xunit;

namespace SecondSelf.Tests;

public sealed class WidgetViewModelTests
{
    private sealed class FakeTaskRepository : ITaskRepository
    {
        public List<ActiveTask> ActiveTasks { get; } = new();
        public List<ArchivedTask> ArchivedTasks { get; } = new();

        public Task<IReadOnlyList<ActiveTask>> GetActiveTasksAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ActiveTask>>(ActiveTasks.ToArray());
        }

        public Task AddActiveTaskAsync(ActiveTask task, CancellationToken cancellationToken = default)
        {
            ActiveTasks.Insert(0, task);
            return Task.CompletedTask;
        }

        public Task UpdateActiveTaskAsync(ActiveTask task, CancellationToken cancellationToken = default)
        {
            var idx = ActiveTasks.FindIndex(t => t.Id == task.Id);
            if (idx >= 0) ActiveTasks[idx] = task;
            return Task.CompletedTask;
        }

        public Task DeleteActiveTaskAsync(string id, CancellationToken cancellationToken = default)
        {
            ActiveTasks.RemoveAll(t => t.Id == id);
            return Task.CompletedTask;
        }

        public Task<ArchivedTask> ArchiveTaskAsync(string taskId, string logicalDate, CancellationToken cancellationToken = default)
        {
            var active = ActiveTasks.Find(t => t.Id == taskId);
            if (active != null) ActiveTasks.Remove(active);

            var archived = new ArchivedTask
            {
                Id = taskId,
                Title = active?.Title ?? "Archived",
                LogicalDate = logicalDate,
                CompletedAt = DateTime.UtcNow.ToString("O")
            };
            ArchivedTasks.Add(archived);
            return Task.FromResult(archived);
        }

        public Task<ActiveTask> RestoreArchivedTaskAsync(string taskId, CancellationToken cancellationToken = default)
        {
            var archived = ArchivedTasks.Find(t => t.Id == taskId || t.OriginalTaskId == taskId);
            if (archived != null) ArchivedTasks.Remove(archived);

            var active = new ActiveTask
            {
                Id = archived?.OriginalTaskId ?? archived?.Id ?? taskId,
                Title = archived?.Title ?? "Restored",
                OrderIndex = 0,
                PostponedDays = 0,
                CreatedAt = DateTime.UtcNow.ToString("O")
            };
            ActiveTasks.Insert(0, active);
            return Task.FromResult(active);
        }

        public Task<IReadOnlyList<ArchivedTask>> GetArchivedTasksAsync(string? logicalDate = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ArchivedTask>>(ArchivedTasks.ToArray());
        }

        public Task ReorderTasksAsync(IEnumerable<(string Id, int NewOrder)> orders, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProgressCalculationEngine : IProgressCalculationEngine
    {
        public MonthlyProgressResult CalculateMonthlyProgress(int year, int month, int completedDays, int completedWeeks, double dailyWeight = 0.3, double weeklyWeight = 0.7)
        {
            return new MonthlyProgressResult
            {
                Year = year,
                Month = month,
                CompletedDays = completedDays,
                TotalDaysInMonth = 31,
                CompletedWeeks = completedWeeks,
                TotalWeeksInMonth = 4,
                DailyWeight = dailyWeight,
                WeeklyWeight = weeklyWeight,
                RawScore = 78.0,
                DisplayScore = 78,
                IsHonest100Capped = false,
                DailyRatio = $"Daily {completedDays}/31",
                WeeklyRatio = $"Weekly {completedWeeks}/4"
            };
        }

        public MonthlyProgressResult CalculateMonthlyProgress(int year, int month, IEnumerable<DateOnly> completedDates, IEnumerable<IsoWeekInfo> completedWeeks, double dailyWeight = 0.3, double weeklyWeight = 0.7)
        {
            return CalculateMonthlyProgress(year, month, 26, 3, dailyWeight, weeklyWeight);
        }

        public IsoWeekInfo GetIsoWeekForDate(DateOnly date) => throw new NotImplementedException();
        public IReadOnlyList<IsoWeekInfo> GetIsoWeeksForMonth(int year, int month) => throw new NotImplementedException();
    }

    [Fact]
    public async Task LoadDataAsync_PopulatesTasksAndCompactTasks()
    {
        var repo = new FakeTaskRepository();
        repo.ActiveTasks.Add(new ActiveTask { Id = "1", Title = "Task 1", OrderIndex = 0 });
        repo.ActiveTasks.Add(new ActiveTask { Id = "2", Title = "Task 2", OrderIndex = 1 });
        repo.ActiveTasks.Add(new ActiveTask { Id = "3", Title = "Task 3", OrderIndex = 2 });
        repo.ActiveTasks.Add(new ActiveTask { Id = "4", Title = "Task 4", OrderIndex = 3 });

        var engine = new FakeProgressCalculationEngine();
        var vm = new WidgetViewModel(repo, engine);

        await vm.LoadDataAsync();

        Assert.Equal(4, vm.Tasks.Count);
        Assert.Equal(3, vm.CompactTasks.Count); // Compact only takes up to 3
        Assert.Equal("Task 1", vm.CompactTasks[0].Title);
        Assert.Equal(78, vm.DisplayScore);
        Assert.Equal("Daily 26/31", vm.DailyRatio);
        Assert.Equal("Weekly 3/4", vm.WeeklyRatio);
    }

    [Fact]
    public async Task AddTaskAsync_AddsTaskToStartAndUpdatesRepository()
    {
        var repo = new FakeTaskRepository();
        var engine = new FakeProgressCalculationEngine();
        var vm = new WidgetViewModel(repo, engine);

        vm.NewTaskTitle = "Buy groceries";
        await vm.AddTaskAsync();

        Assert.Single(vm.Tasks);
        Assert.Equal("Buy groceries", vm.Tasks[0].Title);
        Assert.Single(vm.CompactTasks);
        Assert.Single(repo.ActiveTasks);
        Assert.Equal("Buy groceries", repo.ActiveTasks[0].Title);
        Assert.Empty(vm.NewTaskTitle);
        Assert.True(vm.IsAddingTask); // Готово к следующему вводу по спецификации AC-04
    }

    [Fact]
    public async Task ToggleTaskAsync_ArchivesTaskAndRemovesFromList()
    {
        var repo = new FakeTaskRepository();
        repo.ActiveTasks.Add(new ActiveTask { Id = "t1", Title = "Read book", OrderIndex = 0 });

        var engine = new FakeProgressCalculationEngine();
        var vm = new WidgetViewModel(repo, engine)
        {
            AnimationPhase1Ms = 0,
            AnimationPhase2Ms = 0
        };
        await vm.LoadDataAsync();

        var item = vm.Tasks[0];
        await vm.ToggleTaskAsync(item);

        Assert.Empty(vm.Tasks);
        Assert.Empty(vm.CompactTasks);

        // Фиксация архивации (по истечении таймера Undo)
        await vm.CommitPendingArchivedTaskAsync();
        Assert.Empty(repo.ActiveTasks);
        Assert.Single(repo.ArchivedTasks);
        Assert.Equal("t1", repo.ArchivedTasks[0].Id);
    }

    [Fact]
    public async Task DeleteTaskAsync_DeletesFromRepositoryAndList()
    {
        var repo = new FakeTaskRepository();
        repo.ActiveTasks.Add(new ActiveTask { Id = "t2", Title = "Workout", OrderIndex = 0 });

        var engine = new FakeProgressCalculationEngine();
        var vm = new WidgetViewModel(repo, engine);
        await vm.LoadDataAsync();

        var item = vm.Tasks[0];
        await vm.DeleteTaskAsync(item);

        Assert.Empty(vm.Tasks);
        Assert.Empty(repo.ActiveTasks);
    }

    [Fact]
    public void ToggleExpanded_TogglesStateAndFiresEvent()
    {
        var repo = new FakeTaskRepository();
        var engine = new FakeProgressCalculationEngine();
        var vm = new WidgetViewModel(repo, engine);

        bool eventFired = false;
        bool lastState = false;
        vm.ExpandedStateChanged += (state) =>
        {
            eventFired = true;
            lastState = state;
        };

        Assert.False(vm.IsExpanded);

        vm.ToggleExpanded();
        Assert.True(vm.IsExpanded);
        Assert.True(eventFired);
        Assert.True(lastState);

        vm.Collapse();
        Assert.False(vm.IsExpanded);
        Assert.False(lastState);
    }

    [Fact]
    public void DisplayScoreChanged_CalculatesCorrectProgressBarWidth()
    {
        var repo = new FakeTaskRepository();
        var engine = new FakeProgressCalculationEngine();
        var vm = new WidgetViewModel(repo, engine);

        vm.DisplayScore = 50;

        Assert.Equal(50.0, vm.ProgressPercentage);
        Assert.Equal(170.0, vm.ProgressBarWidth);
        Assert.Equal("This Month: 50%", vm.CompactProgressText);
    }
}
