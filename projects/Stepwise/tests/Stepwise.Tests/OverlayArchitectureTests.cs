using System;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Stepwise.Core.Policy;
using Xunit;

namespace Stepwise.Tests;

/// <summary>
/// Тесты архитектурных контрактов, моделей и алгоритмов позиционирования оверлея (Phase 5 Stage 2).
/// </summary>
public class OverlayArchitectureTests
{
    private static readonly BoundingBox Screen1080P = new(0, 0, 1920, 1080);
    private static readonly BoundingBox MultiMonitorScreen = new(-1920, 0, 3840, 1080);

    [Fact]
    public void OverlayState_ShouldContainAllExpectedStates()
    {
        Assert.Equal(6, Enum.GetValues<OverlayState>().Length);
        Assert.True(Enum.IsDefined(typeof(OverlayState), OverlayState.Hidden));
        Assert.True(Enum.IsDefined(typeof(OverlayState), OverlayState.Showing));
        Assert.True(Enum.IsDefined(typeof(OverlayState), OverlayState.Visible));
        Assert.True(Enum.IsDefined(typeof(OverlayState), OverlayState.Updating));
        Assert.True(Enum.IsDefined(typeof(OverlayState), OverlayState.Hiding));
        Assert.True(Enum.IsDefined(typeof(OverlayState), OverlayState.Failed));
    }

    [Fact]
    public void CalloutPlacement_ShouldContainAllExpectedPlacements()
    {
        Assert.Equal(4, Enum.GetValues<CalloutPlacement>().Length);
        Assert.True(Enum.IsDefined(typeof(CalloutPlacement), CalloutPlacement.Right));
        Assert.True(Enum.IsDefined(typeof(CalloutPlacement), CalloutPlacement.Left));
        Assert.True(Enum.IsDefined(typeof(CalloutPlacement), CalloutPlacement.Below));
        Assert.True(Enum.IsDefined(typeof(CalloutPlacement), CalloutPlacement.Above));
    }

    [Fact]
    public void OverlayTargetInfo_Empty_ShouldHaveExpectedDefaults()
    {
        var target = OverlayTargetInfo.Empty;

        Assert.Equal(BoundingBox.Empty, target.BoundingRectangle);
        Assert.Equal(0, target.WindowHandle);
        Assert.Null(target.Title);
        Assert.Null(target.Description);
        Assert.Equal(0.0, target.ClickX);
        Assert.Equal(0.0, target.ClickY);
        Assert.False(target.IsTargetValid);
    }

    [Fact]
    public void OverlayTargetInfo_FromStep_WithNullStep_ShouldReturnEmpty()
    {
        var target = OverlayTargetInfo.FromStep(null);

        Assert.False(target.IsTargetValid);
        Assert.Equal(BoundingBox.Empty, target.BoundingRectangle);
        Assert.Equal(0, target.WindowHandle);
    }

    [Fact]
    public void OverlayTargetInfo_FromStep_WithValidStep_ShouldPopulateFields()
    {
        var element = new ElementInfo(
            Name: "SubmitButton",
            ControlType: "Button",
            AutomationId: "btnSubmit",
            ClassName: "Button",
            ProcessName: "app",
            ProcessId: 100,
            WindowTitle: "AppWindow",
            WindowHandle: 12345678,
            BoundingRectangle: new BoundingBox(100, 200, 80, 30)
        );

        var step = new Step(
            Id: Guid.NewGuid(),
            SequenceIndex: 1,
            Timestamp: DateTime.UtcNow,
            Action: ActionType.LeftClick,
            ClickX: 140,
            ClickY: 215,
            TargetElement: element,
            Title: "Нажмите 'Submit'",
            Description: "Отправка формы"
        );

        var target = OverlayTargetInfo.FromStep(step);

        Assert.True(target.IsTargetValid);
        Assert.Equal(new BoundingBox(100, 200, 80, 30), target.BoundingRectangle);
        Assert.Equal(12345678, target.WindowHandle);
        Assert.Equal("Нажмите 'Submit'", target.Title);
        Assert.Equal("Отправка формы", target.Description);
        Assert.Equal(140.0, target.ClickX);
        Assert.Equal(215.0, target.ClickY);
    }

