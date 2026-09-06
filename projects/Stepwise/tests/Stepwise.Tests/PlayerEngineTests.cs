using Stepwise.Core.Engine;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Xunit;

namespace Stepwise.Tests;

public class PlayerEngineTests
{
    private static Step CreateSampleStep(int sequenceIndex, string title = "Test Step")
    {
        return new Step(
            Id: Guid.NewGuid(),
            SequenceIndex: sequenceIndex,
            Timestamp: DateTime.UtcNow,
            Action: ActionType.LeftClick,
            ClickX: 100,
            ClickY: 200,
            TargetElement: new ElementInfo(
                Name: $"Element {sequenceIndex}",
                ControlType: "Button",
                AutomationId: $"btn_{sequenceIndex}",
                ClassName: "Button",
                ProcessName: "testapp",
                ProcessId: 1000,
                WindowTitle: "Test Window",
                WindowHandle: 0x1234,
                BoundingRectangle: new BoundingBox(10, 20, 100, 30)
            ),
            Title: title
        );
    }

    [Fact]
    public void PlayerStateMachine_ValidTransitions_ShouldSucceed()
    {
        var sm = new PlayerStateMachine();
        Assert.True(sm.IsIdle);

        // Idle -> Playing
        Assert.True(sm.TryTransition(PlayerState.Playing, out _));
        Assert.True(sm.IsPlaying);

        // Playing -> Paused
        Assert.True(sm.TryTransition(PlayerState.Paused, out _));
        Assert.True(sm.IsPaused);

        // Paused -> Playing
        Assert.True(sm.TryTransition(PlayerState.Playing, out _));
        Assert.True(sm.IsPlaying);

        // Playing -> Completed
        Assert.True(sm.TryTransition(PlayerState.Completed, out _));
        Assert.True(sm.IsCompleted);

        // Completed -> Playing (Restart)
        Assert.True(sm.TryTransition(PlayerState.Playing, out _));
        Assert.True(sm.IsPlaying);

        // Playing -> Failed
        sm.SetFailed("Crash");
        Assert.True(sm.IsFailed);
        Assert.Equal("Crash", sm.LastError);

        // Any state -> Idle via ResetToIdle
        sm.ResetToIdle();
        Assert.True(sm.IsIdle);

        // Paused -> Completed
        sm.TransitionOrThrow(PlayerState.Playing);
        sm.TransitionOrThrow(PlayerState.Paused);
        Assert.True(sm.TryTransition(PlayerState.Completed, out _));
        Assert.True(sm.IsCompleted);

        // Paused -> Failed
        sm.ResetToIdle();
        sm.TransitionOrThrow(PlayerState.Playing);
        sm.TransitionOrThrow(PlayerState.Paused);
        sm.SetFailed("Paused failure");
        Assert.True(sm.IsFailed);
    }

    [Fact]
    public void PlayerStateMachine_InvalidTransitions_ShouldFail()
    {
        var sm = new PlayerStateMachine();

        // Idle invalid transitions
        Assert.False(PlayerStateMachine.IsValidTransition(PlayerState.Idle, PlayerState.Paused));
        Assert.False(PlayerStateMachine.IsValidTransition(PlayerState.Idle, PlayerState.Completed));
        Assert.False(PlayerStateMachine.IsValidTransition(PlayerState.Idle, PlayerState.Failed));
        Assert.False(PlayerStateMachine.IsValidTransition(PlayerState.Idle, PlayerState.Idle));

        Assert.False(sm.TryTransition(PlayerState.Paused, out var error));
        Assert.NotNull(error);
        Assert.Throws<InvalidOperationException>(() => sm.TransitionOrThrow(PlayerState.Paused));

        // Same-state transitions
        Assert.False(PlayerStateMachine.IsValidTransition(PlayerState.Playing, PlayerState.Playing));
        Assert.False(PlayerStateMachine.IsValidTransition(PlayerState.Paused, PlayerState.Paused));

        // Completed invalid transitions
        Assert.False(PlayerStateMachine.IsValidTransition(PlayerState.Completed, PlayerState.Paused));
        Assert.False(PlayerStateMachine.IsValidTransition(PlayerState.Completed, PlayerState.Failed));
        Assert.False(PlayerStateMachine.IsValidTransition(PlayerState.Completed, PlayerState.Completed));

        // Failed invalid transitions
        Assert.False(PlayerStateMachine.IsValidTransition(PlayerState.Failed, PlayerState.Playing));
        Assert.False(PlayerStateMachine.IsValidTransition(PlayerState.Failed, PlayerState.Paused));
        Assert.False(PlayerStateMachine.IsValidTransition(PlayerState.Failed, PlayerState.Completed));
        Assert.False(PlayerStateMachine.IsValidTransition(PlayerState.Failed, PlayerState.Failed));
    }

    [Fact]
    public void PlayerStateMachine_StateChangedEvent_ShouldFireOnTransition()
    {
        var sm = new PlayerStateMachine();
        PlayerState? observedOld = null;
        PlayerState? observedNew = null;

        sm.StateChanged += (oldState, newState) =>
        {
            observedOld = oldState;
            observedNew = newState;
        };

        sm.TransitionOrThrow(PlayerState.Playing);
        Assert.Equal(PlayerState.Idle, observedOld);
        Assert.Equal(PlayerState.Playing, observedNew);
    }

