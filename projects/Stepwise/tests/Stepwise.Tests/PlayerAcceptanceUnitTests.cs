using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Moq;
using Stepwise.App.Services;
using Stepwise.App.ViewModels;
using Stepwise.Core.Engine;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Stepwise.Storage.Repositories;
using Xunit;

namespace Stepwise.Tests;

/// <summary>
/// Dedicated acceptance and unit tests covering all 20 Player requirements specified in Section 16:
///  1. Initial state (InitialState)
///  2. Empty guide (EmptyGuide)
///  3. First step (FirstStep)
///  4. Next (Next)
///  5. Previous (Previous)
///  6. Last step (LastStep)
///  7. Next at last (NextAtLast)
///  8. Previous at first (PreviousAtFirst)
///  9. First (First)
/// 10. Last (Last)
/// 11. Restart (Restart)
/// 12. Play (Play)
/// 13. Pause (Pause)
/// 14. Complete (Complete)
/// 15. Invalid state transition (InvalidStateTransition)
/// 16. Missing screenshot (MissingScreenshot)
/// 17. Corrupt screenshot (CorruptScreenshot)
/// 18. Rapid navigation (RapidNavigation)
/// 19. Cancellation (Cancellation)
/// 20. Player read-only behavior (PlayerReadOnlyBehavior)
/// </summary>
public sealed class PlayerAcceptanceUnitTests : IDisposable
{
    private readonly string _tempTestDir;

