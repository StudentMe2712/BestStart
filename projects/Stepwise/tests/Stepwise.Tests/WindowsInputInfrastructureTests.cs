using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Stepwise.WindowsIntegration.Automation;
using Stepwise.WindowsIntegration.Hooks;
using Stepwise.WindowsIntegration.Native;
using Stepwise.WindowsIntegration.Services;
using Xunit;

namespace Stepwise.Tests;

public class WindowsInputInfrastructureTests
{
    #region Mouse Hook Lifecycle Tests

    [Fact]
    public void LowLevelMouseHookService_StartStopStart_LifecycleWorksCorrectly()
    {
        using var mouseHook = new LowLevelMouseHookService();
        Assert.False(mouseHook.IsRunning);

        // Первый запуск
        mouseHook.Start();
        Assert.True(mouseHook.IsRunning);

        // Остановка
        mouseHook.Stop();
        Assert.False(mouseHook.IsRunning);

        // Повторный запуск (Start -> Stop -> Start)
        mouseHook.Start();
        Assert.True(mouseHook.IsRunning);

        mouseHook.Stop();
        Assert.False(mouseHook.IsRunning);
    }

    [Fact]
    public void LowLevelMouseHookService_DuplicateStart_DoesNotThrowOrDuplicate()
    {
        using var mouseHook = new LowLevelMouseHookService();

        mouseHook.Start();
        Assert.True(mouseHook.IsRunning);

        // Повторный вызов Start не должен вызывать исключений
        var ex = Record.Exception(() => mouseHook.Start());
        Assert.Null(ex);
        Assert.True(mouseHook.IsRunning);

        mouseHook.Stop();
        Assert.False(mouseHook.IsRunning);
    }

    [Fact]
    public void LowLevelMouseHookService_MultipleStop_IsIdempotent()
    {
        using var mouseHook = new LowLevelMouseHookService();

        var ex = Record.Exception(() =>
        {
            mouseHook.Stop();
            mouseHook.Stop();
        });
        Assert.Null(ex);
        Assert.False(mouseHook.IsRunning);
    }

    [Fact]
    public void LowLevelMouseHookService_StartAfterDispose_ThrowsObjectDisposedException()
    {
        var mouseHook = new LowLevelMouseHookService();
        mouseHook.Dispose();

        Assert.Throws<ObjectDisposedException>(() => mouseHook.Start());
    }

    [Fact]
    public void LowLevelMouseHookService_DisposeMultipleTimes_IsSafe()
    {
        var mouseHook = new LowLevelMouseHookService();
        mouseHook.Start();

        var ex = Record.Exception(() =>
        {
            mouseHook.Dispose();
            mouseHook.Dispose();
        });

        Assert.Null(ex);
        Assert.False(mouseHook.IsRunning);
    }

    #endregion

    #region Keyboard Hook Lifecycle Tests

    [Fact]
    public void LowLevelKeyboardHookService_StartStopStart_LifecycleWorksCorrectly()
    {
        using var keyboardHook = new LowLevelKeyboardHookService();
        Assert.False(keyboardHook.IsRunning);

        keyboardHook.Start();
        Assert.True(keyboardHook.IsRunning);

        keyboardHook.Stop();
        Assert.False(keyboardHook.IsRunning);

        // Повторный запуск (Start -> Stop -> Start)
        keyboardHook.Start();
        Assert.True(keyboardHook.IsRunning);

        keyboardHook.Stop();
        Assert.False(keyboardHook.IsRunning);
    }

    [Fact]
    public void LowLevelKeyboardHookService_DuplicateStart_DoesNotThrowOrDuplicate()
    {
        using var keyboardHook = new LowLevelKeyboardHookService();

        keyboardHook.Start();
        Assert.True(keyboardHook.IsRunning);

        var ex = Record.Exception(() => keyboardHook.Start());
        Assert.Null(ex);
        Assert.True(keyboardHook.IsRunning);

        keyboardHook.Stop();
        Assert.False(keyboardHook.IsRunning);
    }

    [Fact]
    public void LowLevelKeyboardHookService_MultipleStop_IsIdempotent()
    {
        using var keyboardHook = new LowLevelKeyboardHookService();

        var ex = Record.Exception(() =>
        {
            keyboardHook.Stop();
            keyboardHook.Stop();
        });
        Assert.Null(ex);
        Assert.False(keyboardHook.IsRunning);
    }

