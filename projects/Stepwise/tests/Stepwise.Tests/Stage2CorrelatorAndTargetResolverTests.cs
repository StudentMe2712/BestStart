using Moq;
using Stepwise.Core.Engine;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Stepwise.WindowsIntegration.Automation;
using Stepwise.WindowsIntegration.Services;
using Xunit;

namespace Stepwise.Tests;

public class Stage2CorrelatorAndTargetResolverTests
{
    #region 1. WindowsSystemMetricsProvider Tests

    [Fact]
    public void WindowsSystemMetricsProvider_ReturnsValidMetrics()
    {
        var provider = new WindowsSystemMetricsProvider();

        Assert.True(provider.DoubleClickTimeMs > 0);
        Assert.True(provider.DoubleClickWidth > 0);
        Assert.True(provider.DoubleClickHeight > 0);
    }

    #endregion

    #region 2. UIATargetResolver Tests

    [Fact]
    public async Task UIATargetResolver_MouseAction_UsesUiaService()
    {
        var mockUia = new Mock<IUIAutomationService>();
        var expectedElement = new ElementInfo(
            Name: "Save Button",
            ControlType: "Button",
            AutomationId: "btnSave",
            ClassName: "Button",
            ProcessName: "notepad",
            ProcessId: 1234,
            WindowTitle: "Untitled - Notepad",
            WindowHandle: 5555,
            BoundingRectangle: new BoundingBox(10, 20, 100, 30)
        );

        mockUia
            .Setup(u => u.InspectElementAt(150, 250))
            .Returns(expectedElement);

        var resolver = new UIATargetResolver(mockUia.Object);

        var context = new WindowContext(5555, 1234, "notepad", "Untitled - Notepad", new BoundingBox(0, 0, 800, 600), DateTime.UtcNow);
        var action = SemanticAction.CreateMouseClick(
            SemanticActionType.LeftClick,
            150,
            250,
            context,
            DateTime.UtcNow
        );

        var result = await resolver.ResolveTargetAsync(action);

        Assert.NotNull(result);
        Assert.Equal("Save Button", result.Name);
        Assert.Equal("Button", result.ControlType);
        Assert.Equal(1234, result.ProcessId);
        mockUia.Verify(u => u.InspectElementAt(150, 250), Times.Once);
    }

    [Fact]
    public async Task UIATargetResolver_KeyboardAction_FallsBackToContextSafely()
    {
        var mockUia = new Mock<IUIAutomationService>();
        var resolver = new UIATargetResolver(mockUia.Object);

        var context = new WindowContext(8888, 4321, "code", "VS Code", new BoundingBox(0, 0, 1920, 1080), DateTime.UtcNow);
        var action = SemanticAction.CreateKeyPress(
            virtualKey: 13,
            keyName: "Enter",
            modifiers: KeyboardModifiers.None,
            context: context,
            timestamp: DateTime.UtcNow
        );

        var result = await resolver.ResolveTargetAsync(action);

        Assert.NotNull(result);
        Assert.Equal(4321, result.ProcessId);
        Assert.Equal("code", result.ProcessName);
        Assert.Equal("VS Code", result.WindowTitle);
        Assert.Equal(8888, result.WindowHandle);
    }

    [Fact]
    public async Task UIATargetResolver_WhenUiaThrows_ReturnsFallbackGracefully()
    {
        var mockUia = new Mock<IUIAutomationService>();
        mockUia
            .Setup(u => u.InspectElementAt(It.IsAny<int>(), It.IsAny<int>()))
            .Throws(new InvalidOperationException("UIA Failure"));

        var resolver = new UIATargetResolver(mockUia.Object);

        var context = new WindowContext(999, 111, "explorer", "File Explorer", new BoundingBox(5, 5, 500, 400), DateTime.UtcNow);
        var action = SemanticAction.CreateMouseClick(
            SemanticActionType.LeftClick,
            50,
            50,
            context,
            DateTime.UtcNow
        );

        var result = await resolver.ResolveTargetAsync(action);

        Assert.NotNull(result);
        Assert.Equal(111, result.ProcessId);
        Assert.Equal("explorer", result.ProcessName);
        Assert.Equal("File Explorer", result.WindowTitle);
    }

