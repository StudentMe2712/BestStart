using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Stepwise.App.Services;
using Stepwise.App.ViewModels;
using Stepwise.Core.Engine;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Stepwise.Core.Policy;
using Stepwise.WindowsIntegration.Automation;
using Stepwise.WindowsIntegration.Native;
using Stepwise.WindowsIntegration.Overlay;
using Xunit;

namespace Stepwise.Tests;

/// <summary>
/// Полный набор приемочных модульных и интеграционных тестов десктопного оверлея (Phase 5 Stage 2).
/// Строго покрывает требования разделов 29 (Unit Tests) и 30 (Integration Tests):
///
/// Раздел 29 (Модульные тесты):
///  1. Overlay hidden state (Unit01)
///  2. Show (Unit02)
///  3. Hide (Unit03)
///  4. Toggle (Unit04)
///  5. Step synchronization (Unit05)
///  6. Invalid rectangle (Unit06)
///  7. Negative virtual coordinates (Unit07)
///  8. Target missing (Unit08)
///  9. Target closed (IsWindow check) (Unit09)
/// 10. Callout placement (right, left, below, above) (Unit10)
/// 11. Edge-of-screen placement (clamping to virtual desktop boundaries) (Unit11)
/// 12. Rapid Step changes (Unit12)
/// 13. Disposal (Unit13)
/// 14. Repeated Show/Hide (at least 20 cycles with handle and memory verification) (Unit14)
/// 15. Player remains usable when Overlay fails (Unit15)
///
/// Раздел 30 (Интеграционные тесты):
/// - Verify: Player -> Overlay service -> target resolution -> overlay update (Integration01)
/// - Verify: No SQLite access from Overlay (Integration02)
/// - Verify: No direct UIA implementation inside Core (Integration03)
/// - Verify: No duplicate target resolution engine (Integration04)
/// - Verify: Reuses existing UIA / target services (Integration05)
/// </summary>
[Collection("GoldenGuiE2ETestsCollection")]
public sealed class DesktopOverlayAcceptanceTests
{
    private static readonly BoundingBox StandardScreen1080P = new(0, 0, 1920, 1080);
    private static readonly BoundingBox MultiMonitorScreen = new(-1920, 0, 3840, 1080);

    #region Section 29: Unit Tests

    /// <summary>
    /// 1. Overlay hidden state:
    /// Проверяет начальное скрытое состояние: оверлей инициализируется в состоянии Hidden,
    /// IsVisible == false, IsEnabled == true, нативное окно создается скрытым (SW_HIDE).
    /// </summary>
    [Fact]
    public void Unit01_OverlayHiddenState_InitialStateIsHiddenAndNotVisible()
    {
        var fakeWindow = new FakeOverlayWindow();
        using var service = new OverlayService(fakeWindow);

        Assert.Equal(OverlayState.Hidden, service.State);
        Assert.False(service.IsVisible, "Оверлей должен быть невидимым в исходном состоянии.");
        Assert.True(service.IsEnabled, "Оверлей должен быть включен и готов к работе.");
        Assert.False(fakeWindow.IsWindowVisible, "Окно оверлея не должно быть видимым изначально.");

        // Проверяем начальное состояние реального нативного окна
        using var realWindow = new NativeOverlayWindow();
        Assert.NotEqual(nint.Zero, realWindow.Handle);
        Assert.True(NativeMethods.IsWindow(realWindow.Handle), "Дескриптор нативного окна должен быть валидным HWND.");
        Assert.False(realWindow.IsWindowVisible, "NativeOverlayWindow.IsWindowVisible должен быть false по умолчанию.");
        Assert.False(NativeMethods.IsWindowVisible(realWindow.Handle), "Win32 IsWindowVisible должен возвращать false при создании.");
    }

    /// <summary>
    /// 2. Show:
    /// Проверяет корректный переход состояний Hidden -> Showing -> Visible,
    /// установку IsVisible == true, вызов ShowWindow() окна, а также идемпотентность повторных вызовов.
    /// </summary>
    [Fact]
    public void Unit02_Show_TransitionsToShowingAndVisible_AndIsIdempotent()
    {
        var fakeWindow = new FakeOverlayWindow();
        using var service = new OverlayService(fakeWindow);

        var stateChanges = new List<(OverlayState OldState, OverlayState NewState)>();
        service.StateChanged += (o, n) => stateChanges.Add((o, n));

        service.Show();

        Assert.Equal(OverlayState.Visible, service.State);
        Assert.True(service.IsVisible);
        Assert.True(fakeWindow.IsWindowVisible);
        Assert.Equal(1, fakeWindow.ShowWindowCallCount);

        Assert.Equal(2, stateChanges.Count);
        Assert.Equal((OverlayState.Hidden, OverlayState.Showing), stateChanges[0]);
        Assert.Equal((OverlayState.Showing, OverlayState.Visible), stateChanges[1]);

        // Идемпотентность: повторный вызов Show() не должен инициировать новые переходы
        service.Show();
        Assert.Equal(OverlayState.Visible, service.State);
        Assert.Equal(1, fakeWindow.ShowWindowCallCount);
        Assert.Equal(2, stateChanges.Count);
    }

