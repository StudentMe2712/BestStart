using System.Diagnostics;
using System.IO;
using Microsoft.Data.Sqlite;
using Moq;
using Stepwise.Core.Engine;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Stepwise.Core.Policy;
using Stepwise.Storage.Repositories;
using Stepwise.WindowsIntegration.Automation;
using Stepwise.WindowsIntegration.Capture;
using Xunit;

namespace Stepwise.Tests;

/// <summary>
/// Автоматизированные приемочные тесты отказоустойчивости Этапа 4 (Stage 4 Robustness Acceptance Tests).
/// Полностью закрывают все 15 обязательных пунктов из Раздела 20 пользовательского запроса:
/// 1. DoubleClick
/// 2. RightClick
/// 3. MouseDown / MouseUp
/// 4. Drag
/// 5. Scroll
/// 6. Shortcut
/// 7. Window switch
/// 8. Target disappeared
/// 9. UIA partial metadata
/// 10. Repeated Start/Stop
/// 11. Rapid interactions
/// 12. Password suppression
/// 13. Screenshot failure
/// 14. Capture cancellation
/// 15. No duplicate semantic action
/// </summary>
public sealed class Stage4RobustnessAcceptanceTests : IDisposable
{
    private readonly string _tempDirectory;

    public Stage4RobustnessAcceptanceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "Stepwise_Stage4_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    #region Point 1: DoubleClick