    [Fact]
    public void LowLevelKeyboardHookService_StartAfterDispose_ThrowsObjectDisposedException()
    {
        var keyboardHook = new LowLevelKeyboardHookService();
        keyboardHook.Dispose();

        Assert.Throws<ObjectDisposedException>(() => keyboardHook.Start());
    }

    [Fact]
    public void LowLevelKeyboardHookService_DisposeMultipleTimes_IsSafe()
    {
        var keyboardHook = new LowLevelKeyboardHookService();
        keyboardHook.Start();

        var ex = Record.Exception(() =>
        {
            keyboardHook.Dispose();
            keyboardHook.Dispose();
        });

        Assert.Null(ex);
        Assert.False(keyboardHook.IsRunning);
    }

    #endregion

    #region Active Window Tracker Tests

    [Fact]
    public void ActiveWindowTracker_StartStopStart_LifecycleWorksCorrectly()
    {
        using var tracker = new ActiveWindowTracker();
        Assert.False(tracker.IsRunning);

        tracker.Start();
        Assert.True(tracker.IsRunning);

        tracker.Stop();
        Assert.False(tracker.IsRunning);

        tracker.Start();
        Assert.True(tracker.IsRunning);

        tracker.Stop();
        Assert.False(tracker.IsRunning);
    }

    [Fact]
    public void ActiveWindowTracker_DuplicateStartAndStop_AreSafe()
    {
        using var tracker = new ActiveWindowTracker();

        tracker.Start();
        var exStart = Record.Exception(() => tracker.Start());
        Assert.Null(exStart);
        Assert.True(tracker.IsRunning);

        tracker.Stop();
        var exStop = Record.Exception(() => tracker.Stop());
        Assert.Null(exStop);
        Assert.False(tracker.IsRunning);
    }

    [Fact]
    public void ActiveWindowTracker_StartAfterDispose_ThrowsObjectDisposedException()
    {
        var tracker = new ActiveWindowTracker();
        tracker.Dispose();

        Assert.Throws<ObjectDisposedException>(() => tracker.Start());
    }

    [Fact]
    public void ActiveWindowTracker_GetActiveWindow_ReturnsCurrentWindowInfo()
    {
        using var tracker = new ActiveWindowTracker();

        var activeWindow = tracker.GetActiveWindow();

        // В среде Windows всегда есть активное окно (или рабочий стол/консоль/test host)
        if (activeWindow != null)
        {
            Assert.True(activeWindow.WindowHandle != 0);
            Assert.True(activeWindow.ProcessId > 0);
            Assert.False(string.IsNullOrEmpty(activeWindow.ProcessName));
        }
    }

    [Fact]
    public void ActiveWindowTracker_CaptureWindowInfo_InvalidHwnd_ReturnsNull()
    {
        var info = ActiveWindowTracker.CaptureWindowInfo(nint.Zero);
        Assert.Null(info);

        var infoInvalid = ActiveWindowTracker.CaptureWindowInfo(new nint(-1));
        Assert.Null(infoInvalid);
    }

    [Fact]
    public void ActiveWindowTracker_ConsecutiveEventsForSameWindow_AreSuppressed()
    {
        using var tracker = new ActiveWindowTracker();
        int eventCount = 0;
        ActiveWindowInfo? lastReceived = null;
        tracker.ActiveWindowChanged += (s, e) =>
        {
            Interlocked.Increment(ref eventCount);
            lastReceived = e;
        };

        var window1 = new ActiveWindowInfo(
            WindowHandle: 1001,
            ProcessId: 42,
            ProcessName: "notepad",
            WindowTitle: "Untitled - Notepad",
            Bounds: new BoundingBox(10, 10, 800, 600),
            Timestamp: DateTime.UtcNow
        );

        // Первый вызов должен пройти
        bool processed1 = tracker.TryProcessActiveWindow(window1);
        Assert.True(processed1);

        // Второй идентичный вызов должен быть подавлен
        bool processed2 = tracker.TryProcessActiveWindow(window1);
        Assert.False(processed2);

        // Третий вызов с другим заголовком должен пройти
        var window2 = window1 with { WindowTitle = "Document1 - Notepad" };
        bool processed3 = tracker.TryProcessActiveWindow(window2);
        Assert.True(processed3);

        // Повтор window2 должен быть подавлен
        bool processed4 = tracker.TryProcessActiveWindow(window2);
        Assert.False(processed4);

        // С другим Handle должен пройти
        var window3 = window2 with { WindowHandle = 2002 };
        bool processed5 = tracker.TryProcessActiveWindow(window3);
        Assert.True(processed5);

        // Проверяем, что Stop сбрасывает состояние дедупликации
        tracker.Stop();
        bool processed6 = tracker.TryProcessActiveWindow(window3);
        Assert.True(processed6);
    }