    [Fact]
    public void CalloutPositionCalculator_Priority1_Right_WhenFitsOnRight()
    {
        // Target: X=200, Y=100, W=100, H=50. Callout: W=200, H=80, Margin=12
        // Right candidate: 200 + 100 + 12 = 312. 312 + 200 = 512 <= 1920 (Fits)
        var target = new BoundingBox(200, 100, 100, 50);
        var pos = CalloutPositionCalculator.Calculate(target, 200, 80, Screen1080P, margin: 12);

        Assert.Equal(CalloutPlacement.Right, pos.Placement);
        Assert.Equal(312.0, pos.X);
        Assert.Equal(100.0, pos.Y);
        Assert.Equal(200.0, pos.Width);
        Assert.Equal(80.0, pos.Height);
    }

    [Fact]
    public void CalloutPositionCalculator_Priority2_Left_WhenRightOverflowsButLeftFits()
    {
        // Target: X=1700, Y=100, W=150, H=50. Callout: W=200, H=80, Margin=12
        // Right candidate: 1700 + 150 + 12 = 1862. 1862 + 200 = 2062 > 1920 (Does not fit)
        // Left candidate: 1700 - 200 - 12 = 1488. 1488 >= 0 (Fits)
        var target = new BoundingBox(1700, 100, 150, 50);
        var pos = CalloutPositionCalculator.Calculate(target, 200, 80, Screen1080P, margin: 12);

        Assert.Equal(CalloutPlacement.Left, pos.Placement);
        Assert.Equal(1488.0, pos.X);
        Assert.Equal(100.0, pos.Y);
    }

    [Fact]
    public void CalloutPositionCalculator_Priority3_Below_WhenHorizontalFailsButBelowFits()
    {
        // Wide target: X=50, Y=100, W=1850, H=60. Callout: W=200, H=80, Margin=12
        // Right: 50 + 1850 + 12 = 1912. 1912 + 200 = 2112 > 1920 (Fails)
        // Left: 50 - 200 - 12 = -162 < 0 (Fails)
        // Below: 100 + 60 + 12 = 172. 172 + 80 = 252 <= 1080 (Fits)
        var target = new BoundingBox(50, 100, 1850, 60);
        var pos = CalloutPositionCalculator.Calculate(target, 200, 80, Screen1080P, margin: 12);

        Assert.Equal(CalloutPlacement.Below, pos.Placement);
        Assert.Equal(50.0, pos.X);
        Assert.Equal(172.0, pos.Y);
    }

    [Fact]
    public void CalloutPositionCalculator_Priority4_Above_WhenRightLeftBelowFailButAboveFits()
    {
        // Wide target near bottom: X=50, Y=950, W=1850, H=100. Callout: W=200, H=80, Margin=12
        // Right: fails
        // Left: fails
        // Below: 950 + 100 + 12 = 1062. 1062 + 80 = 1142 > 1080 (Fails)
        // Above: 950 - 80 - 12 = 858 >= 0 (Fits)
        var target = new BoundingBox(50, 950, 1850, 100);
        var pos = CalloutPositionCalculator.Calculate(target, 200, 80, Screen1080P, margin: 12);

        Assert.Equal(CalloutPlacement.Above, pos.Placement);
        Assert.Equal(50.0, pos.X);
        Assert.Equal(858.0, pos.Y);
    }

    [Fact]
    public void CalloutPositionCalculator_Clamping_NeverPlacesOutsideVirtualScreen()
    {
        // Target near bottom right corner: X=1850, Y=1030, W=60, H=40. Callout: W=250, H=100
        var target = new BoundingBox(1850, 1030, 60, 40);
        var pos = CalloutPositionCalculator.Calculate(target, 250, 100, Screen1080P, margin: 12);

        // Position must be strictly clamped within screen bounds:
        // X <= 1920 - 250 = 1670, Y <= 1080 - 100 = 980
        Assert.True(pos.X >= 0.0);
        Assert.True(pos.X <= 1920 - 250);
        Assert.True(pos.Y >= 0.0);
        Assert.True(pos.Y <= 1080 - 100);
    }

    [Fact]
    public void CalloutPositionCalculator_MultiMonitor_SupportsNegativeCoordinates()
    {
        // Multi-monitor: Left screen: -1920..0, Right screen: 0..1920. Total: -1920..1920
        // Target at left monitor: X=-1800, Y=200, W=100, H=50
        var target = new BoundingBox(-1800, 200, 100, 50);
        var pos = CalloutPositionCalculator.Calculate(target, 200, 80, MultiMonitorScreen, margin: 12);

        Assert.Equal(CalloutPlacement.Right, pos.Placement);
        Assert.Equal(-1800 + 100 + 12, pos.X); // -1688
        Assert.True(pos.X >= -1920);
        Assert.True(pos.X + pos.Width <= 1920);
    }