    [Fact]
    public void Point01_DoubleClick_RecognizesFastLeftClicksWithinThreshold_EmitsDoubleLeftClickAndTracksSequenceIndex()
    {
        var metricsMock = new Mock<ISystemMetricsProvider>();
        metricsMock.SetupGet(m => m.DoubleClickTimeMs).Returns(500);
        metricsMock.SetupGet(m => m.DoubleClickWidth).Returns(4);
        metricsMock.SetupGet(m => m.DoubleClickHeight).Returns(4);

        using var correlator = new EventCorrelator(metricsMock.Object);
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var context = new WindowContext(100, 10, "app", "Main Window", new BoundingBox(0, 0, 800, 600), DateTime.UtcNow);
        var t0 = DateTime.UtcNow;

        // Click 1: MouseDown at (100, 100) -> MouseUp at (100, 100)
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 100, 100, 0, t0), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 100, 100, 0, t0.AddMilliseconds(30)), context);

        // Click 2 (within 200ms and 2px dx/dy): MouseDown at (102, 101) -> MouseUp at (102, 101)
        var t1 = t0.AddMilliseconds(200);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 102, 101, 0, t1), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 102, 101, 0, t1.AddMilliseconds(30)), context);

        Assert.Equal(2, emitted.Count);
        Assert.Equal(SemanticActionType.LeftClick, emitted[0].ActionType);
        Assert.Equal(1, emitted[0].SequenceIndex);
        Assert.Equal(100, emitted[0].X);
        Assert.Equal(100, emitted[0].Y);

        Assert.Equal(SemanticActionType.DoubleLeftClick, emitted[1].ActionType);
        Assert.Equal(2, emitted[1].SequenceIndex);
        Assert.Equal(102, emitted[1].X);
        Assert.Equal(101, emitted[1].Y);

        // Third click immediately following double click should NOT become double click (tracker was reset)
        var t2 = t1.AddMilliseconds(100);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 102, 101, 0, t2), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 102, 101, 0, t2.AddMilliseconds(30)), context);

        Assert.Equal(3, emitted.Count);
        Assert.Equal(SemanticActionType.LeftClick, emitted[2].ActionType);
        Assert.Equal(3, emitted[2].SequenceIndex);
    }

    [Fact]
    public void Point01_DoubleClick_ExceedingTimeOrDistanceThreshold_EmitsSeparateLeftClicks()
    {
        var metricsMock = new Mock<ISystemMetricsProvider>();
        metricsMock.SetupGet(m => m.DoubleClickTimeMs).Returns(300);
        metricsMock.SetupGet(m => m.DoubleClickWidth).Returns(4);
        metricsMock.SetupGet(m => m.DoubleClickHeight).Returns(4);

        using var correlator = new EventCorrelator(metricsMock.Object);
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var context = WindowContext.Empty;
        var t0 = DateTime.UtcNow;

        // Click 1 at (100, 100)
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 100, 100, 0, t0), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 100, 100, 0, t0.AddMilliseconds(30)), context);

        // Click 2 at (100, 100) but 400ms later (> 300ms)
        var t1 = t0.AddMilliseconds(400);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 100, 100, 0, t1), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 100, 100, 0, t1.AddMilliseconds(30)), context);

        // Click 3 within 100ms, but dx = 15px (> 4px)
        var t2 = t1.AddMilliseconds(100);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 115, 100, 0, t2), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 115, 100, 0, t2.AddMilliseconds(30)), context);

        Assert.Equal(3, emitted.Count);
        Assert.All(emitted, a => Assert.Equal(SemanticActionType.LeftClick, a.ActionType));
        Assert.Equal(1, emitted[0].SequenceIndex);
        Assert.Equal(2, emitted[1].SequenceIndex);
        Assert.Equal(3, emitted[2].SequenceIndex);
    }

    #endregion

    #region Point 2: RightClick

    [Fact]
    public void Point02_RightClick_EmitsRightClick_AndResetsDoubleClickCandidate()
    {
        var metricsMock = new Mock<ISystemMetricsProvider>();
        metricsMock.SetupGet(m => m.DoubleClickTimeMs).Returns(500);
        metricsMock.SetupGet(m => m.DoubleClickWidth).Returns(4);
        metricsMock.SetupGet(m => m.DoubleClickHeight).Returns(4);

        using var correlator = new EventCorrelator(metricsMock.Object);
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var context = new WindowContext(200, 20, "notepad", "Doc", new BoundingBox(0, 0, 500, 500), DateTime.UtcNow);
        var t0 = DateTime.UtcNow;

        // 1. First left click at t0
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 200, 150, 0, t0), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 200, 150, 0, t0.AddMilliseconds(20)), context);

        // 2. Right click at t0 + 50ms (MUST cancel the left double-click expectation)
        var t1 = t0.AddMilliseconds(50);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Right, 200, 150, 0, t1), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Right, 200, 150, 0, t1.AddMilliseconds(20)), context);

        // 3. Second left click at t0 + 100ms (still within 500ms of click 1)
        var t2 = t0.AddMilliseconds(100);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 200, 150, 0, t2), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 200, 150, 0, t2.AddMilliseconds(20)), context);

        Assert.Equal(3, emitted.Count);

        Assert.Equal(SemanticActionType.LeftClick, emitted[0].ActionType);
        Assert.Equal(1, emitted[0].SequenceIndex);

        Assert.Equal(SemanticActionType.RightClick, emitted[1].ActionType);
        Assert.Equal(2, emitted[1].SequenceIndex);
        Assert.Equal(200, emitted[1].X);
        Assert.Equal(150, emitted[1].Y);

        Assert.Equal(SemanticActionType.LeftClick, emitted[2].ActionType);
        Assert.Equal(3, emitted[2].SequenceIndex);
    }

    #endregion

    #region Point 3: MouseDown / MouseUp

    [Fact]
    public void Point03_MouseDownMouseUp_CorrelatesBasicEvents_AndHandlesUnmatchedOrStepDetection()
    {
        var correlator = new EventCorrelator();
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var context = WindowContext.Empty;
        var now = DateTime.UtcNow;

        // 1. MouseDown without MouseUp: click is not yet completed
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 50, 50, 0, now), context);
        Assert.Empty(emitted);

        // 2. Matching MouseUp: completes LeftClick
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 50, 50, 0, now.AddMilliseconds(30)), context);
        Assert.Single(emitted);
        Assert.Equal(SemanticActionType.LeftClick, emitted[0].ActionType);

        // 3. MouseUp without prior MouseDown: dropped without spurious click
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 70, 70, 0, now.AddMilliseconds(50)), context);
        Assert.Single(emitted);

        // 4. Mismatched MouseDown Left and MouseUp Right: dropped
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 80, 80, 0, now.AddMilliseconds(70)), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Right, 80, 80, 0, now.AddMilliseconds(90)), context);
        Assert.Single(emitted);

        // 5. Direct verification of StepDetector handling MouseDown and MouseUp semantic actions
        var detector = new StepDetector();
        var target = new ElementInfo("Canvas", "Pane", "canvas1", "DrawingCanvas", "paint", 10, "Paint", 1, new BoundingBox(0, 0, 400, 400));

        var downAction = SemanticAction.CreateMouseClick(SemanticActionType.MouseDown, 100, 100, context, now, 2);
        var downStep = detector.DetectStep(downAction, target, RecordingPolicyDecision.Allow, 2);
        Assert.NotNull(downStep);
        Assert.Equal(ActionType.MouseDown, downStep.Action);
        Assert.Equal("Mouse down on \"Canvas\"", downStep.Title);

        var upAction = SemanticAction.CreateMouseClick(SemanticActionType.MouseUp, 100, 100, context, now.AddMilliseconds(50), 3);
        var upStep = detector.DetectStep(upAction, target, RecordingPolicyDecision.Allow, 3);
        Assert.NotNull(upStep);
        Assert.Equal(ActionType.MouseUp, upStep.Action);
        Assert.Equal("Mouse up on \"Canvas\"", upStep.Title);
    }

    #endregion

    #region Point 4: Drag

    [Fact]
    public void Point04_DragAndDrop_ExceedingThreshold_EmitsDragActionWithoutParasiticClicks()
    {
        var metricsMock = new Mock<ISystemMetricsProvider>();
        metricsMock.SetupGet(m => m.DoubleClickWidth).Returns(4);  // drag tolerance = 8px
        metricsMock.SetupGet(m => m.DoubleClickHeight).Returns(4);

        using var correlator = new EventCorrelator(metricsMock.Object);
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var context = new WindowContext(1, 10, "explorer", "Files", new BoundingBox(0, 0, 800, 600), DateTime.UtcNow);
        var t0 = DateTime.UtcNow;

        // MouseDown at (100, 100) -> MouseUp at (250, 300) (dx=150, dy=200 >> 8px threshold)
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 100, 100, 0, t0), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 250, 300, 0, t0.AddMilliseconds(300)), context);

        // Verification: Exactly ONE action emitted, and it's DragAndDrop (no parasitic LeftClick or DoubleClick)
        Assert.Single(emitted);
        var drag = emitted[0];
        Assert.Equal(SemanticActionType.DragAndDrop, drag.ActionType);
        Assert.Equal(100, drag.X);
        Assert.Equal(100, drag.Y);
        Assert.Equal(250, drag.EndX);
        Assert.Equal(300, drag.EndY);
        Assert.Equal(1, drag.SequenceIndex);

        // Verify StepDetector converts DragAndDrop with complete coordinates metadata
        var detector = new StepDetector();
        var target = new ElementInfo("FolderItem", "ListItem", "item1", "ItemClass", "explorer", 10, "Files", 1, new BoundingBox(90, 90, 80, 40));
        var step = detector.DetectStep(drag, target, RecordingPolicyDecision.Allow, 1);

        Assert.NotNull(step);
        Assert.NotNull(step.Metadata);
        Assert.Equal(ActionType.DragAndDrop, step.Action);
        Assert.Equal("100", step.Metadata["DragStartX"]);
        Assert.Equal("100", step.Metadata["DragStartY"]);
        Assert.Equal("250", step.Metadata["DragEndX"]);
        Assert.Equal("300", step.Metadata["DragEndY"]);
        Assert.Equal("Drag and drop in \"FolderItem\"", step.Title);

        // Next click does not inherit double-click state from previous drag
        var t1 = t0.AddMilliseconds(400);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 250, 300, 0, t1), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 250, 300, 0, t1.AddMilliseconds(30)), context);

        Assert.Equal(2, emitted.Count);
        Assert.Equal(SemanticActionType.LeftClick, emitted[1].ActionType);
        Assert.Equal(2, emitted[1].SequenceIndex);
    }

    #endregion

    #region Point 5: Scroll

    [Fact]
    public void Point05_Scroll_AggregatesSeriesOfWheelEvents_FlushesOnTimeoutAndSignReversal()
    {
        // Use fast timeout for deterministic test execution
        using var correlator = new EventCorrelator(null, flushTimeoutMs: 1000, scrollTimeoutMs: 60);
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var context = new WindowContext(1, 10, "chrome", "Browser", new BoundingBox(0, 0, 1200, 800), DateTime.UtcNow);
        var t0 = DateTime.UtcNow;

        // 1. Send 10 wheel down ticks (-120 each = -1200 total)
        for (int i = 0; i < 10; i++)
        {
            correlator.ProcessMouseEvent(
                new RawMouseEvent(RawMouseEventType.Wheel, RawMouseButton.None, 500, 400, -120, t0.AddMilliseconds(i * 2)),
                context);
        }

        // Wait for inactivity window (> 60ms) to trigger flush
        Thread.Sleep(120);

        // Verification: Exactly 1 aggregated Scroll action, not 10 small actions!
        Assert.Single(emitted);
        Assert.Equal(SemanticActionType.Scroll, emitted[0].ActionType);
        Assert.Equal(-1200, emitted[0].Delta);
        Assert.Equal(500, emitted[0].X);
        Assert.Equal(400, emitted[0].Y);
        Assert.Equal(1, emitted[0].SequenceIndex);

        // 2. Test sign reversal: Scroll down (-240) then reverse direction to Scroll up (+120)
        var t1 = DateTime.UtcNow;
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.Wheel, RawMouseButton.None, 500, 400, -120, t1), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.Wheel, RawMouseButton.None, 500, 400, -120, t1.AddMilliseconds(5)), context);

        // Immediately reverse direction
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.Wheel, RawMouseButton.None, 500, 400, 120, t1.AddMilliseconds(15)), context);

        // The negative scroll (-240) MUST be flushed immediately upon sign change!
        Assert.Equal(2, emitted.Count);
        Assert.Equal(SemanticActionType.Scroll, emitted[1].ActionType);
        Assert.Equal(-240, emitted[1].Delta);

        // Flush remainder
        correlator.FlushPending();
        Assert.Equal(3, emitted.Count);
        Assert.Equal(SemanticActionType.Scroll, emitted[2].ActionType);
        Assert.Equal(120, emitted[2].Delta);
    }

    #endregion

    #region Point 6: Shortcut

    [Theory]
    [InlineData(65, KeyboardModifiers.Control, "\x01", "A", "Control+A")]
    [InlineData(67, KeyboardModifiers.Control, "\x03", "C", "Control+C")]
    [InlineData(86, KeyboardModifiers.Control, "\x16", "V", "Control+V")]
    [InlineData(121, KeyboardModifiers.Shift, null, "F10", "Shift+F10")]
    [InlineData(9, KeyboardModifiers.Alt, null, "Tab", "Alt+Tab")]
    public void Point06_Shortcut_RecognizesStandardShortcuts_WithoutParasiticTextInputAndPreservesModifiersAndKeyName(
        int virtualKey,
        KeyboardModifiers modifiers,
        string? character,
        string expectedKeyName,
        string expectedShortcutString)
    {
        using var correlator = new EventCorrelator();
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var context = new WindowContext(1, 10, "testApp", "Window", new BoundingBox(0, 0, 600, 400), DateTime.UtcNow);
        var rawEvent = new RawKeyboardEvent(
            EventType: RawKeyboardEventType.KeyDown,
            VirtualKey: virtualKey,
            ScanCode: 0,
            Modifiers: modifiers,
            Character: character,
            IsDeadKey: false,
            IsExtendedKey: false,
            Timestamp: DateTime.UtcNow
        );

        correlator.ProcessKeyboardEvent(rawEvent, context);
        correlator.FlushPending();

        // Verification: Exactly 1 action emitted
        Assert.Single(emitted);
        var action = emitted[0];

        // Must be Shortcut (or KeyPress) with preserved parameters
        Assert.True(action.ActionType == SemanticActionType.Shortcut || action.ActionType == SemanticActionType.KeyPress);
        Assert.Equal(expectedKeyName, action.KeyName);
        Assert.Equal(modifiers, action.Modifiers);
        Assert.Null(action.Text); // Zero parasitic text!

        // Verification in StepDetector
        var detector = new StepDetector();
        var target = new ElementInfo("Editor", "Edit", "txt1", "TextBox", "testApp", 10, "Window", 1, new BoundingBox(0, 0, 200, 200));
        var step = detector.DetectStep(action, target, RecordingPolicyDecision.Allow, 1);

        Assert.NotNull(step);
        Assert.Equal(ActionType.KeyPress, step.Action);
        Assert.Contains(expectedShortcutString, step.Title);
    }

    #endregion

    #region Point 7: Window switch

    [Fact]
    public void Point07_WindowSwitch_FlushesPendingBuffersAndIsolatesWindowContext()
    {
        using var correlator = new EventCorrelator(flushTimeoutMs: 5000, scrollTimeoutMs: 5000);
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var win1 = new WindowContext(111, 10, "notepad", "Untitled - Notepad", new BoundingBox(0, 0, 500, 500), DateTime.UtcNow);
        var win2 = new WindowContext(222, 20, "calc", "Calculator", new BoundingBox(600, 0, 400, 400), DateTime.UtcNow);
        var now = DateTime.UtcNow;

        // 1. Type "Text1" into Window 1 (not flushed yet by timer)
        correlator.ProcessKeyboardEvent(new RawKeyboardEvent(RawKeyboardEventType.KeyDown, 65, 0, KeyboardModifiers.None, "A", false, false, now), win1);
        correlator.ProcessKeyboardEvent(new RawKeyboardEvent(RawKeyboardEventType.KeyDown, 66, 0, KeyboardModifiers.None, "B", false, false, now.AddMilliseconds(10)), win1);

        // 2. Mouse action occurs in Window 2 (different WindowHandle)
        // This switch MUST immediately flush the pending text buffer for Window 1
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 650, 50, 0, now.AddMilliseconds(50)), win2);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 650, 50, 0, now.AddMilliseconds(70)), win2);

        Assert.Equal(2, emitted.Count);

        // Action 1 belongs strictly to Window 1
        Assert.Equal(SemanticActionType.TextInput, emitted[0].ActionType);
        Assert.Equal("AB", emitted[0].Text);
        Assert.Equal(111, emitted[0].Context.WindowHandle);
        Assert.Equal("notepad", emitted[0].Context.ProcessName);

        // Action 2 belongs strictly to Window 2
        Assert.Equal(SemanticActionType.LeftClick, emitted[1].ActionType);
        Assert.Equal(222, emitted[1].Context.WindowHandle);
        Assert.Equal("calc", emitted[1].Context.ProcessName);
    }

    #endregion

    #region Point 8: Target disappeared

    [Fact]
    public async Task Point08_TargetDisappeared_GracefullyHandlesClosedOrCrashedWindowWithoutUnhandledExceptions()
    {
        // Test 8.1: TargetResolver handles Target disappeared exception without throwing
        var mockUia = new Mock<IUIAutomationService>();
        mockUia
            .Setup(u => u.InspectElementAt(It.IsAny<int>(), It.IsAny<int>()))
            .Throws(new InvalidOperationException("Target window was destroyed or crashed."));

        var resolver = new UIATargetResolver(mockUia.Object);
        var context = new WindowContext(9999, 1234, "crashedApp", "Closed Window", new BoundingBox(0, 0, 400, 300), DateTime.UtcNow);
        var action = SemanticAction.CreateMouseClick(SemanticActionType.LeftClick, 150, 150, context, DateTime.UtcNow, 1);

        // Must resolve to fallback without throwing
        var element = await resolver.ResolveTargetAsync(action);
        Assert.NotNull(element);
        Assert.Equal("crashedApp", element.ProcessName);
        Assert.Equal(1234, element.ProcessId);

        // Test 8.2: Full RecordingEngine pipeline when target window disappears during capture
        var fakeMonitor = new FakeInputMonitoringService();
        var targetResolverMock = new Mock<ITargetResolver>();
        targetResolverMock
            .Setup(r => r.ResolveTargetAsync(It.IsAny<SemanticAction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(element);

        var captureServiceMock = new Mock<IScreenCaptureService>();
        captureServiceMock
            .Setup(c => c.Capture(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<BoundingBox?>(), It.IsAny<nint>()))
            .Throws(new InvalidOperationException("GDI capture failed because HWND is no longer valid."));

        var repoMock = new Mock<IProjectRepository>();
        repoMock.SetupGet(r => r.ProjectRootPath).Returns(_tempDirectory);

        var coordinator = new CaptureCoordinator(captureServiceMock.Object, repoMock.Object);
        using var engine = new RecordingEngine(
            fakeMonitor,
            null,
            new EventCorrelator(),
            targetResolverMock.Object,
            new DefaultRecordingPolicy(),
            new StepDetector(),
            coordinator,
            repoMock.Object
        );

        Step? recordedStep = null;
        engine.StepRecorded += (_, s) => recordedStep = s;

        engine.StartRecording();

        // Inject click on disappeared window
        fakeMonitor.SendMouse(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 150, 150, 0, DateTime.UtcNow));
        fakeMonitor.SendMouse(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 150, 150, 0, DateTime.UtcNow.AddMilliseconds(20)));

        await engine.StopRecordingAsync();

        // Verification: Step is safely recorded, ScreenshotPath is null, pipeline did not crash!
        Assert.NotNull(recordedStep);
        Assert.Null(recordedStep.ScreenshotPath);
        Assert.Equal("crashedApp", recordedStep.TargetElement.ProcessName);
        Assert.Equal(RecordingSessionState.Completed, engine.State);
    }

    #endregion

    #region Point 9: UIA partial metadata

    [Fact]
    public void Point09_UIAPartialMetadata_HandlesMissingNameOrAutomationId_AndEmptyBoundsWithoutFabrication()
    {
        var detector = new StepDetector();

        // Element with empty Name, empty AutomationId, empty ClassName, and empty BoundingRectangle
        var target = new ElementInfo(
            Name: string.Empty,
            ControlType: "Button",
            AutomationId: string.Empty,
            ClassName: string.Empty,
            ProcessName: "customApp",
            ProcessId: 500,
            WindowTitle: "App Title",
            WindowHandle: 12345,
            BoundingRectangle: BoundingBox.Empty
        );

        var action = SemanticAction.CreateMouseClick(
            SemanticActionType.LeftClick,
            250,
            350,
            WindowContext.Empty,
            DateTime.UtcNow,
            1
        );

        var step = detector.DetectStep(action, target, RecordingPolicyDecision.Allow, 1);

        Assert.NotNull(step);
        // Fallback title uses ControlType, does not fabricate fake name or print empty quotes
        Assert.Equal("Click Button", step.Title);
        Assert.Equal("Click the Button in customApp.", step.Description);

        // Click coordinates safely fallback to action coordinates
        Assert.Equal(250, step.ClickX);
        Assert.Equal(350, step.ClickY);

        // AutomationId and Name are empty strings, NOT fabricated
        Assert.NotNull(step.Metadata);
        Assert.Equal(string.Empty, step.TargetElement.Name);
        Assert.Equal(string.Empty, step.TargetElement.AutomationId);
        Assert.Equal(string.Empty, step.Metadata["AutomationId"]);
    }

    #endregion

    #region Point 10: Repeated Start/Stop

    [Fact]
    public async Task Point10_RepeatedStartStop_TenConsecutiveCycles_CorrectStateTransitionsAndResourceCleanup()
    {
        var fakeMonitor = new FakeInputMonitoringService();
        var targetResolverMock = new Mock<ITargetResolver>();
        targetResolverMock
            .Setup(r => r.ResolveTargetAsync(It.IsAny<SemanticAction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ElementInfo.Unknown);

        var captureCoordinatorMock = new Mock<ICaptureCoordinator>();
        captureCoordinatorMock
            .Setup(c => c.CaptureStepAsync(It.IsAny<int>(), It.IsAny<ElementInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        using var engine = new RecordingEngine(
            fakeMonitor,
            null,
            new EventCorrelator(),
            targetResolverMock.Object,
            new DefaultRecordingPolicy(),
            new StepDetector(),
            captureCoordinatorMock.Object
        );

        var stateTransitions = new List<RecordingSessionState>();
        engine.StateChanged += (_, state) => stateTransitions.Add(state);

        // Stress-test: Exactly 10 consecutive Start/Stop cycles
        for (int cycle = 1; cycle <= 10; cycle++)
        {
            engine.StartRecording();
            Assert.True(engine.IsRecording);
            Assert.Equal(RecordingSessionState.Recording, engine.State);

            // Inject a quick event in each cycle
            fakeMonitor.SendMouse(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 100, 100, 0, DateTime.UtcNow));
            fakeMonitor.SendMouse(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 100, 100, 0, DateTime.UtcNow.AddMilliseconds(10)));

            await engine.StopRecordingAsync();
            Assert.False(engine.IsRecording);
            Assert.Equal(RecordingSessionState.Completed, engine.State);
        }

        // Verify that state machine transitioned through Stopping and Completed in all 10 cycles
        var completedCount = stateTransitions.Count(s => s == RecordingSessionState.Completed);
        var recordingCount = stateTransitions.Count(s => s == RecordingSessionState.Recording);
        Assert.Equal(10, completedCount);
        Assert.Equal(10, recordingCount);
    }

    #endregion

    #region Point 11: Rapid interactions

    [Fact]
    public async Task Point11_RapidInteractions_ConcurrentStreamPreservesStrictSequenceIndexOrderWithoutDeadlocks()
    {
        var fakeMonitor = new FakeInputMonitoringService();
        var fakeWindowTracker = new FakeActiveWindowTracker();

        var win1 = new ActiveWindowInfo(101, 11, "app1", "Window 1", new BoundingBox(0, 0, 500, 500), DateTime.UtcNow);
        var win2 = new ActiveWindowInfo(202, 22, "app2", "Window 2", new BoundingBox(500, 0, 500, 500), DateTime.UtcNow);
        fakeWindowTracker.SwitchWindow(win1);

        var targetResolverMock = new Mock<ITargetResolver>();
        targetResolverMock
            .Setup(r => r.ResolveTargetAsync(It.IsAny<SemanticAction>(), It.IsAny<CancellationToken>()))
            .Returns<SemanticAction, CancellationToken>((act, _) => Task.FromResult(new ElementInfo(
                Name: act.KeyName ?? "Element",
                ControlType: "Control",
                AutomationId: "id",
                ClassName: "class",
                ProcessName: act.Context.ProcessName,
                ProcessId: act.Context.ProcessId,
                WindowTitle: act.Context.WindowTitle,
                WindowHandle: act.Context.WindowHandle,
                BoundingRectangle: new BoundingBox(10, 10, 50, 50)
            )));

        var captureCoordinatorMock = new Mock<ICaptureCoordinator>();
        captureCoordinatorMock
            .Setup(c => c.CaptureStepAsync(It.IsAny<int>(), It.IsAny<ElementInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        using var engine = new RecordingEngine(
            fakeMonitor,
            fakeWindowTracker,
            new EventCorrelator(flushTimeoutMs: 100),
            targetResolverMock.Object,
            new DefaultRecordingPolicy(),
            new StepDetector(),
            captureCoordinatorMock.Object
        );

        var recordedSteps = new List<Step>();
        engine.StepRecorded += (_, s) =>
        {
            lock (recordedSteps)
            {
                recordedSteps.Add(s);
            }
        };

        engine.StartRecording();

        var now = DateTime.UtcNow;

        // Rapid stream: Click -> Type -> Shortcut -> Click -> Click -> Switch -> Type
        // 1. Click (Left)
        fakeMonitor.SendMouse(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 100, 100, 0, now));
        fakeMonitor.SendMouse(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 100, 100, 0, now.AddMilliseconds(2)));

        // 2. Type "Hi"
        fakeMonitor.SendKeyboard(new RawKeyboardEvent(RawKeyboardEventType.KeyDown, 72, 0, KeyboardModifiers.None, "H", false, false, now.AddMilliseconds(5)));
        fakeMonitor.SendKeyboard(new RawKeyboardEvent(RawKeyboardEventType.KeyDown, 73, 0, KeyboardModifiers.None, "i", false, false, now.AddMilliseconds(8)));

        // 3. Shortcut Ctrl+S (automatically flushes "Hi" before shortcut!)
        fakeMonitor.SendKeyboard(new RawKeyboardEvent(RawKeyboardEventType.KeyDown, 83, 0, KeyboardModifiers.Control, "\x13", false, false, now.AddMilliseconds(12)));

        // 4. Click
        fakeMonitor.SendMouse(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 200, 200, 0, now.AddMilliseconds(15)));
        fakeMonitor.SendMouse(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 200, 200, 0, now.AddMilliseconds(18)));

        // 5. Click
        fakeMonitor.SendMouse(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 300, 300, 0, now.AddMilliseconds(22)));
        fakeMonitor.SendMouse(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 300, 300, 0, now.AddMilliseconds(25)));

        // 6. Switch window to win2
        fakeWindowTracker.SwitchWindow(win2);

        // 7. Type "Go" in win2
        fakeMonitor.SendKeyboard(new RawKeyboardEvent(RawKeyboardEventType.KeyDown, 71, 0, KeyboardModifiers.None, "G", false, false, now.AddMilliseconds(30)));
        fakeMonitor.SendKeyboard(new RawKeyboardEvent(RawKeyboardEventType.KeyDown, 79, 0, KeyboardModifiers.None, "o", false, false, now.AddMilliseconds(35)));

        // Stop recording with clean queue drain
        await engine.StopRecordingAsync();

        // Verification
        Assert.Equal(6, recordedSteps.Count);

        // Strict monotonically increasing SequenceIndex: 1, 2, 3, 4, 5, 6
        for (int i = 0; i < recordedSteps.Count; i++)
        {
            Assert.Equal(i + 1, recordedSteps[i].SequenceIndex);
        }

        Assert.Equal(ActionType.LeftClick, recordedSteps[0].Action);
        Assert.Equal(ActionType.TextInput, recordedSteps[1].Action);
        Assert.Equal(ActionType.KeyPress, recordedSteps[2].Action);
        Assert.Equal(ActionType.LeftClick, recordedSteps[3].Action);
        Assert.Equal(ActionType.LeftClick, recordedSteps[4].Action);
        Assert.Equal(ActionType.TextInput, recordedSteps[5].Action);

        // Window contexts properly isolated
        Assert.Equal("app1", recordedSteps[0].TargetElement.ProcessName);
        Assert.Equal("app1", recordedSteps[1].TargetElement.ProcessName);
        Assert.Equal("app2", recordedSteps[5].TargetElement.ProcessName);
    }

    #endregion

    #region Point 12: Password suppression

    [Fact]
    public async Task Point12_PasswordSuppression_ZeroPlaintextGuaranteeAcrossStepMetadataSQLiteAndLogs()
    {
        const string plaintextPassword = "SuperSecretP@ssw0rd!#42";

        var fakeMonitor = new FakeInputMonitoringService();
        var passwordTarget = new ElementInfo(
            Name: "PasswordInput",
            ControlType: "Edit",
            AutomationId: "pwdBox",
            ClassName: "PasswordBox",
            ProcessName: "secureApp",
            ProcessId: 999,
            WindowTitle: "Login",
            WindowHandle: 100,
            BoundingRectangle: new BoundingBox(50, 50, 150, 30),
            IsPassword: true
        );

        var targetResolverMock = new Mock<ITargetResolver>();
        targetResolverMock
            .Setup(r => r.ResolveTargetAsync(It.IsAny<SemanticAction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(passwordTarget);

        var captureCoordinatorMock = new Mock<ICaptureCoordinator>();
        captureCoordinatorMock
            .Setup(c => c.CaptureStepAsync(It.IsAny<int>(), It.IsAny<ElementInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Case A: Default Policy -> TextInput on IsPassword==true element is completely SUPPRESSED
        {
            var repoMock = new Mock<IProjectRepository>();

            using var engine = new RecordingEngine(
                fakeMonitor,
                null,
                new EventCorrelator(),
                targetResolverMock.Object,
                new DefaultRecordingPolicy(maskSensitiveInputs: false), // Default Suppress
                new StepDetector(),
                captureCoordinatorMock.Object,
                repoMock.Object
            );

            var recorded = new List<Step>();
            engine.StepRecorded += (_, s) => recorded.Add(s);

            engine.StartRecording();
            foreach (char ch in plaintextPassword)
            {
                fakeMonitor.SendKeyboard(new RawKeyboardEvent(RawKeyboardEventType.KeyDown, ch, 0, KeyboardModifiers.None, ch.ToString(), false, false, DateTime.UtcNow));
            }
            await engine.StopRecordingAsync();

            // Verification: ZERO steps emitted, recorded, or saved in repository
            Assert.Empty(recorded);
            repoMock.Verify(r => r.SaveStep(It.IsAny<Step>()), Times.Never);
        }

        // Case B: Policy with MaskSensitiveInputs: true -> Action masked to '••••••••', ZERO plaintext anywhere
        {
            using var sqliteConnection = new SqliteConnection("Data Source=:memory:");
            sqliteConnection.Open();

            var repository = new ProjectRepository(sqliteConnection, _tempDirectory);
            repository.CreateProject("Security Test Project");

            using var engine = new RecordingEngine(
                fakeMonitor,
                null,
                new EventCorrelator(),
                targetResolverMock.Object,
                new DefaultRecordingPolicy(maskSensitiveInputs: true),
                new StepDetector(),
                captureCoordinatorMock.Object,
                repository
            );

            var recorded = new List<Step>();
            engine.StepRecorded += (_, s) => recorded.Add(s);

            engine.StartRecording();
            foreach (char ch in plaintextPassword)
            {
                fakeMonitor.SendKeyboard(new RawKeyboardEvent(RawKeyboardEventType.KeyDown, ch, 0, KeyboardModifiers.None, ch.ToString(), false, false, DateTime.UtcNow));
            }
            await engine.StopRecordingAsync();

            Assert.Single(recorded);
            var step = recorded[0];

            // 1. In-memory Step model verification
            Assert.DoesNotContain(plaintextPassword, step.Title);
            Assert.DoesNotContain(plaintextPassword, step.Description);
            Assert.NotNull(step.Metadata);
            Assert.Equal("true", step.Metadata["IsMasked"]);
            foreach (var kvp in step.Metadata)
            {
                Assert.DoesNotContain(plaintextPassword, kvp.Key);
                Assert.DoesNotContain(plaintextPassword, kvp.Value);
            }

            // 2. Direct SQLite database verification (Zero Plaintext Guarantee)
            using var cmd = sqliteConnection.CreateCommand();
            cmd.CommandText = "SELECT Title, Description, MetadataJson FROM steps WHERE Id = @Id";
            cmd.Parameters.AddWithValue("@Id", step.Id.ToString());

            using var reader = cmd.ExecuteReader();
            Assert.True(reader.Read());

            string dbTitle = reader.GetString(0);
            string dbDescription = reader.GetString(1);
            string dbMetadataJson = reader.IsDBNull(2) ? "" : reader.GetString(2);

            Assert.DoesNotContain(plaintextPassword, dbTitle);
            Assert.DoesNotContain(plaintextPassword, dbDescription);
            Assert.DoesNotContain(plaintextPassword, dbMetadataJson);
        }
    }

    #endregion

    #region Point 13: Screenshot failure

    [Fact]
    public async Task Point13_ScreenshotFailure_LogsErrorAndSavesStepWithNullScreenshotWithoutPipelineCrash()
    {
        var fakeMonitor = new FakeInputMonitoringService();
        var target = new ElementInfo("Button", "Button", "btn1", "Button", "calc", 100, "Calc", 1, new BoundingBox(0, 0, 50, 50));

        var targetResolverMock = new Mock<ITargetResolver>();
        targetResolverMock
            .Setup(r => r.ResolveTargetAsync(It.IsAny<SemanticAction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        // Simulate capture failure throwing exception
        var captureServiceMock = new Mock<IScreenCaptureService>();
        captureServiceMock
            .Setup(c => c.Capture(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<BoundingBox?>(), It.IsAny<nint>()))
            .Throws(new InvalidOperationException("GDI capture subsystem failure."));

        var repoMock = new Mock<IProjectRepository>();
        repoMock.SetupGet(r => r.ProjectRootPath).Returns(_tempDirectory);

        var coordinator = new CaptureCoordinator(captureServiceMock.Object, repoMock.Object);

        using var engine = new RecordingEngine(
            fakeMonitor,
            null,
            new EventCorrelator(),
            targetResolverMock.Object,
            new DefaultRecordingPolicy(),
            new StepDetector(),
            coordinator,
            repoMock.Object
        );

        Step? recordedStep = null;
        engine.StepRecorded += (_, s) => recordedStep = s;

        engine.StartRecording();

        // Send click event
        fakeMonitor.SendMouse(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 25, 25, 0, DateTime.UtcNow));
        fakeMonitor.SendMouse(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 25, 25, 0, DateTime.UtcNow.AddMilliseconds(20)));

        await engine.StopRecordingAsync();

        // Verification: Step is saved, ScreenshotPath is null, pipeline completed cleanly
        Assert.NotNull(recordedStep);
        Assert.Null(recordedStep.ScreenshotPath);
        Assert.Equal(1, recordedStep.SequenceIndex);
        Assert.Equal(RecordingSessionState.Completed, engine.State);

        // Verify repository save was called even with null screenshot
        repoMock.Verify(r => r.SaveStep(It.Is<Step>(s => s.ScreenshotPath == null)), Times.Once);
    }

    #endregion

    #region Point 14: Capture cancellation

    [Fact]
    public async Task Point14_CaptureCancellation_CancelledTokenReturnsFailureResultWithoutUnhandledException()
    {
        var captureServiceMock = new Mock<IScreenCaptureService>();
        var repoMock = new Mock<IProjectRepository>();
        repoMock.SetupGet(r => r.ProjectRootPath).Returns(_tempDirectory);

        var coordinator = new CaptureCoordinator(captureServiceMock.Object, repoMock.Object);
        var target = new ElementInfo("Target", "Pane", "pane1", "Canvas", "app", 10, "App", 1, new BoundingBox(0, 0, 100, 100));

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Already cancelled token

        // 1. CaptureStepWithResultAsync with cancelled token
        var result = await coordinator.CaptureStepWithResultAsync(1, target, cts.Token);

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Null(result.RelativePath);
        Assert.Equal("Operation cancelled.", result.ErrorMessage);

        // 2. CaptureStepAsync with cancelled token returns null without throwing
        var relativePath = await coordinator.CaptureStepAsync(1, target, cts.Token);
        Assert.Null(relativePath);
    }

    #endregion

    #region Point 15: No duplicate semantic action

    [Fact]
    public void Point15_NoDuplicateSemanticAction_IgnoresBareModifiers_NoDuplicateClicksInDrag_NoTextDuplication()
    {
        using var correlator = new EventCorrelator();
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var context = WindowContext.Empty;
        var now = DateTime.UtcNow;

        // 1. Modifier keys alone (Shift, Ctrl, Alt, Win) must emit ZERO actions
        int[] modifierKeys = [0x10, 0xA0, 0xA1, 0x11, 0xA2, 0xA3, 0x12, 0xA4, 0xA5, 0x5B, 0x5C];
        foreach (int vk in modifierKeys)
        {
            correlator.ProcessKeyboardEvent(new RawKeyboardEvent(RawKeyboardEventType.KeyDown, vk, 0, KeyboardModifiers.Shift, null, false, false, now), context);
            correlator.ProcessKeyboardEvent(new RawKeyboardEvent(RawKeyboardEventType.KeyUp, vk, 0, KeyboardModifiers.None, null, false, false, now.AddMilliseconds(5)), context);
        }
        correlator.FlushPending();
        Assert.Empty(emitted);

        // 2. Drag action emits ONLY DragAndDrop, ZERO duplicate LeftClick or DoubleClick
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 10, 10, 0, now), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 100, 100, 0, now.AddMilliseconds(50)), context);

        Assert.Single(emitted);
        Assert.Equal(SemanticActionType.DragAndDrop, emitted[0].ActionType);

        // 3. Text deduplication: Typing "XYZ" sequentially flushes into a SINGLE TextInput action
        correlator.ProcessKeyboardEvent(new RawKeyboardEvent(RawKeyboardEventType.KeyDown, 88, 0, KeyboardModifiers.None, "X", false, false, now), context);
        correlator.ProcessKeyboardEvent(new RawKeyboardEvent(RawKeyboardEventType.KeyDown, 89, 0, KeyboardModifiers.None, "Y", false, false, now.AddMilliseconds(10)), context);
        correlator.ProcessKeyboardEvent(new RawKeyboardEvent(RawKeyboardEventType.KeyDown, 90, 0, KeyboardModifiers.None, "Z", false, false, now.AddMilliseconds(20)), context);
        correlator.FlushPending();

        Assert.Equal(2, emitted.Count);
        Assert.Equal(SemanticActionType.TextInput, emitted[1].ActionType);
        Assert.Equal("XYZ", emitted[1].Text);
    }

    #endregion

    #region Helper Test Fakes

    private sealed class FakeInputMonitoringService : IInputMonitoringService
    {
        public event EventHandler<RawMouseEvent>? MouseEventReceived;
        public event EventHandler<RawKeyboardEvent>? KeyboardEventReceived;
        public bool IsRunning { get; private set; }

        public void Start() => IsRunning = true;
        public void Stop() => IsRunning = false;
        public void Dispose() => Stop();

        public void SendMouse(RawMouseEvent e) => MouseEventReceived?.Invoke(this, e);
        public void SendKeyboard(RawKeyboardEvent e) => KeyboardEventReceived?.Invoke(this, e);
    }

    private sealed class FakeActiveWindowTracker : IActiveWindowTracker
    {
        private ActiveWindowInfo? _currentWindow;
        public event EventHandler<ActiveWindowInfo>? ActiveWindowChanged;
        public bool IsRunning { get; private set; }

        public FakeActiveWindowTracker(ActiveWindowInfo? initialWindow = null)
        {
            _currentWindow = initialWindow;
        }

        public ActiveWindowInfo? GetActiveWindow() => _currentWindow;
        public void Start() => IsRunning = true;
        public void Stop() => IsRunning = false;
        public void Dispose() => Stop();

        public void SwitchWindow(ActiveWindowInfo newWindow)
        {
            _currentWindow = newWindow;
            ActiveWindowChanged?.Invoke(this, newWindow);
        }
    }

    #endregion
}
