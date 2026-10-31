using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Core.Services;
using SecondSelf.Core.ViewModels;
using SecondSelf.Data;
using SecondSelf.Data.Entities;
using SecondSelf.Data.Migrations;
using SecondSelf.Data.Repositories;
using Xunit;

namespace SecondSelf.Tests;

/// <summary>
/// BDD-тесты приёмочного критерия AC-13: Связка недели с этапом Большой цели.
/// 
/// Given: создана Большая цель «Развитие карьеры» с этапом «Создать продукт Second Self».
/// When: пользователь в начале недели создаёт Weekly Goal и в выпадающем списке выбирает этот этап.
/// Then:
///   1. В таблице WeeklyGoals поле MilestoneId ссылается на выбранный этап.
///   2. На карточке недели в Dashboard (TodayTabViewModel) отображается тег [Career Growth · Milestone 2].
///   3. Прогресс самой Большой цели НЕ изменяется до тех пор, пока пользователь явно не отметит весь Этап как Completed.
///   4. При явном завершении этапа: статус этапа меняется на Completed, счётчик этапов цели инкрементируется.
/// </summary>
public sealed class AC13_WeeklyGoalMilestoneLinkageTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly BigGoalRepository _bigGoalRepository;
    private readonly WeeklyGoalRepository _weeklyGoalRepository;
    private readonly TaskRepository _taskRepository;
    private readonly DailyRitualRepository _dailyRitualRepository;
    private readonly ProgressCalculationEngine _progressEngine;

    private readonly WeeklyGoalModalViewModel _weeklyModalVm;
    private readonly TodayTabViewModel _todayTabVm;
    private readonly GoalsTabViewModel _goalsTabVm;

    public AC13_WeeklyGoalMilestoneLinkageTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"secondself_ac13_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory(_testDbPath);
        new DatabaseMigrationService(_connectionFactory).MigrateAsync().GetAwaiter().GetResult();

        _bigGoalRepository = new BigGoalRepository(_connectionFactory);
        _weeklyGoalRepository = new WeeklyGoalRepository(_connectionFactory);
        _taskRepository = new TaskRepository(_connectionFactory);
        _dailyRitualRepository = new DailyRitualRepository(_connectionFactory);
        _progressEngine = new ProgressCalculationEngine();

        _weeklyModalVm = new WeeklyGoalModalViewModel(_weeklyGoalRepository, _bigGoalRepository);
        _todayTabVm = new TodayTabViewModel(_weeklyGoalRepository, _taskRepository, _bigGoalRepository, _dailyRitualRepository, _progressEngine);
        _goalsTabVm = new GoalsTabViewModel(_bigGoalRepository);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }

    [Fact]
    public async Task AC13_MilestoneLinkage_And_ZeroProgressInflation_Until_ExplicitCompletion()
    {
        // Given: создана Большая цель «Career Growth» с 4 этапами, второй этап — «Создать продукт Second Self»
        var bigGoal = new BigGoal
        {
            Id = "goal-career",
            Title = "Career Growth",
            Motivation = "Become a top-tier software engineer",
            Status = "Active"
        };
        var milestones = new List<Milestone>
        {
            new() { Id = "ms-1", GoalId = bigGoal.Id, OrderIndex = 1, Title = "Learn WinUI 3 Architecture", Status = "Completed", CompletedAt = "2026-10-01T00:00:00Z" },
            new() { Id = "ms-2", GoalId = bigGoal.Id, OrderIndex = 2, Title = "Создать продукт Second Self", Status = "InProgress" },
            new() { Id = "ms-3", GoalId = bigGoal.Id, OrderIndex = 3, Title = "Scale Architecture to v2", Status = "Upcoming" },
            new() { Id = "ms-4", GoalId = bigGoal.Id, OrderIndex = 4, Title = "Achieve Financial Freedom", Status = "Future" }
        };
        await _bigGoalRepository.AddGoalAsync(bigGoal, milestones);

        // Проверяем начальное состояние цели: 1 of 4 milestones (25%)
        await _goalsTabVm.LoadGoalsAsync();
        var loadedGoal = _goalsTabVm.ActiveGoals.First(g => g.Id == "goal-career");
        Assert.Equal(1, loadedGoal.CompletedMilestonesCount);
        Assert.Equal(4, loadedGoal.TotalMilestonesCount);
        Assert.Equal("1 of 4 milestones", loadedGoal.MilestonesProgressText);
        Assert.Equal(25.0, loadedGoal.ProgressPercentage);

        // When: пользователь в начале недели 44 создаёт Weekly Goal и выбирает этап «Создать продукт Second Self»
        int isoYear = 2026;
        int isoWeek = 44;
        await _weeklyModalVm.LoadForWeekAsync(isoYear, isoWeek);

        // Проверяем наличие этапа в доступных элементах
        var targetLookup = _weeklyModalVm.AvailableMilestones.FirstOrDefault(m => m.MilestoneId == "ms-2");
        Assert.NotNull(targetLookup);
        Assert.Equal("[Career Growth · Создать продукт Second Self]", targetLookup.DisplayName);

        _weeklyModalVm.Title = "Реализовать MVP Second Self";
        _weeklyModalVm.SelectedMilestone = targetLookup;

        // Сохраняем цель недели
        await _weeklyModalVm.SaveAndLockAsync();

        // Then 1: В таблице WeeklyGoals поле MilestoneId ссылается на выбранный этап
        var inDbWeeklyGoal = await _weeklyGoalRepository.GetGoalForWeekAsync(isoYear, isoWeek);
        Assert.NotNull(inDbWeeklyGoal);
        Assert.Equal("ms-2", inDbWeeklyGoal.MilestoneId);
        Assert.Equal("Реализовать MVP Second Self", inDbWeeklyGoal.Title);

        // Then 2: На карточке недели в TodayTabViewModel отображается тег [Career Growth · Создать продукт Second Self]
        var testDate = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);
        await _todayTabVm.LoadDataAsync(testDate);
        Assert.True(_todayTabVm.HasWeeklyGoal);
        Assert.Equal("Реализовать MVP Second Self", _todayTabVm.WeeklyGoalText);
        Assert.Equal("[Career Growth · Создать продукт Second Self]", _todayTabVm.WeeklyGoalTag);

        // Then 3: Прогресс самой Большой цели НЕ изменился от создания или фиксации Weekly Goal!
        // (Инвариант отсутствия накрутки прогресса до явного завершения этапа)
        await _goalsTabVm.LoadGoalsAsync();
        loadedGoal = _goalsTabVm.ActiveGoals.First(g => g.Id == "goal-career");
        Assert.Equal(1, loadedGoal.CompletedMilestonesCount);
        Assert.Equal(4, loadedGoal.TotalMilestonesCount);
        Assert.Equal("1 of 4 milestones", loadedGoal.MilestonesProgressText);
        Assert.Equal(25.0, loadedGoal.ProgressPercentage);

        // When 4: Пользователь явно завершает весь этап через CompleteMilestoneCommand
        await _goalsTabVm.CompleteMilestoneAsync("ms-2");

        // Then 4:
        // Этап ms-2 переходит в Completed, штамп времени зафиксирован
        var updatedMilestone = (await _bigGoalRepository.GetGoalByIdAsync("goal-career"))!
            .Milestones.First(m => m.Id == "ms-2");
        Assert.Equal("Completed", updatedMilestone.Status);
        Assert.NotNull(updatedMilestone.CompletedAt);

        // Теперь прогресс Большой цели честно продвинулся: 2 of 4 milestones (50%)
        await _goalsTabVm.LoadGoalsAsync();
        loadedGoal = _goalsTabVm.ActiveGoals.First(g => g.Id == "goal-career");
        Assert.Equal(2, loadedGoal.CompletedMilestonesCount);
        Assert.Equal("2 of 4 milestones", loadedGoal.MilestonesProgressText);
        Assert.Equal(50.0, loadedGoal.ProgressPercentage);
    }
}
