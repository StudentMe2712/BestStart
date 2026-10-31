using System;
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

public class TaskRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _factory;
    private readonly TaskRepository _repository;

    public TaskRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"secondself_tasks_{Guid.NewGuid():N}.db");
        _factory = new SqliteConnectionFactory(_testDbPath);
        new DatabaseMigrationService(_factory).MigrateAsync().GetAwaiter().GetResult();
        _repository = new TaskRepository(_factory);
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
    public async Task AddAndGetActiveTasks_WorksCorrectly()
    {
        var task1 = new ActiveTask { Title = "Task 1", OrderIndex = 0 };
        var task2 = new ActiveTask { Title = "Task 2", OrderIndex = 1 };

        await _repository.AddActiveTaskAsync(task1);
        await _repository.AddActiveTaskAsync(task2);

        var tasks = await _repository.GetActiveTasksAsync();

        Assert.Equal(2, tasks.Count);
        Assert.Equal("Task 1", tasks[0].Title);
        Assert.Equal("Task 2", tasks[1].Title);
    }

    [Fact]
    public async Task UpdateActiveTask_UpdatesFields()
    {
        var task = new ActiveTask { Title = "Original", OrderIndex = 0 };
        await _repository.AddActiveTaskAsync(task);

        task.Title = "Updated";
        task.PostponedDays = 2;
        await _repository.UpdateActiveTaskAsync(task);

        var tasks = await _repository.GetActiveTasksAsync();
        Assert.Single(tasks);
        Assert.Equal("Updated", tasks[0].Title);
        Assert.Equal(2, tasks[0].PostponedDays);
    }

    [Fact]
    public async Task DeleteActiveTask_RemovesTask()
    {
        var task = new ActiveTask { Title = "To Delete" };
        await _repository.AddActiveTaskAsync(task);

        await _repository.DeleteActiveTaskAsync(task.Id);

        var tasks = await _repository.GetActiveTasksAsync();
        Assert.Empty(tasks);
    }

    [Fact]
    public async Task ArchiveTask_MovesTaskToArchivedAndDeletesFromActive()
    {
        var task = new ActiveTask { Title = "To Archive", OrderIndex = 0 };
        await _repository.AddActiveTaskAsync(task);

        var archived = await _repository.ArchiveTaskAsync(task.Id, "2026-10-31");

        Assert.NotNull(archived);
        Assert.Equal(task.Id, archived.OriginalTaskId);
        Assert.Equal("To Archive", archived.Title);
        Assert.Equal("2026-10-31", archived.LogicalDate);
        Assert.Equal("Today", archived.TaskSource);

        var activeTasks = await _repository.GetActiveTasksAsync();
        Assert.Empty(activeTasks);

        var archivedTasks = await _repository.GetArchivedTasksAsync("2026-10-31");
        Assert.Single(archivedTasks);
        Assert.Equal(archived.Id, archivedTasks[0].Id);
    }

    [Fact]
    public async Task ReorderTasks_UpdatesOrderIndices()
    {
        var taskA = new ActiveTask { Title = "A", OrderIndex = 0 };
        var taskB = new ActiveTask { Title = "B", OrderIndex = 1 };
        await _repository.AddActiveTaskAsync(taskA);
        await _repository.AddActiveTaskAsync(taskB);

        await _repository.ReorderTasksAsync(new[]
        {
            (taskA.Id, 1),
            (taskB.Id, 0)
        });

        var tasks = await _repository.GetActiveTasksAsync();
        Assert.Equal("B", tasks[0].Title);
        Assert.Equal("A", tasks[1].Title);
    }
}
