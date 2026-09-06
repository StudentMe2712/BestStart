using System;
using System.Collections.Generic;
using Moq;
using Stepwise.App.Services;
using Stepwise.App.ViewModels;
using Stepwise.Core.Engine;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Stepwise.WindowsIntegration.Overlay;
using Xunit;

namespace Stepwise.Tests;

/// <summary>
/// Интеграционные тесты взаимодействия подсистемы оверлея (OverlayService) с состоянием плеера (PlayerViewModel).
/// Проверяет:
/// 1. Жизненный цикл OverlayService (Hidden -> Showing -> Visible -> Hiding -> Hidden, Visible -> Updating -> Visible).
/// 2. 25 циклов переключения Toggle без утечек и исключений.
/// 3. Синхронизацию при быстром переключении шагов (Step 1 -> Step 2 -> Step 3).
/// 4. Валидацию цели (missing target, invalid HWND, empty bounding box).
/// 5. Независимость плеера (навигация работает при null или выбрасывающем исключения IOverlayService).
/// 6. Реакцию на смену состояния плеера (Failed скрывает оверлей).
/// 7. Скрытие оверлея при закрытии и освобождении ViewModel.
/// </summary>
public sealed class OverlayIntegrationTests
{
    [Fact]
    public void OverlayService_Lifecycle_TransitionsThroughAllStatesCorrectly()
    {
        var fakeWindow = new FakeOverlayWindow();
        using var service = new OverlayService(fakeWindow);

        Assert.Equal(OverlayState.Hidden, service.State);
        Assert.False(service.IsVisible);
        Assert.True(service.IsEnabled);

        var transitions = new List<(OverlayState OldState, OverlayState NewState)>();
        service.StateChanged += (o, n) => transitions.Add((o, n));

        // Show: Hidden -> Showing -> Visible
        service.Show();
        Assert.Equal(OverlayState.Visible, service.State);
        Assert.True(service.IsVisible);
        Assert.True(fakeWindow.IsWindowVisible);
        Assert.Equal(1, fakeWindow.ShowWindowCallCount);

        // Hide: Visible -> Hiding -> Hidden
        service.Hide();
        Assert.Equal(OverlayState.Hidden, service.State);
        Assert.False(service.IsVisible);
        Assert.False(fakeWindow.IsWindowVisible);
        Assert.Equal(1, fakeWindow.HideWindowCallCount);

        Assert.Equal(4, transitions.Count);
        Assert.Equal((OverlayState.Hidden, OverlayState.Showing), transitions[0]);
        Assert.Equal((OverlayState.Showing, OverlayState.Visible), transitions[1]);
        Assert.Equal((OverlayState.Visible, OverlayState.Hiding), transitions[2]);
        Assert.Equal((OverlayState.Hiding, OverlayState.Hidden), transitions[3]);
    }

    [Fact]
    public void OverlayService_Toggle_25Cycles_SucceedsWithoutErrorsOrLeaks()
    {
        var fakeWindow = new FakeOverlayWindow();
        using var service = new OverlayService(fakeWindow);

        var transitions = new List<(OverlayState OldState, OverlayState NewState)>();
        service.StateChanged += (o, n) => transitions.Add((o, n));

        for (int i = 0; i < 25; i++)
        {
            service.Toggle(); // OFF -> ON
            Assert.Equal(OverlayState.Visible, service.State);
            Assert.True(service.IsVisible);
            Assert.True(fakeWindow.IsWindowVisible);

            service.Toggle(); // ON -> OFF
            Assert.Equal(OverlayState.Hidden, service.State);
            Assert.False(service.IsVisible);
            Assert.False(fakeWindow.IsWindowVisible);
        }

        Assert.Equal(25, fakeWindow.ShowWindowCallCount);
        Assert.Equal(25, fakeWindow.HideWindowCallCount);
        Assert.Equal(100, transitions.Count); // 25 * 4 transitions
    }

