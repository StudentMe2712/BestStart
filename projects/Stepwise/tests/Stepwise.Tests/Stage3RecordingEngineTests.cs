using Moq;
using Stepwise.Core.Engine;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Stepwise.Core.Policy;
using Stepwise.WindowsIntegration.Capture;
using Xunit;

namespace Stepwise.Tests;

public class Stage3RecordingEngineTests
{
    #region 1. DefaultRecordingPolicy Tests

    [Fact]
    public void DefaultRecordingPolicy_EmptyExclusions_AllowsNormalAction()
    {
        var policy = new DefaultRecordingPolicy();
        var context = new WindowContext(1, 100, "calc", "Calculator", new BoundingBox(0, 0, 100, 100), DateTime.UtcNow);
        var action = SemanticAction.CreateMouseClick(SemanticActionType.LeftClick, 50, 50, context, DateTime.UtcNow);
        var target = new ElementInfo("7", "Button", "btn7", "Button", "calc", 100, "Calculator", 1, new BoundingBox(40, 40, 20, 20));

        var decision = policy.Evaluate(action, target);

        Assert.Equal(RecordingPolicyDecision.Allow, decision);
        Assert.Empty(policy.ExcludedProcesses);
    }

    [Theory]
    [InlineData("notepad", "notepad")]
    [InlineData("notepad", "notepad.exe")]
    [InlineData("notepad.exe", "notepad")]
    [InlineData("notepad", "NOTEPAD")]
    [InlineData("notepad.exe", "NOTEPAD.EXE")]
    public void DefaultRecordingPolicy_ExcludedProcesses_SuppressesMatchingProcesses(string configExclusion, string targetProcess)
    {
        var policy = new DefaultRecordingPolicy(new[] { configExclusion });
        var context = new WindowContext(1, 100, targetProcess, "Editor", new BoundingBox(0, 0, 100, 100), DateTime.UtcNow);
        var action = SemanticAction.CreateMouseClick(SemanticActionType.LeftClick, 50, 50, context, DateTime.UtcNow);
        var target = new ElementInfo("Text", "Edit", "txtEdit", "Edit", targetProcess, 100, "Editor", 1, new BoundingBox(10, 10, 50, 50));

        var decision = policy.Evaluate(action, target);

        Assert.Equal(RecordingPolicyDecision.Suppress, decision);
    }

    [Fact]
    public void DefaultRecordingPolicy_AddAndRemoveExcludedProcess_UpdatesCollectionAndEvaluation()
    {
        var policy = new DefaultRecordingPolicy();
        var context = new WindowContext(1, 100, "secretApp", "App", new BoundingBox(0, 0, 100, 100), DateTime.UtcNow);
        var action = SemanticAction.CreateMouseClick(SemanticActionType.LeftClick, 50, 50, context, DateTime.UtcNow);
        var target = new ElementInfo("OK", "Button", "btnOK", "Button", "secretApp", 100, "App", 1, new BoundingBox(0, 0, 10, 10));

        Assert.Equal(RecordingPolicyDecision.Allow, policy.Evaluate(action, target));

        policy.AddExcludedProcess("secretApp");
        Assert.Contains("secretApp", policy.ExcludedProcesses);
        Assert.Equal(RecordingPolicyDecision.Suppress, policy.Evaluate(action, target));

        bool removed = policy.RemoveExcludedProcess("secretApp");
        Assert.True(removed);
        Assert.DoesNotContain("secretApp", policy.ExcludedProcesses);
        Assert.Equal(RecordingPolicyDecision.Allow, policy.Evaluate(action, target));
    }

    [Fact]
    public void DefaultRecordingPolicy_PasswordOrSensitive_SuppressesTextInputByDefault()
    {
        var policy = new DefaultRecordingPolicy();
        var context = WindowContext.Empty;
        var textAction = SemanticAction.CreateTextInput("SuperSecret123", context, DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow);
        var pwdTarget = new ElementInfo("Password", "Edit", "pwdBox", "PasswordBox", "loginApp", 100, "Login", 1, new BoundingBox(0, 0, 100, 30), IsPassword: true);

        var decision = policy.Evaluate(textAction, pwdTarget);

        Assert.Equal(RecordingPolicyDecision.Suppress, decision);
    }

