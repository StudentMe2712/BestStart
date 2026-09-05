using System.Text.Json;
using Moq;
using Stepwise.Core.Engine;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Xunit;

namespace Stepwise.Tests;

public class Stage2CoreArchitectureTests
{
    #region 1. SemanticAction & WindowContext Tests

    [Fact]
    public void WindowContext_Empty_HasExpectedDefaults()
    {
        var empty = WindowContext.Empty;

        Assert.Equal(0, empty.WindowHandle);
        Assert.Equal(0, empty.ProcessId);
        Assert.Equal(string.Empty, empty.ProcessName);
        Assert.Equal(string.Empty, empty.WindowTitle);
        Assert.True(empty.Bounds.IsEmpty);
        Assert.Equal(DateTime.MinValue, empty.Timestamp);
    }

    [Fact]
    public void WindowContext_FromActiveWindowInfo_MapsCorrectly()
    {
        var now = DateTime.UtcNow;
        var info = new ActiveWindowInfo(
            WindowHandle: 12345,
            ProcessId: 6789,
            ProcessName: "code.exe",
            WindowTitle: "Visual Studio Code",
            Bounds: new BoundingBox(10, 20, 800, 600),
            Timestamp: now
        );

        var context = WindowContext.FromActiveWindowInfo(info);

        Assert.Equal(12345, context.WindowHandle);
        Assert.Equal(6789, context.ProcessId);
        Assert.Equal("code.exe", context.ProcessName);
        Assert.Equal("Visual Studio Code", context.WindowTitle);
        Assert.Equal(800, context.Bounds.Width);
        Assert.Equal(now, context.Timestamp);
    }

    [Fact]
    public void SemanticAction_CreateMouseClick_SetsCorrectProperties()
    {
        var now = DateTime.UtcNow;
        var context = new WindowContext(1, 2, "notepad", "Notes", new BoundingBox(0, 0, 100, 100), now);

        var action = SemanticAction.CreateMouseClick(
            SemanticActionType.LeftClick,
            150,
            250,
            context,
            now,
            sequenceIndex: 3
        );

        Assert.NotEqual(Guid.Empty, action.Id);
        Assert.Equal(3, action.SequenceIndex);
        Assert.Equal(SemanticActionType.LeftClick, action.ActionType);
        Assert.Equal(150, action.X);
        Assert.Equal(250, action.Y);
        Assert.Equal(now, action.Timestamp);
        Assert.Equal(now, action.StartedAt);
        Assert.Equal(now, action.CompletedAt);
        Assert.True(action.IsMouseAction);
        Assert.False(action.IsKeyboardAction);
        Assert.Equal(ActionType.LeftClick, action.ToStepActionType());
    }

    [Fact]
    public void SemanticAction_CreateTextInput_SetsCorrectProperties()
    {
        var startedAt = DateTime.UtcNow.AddSeconds(-2);
        var completedAt = DateTime.UtcNow;
        var context = WindowContext.Empty;

        var action = SemanticAction.CreateTextInput(
            "Hello, Stepwise!",
            context,
            startedAt,
            completedAt,
            isSensitive: true,
            sequenceIndex: 1
        );

        Assert.Equal(SemanticActionType.TextInput, action.ActionType);
        Assert.Equal("Hello, Stepwise!", action.Text);
        Assert.Equal(16, action.CharacterCount);
        Assert.True(action.IsSensitive);
        Assert.True(action.IsKeyboardAction);
        Assert.False(action.IsMouseAction);
        Assert.Equal(ActionType.TextInput, action.ToStepActionType());
    }

    [Fact]
    public void SemanticAction_CreateKeyPressAndShortcut_SetsCorrectProperties()
    {
        var now = DateTime.UtcNow;
        var context = WindowContext.Empty;

        var keyPress = SemanticAction.CreateKeyPress(
            virtualKey: 13,
            keyName: "Enter",
            modifiers: KeyboardModifiers.None,
            context: context,
            timestamp: now
        );
        Assert.Equal(SemanticActionType.KeyPress, keyPress.ActionType);
        Assert.Equal(13, keyPress.VirtualKey);
        Assert.Equal("Enter", keyPress.KeyName);
        Assert.True(keyPress.IsKeyboardAction);
        Assert.Equal(ActionType.KeyPress, keyPress.ToStepActionType());

        var shortcut = SemanticAction.CreateShortcut(
            virtualKey: 83,
            keyName: "S",
            modifiers: KeyboardModifiers.Control,
            context: context,
            timestamp: now
        );
        Assert.Equal(SemanticActionType.Shortcut, shortcut.ActionType);
        Assert.Equal(KeyboardModifiers.Control, shortcut.Modifiers);
        Assert.True(shortcut.IsKeyboardAction);
        Assert.Equal(ActionType.KeyPress, shortcut.ToStepActionType());
    }