    [Fact]
    public void OverlayService_RealNativeOverlayWindow_Toggle_25Cycles_WithoutErrors()
    {
        using var realWindow = new NativeOverlayWindow();
        using var service = new OverlayService(realWindow);

        for (int i = 0; i < 25; i++)
        {
            service.Toggle();
            Assert.Equal(OverlayState.Visible, service.State);
            Assert.True(service.IsVisible);

            service.Toggle();
            Assert.Equal(OverlayState.Hidden, service.State);
            Assert.False(service.IsVisible);
        }
    }

    [Fact]
    public void OverlayService_StepUpdateSynchronization_UpdatesTargetToLatestStep()
    {
        var fakeWindow = new FakeOverlayWindow();
        using var service = new OverlayService(fakeWindow);
        service.Show();

        var step1 = CreateStep(1, "Step 1", 100, 100, 80, 40);
        var step2 = CreateStep(2, "Step 2", 200, 200, 90, 50);
        var step3 = CreateStep(3, "Step 3", 300, 300, 120, 60);

        var updatingTransitions = new List<(OverlayState, OverlayState)>();
        service.StateChanged += (o, n) =>
        {
            if ((o == OverlayState.Visible && n == OverlayState.Updating) ||
                (o == OverlayState.Updating && n == OverlayState.Visible))
            {
                updatingTransitions.Add((o, n));
            }
        };

        service.UpdateTarget(step1);
        Assert.Equal("Step 1", fakeWindow.LastTarget?.Title);
        Assert.Equal(new BoundingBox(100, 100, 80, 40), fakeWindow.LastTarget?.BoundingRectangle);
        Assert.False(fakeWindow.LastIsMissingTarget);

        service.UpdateTarget(step2);
        Assert.Equal("Step 2", fakeWindow.LastTarget?.Title);
        Assert.Equal(new BoundingBox(200, 200, 90, 50), fakeWindow.LastTarget?.BoundingRectangle);
        Assert.False(fakeWindow.LastIsMissingTarget);

        service.UpdateTarget(step3);
        Assert.Equal("Step 3", fakeWindow.LastTarget?.Title);
        Assert.Equal(new BoundingBox(300, 300, 120, 60), fakeWindow.LastTarget?.BoundingRectangle);
        Assert.False(fakeWindow.LastIsMissingTarget);

        Assert.Equal(6, updatingTransitions.Count); // 3 обновления * 2 перехода
        Assert.Equal(OverlayState.Visible, service.State);
    }

    [Fact]
    public void OverlayService_RapidStepSwitching_SetsFinalTargetWithoutStaleHighlights()
    {
        var fakeWindow = new FakeOverlayWindow();
        using var service = new OverlayService(fakeWindow);
        service.Show();

        var step1 = CreateStep(1, "Step 1", 100, 100, 80, 40);
        var step2 = CreateStep(2, "Step 2", 200, 200, 90, 50);
        var step3 = CreateStep(3, "Step 3", 300, 300, 120, 60);
        var step4 = CreateStep(4, "Step 4", 400, 400, 150, 70);

        // Быстрое переключение 1 -> 2 -> 3 -> 4
        service.UpdateTarget(step1);
        service.UpdateTarget(step2);
        service.UpdateTarget(step3);
        service.UpdateTarget(step4);

        Assert.Equal("Step 4", fakeWindow.LastTarget?.Title);
        Assert.Equal(new BoundingBox(400, 400, 150, 70), fakeWindow.LastTarget?.BoundingRectangle);
        Assert.Equal("Step 4", service.CurrentTarget?.Title);
        Assert.False(fakeWindow.LastIsMissingTarget);
    }