    /// <summary>
    /// 3. Hide:
    /// Проверяет переход состояний Visible -> Hiding -> Hidden,
    /// сброс IsVisible в false, вызов HideWindow(), а также идемпотентность.
    /// </summary>
    [Fact]
    public void Unit03_Hide_TransitionsToHidingAndHidden_AndIsIdempotent()
    {
        var fakeWindow = new FakeOverlayWindow();
        using var service = new OverlayService(fakeWindow);
        service.Show();

        var stateChanges = new List<(OverlayState OldState, OverlayState NewState)>();
        service.StateChanged += (o, n) => stateChanges.Add((o, n));

        service.Hide();

        Assert.Equal(OverlayState.Hidden, service.State);
        Assert.False(service.IsVisible);
        Assert.False(fakeWindow.IsWindowVisible);
        Assert.Equal(1, fakeWindow.HideWindowCallCount);

        Assert.Equal(2, stateChanges.Count);
        Assert.Equal((OverlayState.Visible, OverlayState.Hiding), stateChanges[0]);
        Assert.Equal((OverlayState.Hiding, OverlayState.Hidden), stateChanges[1]);

        // Идемпотентность: повторный вызов Hide() в уже скрытом состоянии
        service.Hide();
        Assert.Equal(OverlayState.Hidden, service.State);
        Assert.Equal(1, fakeWindow.HideWindowCallCount);
        Assert.Equal(2, stateChanges.Count);
    }

    /// <summary>
    /// 4. Toggle:
    /// Проверяет циклическое переключение видимости оверлея (Show/Hide).
    /// </summary>
    [Fact]
    public void Unit04_Toggle_AlternatesBetweenVisibleAndHidden()
    {
        var fakeWindow = new FakeOverlayWindow();
        using var service = new OverlayService(fakeWindow);

        Assert.Equal(OverlayState.Hidden, service.State);

        // 1. Hidden -> Visible
        service.Toggle();
        Assert.Equal(OverlayState.Visible, service.State);
        Assert.True(service.IsVisible);
        Assert.True(fakeWindow.IsWindowVisible);
        Assert.Equal(1, fakeWindow.ShowWindowCallCount);

        // 2. Visible -> Hidden
        service.Toggle();
        Assert.Equal(OverlayState.Hidden, service.State);
        Assert.False(service.IsVisible);
        Assert.False(fakeWindow.IsWindowVisible);
        Assert.Equal(1, fakeWindow.HideWindowCallCount);

        // 3. Hidden -> Visible
        service.Toggle();
        Assert.Equal(OverlayState.Visible, service.State);
        Assert.True(service.IsVisible);
        Assert.Equal(2, fakeWindow.ShowWindowCallCount);

        // 4. Visible -> Hidden
        service.Toggle();
        Assert.Equal(OverlayState.Hidden, service.State);
        Assert.False(service.IsVisible);
        Assert.Equal(2, fakeWindow.HideWindowCallCount);
    }

    /// <summary>
    /// 5. Step synchronization:
    /// Проверяет синхронизацию данных при передаче шага (Step): координаты, клик, заголовок и описание.
    /// Проверяет переход состояний Visible -> Updating -> Visible.
    /// </summary>
    [Fact]
    public void Unit05_StepSynchronization_UpdatesTargetAndVisualsWithStepData()
    {
        var fakeWindow = new FakeOverlayWindow();
        using var service = new OverlayService(fakeWindow);
        service.Show();

        var stateChanges = new List<(OverlayState, OverlayState)>();
        service.StateChanged += (o, n) => stateChanges.Add((o, n));

        var step = CreateTestStep(
            index: 1,
            title: "Нажмите 'Оформить заказ'",
            x: 250,
            y: 350,
            width: 140,
            height: 45,
            clickX: 320,
            clickY: 372
        );

        service.UpdateTarget(step);

        Assert.Equal(OverlayState.Visible, service.State);
        Assert.NotNull(service.CurrentTarget);
        Assert.Equal("Нажмите 'Оформить заказ'", service.CurrentTarget.Title);
        Assert.Equal(new BoundingBox(250, 350, 140, 45), service.CurrentTarget.BoundingRectangle);
        Assert.Equal(320.0, service.CurrentTarget.ClickX);
        Assert.Equal(372.0, service.CurrentTarget.ClickY);
        Assert.True(service.CurrentTarget.IsTargetValid);

        Assert.NotNull(fakeWindow.LastTarget);
        Assert.Equal(service.CurrentTarget, fakeWindow.LastTarget);
        Assert.NotNull(fakeWindow.LastCallout);
        Assert.False(fakeWindow.LastIsMissingTarget);

        // Проверяем переходы состояний при обновлении: Visible -> Updating -> Visible
        Assert.Equal(2, stateChanges.Count);
        Assert.Equal((OverlayState.Visible, OverlayState.Updating), stateChanges[0]);
        Assert.Equal((OverlayState.Updating, OverlayState.Visible), stateChanges[1]);
    }

    /// <summary>
    /// 6. Invalid rectangle:
    /// Проверяет обработку некорректных прямоугольников цели (пустой, 0x0, отрицательная ширина/высота).
    /// Сервис обязан пометить цель как missing target (isMissingTarget == true) и не выбрасывать исключений.
    /// </summary>
    [Theory]
    [InlineData(0, 0, 0, 0)]          // BoundingBox.Empty
    [InlineData(100, 100, 0, 50)]      // Нулевая ширина
    [InlineData(100, 100, 50, 0)]      // Нулевая высота
    [InlineData(100, 100, -30, 50)]    // Отрицательная ширина
    [InlineData(100, 100, 50, -20)]    // Отрицательная высота
    public void Unit06_InvalidRectangle_DetectsEmptyOrNegativeBounds_FlagsMissingTarget(
        double x, double y, double width, double height)
    {
        var fakeWindow = new FakeOverlayWindow();
        using var service = new OverlayService(fakeWindow);
        service.Show();

        var invalidTarget = new OverlayTargetInfo(
            BoundingRectangle: new BoundingBox(x, y, width, height),
            WindowHandle: 0,
            Title: "Invalid Target Rect",
            Description: "Invalid geometry test",
            ClickX: 100,
            ClickY: 100,
            IsTargetValid: true
        );

        service.UpdateTarget(invalidTarget);

        Assert.True(fakeWindow.LastIsMissingTarget, "Цель с некорректными размерами должна быть помечена как отсутствующая.");
        Assert.NotNull(fakeWindow.LastCallout);
        Assert.True(fakeWindow.LastCallout.Width > 0);
        Assert.True(fakeWindow.LastCallout.Height > 0);
    }