    #endregion

    #region Keyboard Translation & Modifier Tests

    [Fact]
    public void KeyboardHook_TranslateKey_LetterA_TranslatesCorrectly()
    {
        // VK_A = 0x41
        uint vkA = 0x41;
        uint scanCodeA = 0x1E;
        nint layout = NativeMethods.GetKeyboardLayout(0);

        var keyStateLower = new byte[256];
        var (charLower, isDeadLower) = LowLevelKeyboardHookService.TranslateKey(vkA, scanCodeA, keyStateLower, layout);

        Assert.False(isDeadLower);
        Assert.NotNull(charLower);
        Assert.True(charLower == "a" || charLower == "ф"); // Зависит от текущей локали (EN или RU)

        // Теперь нажимаем Shift
        var keyStateUpper = new byte[256];
        keyStateUpper[NativeMethods.VK_SHIFT] = 0x80;
        var (charUpper, isDeadUpper) = LowLevelKeyboardHookService.TranslateKey(vkA, scanCodeA, keyStateUpper, layout);

        Assert.False(isDeadUpper);
        Assert.NotNull(charUpper);
        Assert.True(charUpper == "A" || charUpper == "Ф");
    }

    [Fact]
    public void KeyboardHook_TranslateKey_CapsLock_TogglesLetterCase()
    {
        uint vkA = 0x41;
        uint scanCodeA = 0x1E;
        nint layout = NativeMethods.GetKeyboardLayout(0);

        var keyStateCaps = new byte[256];
        keyStateCaps[NativeMethods.VK_CAPITAL] = 0x01; // CapsLock ON

        var (charCaps, _) = LowLevelKeyboardHookService.TranslateKey(vkA, scanCodeA, keyStateCaps, layout);
        Assert.NotNull(charCaps);
        Assert.True(charCaps == "A" || charCaps == "Ф");
    }

    [Fact]
    public void RawKeyboardEvent_Shortcuts_ProperlyIdentified()
    {
        // Ctrl+C
        var ctrlC = new RawKeyboardEvent(
            EventType: RawKeyboardEventType.KeyDown,
            VirtualKey: 0x43, // 'C'
            ScanCode: 0x2E,
            Modifiers: KeyboardModifiers.Control,
            Character: "\x03",
            IsDeadKey: false,
            IsExtendedKey: false,
            Timestamp: DateTime.UtcNow
        );

        Assert.True(ctrlC.IsShortcut);
        Assert.True(ctrlC.HasControl);
        Assert.False(ctrlC.IsAltGr);
        Assert.False(ctrlC.IsTextInput);

        // Alt+Tab
        var altTab = new RawKeyboardEvent(
            EventType: RawKeyboardEventType.KeyDown,
            VirtualKey: 0x09, // TAB
            ScanCode: 0x0F,
            Modifiers: KeyboardModifiers.Alt,
            Character: null,
            IsDeadKey: false,
            IsExtendedKey: false,
            Timestamp: DateTime.UtcNow
        );

        Assert.True(altTab.IsShortcut);
        Assert.True(altTab.HasAlt);
        Assert.False(altTab.IsTextInput);

        // Ctrl+Shift+S
        var ctrlShiftS = new RawKeyboardEvent(
            EventType: RawKeyboardEventType.KeyDown,
            VirtualKey: 0x53, // 'S'
            ScanCode: 0x1F,
            Modifiers: KeyboardModifiers.Control | KeyboardModifiers.Shift,
            Character: "\x13",
            IsDeadKey: false,
            IsExtendedKey: false,
            Timestamp: DateTime.UtcNow
        );

        Assert.True(ctrlShiftS.IsShortcut);
        Assert.True(ctrlShiftS.HasControl);
        Assert.True(ctrlShiftS.HasShift);
        Assert.False(ctrlShiftS.IsTextInput);
    }

