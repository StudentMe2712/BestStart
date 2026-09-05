using Moq;
using Stepwise.Core.Engine;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Stepwise.Core.Policy;
using Xunit;

namespace Stepwise.Tests;

public class Stage4RecordingEngineSemanticPipelineTests
{
    #region 1. SemanticAction Model & Factory Tests

    [Fact]
    public void SemanticAction_CreateDragAndDrop_SetsAllPropertiesCorrectly()
    {
        var context = new WindowContext(1234, 5678, "explorer", "File Explorer", new BoundingBox(0, 0, 800, 600), DateTime.UtcNow);
        var timestamp = DateTime.UtcNow;

        var action = SemanticAction.CreateDragAndDrop(
            startX: 100,
            startY: 150,
            endX: 300,
            endY: 450,
            button: RawMouseButton.Left,
            context: context,
            timestamp: timestamp,
            sequenceIndex: 5
        );

        Assert.Equal(SemanticActionType.DragAndDrop, action.ActionType);
        Assert.Equal(100, action.X);
        Assert.Equal(150, action.Y);
        Assert.Equal(300, action.EndX);
        Assert.Equal(450, action.EndY);
        Assert.Equal(context, action.Context);
        Assert.Equal(timestamp, action.Timestamp);
        Assert.Equal(5, action.SequenceIndex);
        Assert.True(action.IsMouseAction);
        Assert.False(action.IsKeyboardAction);
        Assert.Equal(ActionType.DragAndDrop, action.ToStepActionType());
    }

    [Fact]
    public void SemanticAction_CreateScroll_SetsAllPropertiesCorrectly()
    {
        var context = new WindowContext(1234, 5678, "chrome", "Google Chrome", new BoundingBox(0, 0, 1920, 1080), DateTime.UtcNow);
        var timestamp = DateTime.UtcNow;

        var action = SemanticAction.CreateScroll(
            x: 500,
            y: 400,
            delta: -240,
            context: context,
            timestamp: timestamp,
            sequenceIndex: 7
        );

        Assert.Equal(SemanticActionType.Scroll, action.ActionType);
        Assert.Equal(500, action.X);
        Assert.Equal(400, action.Y);
        Assert.Equal(-240, action.Delta);
        Assert.Equal(context, action.Context);
        Assert.Equal(timestamp, action.Timestamp);
        Assert.Equal(7, action.SequenceIndex);
        Assert.True(action.IsMouseAction);
        Assert.False(action.IsKeyboardAction);
        Assert.Equal(ActionType.Scroll, action.ToStepActionType());
    }

    [Fact]
    public void SemanticAction_CreateManualStep_AndWindowActions_MapToExpectedActionTypes()
    {
        var context = new WindowContext(100, 200, "notepad", "Untitled", new BoundingBox(0, 0, 400, 300), DateTime.UtcNow);
        var now = DateTime.UtcNow;

        var manual = SemanticAction.CreateManualStep(context, now, 1);
        Assert.Equal(SemanticActionType.ManualStep, manual.ActionType);
        Assert.Equal(ActionType.ManualStep, manual.ToStepActionType());

        var activated = SemanticAction.CreateWindowActivated(context, now, 2);
        Assert.Equal(SemanticActionType.WindowActivated, activated.ActionType);
        Assert.Equal(ActionType.WindowActivated, activated.ToStepActionType());

        var closed = SemanticAction.CreateWindowClosed(context, now, 3);
        Assert.Equal(SemanticActionType.WindowClosed, closed.ActionType);
        Assert.Equal(ActionType.WindowClosed, closed.ToStepActionType());
    }

    [Fact]
    public void SemanticAction_Shortcut_PreservesKeyPressMappingForBackwardCompatibility()
    {
        var context = WindowContext.Empty;
        var now = DateTime.UtcNow;

        var shortcut = SemanticAction.CreateShortcut(83, "S", KeyboardModifiers.Control, context, now, 1);
        Assert.Equal(SemanticActionType.Shortcut, shortcut.ActionType);
        Assert.Equal(ActionType.KeyPress, shortcut.ToStepActionType());
    }

    #endregion

    #region 2. EventCorrelator Drag & Drop Tests