    /// <summary>
    /// 7. Negative virtual coordinates:
    /// Проверяет корректное вычисление геометрии оверлея и подсказки на вторичных мониторах
    /// с отрицательными координатами (например, второй монитор слева: X в диапазоне -1920..0).
    /// </summary>
    [Fact]
    public void Unit07_NegativeVirtualCoordinates_HandlesMultiMonitorSetupCorrectly()
    {
        // Мультимониторный виртуальный экран: [-1920..1920, 0..1080]
        var virtualBounds = MultiMonitorScreen;

        // Цель на левом мониторе с отрицательными координатами
        var target = new BoundingBox(-1400, 250, 180, 60);
        const double calloutWidth = 220.0;
        const double calloutHeight = 80.0;
        const double margin = 12.0;

        var callout = CalloutPositionCalculator.Calculate(target, calloutWidth, calloutHeight, virtualBounds, margin);

        // Priority 1: Right = -1400 + 180 + 12 = -1208.
        // -1208 + 220 = -988 <= 1920 (помещается на экране)
        Assert.Equal(CalloutPlacement.Right, callout.Placement);
        Assert.Equal(-1208.0, callout.X);
        Assert.Equal(250.0, callout.Y);
        Assert.True(callout.X >= -1920, "Координата X подсказки не должна выходить за левую границу виртуального экрана.");
        Assert.True(callout.X + callout.Width <= 1920, "Подсказка не должна выходить за правую границу.");

        // Цель у левого края второго монитора (X = -1900): подсказка справа должна быть в пределах [-1920..1920]
        var leftEdgeTarget = new BoundingBox(-1900, 300, 100, 50);
        var edgeCallout = CalloutPositionCalculator.Calculate(leftEdgeTarget, calloutWidth, calloutHeight, virtualBounds, margin);

        Assert.True(edgeCallout.X >= -1920);
        Assert.Equal(-1900 + 100 + margin, edgeCallout.X);
    }

    /// <summary>
    /// 8. Target missing:
    /// Проверяет обработку отсутствующей цели (null или OverlayTargetInfo.Empty):
    /// флаг isMissingTarget == true, выноска центрируется на виртуальном экране с CalloutPlacement.Below.
    /// </summary>
    [Fact]
    public void Unit08_TargetMissing_NullOrEmptyTarget_HandledSafelyWithCenteredCallout()
    {
        var fakeWindow = new FakeOverlayWindow();
        using var service = new OverlayService(fakeWindow);
        service.Show();

        // 1. null Step
        service.UpdateTarget((Step?)null);
        Assert.True(fakeWindow.LastIsMissingTarget);
        Assert.NotNull(fakeWindow.LastCallout);
        Assert.Equal(CalloutPlacement.Below, fakeWindow.LastCallout.Placement);

        // 2. null OverlayTargetInfo
        service.UpdateTarget((OverlayTargetInfo?)null);
        Assert.True(fakeWindow.LastIsMissingTarget);
        Assert.NotNull(fakeWindow.LastCallout);

        // 3. OverlayTargetInfo.Empty
        service.UpdateTarget(OverlayTargetInfo.Empty);
        Assert.True(fakeWindow.LastIsMissingTarget);

        // Проверяем прямое центрирование через калькулятор
        var centeredCallout = CalloutPositionCalculator.Calculate(
            BoundingBox.Empty,
            OverlayService.DefaultCalloutWidth,
            OverlayService.DefaultCalloutHeight,
            StandardScreen1080P
        );

        Assert.Equal(CalloutPlacement.Below, centeredCallout.Placement);
        Assert.Equal((1920 - OverlayService.DefaultCalloutWidth) / 2.0, centeredCallout.X);
        Assert.Equal((1080 - OverlayService.DefaultCalloutHeight) / 2.0, centeredCallout.Y);
    }

    /// <summary>
    /// 9. Target closed (IsWindow check):
    /// Проверяет сценарий, когда HWND окна целевого элемента стал недействительным
    /// (окно было закрыто пользователем или процесс завершился).
    /// Сервис обязан через NativeMethods.IsWindow определить невалидность и выставить isMissingTarget = true.
    /// </summary>
    [Fact]
    public void Unit09_TargetClosed_IsWindowCheck_FlagsTargetAsMissing()
    {
        var fakeWindow = new FakeOverlayWindow();
        using var service = new OverlayService(fakeWindow);
        service.Show();

        // Передаем заведомо недействительный дескриптор окна
        const long closedHwnd = 0x7FFFFFFF;
        Assert.False(NativeMethods.IsWindow((nint)closedHwnd), "Дескриптор 0x7FFFFFFF не должен быть валидным окном.");

        var targetWithClosedWindow = new OverlayTargetInfo(
            BoundingRectangle: new BoundingBox(100, 100, 200, 80),
            WindowHandle: closedHwnd,
            Title: "Закрытое окно",
            Description: "Окно уже уничтожено",
            ClickX: 150,
            ClickY: 140,
            IsTargetValid: true
        );

        service.UpdateTarget(targetWithClosedWindow);

        // Даже при валидном BoundingBox цель должна быть определена как отсутствующая из-за закрытого окна
        Assert.True(fakeWindow.LastIsMissingTarget, "Цель с закрытым HWND окном обязана классифицироваться как missing target.");
    }