    [Fact]
    public void RawKeyboardEvent_TextInput_ProperlyIdentified()
    {
        // Обычный ввод буквы 'x'
        var textKey = new RawKeyboardEvent(
            EventType: RawKeyboardEventType.KeyDown,
            VirtualKey: 0x58, // 'X'
            ScanCode: 0x2D,
            Modifiers: KeyboardModifiers.None,
            Character: "x",
            IsDeadKey: false,
            IsExtendedKey: false,
            Timestamp: DateTime.UtcNow
        );

        Assert.False(textKey.IsShortcut);
        Assert.True(textKey.IsTextInput);
        Assert.Equal("x", textKey.Character);

        // AltGr ввод (например символ € через AltGr)
        var altGrKey = new RawKeyboardEvent(
            EventType: RawKeyboardEventType.KeyDown,
            VirtualKey: 0x45, // 'E'
            ScanCode: 0x12,
            Modifiers: KeyboardModifiers.Control | KeyboardModifiers.Alt | KeyboardModifiers.AltGr,
            Character: "€",
            IsDeadKey: false,
            IsExtendedKey: false,
            Timestamp: DateTime.UtcNow
        );

        Assert.True(altGrKey.IsAltGr);
        Assert.False(altGrKey.IsShortcut);
        Assert.True(altGrKey.IsTextInput);
        Assert.Equal("€", altGrKey.Character);
    }

    [Fact]
    public void RawKeyboardEvent_DeadKey_NotTextInput()
    {
        var deadKey = new RawKeyboardEvent(
            EventType: RawKeyboardEventType.KeyDown,
            VirtualKey: 0xDE,
            ScanCode: 0x28,
            Modifiers: KeyboardModifiers.None,
            Character: null,
            IsDeadKey: true,
            IsExtendedKey: false,
            Timestamp: DateTime.UtcNow
        );

        Assert.True(deadKey.IsDeadKey);
        Assert.False(deadKey.IsTextInput);
        Assert.False(deadKey.IsShortcut);
    }

    [Fact]
    public void RawKeyboardEvent_KeyUpAndExtended_PropertiesMatch()
    {
        var keyUp = new RawKeyboardEvent(
            EventType: RawKeyboardEventType.KeyUp,
            VirtualKey: 0x25, // Left Arrow
            ScanCode: 0x4B,
            Modifiers: KeyboardModifiers.None,
            Character: null,
            IsDeadKey: false,
            IsExtendedKey: true,
            Timestamp: DateTime.UtcNow
        );

        Assert.False(keyUp.IsKeyDown);
        Assert.True(keyUp.IsKeyUp);
        Assert.True(keyUp.IsExtendedKey);
        Assert.False(keyUp.IsTextInput);
    }

    [Fact]
    public void RawKeyboardEvent_KeyUp_HasNoTextInputAndNullCharacter()
    {
        // 1. Проверяем флаги RawKeyboardEvent напрямую: даже если передан символ, KeyUp не является вводом текста
        var keyUpWithChar = new RawKeyboardEvent(
            EventType: RawKeyboardEventType.KeyUp,
            VirtualKey: 0x41, // 'A'
            ScanCode: 0x1E,
            Modifiers: KeyboardModifiers.None,
            Character: "a",
            IsDeadKey: false,
            IsExtendedKey: false,
            Timestamp: DateTime.UtcNow
        );

        Assert.True(keyUpWithChar.IsKeyUp);
        Assert.False(keyUpWithChar.IsKeyDown);
        Assert.False(keyUpWithChar.IsTextInput);

        // 2. Проверяем низкоуровневую обработку хука: для KeyUp TranslateKey не вызывается, Character == null
        var hookStruct = new NativeMethods.KBDLLHOOKSTRUCT
        {
            VkCode = 0x41,
            ScanCode = 0x1E,
            Flags = 0,
            Time = 0,
            DwExtraInfo = 0
        };

        var keyUpEvent = LowLevelKeyboardHookService.ProcessKeyboardHookData(RawKeyboardEventType.KeyUp, hookStruct);
        Assert.True(keyUpEvent.IsKeyUp);
        Assert.Null(keyUpEvent.Character);
        Assert.False(keyUpEvent.IsTextInput);

        // Для сравнения, KeyDown должен производить ввод текста
        var keyDownEvent = LowLevelKeyboardHookService.ProcessKeyboardHookData(RawKeyboardEventType.KeyDown, hookStruct);
        Assert.True(keyDownEvent.IsKeyDown);
        Assert.NotNull(keyDownEvent.Character);
        Assert.True(keyDownEvent.IsTextInput);
    }