    [Fact]
    public async Task UIATargetResolver_WithWindowTracker_UsesTrackerWhenContextEmpty()
    {
        var mockUia = new Mock<IUIAutomationService>();
        var mockTracker = new Mock<IActiveWindowTracker>();
        mockTracker
            .Setup(t => t.GetActiveWindow())
            .Returns(new ActiveWindowInfo(777, 333, "calc", "Calculator", new BoundingBox(0, 0, 300, 400), DateTime.UtcNow));

        var resolver = new UIATargetResolver(mockUia.Object, mockTracker.Object);

        var action = SemanticAction.CreateKeyPress(
            virtualKey: 27,
            keyName: "Escape",
            modifiers: KeyboardModifiers.None,
            context: WindowContext.Empty,
            timestamp: DateTime.UtcNow
        );

        var result = await resolver.ResolveTargetAsync(action);

        Assert.NotNull(result);
        Assert.Equal(333, result.ProcessId);
        Assert.Equal("calc", result.ProcessName);
        Assert.Equal("Calculator", result.WindowTitle);
        Assert.Equal(777, result.WindowHandle);
    }

    [Fact]
    public async Task UIATargetResolver_DragAndDrop_ResolvesTargetAtStartCoordinates()
    {
        var mockUia = new Mock<IUIAutomationService>();
        var expectedElement = new ElementInfo(
            Name: "Source Item",
            ControlType: "ListItem",
            AutomationId: "itemSrc",
            ClassName: "ListViewItem",
            ProcessName: "explorer",
            ProcessId: 1001,
            WindowTitle: "Documents",
            WindowHandle: 4444,
            BoundingRectangle: new BoundingBox(100, 200, 150, 40)
        );

        mockUia
            .Setup(u => u.InspectElementAt(100, 200))
            .Returns(expectedElement);

        var resolver = new UIATargetResolver(mockUia.Object);
        var context = new WindowContext(4444, 1001, "explorer", "Documents", new BoundingBox(0, 0, 1024, 768), DateTime.UtcNow);
        var action = SemanticAction.CreateDragAndDrop(
            startX: 100,
            startY: 200,
            endX: 450,
            endY: 550,
            button: RawMouseButton.Left,
            context: context,
            timestamp: DateTime.UtcNow
        );

        var result = await resolver.ResolveTargetAsync(action);

        Assert.NotNull(result);
        Assert.Equal("Source Item", result.Name);
        Assert.Equal("ListItem", result.ControlType);
        Assert.Equal("itemSrc", result.AutomationId);
        mockUia.Verify(u => u.InspectElementAt(100, 200), Times.Once);
    }

    [Fact]
    public async Task UIATargetResolver_Scroll_ResolvesTargetAtScrollCoordinates()
    {
        var mockUia = new Mock<IUIAutomationService>();
        var expectedElement = new ElementInfo(
            Name: "Content Viewer",
            ControlType: "Pane",
            AutomationId: "scrollPane",
            ClassName: "ScrollViewer",
            ProcessName: "browser",
            ProcessId: 2002,
            WindowTitle: "Web Page",
            WindowHandle: 6666,
            BoundingRectangle: new BoundingBox(250, 350, 600, 800)
        );

        mockUia
            .Setup(u => u.InspectElementAt(250, 350))
            .Returns(expectedElement);

        var resolver = new UIATargetResolver(mockUia.Object);
        var context = new WindowContext(6666, 2002, "browser", "Web Page", new BoundingBox(0, 0, 1200, 900), DateTime.UtcNow);
        var action = SemanticAction.CreateScroll(
            x: 250,
            y: 350,
            delta: -120,
            context: context,
            timestamp: DateTime.UtcNow
        );

        var result = await resolver.ResolveTargetAsync(action);

        Assert.NotNull(result);
        Assert.Equal("Content Viewer", result.Name);
        Assert.Equal("Pane", result.ControlType);
        Assert.Equal(-120, action.Delta);
        mockUia.Verify(u => u.InspectElementAt(250, 350), Times.Once);
    }

