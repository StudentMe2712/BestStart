using System.Text.Json;
using Stepwise.Core.Engine;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Stepwise.WindowsIntegration.Automation;
using Stepwise.WindowsIntegration.Hooks;
using Xunit;

namespace Stepwise.Tests;

public class PipelineAndModelTests
{
    [Fact]
    public void Step_SerializationAndDeserialization_ShouldPreserveAllFields()
    {
        // Arrange
        var boundingBox = new BoundingBox(100.5, 200.0, 320.0, 48.0);
        var elementInfo = new ElementInfo(
            Name: "Сохранить как...",
            ControlType: "Button",
            AutomationId: "btnSaveAs",
            ClassName: "ButtonControl",
            ProcessName: "notepad",
            ProcessId: 1234,
            WindowTitle: "Безымянный — Блокнот",
            WindowHandle: 0x00010203,
            BoundingRectangle: boundingBox
        );

        var originalStep = new Step(
            Id: Guid.NewGuid(),
            SequenceIndex: 1,
            Timestamp: DateTime.UtcNow,
            Action: ActionType.LeftClick,
            ClickX: 150.0,
            ClickY: 220.0,
            TargetElement: elementInfo,
            ScreenshotPath: @"C:\Stepwise\Assets\step_1.png",
            Title: "Нажмите \"Сохранить как...\" (Button)",
            Description: "Клик по кнопке сохранения",
            Metadata: new Dictionary<string, string>
            {
                ["OS"] = "Windows 11",
                ["MonitorDpi"] = "96"
            }
        );

        // Act
        var json = JsonSerializer.Serialize(originalStep);
        var deserializedStep = JsonSerializer.Deserialize<Step>(json);

        // Assert
        Assert.NotNull(deserializedStep);
        Assert.Equal(originalStep.Id, deserializedStep.Id);
        Assert.Equal(originalStep.SequenceIndex, deserializedStep.SequenceIndex);
        Assert.Equal(originalStep.Action, deserializedStep.Action);
        Assert.Equal(originalStep.ClickX, deserializedStep.ClickX);
        Assert.Equal(originalStep.ClickY, deserializedStep.ClickY);
        Assert.Equal(originalStep.TargetElement.Name, deserializedStep.TargetElement.Name);
        Assert.Equal(originalStep.TargetElement.ControlType, deserializedStep.TargetElement.ControlType);
        Assert.Equal(originalStep.TargetElement.AutomationId, deserializedStep.TargetElement.AutomationId);
        Assert.Equal(originalStep.TargetElement.BoundingRectangle.Width, deserializedStep.TargetElement.BoundingRectangle.Width);
        Assert.Equal(originalStep.Metadata?["OS"], deserializedStep.Metadata?["OS"]);
    }

    [Fact]
    public void RecordingPipelineEngine_WhenMouseClickFired_GeneratesEnrichedStep()
    {
        // Arrange
        var mockHook = new TestMouseHookService();
        var mockUia = new TestUiaService(new ElementInfo(
            Name: "OK",
            ControlType: "Button",
            AutomationId: "1",
            ClassName: "Button",
            ProcessName: "explorer",
            ProcessId: 4444,
            WindowTitle: "Свойства",
            WindowHandle: 0x12345,
            BoundingRectangle: new BoundingBox(50, 60, 80, 25)
        ));

        using var engine = new RecordingPipelineEngine(mockHook, mockUia);
        Step? capturedStep = null;
        engine.StepRecorded += (sender, step) => capturedStep = step;

        // Act
        engine.StartRecording();
        Assert.True(engine.IsRecording);

        mockHook.SimulateClick(100, 200, ActionType.LeftClick);

        // Assert
        Assert.NotNull(capturedStep);
        Assert.Equal(1, capturedStep.SequenceIndex);
        Assert.Equal(ActionType.LeftClick, capturedStep.Action);
        Assert.Equal(100, capturedStep.ClickX);
        Assert.Equal(200, capturedStep.ClickY);
        Assert.Equal("OK", capturedStep.TargetElement.Name);
        Assert.Equal("Button", capturedStep.TargetElement.ControlType);
        Assert.Equal("explorer", capturedStep.TargetElement.ProcessName);
        Assert.Contains("OK", capturedStep.Title);

        engine.StopRecording();
        Assert.False(engine.IsRecording);
    }