    [Fact]
    public void SemanticAction_SerializationRoundtrip_PreservesData()
    {
        var now = DateTime.UtcNow;
        var context = new WindowContext(99, 100, "app", "App", new BoundingBox(5, 5, 50, 50), now);
        var action = SemanticAction.CreateMouseClick(
            SemanticActionType.DoubleLeftClick,
            42,
            84,
            context,
            now,
            sequenceIndex: 5
        );

        var json = JsonSerializer.Serialize(action);
        var deserialized = JsonSerializer.Deserialize<SemanticAction>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(action.Id, deserialized.Id);
        Assert.Equal(action.ActionType, deserialized.ActionType);
        Assert.Equal(action.X, deserialized.X);
        Assert.Equal(action.Y, deserialized.Y);
        Assert.Equal(action.Context.WindowHandle, deserialized.Context.WindowHandle);
    }

    #endregion

    #region 2. RecordingSessionStateMachine Tests

    [Fact]
    public void StateMachine_InitialState_IsIdle()
    {
        var sm = new RecordingSessionStateMachine();

        Assert.Equal(RecordingSessionState.Idle, sm.CurrentState);
        Assert.True(sm.IsIdle);
        Assert.False(sm.IsRecording);
        Assert.False(sm.IsPaused);
        Assert.False(sm.IsStopping);
        Assert.False(sm.IsCompleted);
        Assert.False(sm.IsFailed);
    }

    [Fact]
    public void StateMachine_FullHappyPath_TransitionsCorrectly()
    {
        var sm = new RecordingSessionStateMachine();
        var transitions = new List<(RecordingSessionState Old, RecordingSessionState New)>();
        sm.StateChanged += (o, n) => transitions.Add((o, n));

        // Idle -> Recording
        Assert.True(sm.CanTransitionTo(RecordingSessionState.Recording));
        sm.Transition(RecordingSessionState.Recording);
        Assert.True(sm.IsRecording);

        // Recording -> Paused
        Assert.True(sm.CanTransitionTo(RecordingSessionState.Paused));
        sm.Transition(RecordingSessionState.Paused);
        Assert.True(sm.IsPaused);

        // Paused -> Recording
        Assert.True(sm.CanTransitionTo(RecordingSessionState.Recording));
        sm.Transition(RecordingSessionState.Recording);
        Assert.True(sm.IsRecording);

        // Recording -> Stopping
        Assert.True(sm.CanTransitionTo(RecordingSessionState.Stopping));
        sm.Transition(RecordingSessionState.Stopping);
        Assert.True(sm.IsStopping);

        // Stopping -> Completed
        Assert.True(sm.CanTransitionTo(RecordingSessionState.Completed));
        sm.Transition(RecordingSessionState.Completed);
        Assert.True(sm.IsCompleted);

        // Reset to Idle
        sm.ResetToIdle();
        Assert.True(sm.IsIdle);

        Assert.Equal(6, transitions.Count);
        Assert.Equal((RecordingSessionState.Idle, RecordingSessionState.Recording), transitions[0]);
        Assert.Equal((RecordingSessionState.Recording, RecordingSessionState.Paused), transitions[1]);
        Assert.Equal((RecordingSessionState.Paused, RecordingSessionState.Recording), transitions[2]);
        Assert.Equal((RecordingSessionState.Recording, RecordingSessionState.Stopping), transitions[3]);
        Assert.Equal((RecordingSessionState.Stopping, RecordingSessionState.Completed), transitions[4]);
        Assert.Equal((RecordingSessionState.Completed, RecordingSessionState.Idle), transitions[5]);
    }

    [Fact]
    public void StateMachine_StoppingFromPausedAndFailure_TransitionsCorrectly()
    {
        var sm = new RecordingSessionStateMachine();

        sm.Transition(RecordingSessionState.Recording);
        sm.Transition(RecordingSessionState.Paused);

        // Paused -> Stopping
        Assert.True(sm.CanTransitionTo(RecordingSessionState.Stopping));
        sm.Transition(RecordingSessionState.Stopping);
        Assert.True(sm.IsStopping);

        // Stopping -> Failed
        Assert.True(sm.CanTransitionTo(RecordingSessionState.Failed));
        sm.Transition(RecordingSessionState.Failed);
        Assert.True(sm.IsFailed);

        // Reset from Failed to Idle
        sm.ResetToIdle();
        Assert.True(sm.IsIdle);
    }

