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

        Assert.Empty(emitted);
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
}
