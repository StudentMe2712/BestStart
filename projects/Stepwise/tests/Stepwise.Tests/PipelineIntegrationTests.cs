using Moq;
using Stepwise.Core.Engine;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Xunit;

namespace Stepwise.Tests;

public class PipelineIntegrationTests
{
    [Fact]
    public void RecordingPipelineEngine_FullPipeline_CoordinatesHookUiaCaptureAndRepository()
    {
        // Arrange
        var mockHook = new TestMouseHookService();
        var mockUia = new Mock<IUIAutomationService>();
        var mockCapture = new Mock<IScreenCaptureService>();
        var mockRepo = new Mock<IProjectRepository>();

        const string projectRoot = @"C:\MockProject";
        mockRepo.SetupGet(r => r.ProjectRootPath).Returns(projectRoot);

        var expectedElement = new ElementInfo(
            Name: "Save Button",
            ControlType: "Button",
            AutomationId: "btnSave",
            ClassName: "Button",
            ProcessName: "notepad",
            ProcessId: 1000,
            WindowTitle: "Untitled",
            WindowHandle: 0x1234,
            BoundingRectangle: new BoundingBox(100, 150, 80, 30)
        );

        mockUia.Setup(u => u.InspectElementAt(120, 160)).Returns(expectedElement);

        const string expectedScreenshotPath = "assets/screenshots/step_001.png";
        mockCapture.Setup(c => c.Capture(
            projectRoot,
            1,
            It.Is<BoundingBox?>(b => b.HasValue && b.Value.Width == 80),
            0x1234
        )).Returns(expectedScreenshotPath);

        Step? recordedStep = null;
        mockRepo.Setup(r => r.SaveStep(It.IsAny<Step>()))
            .Callback<Step>(s => recordedStep = s);

        using var engine = new RecordingPipelineEngine(
            mockHook,
            mockUia.Object,
            mockCapture.Object,
            mockRepo.Object
        );

        Step? eventStep = null;
        engine.StepRecorded += (sender, s) => eventStep = s;

        // Act
        engine.StartRecording();
        mockHook.SimulateClick(120, 160, ActionType.LeftClick);

        // Assert
        // 1. Проверяем вызов UI Automation
        mockUia.Verify(u => u.InspectElementAt(120, 160), Times.Once);

        // 2. Проверяем вызов ScreenCapture с правильными параметрами
        mockCapture.Verify(c => c.Capture(
            projectRoot,
            1,
            It.IsAny<BoundingBox?>(),
            0x1234
        ), Times.Once);

        // 3. Проверяем сохранение в репозиторий
        mockRepo.Verify(r => r.SaveStep(It.IsAny<Step>()), Times.Once);

        // 4. Проверяем свойства сохраненного шага
        Assert.NotNull(recordedStep);
        Assert.Equal(1, recordedStep.SequenceIndex);
        Assert.Equal(expectedScreenshotPath, recordedStep.ScreenshotPath);
        Assert.Equal("Save Button", recordedStep.TargetElement.Name);
        Assert.Equal("notepad", recordedStep.TargetElement.ProcessName);

        // 5. Проверяем генерацию события
        Assert.NotNull(eventStep);
        Assert.Equal(recordedStep.Id, eventStep.Id);

        engine.StopRecording();
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
}
