using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Data;
using SecondSelf.Data.Entities;
using SecondSelf.Data.Migrations;
using SecondSelf.Data.Repositories;
using Xunit;

namespace SecondSelf.Tests;

public sealed class BigGoalRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _factory;
    private readonly BigGoalRepository _repository;

    public BigGoalRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"secondself_biggoals_{Guid.NewGuid():N}.db");
        _factory = new SqliteConnectionFactory(_testDbPath);
        new DatabaseMigrationService(_factory).MigrateAsync().GetAwaiter().GetResult();
        _repository = new BigGoalRepository(_factory);
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
    public async Task AddGoalAsync_ActiveGoalsLimit4_Enforced()
    {
        // Добавляем 4 активные цели - должно пройти успешно
        for (int i = 1; i <= 4; i++)
        {
            var goal = new BigGoal
            {
                Title = $"Goal {i}",
                Motivation = $"Motivation {i}",
                Status = "Active"
            };
            await _repository.AddGoalAsync(goal, Array.Empty<Milestone>());
        }

        var activeGoals = await _repository.GetActiveGoalsAsync();
        Assert.Equal(4, activeGoals.Count);

        // Попытка добавить 5-ю активную цель должна выбросить исключение
        var fifthGoal = new BigGoal
        {
            Title = "Goal 5",
            Status = "Active"
        };

        await Assert.ThrowsAnyAsync<Exception>(() =>
            _repository.AddGoalAsync(fifthGoal, Array.Empty<Milestone>()));

        // Архивируем одну из целей
        await _repository.ArchiveGoalAsync(activeGoals[0].Id);

        var activeAfterArchive = await _repository.GetActiveGoalsAsync();
        Assert.Equal(3, activeAfterArchive.Count);

        // Теперь 5-ю цель можно добавить
        await _repository.AddGoalAsync(fifthGoal, Array.Empty<Milestone>());

        var activeFinal = await _repository.GetActiveGoalsAsync();
        Assert.Equal(4, activeFinal.Count);
        Assert.Contains(activeFinal, g => g.Title == "Goal 5");
    }

    [Fact]
    public async Task AddGoalAsync_WithMilestones_OrdersCorrectly()
    {
        var goal = new BigGoal
        {
            Title = "Master WinUI 3",
            Motivation = "Career leap",
            TargetResult = "Ship AppStore product",
            TargetPeriod = "1 year"
        };

        var initialMilestones = new List<Milestone>
        {
            new() { Title = "Milestone 3", OrderIndex = 3 },
            new() { Title = "Milestone 1", OrderIndex = 1 },
            new() { Title = "Milestone 2", OrderIndex = 2 }
        };

        await _repository.AddGoalAsync(goal, initialMilestones);

        // Проверяем через GetActiveGoalsAsync
        var activeGoals = await _repository.GetActiveGoalsAsync();
        var retrieved = Assert.Single(activeGoals);
        Assert.Equal("Master WinUI 3", retrieved.Title);
        Assert.Equal(3, retrieved.Milestones.Count);
        Assert.Equal("Milestone 1", retrieved.Milestones[0].Title);
        Assert.Equal("Milestone 2", retrieved.Milestones[1].Title);
        Assert.Equal("Milestone 3", retrieved.Milestones[2].Title);

        // Проверяем через GetGoalByIdAsync
        var byId = await _repository.GetGoalByIdAsync(goal.Id);
        Assert.NotNull(byId);
        Assert.Equal(3, byId.Milestones.Count);
        Assert.Equal(1, byId.Milestones[0].OrderIndex);
        Assert.Equal(2, byId.Milestones[1].OrderIndex);
        Assert.Equal(3, byId.Milestones[2].OrderIndex);
    }

    [Fact]
    public async Task CompleteMilestoneAsync_SetsStatusAndCompletedAt()
    {
        var goal = new BigGoal { Title = "Goal With Milestones" };
        var ms1 = new Milestone { Title = "Step 1", OrderIndex = 1, Status = "Upcoming" };
        await _repository.AddGoalAsync(goal, new[] { ms1 });

        await _repository.CompleteMilestoneAsync(ms1.Id);

        var byId = await _repository.GetGoalByIdAsync(goal.Id);
        Assert.NotNull(byId);
        var completedMs = Assert.Single(byId.Milestones);
        Assert.Equal("Completed", completedMs.Status);
        Assert.NotNull(completedMs.CompletedAt);
    }

    [Fact]
    public async Task UpdateGoalAsync_UpdatesFieldsSuccessfully()
    {
        var goal = new BigGoal
        {
            Title = "Original Title",
            Motivation = "Original Mot",
            IconKey = "Compass"
        };
        await _repository.AddGoalAsync(goal, Array.Empty<Milestone>());

        goal.Title = "Updated Title";
        goal.Motivation = "Updated Mot";
        goal.IconKey = "Trophy";
        goal.TargetResult = "Complete sprint";

        await _repository.UpdateGoalAsync(goal);

        var updated = await _repository.GetGoalByIdAsync(goal.Id);
        Assert.NotNull(updated);
        Assert.Equal("Updated Title", updated.Title);
        Assert.Equal("Updated Mot", updated.Motivation);
        Assert.Equal("Trophy", updated.IconKey);
        Assert.Equal("Complete sprint", updated.TargetResult);
    }

    [Fact]
    public async Task AddMilestoneAsync_And_UpdateMilestoneAsync_WorkCorrectly()
    {
        var goal = new BigGoal { Title = "Goal For Ms Updates" };
        await _repository.AddGoalAsync(goal, Array.Empty<Milestone>());

        var newMs = new Milestone
        {
            GoalId = goal.Id,
            OrderIndex = 1,
            Title = "Initial Milestone",
            Status = "Upcoming"
        };
        await _repository.AddMilestoneAsync(newMs);

        var retrieved = await _repository.GetGoalByIdAsync(goal.Id);
        Assert.NotNull(retrieved);
        Assert.Single(retrieved.Milestones);

        newMs.Title = "Renamed Milestone";
        newMs.OrderIndex = 5;
        newMs.Status = "InProgress";
        await _repository.UpdateMilestoneAsync(newMs);

        var retrievedAfter = await _repository.GetGoalByIdAsync(goal.Id);
        Assert.NotNull(retrievedAfter);
        var updatedMs = Assert.Single(retrievedAfter.Milestones);
        Assert.Equal("Renamed Milestone", updatedMs.Title);
        Assert.Equal(5, updatedMs.OrderIndex);
        Assert.Equal("InProgress", updatedMs.Status);
    }

    [Fact]
    public async Task DeleteGoalAsync_RemovesGoalAndMilestonesCascade()
    {
        var goal = new BigGoal { Title = "Goal To Delete" };
        var ms = new Milestone { Title = "Child Milestone", OrderIndex = 1 };
        await _repository.AddGoalAsync(goal, new[] { ms });

        await _repository.DeleteGoalAsync(goal.Id);

        var retrieved = await _repository.GetGoalByIdAsync(goal.Id);
        Assert.Null(retrieved);

        var active = await _repository.GetActiveGoalsAsync();
        Assert.Empty(active);
    }
}