    [Fact]
    public void DefaultRecordingPolicy_PasswordWithMaskSensitiveInputs_ReturnsMask()
    {
        var policy = new DefaultRecordingPolicy(maskSensitiveInputs: true);
        var context = WindowContext.Empty;
        var textAction = SemanticAction.CreateTextInput("SuperSecret123", context, DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow);
        var pwdTarget = new ElementInfo("Password", "Edit", "pwdBox", "PasswordBox", "loginApp", 100, "Login", 1, new BoundingBox(0, 0, 100, 30), IsPassword: true);

        var decision = policy.Evaluate(textAction, pwdTarget);

        Assert.Equal(RecordingPolicyDecision.Mask, decision);
    }

    [Fact]
    public void DefaultRecordingPolicy_ClickOnPasswordElement_ReturnsMask()
    {
        var policy = new DefaultRecordingPolicy();
        var context = WindowContext.Empty;
        var clickAction = SemanticAction.CreateMouseClick(SemanticActionType.LeftClick, 50, 15, context, DateTime.UtcNow);
        var pwdTarget = new ElementInfo("Password", "Edit", "pwdBox", "PasswordBox", "loginApp", 100, "Login", 1, new BoundingBox(0, 0, 100, 30), IsPassword: true);

        var decision = policy.Evaluate(clickAction, pwdTarget);

        Assert.Equal(RecordingPolicyDecision.Mask, decision);
    }

    #endregion

    #region 2. StepDetector Tests

    [Fact]
    public void StepDetector_SuppressDecision_ReturnsNull()
    {
        var detector = new StepDetector();
        var action = SemanticAction.CreateMouseClick(SemanticActionType.LeftClick, 10, 10, WindowContext.Empty, DateTime.UtcNow);
        var target = ElementInfo.Unknown;

        var step = detector.DetectStep(action, target, RecordingPolicyDecision.Suppress, 1);

        Assert.Null(step);
    }

    [Fact]
    public void StepDetector_LeftClick_GeneratesExpectedTitleAndDescription()
    {
        var detector = new StepDetector();
        var now = DateTime.UtcNow;
        var action = SemanticAction.CreateMouseClick(SemanticActionType.LeftClick, 105, 205, WindowContext.Empty, now);
        var target = new ElementInfo("Save", "Button", "btnSave", "Button", "notepad", 10, "Notepad", 1, new BoundingBox(100, 200, 50, 20));

        var step = detector.DetectStep(action, target, RecordingPolicyDecision.Allow, 1);

        Assert.NotNull(step);
        Assert.Equal(1, step.SequenceIndex);
        Assert.Equal(ActionType.LeftClick, step.Action);
        Assert.Equal(105, step.ClickX);
        Assert.Equal(205, step.ClickY);
        Assert.Equal("Click \"Save\"", step.Title);
        Assert.Equal("Click the Save (Button) in notepad.", step.Description);
        Assert.NotNull(step.Metadata);
        Assert.Equal("notepad", step.Metadata["ProcessName"]);
        Assert.Equal("Save", step.TargetElement.Name);
    }

    [Fact]
    public void StepDetector_ClicksWithoutTargetName_FallbacksToControlType()
    {
        var detector = new StepDetector();
        var action = SemanticAction.CreateMouseClick(SemanticActionType.DoubleLeftClick, 10, 10, WindowContext.Empty, DateTime.UtcNow);
        var target = new ElementInfo("", "ListItem", "item1", "ListViewItem", "explorer", 10, "Explorer", 1, new BoundingBox(0, 0, 20, 20));

        var step = detector.DetectStep(action, target, RecordingPolicyDecision.Allow, 2);

        Assert.NotNull(step);
        Assert.Equal("Double-click ListItem", step.Title);
        Assert.Equal("Double-click the ListItem in explorer.", step.Description);
    }