    [Fact]
    public async Task UIATargetResolver_WindowActivated_ResolvesFromContextDirectlyWithoutUiaCall()
    {
        var mockUia = new Mock<IUIAutomationService>();
        var resolver = new UIATargetResolver(mockUia.Object);

        var context = new WindowContext(7788, 3003, "notepad", "Notes - Notepad", new BoundingBox(50, 50, 800, 600), DateTime.UtcNow);
        var action = SemanticAction.CreateWindowActivated(context, DateTime.UtcNow);

        var result = await resolver.ResolveTargetAsync(action);

        Assert.NotNull(result);
        Assert.Equal("WindowControl", result.ControlType);
        Assert.Equal(3003, result.ProcessId);
        Assert.Equal("notepad", result.ProcessName);
        Assert.Equal("Notes - Notepad", result.WindowTitle);
        Assert.Equal(7788, result.WindowHandle);
        Assert.Equal(new BoundingBox(50, 50, 800, 600), result.BoundingRectangle);

        mockUia.Verify(u => u.InspectElementAt(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UIATargetResolver_WindowClosed_ResolvesFromContextAndResetsLastElement()
    {
        var mockUia = new Mock<IUIAutomationService>();
        var clickedElement = new ElementInfo(
            Name: "Submit",
            ControlType: "Button",
            AutomationId: "btnSub",
            ClassName: "Button",
            ProcessName: "oldApp",
            ProcessId: 4004,
            WindowTitle: "Old App",
            WindowHandle: 8899,
            BoundingRectangle: new BoundingBox(10, 10, 50, 20)
        );

        mockUia.Setup(u => u.InspectElementAt(10, 10)).Returns(clickedElement);
        var resolver = new UIATargetResolver(mockUia.Object);

        var clickContext = new WindowContext(8899, 4004, "oldApp", "Old App", new BoundingBox(0, 0, 500, 500), DateTime.UtcNow);
        var clickAction = SemanticAction.CreateMouseClick(SemanticActionType.LeftClick, 10, 10, clickContext, DateTime.UtcNow);
        await resolver.ResolveTargetAsync(clickAction);

        // WindowClosed for the window
        var closeAction = SemanticAction.CreateWindowClosed(clickContext, DateTime.UtcNow);
        var closeResult = await resolver.ResolveTargetAsync(closeAction);

        Assert.NotNull(closeResult);
        Assert.Equal("WindowControl", closeResult.ControlType);
        Assert.Equal(4004, closeResult.ProcessId);
        Assert.Equal("oldApp", closeResult.ProcessName);

        // Subsequent keyboard action in new window should NOT reuse the old closed window's element
        var newContext = new WindowContext(9900, 5005, "newApp", "New App", new BoundingBox(0, 0, 600, 600), DateTime.UtcNow);
        var keyAction = SemanticAction.CreateKeyPress(13, "Enter", KeyboardModifiers.None, newContext, DateTime.UtcNow);
        var keyResult = await resolver.ResolveTargetAsync(keyAction);

        Assert.Equal(5005, keyResult.ProcessId);
        Assert.Equal("newApp", keyResult.ProcessName);
        Assert.NotEqual("Submit", keyResult.Name);
    }

    [Fact]
    public async Task UIATargetResolver_PartialElementInfo_DoesNotFabricateFakeIdsOrNames()
    {
        var mockUia = new Mock<IUIAutomationService>();
        var partialElement = new ElementInfo(
            Name: string.Empty,
            ControlType: "Edit",
            AutomationId: string.Empty,
            ClassName: "Edit",
            ProcessName: "notepad",
            ProcessId: 1234,
            WindowTitle: "Untitled - Notepad",
            WindowHandle: 5555,
            BoundingRectangle: new BoundingBox(10, 20, 100, 30),
            FrameworkId: "Win32",
            IsPassword: false
        );

        mockUia
            .Setup(u => u.InspectElementAt(50, 50))
            .Returns(partialElement);

        var resolver = new UIATargetResolver(mockUia.Object);
        var context = new WindowContext(5555, 1234, "notepad", "Untitled - Notepad", new BoundingBox(0, 0, 800, 600), DateTime.UtcNow);
        var action = SemanticAction.CreateMouseClick(SemanticActionType.LeftClick, 50, 50, context, DateTime.UtcNow);

        var result = await resolver.ResolveTargetAsync(action);

        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.Name);
        Assert.Equal(string.Empty, result.AutomationId);
        Assert.Equal("Edit", result.ControlType);
        Assert.Equal("Edit", result.ClassName);
        Assert.Equal("Win32", result.FrameworkId);
    }

    #endregion

    #region 3. EventCorrelator Mouse Tests

    [Fact]
    public void EventCorrelator_SingleLeftClick_EmitsLeftClick()
    {
        using var correlator = new EventCorrelator();
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var context = new WindowContext(1, 10, "app", "App", new BoundingBox(0, 0, 100, 100), DateTime.UtcNow);
        var t0 = DateTime.UtcNow;

        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 100, 200, 0, t0), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 100, 200, 0, t0.AddMilliseconds(50)), context);