    /// <summary>
    /// 10. Callout placement (right, left, below, above):
    /// Проверяет детерминированный выбор всех 4 сторон размещения выноски согласно спецификации:
    /// Приоритет 1 (Right) -> Приоритет 2 (Left) -> Приоритет 3 (Below) -> Приоритет 4 (Above).
    /// </summary>
    [Fact]
    public void Unit10_CalloutPlacement_RightLeftBelowAbove_FollowsPriorityOrder()
    {
        const double w = 200.0;
        const double h = 80.0;
        const double m = 12.0;
        var screen = StandardScreen1080P;

        // 1. Right: цель по центру экрана, справа достаточно места
        var centerTarget = new BoundingBox(300, 400, 100, 50);
        var rightResult = CalloutPositionCalculator.Calculate(centerTarget, w, h, screen, m);
        Assert.Equal(CalloutPlacement.Right, rightResult.Placement);
        Assert.Equal(300 + 100 + m, rightResult.X);
        Assert.Equal(400.0, rightResult.Y);

        // 2. Left: цель у правого края, справа не помещается, слева места достаточно
        var rightOverflowTarget = new BoundingBox(1750, 400, 100, 50);
        var leftResult = CalloutPositionCalculator.Calculate(rightOverflowTarget, w, h, screen, m);
        Assert.Equal(CalloutPlacement.Left, leftResult.Placement);
        Assert.Equal(1750 - w - m, leftResult.X);
        Assert.Equal(400.0, leftResult.Y);

        // 3. Below: цель широкая во всю ширину экрана (Left и Right не влезают), снизу места достаточно
        var fullWidthTarget = new BoundingBox(50, 200, 1850, 60);
        var belowResult = CalloutPositionCalculator.Calculate(fullWidthTarget, w, h, screen, m);
        Assert.Equal(CalloutPlacement.Below, belowResult.Placement);
        Assert.Equal(50.0, belowResult.X);
        Assert.Equal(200 + 60 + m, belowResult.Y);

        // 4. Above: цель широкая и внизу экрана (Left, Right, Below не влезают), сверху места достаточно
        var fullWidthBottomTarget = new BoundingBox(50, 950, 1850, 60);
        var aboveResult = CalloutPositionCalculator.Calculate(fullWidthBottomTarget, w, h, screen, m);
        Assert.Equal(CalloutPlacement.Above, aboveResult.Placement);
        Assert.Equal(50.0, aboveResult.X);
        Assert.Equal(950 - h - m, aboveResult.Y);
    }

    /// <summary>
    /// 11. Edge-of-screen placement (clamping to virtual desktop boundaries):
    /// Проверяет, что при расположении цели у любых краев и углов виртуального экрана
    /// блок подсказки всегда строго удерживается внутри границ виртуального рабочего стола.
    /// </summary>
    [Fact]
    public void Unit11_EdgeOfScreenPlacement_ClampingToVirtualDesktopBoundaries()
    {
        const double w = 250.0;
        const double h = 90.0;
        var screen = StandardScreen1080P;

        var edgeTargets = new[]
        {
            new BoundingBox(1880, 1040, 30, 30),  // Правый нижний угол
            new BoundingBox(5, 5, 20, 20),        // Левый верхний угол
            new BoundingBox(1880, 20, 30, 20),    // Правый верхний угол
            new BoundingBox(5, 1040, 20, 30),     // Левый нижний угол
            new BoundingBox(-50, -50, 100, 100),  // Частично за пределами экрана
            new BoundingBox(1900, 500, 100, 100)  // Частично за правым краем
        };

        foreach (var target in edgeTargets)
        {
            var pos = CalloutPositionCalculator.Calculate(target, w, h, screen);

            Assert.True(pos.X >= screen.X, $"Callout X ({pos.X}) должен быть >= screen.X ({screen.X}) для цели {target}");
            Assert.True(pos.X + pos.Width <= screen.X + screen.Width,
                $"Callout правый край ({pos.X + pos.Width}) должен быть <= screen.Right ({screen.X + screen.Width})");
            Assert.True(pos.Y >= screen.Y, $"Callout Y ({pos.Y}) должен быть >= screen.Y ({screen.Y})");
            Assert.True(pos.Y + pos.Height <= screen.Y + screen.Height,
                $"Callout нижний край ({pos.Y + pos.Height}) должен быть <= screen.Bottom ({screen.Y + screen.Height})");
        }
    }

    /// <summary>
    /// 12. Rapid Step changes:
    /// Проверяет устойчивость сервиса при быстрой пачке последовательных смен шагов (UpdateTarget)
    /// без блокировок, зависаний или проявления артефактов устаревших подсветок.
    /// </summary>
    [Fact]
    public void Unit12_RapidStepChanges_MaintainsConsistencyAndAppliesFinalStep()
    {
        var fakeWindow = new FakeOverlayWindow();
        using var service = new OverlayService(fakeWindow);
        service.Show();

        const int rapidCount = 20;
        for (int i = 1; i <= rapidCount; i++)
        {
            var step = CreateTestStep(
                index: i,
                title: $"Быстрый шаг {i}",
                x: 100 + i * 20,
                y: 150 + i * 15,
                width: 120,
                height: 40
            );

            service.UpdateTarget(step);
        }

        // Конечное состояние должно быть Visible и соответствовать последнему переданному шагу (20)
        Assert.Equal(OverlayState.Visible, service.State);
        Assert.NotNull(service.CurrentTarget);
        Assert.Equal($"Быстрый шаг {rapidCount}", service.CurrentTarget.Title);
        Assert.Equal(new BoundingBox(100 + rapidCount * 20, 150 + rapidCount * 15, 120, 40), service.CurrentTarget.BoundingRectangle);
        Assert.Equal($"Быстрый шаг {rapidCount}", fakeWindow.LastTarget?.Title);
        Assert.False(fakeWindow.LastIsMissingTarget);
    }

