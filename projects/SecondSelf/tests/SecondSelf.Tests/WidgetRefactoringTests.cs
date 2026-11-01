using System;
using System.IO;
using System.Threading.Tasks;
using SecondSelf.Core.Models;
using SecondSelf.Core.Services;
using SecondSelf.Core.ViewModels;
using SecondSelf.Data.Entities;
using SecondSelf.Data.Repositories;
using Xunit;

namespace SecondSelf.Tests;

/// <summary>
/// Тесты верификации рефакторинга UI, устранения чёрного экрана, логики задач и ликвидации крашей (Шаги 2-6).
/// </summary>
public sealed class WidgetRefactoringTests
{
    private sealed class InMemoryTestTaskRepository : ITaskRepository
    {
        public System.Collections.Generic.List<ActiveTask> ActiveTasks { get; } = new();
        public System.Collections.Generic.List<ArchivedTask> ArchivedTasks { get; } = new();

        public Task<System.Collections.Generic.IReadOnlyList<ActiveTask>> GetActiveTasksAsync(System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult<System.Collections.Generic.IReadOnlyList<ActiveTask>>(ActiveTasks);

        public Task AddActiveTaskAsync(ActiveTask task, System.Threading.CancellationToken cancellationToken = default)
        {
            ActiveTasks.Add(task);
            return Task.CompletedTask;
        }

        public Task UpdateActiveTaskAsync(ActiveTask task, System.Threading.CancellationToken cancellationToken = default)
        {
            var existing = ActiveTasks.FindIndex(t => t.Id == task.Id);
            if (existing >= 0) ActiveTasks[existing] = task;
            return Task.CompletedTask;
        }

        public Task DeleteActiveTaskAsync(string id, System.Threading.CancellationToken cancellationToken = default)
        {
            ActiveTasks.RemoveAll(t => t.Id == id);
            return Task.CompletedTask;
        }

        public Task<ArchivedTask> ArchiveTaskAsync(string taskId, string logicalDate, System.Threading.CancellationToken cancellationToken = default)
        {
            var task = ActiveTasks.Find(t => t.Id == taskId);
            if (task != null) ActiveTasks.Remove(task);
            var archived = new ArchivedTask { Id = taskId, Title = task?.Title ?? "Task", LogicalDate = logicalDate };
            ArchivedTasks.Add(archived);
            return Task.FromResult(archived);
        }

        public Task<ActiveTask> RestoreArchivedTaskAsync(string taskId, System.Threading.CancellationToken cancellationToken = default)
        {
            var task = ArchivedTasks.Find(t => t.Id == taskId);
            if (task != null) ArchivedTasks.Remove(task);
            var restored = new ActiveTask { Id = taskId, Title = task?.Title ?? "Restored" };
            ActiveTasks.Add(restored);
            return Task.FromResult(restored);
        }

        public Task<System.Collections.Generic.IReadOnlyList<ArchivedTask>> GetArchivedTasksAsync(string? logicalDate = null, System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult<System.Collections.Generic.IReadOnlyList<ArchivedTask>>(ArchivedTasks);

        public Task ReorderTasksAsync(System.Collections.Generic.IEnumerable<(string Id, int NewOrder)> orders, System.Threading.CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    [Fact]
    public void TaskItemViewModel_IsHovered_PropertyTogglesAndResets()
    {
        var item = new TaskItemViewModel("1", "Test task");
        Assert.False(item.IsHovered);

        item.IsHovered = true;
        Assert.True(item.IsHovered);

        item.ResetAnimationState();
        Assert.False(item.IsHovered);
    }

    [Fact]
    public async Task CompactTasks_AlwaysSynchronizedWithActiveTasks()
    {
        var repo = new InMemoryTestTaskRepository();
        repo.ActiveTasks.Add(new ActiveTask { Id = "1", Title = "Focus 1", OrderIndex = 0 });
        repo.ActiveTasks.Add(new ActiveTask { Id = "2", Title = "Focus 2", OrderIndex = 1 });

        var vm = new WidgetViewModel(repo, new ProgressCalculationEngine());
        await vm.LoadDataAsync();

        Assert.Equal(2, vm.Tasks.Count);
        Assert.Equal(2, vm.CompactTasks.Count);
        Assert.Equal("Focus 1", vm.CompactTasks[0].Title);

        // Добавление новой задачи через инлайн ввод
        vm.NewTaskTitle = "Focus 3";
        await vm.AddTaskAsync();

        Assert.Equal(3, vm.Tasks.Count);
        Assert.Equal(3, vm.CompactTasks.Count);
        Assert.Equal("Focus 3", vm.CompactTasks[0].Title);
    }

    [Fact]
    public void WidgetWindowXaml_MatchesSpecRequirements()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "SecondSelf.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        var xamlPath = Path.Combine(dir!, "src", "SecondSelf", "UI", "Views", "WidgetWindow.xaml");
        Assert.True(File.Exists(xamlPath));

        var content = File.ReadAllText(xamlPath);

        // 1. RootGrid с Background="{x:Null}"
        Assert.Contains("Background=\"{x:Null}\"", content);

        // 2. WarmGlassCardBrush формула: #60D27A38 и #354A2411
        Assert.Contains("#60D27A38", content);
        Assert.Contains("#354A2411", content);

        // 3. SpecularRimBrush формула: #99FFD7B4 и #20FFFFFF
        Assert.Contains("#99FFD7B4", content);
        Assert.Contains("#20FFFFFF", content);

        // 4. Размеры капсулы (260x42) и TranslateTransform Y="0" / -244
        Assert.Contains("Width=\"260\"", content);
        Assert.Contains("Height=\"42\"", content);
        Assert.Contains("Value=\"-244\"", content);

        // 5. Шеврон и BottomTabTrigger
        Assert.Contains("BottomTabTrigger", content);
        Assert.Contains("Second Self", content);

        // 6. Неперематываемая полоса воспроизведения
        Assert.Contains("IsHitTestVisible=\"False\"", content);
        Assert.Contains("Width=\"{x:Bind ViewModel.Player.ProgressBarWidth, Mode=OneWay}\"", content);
    }
}