    [Fact]
    public void RawKeyboardEvent_CtrlAltS_ClassifiedAsShortcutAndNotAltGr()
    {
        // Ctrl+Alt+S (Left Ctrl + Left Alt + S)
        var ctrlAltS = new RawKeyboardEvent(
            EventType: RawKeyboardEventType.KeyDown,
            VirtualKey: 0x53, // 'S'
            ScanCode: 0x1F,
            Modifiers: KeyboardModifiers.Control | KeyboardModifiers.Alt,
            Character: null,
            IsDeadKey: false,
            IsExtendedKey: false,
            Timestamp: DateTime.UtcNow
        );

        Assert.True(ctrlAltS.IsShortcut);
        Assert.False(ctrlAltS.IsAltGr);
        Assert.True(ctrlAltS.HasControl);
        Assert.True(ctrlAltS.HasAlt);
        Assert.False(ctrlAltS.IsTextInput);
    }

    [Fact]
    public void RawKeyboardEvent_AltGr_ClassifiedAsAltGrAndNotShortcut()
    {
        // Нажатие правой клавиши Alt (AltGr) с буквой Q (например '@' на немецкой раскладке)
        var altGrAt = new RawKeyboardEvent(
            EventType: RawKeyboardEventType.KeyDown,
            VirtualKey: 0x51, // 'Q'
            ScanCode: 0x10,
            Modifiers: KeyboardModifiers.Control | KeyboardModifiers.Alt | KeyboardModifiers.AltGr,
            Character: "@",
            IsDeadKey: false,
            IsExtendedKey: false,
            Timestamp: DateTime.UtcNow
        );

        Assert.True(altGrAt.IsAltGr);
        Assert.False(altGrAt.IsShortcut);
        Assert.True(altGrAt.IsTextInput);
        Assert.Equal("@", altGrAt.Character);
    }

    #endregion

    #region UI Automation IsPassword Detection Tests

    [Fact]
    public void ElementInfo_IsPassword_DefaultAndCustomValue_WorksCorrectly()
    {
        var defaultElement = new ElementInfo(
            Name: "Имя пользователя",
            ControlType: "Edit",
            AutomationId: "txtUser",
            ClassName: "TextBox",
            ProcessName: "app",
            ProcessId: 100,
            WindowTitle: "Вход",
            WindowHandle: 12345,
            BoundingRectangle: new BoundingBox(10, 10, 100, 30)
        );

        Assert.False(defaultElement.IsPassword);
        Assert.Equal("Unknown", defaultElement.FrameworkId);

        var passwordElement = defaultElement with { IsPassword = true };
        Assert.True(passwordElement.IsPassword);
        Assert.Equal("txtUser", passwordElement.AutomationId);
    }

    [Fact]
    public void UIAutomationService_InspectElementAtInvalidCoordinates_ReturnsIsPasswordFalse()
    {
        var service = new UIAutomationService();
        var element = service.InspectElementAt(-99999, -99999);

        Assert.NotNull(element);
        Assert.False(element.IsPassword);
    }

    #endregion

    #region InputMonitoringService Tests

