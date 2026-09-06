using System.Diagnostics;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Stepwise.WindowsIntegration.Native;
using Stepwise.WindowsIntegration.Overlay;
using Xunit;

namespace Stepwise.Tests;

/// <summary>
/// Тесты жизненного цикла, стилей и клик-сквозного поведения нативного окна оверлея (NativeOverlayWindow).
/// </summary>
public class NativeOverlayWindowLifecycleTests
{
    [Fact]
    public void WindowCreation_ShouldInitializeWindow_WithValidHandleAndHiddenByDefault()
    {
        using var window = new NativeOverlayWindow();

        Assert.NotEqual(nint.Zero, window.Handle);
        Assert.True(NativeMethods.IsWindow(window.Handle), "HWND дескриптор должен быть валидным окном Windows.");
        Assert.False(window.IsWindowVisible, "Окно оверлея должно быть изначально скрыто (SW_HIDE).");
    }

    [Fact]
    public void WindowCreation_ShouldMatchVirtualScreenBounds()
    {
        using var window = new NativeOverlayWindow();

        Assert.True(window.VirtualWidth > 0, "Ширина виртуального экрана должна быть больше 0.");
        Assert.True(window.VirtualHeight > 0, "Высота виртуального экрана должна быть больше 0.");

        bool rectSuccess = NativeMethods.GetWindowRect(window.Handle, out var rect);
        Assert.True(rectSuccess, "GetWindowRect должен вернуть true.");

        int actualWidth = rect.Right - rect.Left;
        int actualHeight = rect.Bottom - rect.Top;

        Assert.Equal(window.VirtualX, rect.Left);
        Assert.Equal(window.VirtualY, rect.Top);
        Assert.Equal(window.VirtualWidth, actualWidth);
        Assert.Equal(window.VirtualHeight, actualHeight);
    }