    /// <summary>
    /// 13. Disposal:
    /// Проверяет корректное освобождение ресурсов:
    /// - IsEnabled становится false
    /// - Окно закрывается/освобождается
    /// - Отписка от событий трекера окон
    /// - Вызов методов после Dispose выбрасывает ObjectDisposedException
    /// - Повторный вызов Dispose безопасен
    /// </summary>
    [Fact]
    public void Unit13_Disposal_CleansUpWindow_UnsubscribesEvents_ThrowsObjectDisposedException()
    {
        var fakeWindow = new FakeOverlayWindow();
        var fakeTracker = new FakeActiveWindowTracker();
        var service = new OverlayService(fakeWindow, fakeTracker);

        Assert.True(service.IsEnabled);

        service.Dispose();

        Assert.False(service.IsEnabled, "IsEnabled должен стать false после Dispose.");
        Assert.True(fakeWindow.IsClosed || fakeWindow.DisposeCallCount > 0, "Окно должно быть закрыто или освобождено.");

        // Проверяем отписку от событий: генерация события в трекере не должна вызывать активность в сервисе
        int updateCount = fakeWindow.UpdateVisualsCallCount;
        fakeTracker.RaiseActiveWindowChanged(new ActiveWindowInfo(1, 10, "app", "title", new BoundingBox(0, 0, 100, 100), DateTime.UtcNow));
        Assert.Equal(updateCount, fakeWindow.UpdateVisualsCallCount);

        // Вызовы методов после Dispose должны выбрасывать ObjectDisposedException
        Assert.Throws<ObjectDisposedException>(() => service.Show());
        Assert.Throws<ObjectDisposedException>(() => service.UpdateTarget((Step?)null));

        // Повторный Dispose безопасен
        var ex = Record.Exception(() => service.Dispose());
        Assert.Null(ex);
    }

    /// <summary>
    /// 14. Repeated Show/Hide (at least 20 cycles with handle and memory verification):
    /// Выполняет 25 циклов переключения Show/Hide с использованием реального нативного окна NativeOverlayWindow.
    /// Подтверждает, что дескриптор HWND остается строго постоянным (без пересоздания окна)
    /// и отсутствуют утечки GDI дескрипторов и памяти процесса.
    /// </summary>
    [Fact]
    public void Unit14_RepeatedShowHide_AtLeast20Cycles_HandleAndMemoryVerification()
    {
        using var realWindow = new NativeOverlayWindow();
        using var service = new OverlayService(realWindow);

        nint initialHandle = realWindow.Handle;
        Assert.NotEqual(nint.Zero, initialHandle);
        Assert.True(NativeMethods.IsWindow(initialHandle));

        // Прогрев внутренних структур
        service.Show();
        service.Hide();

        nint processHandle = Process.GetCurrentProcess().Handle;
        uint initialGdiObjects = NativeMethods.GetGuiResources(processHandle, 0);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long initialMemory = GC.GetTotalMemory(true);

        const int cycleCount = 25; // Строго больше требуемых 20 циклов
        for (int cycle = 1; cycle <= cycleCount; cycle++)
        {
            service.Show();
            Assert.True(service.IsVisible, $"Цикл {cycle}: оверлей должен быть видимым после Show.");
            Assert.True(realWindow.IsWindowVisible);
            Assert.Equal(initialHandle, realWindow.Handle);

            service.Hide();
            Assert.False(service.IsVisible, $"Цикл {cycle}: оверлей должен быть скрытым после Hide.");
            Assert.False(realWindow.IsWindowVisible);
            Assert.Equal(initialHandle, realWindow.Handle);
        }

        // Проверяем стабильность дескриптора HWND
        Assert.Equal(initialHandle, realWindow.Handle);
        Assert.True(NativeMethods.IsWindow(realWindow.Handle), "HWND дескриптор окна должен оставаться валидным.");

        // Проверяем отсутствие утечек дескрипторов GDI
        uint finalGdiObjects = NativeMethods.GetGuiResources(processHandle, 0);
        long gdiDelta = Math.Abs((long)finalGdiObjects - (long)initialGdiObjects);
        Assert.True(gdiDelta <= 3, $"Обнаружена утечка дескрипторов GDI: было {initialGdiObjects}, стало {finalGdiObjects} (дельта {gdiDelta}).");

        // Проверяем стабильность управляемой памяти
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long finalMemory = GC.GetTotalMemory(true);
        long memoryDelta = Math.Abs(finalMemory - initialMemory);

        // 25 циклов скрытия/показа не должны вызывать значительного роста памяти (< 8 МБ)
        Assert.True(memoryDelta < 8 * 1024 * 1024, $"Подозрение на утечку памяти: дельта {memoryDelta / 1024} КБ.");
    }