        Assert.Single(emitted);
        var action = emitted[0];
        Assert.Equal(SemanticActionType.LeftClick, action.ActionType);
        Assert.Equal(100, action.X);
        Assert.Equal(200, action.Y);
        Assert.Equal(1, action.SequenceIndex);
        Assert.Equal("app", action.Context.ProcessName);
    }

    [Fact]
    public void EventCorrelator_DoubleClick_EmitsDoubleLeftClick()
    {
        var metricsMock = new Mock<ISystemMetricsProvider>();
        metricsMock.SetupGet(m => m.DoubleClickTimeMs).Returns(500);
        metricsMock.SetupGet(m => m.DoubleClickWidth).Returns(4);
        metricsMock.SetupGet(m => m.DoubleClickHeight).Returns(4);

        using var correlator = new EventCorrelator(metricsMock.Object);
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var context = new WindowContext(1, 10, "app", "App", new BoundingBox(0, 0, 100, 100), DateTime.UtcNow);
        var t0 = DateTime.UtcNow;

        // Click 1
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 100, 100, 0, t0), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 100, 100, 0, t0.AddMilliseconds(20)), context);

        // Click 2 within 200ms and within 2px
        var t1 = t0.AddMilliseconds(200);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 102, 101, 0, t1), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 102, 101, 0, t1.AddMilliseconds(20)), context);

        Assert.Equal(2, emitted.Count);
        Assert.Equal(SemanticActionType.LeftClick, emitted[0].ActionType);
        Assert.Equal(1, emitted[0].SequenceIndex);
        Assert.Equal(SemanticActionType.DoubleLeftClick, emitted[1].ActionType);
        Assert.Equal(2, emitted[1].SequenceIndex);

        // Click 3 (immediately after click 2) -> must be a regular LeftClick, NOT a double click!
        var t2 = t1.AddMilliseconds(100);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 102, 101, 0, t2), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 102, 101, 0, t2.AddMilliseconds(20)), context);

        Assert.Equal(3, emitted.Count);
        Assert.Equal(SemanticActionType.LeftClick, emitted[2].ActionType);
        Assert.Equal(3, emitted[2].SequenceIndex);
    }

    [Fact]
    public void EventCorrelator_DoubleClick_ExceedingTime_EmitsTwoSingleClicks()
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

        // Click 1
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 100, 100, 0, t0), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 100, 100, 0, t0.AddMilliseconds(20)), context);

        // Click 2 after 400ms (> 300ms)
        var t1 = t0.AddMilliseconds(400);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 100, 100, 0, t1), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 100, 100, 0, t1.AddMilliseconds(20)), context);

        Assert.Equal(2, emitted.Count);
        Assert.Equal(SemanticActionType.LeftClick, emitted[0].ActionType);
        Assert.Equal(SemanticActionType.LeftClick, emitted[1].ActionType);
    }

    [Fact]
    public void EventCorrelator_RightAndMiddleClicks_EmitCorrectActions()
    {
        using var correlator = new EventCorrelator();
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var now = DateTime.UtcNow;
        var context = WindowContext.Empty;

        // Right Click
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Right, 50, 60, 0, now), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Right, 50, 60, 0, now.AddMilliseconds(10)), context);

        // Middle Click
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Middle, 70, 80, 0, now.AddMilliseconds(20)), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Middle, 70, 80, 0, now.AddMilliseconds(30)), context);

        Assert.Equal(2, emitted.Count);
        Assert.Equal(SemanticActionType.RightClick, emitted[0].ActionType);
        Assert.Equal(SemanticActionType.MiddleClick, emitted[1].ActionType);
    }

    [Fact]
    public void EventCorrelator_DragExceedingTolerance_DoesNotEmitClick()
    {
        var metricsMock = new Mock<ISystemMetricsProvider>();
        metricsMock.SetupGet(m => m.DoubleClickWidth).Returns(4); // tolerance = 8
        metricsMock.SetupGet(m => m.DoubleClickHeight).Returns(4); // tolerance = 8

        using var correlator = new EventCorrelator(metricsMock.Object);
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var now = DateTime.UtcNow;
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 100, 100, 0, now));
        // MouseUp at 150, 200 (delta 50, 100 > 8)
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 150, 200, 0, now.AddMilliseconds(200)));

        Assert.DoesNotContain(emitted, a => a.ActionType == SemanticActionType.LeftClick);
        Assert.Single(emitted);
        Assert.Equal(SemanticActionType.DragAndDrop, emitted[0].ActionType);
        Assert.Equal(100, emitted[0].X);
        Assert.Equal(100, emitted[0].Y);
        Assert.Equal(150, emitted[0].EndX);
        Assert.Equal(200, emitted[0].EndY);
    }

    #endregion

    #region 4. EventCorrelator Keyboard Tests

    [Fact]
    public void EventCorrelator_KeyUp_IsIgnored()
    {
        using var correlator = new EventCorrelator();
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var keyUp = new RawKeyboardEvent(
            EventType: RawKeyboardEventType.KeyUp,
            VirtualKey: 13,
            ScanCode: 0,
            Modifiers: KeyboardModifiers.None,
            Character: null,
            IsDeadKey: false,
            IsExtendedKey: false,
            Timestamp: DateTime.UtcNow
        );

        correlator.ProcessKeyboardEvent(keyUp);
        Assert.Empty(emitted);
    }

    [Fact]
    public void EventCorrelator_Shortcut_EmitsShortcutAction()
    {
        using var correlator = new EventCorrelator();
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var shortcutEvent = new RawKeyboardEvent(
            EventType: RawKeyboardEventType.KeyDown,
            VirtualKey: 83, // 'S'
            ScanCode: 0,
            Modifiers: KeyboardModifiers.Control,
            Character: "\u0013",
            IsDeadKey: false,
            IsExtendedKey: false,
            Timestamp: DateTime.UtcNow
        );

        correlator.ProcessKeyboardEvent(shortcutEvent);

        Assert.Single(emitted);
        var action = emitted[0];
        Assert.Equal(SemanticActionType.Shortcut, action.ActionType);
        Assert.Equal(83, action.VirtualKey);
        Assert.Equal("S", action.KeyName);
        Assert.Equal(KeyboardModifiers.Control, action.Modifiers);
    }

    [Theory]
    [InlineData(13, "Enter")]
    [InlineData(27, "Escape")]
    [InlineData(9, "Tab")]
    public void EventCorrelator_EnterEscapeTab_EmitsKeyPressAction(int vk, string expectedKeyName)
    {
        using var correlator = new EventCorrelator();
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var keyEvent = new RawKeyboardEvent(
            EventType: RawKeyboardEventType.KeyDown,
            VirtualKey: vk,
            ScanCode: 0,
            Modifiers: KeyboardModifiers.None,
            Character: null,
            IsDeadKey: false,
            IsExtendedKey: false,
            Timestamp: DateTime.UtcNow
        );

        correlator.ProcessKeyboardEvent(keyEvent);

        Assert.Single(emitted);
        var action = emitted[0];
        Assert.Equal(SemanticActionType.KeyPress, action.ActionType);
        Assert.Equal(vk, action.VirtualKey);
        Assert.Equal(expectedKeyName, action.KeyName);
    }

    [Fact]
    public void EventCorrelator_NavigationKeys_EmitKeyPressAction()
    {
        using var correlator = new EventCorrelator();
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var backspace = new RawKeyboardEvent(
            EventType: RawKeyboardEventType.KeyDown,
            VirtualKey: 8,
            ScanCode: 0,
            Modifiers: KeyboardModifiers.None,
            Character: "\b",
            IsDeadKey: false,
            IsExtendedKey: false,
            Timestamp: DateTime.UtcNow
        );

        correlator.ProcessKeyboardEvent(backspace);

        Assert.Single(emitted);
        var action = emitted[0];
        Assert.Equal(SemanticActionType.KeyPress, action.ActionType);
        Assert.Equal(8, action.VirtualKey);
        Assert.Equal("Backspace", action.KeyName);
    }

    [Fact]
    public void EventCorrelator_TextInput_BuffersAndFlushesOnExplicitFlush()
    {
        using var correlator = new EventCorrelator();
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var now = DateTime.UtcNow;
        var context = new WindowContext(10, 20, "notepad", "Notes", new BoundingBox(0, 0, 500, 500), now);

        string text = "Hello World";
        for (int i = 0; i < text.Length; i++)
        {
            var keyEvent = new RawKeyboardEvent(
                EventType: RawKeyboardEventType.KeyDown,
                VirtualKey: text[i],
                ScanCode: 0,
                Modifiers: char.IsUpper(text[i]) ? KeyboardModifiers.Shift : KeyboardModifiers.None,
                Character: text[i].ToString(),
                IsDeadKey: false,
                IsExtendedKey: false,
                Timestamp: now.AddMilliseconds(i * 50)
            );
            correlator.ProcessKeyboardEvent(keyEvent, context);
        }

        // Initially buffered, no action emitted yet
        Assert.Empty(emitted);

        // Explicit flush
        correlator.FlushPending();

        Assert.Single(emitted);
        var action = emitted[0];
        Assert.Equal(SemanticActionType.TextInput, action.ActionType);
        Assert.Equal("Hello World", action.Text);
        Assert.Equal(11, action.CharacterCount);
        Assert.Equal("notepad", action.Context.ProcessName);
        Assert.Equal(now, action.StartedAt);
        Assert.Equal(now.AddMilliseconds(10 * 50), action.CompletedAt);
    }

    [Fact]
    public async Task EventCorrelator_TextInput_FlushesOnInactivityTimer()
    {
        // 50ms flush timeout for fast unit testing
        using var correlator = new EventCorrelator(flushTimeoutMs: 50);
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var keyEvent = new RawKeyboardEvent(
            EventType: RawKeyboardEventType.KeyDown,
            VirtualKey: 65,
            ScanCode: 0,
            Modifiers: KeyboardModifiers.None,
            Character: "A",
            IsDeadKey: false,
            IsExtendedKey: false,
            Timestamp: DateTime.UtcNow
        );

        correlator.ProcessKeyboardEvent(keyEvent);
        Assert.Empty(emitted);

        // Wait for timer to fire
        await Task.Delay(150);

        Assert.Single(emitted);
        Assert.Equal(SemanticActionType.TextInput, emitted[0].ActionType);
        Assert.Equal("A", emitted[0].Text);
    }

    [Fact]
    public void EventCorrelator_MouseClick_FlushesPendingTextInputFirst()
    {
        using var correlator = new EventCorrelator();
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var now = DateTime.UtcNow;

        // Type 'A'
        var keyEvent = new RawKeyboardEvent(
            EventType: RawKeyboardEventType.KeyDown,
            VirtualKey: 65,
            ScanCode: 0,
            Modifiers: KeyboardModifiers.None,
            Character: "A",
            IsDeadKey: false,
            IsExtendedKey: false,
            Timestamp: now
        );
        correlator.ProcessKeyboardEvent(keyEvent);

        // Click mouse
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 100, 100, 0, now.AddMilliseconds(100)));
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 100, 100, 0, now.AddMilliseconds(120)));

        Assert.Equal(2, emitted.Count);
        // Sequence index 1: TextInput
        Assert.Equal(SemanticActionType.TextInput, emitted[0].ActionType);
        Assert.Equal(1, emitted[0].SequenceIndex);
        Assert.Equal("A", emitted[0].Text);

        // Sequence index 2: LeftClick
        Assert.Equal(SemanticActionType.LeftClick, emitted[1].ActionType);
        Assert.Equal(2, emitted[1].SequenceIndex);
    }

    [Fact]
    public void EventCorrelator_Reset_ClearsBufferAndStateWithoutEmitting()
    {
        using var correlator = new EventCorrelator();
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var keyEvent = new RawKeyboardEvent(
            EventType: RawKeyboardEventType.KeyDown,
            VirtualKey: 65,
            ScanCode: 0,
            Modifiers: KeyboardModifiers.None,
            Character: "A",
            IsDeadKey: false,
            IsExtendedKey: false,
            Timestamp: DateTime.UtcNow
        );
        correlator.ProcessKeyboardEvent(keyEvent);

        correlator.Reset();
        correlator.FlushPending();

        Assert.Empty(emitted);
    }

    [Fact]
    public void EventCorrelator_Dispose_FlushesPendingText()
    {
        var correlator = new EventCorrelator();
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var keyEvent = new RawKeyboardEvent(
            EventType: RawKeyboardEventType.KeyDown,
            VirtualKey: 65,
            ScanCode: 0,
            Modifiers: KeyboardModifiers.None,
            Character: "Z",
            IsDeadKey: false,
            IsExtendedKey: false,
            Timestamp: DateTime.UtcNow
        );
        correlator.ProcessKeyboardEvent(keyEvent);

        correlator.Dispose();

        Assert.Single(emitted);
        Assert.Equal("Z", emitted[0].Text);
    }

    #endregion

    #region 5. UIAutomationService Robustness & Fallback Tests

    [Fact]
    public void UIAutomationService_GetProcessNameById_WhenPidIsZeroOrNegative_ReturnsUnknownOrFallback()
    {
        var resultZero = UIAutomationService.GetProcessNameById(0);
        Assert.Equal("Unknown", resultZero);

        var resultNegative = UIAutomationService.GetProcessNameById(-1);
        Assert.Equal("Unknown", resultNegative);

        var fallbackContext = new WindowContext(123, 0, "fallbackProc", "Title", new BoundingBox(0, 0, 100, 100), DateTime.UtcNow);
        var resultWithFallback = UIAutomationService.GetProcessNameById(0, fallbackContext);
        Assert.Equal("fallbackProc", resultWithFallback);
    }

    [Fact]
    public void UIAutomationService_GetProcessNameById_WhenProcessTerminated_ReturnsUnknownOrFallbackWithoutCrashing()
    {
        int nonExistentPid = 99999999;

        var resultWithoutFallback = UIAutomationService.GetProcessNameById(nonExistentPid);
        Assert.Equal("Unknown", resultWithoutFallback);

        var fallbackContext = new WindowContext(123, nonExistentPid, "cachedApp", "Title", new BoundingBox(0, 0, 100, 100), DateTime.UtcNow);
        var resultWithFallback = UIAutomationService.GetProcessNameById(nonExistentPid, fallbackContext);
        Assert.Equal("cachedApp", resultWithFallback);
    }

    [Fact]
    public void UIAutomationService_NativeHelpers_WhenHwndZero_ReturnSafeDefaults()
    {
        Assert.Equal(nint.Zero, UIAutomationService.GetRootWindowHandle(nint.Zero));
        Assert.Equal(string.Empty, UIAutomationService.GetWindowTitle(nint.Zero));
        Assert.Equal(string.Empty, UIAutomationService.GetWindowClassName(nint.Zero));
        Assert.Equal(BoundingBox.Empty, UIAutomationService.GetWindowBounds(nint.Zero));
    }

    [Fact]
    public void UIAutomationService_FallbackWin32Inspection_WhenCoordinatesInvalid_ReturnsUnknownOrContextFallback()
    {
        var resultWithoutContext = UIAutomationService.FallbackWin32Inspection(-999999, -999999);
        Assert.Equal(ElementInfo.Unknown, resultWithoutContext);

        var context = new WindowContext(555, 666, "myProc", "My Window", new BoundingBox(10, 20, 300, 200), DateTime.UtcNow);
        var resultWithContext = UIAutomationService.FallbackWin32Inspection(-999999, -999999, context);

        Assert.NotNull(resultWithContext);
        Assert.Equal("WindowControl", resultWithContext.ControlType);
        Assert.Equal("myProc", resultWithContext.ProcessName);
        Assert.Equal(666, resultWithContext.ProcessId);
        Assert.Equal("My Window", resultWithContext.WindowTitle);
        Assert.Equal(555, resultWithContext.WindowHandle);
        Assert.Equal(new BoundingBox(10, 20, 300, 200), resultWithContext.BoundingRectangle);
        Assert.Equal(string.Empty, resultWithContext.Name);
        Assert.Equal(string.Empty, resultWithContext.AutomationId);
    }

    [Fact]
    public void UIAutomationService_InspectElementAt_InvalidCoordinates_ReturnsSafeUnknown()
    {
        var service = new UIAutomationService();
        var element = service.InspectElementAt(-50000, -50000);

        Assert.NotNull(element);
        Assert.Equal(string.Empty, element.Name);
        Assert.Equal(string.Empty, element.AutomationId);
        Assert.False(element.IsPassword);
    }

    [Fact]
    public void UIATargetResolver_CreateFallbackFromContext_EmptyContext_ReturnsUnknown()
    {
        var nullResult = UIATargetResolver.CreateFallbackFromContext(null);
        Assert.Equal(ElementInfo.Unknown, nullResult);

        var emptyResult = UIATargetResolver.CreateFallbackFromContext(WindowContext.Empty);
        Assert.Equal(ElementInfo.Unknown, emptyResult);
    }

    [Fact]
    public void UIATargetResolver_CreateFallbackFromContext_ValidContext_PopulatesAll11Properties()
    {
        var context = new WindowContext(
            WindowHandle: 123456,
            ProcessId: 7890,
            ProcessName: "testHost",
            WindowTitle: "Test Application",
            Bounds: new BoundingBox(100, 100, 800, 600),
            Timestamp: DateTime.UtcNow
        );

        var element = UIATargetResolver.CreateFallbackFromContext(context);

        Assert.NotNull(element);
        Assert.Equal(string.Empty, element.Name);
        Assert.Equal("WindowControl", element.ControlType);
        Assert.Equal(string.Empty, element.AutomationId);
        Assert.Equal("testHost", element.ProcessName);
        Assert.Equal(7890, element.ProcessId);
        Assert.Equal("Test Application", element.WindowTitle);
        Assert.Equal(123456, element.WindowHandle);
        Assert.Equal(new BoundingBox(100, 100, 800, 600), element.BoundingRectangle);
        Assert.Equal("Win32", element.FrameworkId);
        Assert.False(element.IsPassword);
    }

    [Fact]
    public void UIAutomationService_DoesNotFabricateFakeAutomationIdOrName()
    {
        var context = new WindowContext(999, 111, "app", "App", new BoundingBox(0, 0, 100, 100), DateTime.UtcNow);
        var element = UIAutomationService.FallbackWin32Inspection(-100000, -100000, context);

        Assert.Equal(string.Empty, element.Name);
        Assert.Equal(string.Empty, element.AutomationId);
        Assert.DoesNotContain("Unknown", element.AutomationId);
        Assert.DoesNotContain("fake", element.AutomationId, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("auto", element.AutomationId, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