    [Fact]
    public void OverlayService_TargetValidation_IdentifiesMissingTarget_InvalidHwnd_EmptyBoundingBox()
    {
        var fakeWindow = new FakeOverlayWindow();
        using var service = new OverlayService(fakeWindow);
        service.Show();

        // 1. Missing target (null)
        service.UpdateTarget((OverlayTargetInfo?)null);
        Assert.True(fakeWindow.LastIsMissingTarget);

        // 2. Empty OverlayTargetInfo
        service.UpdateTarget(OverlayTargetInfo.Empty);
        Assert.True(fakeWindow.LastIsMissingTarget);

        // 3. Target with empty bounding box (0x0)
        var emptyBoxTarget = new OverlayTargetInfo(
            BoundingRectangle: new BoundingBox(100, 100, 0, 0),
            WindowHandle: 0,
            Title: "EmptyBox",
            Description: "Desc",
            ClickX: 100,
            ClickY: 100,
            IsTargetValid: true
        );
        service.UpdateTarget(emptyBoxTarget);
        Assert.True(fakeWindow.LastIsMissingTarget);

        // 4. Target with negative width/height
        var negativeBoxTarget = new OverlayTargetInfo(
            BoundingRectangle: new BoundingBox(100, 100, -10, 50),
            WindowHandle: 0,
            Title: "NegativeBox",
            Description: "Desc",
            ClickX: 100,
            ClickY: 100,
            IsTargetValid: true
        );
        service.UpdateTarget(negativeBoxTarget);
        Assert.True(fakeWindow.LastIsMissingTarget);

        // 5. Invalid HWND (non-existent window handle like 0x7FFFFFFF)
        var invalidHwndTarget = new OverlayTargetInfo(
            BoundingRectangle: new BoundingBox(100, 100, 150, 80),
            WindowHandle: 0x7FFFFFFF,
            Title: "InvalidHwnd",
            Description: "Desc",
            ClickX: 120,
            ClickY: 120,
            IsTargetValid: true
        );
        service.UpdateTarget(invalidHwndTarget);
        Assert.True(fakeWindow.LastIsMissingTarget);

        // 6. Valid target with WindowHandle = 0 (valid coordinates on screen)
        var validTarget = new OverlayTargetInfo(
            BoundingRectangle: new BoundingBox(100, 100, 200, 80),
            WindowHandle: 0,
            Title: "ValidTarget",
            Description: "Valid",
            ClickX: 150,
            ClickY: 130,
            IsTargetValid: true
        );
        service.UpdateTarget(validTarget);
        Assert.False(fakeWindow.LastIsMissingTarget);
    }

    [Fact]
    public void PlayerIndependence_NavigationWorks_WhenOverlayServiceIsNull()
    {
        var engine = new PlayerEngine();
        var mockLoader = new Mock<IImageLoaderService>();
        var steps = CreateTestSteps(3);
        engine.LoadGuide(Guid.NewGuid(), steps);

        using var vm = new PlayerViewModel(
            engine,
            mockLoader.Object,
            repository: null,
            dispatcherQueue: null,
            overlayService: null
        );

        Assert.Equal(0, vm.CurrentIndex);
        Assert.True(vm.CanNext);

        vm.NextCommand.Execute(null);
        Assert.Equal(1, vm.CurrentIndex);

        vm.NextCommand.Execute(null);
        Assert.Equal(2, vm.CurrentIndex);

        vm.PreviousCommand.Execute(null);
        Assert.Equal(1, vm.CurrentIndex);

        vm.FirstCommand.Execute(null);
        Assert.Equal(0, vm.CurrentIndex);

        vm.LastCommand.Execute(null);
        Assert.Equal(2, vm.CurrentIndex);

        vm.RestartCommand.Execute(null);
        Assert.Equal(0, vm.CurrentIndex);
    }

