using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Core.Models;
using SecondSelf.Core.Services;
using SecondSelf.Core.ViewModels;
using SecondSelf.Data;
using SecondSelf.Data.Migrations;
using SecondSelf.Data.Repositories;
using Xunit;

namespace SecondSelf.Tests;

/// <summary>
/// BDD-тесты приёмочного критерия AC-04: Инлайн-добавление задачи дня (Today's Focus).
/// Given: открыт виджет Today's Focus.
/// When: пользователь нажимает круглую кнопку + или глобальный шорткат Alt+Shift+N, вводит текст и нажимает Enter.
/// Then:
/// 1. Задача сохраняется в таблице ActiveTasks в SQLite с OrderIndex = 0.
/// 2. Новая строка задачи появляется вверху списка задач дня.
/// 3. Поле ввода очищается и готово к следующему вводу.
/// 4. При нажатии Esc поле ввода закрывается без сохранения.
/// </summary>
public sealed class AC04_InlineTaskCreationTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly TaskRepository _taskRepository;
    private readonly WidgetViewModel _viewModel;

    public AC04_InlineTaskCreationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"secondself_ac04_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory(_testDbPath);
        new DatabaseMigrationService(_connectionFactory).MigrateAsync().GetAwaiter().GetResult();
        _taskRepository = new TaskRepository(_connectionFactory);

        var progressEngine = new ProgressCalculationEngine();
        _viewModel = new WidgetViewModel(_taskRepository, progressEngine);
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
    public async Task AC04_InlineTaskCreation_SavesWithOrderIndexZero_AndClearsInputForNext()
    {
        // Given: открыт раскрытый виджет Today's Focus
        _viewModel.Expand();
        Assert.True(_viewModel.IsExpanded);

        // When: пользователь нажимает + (или глобальный шорткат Alt+Shift+N)
        _viewModel.StartAddingTask();
        Assert.True(_viewModel.IsAddingTask);

        // Вводит текст задачи
        _viewModel.NewTaskTitle = "Завершить модуль авторизации";

        // Нажимает Enter
        await _viewModel.AddTaskAsync();

        // Then 1: Задача сохраняется в таблице ActiveTasks в SQLite с OrderIndex = 0
        var activeInDb = await _taskRepository.GetActiveTasksAsync();
        Assert.Single(activeInDb);
        Assert.Equal("Завершить модуль авторизации", activeInDb[0].Title);
        Assert.Equal(0, activeInDb[0].OrderIndex);

        // Then 2: Новая строка задачи плавно появляется вверху списка задач дня
        Assert.Single(_viewModel.Tasks);
        Assert.Equal("Завершить модуль авторизации", _viewModel.Tasks[0].Title);
        Assert.Equal(0, _viewModel.Tasks[0].OrderIndex);
        Assert.Single(_viewModel.CompactTasks);

        // Then 3: Поле ввода очищается и готово к следующему вводу
        Assert.Equal(string.Empty, _viewModel.NewTaskTitle);
        Assert.True(_viewModel.IsAddingTask);

        // When: Добавление второй задачи
        _viewModel.NewTaskTitle = "Протестировать P/Invoke";
        await _viewModel.AddTaskAsync();

        // Then: Новая задача встает на OrderIndex = 0, а предыдущая сдвигается
        var allInDb = await _taskRepository.GetActiveTasksAsync();
        Assert.Equal(2, allInDb.Count);
        Assert.Equal("Протестировать P/Invoke", _viewModel.Tasks[0].Title);
        Assert.Equal(0, _viewModel.Tasks[0].OrderIndex);
    }

    [Fact]
    public async Task AC04_EscapeKey_CancelsTaskCreationWithoutSaving()
    {
        // Given: открыто поле добавления задачи
        _viewModel.StartAddingTask();
        _viewModel.NewTaskTitle = "Черновик задачи";
        Assert.True(_viewModel.IsAddingTask);

        // When: пользователь нажимает Esc
        _viewModel.CancelAddingTask();

        // Then: поле закрывается, черновик сброшен, в БД ничего не сохранено
        Assert.False(_viewModel.IsAddingTask);
        Assert.Equal(string.Empty, _viewModel.NewTaskTitle);

        var tasksInDb = await _taskRepository.GetActiveTasksAsync();
        Assert.Empty(tasksInDb);
    }

    [Fact]
    public void AC04_GlobalHotkey_AltShiftN_TriggersQuickAddTask()
    {
        // Given: сервис трея с глобальным шорткатом Alt+Shift+N
        using var trayService = new SystemTrayService();
        bool hotkeyTriggered = false;
        trayService.QuickAddTaskRequested += () => hotkeyTriggered = true;

        // When: операционная система передает WM_HOTKEY с HOTKEY_ADD_TASK_ID
        trayService.TriggerHotkey(SystemTrayService.HOTKEY_ADD_TASK_ID);

        // Then: инициируется быстрое добавление задачи
        Assert.True(hotkeyTriggered);
    }
}