    [Fact]
    public void RecordingPipelineEngine_MultipleClicks_IncrementsSequenceIndices()
    {
        // Arrange
        var mockHook = new TestMouseHookService();
        var mockUia = new TestUiaService(new ElementInfo(
            Name: "Item",
            ControlType: "ListItem",
            AutomationId: "list_1",
            ClassName: "ListViewItem",
            ProcessName: "app",
            ProcessId: 100,
            WindowTitle: "List Window",
            WindowHandle: 0x222,
            BoundingRectangle: new BoundingBox(10, 20, 100, 30)
        ));

        using var engine = new RecordingPipelineEngine(mockHook, mockUia);
        var capturedSteps = new List<Step>();
        engine.StepRecorded += (sender, step) => capturedSteps.Add(step);

        // Act
        engine.StartRecording();
        mockHook.SimulateClick(10, 20, ActionType.LeftClick);
        mockHook.SimulateClick(30, 40, ActionType.RightClick);
        mockHook.SimulateClick(50, 60, ActionType.DoubleLeftClick);
        engine.StopRecording();

        // Assert
        Assert.Equal(3, capturedSteps.Count);
        Assert.Equal(1, capturedSteps[0].SequenceIndex);
        Assert.Equal(ActionType.LeftClick, capturedSteps[0].Action);
        Assert.Equal(2, capturedSteps[1].SequenceIndex);
        Assert.Equal(ActionType.RightClick, capturedSteps[1].Action);
        Assert.Equal(3, capturedSteps[2].SequenceIndex);
        Assert.Equal(ActionType.DoubleLeftClick, capturedSteps[2].Action);
    }

    [Fact]
    public void UIAutomationService_InspectElementAtInvalidPoint_ReturnsGracefullyWithoutThrowing()
    {
        // Arrange
        var uia = new UIAutomationService();

        // Act - инспекция точки далеко за пределами экрана
        var element = uia.InspectElementAt(-99999, -99999);

        // Assert
        Assert.NotNull(element);
        Assert.NotNull(element.ProcessName);
        Assert.NotNull(element.ControlType);
    }

    [Fact]
    public void LowLevelMouseHookService_Lifecycle_StartsAndStopsCleanly()
    {
        // Arrange & Act
        using var hook = new LowLevelMouseHookService();
        Assert.False(hook.IsRunning);

        hook.Start();
        Assert.True(hook.IsRunning);

        hook.Stop();
        Assert.False(hook.IsRunning);
    }

    [Fact]
    public void BoundingBox_Empty_BehavesCorrectly()
    {
        var emptyBox = BoundingBox.Empty;
        Assert.True(emptyBox.IsEmpty);

        var validBox = new BoundingBox(10, 20, 100, 200);
        Assert.False(validBox.IsEmpty);
        Assert.Equal(10, validBox.X);
        Assert.Equal(20, validBox.Y);
        Assert.Equal(100, validBox.Width);
        Assert.Equal(200, validBox.Height);
    }

    private sealed class TestMouseHookService : IMouseHookService
    {
        public event EventHandler<MouseClickEvent>? MouseClicked;
        public bool IsRunning { get; private set; }

        public void Start() => IsRunning = true;
        public void Stop() => IsRunning = false;
        public void Dispose() => Stop();

        public void SimulateClick(int x, int y, ActionType action)
        {
            MouseClicked?.Invoke(this, new MouseClickEvent(x, y, action, DateTime.UtcNow));
        }
    }

    private sealed class TestUiaService : IUIAutomationService
    {
        private readonly ElementInfo _result;

        public TestUiaService(ElementInfo result) => _result = result;

        public ElementInfo InspectElementAt(int x, int y) => _result;
    }
}