    [Fact]
    public void WindowStyles_ShouldHavePopupAndAllRequiredExtendedStyles()
    {
        using var window = new NativeOverlayWindow();

        uint style = unchecked((uint)NativeMethods.GetWindowLongPtr(window.Handle, NativeMethods.GWL_STYLE));
        uint exStyle = unchecked((uint)NativeMethods.GetWindowLongPtr(window.Handle, NativeMethods.GWL_EXSTYLE));

        // Проверяем стиль WS_POPUP
        Assert.True((style & NativeMethods.WS_POPUP) != 0, "Окно должно иметь стиль WS_POPUP.");

        // Проверяем расширенные стили: WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST
        Assert.True((exStyle & NativeMethods.WS_EX_LAYERED) != 0, "Окно должно иметь стиль WS_EX_LAYERED.");
        Assert.True((exStyle & NativeMethods.WS_EX_TRANSPARENT) != 0, "Окно должно иметь стиль WS_EX_TRANSPARENT.");
        Assert.True((exStyle & NativeMethods.WS_EX_NOACTIVATE) != 0, "Окно должно иметь стиль WS_EX_NOACTIVATE.");
        Assert.True((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0, "Окно должно иметь стиль WS_EX_TOOLWINDOW.");
        Assert.True((exStyle & NativeMethods.WS_EX_TOPMOST) != 0, "Окно должно иметь стиль WS_EX_TOPMOST.");
    }

    [Fact]
    public void HitTest_ShouldReturnTransparent_ClickThrough()
    {
        using var window = new NativeOverlayWindow();

        // Посылаем сообщение WM_NCHITTEST напрямую окну
        nint hitTestResult = NativeMethods.SendMessage(window.Handle, NativeMethods.WM_NCHITTEST, nint.Zero, nint.Zero);

        // WM_NCHITTEST обязан возвращать HTTRANSPARENT (-1) для нативного сквозного проклика
        Assert.Equal(NativeMethods.HTTRANSPARENT, hitTestResult);
    }

    [Fact]
    public void ShowHide_ToggleIdempotency_25Cycles_WithoutHandleChange()
    {
        using var window = new NativeOverlayWindow();
        nint initialHandle = window.Handle;
        Assert.NotEqual(nint.Zero, initialHandle);

        // 25 циклов переключения видимости для подтверждения отсутствия пересоздания HWND и утечек дескрипторов
        for (int cycle = 1; cycle <= 25; cycle++)
        {
            window.ShowWindow();
            Assert.True(window.IsWindowVisible, $"Цикл {cycle}: окно должно быть видимым после ShowWindow.");
            Assert.Equal(initialHandle, window.Handle);

            window.HideWindow();
            Assert.False(window.IsWindowVisible, $"Цикл {cycle}: окно должно быть скрытым после HideWindow.");
            Assert.Equal(initialHandle, window.Handle);
        }

        // Проверяем, что дескриптор всё еще валиден и не изменился
        Assert.Equal(initialHandle, window.Handle);
        Assert.True(NativeMethods.IsWindow(window.Handle), "Окно должно оставаться валидным после 25 циклов Show/Hide.");
    }

    [Fact]
    public void SetBounds_ShouldUpdateVirtualCoordinatesAndWindowPosition()
    {
        using var window = new NativeOverlayWindow();

        window.SetBounds(120, 240, 850, 650);

        Assert.Equal(120, window.VirtualX);
        Assert.Equal(240, window.VirtualY);
        Assert.Equal(850, window.VirtualWidth);
        Assert.Equal(650, window.VirtualHeight);

        bool rectSuccess = NativeMethods.GetWindowRect(window.Handle, out var rect);
        Assert.True(rectSuccess);
        Assert.Equal(120, rect.Left);
        Assert.Equal(240, rect.Top);
        Assert.Equal(850, rect.Right - rect.Left);
        Assert.Equal(650, rect.Bottom - rect.Top);
    }

    [Fact]
    public void UpdateVisuals_Stub_ShouldStoreDataAndTriggerEvent()
    {
        using var window = new NativeOverlayWindow();

        var target = new OverlayTargetInfo(
            BoundingRectangle: new BoundingBox(200, 300, 150, 50),
            WindowHandle: 99999,
            Title: "Test Element",
            Description: "Click this button",
            ClickX: 275,
            ClickY: 325,
            IsTargetValid: true
        );

        var callout = new CalloutPosition(362, 300, 220, 90, CalloutPlacement.Right);

        OverlayVisualsEventArgs? receivedArgs = null;
        window.VisualsUpdated += (_, args) =>
        {
            receivedArgs = args;
        };

        window.UpdateVisuals(target, callout, isMissingTarget: false);

        Assert.NotNull(receivedArgs);
        Assert.Equal(target, receivedArgs.Target);
        Assert.Equal(callout, receivedArgs.Callout);
        Assert.False(receivedArgs.IsMissingTarget);

        Assert.Equal(target, window.LastTarget);
        Assert.Equal(callout, window.LastCallout);
        Assert.False(window.LastIsMissingTarget);
    }

    [Fact]
    public void Disposal_ShouldCleanlyDestroyWindow_AndHandleBecomesZero()
    {
        var window = new NativeOverlayWindow();
        nint originalHwnd = window.Handle;

        Assert.NotEqual(nint.Zero, originalHwnd);
        Assert.True(NativeMethods.IsWindow(originalHwnd));

        window.ShowWindow();
        Assert.True(window.IsWindowVisible);

        window.Dispose();

        Assert.Equal(nint.Zero, window.Handle);
        Assert.False(window.IsWindowVisible);

        // Окно должно быть уничтожено в Windows
        bool isWindowStillAlive = NativeMethods.IsWindow(originalHwnd);
        Assert.False(isWindowStillAlive, "HWND окно должно быть уничтожено после вызова Dispose().");

        // Повторные вызовы Dispose / CloseWindow не должны вызывать исключений
        window.Dispose();
        window.CloseWindow();
    }

    [Fact]
    public void MultiCycleCreationAndDisposal_ShouldNotLeakOrThrow()
    {
        for (int i = 0; i < 5; i++)
        {
            using var window = new NativeOverlayWindow();
            Assert.NotEqual(nint.Zero, window.Handle);
            Assert.True(NativeMethods.IsWindow(window.Handle));

            window.ShowWindow();
            Assert.True(window.IsWindowVisible);

            window.HideWindow();
            Assert.False(window.IsWindowVisible);
        }
    }
}
