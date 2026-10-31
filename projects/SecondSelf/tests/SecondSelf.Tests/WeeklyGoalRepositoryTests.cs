using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Data;
using SecondSelf.Data.Entities;
using SecondSelf.Data.Migrations;
using SecondSelf.Data.Repositories;
using Xunit;

namespace SecondSelf.Tests;

public sealed class WeeklyGoalRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _factory;
    private readonly WeeklyGoalRepository _repository;

    public WeeklyGoalRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"secondself_weeklygoals_{Guid.NewGuid():N}.db");
        _factory = new SqliteConnectionFactory(_testDbPath);
        new DatabaseMigrationService(_factory).MigrateAsync().GetAwaiter().GetResult();
        _repository = new WeeklyGoalRepository(_factory);
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
    public async Task AddAndGetGoalForWeek_WorksCorrectly()
    {
        var goal = new WeeklyGoal
        {
            IsoYear = 2026,
            IsoWeek = 44,
            Title = "Finish Sprint 4 Implementation",
            BelongsToMonth = "2026-10"
        };

        await _repository.AddOrUpdateGoalAsync(goal);

        var retrieved = await _repository.GetGoalForWeekAsync(2026, 44);
        Assert.NotNull(retrieved);
        Assert.Equal("Finish Sprint 4 Implementation", retrieved.Title);
        Assert.Equal(2026, retrieved.IsoYear);
        Assert.Equal(44, retrieved.IsoWeek);
        Assert.Equal("2026-10", retrieved.BelongsToMonth);
        Assert.Equal("Active", retrieved.Status);
    }

    [Fact]
    public async Task GetGoalsForMonth_ReturnsAllGoalsInMonth()
    {
        var goal1 = new WeeklyGoal
        {
            IsoYear = 2026,
            IsoWeek = 41,
            BelongsToMonth = "2026-10",
            Title = "Week 41 Goal"
        };

        var goal2 = new WeeklyGoal
        {
            IsoYear = 2026,
            IsoWeek = 42,
            BelongsToMonth = "2026-10",
            Title = "Week 42 Goal"
        };

        var goalOtherMonth = new WeeklyGoal
        {
            IsoYear = 2026,
            IsoWeek = 46,
            BelongsToMonth = "2026-11",
            Title = "November Goal"
        };

        await _repository.AddOrUpdateGoalAsync(goal1);
        await _repository.AddOrUpdateGoalAsync(goal2);
        await _repository.AddOrUpdateGoalAsync(goalOtherMonth);

        var octGoals = await _repository.GetGoalsForMonthAsync("2026-10");
        Assert.Equal(2, octGoals.Count);
        Assert.Equal("Week 41 Goal", octGoals[0].Title);
        Assert.Equal("Week 42 Goal", octGoals[1].Title);
    }

    [Fact]
    public async Task LockGoalAsync_LocksGoalAndSetsTimestamp()
    {
        var goal = new WeeklyGoal
        {
            IsoYear = 2026,
            IsoWeek = 44,
            Title = "Uncommitted Goal"
        };
        await _repository.AddOrUpdateGoalAsync(goal);

        await _repository.LockGoalAsync(goal.Id);

        var locked = await _repository.GetGoalByIdAsync(goal.Id);
        Assert.NotNull(locked);
        Assert.Equal("Locked", locked.Status);
        Assert.NotNull(locked.LockedAt);
    }

    [Fact]
    public async Task CompleteGoalAsync_MarksCompletedAndSetsTimestamp()
    {
        var goal = new WeeklyGoal
        {
            IsoYear = 2026,
            IsoWeek = 44,
            Title = "Goal to Complete"
        };
        await _repository.AddOrUpdateGoalAsync(goal);

        await _repository.CompleteGoalAsync(goal.Id);

        var completed = await _repository.GetGoalByIdAsync(goal.Id);
        Assert.NotNull(completed);
        Assert.Equal("Completed", completed.Status);
        Assert.NotNull(completed.CompletedAt);
    }

    [Fact]
    public async Task CancelGoalAsync_SetsCancelledWithReason()
    {
        var goal = new WeeklyGoal
        {
            IsoYear = 2026,
            IsoWeek = 44,
            Title = "Goal to Cancel"
        };
        await _repository.AddOrUpdateGoalAsync(goal);

        await _repository.CancelGoalAsync(goal.Id, "Scope changed due to new architecture");

        var cancelled = await _repository.GetGoalByIdAsync(goal.Id);
        Assert.NotNull(cancelled);
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal("Scope changed due to new architecture", cancelled.CancellationReason);
    }

    [Fact]
    public async Task PostponeGoalAsync_MarksCurrentPostponed_AndCreatesNextWeekGoal()
    {
        var bigGoalRepo = new BigGoalRepository(_factory);
        var bigGoal = new BigGoal { Title = "Goal For Milestone" };
        var ms = new Milestone { Title = "Milestone Step 1", OrderIndex = 1 };
        await bigGoalRepo.AddGoalAsync(bigGoal, new[] { ms });

        var currentGoal = new WeeklyGoal
        {
            IsoYear = 2026,
            IsoWeek = 44,
            Title = "Postponed Goal Test",
            MilestoneId = ms.Id
        };
        await _repository.AddOrUpdateGoalAsync(currentGoal);

        var nextGoal = await _repository.PostponeGoalAsync(
            currentGoal.Id,
            "Urgent client emergency",
            2026,
            45);

        // Проверяем текущую цель
        var updatedCurrent = await _repository.GetGoalByIdAsync(currentGoal.Id);
        Assert.NotNull(updatedCurrent);
        Assert.Equal("Postponed", updatedCurrent.Status);
        Assert.Equal("Urgent client emergency", updatedCurrent.CancellationReason);

        // Проверяем созданную новую цель
        Assert.NotNull(nextGoal);
        Assert.Equal(2026, nextGoal.IsoYear);
        Assert.Equal(45, nextGoal.IsoWeek);
        Assert.Equal("Postponed Goal Test", nextGoal.Title);
        Assert.Equal(ms.Id, nextGoal.MilestoneId);
        Assert.Equal("Active", nextGoal.Status);

        var loadedNext = await _repository.GetGoalForWeekAsync(2026, 45);
        Assert.NotNull(loadedNext);
        Assert.Equal(nextGoal.Id, loadedNext.Id);
    }
}