    /// <summary>
    /// 15. Player remains usable when Overlay fails:
    /// Проверяет, что при возникновении сбоев в работе подсистемы оверлея
    /// (выбрасывание исключений методами Show, Hide, Toggle, UpdateTarget или переход в OverlayState.Failed)
    /// плеер PlayerViewModel сохраняет 100% работоспособность и позволяет пользователю беспрепятственно
    /// выполнять навигацию по руководству.
    /// </summary>
    [Fact]
    public void Unit15_PlayerRemainsUsable_WhenOverlayFailsOrThrows()
    {
        var engine = new PlayerEngine();
        var mockLoader = new Mock<IImageLoaderService>();
        var steps = CreateTestStepList(4);
        engine.LoadGuide(Guid.NewGuid(), steps);

        // Создаем мок сервиса оверлея, который выбрасывает исключения на любой вызов
        var faultyOverlay = new Mock<IOverlayService>();
        faultyOverlay.Setup(o => o.State).Returns(OverlayState.Failed);
        faultyOverlay.Setup(o => o.IsVisible).Returns(false);
        faultyOverlay.Setup(o => o.IsEnabled).Returns(false);
        faultyOverlay.Setup(o => o.Show()).Throws(new InvalidOperationException("Overlay Show catastrophic failure!"));
        faultyOverlay.Setup(o => o.Hide()).Throws(new InvalidOperationException("Overlay Hide catastrophic failure!"));
        faultyOverlay.Setup(o => o.Toggle()).Throws(new InvalidOperationException("Overlay Toggle catastrophic failure!"));
        faultyOverlay.Setup(o => o.UpdateTarget(It.IsAny<Step?>())).Throws(new InvalidOperationException("Overlay UpdateTarget catastrophic failure!"));
        faultyOverlay.Setup(o => o.UpdateTarget(It.IsAny<OverlayTargetInfo?>())).Throws(new InvalidOperationException("Overlay UpdateTarget catastrophic failure!"));

        using var vm = new PlayerViewModel(
            engine,
            mockLoader.Object,
            repository: null,
            dispatcherQueue: null,
            overlayService: faultyOverlay.Object
        );

        // Все команды плеера должны выполняться безошибочно
        var exception = Record.Exception(() =>
        {
            Assert.Equal(0, vm.CurrentIndex);

            // Навигация вперед
            vm.NextCommand.Execute(null);
            Assert.Equal(1, vm.CurrentIndex);

            vm.NextCommand.Execute(null);
            Assert.Equal(2, vm.CurrentIndex);

            // Навигация назад
            vm.PreviousCommand.Execute(null);
            Assert.Equal(1, vm.CurrentIndex);

            // Переход в конец и в начало
            vm.LastCommand.Execute(null);
            Assert.Equal(3, vm.CurrentIndex);

            vm.FirstCommand.Execute(null);
            Assert.Equal(0, vm.CurrentIndex);

            // Перезапуск
            vm.RestartCommand.Execute(null);
            Assert.Equal(0, vm.CurrentIndex);

            // Переключение тумблера оверлея
            vm.ToggleHighlightOverlayCommand.Execute(null);

            // Закрытие плеера
            vm.CloseCommand.Execute(null);
        });

        Assert.Null(exception);
    }

    #endregion

    #region Section 30: Integration Tests

