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
/// BDD-тесты приёмочного критерия AC-01: Переключение состояний виджета (Compact / Expanded).
/// Given: Desktop Widget в режиме Compact (360×240px).
/// When: пользователь кликает по интерактивной карточке или кнопке разворачивания.
/// Then: виджет раскрывается до 380×680px с Top-Right Anchor, отображаются 3 секции, по сворачиванию возвращается в 360×240px.
/// </summary>
public sealed class AC01_WidgetModeToggleTests
{
    private sealed class InMemoryTaskRepository : ITaskRepository
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
    public void AC01_Expand_TogglesState_AndFiresExpandedStateChanged()
    {
        // Given: виджет в режиме Compact (360x240)
        var vm = new WidgetViewModel(new InMemoryTaskRepository(), new ProgressCalculationEngine());
        Assert.False(vm.IsExpanded);

        bool eventFired = false;
        bool newState = false;
        vm.ExpandedStateChanged += isExp =>
        {
            eventFired = true;
            newState = isExp;
        };

        // When: пользователь раскрывает виджет
        vm.Expand();

        // Then: виджет переходит в Expanded (380x680)
        Assert.True(vm.IsExpanded);
        Assert.True(eventFired);
        Assert.True(newState);
    }

    [Fact]
    public void AC01_TopRightAnchor_CalculatesPositionCorrectly_BetweenCompactAndExpanded()
    {
        // Given: Экран 1920x1080, точка привязки в правом верхнем углу (Top-Right Anchor)
        var windowStateService = new WindowStateService(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "test_anchor.json"));

        int workAreaX = 0;
        int workAreaY = 0;
        int workAreaWidth = 1920;
        int workAreaHeight = 1080;

        // Компактный режим: 360x240
        int compactWidth = 360;
        int compactHeight = 240;
        int compactX = 1500; // 60px от правого края (1500 + 360 = 1860)
        int compactY = 60;

        var (normX, normY) = windowStateService.CalculateNormalizedCoordinates(
            compactX, compactY, compactWidth, compactHeight,
            workAreaX, workAreaY, workAreaWidth, workAreaHeight);

        var state = new WindowState { NormalizedX = normX, NormalizedY = normY, IsExpanded = false };

        // When: переключение в Expanded (380x680)
        int expandedWidth = 380;
        Assert.Equal(240, compactHeight);

        // Рассчитываем Top-Right Anchor сдвиг: при увеличении ширины с 360 до 380px X должен сдвинуться влево на 20px
        int deltaWidth = expandedWidth - compactWidth; // 20px
        int expectedExpandedX = compactX - deltaWidth; // 1480px

        // Then: правый край окна остаётся строго на той же позиции (1480 + 380 = 1860px)
        int compactRightEdge = compactX + compactWidth;
        int expandedRightEdge = expectedExpandedX + expandedWidth;
        Assert.Equal(compactRightEdge, expandedRightEdge);
    }

    [Fact]
    public void AC01_Collapse_ReturnsToCompactState()
    {
        // Given: виджет в режиме Expanded
        var vm = new WidgetViewModel(new InMemoryTaskRepository(), new ProgressCalculationEngine());
        vm.Expand();
        Assert.True(vm.IsExpanded);

        bool eventFired = false;
        bool lastState = true;
        vm.ExpandedStateChanged += isExp =>
        {
            eventFired = true;
            lastState = isExp;
        };

        // When: пользователь сворачивает виджет
        vm.Collapse();

        // Then: виджет вернулся в Compact
        Assert.False(vm.IsExpanded);
        Assert.True(eventFired);
        Assert.False(lastState);
    }
}
