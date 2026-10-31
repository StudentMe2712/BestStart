using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Core.ViewModels;
using SecondSelf.Data;
using SecondSelf.Data.Entities;
using SecondSelf.Data.Migrations;
using SecondSelf.Data.Repositories;
using Xunit;

namespace SecondSelf.Tests;

/// <summary>
/// BDD-тесты приёмочного критерия AC-12: Лимит 4 активных Больших целей.
/// 
/// Given: в Personal Dashboard уже создано 4 активных Больших цели.
/// When: пользователь пытается нажать кнопку «+ Добавить цель».
/// Then:
///   1. Кнопка неактивна (CanAddGoal = false), клик не открывает форму создания.
///   2. Всплывает подсказка: «Одновременно можно вести до 4 Больших целей для удержания фокуса. Завершите или архивируйте одну из текущих целей».
///   3. Прямая попытка вставки в SQLite блокируется триггером TR_BigGoals_MaxActiveCheck.
///   4. При переводе одной из целей в архив (ArchiveGoal) возможность добавления цели восстанавливается (CanAddGoal = true).
/// </summary>
public sealed class AC12_BigGoalsLimitAndValidationTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly BigGoalRepository _bigGoalRepository;
    private readonly GoalsTabViewModel _viewModel;

    public AC12_BigGoalsLimitAndValidationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"secondself_ac12_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory(_testDbPath);
        new DatabaseMigrationService(_connectionFactory).MigrateAsync().GetAwaiter().GetResult();

        _bigGoalRepository = new BigGoalRepository(_connectionFactory);
        _viewModel = new GoalsTabViewModel(_bigGoalRepository);
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
    public async Task AC12_WithFourActiveGoals_CanAddGoalIsFalse_AndWarningIsShown()
    {
        // Given: создано 4 активные Большие цели
        for (int i = 1; i <= 4; i++)
        {
            var goal = new BigGoal
            {
                Id = $"goal-{i}",
                Title = $"Big Goal {i}",
                Status = "Active",
                CreatedAt = DateTime.UtcNow.ToString("O")
            };
            var milestones = new List<Milestone>
            {
                new() { Id = $"m-{i}-1", GoalId = goal.Id, OrderIndex = 1, Title = $"Milestone 1 for Goal {i}" }
            };
            await _bigGoalRepository.AddGoalAsync(goal, milestones);
        }

        // When: загружаются цели в GoalsTabViewModel
        await _viewModel.LoadGoalsAsync();

        // Then:
        Assert.Equal(4, _viewModel.ActiveGoals.Count);
        Assert.False(_viewModel.CanAddGoal);

        // When: пользователь пытается открыть модальное окно добавления цели
        _viewModel.OpenAddGoalModal();

        // Then:
        // Форма добавления не открывается, всплывает предупреждение о лимите
        Assert.False(_viewModel.IsAddGoalModalOpen);
        Assert.True(_viewModel.IsLimitWarningVisible);
        Assert.Equal(GoalsTabViewModel.MaxGoalsWarningMessage, _viewModel.LimitWarningMessage);
    }

    [Fact]
    public async Task AC12_AttemptingToSaveFifthGoal_ThrowsInvalidOperationException()
    {
        // Given: в системе 4 активные цели
        for (int i = 1; i <= 4; i++)
        {
            var goal = new BigGoal { Id = $"g-{i}", Title = $"Goal {i}", Status = "Active" };
            await _bigGoalRepository.AddGoalAsync(goal, Array.Empty<Milestone>());
        }
        await _viewModel.LoadGoalsAsync();
        Assert.False(_viewModel.CanAddGoal);

        // When: попытка программного вызова сохранения 5-й цели
        _viewModel.NewGoalTitle = "5th Big Goal Attempt";

        // Then: выбрасывается InvalidOperationException
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _viewModel.SaveNewGoalAsync());
        Assert.Contains("Превышен лимит активных целей", ex.Message);
    }

    [Fact]
    public async Task AC12_DirectSqliteInsert_IsBlockedByTrigger_TR_BigGoals_MaxActiveCheck()
    {
        // Given: 4 цели вставлены напрямую в базу
        await using var conn = await _connectionFactory.OpenConnectionAsync();
        for (int i = 1; i <= 4; i++)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO BigGoals (Id, Title, Status, CreatedAt) VALUES (@Id, @Title, 'Active', @CreatedAt);";
            cmd.Parameters.AddWithValue("@Id", $"db-goal-{i}");
            cmd.Parameters.AddWithValue("@Title", $"DB Goal {i}");
            cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
        }

        // When: попытка выполнить 5-ю прямую вставку активной цели
        await using var fifthCmd = conn.CreateCommand();
        fifthCmd.CommandText = "INSERT INTO BigGoals (Id, Title, Status, CreatedAt) VALUES ('db-goal-5', 'Illegal 5th Goal', 'Active', datetime('now'));";

        // Then: триггер TR_BigGoals_MaxActiveCheck вызывает RAISE(FAIL)
        var sqlEx = await Assert.ThrowsAsync<SqliteException>(() => fifthCmd.ExecuteNonQueryAsync());
        Assert.Contains("Превышен лимит активных целей: одновременно можно вести не более 4 Больших целей.", sqlEx.Message);
    }

    [Fact]
    public async Task AC12_ArchivingOneGoal_RestoresAbilityToAddGoal()
    {
        // Given: 4 активные цели, CanAddGoal = false
        for (int i = 1; i <= 4; i++)
        {
            var goal = new BigGoal { Id = $"goal-arch-{i}", Title = $"Goal {i}", Status = "Active" };
            await _bigGoalRepository.AddGoalAsync(goal, Array.Empty<Milestone>());
        }
        await _viewModel.LoadGoalsAsync();
        Assert.False(_viewModel.CanAddGoal);

        // When: пользователь архивирует одну из целей (например, достигнутую или отложенную)
        await _viewModel.ArchiveGoalAsync("goal-arch-1");

        // Then:
        // В активном списке осталось 3 цели
        Assert.Equal(3, _viewModel.ActiveGoals.Count);
        // CanAddGoal снова true, предупреждение скрыто
        Assert.True(_viewModel.CanAddGoal);
        Assert.False(_viewModel.IsLimitWarningVisible);

        // Теперь модальное окно добавления цели успешно открывается
        _viewModel.OpenAddGoalModal();
        Assert.True(_viewModel.IsAddGoalModalOpen);

        // И новая 4-я цель успешно сохраняется
        _viewModel.NewGoalTitle = "New 4th Big Goal";
        await _viewModel.SaveNewGoalAsync();

        Assert.Equal(4, _viewModel.ActiveGoals.Count);
        Assert.False(_viewModel.CanAddGoal);
    }
}