    [Fact]
    public void InputMonitoringService_AggregatesMouseAndKeyboardHooks()
    {
        var mockMouse = new MockMouseHook();
        var mockKeyboard = new MockKeyboardHook();

        using var monitor = new InputMonitoringService(mockMouse, mockKeyboard);
        Assert.False(monitor.IsRunning);

        // Старт запускает оба хука
        monitor.Start();
        Assert.True(monitor.IsRunning);
        Assert.True(mockMouse.IsRunning);
        Assert.True(mockKeyboard.IsRunning);

        // Проверяем проброс событий мыши
        RawMouseEvent? receivedMouseEvent = null;
        monitor.MouseEventReceived += (s, e) => receivedMouseEvent = e;

        var sampleMouseEvent = new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 100, 200, 0, DateTime.UtcNow);
        mockMouse.TriggerRawEvent(sampleMouseEvent);

        Assert.NotNull(receivedMouseEvent);
        Assert.Equal(RawMouseEventType.MouseDown, receivedMouseEvent.Value.EventType);
        Assert.Equal(100, receivedMouseEvent.Value.X);
        Assert.Equal(200, receivedMouseEvent.Value.Y);

        // Проверяем проброс событий клавиатуры
        RawKeyboardEvent? receivedKeyboardEvent = null;
        monitor.KeyboardEventReceived += (s, e) => receivedKeyboardEvent = e;

        var sampleKeyboardEvent = new RawKeyboardEvent(
            RawKeyboardEventType.KeyDown,
            0x41,
            0x1E,
            KeyboardModifiers.Shift,
            "A",
            false,
            false,
            DateTime.UtcNow
        );
        mockKeyboard.TriggerKeyboardEvent(sampleKeyboardEvent);

        Assert.NotNull(receivedKeyboardEvent);
        Assert.Equal(0x41, receivedKeyboardEvent.Value.VirtualKey);
        Assert.Equal("A", receivedKeyboardEvent.Value.Character);
        Assert.True(receivedKeyboardEvent.Value.HasShift);

        // Остановка останавливает оба хука
        monitor.Stop();
        Assert.False(monitor.IsRunning);
        Assert.False(mockMouse.IsRunning);
        Assert.False(mockKeyboard.IsRunning);
    }

    [Fact]
    public void InputMonitoringService_NativeHooks_LifecycleStartsAndStops()
    {
        using var monitor = new InputMonitoringService();
        Assert.False(monitor.IsRunning);

        monitor.Start();
        Assert.True(monitor.IsRunning);

        monitor.Stop();
        Assert.False(monitor.IsRunning);
    }

    [Fact]
    public void InputMonitoringService_KeyboardStartFails_StopsMouseHookAndRethrows()
    {
        var mockMouse = new MockMouseHook();
        var mockKeyboard = new FailingStartKeyboardHook();

        using var monitor = new InputMonitoringService(mockMouse, mockKeyboard);

        Assert.Throws<InvalidOperationException>(() => monitor.Start());

        // Мышиный хук должен быть гарантированно остановлен при откате
        Assert.False(mockMouse.IsRunning);
        Assert.False(monitor.IsRunning);
    }

    #endregion

    #region Mock Helpers

    private sealed class FailingStartKeyboardHook : IKeyboardHookService
    {
        public event EventHandler<RawKeyboardEvent>? KeyboardEventReceived
        {
            add { }
            remove { }
        }
        public bool IsRunning => false;
        public void Start() => throw new InvalidOperationException("Hook start failed");
        public void Stop() { }
        public void Dispose() { }
    }

    private sealed class MockMouseHook : IMouseHookService
    {
        public event EventHandler<MouseClickEvent>? MouseClicked
        {
            add { }
            remove { }
        }
        public event EventHandler<RawMouseEvent>? RawMouseEventReceived;

        public bool IsRunning { get; private set; }

        public void Start() => IsRunning = true;
        public void Stop() => IsRunning = false;
        public void Dispose() => Stop();

        public void TriggerRawEvent(RawMouseEvent ev)
        {
            RawMouseEventReceived?.Invoke(this, ev);
        }
    }

    private sealed class MockKeyboardHook : IKeyboardHookService
    {
        public event EventHandler<RawKeyboardEvent>? KeyboardEventReceived;

        public bool IsRunning { get; private set; }

        public void Start() => IsRunning = true;
        public void Stop() => IsRunning = false;
        public void Dispose() => Stop();

        public void TriggerKeyboardEvent(RawKeyboardEvent ev)
        {
            KeyboardEventReceived?.Invoke(this, ev);
        }
    }

    #endregion
}
