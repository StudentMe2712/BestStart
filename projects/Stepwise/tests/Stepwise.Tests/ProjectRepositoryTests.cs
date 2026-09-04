using Microsoft.Data.Sqlite;
using Stepwise.Core.Models;
using Stepwise.Storage.Repositories;
using Xunit;

namespace Stepwise.Tests;

public class ProjectRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public ProjectRepositoryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    [Fact]
    public void CreateProject_ShouldInitializeDatabaseAndReturnProject()
    {
        // Arrange
        using var repository = new ProjectRepository(_connection, projectRootPath: @"C:\TestProject");

        // Act
        var project = repository.CreateProject("Demo Guide", "Test walkthrough description");

        // Assert
        Assert.NotNull(project);
        Assert.Equal("Demo Guide", project.Name);
        Assert.Equal("Test walkthrough description", project.Description);
        Assert.Equal(@"C:\TestProject", project.RootPath);

        var loaded = repository.LoadProject();
        Assert.NotNull(loaded);
        Assert.Equal(project.Id, loaded.Id);
        Assert.Equal("Demo Guide", loaded.Name);
    }

    [Fact]
    public void SaveStep_And_LoadSteps_ShouldPersistAndRetrieveAllStepProperties()
    {
        // Arrange
        using var repository = new ProjectRepository(_connection, projectRootPath: @"C:\TestProject");
        repository.CreateProject("Test Guide");

        var step1 = new Step(
            Id: Guid.NewGuid(),
            SequenceIndex: 1,
            Timestamp: DateTime.UtcNow,
            Action: ActionType.LeftClick,
            ClickX: 350.5,
            ClickY: 420.0,
            TargetElement: new ElementInfo(
                Name: "Submit",
                ControlType: "Button",
                AutomationId: "btnSubmit",
                ClassName: "WpfButton",
                ProcessName: "calculator",
                ProcessId: 5432,
                WindowTitle: "Calculator Window",
                WindowHandle: 0x998877,
                BoundingRectangle: new BoundingBox(300, 400, 100, 40)
            ),
            ScreenshotPath: "assets/screenshots/step_001.png",
            Title: "Нажмите Submit (Button)",
            Description: "Клик по кнопке отправки",
            Metadata: new Dictionary<string, string> { ["Env"] = "QA" }
        );

        var step2 = new Step(
            Id: Guid.NewGuid(),
            SequenceIndex: 2,
            Timestamp: DateTime.UtcNow.AddSeconds(1),
            Action: ActionType.RightClick,
            ClickX: 500,
            ClickY: 600,
            TargetElement: new ElementInfo(
                Name: "Context Menu",
                ControlType: "MenuItem",
                AutomationId: "menuItemCopy",
                ClassName: "MenuItem",
                ProcessName: "calculator",
                ProcessId: 5432,
                WindowTitle: "Calculator Window",
                WindowHandle: 0x998877,
                BoundingRectangle: new BoundingBox(450, 580, 150, 30)
            ),
            ScreenshotPath: "assets/screenshots/step_002.png",
            Title: "Нажмите правой кнопкой Context Menu",
            Description: "Вызов контекстного меню",
            Metadata: null
        );

        // Act
        repository.SaveStep(step1);
        repository.SaveStep(step2);

        var steps = repository.LoadSteps();

        // Assert
        Assert.Equal(2, steps.Count);

        var loadedStep1 = steps[0];
        Assert.Equal(step1.Id, loadedStep1.Id);
        Assert.Equal(1, loadedStep1.SequenceIndex);
        Assert.Equal(ActionType.LeftClick, loadedStep1.Action);
        Assert.Equal(350.5, loadedStep1.ClickX);
        Assert.Equal(420.0, loadedStep1.ClickY);
        Assert.Equal("Submit", loadedStep1.TargetElement.Name);
        Assert.Equal("Button", loadedStep1.TargetElement.ControlType);
        Assert.Equal("btnSubmit", loadedStep1.TargetElement.AutomationId);
        Assert.Equal("calculator", loadedStep1.TargetElement.ProcessName);
        Assert.Equal(5432, loadedStep1.TargetElement.ProcessId);
        Assert.Equal("assets/screenshots/step_001.png", loadedStep1.ScreenshotPath);
        Assert.Equal("Нажмите Submit (Button)", loadedStep1.Title);
        Assert.Equal("QA", loadedStep1.Metadata?["Env"]);

        var loadedStep2 = steps[1];
        Assert.Equal(step2.Id, loadedStep2.Id);
        Assert.Equal(2, loadedStep2.SequenceIndex);
        Assert.Equal(ActionType.RightClick, loadedStep2.Action);
        Assert.Equal("assets/screenshots/step_002.png", loadedStep2.ScreenshotPath);
    }

    [Fact]
    public void LoadProject_ShouldIncludeLoadedSteps()
    {
        // Arrange
        using var repository = new ProjectRepository(_connection, projectRootPath: @"C:\TestProject");
        repository.CreateProject("Full Project Test");

        var step = new Step(
            Id: Guid.NewGuid(),
            SequenceIndex: 1,
            Timestamp: DateTime.UtcNow,
            Action: ActionType.LeftClick,
            ClickX: 10,
            ClickY: 20,
            TargetElement: ElementInfo.Unknown,
            ScreenshotPath: "assets/screenshots/step_001.png",
            Title: "Step 1"
        );
        repository.SaveStep(step);

        // Act
        var project = repository.LoadProject();

        // Assert
        Assert.NotNull(project);
        Assert.NotNull(project.Steps);
        Assert.Single(project.Steps);
        Assert.Equal(step.Id, project.Steps[0].Id);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