    [Theory]
    [InlineData(RecordingSessionState.Idle, RecordingSessionState.Stopping)]
    [InlineData(RecordingSessionState.Idle, RecordingSessionState.Paused)]
    [InlineData(RecordingSessionState.Idle, RecordingSessionState.Completed)]
    [InlineData(RecordingSessionState.Idle, RecordingSessionState.Failed)]
    [InlineData(RecordingSessionState.Recording, RecordingSessionState.Recording)]
    [InlineData(RecordingSessionState.Recording, RecordingSessionState.Completed)]
    [InlineData(RecordingSessionState.Recording, RecordingSessionState.Failed)]
    [InlineData(RecordingSessionState.Paused, RecordingSessionState.Paused)]
    [InlineData(RecordingSessionState.Paused, RecordingSessionState.Completed)]
    [InlineData(RecordingSessionState.Stopping, RecordingSessionState.Stopping)]
    [InlineData(RecordingSessionState.Stopping, RecordingSessionState.Recording)]
    [InlineData(RecordingSessionState.Completed, RecordingSessionState.Recording)]
    [InlineData(RecordingSessionState.Completed, RecordingSessionState.Stopping)]
    [InlineData(RecordingSessionState.Failed, RecordingSessionState.Recording)]
    [InlineData(RecordingSessionState.Failed, RecordingSessionState.Stopping)]
    public void StateMachine_InvalidTransitions_ReturnFalseAndThrow(RecordingSessionState from, RecordingSessionState to)
    {
        var sm = new RecordingSessionStateMachine();

        // Navigate state machine to 'from' state
        if (from != RecordingSessionState.Idle)
        {
            sm.Transition(RecordingSessionState.Recording);
            if (from == RecordingSessionState.Paused)
            {
                sm.Transition(RecordingSessionState.Paused);
            }
            else if (from == RecordingSessionState.Stopping)
            {
                sm.Transition(RecordingSessionState.Stopping);
            }
            else if (from == RecordingSessionState.Completed)
            {
                sm.Transition(RecordingSessionState.Stopping);
                sm.Transition(RecordingSessionState.Completed);
            }
            else if (from == RecordingSessionState.Failed)
            {
                sm.Transition(RecordingSessionState.Stopping);
                sm.Transition(RecordingSessionState.Failed);
            }
        }

        Assert.False(sm.CanTransitionTo(to));
        Assert.False(sm.TryTransition(to, out var error));
        Assert.NotNull(error);

        var ex = Assert.Throws<InvalidOperationException>(() => sm.Transition(to));
        Assert.Contains("Invalid state transition", ex.Message);
    }

    [Fact]
    public void StateMachine_ResetToIdle_FromIllegalState_ThrowsInvalidOperationException()
    {
        var sm = new RecordingSessionStateMachine();
        sm.Transition(RecordingSessionState.Recording);

        Assert.Throws<InvalidOperationException>(() => sm.ResetToIdle());
    }

    [Fact]
    public void StateMachine_ResetToIdle_WhenAlreadyIdle_IsNoOp()
    {
        var sm = new RecordingSessionStateMachine();
        int eventCount = 0;
        sm.StateChanged += (_, _) => eventCount++;

        sm.ResetToIdle();

        Assert.True(sm.IsIdle);
        Assert.Equal(0, eventCount);
    }

    #endregion

    #region 3. ISystemMetricsProvider Tests

    [Fact]
    public void DefaultSystemMetricsProvider_DefaultValues_AreCorrect()
    {
        var provider = DefaultSystemMetricsProvider.Instance;

        Assert.Equal(500, provider.DoubleClickTimeMs);
        Assert.Equal(4, provider.DoubleClickWidth);
        Assert.Equal(4, provider.DoubleClickHeight);
    }

    [Fact]
    public void DefaultSystemMetricsProvider_CustomValues_AreRespected()
    {
        var provider = new DefaultSystemMetricsProvider(300, 8, 8);

        Assert.Equal(300, provider.DoubleClickTimeMs);
        Assert.Equal(8, provider.DoubleClickWidth);
        Assert.Equal(8, provider.DoubleClickHeight);
    }