    [Fact]
    public void StepDetector_RightAndMiddleClick_GeneratesExpectedTitles()
    {
        var detector = new StepDetector();
        var target = new ElementInfo("Canvas", "Pane", "canvas1", "Canvas", "paint", 10, "Paint", 1, new BoundingBox(0, 0, 100, 100));

        var rightClick = SemanticAction.CreateMouseClick(SemanticActionType.RightClick, 10, 10, WindowContext.Empty, DateTime.UtcNow);
        var rightStep = detector.DetectStep(rightClick, target, RecordingPolicyDecision.Allow, 1);
        Assert.NotNull(rightStep);
        Assert.Equal("Right-click \"Canvas\"", rightStep.Title);
        Assert.Equal("Right-click the Canvas (Pane) in paint.", rightStep.Description);

        var middleClick = SemanticAction.CreateMouseClick(SemanticActionType.MiddleClick, 10, 10, WindowContext.Empty, DateTime.UtcNow);
        var middleStep = detector.DetectStep(middleClick, target, RecordingPolicyDecision.Allow, 2);
        Assert.NotNull(middleStep);
        Assert.Equal("Middle-click \"Canvas\"", middleStep.Title);
        Assert.Equal("Middle-click the Canvas (Pane) in paint.", middleStep.Description);
    }

    [Fact]
    public void StepDetector_TextInputNormal_GeneratesExpectedTitleAndMetadata()
    {
        var detector = new StepDetector();
        var startedAt = DateTime.UtcNow.AddSeconds(-2);
        var completedAt = DateTime.UtcNow;
        var action = SemanticAction.CreateTextInput("Hello World", WindowContext.Empty, startedAt, completedAt);
        var target = new ElementInfo("SearchBox", "Edit", "txtSearch", "TextBox", "browser", 10, "Browser", 1, new BoundingBox(10, 10, 200, 30));

        var step = detector.DetectStep(action, target, RecordingPolicyDecision.Allow, 3);

        Assert.NotNull(step);
        Assert.Equal(ActionType.TextInput, step.Action);
        Assert.Equal("Type \"Hello World\" into \"SearchBox\"", step.Title);
        Assert.Equal("Type \"Hello World\" into SearchBox in browser.", step.Description);
        Assert.NotNull(step.Metadata);
        Assert.Equal(startedAt.ToString("o"), step.Metadata["StartedAt"]);
        Assert.Equal(completedAt.ToString("o"), step.Metadata["CompletedAt"]);
        Assert.Equal("11", step.Metadata["CharacterCount"]);
        Assert.False(step.Metadata.ContainsKey("IsMasked"));
    }

    [Fact]
    public void StepDetector_TextInputMasked_GeneratesMaskedMetadataAndDescription()
    {
        var detector = new StepDetector();
        var action = SemanticAction.CreateTextInput("SecretPassword", WindowContext.Empty, DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow);
        var target = new ElementInfo("Password", "Edit", "pwdField", "PasswordBox", "auth", 10, "Login", 1, new BoundingBox(10, 10, 100, 30), IsPassword: true);

        var step = detector.DetectStep(action, target, RecordingPolicyDecision.Mask, 4);

        Assert.NotNull(step);
        Assert.Equal("Type text into \"Password\"", step.Title);
        Assert.Equal("Type sensitive text into Password.", step.Description);
        Assert.NotNull(step.Metadata);
        Assert.Equal("true", step.Metadata["IsMasked"]);
    }

    [Fact]
    public void StepDetector_ShortcutAndKeyPress_GeneratesExpectedTitles()
    {
        var detector = new StepDetector();
        var target = new ElementInfo("Document", "Edit", "doc", "Edit", "word", 10, "Word", 1, new BoundingBox(0, 0, 100, 100));

        var shortcut = SemanticAction.CreateShortcut(83, "S", KeyboardModifiers.Control, WindowContext.Empty, DateTime.UtcNow);
        var step1 = detector.DetectStep(shortcut, target, RecordingPolicyDecision.Allow, 1);
        Assert.NotNull(step1);
        Assert.Equal("Press Control+S", step1.Title);
        Assert.Equal("Press keyboard shortcut Control+S in word.", step1.Description);

        var keyPress = SemanticAction.CreateKeyPress(13, "Enter", KeyboardModifiers.None, WindowContext.Empty, DateTime.UtcNow);
        var step2 = detector.DetectStep(keyPress, target, RecordingPolicyDecision.Allow, 2);
        Assert.NotNull(step2);
        Assert.Equal("Press Enter", step2.Title);
        Assert.Equal("Press Enter key in word.", step2.Description);
    }