    [Fact]
    public void PlayerIndependence_NavigationWorks_WhenOverlayServiceThrowsExceptions()
    {
        var engine = new PlayerEngine();
        var mockLoader = new Mock<IImageLoaderService>();
        var steps = CreateTestSteps(3);
        engine.LoadGuide(Guid.NewGuid(), steps);

        var mockOverlay = new Mock<IOverlayService>();
        mockOverlay.Setup(m => m.Show()).Throws(new InvalidOperationException("Overlay Show failed!"));
        mockOverlay.Setup(m => m.Hide()).Throws(new InvalidOperationException("Overlay Hide failed!"));
        mockOverlay.Setup(m => m.Toggle()).Throws(new InvalidOperationException("Overlay Toggle failed!"));
        mockOverlay.Setup(m => m.UpdateTarget(It.IsAny<Step?>())).Throws(new InvalidOperationException("Overlay UpdateTarget failed!"));
        mockOverlay.Setup(m => m.UpdateTarget(It.IsAny<OverlayTargetInfo?>())).Throws(new InvalidOperationException("Overlay UpdateTarget failed!"));

        using var vm = new PlayerViewModel(
            engine,
            mockLoader.Object,
            repository: null,
            dispatcherQueue: null,
            overlayService: mockOverlay.Object
        );

        // All navigation and actions must complete without unhandled exceptions
        var ex = Record.Exception(() =>
        {
            vm.NextCommand.Execute(null);
            vm.NextCommand.Execute(null);
            vm.PreviousCommand.Execute(null);
            vm.FirstCommand.Execute(null);
            vm.LastCommand.Execute(null);
            vm.RestartCommand.Execute(null);
            vm.ToggleHighlightOverlayCommand.Execute(null);
            vm.CloseCommand.Execute(null);
        });

        Assert.Null(ex);
        Assert.Equal(0, vm.CurrentIndex);
    }

    [Fact]
    public void PlayerViewModel_ShowHighlightOverlay_SynchronizesWithOverlayService()
    {
        var engine = new PlayerEngine();
        var mockLoader = new Mock<IImageLoaderService>();
        var steps = CreateTestSteps(3);
        engine.LoadGuide(Guid.NewGuid(), steps);

        var mockOverlay = new Mock<IOverlayService>();
        using var vm = new PlayerViewModel(
            engine,
            mockLoader.Object,
            repository: null,
            dispatcherQueue: null,
            overlayService: mockOverlay.Object
        );

        // Toggle from false to true -> Show() and UpdateTarget()
        vm.ShowHighlightOverlay = true;
        mockOverlay.Verify(m => m.Show(), Times.Once);
        mockOverlay.Verify(m => m.UpdateTarget(It.IsAny<Step?>()), Times.AtLeastOnce);

        // Toggle from true to false -> Hide()
        vm.ShowHighlightOverlay = false;
        mockOverlay.Verify(m => m.Hide(), Times.AtLeastOnce);
    }

    [Fact]
    public void PlayerViewModel_StateChangedToFailed_HidesOverlay()
    {
        var engine = new PlayerEngine();
        var mockLoader = new Mock<IImageLoaderService>();
        var mockOverlay = new Mock<IOverlayService>();
        using var vm = new PlayerViewModel(
            engine,
            mockLoader.Object,
            repository: null,
            dispatcherQueue: null,
            overlayService: mockOverlay.Object
        );

        engine.Fail("Simulated catastrophic playback failure");

        mockOverlay.Verify(m => m.Hide(), Times.AtLeastOnce);
    }

    [Fact]
    public void PlayerViewModel_CloseAndDispose_HidesOverlay()
    {
        var engine = new PlayerEngine();
        var mockLoader = new Mock<IImageLoaderService>();
        var mockOverlay = new Mock<IOverlayService>();
        var vm = new PlayerViewModel(
            engine,
            mockLoader.Object,
            repository: null,
            dispatcherQueue: null,
            overlayService: mockOverlay.Object
        );

        vm.CloseCommand.Execute(null);
        mockOverlay.Verify(m => m.Hide(), Times.Once);

        vm.Dispose();
        mockOverlay.Verify(m => m.Hide(), Times.AtLeast(2));
    }

