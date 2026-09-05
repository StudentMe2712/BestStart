using System.Diagnostics;
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
using Xunit.Abstractions;

namespace Stepwise.Tests;

/// <summary>
/// Полный комплексный набор тестов для Этапа 2 (Stage 2):
/// 1. Модульные тесты EventCorrelator
/// 2. Модульные тесты конечного автомата состояний RecordingSessionStateMachine и жизненного цикла RecordingEngine
/// 3. Модульные тесты политики записи RecordingPolicy
/// 4. Тесты инвариантов и свойств конвейера
/// 5. Сквозной интеграционный тест конвейера с SQLite
/// 6. Нагрузочный тест производительности и измерение задержек
/// 7. Тесты обработки сбоев и отказоустойчивости
/// </summary>
public sealed class Stage2ComprehensiveTests
{
    private readonly ITestOutputHelper _output;

    public Stage2ComprehensiveTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region 1. EventCorrelator Unit Tests

    [Fact]
    public void EventCorrelator_MouseDownAndMouseUp_EmitsLeftClick()
    {
        using var correlator = new EventCorrelator();
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, action) => emitted.Add(action);

        var context = new WindowContext(100, 200, "app", "Main Window", new BoundingBox(0, 0, 500, 500), DateTime.UtcNow);
        var t0 = DateTime.UtcNow;

        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 150, 250, 0, t0), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 150, 250, 0, t0.AddMilliseconds(30)), context);

        Assert.Single(emitted);
        var click = emitted[0];
        Assert.Equal(SemanticActionType.LeftClick, click.ActionType);
        Assert.Equal(150, click.X);
        Assert.Equal(250, click.Y);
        Assert.Equal(1, click.SequenceIndex);
        Assert.True(click.IsMouseAction);
        Assert.False(click.IsKeyboardAction);
        Assert.Equal(ActionType.LeftClick, click.ToStepActionType());
        Assert.Equal("app", click.Context.ProcessName);
        Assert.Equal(200, click.Context.ProcessId);
    }

    [Fact]
    public void EventCorrelator_RightDownAndRightUp_EmitsRightClick()
    {
        using var correlator = new EventCorrelator();
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, action) => emitted.Add(action);

        var t0 = DateTime.UtcNow;
        var context = new WindowContext(101, 201, "explorer", "Files", new BoundingBox(0, 0, 600, 400), t0);

        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Right, 80, 95, 0, t0), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Right, 80, 95, 0, t0.AddMilliseconds(40)), context);

        Assert.Single(emitted);
        var action = emitted[0];
        Assert.Equal(SemanticActionType.RightClick, action.ActionType);
        Assert.Equal(80, action.X);
        Assert.Equal(95, action.Y);
        Assert.Equal(1, action.SequenceIndex);
        Assert.True(action.IsMouseAction);
        Assert.Equal(ActionType.RightClick, action.ToStepActionType());
    }

    [Fact]
    public void EventCorrelator_DoubleClick_WithinTimeAndThresholds_EmitsDoubleLeftClick()
    {
        var metrics = new Mock<ISystemMetricsProvider>();
        metrics.SetupGet(m => m.DoubleClickTimeMs).Returns(500);
        metrics.SetupGet(m => m.DoubleClickWidth).Returns(4);
        metrics.SetupGet(m => m.DoubleClickHeight).Returns(4);

        using var correlator = new EventCorrelator(metrics.Object);
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, action) => emitted.Add(action);

        var t0 = DateTime.UtcNow;
        var context = WindowContext.Empty;

        // Первый левый клик
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 100, 100, 0, t0), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 100, 100, 0, t0.AddMilliseconds(20)), context);

        // Второй левый клик через 200 мс в пределах 2 пикселей
        var t1 = t0.AddMilliseconds(200);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 102, 101, 0, t1), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 102, 101, 0, t1.AddMilliseconds(20)), context);

        Assert.Equal(2, emitted.Count);
        Assert.Equal(SemanticActionType.LeftClick, emitted[0].ActionType);
        Assert.Equal(1, emitted[0].SequenceIndex);
        Assert.Equal(SemanticActionType.DoubleLeftClick, emitted[1].ActionType);
        Assert.Equal(2, emitted[1].SequenceIndex);
        Assert.Equal(ActionType.DoubleLeftClick, emitted[1].ToStepActionType());

        // Третий клик сразу же после двойного — должен быть одиночным LeftClick
        var t2 = t1.AddMilliseconds(100);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 102, 101, 0, t2), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 102, 101, 0, t2.AddMilliseconds(20)), context);

        Assert.Equal(3, emitted.Count);
        Assert.Equal(SemanticActionType.LeftClick, emitted[2].ActionType);
        Assert.Equal(3, emitted[2].SequenceIndex);
    }

    [Fact]
    public void EventCorrelator_DoubleClick_ExceedingDistanceOrTime_EmitsTwoSingleClicks()
    {
        var metrics = new Mock<ISystemMetricsProvider>();
        metrics.SetupGet(m => m.DoubleClickTimeMs).Returns(400);
        metrics.SetupGet(m => m.DoubleClickWidth).Returns(4);
        metrics.SetupGet(m => m.DoubleClickHeight).Returns(4);

        using var correlator = new EventCorrelator(metrics.Object);
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, action) => emitted.Add(action);

        var t0 = DateTime.UtcNow;

        // Клик 1
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 100, 100, 0, t0));
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 100, 100, 0, t0.AddMilliseconds(20)));

        // Клик 2: расстояние dx = 20 (> 4)
        var t1 = t0.AddMilliseconds(100);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 120, 100, 0, t1));
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 120, 100, 0, t1.AddMilliseconds(20)));

        Assert.Equal(2, emitted.Count);
        Assert.Equal(SemanticActionType.LeftClick, emitted[0].ActionType);
        Assert.Equal(SemanticActionType.LeftClick, emitted[1].ActionType);
    }

    [Fact]
    public void EventCorrelator_MiddleDownAndMiddleUp_EmitsMiddleClick()
    {
        using var correlator = new EventCorrelator();
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, action) => emitted.Add(action);

        var t0 = DateTime.UtcNow;
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Middle, 300, 400, 0, t0));
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Middle, 300, 400, 0, t0.AddMilliseconds(30)));

        Assert.Single(emitted);
        var click = emitted[0];
        Assert.Equal(SemanticActionType.MiddleClick, click.ActionType);
        Assert.Equal(300, click.X);
        Assert.Equal(400, click.Y);
        Assert.Equal(ActionType.MiddleClick, click.ToStepActionType());
    }

    [Fact]
    public void EventCorrelator_TextInputGrouping_BuffersCharactersAndFlushes()
    {
        using var correlator = new EventCorrelator();
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, action) => emitted.Add(action);

        var now = DateTime.UtcNow;
        var context = new WindowContext(500, 600, "notepad", "Untitled", new BoundingBox(0, 0, 800, 600), now);

        string characters = "Hello";
        for (int i = 0; i < characters.Length; i++)
        {
            var keyEvent = new RawKeyboardEvent(
                EventType: RawKeyboardEventType.KeyDown,
                VirtualKey: characters[i],
                ScanCode: 0,
                Modifiers: char.IsUpper(characters[i]) ? KeyboardModifiers.Shift : KeyboardModifiers.None,
                Character: characters[i].ToString(),
                IsDeadKey: false,
                IsExtendedKey: false,
                Timestamp: now.AddMilliseconds(i * 30)
            );
            correlator.ProcessKeyboardEvent(keyEvent, context);
        }

        // До явного сброса или таймера действия еще нет
        Assert.Empty(emitted);

        // Явный сброс
        correlator.FlushPending();

        Assert.Single(emitted);
        var action = emitted[0];
        Assert.Equal(SemanticActionType.TextInput, action.ActionType);
        Assert.Equal("Hello", action.Text);
        Assert.Equal(5, action.CharacterCount);
        Assert.Equal(ActionType.TextInput, action.ToStepActionType());
        Assert.Equal("notepad", action.Context.ProcessName);
        Assert.Equal(now, action.StartedAt);
        Assert.Equal(now.AddMilliseconds(4 * 30), action.CompletedAt);
    }

    [Fact]
    public async Task EventCorrelator_TextInput_InactivityTimerFlush_EmitsAutomatically()
    {
        using var correlator = new EventCorrelator(flushTimeoutMs: 50);
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, action) => emitted.Add(action);

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

        // Ждем срабатывания таймера неактивности
        await Task.Delay(150);

        Assert.Single(emitted);
        var action = emitted[0];
        Assert.Equal(SemanticActionType.TextInput, action.ActionType);
        Assert.Equal("A", action.Text);
    }

    [Fact]
    public void EventCorrelator_ShortcutDetection_CtrlS_EmitsShortcutNotTextInput()
    {
        using var correlator = new EventCorrelator();
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, action) => emitted.Add(action);

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
        Assert.True(action.IsKeyboardAction);
        Assert.False(action.IsMouseAction);
        Assert.Equal(ActionType.KeyPress, action.ToStepActionType());
    }

    [Theory]
    [InlineData(13, "Enter")]
    [InlineData(27, "Escape")]
    [InlineData(9, "Tab")]
    public void EventCorrelator_EnterEscapeTab_EmitsKeyPressAction(int vk, string expectedKeyName)
    {
        using var correlator = new EventCorrelator();
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, action) => emitted.Add(action);

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
        Assert.Equal(ActionType.KeyPress, action.ToStepActionType());
    }

    [Fact]
    public void EventCorrelator_WindowContextPropagation_PropagatesToAllSemanticActions()
    {
        using var correlator = new EventCorrelator();
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, action) => emitted.Add(action);

        var now = DateTime.UtcNow;
        var context = new WindowContext(
            WindowHandle: 98765,
            ProcessId: 4321,
            ProcessName: "chrome.exe",
            WindowTitle: "Google Chrome",
            Bounds: new BoundingBox(10, 20, 1200, 800),
            Timestamp: now
        );

        // 1. Mouse click
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 100, 200, 0, now), context);
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 100, 200, 0, now.AddMilliseconds(20)), context);

        // 2. Text input
        var textKey = new RawKeyboardEvent(RawKeyboardEventType.KeyDown, 65, 0, KeyboardModifiers.None, "A", false, false, now.AddMilliseconds(50));
        correlator.ProcessKeyboardEvent(textKey, context);
        correlator.FlushPending();

        // 3. Enter key
        var enterKey = new RawKeyboardEvent(RawKeyboardEventType.KeyDown, 13, 0, KeyboardModifiers.None, null, false, false, now.AddMilliseconds(100));
        correlator.ProcessKeyboardEvent(enterKey, context);

        // 4. Shortcut Ctrl+S
        var shortcutKey = new RawKeyboardEvent(RawKeyboardEventType.KeyDown, 83, 0, KeyboardModifiers.Control, null, false, false, now.AddMilliseconds(150));
        correlator.ProcessKeyboardEvent(shortcutKey, context);

        Assert.Equal(4, emitted.Count);
        foreach (var action in emitted)
        {
            Assert.Equal(98765, action.Context.WindowHandle);
            Assert.Equal(4321, action.Context.ProcessId);
            Assert.Equal("chrome.exe", action.Context.ProcessName);
            Assert.Equal("Google Chrome", action.Context.WindowTitle);
            Assert.Equal(1200, action.Context.Bounds.Width);
            Assert.Equal(800, action.Context.Bounds.Height);
        }
    }

    #endregion

    #region 2. RecordingSession State Machine Unit Tests

    [Fact]
    public void StateMachine_AllValidTransitions_Succeed()
    {
        var sm = new RecordingSessionStateMachine();
        var history = new List<(RecordingSessionState Old, RecordingSessionState New)>();
        sm.StateChanged += (o, n) => history.Add((o, n));

        // 1. Idle -> Recording
        Assert.True(sm.CanTransitionTo(RecordingSessionState.Recording));
        Assert.True(sm.TryTransition(RecordingSessionState.Recording, out var err1));
        Assert.Null(err1);
        Assert.True(sm.IsRecording);

        // 2. Recording -> Paused
        Assert.True(sm.CanTransitionTo(RecordingSessionState.Paused));
        sm.Transition(RecordingSessionState.Paused);
        Assert.True(sm.IsPaused);

        // 3. Paused -> Recording
        Assert.True(sm.CanTransitionTo(RecordingSessionState.Recording));
        sm.Transition(RecordingSessionState.Recording);
        Assert.True(sm.IsRecording);

        // 4. Recording -> Stopping
        Assert.True(sm.CanTransitionTo(RecordingSessionState.Stopping));
        sm.Transition(RecordingSessionState.Stopping);
        Assert.True(sm.IsStopping);

        // 5. Stopping -> Completed
        Assert.True(sm.CanTransitionTo(RecordingSessionState.Completed));
        sm.Transition(RecordingSessionState.Completed);
        Assert.True(sm.IsCompleted);

        // 6. Completed -> Idle (ResetToIdle)
        sm.ResetToIdle();
        Assert.True(sm.IsIdle);

        // 7. Paused -> Stopping -> Failed path
        sm.Transition(RecordingSessionState.Recording);
        sm.Transition(RecordingSessionState.Paused);
        Assert.True(sm.CanTransitionTo(RecordingSessionState.Stopping));
        sm.Transition(RecordingSessionState.Stopping);
        Assert.True(sm.CanTransitionTo(RecordingSessionState.Failed));
        sm.Transition(RecordingSessionState.Failed);
        Assert.True(sm.IsFailed);

        // 8. Failed -> Idle (ResetToIdle)
        sm.ResetToIdle();
        Assert.True(sm.IsIdle);

        Assert.Equal(11, history.Count);
        Assert.Equal((RecordingSessionState.Idle, RecordingSessionState.Recording), history[0]);
        Assert.Equal((RecordingSessionState.Recording, RecordingSessionState.Paused), history[1]);
        Assert.Equal((RecordingSessionState.Paused, RecordingSessionState.Recording), history[2]);
        Assert.Equal((RecordingSessionState.Recording, RecordingSessionState.Stopping), history[3]);
        Assert.Equal((RecordingSessionState.Stopping, RecordingSessionState.Completed), history[4]);
        Assert.Equal((RecordingSessionState.Completed, RecordingSessionState.Idle), history[5]);
        Assert.Equal((RecordingSessionState.Idle, RecordingSessionState.Recording), history[6]);
        Assert.Equal((RecordingSessionState.Recording, RecordingSessionState.Paused), history[7]);
        Assert.Equal((RecordingSessionState.Paused, RecordingSessionState.Stopping), history[8]);
        Assert.Equal((RecordingSessionState.Stopping, RecordingSessionState.Failed), history[9]);
        Assert.Equal((RecordingSessionState.Failed, RecordingSessionState.Idle), history[10]);
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
    [InlineData(RecordingSessionState.Paused, RecordingSessionState.Failed)]
    [InlineData(RecordingSessionState.Stopping, RecordingSessionState.Stopping)]
    [InlineData(RecordingSessionState.Stopping, RecordingSessionState.Recording)]
    [InlineData(RecordingSessionState.Stopping, RecordingSessionState.Paused)]
    [InlineData(RecordingSessionState.Completed, RecordingSessionState.Recording)]
    [InlineData(RecordingSessionState.Completed, RecordingSessionState.Stopping)]
    [InlineData(RecordingSessionState.Completed, RecordingSessionState.Paused)]
    [InlineData(RecordingSessionState.Failed, RecordingSessionState.Recording)]
    [InlineData(RecordingSessionState.Failed, RecordingSessionState.Stopping)]
    [InlineData(RecordingSessionState.Failed, RecordingSessionState.Paused)]
    public void StateMachine_InvalidTransitions_ThrowOrReturnFalse(RecordingSessionState from, RecordingSessionState to)
    {
        var sm = new RecordingSessionStateMachine();

        // Приводим автомат в состояние 'from'
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
    public void StateMachine_StartTwice_ThrowsOrReturnsFalse()
    {
        var sm = new RecordingSessionStateMachine();
        sm.Transition(RecordingSessionState.Recording);

        Assert.False(sm.CanTransitionTo(RecordingSessionState.Recording));
        Assert.False(sm.TryTransition(RecordingSessionState.Recording, out var err));
        Assert.NotNull(err);
        Assert.Throws<InvalidOperationException>(() => sm.Transition(RecordingSessionState.Recording));
    }

    [Fact]
    public void RecordingEngine_StartTwice_SafelyIgnored()
    {
        var inputMonitor = new Mock<IInputMonitoringService>();
        var correlator = new EventCorrelator();
        var targetResolver = new Mock<ITargetResolver>();
        var capture = new Mock<ICaptureCoordinator>();

        using var engine = new RecordingEngine(
            inputMonitor.Object,
            null,
            correlator,
            targetResolver.Object,
            new DefaultRecordingPolicy(),
            new StepDetector(),
            capture.Object);

        engine.StartRecording();
        Assert.Equal(RecordingSessionState.Recording, engine.State);

        // Повторный вызов StartRecording не должен выбрасывать исключений и нарушать состояние
        engine.StartRecording();
        Assert.Equal(RecordingSessionState.Recording, engine.State);

        inputMonitor.Verify(m => m.Start(), Times.Once);
    }

    [Fact]
    public async Task StateMachine_And_Engine_StopTwice_HandledGracefully()
    {
        var sm = new RecordingSessionStateMachine();
        sm.Transition(RecordingSessionState.Recording);
        sm.Transition(RecordingSessionState.Stopping);
        sm.Transition(RecordingSessionState.Completed);

        // State machine rejects stopping from completed
        Assert.False(sm.CanTransitionTo(RecordingSessionState.Stopping));
        Assert.Throws<InvalidOperationException>(() => sm.Transition(RecordingSessionState.Stopping));

        // RecordingEngine handles stop twice safely
        var inputMonitor = new Mock<IInputMonitoringService>();
        var correlator = new EventCorrelator();
        var targetResolver = new Mock<ITargetResolver>();
        var capture = new Mock<ICaptureCoordinator>();

        using var engine = new RecordingEngine(
            inputMonitor.Object,
            null,
            correlator,
            targetResolver.Object,
            new DefaultRecordingPolicy(),
            new StepDetector(),
            capture.Object);

        engine.StartRecording();
        await engine.StopRecordingAsync();
        Assert.Equal(RecordingSessionState.Completed, engine.State);

        // Второе закрытие сессии — no-op
        await engine.StopRecordingAsync();
        Assert.Equal(RecordingSessionState.Completed, engine.State);
    }

    [Fact]
    public void StateMachine_And_Engine_PauseTwice_HandledGracefully()
    {
        var sm = new RecordingSessionStateMachine();
        sm.Transition(RecordingSessionState.Recording);
        sm.Transition(RecordingSessionState.Paused);

        Assert.False(sm.CanTransitionTo(RecordingSessionState.Paused));
        Assert.Throws<InvalidOperationException>(() => sm.Transition(RecordingSessionState.Paused));

        var inputMonitor = new Mock<IInputMonitoringService>();
        var correlator = new EventCorrelator();
        var targetResolver = new Mock<ITargetResolver>();
        var capture = new Mock<ICaptureCoordinator>();

        using var engine = new RecordingEngine(
            inputMonitor.Object,
            null,
            correlator,
            targetResolver.Object,
            new DefaultRecordingPolicy(),
            new StepDetector(),
            capture.Object);

        engine.StartRecording();
        engine.PauseRecording();
        Assert.Equal(RecordingSessionState.Paused, engine.State);

        // Повторная пауза в движке — безопасный no-op
        engine.PauseRecording();
        Assert.Equal(RecordingSessionState.Paused, engine.State);
    }

    [Fact]
    public void StateMachine_And_Engine_ResumeWithoutPause_HandledGracefully()
    {
        var sm = new RecordingSessionStateMachine();
        sm.Transition(RecordingSessionState.Recording);

        // Нельзя перейти из Recording в Recording (Resume без Pause)
        Assert.False(sm.CanTransitionTo(RecordingSessionState.Recording));
        Assert.Throws<InvalidOperationException>(() => sm.Transition(RecordingSessionState.Recording));

        var inputMonitor = new Mock<IInputMonitoringService>();
        var correlator = new EventCorrelator();
        var targetResolver = new Mock<ITargetResolver>();
        var capture = new Mock<ICaptureCoordinator>();

        using var engine = new RecordingEngine(
            inputMonitor.Object,
            null,
            correlator,
            targetResolver.Object,
            new DefaultRecordingPolicy(),
            new StepDetector(),
            capture.Object);

        engine.StartRecording();
        // Движок безопасно игнорирует ResumeRecording, если сессия не на паузе
        engine.ResumeRecording();
        Assert.Equal(RecordingSessionState.Recording, engine.State);
    }

    [Fact]
    public void RecordingEngine_DisposeSafety_CanDisposeMultipleTimesAndWhileRecording()
    {
        var inputMonitor = new Mock<IInputMonitoringService>();
        var correlator = new EventCorrelator();
        var targetResolver = new Mock<ITargetResolver>();
        var capture = new Mock<ICaptureCoordinator>();

        var engine = new RecordingEngine(
            inputMonitor.Object,
            null,
            correlator,
            targetResolver.Object,
            new DefaultRecordingPolicy(),
            new StepDetector(),
            capture.Object);

        engine.StartRecording();
        Assert.True(engine.IsRecording);

        // Dispose во время активной записи
        engine.Dispose();
        Assert.False(engine.IsRecording);

        // Повторный Dispose безопасен
        engine.Dispose();
        engine.Dispose();

        // Попытка вызвать методы после Dispose выбрасывает ObjectDisposedException
        Assert.Throws<ObjectDisposedException>(() => engine.StartRecording());
        Assert.Throws<ObjectDisposedException>(() => engine.PauseRecording());
        Assert.Throws<ObjectDisposedException>(() => engine.ResumeRecording());
    }

    #endregion

    #region 3. RecordingPolicy Unit Tests

    [Fact]
    public void RecordingPolicy_NormalField_EvaluatesToAllow()
    {
        var policy = new DefaultRecordingPolicy();
        var context = new WindowContext(1, 10, "notepad", "Notes", new BoundingBox(0, 0, 100, 100), DateTime.UtcNow);
        var action = SemanticAction.CreateMouseClick(SemanticActionType.LeftClick, 50, 50, context, DateTime.UtcNow);
        var target = new ElementInfo("Save", "Button", "btnSave", "Button", "notepad", 10, "Notes", 1, new BoundingBox(40, 40, 50, 20));

        var decision = policy.Evaluate(action, target);

        Assert.Equal(RecordingPolicyDecision.Allow, decision);
    }

    [Fact]
    public void RecordingPolicy_PasswordField_TextInput_EvaluatesToSuppressAndHidesText()
    {
        var policy = new DefaultRecordingPolicy(maskSensitiveInputs: false);
        var context = WindowContext.Empty;
        var textAction = SemanticAction.CreateTextInput("SecretPassword123", context, DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow);
        var pwdTarget = new ElementInfo("PasswordBox", "Edit", "txtPwd", "PasswordBox", "authApp", 10, "Login", 1, new BoundingBox(0, 0, 100, 30), IsPassword: true);

        var decision = policy.Evaluate(textAction, pwdTarget);

        Assert.Equal(RecordingPolicyDecision.Suppress, decision);

        // StepDetector при Suppress возвращает null — значение пароля не сохраняется
        var detector = new StepDetector();
        var step = detector.DetectStep(textAction, pwdTarget, decision, 1);
        Assert.Null(step);
    }

    [Fact]
    public void RecordingPolicy_PasswordField_TextInputWithMasking_EvaluatesToMask()
    {
        var policy = new DefaultRecordingPolicy(maskSensitiveInputs: true);
        var context = WindowContext.Empty;
        var textAction = SemanticAction.CreateTextInput("SecretPassword123", context, DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow);
        var pwdTarget = new ElementInfo("PasswordBox", "Edit", "txtPwd", "PasswordBox", "authApp", 10, "Login", 1, new BoundingBox(0, 0, 100, 30), IsPassword: true);

        var decision = policy.Evaluate(textAction, pwdTarget);

        Assert.Equal(RecordingPolicyDecision.Mask, decision);

        var detector = new StepDetector();
        var step = detector.DetectStep(textAction, pwdTarget, decision, 1);
        Assert.NotNull(step);
        Assert.Equal("Type text into \"PasswordBox\"", step.Title);
        Assert.Equal("Type sensitive text into PasswordBox.", step.Description);
        Assert.DoesNotContain("SecretPassword123", step.Title);
        Assert.DoesNotContain("SecretPassword123", step.Description);
        Assert.Equal("true", step.Metadata?["IsMasked"]);
    }

    [Fact]
    public void RecordingPolicy_PasswordField_Click_EvaluatesToMask()
    {
        var policy = new DefaultRecordingPolicy();
        var click = SemanticAction.CreateMouseClick(SemanticActionType.LeftClick, 10, 10, WindowContext.Empty, DateTime.UtcNow);
        var pwdTarget = new ElementInfo("Password", "Edit", "pwd", "PasswordBox", "login", 1, "Login", 1, new BoundingBox(0, 0, 50, 20), IsPassword: true);

        var decision = policy.Evaluate(click, pwdTarget);

        Assert.Equal(RecordingPolicyDecision.Mask, decision);
    }

    [Theory]
    [InlineData("keepass", "keepass")]
    [InlineData("keepass", "KEEPASS")]
    [InlineData("keepass.exe", "keepass")]
    [InlineData("keepass", "keepass.exe")]
    [InlineData("1password.exe", "1password.exe")]
    [InlineData("1password", "1password.exe")]
    public void RecordingPolicy_ProcessExclusion_BlacklistedProcess_EvaluatesToSuppress(string configuredExclusion, string runtimeProcess)
    {
        var policy = new DefaultRecordingPolicy(new[] { configuredExclusion });
        var context = new WindowContext(1, 10, runtimeProcess, "Protected App", new BoundingBox(0, 0, 100, 100), DateTime.UtcNow);
        var action = SemanticAction.CreateMouseClick(SemanticActionType.LeftClick, 10, 10, context, DateTime.UtcNow);
        var target = new ElementInfo("Button", "Button", "btn", "Button", runtimeProcess, 10, "Protected App", 1, new BoundingBox(0, 0, 50, 20));

        var decision = policy.Evaluate(action, target);

        Assert.Equal(RecordingPolicyDecision.Suppress, decision);
    }

    [Fact]
    public void RecordingPolicy_ProcessExclusion_AddAndRemove_UpdatesCorrectly()
    {
        var policy = new DefaultRecordingPolicy();
        var target = new ElementInfo("OK", "Button", "btnOK", "Button", "customApp", 100, "App", 1, new BoundingBox(0, 0, 10, 10));
        var action = SemanticAction.CreateMouseClick(SemanticActionType.LeftClick, 5, 5, WindowContext.Empty, DateTime.UtcNow);

        Assert.Equal(RecordingPolicyDecision.Allow, policy.Evaluate(action, target));

        policy.AddExcludedProcess("customApp");
        Assert.Equal(RecordingPolicyDecision.Suppress, policy.Evaluate(action, target));

        policy.RemoveExcludedProcess("customApp");
        Assert.Equal(RecordingPolicyDecision.Allow, policy.Evaluate(action, target));
    }

    #endregion

    #region 4. Property & Invariant Tests

    [Fact]
    public async Task Invariants_EventOrderingPreserved_MaintainsExactOrder()
    {
        // Последовательность: Click -> TextInput -> Click -> Shortcut
        var inputMonitor = new Mock<IInputMonitoringService>();
        var correlator = new EventCorrelator();
        var targetResolver = new Mock<ITargetResolver>();
        var capture = new Mock<ICaptureCoordinator>();
        var repo = new Mock<IProjectRepository>();

        targetResolver
            .Setup(r => r.ResolveTargetAsync(It.IsAny<SemanticAction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SemanticAction a, CancellationToken _) =>
                new ElementInfo($"Target_{a.ActionType}", "Control", "id", "class", "app", 1, "App", 1, new BoundingBox(0, 0, 50, 20)));

        using var engine = new RecordingEngine(
            inputMonitor.Object,
            null,
            correlator,
            targetResolver.Object,
            new DefaultRecordingPolicy(),
            new StepDetector(),
            capture.Object,
            repo.Object);

        var emittedSteps = new List<Step>();
        engine.StepRecorded += (_, s) => emittedSteps.Add(s);

        engine.StartRecording();

        var t = DateTime.UtcNow;

        // 1. Click 1
        inputMonitor.Raise(m => m.MouseEventReceived += null, inputMonitor.Object,
            new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 10, 10, 0, t));
        inputMonitor.Raise(m => m.MouseEventReceived += null, inputMonitor.Object,
            new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 10, 10, 0, t.AddMilliseconds(20)));

        // 2. Typing "Hello"
        string text = "Hello";
        for (int i = 0; i < text.Length; i++)
        {
            inputMonitor.Raise(m => m.KeyboardEventReceived += null, inputMonitor.Object,
                new RawKeyboardEvent(RawKeyboardEventType.KeyDown, text[i], 0, KeyboardModifiers.None, text[i].ToString(), false, false, t.AddMilliseconds(50 + i * 20)));
        }

        // 3. Click 2 (автоматически сбрасывает "Hello" перед кликом!)
        var tClick2 = t.AddMilliseconds(200);
        inputMonitor.Raise(m => m.MouseEventReceived += null, inputMonitor.Object,
            new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 50, 50, 0, tClick2));
        inputMonitor.Raise(m => m.MouseEventReceived += null, inputMonitor.Object,
            new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 50, 50, 0, tClick2.AddMilliseconds(20)));

        // 4. Shortcut Ctrl+S
        var tShortcut = t.AddMilliseconds(300);
        inputMonitor.Raise(m => m.KeyboardEventReceived += null, inputMonitor.Object,
            new RawKeyboardEvent(RawKeyboardEventType.KeyDown, 83, 0, KeyboardModifiers.Control, null, false, false, tShortcut));

        await engine.StopRecordingAsync();

        Assert.Equal(4, emittedSteps.Count);
        Assert.Equal(ActionType.LeftClick, emittedSteps[0].Action);
        Assert.Equal(ActionType.TextInput, emittedSteps[1].Action);
        Assert.Equal(ActionType.LeftClick, emittedSteps[2].Action);
        Assert.Equal(ActionType.KeyPress, emittedSteps[3].Action);

        // Инвариант монотонности SequenceIndex (1, 2, 3, 4)
        for (int i = 0; i < emittedSteps.Count; i++)
        {
            Assert.Equal(i + 1, emittedSteps[i].SequenceIndex);
        }

        // Инвариант уникальности Id шагов
        var uniqueIds = emittedSteps.Select(s => s.Id).Distinct().Count();
        Assert.Equal(emittedSteps.Count, uniqueIds);
        Assert.All(emittedSteps, s => Assert.NotEqual(Guid.Empty, s.Id));
    }

    [Fact]
    public async Task Invariants_NoEventAcceptedAfterStop_DoesNotGenerateNewSteps()
    {
        var inputMonitor = new Mock<IInputMonitoringService>();
        var correlator = new EventCorrelator();
        var targetResolver = new Mock<ITargetResolver>();
        var capture = new Mock<ICaptureCoordinator>();
        var repo = new Mock<IProjectRepository>();

        targetResolver
            .Setup(r => r.ResolveTargetAsync(It.IsAny<SemanticAction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ElementInfo.Unknown);

        using var engine = new RecordingEngine(
            inputMonitor.Object,
            null,
            correlator,
            targetResolver.Object,
            new DefaultRecordingPolicy(),
            new StepDetector(),
            capture.Object,
            repo.Object);

        var recordedSteps = new List<Step>();
        engine.StepRecorded += (_, s) => recordedSteps.Add(s);

        engine.StartRecording();

        var now = DateTime.UtcNow;
        inputMonitor.Raise(m => m.MouseEventReceived += null, inputMonitor.Object,
            new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 10, 10, 0, now));
        inputMonitor.Raise(m => m.MouseEventReceived += null, inputMonitor.Object,
            new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 10, 10, 0, now.AddMilliseconds(20)));

        await engine.StopRecordingAsync();
        Assert.Equal(RecordingSessionState.Completed, engine.State);
        Assert.Single(recordedSteps);

        // Посылаем события после StopRecordingAsync
        inputMonitor.Raise(m => m.MouseEventReceived += null, inputMonitor.Object,
            new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 20, 20, 0, now.AddSeconds(1)));
        inputMonitor.Raise(m => m.MouseEventReceived += null, inputMonitor.Object,
            new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 20, 20, 0, now.AddSeconds(1.02)));

        inputMonitor.Raise(m => m.KeyboardEventReceived += null, inputMonitor.Object,
            new RawKeyboardEvent(RawKeyboardEventType.KeyDown, 13, 0, KeyboardModifiers.None, null, false, false, now.AddSeconds(2)));

        // Не должно добавиться ни одного нового шага
        Assert.Single(recordedSteps);
    }

    [Fact]
    public void Invariants_NoDuplicateSemanticActionsFromSameRawEvent()
    {
        using var correlator = new EventCorrelator();
        var emitted = new List<SemanticAction>();
        correlator.ActionCorrelated += (_, a) => emitted.Add(a);

        var now = DateTime.UtcNow;

        // Одиночный MouseDown + MouseUp порождает ровно 1 LeftClick
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 10, 10, 0, now));
        correlator.ProcessMouseEvent(new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 10, 10, 0, now.AddMilliseconds(20)));
        Assert.Single(emitted);

        // Одиночный KeyDown Enter порождает ровно 1 KeyPress
        correlator.ProcessKeyboardEvent(new RawKeyboardEvent(RawKeyboardEventType.KeyDown, 13, 0, KeyboardModifiers.None, null, false, false, now.AddMilliseconds(100)));
        Assert.Equal(2, emitted.Count);

        // KeyUp игнорируется и не дублирует действие
        correlator.ProcessKeyboardEvent(new RawKeyboardEvent(RawKeyboardEventType.KeyUp, 13, 0, KeyboardModifiers.None, null, false, false, now.AddMilliseconds(120)));
        Assert.Equal(2, emitted.Count);
    }

    #endregion

    #region 5. Full Pipeline Integration Test

    [Fact]
    public async Task IntegrationTest_FullPipeline_SimulatesClickTypingEnterShortcut_SavesToSQLite()
    {
        // 1. Инициализация реального SQLite хранилища в памяти
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var repository = new ProjectRepository(connection, projectRootPath: @"C:\StepwiseIntegrationTest");
        repository.CreateProject("Pipeline Test Guide", "End-to-end stage 2 verification");

        // 2. Настройка компонентов конвейера
        var inputMonitor = new Mock<IInputMonitoringService>();
        var windowTracker = new Mock<IActiveWindowTracker>();

        var testWindow = new ActiveWindowInfo(
            WindowHandle: 0x1234,
            ProcessId: 5678,
            ProcessName: "calculator",
            WindowTitle: "Calculator",
            Bounds: new BoundingBox(100, 100, 400, 600),
            Timestamp: DateTime.UtcNow
        );
        windowTracker.Setup(w => w.GetActiveWindow()).Returns(testWindow);

        var correlator = new EventCorrelator();

        var targetResolver = new Mock<ITargetResolver>();
        targetResolver
            .Setup(r => r.ResolveTargetAsync(It.IsAny<SemanticAction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SemanticAction action, CancellationToken _) =>
            {
                if (action.ActionType == SemanticActionType.LeftClick)
                {
                    return new ElementInfo(
                        Name: "Submit",
                        ControlType: "Button",
                        AutomationId: "btnSubmit",
                        ClassName: "Button",
                        ProcessName: "calculator",
                        ProcessId: 5678,
                        WindowTitle: "Calculator",
                        WindowHandle: 0x1234,
                        BoundingRectangle: new BoundingBox(120, 120, 80, 30)
                    );
                }
                if (action.ActionType == SemanticActionType.TextInput)
                {
                    return new ElementInfo(
                        Name: "Display",
                        ControlType: "Edit",
                        AutomationId: "txtDisplay",
                        ClassName: "TextBox",
                        ProcessName: "calculator",
                        ProcessId: 5678,
                        WindowTitle: "Calculator",
                        WindowHandle: 0x1234,
                        BoundingRectangle: new BoundingBox(100, 150, 200, 40)
                    );
                }
                return new ElementInfo(
                    Name: "Window",
                    ControlType: "Window",
                    AutomationId: "wndMain",
                    ClassName: "CalcWindow",
                    ProcessName: "calculator",
                    ProcessId: 5678,
                    WindowTitle: "Calculator",
                    WindowHandle: 0x1234,
                    BoundingRectangle: new BoundingBox(100, 100, 400, 600)
                );
            });

        var policy = new DefaultRecordingPolicy();
        var stepDetector = new StepDetector();

        var capture = new Mock<ICaptureCoordinator>();
        capture
            .Setup(c => c.CaptureStepAsync(It.IsAny<int>(), It.IsAny<ElementInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int seq, ElementInfo _, CancellationToken _) => $"assets/screenshots/step_{seq:D3}.png");

        using var engine = new RecordingEngine(
            inputMonitor.Object,
            windowTracker.Object,
            correlator,
            targetResolver.Object,
            policy,
            stepDetector,
            capture.Object,
            repository);

        var recordedSteps = new List<Step>();
        engine.StepRecorded += (_, step) => recordedSteps.Add(step);

        // 3. Запуск записи
        engine.StartRecording();
        Assert.Equal(RecordingSessionState.Recording, engine.State);

        var t = DateTime.UtcNow;

        // Действие 1: Mouse click on button
        inputMonitor.Raise(m => m.MouseEventReceived += null, inputMonitor.Object,
            new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 140, 135, 0, t));
        inputMonitor.Raise(m => m.MouseEventReceived += null, inputMonitor.Object,
            new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 140, 135, 0, t.AddMilliseconds(20)));

        // Действие 2: Keyboard typing "Hello"
        string typedText = "Hello";
        for (int i = 0; i < typedText.Length; i++)
        {
            inputMonitor.Raise(m => m.KeyboardEventReceived += null, inputMonitor.Object,
                new RawKeyboardEvent(RawKeyboardEventType.KeyDown, typedText[i], 0, KeyboardModifiers.None, typedText[i].ToString(), false, false, t.AddMilliseconds(50 + i * 20)));
        }

        // Действие 3: Pressing Enter (сбрасывает накопленный текст "Hello" перед нажатием Enter!)
        inputMonitor.Raise(m => m.KeyboardEventReceived += null, inputMonitor.Object,
            new RawKeyboardEvent(RawKeyboardEventType.KeyDown, 13, 0, KeyboardModifiers.None, null, false, false, t.AddMilliseconds(200)));

        // Действие 4: Pressing Ctrl+S
        inputMonitor.Raise(m => m.KeyboardEventReceived += null, inputMonitor.Object,
            new RawKeyboardEvent(RawKeyboardEventType.KeyDown, 83, 0, KeyboardModifiers.Control, null, false, false, t.AddMilliseconds(250)));

        // 4. Остановка записи и ожидание дренажа всех очередей
        await engine.StopRecordingAsync();
        Assert.Equal(RecordingSessionState.Completed, engine.State);

        // 5. Верификация данных из репозитория SQLite
        var persistedSteps = repository.LoadSteps();
        Assert.Equal(4, persistedSteps.Count);
        Assert.Equal(4, recordedSteps.Count);

        // Шаг 1: Click Submit
        var step1 = persistedSteps[0];
        Assert.Equal(1, step1.SequenceIndex);
        Assert.Equal(ActionType.LeftClick, step1.Action);
        Assert.Equal(140, step1.ClickX);
        Assert.Equal(135, step1.ClickY);
        Assert.Equal("Click \"Submit\"", step1.Title);
        Assert.Equal("assets/screenshots/step_001.png", step1.ScreenshotPath);
        Assert.Equal("Submit", step1.TargetElement.Name);
        Assert.Equal("calculator", step1.Metadata?["ProcessName"]);

        // Шаг 2: TextInput "Hello"
        var step2 = persistedSteps[1];
        Assert.Equal(2, step2.SequenceIndex);
        Assert.Equal(ActionType.TextInput, step2.Action);
        Assert.Equal("Type \"Hello\" into \"Display\"", step2.Title);
        Assert.Equal("assets/screenshots/step_002.png", step2.ScreenshotPath);
        Assert.Equal("5", step2.Metadata?["CharacterCount"]);

        // Шаг 3: Press Enter
        var step3 = persistedSteps[2];
        Assert.Equal(3, step3.SequenceIndex);
        Assert.Equal(ActionType.KeyPress, step3.Action);
        Assert.Equal("Press Enter", step3.Title);
        Assert.Equal("assets/screenshots/step_003.png", step3.ScreenshotPath);

        // Шаг 4: Press Control+S
        var step4 = persistedSteps[3];
        Assert.Equal(4, step4.SequenceIndex);
        Assert.Equal(ActionType.KeyPress, step4.Action);
        Assert.Equal("Press Control+S", step4.Title);
        Assert.Equal("assets/screenshots/step_004.png", step4.ScreenshotPath);

        _output.WriteLine("Pipeline SQLite verification succeeded: 4/4 steps persisted with correct sequence and metadata.");
    }

    #endregion

    #region 6. Performance Measurement Test

    [Fact]
    public async Task Performance_RapidInput_100ClicksAnd100TextEvents_MeasuresLatencyAndMemory()
    {
        var inputMonitor = new Mock<IInputMonitoringService>();
        var correlator = new EventCorrelator();
        var targetResolver = new Mock<ITargetResolver>();
        targetResolver
            .Setup(r => r.ResolveTargetAsync(It.IsAny<SemanticAction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ElementInfo("FastBtn", "Button", "btnFast", "Button", "perfApp", 1, "Perf", 1, new BoundingBox(10, 10, 20, 20)));

        var capture = new Mock<ICaptureCoordinator>();
        capture
            .Setup(c => c.CaptureStepAsync(It.IsAny<int>(), It.IsAny<ElementInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int seq, ElementInfo _, CancellationToken _) => $"assets/perf_{seq}.png");

        using var engine = new RecordingEngine(
            inputMonitor.Object,
            null,
            correlator,
            targetResolver.Object,
            new DefaultRecordingPolicy(),
            new StepDetector(),
            capture.Object);

        var recordedSteps = new List<Step>();
        engine.StepRecorded += (_, s) =>
        {
            lock (recordedSteps)
            {
                recordedSteps.Add(s);
            }
        };

        const int pairsCount = 100; // 100 clicks + 100 text flushes = 200 actions

        // Измерение памяти до нагрузки
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long memBefore = GC.GetTotalMemory(true);
        int gen0Before = GC.CollectionCount(0);
        int gen1Before = GC.CollectionCount(1);
        int gen2Before = GC.CollectionCount(2);

        var stopwatch = Stopwatch.StartNew();

        engine.StartRecording();

        var baseTime = DateTime.UtcNow;

        // Быстрая подача чередующихся 100 кликов и 100 текстовых символов (клик сбрасывает текст -> 200 шагов)
        for (int i = 0; i < pairsCount; i++)
        {
            // 1. Text input: "X"
            var tText = baseTime.AddMilliseconds(i * 10);
            inputMonitor.Raise(m => m.KeyboardEventReceived += null, inputMonitor.Object,
                new RawKeyboardEvent(RawKeyboardEventType.KeyDown, 88, 0, KeyboardModifiers.None, "X", false, false, tText));

            // 2. Mouse click (Down + Up) — вызывает сброс текста и создает клик
            var tClick = tText.AddMilliseconds(2);
            inputMonitor.Raise(m => m.MouseEventReceived += null, inputMonitor.Object,
                new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 10 + i, 20 + i, 0, tClick));
            inputMonitor.Raise(m => m.MouseEventReceived += null, inputMonitor.Object,
                new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 10 + i, 20 + i, 0, tClick.AddMilliseconds(1)));
        }

        await engine.StopRecordingAsync();
        stopwatch.Stop();

        long memAfter = GC.GetTotalMemory(false);
        int gen0Count = GC.CollectionCount(0) - gen0Before;
        int gen1Count = GC.CollectionCount(1) - gen1Before;
        int gen2Count = GC.CollectionCount(2) - gen2Before;

        int totalExpectedEvents = pairsCount * 2; // 200 steps
        double totalMs = stopwatch.Elapsed.TotalMilliseconds;
        double avgLatencyPerEvent = totalMs / totalExpectedEvents;

        _output.WriteLine("=== PERFORMANCE BENCHMARK REPORT ===");
        _output.WriteLine($"Total Events Processed: {recordedSteps.Count} (Expected: {totalExpectedEvents})");
        _output.WriteLine($"Total Processing Time: {totalMs:F2} ms");
        _output.WriteLine($"Average Latency Per Event: {avgLatencyPerEvent:F3} ms");
        _output.WriteLine($"Memory Delta: {(memAfter - memBefore) / 1024.0:F2} KB");
        _output.WriteLine($"GC Collections: Gen0={gen0Count}, Gen1={gen1Count}, Gen2={gen2Count}");
        _output.WriteLine("====================================");

        // Проверка отсутствия потерь и искажений
        Assert.Equal(totalExpectedEvents, recordedSteps.Count);
        for (int i = 0; i < recordedSteps.Count; i++)
        {
            Assert.Equal(i + 1, recordedSteps[i].SequenceIndex);
        }
        Assert.True(avgLatencyPerEvent < 50.0, $"Average latency {avgLatencyPerEvent}ms exceeds SLA 50ms.");
    }

    #endregion

    #region 7. Failure Handling Tests

    [Fact]
    public async Task FailureHandling_UIALookupFails_ReturnsFallbackElementInfo_NoCrash()
    {
        var mockUia = new Mock<IUIAutomationService>();
        mockUia
            .Setup(u => u.InspectElementAt(It.IsAny<int>(), It.IsAny<int>()))
            .Throws(new InvalidOperationException("COM Exception 0x80004005: UIA Not Available"));

        var resolver = new UIATargetResolver(mockUia.Object);

        var context = new WindowContext(
            WindowHandle: 12345,
            ProcessId: 6789,
            ProcessName: "faultyApp",
            WindowTitle: "Faulty App Window",
            Bounds: new BoundingBox(0, 0, 500, 300),
            Timestamp: DateTime.UtcNow
        );

        var action = SemanticAction.CreateMouseClick(
            SemanticActionType.LeftClick,
            100,
            150,
            context,
            DateTime.UtcNow
        );

        // Не выбрасывает исключение, а возвращает безопасный fallback элемент
        var target = await resolver.ResolveTargetAsync(action);

        Assert.NotNull(target);
        Assert.Equal("faultyApp", target.ProcessName);
        Assert.Equal(6789, target.ProcessId);
        Assert.Equal("Faulty App Window", target.WindowTitle);
        Assert.Equal(12345, target.WindowHandle);
    }

    [Fact]
    public async Task FailureHandling_CaptureFails_ReturnsNullScreenshot_StepSavedGracefully()
    {
        var mockCapture = new Mock<IScreenCaptureService>();
        var mockRepo = new Mock<IProjectRepository>();
        mockRepo.SetupGet(r => r.ProjectRootPath).Returns(@"C:\TestProject");

        mockCapture
            .Setup(c => c.Capture(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<BoundingBox?>(), It.IsAny<long>()))
            .Throws(new InvalidOperationException("GDI screen capture failed: Desktop locked"));

        var coordinator = new CaptureCoordinator(mockCapture.Object, mockRepo.Object);
        var target = ElementInfo.Unknown;

        // Координатор перехватывает сбой и возвращает null без аварийного завершения
        var screenshot = await coordinator.CaptureStepAsync(1, target);
        Assert.Null(screenshot);

        // Интеграция в движке: шаг создается и сохраняется даже с ScreenshotPath == null
        var inputMonitor = new Mock<IInputMonitoringService>();
        var correlator = new EventCorrelator();
        var targetResolver = new Mock<ITargetResolver>();
        targetResolver
            .Setup(r => r.ResolveTargetAsync(It.IsAny<SemanticAction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        Step? savedStep = null;
        mockRepo
            .Setup(r => r.SaveStep(It.IsAny<Step>()))
            .Callback<Step>(s => savedStep = s);

        using var engine = new RecordingEngine(
            inputMonitor.Object,
            null,
            correlator,
            targetResolver.Object,
            new DefaultRecordingPolicy(),
            new StepDetector(),
            coordinator,
            mockRepo.Object);

        engine.StartRecording();

        var now = DateTime.UtcNow;
        inputMonitor.Raise(m => m.MouseEventReceived += null, inputMonitor.Object,
            new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 10, 10, 0, now));
        inputMonitor.Raise(m => m.MouseEventReceived += null, inputMonitor.Object,
            new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 10, 10, 0, now.AddMilliseconds(20)));

        await engine.StopRecordingAsync();

        Assert.NotNull(savedStep);
        Assert.Null(savedStep.ScreenshotPath);
        Assert.Equal(1, savedStep.SequenceIndex);
    }

    [Fact]
    public async Task FailureHandling_RepositoryError_HandledCleanly_NoUnhandledCrash()
    {
        var inputMonitor = new Mock<IInputMonitoringService>();
        var correlator = new EventCorrelator();
        var targetResolver = new Mock<ITargetResolver>();
        targetResolver
            .Setup(r => r.ResolveTargetAsync(It.IsAny<SemanticAction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ElementInfo.Unknown);

        var capture = new Mock<ICaptureCoordinator>();

        var repoMock = new Mock<IProjectRepository>();
        repoMock
            .Setup(r => r.SaveStep(It.IsAny<Step>()))
            .Throws(new InvalidOperationException("SQLite Database locked / Disk full error"));

        using var engine = new RecordingEngine(
            inputMonitor.Object,
            null,
            correlator,
            targetResolver.Object,
            new DefaultRecordingPolicy(),
            new StepDetector(),
            capture.Object,
            repoMock.Object);

        var emittedSteps = new List<Step>();
        engine.StepRecorded += (_, s) => emittedSteps.Add(s);

        engine.StartRecording();

        var now = DateTime.UtcNow;
        inputMonitor.Raise(m => m.MouseEventReceived += null, inputMonitor.Object,
            new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, 10, 10, 0, now));
        inputMonitor.Raise(m => m.MouseEventReceived += null, inputMonitor.Object,
            new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, 10, 10, 0, now.AddMilliseconds(20)));

        // Остановка не должна падать с необработанным исключением
        await engine.StopRecordingAsync();

        // Проверяем, что событие StepRecorded было вызвано, несмотря на сбой диска/БД
        Assert.Single(emittedSteps);
        Assert.Equal(RecordingSessionState.Completed, engine.State);
    }

    [Fact]
    public async Task FailureHandling_AbruptStopDuringRapidProcessing_DrainsGracefullyInCompletedState()
    {
        var inputMonitor = new Mock<IInputMonitoringService>();
        var correlator = new EventCorrelator();
        var targetResolver = new Mock<ITargetResolver>();
        targetResolver
            .Setup(r => r.ResolveTargetAsync(It.IsAny<SemanticAction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ElementInfo.Unknown);

        var capture = new Mock<ICaptureCoordinator>();

        using var engine = new RecordingEngine(
            inputMonitor.Object,
            null,
            correlator,
            targetResolver.Object,
            new DefaultRecordingPolicy(),
            new StepDetector(),
            capture.Object);

        engine.StartRecording();

        // Начинаем слать события в фоне
        var sendingTask = Task.Run(async () =>
        {
            var now = DateTime.UtcNow;
            for (int i = 0; i < 50; i++)
            {
                if (!engine.IsRecording) break;

                inputMonitor.Raise(m => m.MouseEventReceived += null, inputMonitor.Object,
                    new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, i, i, 0, now.AddMilliseconds(i)));
                inputMonitor.Raise(m => m.MouseEventReceived += null, inputMonitor.Object,
                    new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, i, i, 0, now.AddMilliseconds(i + 1)));

                await Task.Yield();
            }
        });

        // Внезапная остановка прямо во время лавины событий
        await Task.Delay(10);
        await engine.StopRecordingAsync();

        await sendingTask;

        Assert.Equal(RecordingSessionState.Completed, engine.State);
        Assert.False(engine.IsRecording);
    }

    #endregion
}
