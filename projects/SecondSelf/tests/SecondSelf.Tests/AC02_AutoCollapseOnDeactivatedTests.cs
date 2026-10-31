using System;
using System.Threading.Tasks;
using SecondSelf.Core.Models;
using SecondSelf.Core.Services;
using SecondSelf.Core.ViewModels;
using SecondSelf.Data.Entities;
using SecondSelf.Data.Repositories;
using Xunit;

namespace SecondSelf.Tests;

/// <summary>
/// BDD-тесты приёмочного критерия AC-02: Автосворачивание виджета при потере фокуса (Deactivated).
/// Given: виджет находится в состоянии Expanded, возможно открыто инлайн-поле добавления задачи.
/// When: окно теряет фокус (событие Deactivated / клик вне окна / Esc).
/// Then: виджет автоматически сворачивается в Compact (360×240px), инлайн-ввод закрывается.
/// </summary>
public sealed class AC02_AutoCollapseOnDeactivatedTests
{
    private sealed class StubTaskRepository : ITaskRepository
    {
        public Task<IReadOnlyList<ActiveTask>> GetActiveTasksAsync(System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ActiveTask>>(Array.Empty<ActiveTask>());
        public Task AddActiveTaskAsync(ActiveTask task, System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateActiveTaskAsync(ActiveTask task, System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteActiveTaskAsync(string id, System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ArchivedTask> ArchiveTaskAsync(string taskId, string logicalDate, System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult(new ArchivedTask { Id = taskId, Title = "Test" });
        public Task<ActiveTask> RestoreArchivedTaskAsync(string taskId, System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult(new ActiveTask { Id = taskId, Title = "Test" });
        public Task<IReadOnlyList<ArchivedTask>> GetArchivedTasksAsync(string? logicalDate = null, System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ArchivedTask>>(Array.Empty<ArchivedTask>());
        public Task ReorderTasksAsync(System.Collections.Generic.IEnumerable<(string Id, int NewOrder)> orders, System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public void AC02_Deactivation_CollapsesExpandedWidget_AndClosesInlineTaskInput()
    {
        // Given: виджет в состоянии Expanded, открыто поле добавления задачи (IsAddingTask = true)
        var vm = new WidgetViewModel(new StubTaskRepository(), new ProgressCalculationEngine());
        vm.Expand();
        vm.StartAddingTask();

        Assert.True(vm.IsExpanded);
        Assert.True(vm.IsAddingTask);

        bool stateChangedFired = false;
        bool reportedState = true;
        vm.ExpandedStateChanged += isExp =>
        {
            stateChangedFired = true;
            reportedState = isExp;
        };

        // When: окно теряет фокус (пользователь кликнул вне виджета -> срабатывает Collapse)
        vm.Collapse();

        // Then: виджет свернулся в Compact, режим добавления задачи выключен
        Assert.False(vm.IsExpanded);
        Assert.False(vm.IsAddingTask);
        Assert.True(stateChangedFired);
        Assert.False(reportedState);
    }

    [Fact]
    public void AC02_Deactivation_WhenAlreadyCollapsed_DoesNotFireRedundantCollapseEvent()
    {
        // Given: виджет уже в Compact
        var vm = new WidgetViewModel(new StubTaskRepository(), new ProgressCalculationEngine());
        Assert.False(vm.IsExpanded);

        bool stateChangedFired = false;
        vm.ExpandedStateChanged += _ => stateChangedFired = true;

        // When: повторный вызов Collapse (например, при повторной деактивации)
        vm.Collapse();

        // Then: лишнее событие не срабатывает
        Assert.False(stateChangedFired);
        Assert.False(vm.IsExpanded);
    }
}
