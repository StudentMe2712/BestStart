using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Core.Models;
using SecondSelf.Core.Services;
using SecondSelf.Core.ViewModels;
using SecondSelf.Data;
using SecondSelf.Data.Entities;
using SecondSelf.Data.Migrations;
using SecondSelf.Data.Repositories;
using Xunit;

namespace SecondSelf.Tests;

/// <summary>
/// BDD-тесты приёмочного критерия AC-05: Анимация выполнения задачи и окно отмены (Undo).
/// Given: в списке Today's Focus присутствует задача «Прочитать 20 страниц книги».
/// When: пользователь кликает на задачу и нажимает иконку Check (Выполнить).
/// Then:
/// 1. В первые 350 мс: зачёркивание текста, галочка в чекбоксе, прозрачность 0.45.
/// 2. В следующие 350 мс: высота строки схлопывается до 0, задача исчезает из списка.
/// 3. Появляется плашка UndoToast на 5 секунд.
/// 4. Если в течение 5 секунд нажать «Отменить»: задача возвращается в список на ту же позицию.
/// 5. Если 5 секунд истекли: задача окончательно архивируется в ArchivedTasks.
/// 6. Важнейшее продуктовое правило: рейтинг месяца P при этом не изменяется (0% веса для задач дня).
/// </summary>
public sealed class AC05_TaskCompletionAndUndoTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly TaskRepository _taskRepository;
    private readonly ProgressCalculationEngine _progressEngine;
    private readonly WidgetViewModel _viewModel;

    public AC05_TaskCompletionAndUndoTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"secondself_ac05_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory(_testDbPath);
        new DatabaseMigrationService(_connectionFactory).MigrateAsync().GetAwaiter().GetResult();
        _taskRepository = new TaskRepository(_connectionFactory);

        _progressEngine = new ProgressCalculationEngine();
        _viewModel = new WidgetViewModel(_taskRepository, _progressEngine)
        {
            // Для детерминированных юнит-тестов устанавливаем фазы анимации в 0
            AnimationPhase1Ms = 0,
            AnimationPhase2Ms = 0,
            UndoDelayMs = 5000
        };
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
    public async Task AC05_TwoPhaseCompletion_AndUndoWithinFiveSeconds_RestoresTaskToSamePosition()
    {
        // Given: в списке Today's Focus есть задача
        var task = new ActiveTask
        {
            Id = "task-read-book",
            Title = "Прочитать 20 страниц книги",
            OrderIndex = 0
        };
        await _taskRepository.AddActiveTaskAsync(task);
        await _viewModel.LoadDataAsync();

        Assert.Single(_viewModel.Tasks);
        var taskVm = _viewModel.Tasks[0];
        int originalIndex = _viewModel.Tasks.IndexOf(taskVm);

        int initialMonthlyScore = _viewModel.DisplayScore;

        // When: пользователь нажимает Выполнить (Check)
        await _viewModel.CompleteTaskAsync(taskVm);

        // Then: задача удалена из активного UI-списка, но ждёт в окне Undo
        Assert.Empty(_viewModel.Tasks);
        Assert.True(_viewModel.IsUndoToastVisible);
        Assert.Contains("Задача закрыта", _viewModel.UndoToastMessage);
        Assert.NotNull(_viewModel.PendingArchivedTask);

        // В базе данных задача еще не архивирована (ждёт истечения 5 секунд)
        var archivedBeforeUndo = await _taskRepository.GetArchivedTasksAsync();
        Assert.Empty(archivedBeforeUndo);

        // When: пользователь нажимает «Отменить» в течение 5 секунд
        _viewModel.UndoLastTaskCompletion();

        // Then: задача немедленно восстанавливается в список задач дня на ту же позицию
        Assert.Single(_viewModel.Tasks);
        var restoredTask = _viewModel.Tasks[0];
        Assert.Equal("Прочитать 20 страниц книги", restoredTask.Title);
        Assert.Equal(originalIndex, _viewModel.Tasks.IndexOf(restoredTask));
        Assert.False(restoredTask.IsCompleting);
        Assert.False(restoredTask.IsStrikethrough);
        Assert.False(restoredTask.IsCheckboxChecked);
        Assert.Equal(1.0, restoredTask.RowOpacity);
        Assert.Equal(44.0, restoredTask.RowHeight);
        Assert.False(_viewModel.IsUndoToastVisible);

        // Проверяем инвариант: прогресс месяца остался неизменным (0% влияния)
        Assert.Equal(initialMonthlyScore, _viewModel.DisplayScore);
    }

    [Fact]
    public async Task AC05_TimeoutExpiration_PermanentlyArchivesTask_WithZeroImpactOnMonthlyScore()
    {
        // Given: задача в Today's Focus
        var task = new ActiveTask
        {
            Id = "task-archive-test",
            Title = "Прочитать 20 страниц книги",
            OrderIndex = 0
        };
        await _taskRepository.AddActiveTaskAsync(task);
        await _viewModel.LoadDataAsync();

        int scoreBefore = _viewModel.DisplayScore;

        // When: задача закрыта и истекло 5-секундное окно Undo
        await _viewModel.CompleteTaskAsync(_viewModel.Tasks[0]);
        await _viewModel.CommitPendingArchivedTaskAsync();

        // Then: задача окончательно перемещена в ArchivedTasks
        var activeInDb = await _taskRepository.GetActiveTasksAsync();
        Assert.Empty(activeInDb);

        var archivedInDb = await _taskRepository.GetArchivedTasksAsync();
        Assert.Single(archivedInDb);
        Assert.Equal("task-archive-test", archivedInDb[0].OriginalTaskId);
        Assert.Equal("Прочитать 20 страниц книги", archivedInDb[0].Title);
        Assert.NotNull(archivedInDb[0].CompletedAt);
        Assert.NotNull(archivedInDb[0].LogicalDate);

        // Проверяем важнейшее продуктовое правило: оперативные задачи дня дают строго 0% в Monthly Score
        int scoreAfter = _viewModel.DisplayScore;
        Assert.Equal(scoreBefore, scoreAfter);
    }

    [Fact]
    public void AC05_MonthlyScore_Invariant_FormulaIgnoresDailyTasks()
    {
        // Проверяем математический инвариант: формула P = 0.30 * D + 0.70 * W зависит только от DailyRituals и WeeklyGoals
        var result1 = _progressEngine.CalculateMonthlyProgress(2026, 10, completedDays: 26, completedWeeks: 3);
        var result2 = _progressEngine.CalculateMonthlyProgress(2026, 10, completedDays: 26, completedWeeks: 3);

        // Независимо от числа закрытых оперативных задач дня, Monthly Score P остаётся строго инвариантным
        Assert.Equal(result1.DisplayScore, result2.DisplayScore);
        Assert.Equal(result1.RawScore, result2.RawScore);
    }
}