    /// <summary>
    /// Интеграционный тест: Player -> Overlay service -> target resolution -> overlay update
    /// Проверяет сквозную цепочку:
    /// 1. Разрешение целевого UI-элемента через ITargetResolver (семантическое действие -> ElementInfo).
    /// 2. Формирование шага руководства (Step) с целевым элементом.
    /// 3. Загрузка в PlayerEngine и привязка к PlayerViewModel.
    /// 4. Переключение шага в плеере автоматически инициирует обновление в OverlayService.
    /// 5. OverlayService валидирует цель, рассчитывает выноску и передает точные визуальные данные в IOverlayWindow.
    /// </summary>
    [Fact]
    public async Task Integration01_PlayerToOverlayService_TargetResolution_OverlayUpdatePipeline()
    {
        // 1. Target resolution: симулируем работу сервиса разрешения цели
        var mockResolver = new Mock<ITargetResolver>();
        var resolvedElement = new ElementInfo(
            Name: "Кнопка 'Купить сейчас'",
            ControlType: "Button",
            AutomationId: "btnBuyNow",
            ClassName: "StandardButton",
            ProcessName: "ShopApplication",
            ProcessId: 7890,
            WindowTitle: "Окно Корзины",
            WindowHandle: 0,
            BoundingRectangle: new BoundingBox(320, 240, 160, 50)
        );

        mockResolver.Setup(r => r.ResolveTargetAsync(It.IsAny<SemanticAction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolvedElement);

        var action = SemanticAction.CreateMouseClick(SemanticActionType.LeftClick, 400, 265, WindowContext.Empty, DateTime.UtcNow);
        var targetElement = await mockResolver.Object.ResolveTargetAsync(action);
        Assert.Equal("btnBuyNow", targetElement.AutomationId);

        // 2. Формируем шаги плеера
        var step1 = new Step(
            Id: Guid.NewGuid(),
            SequenceIndex: 1,
            Timestamp: DateTime.UtcNow,
            Action: ActionType.LeftClick,
            ClickX: 400,
            ClickY: 265,
            TargetElement: targetElement,
            Title: "Шаг 1: Нажмите Купить",
            Description: "Переход к оформлению"
        );

        var step2Element = new ElementInfo(
            Name: "Поле 'Адрес'",
            ControlType: "Edit",
            AutomationId: "txtAddress",
            ClassName: "TextBox",
            ProcessName: "ShopApplication",
            ProcessId: 7890,
            WindowTitle: "Окно Корзины",
            WindowHandle: 0,
            BoundingRectangle: new BoundingBox(320, 320, 250, 40)
        );

        var step2 = new Step(
            Id: Guid.NewGuid(),
            SequenceIndex: 2,
            Timestamp: DateTime.UtcNow,
            Action: ActionType.LeftClick,
            ClickX: 350,
            ClickY: 340,
            TargetElement: step2Element,
            Title: "Шаг 2: Введите адрес",
            Description: "Укажите улицу и дом"
        );

        // 3. Подготавливаем плеер и оверлей
        var fakeWindow = new FakeOverlayWindow();
        using var overlayService = new OverlayService(fakeWindow);
        overlayService.Show();

        var engine = new PlayerEngine();
        engine.LoadGuide(Guid.NewGuid(), new List<Step> { step1, step2 });

        var mockLoader = new Mock<IImageLoaderService>();
        using var vm = new PlayerViewModel(
            engine,
            mockLoader.Object,
            repository: null,
            dispatcherQueue: null,
            overlayService: overlayService
        );

        // Проверяем начальное обновление оверлея для Шага 1
        Assert.Equal(0, vm.CurrentIndex);
        Assert.NotNull(fakeWindow.LastTarget);
        Assert.Equal("Шаг 1: Нажмите Купить", fakeWindow.LastTarget.Title);
        Assert.Equal(new BoundingBox(320, 240, 160, 50), fakeWindow.LastTarget.BoundingRectangle);
        Assert.False(fakeWindow.LastIsMissingTarget);
        Assert.NotNull(fakeWindow.LastCallout);
        Assert.Equal(CalloutPlacement.Right, fakeWindow.LastCallout.Placement);

        // 4. Переключаем шаг в плеере (Next)
        vm.NextCommand.Execute(null);

        // 5. Проверяем синхронное обновление оверлея для Шага 2
        Assert.Equal(1, vm.CurrentIndex);
        Assert.NotNull(fakeWindow.LastTarget);
        Assert.Equal("Шаг 2: Введите адрес", fakeWindow.LastTarget.Title);
        Assert.Equal(new BoundingBox(320, 320, 250, 40), fakeWindow.LastTarget.BoundingRectangle);
        Assert.Equal(350.0, fakeWindow.LastTarget.ClickX);
        Assert.Equal(340.0, fakeWindow.LastTarget.ClickY);
        Assert.False(fakeWindow.LastIsMissingTarget);
        Assert.NotNull(fakeWindow.LastCallout);
    }

    /// <summary>
    /// Архитектурная верификация: No SQLite access from Overlay.
    /// Проверяет отсутствие каких-либо зависимостей от SQLite, Stepwise.Storage,
    /// строк подключения или интерфейсов репозиториев внутри классов оверлея.
    /// </summary>
    [Fact]
    public void Integration02_Architecture_NoSqliteAccessFromOverlay()
    {
        var overlayAssembly = typeof(OverlayService).Assembly;
        var coreAssembly = typeof(IOverlayService).Assembly;

        // 1. Проверяем ссылки сборок
        var overlayReferences = overlayAssembly.GetReferencedAssemblies();
        var coreReferences = coreAssembly.GetReferencedAssemblies();

        Assert.DoesNotContain(overlayReferences, r => r.Name?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain(overlayReferences, r => r.Name?.Contains("Stepwise.Storage", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain(coreReferences, r => r.Name?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain(coreReferences, r => r.Name?.Contains("Stepwise.Storage", StringComparison.OrdinalIgnoreCase) == true);

        // 2. Проверяем типы пространства имен Stepwise.WindowsIntegration.Overlay
        var overlayTypes = overlayAssembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("Stepwise.WindowsIntegration.Overlay") == true)
            .ToList();

        Assert.NotEmpty(overlayTypes);

        foreach (var type in overlayTypes)
        {
            // Конструкторы не должны принимать хранилища или SQLite параметры
            foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                foreach (var param in ctor.GetParameters())
                {
                    Assert.False(
                        param.ParameterType.FullName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true ||
                        param.ParameterType.FullName?.Contains("Storage", StringComparison.OrdinalIgnoreCase) == true,
                        $"Класс {type.FullName} в конструкторе принимает хранилище/SQLite параметр {param.Name} ({param.ParameterType})"
                    );
                }
            }

            // Поля и методы не должны использовать типы SQLite
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                Assert.False(
                    field.FieldType.FullName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true,
                    $"Класс {type.FullName} содержит SQLite поле {field.Name}"
                );
            }
        }
    }

    /// <summary>
    /// Архитектурная верификация: No direct UIA implementation inside Core.
    /// Проверяет, что сборка Stepwise.Core полностью изолирована от нативных и управляемых
    /// реализаций Windows UI Automation (UIAutomationClient, UIAutomationTypes, System.Windows.Automation, COM IUIAutomation).
    /// </summary>
    [Fact]
    public void Integration03_Architecture_NoDirectUiaImplementationInsideCore()
    {
        var coreAssembly = typeof(IOverlayService).Assembly;
        var coreReferences = coreAssembly.GetReferencedAssemblies();

        // Stepwise.Core не должна иметь зависимостей от UIA
        Assert.DoesNotContain(coreReferences, r => r.Name?.Contains("UIAutomation", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain(coreReferences, r => r.Name?.Contains("Windows.Automation", StringComparison.OrdinalIgnoreCase) == true);

        // Ни один тип в Core не должен реализовывать COM-интерфейсы UIA или наследоваться от UIA классов
        foreach (var type in coreAssembly.GetTypes())
        {
            var baseType = type.BaseType;
            while (baseType != null && baseType != typeof(object))
            {
                Assert.False(
                    baseType.FullName?.Contains("UIAutomation", StringComparison.OrdinalIgnoreCase) == true,
                    $"Тип Core {type.FullName} наследуется от внешнего UIA типа {baseType.FullName}"
                );
                baseType = baseType.BaseType;
            }

            foreach (var iface in type.GetInterfaces())
            {
                Assert.False(
                    iface.FullName?.Contains("IUIAutomation", StringComparison.OrdinalIgnoreCase) == true,
                    $"Тип Core {type.FullName} реализует UIA-интерфейс {iface.FullName}"
                );
            }
        }
    }

    /// <summary>
    /// Архитектурная верификация: No duplicate target resolution engine.
    /// Проверяет, что OverlayService не содержит дублирующего механизма поиска/инспекции элементов,
    /// не реализует ITargetResolver и строго потребляет уже разрешенные данные OverlayTargetInfo.
    /// </summary>
    [Fact]
    public void Integration04_Architecture_NoDuplicateTargetResolutionEngine()
    {
        var overlayServiceType = typeof(OverlayService);

        // OverlayService не должен реализовывать интерфейсы разрешения целей
        var interfaces = overlayServiceType.GetInterfaces();
        Assert.DoesNotContain(interfaces, i => i.Name == "ITargetResolver");
        Assert.DoesNotContain(interfaces, i => i.Name.Contains("TargetResolver"));
        Assert.DoesNotContain(interfaces, i => i.Name.Contains("ElementDetector"));

        // Публичные методы OverlayService должны быть ограничены жизненным циклом и обновлением цели
        var publicMethodNames = overlayServiceType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName) // исключаем геттеры/сеттеры свойств и событий
            .Select(m => m.Name)
            .ToHashSet();

        var allowedMethods = new HashSet<string> { "Show", "Hide", "Toggle", "UpdateTarget", "Close", "Dispose" };
        foreach (var method in publicMethodNames)
        {
            Assert.Contains(method, allowedMethods);
        }

        // Подтверждаем, что разрешение целей централизованно в UIATargetResolver
        Assert.True(typeof(ITargetResolver).IsAssignableFrom(typeof(UIATargetResolver)));
    }

    /// <summary>
    /// Архитектурная верификация: Reuses existing UIA / target services.
    /// Проверяет, что подсистема оверлея повторно использует существующие контракты и сервисы
    /// (IActiveWindowTracker, Step, ElementInfo, BoundingBox) вместо дублирования моделей.
    /// </summary>
    [Fact]
    public void Integration05_Architecture_ReusesExistingUiaAndTargetServices()
    {
        // 1. OverlayService принимает IActiveWindowTracker в конструкторе
        var constructor = typeof(OverlayService).GetConstructors()
            .FirstOrDefault(c => c.GetParameters().Any(p => p.ParameterType == typeof(IActiveWindowTracker)));
        Assert.NotNull(constructor);

        // 2. OverlayTargetInfo.FromStep конвертирует существующую доменную модель Step
        var step = CreateTestStep(1, "Тестовый шаг", 100, 100, 150, 40);
        var targetInfo = OverlayTargetInfo.FromStep(step);

        Assert.True(targetInfo.IsTargetValid);
        Assert.Equal(step.TargetElement.BoundingRectangle, targetInfo.BoundingRectangle);
        Assert.Equal(step.Title, targetInfo.Title);
        Assert.Equal(step.Description, targetInfo.Description);

        // 3. Проверяем реакцию OverlayService на событие смены активного окна из IActiveWindowTracker
        var fakeWindow = new FakeOverlayWindow();
        var fakeTracker = new FakeActiveWindowTracker();
        using var service = new OverlayService(fakeWindow, windowTracker: fakeTracker);
        service.Show();

        var targetWithHwnd = new OverlayTargetInfo(
            BoundingRectangle: new BoundingBox(100, 100, 200, 60),
            WindowHandle: 12345,
            Title: "Tracked Window Target",
            Description: "Desc",
            ClickX: 150,
            ClickY: 130,
            IsTargetValid: true
        );
        service.UpdateTarget(targetWithHwnd);

        int countBefore = fakeWindow.UpdateVisualsCallCount;

        // Генерируем событие переключения активного окна
        fakeTracker.RaiseActiveWindowChanged(new ActiveWindowInfo(
            WindowHandle: 99999,
            ProcessId: 500,
            ProcessName: "OtherApp",
            WindowTitle: "Other Window",
            Bounds: new BoundingBox(0, 0, 1024, 768),
            Timestamp: DateTime.UtcNow
        ));

        // Сервис должен был заново валидировать цель при смене активного окна
        Assert.True(fakeWindow.UpdateVisualsCallCount > countBefore,
            "OverlayService должен реагировать на смену активного окна через IActiveWindowTracker.");
    }

    #endregion

    #region Helper Methods and Test Doubles

    private static Step CreateTestStep(
        int index,
        string title,
        double x,
        double y,
        double width,
        double height,
        double? clickX = null,
        double? clickY = null)
    {
        var element = new ElementInfo(
            Name: title,
            ControlType: "Button",
            AutomationId: $"btn_{index}",
            ClassName: "Button",
            ProcessName: "TestApp",
            ProcessId: 1000,
            WindowTitle: "Test Window",
            WindowHandle: 0,
            BoundingRectangle: new BoundingBox(x, y, width, height)
        );

        return new Step(
            Id: Guid.NewGuid(),
            SequenceIndex: index,
            Timestamp: DateTime.UtcNow,
            Action: ActionType.LeftClick,
            ClickX: clickX ?? (x + width / 2.0),
            ClickY: clickY ?? (y + height / 2.0),
            TargetElement: element,
            Title: title,
            Description: $"Инструкция для {title}"
        );
    }

    private static List<Step> CreateTestStepList(int count)
    {
        var list = new List<Step>();
        for (int i = 1; i <= count; i++)
        {
            list.Add(CreateTestStep(i, $"Шаг {i}", 100 + i * 40, 100 + i * 30, 120, 40));
        }
        return list;
    }

    private sealed class FakeOverlayWindow : IOverlayWindow, IDisposable
    {
        public bool IsWindowVisible { get; private set; }
        public bool IsClosed { get; private set; }
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
            IsClosed = true;
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
            CloseWindow();
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

    #endregion
}