    [Fact]
    public void OverlayService_ActiveWindowTracker_TriggersTargetReverification()
    {
        var fakeWindow = new FakeOverlayWindow();
        var fakeTracker = new FakeActiveWindowTracker();
        using var service = new OverlayService(fakeWindow, windowTracker: fakeTracker);

        service.Show();

        var target = new OverlayTargetInfo(
            BoundingRectangle: new BoundingBox(100, 100, 100, 50),
            WindowHandle: 12345,
            Title: "Tracked Window",
            Description: "Test",
            ClickX: 110,
            ClickY: 110,
            IsTargetValid: true
        );
        service.UpdateTarget(target);

        int previousUpdateCount = fakeWindow.UpdateVisualsCallCount;

        fakeTracker.RaiseActiveWindowChanged(new ActiveWindowInfo(
            WindowHandle: 99999,
            ProcessId: 100,
            ProcessName: "other",
            WindowTitle: "Other App",
            Bounds: new BoundingBox(0, 0, 800, 600),
            Timestamp: DateTime.UtcNow
        ));

        Assert.True(fakeWindow.UpdateVisualsCallCount > previousUpdateCount);
    }

    // --- Вспомогательные классы и методы ---

    private static Step CreateStep(int index, string title, double x, double y, double width, double height)
    {
        var element = new ElementInfo(
            Name: title,
            ControlType: "Button",
            AutomationId: $"btn_{index}",
            ClassName: "Button",
            ProcessName: "TestApp",
            ProcessId: 1234,
            WindowTitle: "Test Window",
            WindowHandle: 0,
            BoundingRectangle: new BoundingBox(x, y, width, height)
        );

        return new Step(
            Id: Guid.NewGuid(),
            SequenceIndex: index,
            Timestamp: DateTime.UtcNow,
            Action: ActionType.LeftClick,
            ClickX: x + width / 2.0,
            ClickY: y + height / 2.0,
            TargetElement: element,
            Title: title,
            Description: $"Description for {title}"
        );
    }

    private static List<Step> CreateTestSteps(int count)
    {
        var list = new List<Step>();
        for (int i = 0; i < count; i++)
        {
            list.Add(CreateStep(i + 1, $"Шаг {i + 1}", 100 + i * 50, 100 + i * 50, 120, 40));
        }
        return list;
    }

    private sealed class FakeOverlayWindow : IOverlayWindow, IDisposable
    {
        public bool IsWindowVisible { get; set; }
        public int ShowWindowCallCount { get; private set; }
        public int HideWindowCallCount { get; private set; }
        public int CloseWindowCallCount { get; private set; }
        public int DisposeCallCount { get; private set; }
        public int UpdateVisualsCallCount { get; private set; }

        public OverlayTargetInfo? LastTarget { get; private set; }
        public CalloutPosition? LastCallout { get; private set; }
        public bool LastIsMissingTarget { get; private set; }

        public void SetBounds(int x, int y, int width, int height) { }

        public void ShowWindow()
        {
            ShowWindowCallCount++;
            IsWindowVisible = true;
        }

        public void HideWindow()
        {
            HideWindowCallCount++;
            IsWindowVisible = false;
        }

        public void CloseWindow()
        {
            CloseWindowCallCount++;
            IsWindowVisible = false;
        }

        public void UpdateVisuals(OverlayTargetInfo? target, CalloutPosition? callout, bool isMissingTarget)
        {
            UpdateVisualsCallCount++;
            LastTarget = target;
            LastCallout = callout;
            LastIsMissingTarget = isMissingTarget;
        }

        public void Dispose()
        {
            DisposeCallCount++;
        }
    }

    private sealed class FakeActiveWindowTracker : IActiveWindowTracker
    {
        public bool IsRunning { get; private set; }
        public event EventHandler<ActiveWindowInfo>? ActiveWindowChanged;

        public void Start() => IsRunning = true;
        public void Stop() => IsRunning = false;
        public ActiveWindowInfo? GetActiveWindow() => null;
        public void RaiseActiveWindowChanged(ActiveWindowInfo info) => ActiveWindowChanged?.Invoke(this, info);
        public void Dispose() { }
    }
}
