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

    [Fact]
    public void UpdateStepDetails_ShouldUpdateOnlyTitleAndDescription()
    {
        // Arrange
        using var repository = new ProjectRepository(_connection, projectRootPath: @"C:\TestProject");
        repository.CreateProject("Update Details Test");

        var originalTimestamp = new DateTime(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);
        var originalElement = new ElementInfo(
            Name: "SubmitButton",
            ControlType: "Button",
            AutomationId: "btn_submit",
            ClassName: "WpfButton",
            ProcessName: "notepad",
            ProcessId: 1234,
            WindowTitle: "Untitled - Notepad",
            WindowHandle: 0x12345,
            BoundingRectangle: new BoundingBox(100, 200, 50, 25)
        );

        var originalStep = new Step(
            Id: Guid.NewGuid(),
            SequenceIndex: 1,
            Timestamp: originalTimestamp,
            Action: ActionType.LeftClick,
            ClickX: 125.0,
            ClickY: 212.5,
            TargetElement: originalElement,
            ScreenshotPath: "assets/step_1.png",
            Title: "Original Title",
            Description: "Original Description",
            Metadata: new Dictionary<string, string> { ["Key"] = "Val" }
        );

        repository.SaveStep(originalStep);

        const string newTitle = "Updated Title";
        const string newDescription = "Updated Description";

        // Act
        repository.UpdateStepDetails(originalStep.Id, newTitle, newDescription);

        // Assert
        var steps = repository.LoadSteps();
        Assert.Single(steps);

        var updatedStep = steps[0];
        Assert.Equal(originalStep.Id, updatedStep.Id);
        Assert.Equal(originalStep.SequenceIndex, updatedStep.SequenceIndex);
        Assert.Equal(originalStep.Timestamp.ToUniversalTime(), updatedStep.Timestamp.ToUniversalTime());
        Assert.Equal(originalStep.Action, updatedStep.Action);
        Assert.Equal(originalStep.ClickX, updatedStep.ClickX);
        Assert.Equal(originalStep.ClickY, updatedStep.ClickY);
        Assert.Equal(originalStep.ScreenshotPath, updatedStep.ScreenshotPath);

        // TargetElement details
        Assert.Equal(originalElement.Name, updatedStep.TargetElement.Name);
        Assert.Equal(originalElement.ControlType, updatedStep.TargetElement.ControlType);
        Assert.Equal(originalElement.AutomationId, updatedStep.TargetElement.AutomationId);
        Assert.Equal(originalElement.ClassName, updatedStep.TargetElement.ClassName);
        Assert.Equal(originalElement.ProcessName, updatedStep.TargetElement.ProcessName);
        Assert.Equal(originalElement.ProcessId, updatedStep.TargetElement.ProcessId);
        Assert.Equal(originalElement.WindowTitle, updatedStep.TargetElement.WindowTitle);
        Assert.Equal(originalElement.WindowHandle, updatedStep.TargetElement.WindowHandle);
        Assert.Equal(originalElement.BoundingRectangle.X, updatedStep.TargetElement.BoundingRectangle.X);
        Assert.Equal(originalElement.BoundingRectangle.Y, updatedStep.TargetElement.BoundingRectangle.Y);
        Assert.Equal(originalElement.BoundingRectangle.Width, updatedStep.TargetElement.BoundingRectangle.Width);
        Assert.Equal(originalElement.BoundingRectangle.Height, updatedStep.TargetElement.BoundingRectangle.Height);

        // Title and Description updated
        Assert.Equal(newTitle, updatedStep.Title);
        Assert.Equal(newDescription, updatedStep.Description);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