    [Fact]
    public void StepDetector_MissingActionCoordinates_UsesTargetBoundingRectangleCenter()
    {
        var detector = new StepDetector();
        var action = SemanticAction.CreateTextInput("text", WindowContext.Empty, DateTime.UtcNow, DateTime.UtcNow);
        var target = new ElementInfo("Box", "Edit", "b", "Edit", "app", 1, "App", 1, new BoundingBox(100, 200, 50, 40));

        var step = detector.DetectStep(action, target, RecordingPolicyDecision.Allow, 1);

        Assert.NotNull(step);
        Assert.Equal(125.0, step.ClickX); // 100 + 50/2
        Assert.Equal(220.0, step.ClickY); // 200 + 40/2
    }

    #endregion

    #region 3. CaptureCoordinator Tests

    [Fact]
    public async Task CaptureCoordinator_NullServices_ReturnsNull()
    {
        var coordinator = new CaptureCoordinator(null, null);
        var target = ElementInfo.Unknown;

        var result = await coordinator.CaptureStepAsync(1, target);

        Assert.Null(result);
    }

    [Fact]
    public async Task CaptureCoordinator_ValidCapture_CallsServiceAndReturnsPath()
    {
        var captureServiceMock = new Mock<IScreenCaptureService>();
        var repoMock = new Mock<IProjectRepository>();
        repoMock.SetupGet(r => r.ProjectRootPath).Returns(@"C:\TestProject");

        var target = new ElementInfo("Btn", "Button", "b", "Button", "app", 1, "Title", 1234, new BoundingBox(10, 20, 30, 40));
        captureServiceMock
            .Setup(c => c.Capture(@"C:\TestProject", 1, target.BoundingRectangle, 1234))
            .Returns("assets/screenshots/step_001.png");

        var coordinator = new CaptureCoordinator(captureServiceMock.Object, repoMock.Object);

        var result = await coordinator.CaptureStepAsync(1, target);

        Assert.Equal("assets/screenshots/step_001.png", result);
        captureServiceMock.Verify(c => c.Capture(@"C:\TestProject", 1, target.BoundingRectangle, 1234), Times.Once);
    }

    [Fact]
    public async Task CaptureCoordinator_CaptureThrows_CatchesAndReturnsNullWithoutCrashing()
    {
        var captureServiceMock = new Mock<IScreenCaptureService>();
        var repoMock = new Mock<IProjectRepository>();
        repoMock.SetupGet(r => r.ProjectRootPath).Returns(@"C:\TestProject");

        var target = ElementInfo.Unknown;
        captureServiceMock
            .Setup(c => c.Capture(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<BoundingBox?>(), It.IsAny<long>()))
            .Throws(new InvalidOperationException("GDI capture failed"));

        var coordinator = new CaptureCoordinator(captureServiceMock.Object, repoMock.Object);

        var result = await coordinator.CaptureStepAsync(1, target);

        Assert.Null(result);
    }