    public PlayerAcceptanceUnitTests()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), "Stepwise_PlayerAcceptance_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempTestDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempTestDir))
            {
                Directory.Delete(_tempTestDir, recursive: true);
            }
        }
        catch
        {
            // Ignore OS file-lock delays during cleanup
        }
    }

    #region Helper Methods

    private static Step CreateTestStep(
        int sequenceIndex,
        string? screenshotPath = null,
        string? title = null,
        string? description = null,
        double clickX = 100.0,
        double clickY = 200.0)
    {
        return new Step(
            Id: Guid.NewGuid(),
            SequenceIndex: sequenceIndex,
            Timestamp: DateTime.UtcNow,
            Action: ActionType.LeftClick,
            ClickX: clickX,
            ClickY: clickY,
            TargetElement: new ElementInfo(
                Name: $"Element_{sequenceIndex}",
                ControlType: "Button",
                AutomationId: $"btn_{sequenceIndex}",
                ClassName: "StandardButton",
                ProcessName: "test_process",
                ProcessId: 4321,
                WindowTitle: "Test Window Title",
                WindowHandle: 0x5678,
                BoundingRectangle: new BoundingBox(50.0 * sequenceIndex, 60.0 * sequenceIndex, 120.0, 40.0)
            ),
            ScreenshotPath: screenshotPath ?? $"assets/screenshots/step_{sequenceIndex:D3}.png",
            Title: title ?? $"Step {sequenceIndex}",
            Description: description ?? $"Description for Step {sequenceIndex}"
        );
    }

    private static List<Step> CreateSampleSteps(int count)
    {
        var steps = new List<Step>(count);
        for (int i = 1; i <= count; i++)
        {
            steps.Add(CreateTestStep(i, $"assets/screenshots/step_{i:D3}.png", $"Step {i}", $"Description {i}", 100 + i * 10, 200 + i * 10));
        }
        return steps;
    }

    private string CreateDummyImageFile(string fileName, int width = 80, int height = 40)
    {
        var filePath = Path.Combine(_tempTestDir, fileName);
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var bmp = new Bitmap(width, height);
        using var gfx = Graphics.FromImage(bmp);
        gfx.Clear(Color.DarkSlateBlue);
        bmp.Save(filePath, ImageFormat.Png);
        return filePath;
    }

    #endregion

    #region 1. Initial State

    [Fact(DisplayName = "1. Initial state (InitialState)")]
    public void InitialState()
    {
        // Engine initial state verification
        IPlayerEngine engine = new PlayerEngine();

        Assert.Equal(PlayerState.Idle, engine.State);
        Assert.Equal(-1, engine.CurrentIndex);
        Assert.Null(engine.CurrentStep);
        Assert.Equal(0, engine.TotalSteps);
        Assert.Null(engine.CurrentGuideId);
        Assert.Empty(engine.Steps);

        // Capability flags must all be false when idle without steps
        Assert.False(engine.CanPlay);
        Assert.False(engine.CanPause);
        Assert.False(engine.CanNext);
        Assert.False(engine.CanPrevious);
        Assert.False(engine.CanFirst);
        Assert.False(engine.CanLast);
        Assert.False(engine.CanRestart);

        // ViewModel initial state verification
        var mockLoader = new Mock<IImageLoaderService>();
        using var vm = new PlayerViewModel(engine, mockLoader.Object);

        Assert.Equal(-1, vm.CurrentIndex);
        Assert.Equal(0, vm.TotalSteps);
        Assert.Null(vm.CurrentStep);
        Assert.Equal(PlayerState.Idle, vm.PlayerState);
        Assert.Equal("Нет шагов", vm.StepPositionText);
        Assert.Equal(Visibility.Visible, vm.EmptyStateVisibility);
        Assert.Equal("Без названия", vm.CurrentStepTitle);
        Assert.Equal("Нет описания", vm.CurrentStepDescription);
        Assert.False(vm.CanPlayPause);
        Assert.False(vm.CanNext);
        Assert.False(vm.CanPrevious);
        Assert.False(vm.CanFirst);
        Assert.False(vm.CanLast);
        Assert.False(vm.CanRestart);
        Assert.False(vm.IsPreviewLoading);
        Assert.False(vm.IsPreviewError);
        Assert.Null(vm.PreviewImage);
    }

    #endregion

    #region 2. Empty Guide

    [Fact(DisplayName = "2. Empty guide (EmptyGuide)")]
    public void EmptyGuide()
    {
        IPlayerEngine engine = new PlayerEngine();
        var mockLoader = new Mock<IImageLoaderService>();
        using var vm = new PlayerViewModel(engine, mockLoader.Object);

        // 2.1 Load with null list
        bool loadNull = engine.LoadGuide(Guid.NewGuid(), null);
        Assert.False(loadNull);
        Assert.Equal(-1, engine.CurrentIndex);
        Assert.Null(engine.CurrentStep);
        Assert.Equal(0, engine.TotalSteps);
        Assert.Equal(PlayerState.Idle, engine.State);
        Assert.False(engine.CanPlay);

        // 2.2 Load with empty list
        bool loadEmpty = engine.LoadGuide(Guid.NewGuid(), new List<Step>());
        Assert.False(loadEmpty);
        Assert.Equal(-1, engine.CurrentIndex);
        Assert.Null(engine.CurrentStep);
        Assert.Equal(0, engine.TotalSteps);
        Assert.Equal(PlayerState.Idle, engine.State);

        // 2.3 Load with Guid.Empty
        var dummySteps = CreateSampleSteps(2);
        bool loadEmptyGuid = engine.LoadGuide(Guid.Empty, dummySteps);
        Assert.False(loadEmptyGuid);
        Assert.Equal(-1, engine.CurrentIndex);
        Assert.Null(engine.CurrentStep);
        Assert.Equal(0, engine.TotalSteps);
        Assert.Equal(PlayerState.Idle, engine.State);

        // 2.4 Calling operations on empty guide safely returns false
        Assert.False(engine.Next());
        Assert.False(engine.Previous());
        Assert.False(engine.First());
        Assert.False(engine.Last());
        Assert.False(engine.Play());
        Assert.False(engine.Pause());
        Assert.False(engine.Resume());
        Assert.False(engine.Restart());

        // ViewModel reflects empty state
        Assert.Equal(Visibility.Visible, vm.EmptyStateVisibility);
        Assert.Equal("Нет шагов", vm.StepPositionText);
    }

    #endregion

    #region 3. First Step

    [Fact(DisplayName = "3. First step (FirstStep)")]
    public void FirstStep()
    {
        IPlayerEngine engine = new PlayerEngine();
        var steps = CreateSampleSteps(3);
        var guideId = Guid.NewGuid();

        int observedIndex = -1;
        Step? observedStep = null;
        engine.StepChanged += (idx, s) =>
        {
            observedIndex = idx;
            observedStep = s;
        };

        bool loaded = engine.LoadGuide(guideId, steps);

        // Deterministic first-step load state
        Assert.True(loaded);
        Assert.Equal(guideId, engine.CurrentGuideId);
        Assert.Equal(0, engine.CurrentIndex);
        Assert.NotNull(engine.CurrentStep);
        Assert.Equal(1, engine.CurrentStep?.SequenceIndex);
        Assert.Equal("Step 1", engine.CurrentStep?.Title);
        Assert.Equal(3, engine.TotalSteps);
        Assert.Equal(PlayerState.Idle, engine.State);

        // Event fired with first step
        Assert.Equal(0, observedIndex);
        Assert.Same(steps[0], observedStep);

        // Capability flags at first step
        Assert.True(engine.CanNext);
        Assert.False(engine.CanPrevious);
        Assert.False(engine.CanFirst);
        Assert.True(engine.CanLast);
        Assert.True(engine.CanPlay);
        Assert.False(engine.CanPause);
        Assert.True(engine.CanRestart);

        // ViewModel binding verification
        var mockLoader = new Mock<IImageLoaderService>();
        using var vm = new PlayerViewModel(engine, mockLoader.Object);

        Assert.Equal(0, vm.CurrentIndex);
        Assert.Equal(3, vm.TotalSteps);
        Assert.Equal("Шаг 1 из 3", vm.StepPositionText);
        Assert.Equal(Visibility.Collapsed, vm.EmptyStateVisibility);
        Assert.Equal("Step 1", vm.CurrentStepTitle);
        Assert.Equal("Description 1", vm.CurrentStepDescription);
        Assert.Equal("Element_1", vm.TargetElementName);
        Assert.Equal("Button", vm.TargetControlType);
        Assert.Equal("btn_1", vm.TargetAutomationId);
    }

    #endregion

    #region 4. Next

    [Fact(DisplayName = "4. Next (Next)")]
    public void Next()
    {
        IPlayerEngine engine = new PlayerEngine();
        var steps = CreateSampleSteps(3);
        engine.LoadGuide(Guid.NewGuid(), steps);

        int eventCount = 0;
        int observedIndex = -1;
        Step? observedStep = null;
        engine.StepChanged += (idx, s) =>
        {
            eventCount++;
            observedIndex = idx;
            observedStep = s;
        };

        var mockLoader = new Mock<IImageLoaderService>();
        using var vm = new PlayerViewModel(engine, mockLoader.Object);

        // Forward transition from 0 -> 1
        bool nextSuccess = engine.Next();

        Assert.True(nextSuccess);
        Assert.Equal(1, engine.CurrentIndex);
        Assert.Same(steps[1], engine.CurrentStep);
        Assert.Equal(1, eventCount);
        Assert.Equal(1, observedIndex);
        Assert.Same(steps[1], observedStep);

        // Navigation flags updated
        Assert.True(engine.CanPrevious);
        Assert.True(engine.CanFirst);
        Assert.True(engine.CanNext);
        Assert.True(engine.CanLast);

        // ViewModel synchronization
        Assert.Equal(1, vm.CurrentIndex);
        Assert.Equal("Шаг 2 из 3", vm.StepPositionText);
        Assert.Equal("Step 2", vm.CurrentStepTitle);
    }

    #endregion

    #region 5. Previous

    [Fact(DisplayName = "5. Previous (Previous)")]
    public void Previous()
    {
        IPlayerEngine engine = new PlayerEngine();
        var steps = CreateSampleSteps(3);
        engine.LoadGuide(Guid.NewGuid(), steps);

        // Advance to step 1
        engine.Next();
        Assert.Equal(1, engine.CurrentIndex);

        int eventCount = 0;
        int observedIndex = -1;
        Step? observedStep = null;
        engine.StepChanged += (idx, s) =>
        {
            eventCount++;
            observedIndex = idx;
            observedStep = s;
        };

        var mockLoader = new Mock<IImageLoaderService>();
        using var vm = new PlayerViewModel(engine, mockLoader.Object);

        // Backward transition from 1 -> 0
        bool prevSuccess = engine.Previous();

        Assert.True(prevSuccess);
        Assert.Equal(0, engine.CurrentIndex);
        Assert.Same(steps[0], engine.CurrentStep);
        Assert.Equal(1, eventCount);
        Assert.Equal(0, observedIndex);
        Assert.Same(steps[0], observedStep);

        // Navigation flags at first step
        Assert.False(engine.CanPrevious);
        Assert.False(engine.CanFirst);
        Assert.True(engine.CanNext);
        Assert.True(engine.CanLast);

        // ViewModel synchronization
        Assert.Equal(0, vm.CurrentIndex);
        Assert.Equal("Шаг 1 из 3", vm.StepPositionText);
        Assert.Equal("Step 1", vm.CurrentStepTitle);
    }

    #endregion

    #region 6. Last Step

    [Fact(DisplayName = "6. Last step (LastStep)")]
    public void LastStep()
    {
        IPlayerEngine engine = new PlayerEngine();
        var steps = CreateSampleSteps(3);
        engine.LoadGuide(Guid.NewGuid(), steps);

        // Navigate to last step
        bool lastSuccess = engine.Last();

        Assert.True(lastSuccess);
        Assert.Equal(2, engine.CurrentIndex);
        Assert.Equal(engine.TotalSteps - 1, engine.CurrentIndex);
        Assert.Same(steps[2], engine.CurrentStep);
        Assert.Equal("Step 3", engine.CurrentStep?.Title);

        // Capability flags at last step:
        // CanLast is false (already at last step)
        // CanNext is true (transitions to Completed)
        // CanPrevious and CanFirst are true
        Assert.False(engine.CanLast);
        Assert.True(engine.CanNext);
        Assert.True(engine.CanPrevious);
        Assert.True(engine.CanFirst);
        Assert.NotEqual(PlayerState.Completed, engine.State);
    }

    #endregion

    #region 7. Next at Last

    [Fact(DisplayName = "7. Next at last (NextAtLast)")]
    public void NextAtLast()
    {
        IPlayerEngine engine = new PlayerEngine();
        var steps = CreateSampleSteps(3);
        engine.LoadGuide(Guid.NewGuid(), steps);

        // Navigate to last step
        engine.Last();
        Assert.Equal(2, engine.CurrentIndex);

        // Next at last step transitions engine to Completed
        bool nextAtLast = engine.Next();

        Assert.True(nextAtLast);
        Assert.Equal(PlayerState.Completed, engine.State);
        Assert.Equal(2, engine.CurrentIndex); // Stays at last step, does NOT overflow
        Assert.Same(steps[2], engine.CurrentStep);

        // Capability flags when Completed:
        // CanNext is false (cannot advance further)
        // CanPrevious is true (can navigate backward into the guide)
        // CanFirst is true
        // CanPlay and CanRestart are true
        Assert.False(engine.CanNext);
        Assert.True(engine.CanPrevious);
        Assert.True(engine.CanFirst);
        Assert.False(engine.CanLast);
        Assert.True(engine.CanPlay);
        Assert.True(engine.CanRestart);

        // Subsequent Next() call returns false and does not alter state
        bool nextWhenAlreadyCompleted = engine.Next();
        Assert.False(nextWhenAlreadyCompleted);
        Assert.Equal(PlayerState.Completed, engine.State);
        Assert.Equal(2, engine.CurrentIndex);
    }

    #endregion

    #region 8. Previous at First

    [Fact(DisplayName = "8. Previous at first (PreviousAtFirst)")]
    public void PreviousAtFirst()
    {
        IPlayerEngine engine = new PlayerEngine();
        var steps = CreateSampleSteps(3);
        engine.LoadGuide(Guid.NewGuid(), steps);

        Assert.Equal(0, engine.CurrentIndex);
        Assert.False(engine.CanPrevious);

        // Attempting Previous at index 0 must return false and not underflow
        bool prevResult = engine.Previous();

        Assert.False(prevResult);
        Assert.Equal(0, engine.CurrentIndex);
        Assert.Same(steps[0], engine.CurrentStep);
        Assert.Equal(PlayerState.Idle, engine.State);

        var mockLoader = new Mock<IImageLoaderService>();
        using var vm = new PlayerViewModel(engine, mockLoader.Object);
        Assert.False(vm.CanPrevious);
        Assert.False(vm.PreviousCommand.CanExecute(null));
    }

    #endregion

    #region 9. First

    [Fact(DisplayName = "9. First (First)")]
    public void First()
    {
        IPlayerEngine engine = new PlayerEngine();
        var steps = CreateSampleSteps(4);
        engine.LoadGuide(Guid.NewGuid(), steps);

        // Move to step 3
        engine.Next();
        engine.Next();
        Assert.Equal(2, engine.CurrentIndex);

        int eventCount = 0;
        int observedIndex = -1;
        engine.StepChanged += (idx, _) =>
        {
            eventCount++;
            observedIndex = idx;
        };

        // First() jumps directly to 0
        bool firstResult = engine.First();

        Assert.True(firstResult);
        Assert.Equal(0, engine.CurrentIndex);
        Assert.Same(steps[0], engine.CurrentStep);
        Assert.Equal(1, eventCount);
        Assert.Equal(0, observedIndex);
        Assert.False(engine.CanFirst);
        Assert.False(engine.CanPrevious);
        Assert.True(engine.CanNext);

        // First() from Completed state transitions back to Playing
        engine.Last();
        engine.Next(); // To Completed
        Assert.Equal(PlayerState.Completed, engine.State);

        bool firstFromCompleted = engine.First();
        Assert.True(firstFromCompleted);
        Assert.Equal(0, engine.CurrentIndex);
        Assert.Equal(PlayerState.Playing, engine.State);
    }

    #endregion

    #region 10. Last

    [Fact(DisplayName = "10. Last (Last)")]
    public void Last()
    {
        IPlayerEngine engine = new PlayerEngine();
        var steps = CreateSampleSteps(4);
        engine.LoadGuide(Guid.NewGuid(), steps);

        Assert.Equal(0, engine.CurrentIndex);

        int eventCount = 0;
        int observedIndex = -1;
        engine.StepChanged += (idx, _) =>
        {
            eventCount++;
            observedIndex = idx;
        };

        // Last() jumps directly to TotalSteps - 1
        bool lastResult = engine.Last();

        Assert.True(lastResult);
        Assert.Equal(3, engine.CurrentIndex);
        Assert.Same(steps[3], engine.CurrentStep);
        Assert.Equal(1, eventCount);
        Assert.Equal(3, observedIndex);
        Assert.False(engine.CanLast);
        Assert.True(engine.CanPrevious);
        Assert.True(engine.CanFirst);

        // Calling Last() when already at last step returns true idempotently
        bool lastAgain = engine.Last();
        Assert.True(lastAgain);
        Assert.Equal(3, engine.CurrentIndex);
    }

    #endregion

    #region 11. Restart

    [Fact(DisplayName = "11. Restart (Restart)")]
    public void Restart()
    {
        IPlayerEngine engine = new PlayerEngine();
        var steps = CreateSampleSteps(3);
        engine.LoadGuide(Guid.NewGuid(), steps);

        // Scenario 1: Restart from Paused at Step 2
        engine.Play();
        engine.Next();
        engine.Pause();
        Assert.Equal(1, engine.CurrentIndex);
        Assert.Equal(PlayerState.Paused, engine.State);

        bool restartFromPaused = engine.Restart();
        Assert.True(restartFromPaused);
        Assert.Equal(0, engine.CurrentIndex);
        Assert.Same(steps[0], engine.CurrentStep);
        Assert.Equal(PlayerState.Playing, engine.State);
        Assert.True(engine.CanPause);

        // Scenario 2: Restart from Completed
        engine.Last();
        engine.Next(); // Completed
        Assert.Equal(PlayerState.Completed, engine.State);

        bool restartFromCompleted = engine.Restart();
        Assert.True(restartFromCompleted);
        Assert.Equal(0, engine.CurrentIndex);
        Assert.Equal(PlayerState.Playing, engine.State);

        // Scenario 3: Restart from Failed
        engine.Fail("Simulated error");
        Assert.Equal(PlayerState.Failed, engine.State);

        bool restartFromFailed = engine.Restart();
        Assert.True(restartFromFailed);
        Assert.Equal(0, engine.CurrentIndex);
        Assert.Equal(PlayerState.Playing, engine.State);

        // ViewModel verification
        var mockLoader = new Mock<IImageLoaderService>();
        using var vm = new PlayerViewModel(engine, mockLoader.Object);
        Assert.Equal(PlayerState.Playing, vm.PlayerState);
        Assert.Equal("\uE769", vm.PlayPauseGlyph);
    }

    #endregion

    #region 12. Play

    [Fact(DisplayName = "12. Play (Play)")]
    public void Play()
    {
        IPlayerEngine engine = new PlayerEngine();
        var steps = CreateSampleSteps(3);
        engine.LoadGuide(Guid.NewGuid(), steps);

        Assert.Equal(PlayerState.Idle, engine.State);
        Assert.True(engine.CanPlay);

        // 12.1 Play from Idle -> Playing
        bool playResult = engine.Play();
        Assert.True(playResult);
        Assert.Equal(PlayerState.Playing, engine.State);
        Assert.True(engine.CanPause);
        Assert.False(engine.CanPlay);

        // 12.2 Calling Play while already Playing returns false (idempotent)
        bool duplicatePlay = engine.Play();
        Assert.False(duplicatePlay);
        Assert.Equal(PlayerState.Playing, engine.State);

        // 12.3 Play from Paused -> Playing
        engine.Pause();
        Assert.Equal(PlayerState.Paused, engine.State);
        bool playFromPaused = engine.Play();
        Assert.True(playFromPaused);
        Assert.Equal(PlayerState.Playing, engine.State);

        // 12.4 ViewModel glyph and tooltip reflect Playing state
        var mockLoader = new Mock<IImageLoaderService>();
        using var vm = new PlayerViewModel(engine, mockLoader.Object);
        Assert.Equal("\uE769", vm.PlayPauseGlyph);
        Assert.Equal("Пауза (Пробел)", vm.PlayPauseToolTip);
    }

    #endregion

    #region 13. Pause

    [Fact(DisplayName = "13. Pause (Pause)")]
    public void Pause()
    {
        IPlayerEngine engine = new PlayerEngine();
        var steps = CreateSampleSteps(3);
        engine.LoadGuide(Guid.NewGuid(), steps);

        // 13.1 Calling Pause while Idle returns false
        Assert.False(engine.Pause());
        Assert.Equal(PlayerState.Idle, engine.State);

        // 13.2 Play -> Pause succeeds
        engine.Play();
        Assert.Equal(PlayerState.Playing, engine.State);

        bool pauseResult = engine.Pause();
        Assert.True(pauseResult);
        Assert.Equal(PlayerState.Paused, engine.State);
        Assert.False(engine.CanPause);
        Assert.True(engine.CanPlay);

        // 13.3 Calling Pause while already Paused returns false
        bool duplicatePause = engine.Pause();
        Assert.False(duplicatePause);
        Assert.Equal(PlayerState.Paused, engine.State);

        // 13.4 Resume from Pause
        bool resumeResult = engine.Resume();
        Assert.True(resumeResult);
        Assert.Equal(PlayerState.Playing, engine.State);

        // ViewModel Play/Pause toggling
        var mockLoader = new Mock<IImageLoaderService>();
        using var vm = new PlayerViewModel(engine, mockLoader.Object);
        vm.PlayPauseCommand.Execute(null); // Toggles Playing -> Paused
        Assert.Equal(PlayerState.Paused, engine.State);
        Assert.Equal("\uE768", vm.PlayPauseGlyph);
        Assert.Equal("Воспроизведение (Пробел)", vm.PlayPauseToolTip);
    }

    #endregion

    #region 14. Complete

    [Fact(DisplayName = "14. Complete (Complete)")]
    public void Complete()
    {
        IPlayerEngine engine = new PlayerEngine();
        var steps = CreateSampleSteps(2);
        engine.LoadGuide(Guid.NewGuid(), steps);

        engine.Play();
        Assert.Equal(PlayerState.Playing, engine.State);

        // Move to step 1 (last step)
        engine.Next();
        Assert.Equal(1, engine.CurrentIndex);

        // Next on last step completes the guide
        bool completed = engine.Next();
        Assert.True(completed);
        Assert.Equal(PlayerState.Completed, engine.State);
        Assert.Equal(1, engine.CurrentIndex);
        Assert.False(engine.CanNext);

        // Previous from Completed decrements index and transitions to Playing
        bool prevFromCompleted = engine.Previous();
        Assert.True(prevFromCompleted);
        Assert.Equal(0, engine.CurrentIndex);
        Assert.Equal(PlayerState.Playing, engine.State);

        // Complete again
        engine.Next();
        engine.Next();
        Assert.Equal(PlayerState.Completed, engine.State);

        // First from Completed transitions to Playing
        bool firstFromCompleted = engine.First();
        Assert.True(firstFromCompleted);
        Assert.Equal(0, engine.CurrentIndex);
        Assert.Equal(PlayerState.Playing, engine.State);
    }

    #endregion

    #region 15. Invalid State Transition

    [Fact(DisplayName = "15. Invalid state transition (InvalidStateTransition)")]
    public void InvalidStateTransition()
    {
        // 15.1 Direct static matrix verification of invalid transitions
        Assert.False(PlayerStateMachine.IsValidTransition(PlayerState.Idle, PlayerState.Paused));
        Assert.False(PlayerStateMachine.IsValidTransition(PlayerState.Idle, PlayerState.Completed));
        Assert.False(PlayerStateMachine.IsValidTransition(PlayerState.Idle, PlayerState.Failed));
        Assert.False(PlayerStateMachine.IsValidTransition(PlayerState.Idle, PlayerState.Idle));

        Assert.False(PlayerStateMachine.IsValidTransition(PlayerState.Playing, PlayerState.Playing));
        Assert.False(PlayerStateMachine.IsValidTransition(PlayerState.Paused, PlayerState.Paused));

        Assert.False(PlayerStateMachine.IsValidTransition(PlayerState.Completed, PlayerState.Paused));
        Assert.False(PlayerStateMachine.IsValidTransition(PlayerState.Completed, PlayerState.Failed));
        Assert.False(PlayerStateMachine.IsValidTransition(PlayerState.Completed, PlayerState.Completed));

        Assert.False(PlayerStateMachine.IsValidTransition(PlayerState.Failed, PlayerState.Playing));
        Assert.False(PlayerStateMachine.IsValidTransition(PlayerState.Failed, PlayerState.Paused));
        Assert.False(PlayerStateMachine.IsValidTransition(PlayerState.Failed, PlayerState.Completed));
        Assert.False(PlayerStateMachine.IsValidTransition(PlayerState.Failed, PlayerState.Failed));

        // 15.2 State machine instance behavior
        var sm = new PlayerStateMachine();
        Assert.False(sm.TryTransition(PlayerState.Paused, out var error));
        Assert.NotNull(error);
        Assert.Contains("Invalid state transition", error);

        // TransitionOrThrow must throw InvalidOperationException
        var ex = Assert.Throws<InvalidOperationException>(() => sm.TransitionOrThrow(PlayerState.Paused));
        Assert.Contains("Invalid state transition", ex.Message);

        // 15.3 PlayerEngine safeguards against invalid commands
        IPlayerEngine engine = new PlayerEngine();
        Assert.False(engine.Pause());
        Assert.False(engine.Resume());
    }

    #endregion

    #region 16. Missing Screenshot

    [Fact(DisplayName = "16. Missing screenshot (MissingScreenshot)")]
    public async Task MissingScreenshot()
    {
        var nonExistentPath = Path.Combine(_tempTestDir, "assets", "screenshots", "missing_file_001.png");
        Assert.False(File.Exists(nonExistentPath));

        var step = CreateTestStep(1, screenshotPath: nonExistentPath);
        var steps = new List<Step> { step };

        var mockLoader = new Mock<IImageLoaderService>();
        var engine = new PlayerEngine();
        using var vm = new PlayerViewModel(engine, mockLoader.Object);
        vm.ProjectRootPath = _tempTestDir;

        // Load step with missing image
        engine.LoadGuide(Guid.NewGuid(), steps);
        await vm.LoadPreviewForStepAsync(step);

        // Error state assertions
        Assert.True(vm.IsPreviewError, "IsPreviewError must be true for missing screenshot.");
        Assert.False(vm.IsPreviewLoading, "IsPreviewLoading must be false after missing screenshot check.");
        Assert.Null(vm.PreviewImage);
        Assert.NotNull(vm.PreviewErrorMessage);
        Assert.Contains("не найден", vm.PreviewErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Visibility.Visible, vm.PreviewErrorVisibility);

        // Image loader must not be invoked for non-existent file
        mockLoader.Verify(l => l.LoadPreviewAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region 17. Corrupt Screenshot

    [Fact(DisplayName = "17. Corrupt screenshot (CorruptScreenshot)")]
    public async Task CorruptScreenshot()
    {
        // 17.1 Zero-byte file
        var emptyFile = Path.Combine(_tempTestDir, "empty_screenshot.png");
        await File.WriteAllBytesAsync(emptyFile, Array.Empty<byte>());
        Assert.True(File.Exists(emptyFile));
        Assert.Equal(0, new FileInfo(emptyFile).Length);

        // 17.2 Corrupted header file
        var corruptFile = Path.Combine(_tempTestDir, "corrupted_screenshot.png");
        await File.WriteAllBytesAsync(corruptFile, new byte[] { 0xFF, 0xD8, 0x00, 0x00, 0xDE, 0xAD });
        Assert.True(File.Exists(corruptFile));

        var stepEmpty = CreateTestStep(1, screenshotPath: emptyFile);
        var stepCorrupt = CreateTestStep(2, screenshotPath: corruptFile);
        var steps = new List<Step> { stepEmpty, stepCorrupt };

        var mockLoader = new Mock<IImageLoaderService>();
        mockLoader
            .Setup(l => l.LoadPreviewAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BitmapImage?)null);

        var engine = new PlayerEngine();
        using var vm = new PlayerViewModel(engine, mockLoader.Object);
        vm.ProjectRootPath = _tempTestDir;

        // Test empty file
        engine.LoadGuide(Guid.NewGuid(), steps);
        await vm.LoadPreviewForStepAsync(stepEmpty);

        Assert.True(vm.IsPreviewError);
        Assert.False(vm.IsPreviewLoading);
        Assert.Null(vm.PreviewImage);
        Assert.NotNull(vm.PreviewErrorMessage);
        Assert.Contains("поврежден или пуст", vm.PreviewErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Test corrupt file
        engine.Next();
        await vm.LoadPreviewForStepAsync(stepCorrupt);

        Assert.True(vm.IsPreviewError);
        Assert.False(vm.IsPreviewLoading);
        Assert.Null(vm.PreviewImage);
        Assert.Contains("поврежден или пуст", vm.PreviewErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Test RetryImageLoad command capability
        vm.RetryImageLoadCommand.Execute(null);
        await Task.Yield();

        mockLoader.Verify(l => l.LoadPreviewAsync(corruptFile, It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    #endregion

    #region 18. Rapid Navigation

    [Fact(DisplayName = "18. Rapid navigation (RapidNavigation)")]
    public async Task RapidNavigation()
    {
        // 18.1 Sequential rapid navigation token cancellation
        var steps = new List<Step>
        {
            CreateTestStep(1, CreateDummyImageFile("rapid_01.png")),
            CreateTestStep(2, CreateDummyImageFile("rapid_02.png")),
            CreateTestStep(3, CreateDummyImageFile("rapid_03.png")),
            CreateTestStep(4, CreateDummyImageFile("rapid_04.png")),
            CreateTestStep(5, CreateDummyImageFile("rapid_05.png"))
        };

        var observedTokens = new List<CancellationToken>();
        var mockLoader = new Mock<IImageLoaderService>();
        mockLoader
            .Setup(l => l.LoadPreviewAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns<string?, CancellationToken>((_, token) =>
            {
                lock (observedTokens)
                {
                    observedTokens.Add(token);
                }
                return Task.FromResult<BitmapImage?>(null);
            });

        var engine = new PlayerEngine();
        using var vm = new PlayerViewModel(engine, mockLoader.Object);
        vm.ProjectRootPath = _tempTestDir;

        engine.LoadGuide(Guid.NewGuid(), steps);

        // Rapid step advancement
        engine.Next();
        engine.Next();
        engine.Next();
        engine.Next();

        await Task.Yield();

        Assert.Equal(4, vm.CurrentIndex);
        Assert.Equal("Step 5", vm.CurrentStepTitle);
        Assert.True(observedTokens.Count >= 5);
        Assert.True(observedTokens[0].IsCancellationRequested, "Step 1 preview must be cancelled.");
        Assert.True(observedTokens[1].IsCancellationRequested, "Step 2 preview must be cancelled.");
        Assert.True(observedTokens[2].IsCancellationRequested, "Step 3 preview must be cancelled.");
        Assert.True(observedTokens[3].IsCancellationRequested, "Step 4 preview must be cancelled.");
        Assert.False(observedTokens[4].IsCancellationRequested, "Active Step 5 preview must not be cancelled.");

        // 18.2 Concurrent stress test: multiple threads hammering navigation
        var concurrentEngine = new PlayerEngine();
        concurrentEngine.LoadGuide(Guid.NewGuid(), steps);

        var tasks = new List<Task>();
        for (int i = 0; i < 20; i++)
        {
            int threadId = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 50; j++)
                {
                    if (threadId % 2 == 0)
                    {
                        concurrentEngine.Next();
                    }
                    else
                    {
                        concurrentEngine.Previous();
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Invariants must remain intact after heavy concurrency
        Assert.True(concurrentEngine.CurrentIndex >= 0 && concurrentEngine.CurrentIndex < concurrentEngine.TotalSteps);
        Assert.NotNull(concurrentEngine.CurrentStep);
    }

    #endregion

    #region 19. Cancellation

    [Fact(DisplayName = "19. Cancellation (Cancellation)")]
    public async Task Cancellation()
    {
        var step1Image = CreateDummyImageFile("cancel_01.png");
        var step2Image = CreateDummyImageFile("cancel_02.png");

        var step1 = CreateTestStep(1, step1Image);
        var step2 = CreateTestStep(2, step2Image);
        var steps = new List<Step> { step1, step2 };

        var step1Tcs = new TaskCompletionSource<BitmapImage?>();
        var observedTokens = new List<CancellationToken>();

        var mockLoader = new Mock<IImageLoaderService>();
        mockLoader
            .Setup(l => l.LoadPreviewAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns<string?, CancellationToken>((path, token) =>
            {
                lock (observedTokens)
                {
                    observedTokens.Add(token);
                }

                if (path == step1Image)
                {
                    token.Register(() => step1Tcs.TrySetCanceled(token));
                    return step1Tcs.Task;
                }

                return Task.FromResult<BitmapImage?>(null);
            });

        var engine = new PlayerEngine();
        var vm = new PlayerViewModel(engine, mockLoader.Object);
        vm.ProjectRootPath = _tempTestDir;

        // 19.1 Active loading cancellation on navigation
        engine.LoadGuide(Guid.NewGuid(), steps);
        Assert.Equal(0, vm.CurrentIndex);

        var tokenForStep1 = observedTokens.Last();
        Assert.False(tokenForStep1.IsCancellationRequested);

        // Advancing to step 2 must cancel step 1's token
        engine.Next();
        await Task.Yield();

        Assert.Equal(1, vm.CurrentIndex);
        Assert.True(tokenForStep1.IsCancellationRequested, "Prior token must be canceled when switching to step 2.");

        // Completing step 1's cancelled task must not affect current step
        step1Tcs.TrySetResult(null);
        await Task.Yield();
        Assert.Equal(1, vm.CurrentIndex);

        // 19.2 Cancellation on ViewModel disposal
        var activeCts = vm.ActivePreviewCts;
        Assert.NotNull(activeCts);

        vm.Dispose();

        // Disposed ViewModel cancels CTS and unhooks from engine events
        Assert.True(activeCts.IsCancellationRequested);

        // Subsequent engine event does not throw or mutate disposed VM
        engine.Previous();
        Assert.Equal(0, engine.CurrentIndex);
    }

    #endregion

    #region 20. Player Read-Only Behavior

    [Fact(DisplayName = "20. Player read-only behavior (PlayerReadOnlyBehavior)")]
    public async Task PlayerReadOnlyBehavior()
    {
        // 20.1 In-Memory Step Objects Immutability
        var originalSteps = CreateSampleSteps(4);
        var initialSnapshots = originalSteps.Select(s => new
        {
            s.Id,
            s.SequenceIndex,
            s.Timestamp,
            s.Action,
            s.ClickX,
            s.ClickY,
            s.ScreenshotPath,
            s.Title,
            s.Description,
            ElemName = s.TargetElement.Name,
            ElemType = s.TargetElement.ControlType,
            ElemAutoId = s.TargetElement.AutomationId,
            ElemClass = s.TargetElement.ClassName,
            ElemProc = s.TargetElement.ProcessName,
            ElemPid = s.TargetElement.ProcessId,
            ElemHwnd = s.TargetElement.WindowHandle,
            ElemWinTitle = s.TargetElement.WindowTitle,
            ElemBBox = s.TargetElement.BoundingRectangle
        }).ToList();

        var engine = new PlayerEngine();
        engine.LoadGuide(Guid.NewGuid(), originalSteps);

        // Exhaustive playback navigation
        engine.Play();
        engine.Next();
        engine.Next();
        engine.Previous();
        engine.Last();
        engine.First();
        engine.Pause();
        engine.Resume();
        engine.Restart();
        engine.Fail("Simulated error");
        engine.Unload();

        // Verify in-memory step models were strictly untouched
        for (int i = 0; i < originalSteps.Count; i++)
        {
            var s = originalSteps[i];
            var snap = initialSnapshots[i];

            Assert.Equal(snap.Id, s.Id);
            Assert.Equal(snap.SequenceIndex, s.SequenceIndex);
            Assert.Equal(snap.Timestamp, s.Timestamp);
            Assert.Equal(snap.Action, s.Action);
            Assert.Equal(snap.ClickX, s.ClickX);
            Assert.Equal(snap.ClickY, s.ClickY);
            Assert.Equal(snap.ScreenshotPath, s.ScreenshotPath);
            Assert.Equal(snap.Title, s.Title);
            Assert.Equal(snap.Description, s.Description);
            Assert.Equal(snap.ElemName, s.TargetElement.Name);
            Assert.Equal(snap.ElemType, s.TargetElement.ControlType);
            Assert.Equal(snap.ElemAutoId, s.TargetElement.AutomationId);
            Assert.Equal(snap.ElemClass, s.TargetElement.ClassName);
            Assert.Equal(snap.ElemProc, s.TargetElement.ProcessName);
            Assert.Equal(snap.ElemPid, s.TargetElement.ProcessId);
            Assert.Equal(snap.ElemHwnd, s.TargetElement.WindowHandle);
            Assert.Equal(snap.ElemWinTitle, s.TargetElement.WindowTitle);
            Assert.Equal(snap.ElemBBox, s.TargetElement.BoundingRectangle);
        }

        // 20.2 SQLite Database Safety
        var projectDir = Path.Combine(_tempTestDir, "ReadOnlyDatabaseProject");
        Directory.CreateDirectory(projectDir);

        using (var repo = new ProjectRepository(projectDir))
        {
            repo.CreateProject("Read Only Verification Guide", "Testing that playback never mutates SQLite database.");
            for (int i = 1; i <= 3; i++)
            {
                repo.SaveStep(CreateTestStep(i, $"assets/screenshots/step_{i:D3}.png", $"Step Title {i}", $"Step Desc {i}"));
            }
        }

        // Read snapshot of steps from SQLite
        IReadOnlyList<Step> beforePlaybackSteps;
        using (var verifyRepo = new ProjectRepository(projectDir))
        {
            beforePlaybackSteps = verifyRepo.LoadSteps();
        }
        Assert.Equal(3, beforePlaybackSteps.Count);

        // Run full player session via ViewModel against the database
        var mockLoader = new Mock<IImageLoaderService>();
        var playerEngine = new PlayerEngine();
        using (var vm = new PlayerViewModel(playerEngine, mockLoader.Object))
        {
            await vm.InitializeAsync(projectDir);

            // Trigger various commands
            vm.PlayPauseCommand.Execute(null);
            vm.NextCommand.Execute(null);
            vm.NextCommand.Execute(null);
            vm.PreviousCommand.Execute(null);
            vm.FirstCommand.Execute(null);
            vm.LastCommand.Execute(null);
            vm.RestartCommand.Execute(null);
            vm.ToggleHighlightOverlayCommand.Execute(null);
            vm.ToggleMetadataCommand.Execute(null);
            vm.RetryImageLoadCommand.Execute(null);
        }

        // Read steps from SQLite after playback
        IReadOnlyList<Step> afterPlaybackSteps;
        using (var verifyRepo = new ProjectRepository(projectDir))
        {
            afterPlaybackSteps = verifyRepo.LoadSteps();
        }

        // Assert 100% database immutability
        Assert.Equal(beforePlaybackSteps.Count, afterPlaybackSteps.Count);
        for (int i = 0; i < beforePlaybackSteps.Count; i++)
        {
            var b = beforePlaybackSteps[i];
            var a = afterPlaybackSteps[i];

            Assert.Equal(b.Id, a.Id);
            Assert.Equal(b.SequenceIndex, a.SequenceIndex);
            Assert.Equal(b.Timestamp, a.Timestamp);
            Assert.Equal(b.Action, a.Action);
            Assert.Equal(b.ClickX, a.ClickX);
            Assert.Equal(b.ClickY, a.ClickY);
            Assert.Equal(b.ScreenshotPath, a.ScreenshotPath);
            Assert.Equal(b.Title, a.Title);
            Assert.Equal(b.Description, a.Description);
            Assert.Equal(b.TargetElement.Name, a.TargetElement.Name);
            Assert.Equal(b.TargetElement.ControlType, a.TargetElement.ControlType);
            Assert.Equal(b.TargetElement.AutomationId, a.TargetElement.AutomationId);
            Assert.Equal(b.TargetElement.ClassName, a.TargetElement.ClassName);
            Assert.Equal(b.TargetElement.ProcessName, a.TargetElement.ProcessName);
            Assert.Equal(b.TargetElement.ProcessId, a.TargetElement.ProcessId);
            Assert.Equal(b.TargetElement.WindowHandle, a.TargetElement.WindowHandle);
            Assert.Equal(b.TargetElement.WindowTitle, a.TargetElement.WindowTitle);
            Assert.Equal(b.TargetElement.BoundingRectangle, a.TargetElement.BoundingRectangle);
        }
    }

    #endregion
}