    [Theory]
    [InlineData(0, 4, 4)]
    [InlineData(-10, 4, 4)]
    [InlineData(500, 0, 4)]
    [InlineData(500, 4, -1)]
    public void DefaultSystemMetricsProvider_InvalidArguments_ThrowsOutOfRange(int time, int w, int h)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DefaultSystemMetricsProvider(time, w, h));
    }

    #endregion

    #region 4. Stage 2 Interfaces Mock Verification

    [Fact]
    public async Task Stage2Interfaces_CanBeMockedAndCoordinated()
    {
        // 1. IEventCorrelator
        var correlatorMock = new Mock<IEventCorrelator>();
        SemanticAction? receivedAction = null;
        correlatorMock.Object.ActionCorrelated += (s, e) => receivedAction = e;

        var windowContext = WindowContext.Empty;
        var mouseAction = SemanticAction.CreateMouseClick(
            SemanticActionType.LeftClick,
            100,
            100,
            windowContext,
            DateTime.UtcNow
        );
        correlatorMock.Raise(c => c.ActionCorrelated += null, correlatorMock.Object, mouseAction);
        Assert.Equal(mouseAction, receivedAction);

        // 2. ITargetResolver
        var targetResolverMock = new Mock<ITargetResolver>();
        var expectedElement = new ElementInfo(
            Name: "Submit",
            ControlType: "Button",
            AutomationId: "btnSubmit",
            ClassName: "Button",
            ProcessName: "demo",
            ProcessId: 100,
            WindowTitle: "Demo Window",
            WindowHandle: 1,
            BoundingRectangle: new BoundingBox(90, 90, 50, 20)
        );
        targetResolverMock
            .Setup(r => r.ResolveTargetAsync(mouseAction, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedElement);

        // 3. IRecordingPolicy
        var policyMock = new Mock<IRecordingPolicy>();
        policyMock
            .Setup(p => p.Evaluate(mouseAction, expectedElement))
            .Returns(RecordingPolicyDecision.Allow);

        // 4. ICaptureCoordinator
        var captureMock = new Mock<ICaptureCoordinator>();
        captureMock
            .Setup(c => c.CaptureStepAsync(1, expectedElement, It.IsAny<CancellationToken>()))
            .ReturnsAsync("assets/step_1.png");

        // 5. IStepDetector
        var detectorMock = new Mock<IStepDetector>();
        var expectedStep = new Step(
            Id: Guid.NewGuid(),
            SequenceIndex: 1,
            Timestamp: mouseAction.Timestamp,
            Action: ActionType.LeftClick,
            ClickX: 100,
            ClickY: 100,
            TargetElement: expectedElement,
            ScreenshotPath: "assets/step_1.png",
            Title: "Click Submit"
        );
        detectorMock
            .Setup(d => d.DetectStep(mouseAction, expectedElement, RecordingPolicyDecision.Allow, 1))
            .Returns(expectedStep);

        // Act - Simulate coordination pipeline
        var target = await targetResolverMock.Object.ResolveTargetAsync(mouseAction);
        var decision = policyMock.Object.Evaluate(mouseAction, target);
        var screenshot = await captureMock.Object.CaptureStepAsync(1, target);
        var step = detectorMock.Object.DetectStep(mouseAction, target, decision, 1);

        // Assert
        Assert.Equal(expectedElement, target);
        Assert.Equal(RecordingPolicyDecision.Allow, decision);
        Assert.Equal("assets/step_1.png", screenshot);
        Assert.NotNull(step);
        Assert.Equal(expectedStep.Id, step.Id);
    }

    #endregion

    #region 5. RecordingPipelineEngine Stage 2 Lifecycle Integration

    [Fact]
    public async Task RecordingPipelineEngine_Stage2Lifecycle_ManagesStateAndEvents()
    {
        var mockHook = new Mock<IMouseHookService>();
        var mockUia = new Mock<IUIAutomationService>();

        using var engine = new RecordingPipelineEngine(mockHook.Object, mockUia.Object);

        var stateChanges = new List<RecordingSessionState>();
        engine.StateChanged += (s, state) => stateChanges.Add(state);

        Assert.Equal(RecordingSessionState.Idle, engine.State);
        Assert.False(engine.IsRecording);

        // Start
        engine.StartRecording();
        Assert.Equal(RecordingSessionState.Recording, engine.State);
        Assert.True(engine.IsRecording);

        // Pause
        engine.PauseRecording();
        Assert.Equal(RecordingSessionState.Paused, engine.State);
        Assert.False(engine.IsRecording);

        // Resume
        engine.ResumeRecording();
        Assert.Equal(RecordingSessionState.Recording, engine.State);
        Assert.True(engine.IsRecording);

        // Stop Async
        await engine.StopRecordingAsync();
        Assert.Equal(RecordingSessionState.Completed, engine.State);
        Assert.False(engine.IsRecording);

        // Verify state change sequence
        Assert.Contains(RecordingSessionState.Recording, stateChanges);
        Assert.Contains(RecordingSessionState.Paused, stateChanges);
        Assert.Contains(RecordingSessionState.Stopping, stateChanges);
        Assert.Contains(RecordingSessionState.Completed, stateChanges);
    }

    #endregion
}