    [Fact]
    public async Task CaptureCoordinator_CancelledToken_ReturnsNull()
    {
        var captureServiceMock = new Mock<IScreenCaptureService>();
        var repoMock = new Mock<IProjectRepository>();
        var coordinator = new CaptureCoordinator(captureServiceMock.Object, repoMock.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await coordinator.CaptureStepAsync(1, ElementInfo.Unknown, cts.Token);

        Assert.Null(result);
    }

    #endregion

    #region 4. RecordingEngine Tests

    [Fact]
    public async Task RecordingEngine_FullPipeline_CoordinatesAllStages()
    {
        // 1. Arrange Mocks
        var inputMonitorMock = new Mock<IInputMonitoringService>();
        var windowTrackerMock = new Mock<IActiveWindowTracker>();
        var correlator = new EventCorrelator();
        var targetResolverMock = new Mock<ITargetResolver>();
        var policy = new DefaultRecordingPolicy();
        var detector = new StepDetector();
        var captureCoordinatorMock = new Mock<ICaptureCoordinator>();
        var repositoryMock = new Mock<IProjectRepository>();

        var expectedTarget = new ElementInfo("Submit", "Button", "btnSubmit", "Button", "demoApp", 100, "Demo", 1, new BoundingBox(10, 10, 50, 20));
        targetResolverMock
            .Setup(r => r.ResolveTargetAsync(It.IsAny<SemanticAction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTarget);

        captureCoordinatorMock
            .Setup(c => c.CaptureStepAsync(It.IsAny<int>(), It.IsAny<ElementInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("assets/screenshots/step_001.png");

        Step? savedStep = null;
        repositoryMock
            .Setup(r => r.SaveStep(It.IsAny<Step>()))
            .Callback<Step>(s => savedStep = s);

        using var engine = new RecordingEngine(
            inputMonitorMock.Object,
            windowTrackerMock.Object,
            correlator,
            targetResolverMock.Object,
            policy,
            detector,
            captureCoordinatorMock.Object,
            repositoryMock.Object);

        var recordedSteps = new List<Step>();
        engine.StepRecorded += (_, s) => recordedSteps.Add(s);

        // 2. Start
        engine.StartRecording();
        Assert.Equal(RecordingSessionState.Recording, engine.State);
        Assert.True(engine.IsRecording);
        inputMonitorMock.Verify(m => m.Start(), Times.Once);
        windowTrackerMock.Verify(w => w.Start(), Times.Once);

        // 3. Simulate raw mouse input (Click = Down + Up)
        var now = DateTime.UtcNow;
        inputMonitorMock.Raise(m => m.MouseEventReceived += null, inputMonitorMock.Object,
            new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 30, 20, 0, now));
        inputMonitorMock.Raise(m => m.MouseEventReceived += null, inputMonitorMock.Object,
            new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 30, 20, 0, now.AddMilliseconds(50)));

        // 4. Stop
        await engine.StopRecordingAsync();
        Assert.Equal(RecordingSessionState.Completed, engine.State);
        Assert.False(engine.IsRecording);

        // 5. Verify results
        Assert.Single(recordedSteps);
        var step = recordedSteps[0];
        Assert.Equal(1, step.SequenceIndex);
        Assert.Equal(ActionType.LeftClick, step.Action);
        Assert.Equal(30, step.ClickX);
        Assert.Equal(20, step.ClickY);
        Assert.Equal("Click \"Submit\"", step.Title);
        Assert.Equal("assets/screenshots/step_001.png", step.ScreenshotPath);
        Assert.NotNull(savedStep);
        Assert.Equal(step.Id, savedStep.Id);
    }

    [Fact]
    public async Task RecordingEngine_PolicySuppression_DoesNotRecordStep()
    {
        var inputMonitorMock = new Mock<IInputMonitoringService>();
        var correlator = new EventCorrelator();
        var targetResolverMock = new Mock<ITargetResolver>();
        var policy = new DefaultRecordingPolicy(new[] { "blockedApp" });
        var detector = new StepDetector();
        var captureCoordinatorMock = new Mock<ICaptureCoordinator>();
        var repositoryMock = new Mock<IProjectRepository>();

        var suppressedTarget = new ElementInfo("Hidden", "Button", "b", "Button", "blockedApp", 100, "Blocked", 1, new BoundingBox(0, 0, 10, 10));
        targetResolverMock
            .Setup(r => r.ResolveTargetAsync(It.IsAny<SemanticAction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(suppressedTarget);

        using var engine = new RecordingEngine(
            inputMonitorMock.Object,
            null,
            correlator,
            targetResolverMock.Object,
            policy,
            detector,
            captureCoordinatorMock.Object,
            repositoryMock.Object);

        var recordedSteps = new List<Step>();
        engine.StepRecorded += (_, s) => recordedSteps.Add(s);

        engine.StartRecording();

        var now = DateTime.UtcNow;
        inputMonitorMock.Raise(m => m.MouseEventReceived += null, inputMonitorMock.Object,
            new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 5, 5, 0, now));
        inputMonitorMock.Raise(m => m.MouseEventReceived += null, inputMonitorMock.Object,
            new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 5, 5, 0, now.AddMilliseconds(50)));

        await engine.StopRecordingAsync();

        Assert.Empty(recordedSteps);
        repositoryMock.Verify(r => r.SaveStep(It.IsAny<Step>()), Times.Never);
        captureCoordinatorMock.Verify(c => c.CaptureStepAsync(It.IsAny<int>(), It.IsAny<ElementInfo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecordingEngine_RestartSafety_CanRecordStopAndRecordAgain()
    {
        var inputMonitorMock = new Mock<IInputMonitoringService>();
        var correlator = new EventCorrelator();
        var targetResolverMock = new Mock<ITargetResolver>();
        var policy = new DefaultRecordingPolicy();
        var detector = new StepDetector();
        var captureCoordinatorMock = new Mock<ICaptureCoordinator>();
        var repositoryMock = new Mock<IProjectRepository>();

        var target = new ElementInfo("Item", "Button", "btn", "Button", "app", 1, "App", 1, new BoundingBox(0, 0, 10, 10));
        targetResolverMock
            .Setup(r => r.ResolveTargetAsync(It.IsAny<SemanticAction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        using var engine = new RecordingEngine(
            inputMonitorMock.Object,
            null,
            correlator,
            targetResolverMock.Object,
            policy,
            detector,
            captureCoordinatorMock.Object,
            repositoryMock.Object);

        var recordedSteps = new List<Step>();
        engine.StepRecorded += (_, s) => recordedSteps.Add(s);

        // Session 1
        engine.StartRecording();
        Assert.Equal(RecordingSessionState.Recording, engine.State);

        var now = DateTime.UtcNow;
        inputMonitorMock.Raise(m => m.MouseEventReceived += null, inputMonitorMock.Object,
            new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 5, 5, 0, now));
        inputMonitorMock.Raise(m => m.MouseEventReceived += null, inputMonitorMock.Object,
            new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 5, 5, 0, now.AddMilliseconds(50)));

        await engine.StopRecordingAsync();
        Assert.Equal(RecordingSessionState.Completed, engine.State);
        Assert.Single(recordedSteps);

        // Session 2 - Restart
        engine.StartRecording();
        Assert.Equal(RecordingSessionState.Recording, engine.State);

        now = DateTime.UtcNow;
        inputMonitorMock.Raise(m => m.MouseEventReceived += null, inputMonitorMock.Object,
            new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 10, 10, 0, now));
        inputMonitorMock.Raise(m => m.MouseEventReceived += null, inputMonitorMock.Object,
            new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 10, 10, 0, now.AddMilliseconds(50)));

        await engine.StopRecordingAsync();
        Assert.Equal(RecordingSessionState.Completed, engine.State);
        Assert.Equal(2, recordedSteps.Count);
        Assert.Equal(1, recordedSteps[1].SequenceIndex); // Sequence index resets for new session!
    }

    [Fact]
    public void RecordingEngine_PauseAndResume_IgnoresInputsWhilePaused()
    {
        var inputMonitorMock = new Mock<IInputMonitoringService>();
        var correlator = new EventCorrelator();
        var targetResolverMock = new Mock<ITargetResolver>();
        var policy = new DefaultRecordingPolicy();
        var detector = new StepDetector();
        var captureCoordinatorMock = new Mock<ICaptureCoordinator>();
        var repositoryMock = new Mock<IProjectRepository>();

        targetResolverMock
            .Setup(r => r.ResolveTargetAsync(It.IsAny<SemanticAction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ElementInfo.Unknown);

        using var engine = new RecordingEngine(
            inputMonitorMock.Object,
            null,
            correlator,
            targetResolverMock.Object,
            policy,
            detector,
            captureCoordinatorMock.Object,
            repositoryMock.Object);

        var recordedSteps = new List<Step>();
        engine.StepRecorded += (_, s) => recordedSteps.Add(s);

        engine.StartRecording();
        Assert.Equal(RecordingSessionState.Recording, engine.State);

        engine.PauseRecording();
        Assert.Equal(RecordingSessionState.Paused, engine.State);
        Assert.False(engine.IsRecording);

        // Input during pause should be discarded
        var now = DateTime.UtcNow;
        inputMonitorMock.Raise(m => m.MouseEventReceived += null, inputMonitorMock.Object,
            new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 5, 5, 0, now));
        inputMonitorMock.Raise(m => m.MouseEventReceived += null, inputMonitorMock.Object,
            new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 5, 5, 0, now.AddMilliseconds(50)));

        engine.ResumeRecording();
        Assert.Equal(RecordingSessionState.Recording, engine.State);
        Assert.True(engine.IsRecording);

        engine.StopRecording();
        Assert.Equal(RecordingSessionState.Completed, engine.State);
        Assert.Empty(recordedSteps);
    }

    #endregion
}