    [Fact]
    public void PlayerEngine_EmptyGuideLoaded_SetsDeterministicDefaults()
    {
        IPlayerEngine engine = new PlayerEngine();

        // 1. null steps
        bool resultNull = engine.LoadGuide(Guid.NewGuid(), null);
        Assert.False(resultNull);
        Assert.Equal(-1, engine.CurrentIndex);
        Assert.Null(engine.CurrentStep);
        Assert.Equal(0, engine.TotalSteps);
        Assert.Equal(PlayerState.Idle, engine.State);

        // 2. empty list
        bool resultEmpty = engine.LoadGuide(Guid.NewGuid(), new List<Step>());
        Assert.False(resultEmpty);
        Assert.Equal(-1, engine.CurrentIndex);
        Assert.Null(engine.CurrentStep);
        Assert.Equal(0, engine.TotalSteps);
        Assert.Equal(PlayerState.Idle, engine.State);
        Assert.False(engine.CanPlay);
        Assert.False(engine.CanNext);
        Assert.False(engine.CanPrevious);
        Assert.False(engine.CanFirst);
        Assert.False(engine.CanLast);
        Assert.False(engine.CanRestart);
    }

    [Fact]
    public void PlayerEngine_ValidGuideLoaded_InitialStateAndNavigation()
    {
        IPlayerEngine engine = new PlayerEngine();
        var steps = new List<Step>
        {
            CreateSampleStep(1, "Step 1"),
            CreateSampleStep(2, "Step 2"),
            CreateSampleStep(3, "Step 3")
        };
        var guideId = Guid.NewGuid();

        bool loaded = engine.LoadGuide(guideId, steps);
        Assert.True(loaded);
        Assert.Equal(guideId, engine.CurrentGuideId);
        Assert.Equal(0, engine.CurrentIndex);
        Assert.NotNull(engine.CurrentStep);
        Assert.Equal("Step 1", engine.CurrentStep?.Title);
        Assert.Equal(3, engine.TotalSteps);
        Assert.Equal(PlayerState.Idle, engine.State);

        // Can flags at start
        Assert.True(engine.CanPlay);
        Assert.True(engine.CanNext);
        Assert.False(engine.CanPrevious);
        Assert.False(engine.CanFirst);
        Assert.True(engine.CanLast);
        Assert.True(engine.CanRestart);

        // Play
        Assert.True(engine.Play());
        Assert.Equal(PlayerState.Playing, engine.State);
        Assert.True(engine.CanPause);

        // Pause and Resume
        Assert.True(engine.Pause());
        Assert.Equal(PlayerState.Paused, engine.State);
        Assert.True(engine.Resume());
        Assert.Equal(PlayerState.Playing, engine.State);

        // Next to step 2
        int recordedIndex = -1;
        Step? recordedStep = null;
        engine.StepChanged += (idx, s) =>
        {
            recordedIndex = idx;
            recordedStep = s;
        };

        Assert.True(engine.Next());
        Assert.Equal(1, engine.CurrentIndex);
        Assert.Equal(1, recordedIndex);
        Assert.Equal("Step 2", recordedStep?.Title);
        Assert.True(engine.CanPrevious);
        Assert.True(engine.CanFirst);
        Assert.True(engine.CanLast);

        // Next to step 3 (last step)
        Assert.True(engine.Next());
        Assert.Equal(2, engine.CurrentIndex);
        Assert.Equal("Step 3", engine.CurrentStep?.Title);
        Assert.True(engine.CanNext); // Can still Next to complete!
        Assert.False(engine.CanLast); // Already at last step

        // Next at last step transitions to Completed
        Assert.True(engine.Next());
        Assert.Equal(PlayerState.Completed, engine.State);
        Assert.Equal(2, engine.CurrentIndex); // Remains at last step!

        // If already Completed, Next returns false
        Assert.False(engine.Next());
        Assert.False(engine.CanNext);

        // Previous from Completed decrements index and transitions to Playing
        Assert.True(engine.Previous());
        Assert.Equal(PlayerState.Playing, engine.State);
        Assert.Equal(1, engine.CurrentIndex);

        // First() sets CurrentIndex = 0 and fires StepChanged
        Assert.True(engine.First());
        Assert.Equal(0, engine.CurrentIndex);
        Assert.Equal("Step 1", engine.CurrentStep?.Title);

        // Previous at index 0 returns false
        Assert.False(engine.Previous());
        Assert.Equal(0, engine.CurrentIndex);

        // Last() sets CurrentIndex = TotalSteps - 1
        Assert.True(engine.Last());
        Assert.Equal(2, engine.CurrentIndex);
        Assert.Equal("Step 3", engine.CurrentStep?.Title);

        // Restart() sets CurrentIndex = 0 and state to Playing
        Assert.True(engine.Restart());
        Assert.Equal(0, engine.CurrentIndex);
        Assert.Equal(PlayerState.Playing, engine.State);

        // Fail() transitions to Failed
        engine.Fail("Something crashed");
        Assert.Equal(PlayerState.Failed, engine.State);
        Assert.False(engine.Next());
        Assert.False(engine.Previous());

        // Unload() clears steps, resets to Idle
        engine.Unload();
        Assert.Equal(PlayerState.Idle, engine.State);
        Assert.Equal(-1, engine.CurrentIndex);
        Assert.Null(engine.CurrentStep);
        Assert.Equal(0, engine.TotalSteps);
        Assert.Null(engine.CurrentGuideId);
    }
}