    [Fact]
    public void CalloutPositionCalculator_EmptyTarget_PlacesInScreenCenter()
    {
        var pos = CalloutPositionCalculator.Calculate(BoundingBox.Empty, 200, 100, Screen1080P);

        Assert.Equal(CalloutPlacement.Below, pos.Placement);
        Assert.Equal((1920 - 200) / 2.0, pos.X);
        Assert.Equal((1080 - 100) / 2.0, pos.Y);
    }

    [Fact]
    public void IOverlayService_ContractVerification_CanBeImplementedCleanly()
    {
        var fakeService = new FakeOverlayService();

        Assert.Equal(OverlayState.Hidden, fakeService.State);
        Assert.False(fakeService.IsVisible);
        Assert.True(fakeService.IsEnabled);

        OverlayState? oldStateReceived = null;
        OverlayState? newStateReceived = null;
        fakeService.StateChanged += (o, n) =>
        {
            oldStateReceived = o;
            newStateReceived = n;
        };

        fakeService.Show();
        Assert.Equal(OverlayState.Visible, fakeService.State);
        Assert.True(fakeService.IsVisible);
        Assert.Equal(OverlayState.Hidden, oldStateReceived);
        Assert.Equal(OverlayState.Visible, newStateReceived);

        fakeService.Toggle();
        Assert.Equal(OverlayState.Hidden, fakeService.State);
        Assert.False(fakeService.IsVisible);

        var targetInfo = new OverlayTargetInfo(new BoundingBox(10, 10, 100, 100), 1, "Title", "Desc", 50, 50, true);
        fakeService.UpdateTarget(targetInfo);
        Assert.Equal(targetInfo, fakeService.LastTarget);
    }

    [Fact]
    public void IOverlayWindow_ContractVerification_CanBeImplementedCleanly()
    {
        var fakeWindow = new FakeOverlayWindow();

        Assert.False(fakeWindow.IsWindowVisible);
        fakeWindow.SetBounds(0, 0, 1920, 1080);
        Assert.Equal(1920, fakeWindow.Width);
        Assert.Equal(1080, fakeWindow.Height);

        fakeWindow.ShowWindow();
        Assert.True(fakeWindow.IsWindowVisible);

        var target = new OverlayTargetInfo(new BoundingBox(50, 50, 100, 40), 123, "T", "D", 60, 60, true);
        var callout = new CalloutPosition(162, 50, 200, 80, CalloutPlacement.Right);
        fakeWindow.UpdateVisuals(target, callout, isMissingTarget: false);

        Assert.Equal(target, fakeWindow.LastTarget);
        Assert.Equal(callout, fakeWindow.LastCallout);
        Assert.False(fakeWindow.LastIsMissingTarget);

        fakeWindow.HideWindow();
        Assert.False(fakeWindow.IsWindowVisible);

        fakeWindow.CloseWindow();
        Assert.True(fakeWindow.IsClosed);
    }

    private sealed class FakeOverlayService : IOverlayService
    {
        public OverlayState State { get; private set; } = OverlayState.Hidden;
        public bool IsVisible => State == OverlayState.Visible;
        public bool IsEnabled => true;
        public OverlayTargetInfo? LastTarget { get; private set; }

        public event Action<OverlayState, OverlayState>? StateChanged;

        public void Show()
        {
            var old = State;
            State = OverlayState.Visible;
            StateChanged?.Invoke(old, State);
        }

        public void Hide()
        {
            var old = State;
            State = OverlayState.Hidden;
            StateChanged?.Invoke(old, State);
        }

        public void Toggle()
        {
            if (IsVisible)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        public void UpdateTarget(Step? step)
        {
            UpdateTarget(OverlayTargetInfo.FromStep(step));
        }

        public void UpdateTarget(OverlayTargetInfo? target)
        {
            LastTarget = target;
        }
    }

    private sealed class FakeOverlayWindow : IOverlayWindow
    {
        public int X { get; private set; }
        public int Y { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public bool IsWindowVisible { get; private set; }
        public bool IsClosed { get; private set; }
        public OverlayTargetInfo? LastTarget { get; private set; }
        public CalloutPosition? LastCallout { get; private set; }
        public bool LastIsMissingTarget { get; private set; }

        public void SetBounds(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public void ShowWindow() => IsWindowVisible = true;
        public void HideWindow() => IsWindowVisible = false;
        public void CloseWindow()
        {
            IsWindowVisible = false;
            IsClosed = true;
        }

        public void UpdateVisuals(OverlayTargetInfo? target, CalloutPosition? callout, bool isMissingTarget)
        {
            LastTarget = target;
            LastCallout = callout;
            LastIsMissingTarget = isMissingTarget;
        }
    }
}