    [Fact]
    public void EventCorrelator_DragAndDrop_ExceedingTolerance_EmitsDragAndDropAction()
    {
        var metricsMock = new Mock<ISystemMetricsProvider>();
        metricsMock.SetupGet(m => m.DoubleClickWidth).Returns(4);  // tolerance = 8
        metricsMock.SetupGet(m => m.DoubleClickHeight).Returns(4); // tolerance = 8

        using var correlator = new EventCorrelator(metricsMock.Object);
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var context = new WindowContext(1, 10, "explorer", "Files", new BoundingBox(0, 0, 800, 600), DateTime.UtcNow);
        var t0 = DateTime.UtcNow;

        // Mouse down at (100, 100)
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 100, 100, 0, t0), context);

        // Mouse up at (150, 200) -> dx=50, dy=100 > 8px
        var t1 = t0.AddMilliseconds(250);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 150, 200, 0, t1), context);

        Assert.Single(emitted);
        var action = emitted[0];
        Assert.Equal(SemanticActionType.DragAndDrop, action.ActionType);
        Assert.Equal(100, action.X);
        Assert.Equal(100, action.Y);
        Assert.Equal(150, action.EndX);
        Assert.Equal(200, action.EndY);
        Assert.Equal(1, action.SequenceIndex);
        Assert.Equal("explorer", action.Context.ProcessName);
    }

    [Fact]
    public void EventCorrelator_DragAndDrop_FlushesPendingTextBeforeEmitting()
    {
        var metricsMock = new Mock<ISystemMetricsProvider>();
        metricsMock.SetupGet(m => m.DoubleClickWidth).Returns(4);
        metricsMock.SetupGet(m => m.DoubleClickHeight).Returns(4);

        using var correlator = new EventCorrelator(metricsMock.Object);
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var context = new WindowContext(1, 10, "app", "App", new BoundingBox(0, 0, 500, 500), DateTime.UtcNow);
        var t0 = DateTime.UtcNow;

        // Type "Hi"
        correlator.ProcessKeyboardEvent(new RawKeyboardEvent(RawKeyboardEventType.KeyDown, 72, 0, KeyboardModifiers.None, "H", false, false, t0), context);
        correlator.ProcessKeyboardEvent(new RawKeyboardEvent(RawKeyboardEventType.KeyDown, 73, 0, KeyboardModifiers.None, "i", false, false, t0.AddMilliseconds(10)), context);

        // Drag from (50, 50) to (120, 120)
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 50, 50, 0, t0.AddMilliseconds(50)), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 120, 120, 0, t0.AddMilliseconds(200)), context);

        Assert.Equal(2, emitted.Count);
        Assert.Equal(SemanticActionType.TextInput, emitted[0].ActionType);
        Assert.Equal("Hi", emitted[0].Text);
        Assert.Equal(1, emitted[0].SequenceIndex);

        Assert.Equal(SemanticActionType.DragAndDrop, emitted[1].ActionType);
        Assert.Equal(50, emitted[1].X);
        Assert.Equal(50, emitted[1].Y);
        Assert.Equal(120, emitted[1].EndX);
        Assert.Equal(120, emitted[1].EndY);
        Assert.Equal(2, emitted[1].SequenceIndex);
    }

    #endregion

    #region 3. EventCorrelator Scroll Aggregation Tests

    [Fact]
    public void EventCorrelator_Scroll_SeriesOfWheelEvents_AggregatesIntoSingleScrollAction()
    {
        // Use shorter scroll timeout for fast unit testing
        using var correlator = new EventCorrelator(null, flushTimeoutMs: 600, scrollTimeoutMs: 50);
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var context = new WindowContext(10, 20, "browser", "Web", new BoundingBox(0, 0, 1000, 800), DateTime.UtcNow);
        var t0 = DateTime.UtcNow;

        // 5 wheel down ticks: delta = -120 each
        for (int i = 0; i < 5; i++)
        {
            correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.Wheel, RawMouseButton.None, 400, 300, -120, t0.AddMilliseconds(i * 5)), context);
        }

        // Wait for scroll timer to elapse (> 50ms)
        Thread.Sleep(120);

        Assert.Single(emitted);
        var action = emitted[0];
        Assert.Equal(SemanticActionType.Scroll, action.ActionType);
        Assert.Equal(400, action.X);
        Assert.Equal(300, action.Y);
        Assert.Equal(-600, action.Delta);
        Assert.Equal("browser", action.Context.ProcessName);
    }

    [Fact]
    public void EventCorrelator_Scroll_DirectionReversal_FlushesPreviousAndAggregatesNew()
    {
        using var correlator = new EventCorrelator(null, flushTimeoutMs: 600, scrollTimeoutMs: 200);
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var context = new WindowContext(10, 20, "browser", "Web", new BoundingBox(0, 0, 1000, 800), DateTime.UtcNow);
        var t0 = DateTime.UtcNow;

        // Scroll down twice: -120 + -120 = -240
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.Wheel, RawMouseButton.None, 400, 300, -120, t0), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.Wheel, RawMouseButton.None, 400, 300, -120, t0.AddMilliseconds(10)), context);

        // Reverse direction: scroll up +120
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.Wheel, RawMouseButton.None, 400, 300, 120, t0.AddMilliseconds(30)), context);

        // Immediately upon reversal, the negative scroll should be flushed!
        Assert.Single(emitted);
        Assert.Equal(SemanticActionType.Scroll, emitted[0].ActionType);
        Assert.Equal(-240, emitted[0].Delta);

        // Manually flush remaining
        correlator.FlushPending();

        Assert.Equal(2, emitted.Count);
        Assert.Equal(SemanticActionType.Scroll, emitted[1].ActionType);
        Assert.Equal(120, emitted[1].Delta);
    }

    [Fact]
    public void EventCorrelator_Scroll_FlushedOnMouseDown()
    {
        using var correlator = new EventCorrelator(null, flushTimeoutMs: 600, scrollTimeoutMs: 500);
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var context = new WindowContext(10, 20, "editor", "Doc", new BoundingBox(0, 0, 800, 600), DateTime.UtcNow);
        var t0 = DateTime.UtcNow;

        // Wheel event
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.Wheel, RawMouseButton.None, 200, 200, 120, t0), context);

        // Mouse click before scroll timer expires
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 200, 200, 0, t0.AddMilliseconds(20)), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 200, 200, 0, t0.AddMilliseconds(40)), context);

        Assert.Equal(2, emitted.Count);
        Assert.Equal(SemanticActionType.Scroll, emitted[0].ActionType);
        Assert.Equal(120, emitted[0].Delta);
        Assert.Equal(1, emitted[0].SequenceIndex);

        Assert.Equal(SemanticActionType.LeftClick, emitted[1].ActionType);
        Assert.Equal(2, emitted[1].SequenceIndex);
    }

    [Fact]
    public void EventCorrelator_Scroll_FlushedOnKeyboardEvent()
    {
        using var correlator = new EventCorrelator(null, flushTimeoutMs: 600, scrollTimeoutMs: 500);
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var context = new WindowContext(10, 20, "editor", "Doc", new BoundingBox(0, 0, 800, 600), DateTime.UtcNow);
        var t0 = DateTime.UtcNow;

        // Wheel event
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.Wheel, RawMouseButton.None, 200, 200, -120, t0), context);

        // Enter key before scroll timer expires
        correlator.ProcessKeyboardEvent(new RawKeyboardEvent(RawKeyboardEventType.KeyDown, 13, 0, KeyboardModifiers.None, null, false, false, t0.AddMilliseconds(20)), context);

        Assert.Equal(2, emitted.Count);
        Assert.Equal(SemanticActionType.Scroll, emitted[0].ActionType);
        Assert.Equal(-120, emitted[0].Delta);

        Assert.Equal(SemanticActionType.KeyPress, emitted[1].ActionType);
        Assert.Equal("Enter", emitted[1].KeyName);
    }

    [Fact]
    public void EventCorrelator_Scroll_ResetCancelsPendingScrollWithoutEmitting()
    {
        using var correlator = new EventCorrelator(null, flushTimeoutMs: 600, scrollTimeoutMs: 500);
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.Wheel, RawMouseButton.None, 100, 100, 120, DateTime.UtcNow));
        correlator.Reset();

        // Even after wait, nothing should be emitted
        Thread.Sleep(50);
        Assert.Empty(emitted);
    }

    #endregion

    #region 4. Window Context Change Tests

    [Fact]
    public void EventCorrelator_WindowContextChange_FlushesPendingBuffersWithOldContext()
    {
        using var correlator = new EventCorrelator(null, flushTimeoutMs: 2000, scrollTimeoutMs: 2000);
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var win1 = new WindowContext(1001, 100, "notepad", "Notepad", new BoundingBox(0, 0, 500, 500), DateTime.UtcNow);
        var win2 = new WindowContext(2002, 200, "calc", "Calculator", new BoundingBox(600, 0, 400, 400), DateTime.UtcNow);
        var now = DateTime.UtcNow;

        // Type in Window 1
        correlator.ProcessKeyboardEvent(new RawKeyboardEvent(RawKeyboardEventType.KeyDown, 65, 0, KeyboardModifiers.None, "A", false, false, now), win1);

        // Click in Window 2 (different WindowHandle)
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 650, 50, 0, now.AddMilliseconds(50)), win2);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 650, 50, 0, now.AddMilliseconds(70)), win2);

        Assert.Equal(2, emitted.Count);
        // Text action belongs to Window 1
        Assert.Equal(SemanticActionType.TextInput, emitted[0].ActionType);
        Assert.Equal("A", emitted[0].Text);
        Assert.Equal(1001, emitted[0].Context.WindowHandle);
        Assert.Equal("notepad", emitted[0].Context.ProcessName);

        // Click belongs to Window 2
        Assert.Equal(SemanticActionType.LeftClick, emitted[1].ActionType);
        Assert.Equal(2002, emitted[1].Context.WindowHandle);
        Assert.Equal("calc", emitted[1].Context.ProcessName);
    }

    #endregion

    #region 5. StepDetector Tests for DragAndDrop, Scroll, Window, and Manual Actions

    [Fact]
    public void StepDetector_DragAndDrop_GeneratesExpectedMetadataAndText()
    {
        var detector = new StepDetector();
        var context = new WindowContext(1, 10, "explorer", "Files", new BoundingBox(0, 0, 800, 600), DateTime.UtcNow);
        var action = SemanticAction.CreateDragAndDrop(100, 150, 350, 400, RawMouseButton.Left, context, DateTime.UtcNow, 1);
        var target = new ElementInfo("FileA.txt", "ListItem", "itemA", "ListViewItem", "explorer", 10, "Files", 1, new BoundingBox(80, 130, 80, 40));

        var step = detector.DetectStep(action, target, RecordingPolicyDecision.Allow, 1);

        Assert.NotNull(step);
        Assert.Equal(ActionType.DragAndDrop, step.Action);
        Assert.Equal("Drag and drop in \"FileA.txt\"", step.Title);
        Assert.Equal("Drag from (100, 150) to (350, 400) in explorer.", step.Description);
        Assert.NotNull(step.Metadata);
        Assert.Equal("100", step.Metadata["DragStartX"]);
        Assert.Equal("150", step.Metadata["DragStartY"]);
        Assert.Equal("350", step.Metadata["DragEndX"]);
        Assert.Equal("400", step.Metadata["DragEndY"]);
    }

    [Fact]
    public void StepDetector_DragAndDrop_WithoutTargetName_FallbacksToControlType()
    {
        var detector = new StepDetector();
        var context = WindowContext.Empty;
        var action = SemanticAction.CreateDragAndDrop(10, 20, 30, 40, RawMouseButton.Left, context, DateTime.UtcNow, 1);
        var target = new ElementInfo("", "Pane", "", "Canvas", "paint", 5, "Paint", 1, new BoundingBox(0, 0, 500, 500));

        var step = detector.DetectStep(action, target, RecordingPolicyDecision.Allow, 1);

        Assert.NotNull(step);
        Assert.Equal("Drag and drop Pane", step.Title);
        Assert.Equal("Drag from (10, 20) to (30, 40) in paint.", step.Description);
    }

    [Fact]
    public void StepDetector_Scroll_GeneratesExpectedTitleDescriptionAndMetadata()
    {
        var detector = new StepDetector();
        var context = new WindowContext(1, 10, "browser", "Web", new BoundingBox(0, 0, 1200, 800), DateTime.UtcNow);

        // Scroll Down
        var scrollDown = SemanticAction.CreateScroll(300, 400, -360, context, DateTime.UtcNow, 1);
        var targetWithTitle = new ElementInfo("Article", "Document", "doc1", "Chrome_RenderWidgetHostHWND", "chrome", 10, "Web", 1, new BoundingBox(0, 0, 1200, 800));

        var stepDown = detector.DetectStep(scrollDown, targetWithTitle, RecordingPolicyDecision.Allow, 1);
        Assert.NotNull(stepDown);
        Assert.NotNull(stepDown.Metadata);
        Assert.Equal(ActionType.Scroll, stepDown.Action);
        Assert.Equal("Scroll down in \"Article\"", stepDown.Title);
        Assert.Equal("Scroll down by 360 in chrome.", stepDown.Description);
        Assert.Equal("-360", stepDown.Metadata["ScrollDelta"]);

        // Scroll Up
        var scrollUp = SemanticAction.CreateScroll(300, 400, 120, context, DateTime.UtcNow, 2);
        var targetWithoutName = new ElementInfo("", "Document", "doc1", "Chrome_RenderWidgetHostHWND", "chrome", 10, "Web", 1, new BoundingBox(0, 0, 1200, 800));

        var stepUp = detector.DetectStep(scrollUp, targetWithoutName, RecordingPolicyDecision.Allow, 2);
        Assert.NotNull(stepUp);
        Assert.NotNull(stepUp.Metadata);
        Assert.Equal("Scroll up Document", stepUp.Title);
        Assert.Equal("Scroll up by 120 in chrome.", stepUp.Description);
        Assert.Equal("120", stepUp.Metadata["ScrollDelta"]);
    }

    [Fact]
    public void StepDetector_WindowActivated_WindowClosed_ManualStep_GenerateExpectedSteps()
    {
        var detector = new StepDetector();
        var target = new ElementInfo("MainForm", "Window", "form1", "Win32Window", "myApp", 50, "My Application", 999, new BoundingBox(0, 0, 800, 600));

        var activated = SemanticAction.CreateWindowActivated(WindowContext.Empty, DateTime.UtcNow, 1);
        var stepAct = detector.DetectStep(activated, target, RecordingPolicyDecision.Allow, 1);
        Assert.NotNull(stepAct);
        Assert.Equal(ActionType.WindowActivated, stepAct.Action);
        Assert.Equal("Activate \"MainForm\"", stepAct.Title);

        var closed = SemanticAction.CreateWindowClosed(WindowContext.Empty, DateTime.UtcNow, 2);
        var stepClosed = detector.DetectStep(closed, target, RecordingPolicyDecision.Allow, 2);
        Assert.NotNull(stepClosed);
        Assert.Equal(ActionType.WindowClosed, stepClosed.Action);
        Assert.Equal("Close \"MainForm\"", stepClosed.Title);

        var manual = SemanticAction.CreateManualStep(WindowContext.Empty, DateTime.UtcNow, 3);
        var stepManual = detector.DetectStep(manual, target, RecordingPolicyDecision.Allow, 3);
        Assert.NotNull(stepManual);
        Assert.Equal(ActionType.ManualStep, stepManual.Action);
        Assert.Equal("Manual step: MainForm", stepManual.Title);
    }

    #endregion

    #region 6. RecordingEngine Window Change & Lifecycle Tests

    [Fact]
    public void RecordingEngine_OnActiveWindowChanged_FlushesCorrelator()
    {
        var inputMonitorMock = new Mock<IInputMonitoringService>();
        var windowTrackerMock = new Mock<IActiveWindowTracker>();
        var correlatorMock = new Mock<IEventCorrelator>();
        var targetResolverMock = new Mock<ITargetResolver>();
        var policyMock = new Mock<IRecordingPolicy>();
        var stepDetectorMock = new Mock<IStepDetector>();
        var captureCoordinatorMock = new Mock<ICaptureCoordinator>();

        using var engine = new RecordingEngine(
            inputMonitorMock.Object,
            windowTrackerMock.Object,
            correlatorMock.Object,
            targetResolverMock.Object,
            policyMock.Object,
            stepDetectorMock.Object,
            captureCoordinatorMock.Object
        );

        engine.StartRecording();

        // Raise window changed
        var newWindow = new ActiveWindowInfo(999, 888, "notepad", "Notepad", new BoundingBox(0, 0, 500, 500), DateTime.UtcNow);
        windowTrackerMock.Raise(w => w.ActiveWindowChanged += null, windowTrackerMock.Object, newWindow);

        // Verify correlator.FlushPending() was called
        correlatorMock.Verify(c => c.FlushPending(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RecordingEngine_MultipleStartStopCycles_CompletesCleanly()
    {
        var inputMonitorMock = new Mock<IInputMonitoringService>();
        var correlator = new EventCorrelator();
        var targetResolverMock = new Mock<ITargetResolver>();
        var policy = new DefaultRecordingPolicy();
        var stepDetector = new StepDetector();
        var captureCoordinatorMock = new Mock<ICaptureCoordinator>();

        using var engine = new RecordingEngine(
            inputMonitorMock.Object,
            null,
            correlator,
            targetResolverMock.Object,
            policy,
            stepDetector,
            captureCoordinatorMock.Object
        );

        for (int cycle = 0; cycle < 3; cycle++)
        {
            engine.StartRecording();
            Assert.True(engine.IsRecording);
            Assert.Equal(RecordingSessionState.Recording, engine.State);

            await engine.StopRecordingAsync();
            Assert.False(engine.IsRecording);
            Assert.Equal(RecordingSessionState.Completed, engine.State);
        }
    }

    #endregion
}
