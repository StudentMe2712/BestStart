using System;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Stepwise.Core.Policy;
using Stepwise.WindowsIntegration.Native;

namespace Stepwise.WindowsIntegration.Overlay;

/// <summary>
/// Сервис управления визуальным оверлеем подсказок на рабочем столе.
/// Реализует <see cref="IOverlayService"/> и управляет жизненным циклом <see cref="IOverlayWindow"/>,
/// валидацией целевых элементов, расчетом выносок подсказок и связыванием со слоем воспроизведения Player.
/// </summary>
public sealed class OverlayService : IOverlayService, IDisposable
{
    public const double DefaultCalloutWidth = 300.0;
    public const double DefaultCalloutHeight = 90.0;

    private readonly object _syncLock = new();
    private readonly IOverlayWindow _overlayWindow;
    private readonly IActiveWindowTracker? _windowTracker;
    private readonly ISystemMetricsProvider _metricsProvider;

    private OverlayState _state = OverlayState.Hidden;
    private OverlayTargetInfo? _currentTarget;
    private bool _isDisposed;

    /// <summary>
    /// Окно оверлея, управляемое сервисом.
    /// </summary>
    public IOverlayWindow OverlayWindow => _overlayWindow;

    /// <summary>
    /// Трекер активных окон (если передан).
    /// </summary>
    public IActiveWindowTracker? WindowTracker => _windowTracker;

    /// <summary>
    /// Поставщик системных метрик.
    /// </summary>
    public ISystemMetricsProvider MetricsProvider => _metricsProvider;

    /// <summary>
    /// Текущая переданная информация о целевом элементе.
    /// </summary>
    public OverlayTargetInfo? CurrentTarget
    {
        get
        {
            lock (_syncLock)
            {
                return _currentTarget;
            }
        }
    }

    /// <inheritdoc />
    public OverlayState State
    {
        get
        {
            lock (_syncLock)
            {
                return _state;
            }
        }
        private set
        {
            if (_state == value)
            {
                return;
            }

            var oldState = _state;
            _state = value;
            StateChanged?.Invoke(oldState, value);
        }
    }

    /// <inheritdoc />
    public bool IsVisible
    {
        get
        {
            lock (_syncLock)
            {
                return _state == OverlayState.Visible || _state == OverlayState.Updating;
            }
        }
    }

    /// <inheritdoc />
    public bool IsEnabled
    {
        get
        {
            lock (_syncLock)
            {
                return !_isDisposed && _state != OverlayState.Failed;
            }
        }
    }

    /// <inheritdoc />
    public event Action<OverlayState, OverlayState>? StateChanged;

    public OverlayService(
        IOverlayWindow overlayWindow,
        IActiveWindowTracker? windowTracker = null,
        ISystemMetricsProvider? metricsProvider = null)
    {
        _overlayWindow = overlayWindow ?? throw new ArgumentNullException(nameof(overlayWindow));
        _windowTracker = windowTracker;
        _metricsProvider = metricsProvider ?? DefaultSystemMetricsProvider.Instance;

        if (_windowTracker != null)
        {
            _windowTracker.ActiveWindowChanged += OnActiveWindowChanged;
        }
    }

    /// <inheritdoc />
    public void Show()
    {
        lock (_syncLock)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            if (_state == OverlayState.Visible || _state == OverlayState.Showing)
            {
                return;
            }

            try
            {
                State = OverlayState.Showing;
                _overlayWindow.ShowWindow();
                ApplyTargetVisualsInternal();
                State = OverlayState.Visible;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OverlayService] Ошибка при отображении оверлея: {ex.Message}");
                State = OverlayState.Failed;
            }
        }
    }

    /// <inheritdoc />
    public void Hide()
    {
        lock (_syncLock)
        {
            if (_isDisposed)
            {
                return;
            }

            if (_state == OverlayState.Hidden || _state == OverlayState.Hiding)
            {
                return;
            }

            try
            {
                State = OverlayState.Hiding;
                _overlayWindow.HideWindow();
                State = OverlayState.Hidden;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OverlayService] Ошибка при скрытии оверлея: {ex.Message}");
                State = OverlayState.Failed;
            }
        }
    }

    /// <inheritdoc />
    public void Toggle()
    {
        lock (_syncLock)
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
    }

    /// <inheritdoc />
    public void UpdateTarget(Step? step)
    {
        UpdateTarget(OverlayTargetInfo.FromStep(step));
    }

    /// <inheritdoc />
    public void UpdateTarget(OverlayTargetInfo? target)
    {
        lock (_syncLock)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            _currentTarget = target;

            if (_state == OverlayState.Visible)
            {
                try
                {
                    State = OverlayState.Updating;
                    ApplyTargetVisualsInternal();
                    State = OverlayState.Visible;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[OverlayService] Ошибка при обновлении целевого элемента: {ex.Message}");
                    State = OverlayState.Failed;
                }
            }
            else
            {
                try
                {
                    ApplyTargetVisualsInternal();
                }
                catch
                {
                    // Игнорируем ошибки отрисовки в скрытом состоянии
                }
            }
        }
    }

    /// <summary>
    /// Закрывает окно оверлея и освобождает ресурсы окна.
    /// </summary>
    public void Close()
    {
        lock (_syncLock)
        {
            if (_isDisposed)
            {
                return;
            }

            Hide();

            try
            {
                _overlayWindow.CloseWindow();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OverlayService] Ошибка при закрытии окна оверлея: {ex.Message}");
            }
        }
    }

    private void ApplyTargetVisualsInternal()
    {
        var target = _currentTarget;
        bool isMissingTarget = ValidateTarget(target);

        var virtualBounds = GetVirtualScreenBounds();
        var callout = CalloutPositionCalculator.Calculate(
            isMissingTarget ? null : target,
            DefaultCalloutWidth,
            DefaultCalloutHeight,
            virtualBounds
        );

        _overlayWindow.UpdateVisuals(target, callout, isMissingTarget);
    }

    private static bool ValidateTarget(OverlayTargetInfo? target)
    {
        if (target == null || !target.IsTargetValid)
        {
            return true; // Missing target
        }

        if (target.BoundingRectangle.Width <= 0 || target.BoundingRectangle.Height <= 0)
        {
            return true; // Missing target due to empty bounds
        }

        if (target.WindowHandle != 0)
        {
            try
            {
                if (!NativeMethods.IsWindow((nint)target.WindowHandle))
                {
                    return true; // Missing target because target window was closed
                }
            }
            catch
            {
                return true;
            }
        }

        return false; // Valid target
    }

    private BoundingBox GetVirtualScreenBounds()
    {
        if (_overlayWindow is NativeOverlayWindow nativeWin && nativeWin.VirtualWidth > 0 && nativeWin.VirtualHeight > 0)
        {
            return new BoundingBox(nativeWin.VirtualX, nativeWin.VirtualY, nativeWin.VirtualWidth, nativeWin.VirtualHeight);
        }

        try
        {
            var x = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
            var y = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
            var width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
            var height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);

            if (width <= 0) width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
            if (height <= 0) height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);
            if (width <= 0) width = 1920;
            if (height <= 0) height = 1080;

            return new BoundingBox(x, y, width, height);
        }
        catch
        {
            return new BoundingBox(0, 0, 1920, 1080);
        }
    }

    private void OnActiveWindowChanged(object? sender, ActiveWindowInfo activeWindow)
    {
        lock (_syncLock)
        {
            if (_isDisposed || _state != OverlayState.Visible || _currentTarget == null)
            {
                return;
            }

            try
            {
                // При смене активного окна проверяем геометрию и валидность окна целевого элемента
                if (_currentTarget.WindowHandle != 0)
                {
                    UpdateTarget(_currentTarget);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OverlayService] Ошибка при обработке смены активного окна: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        lock (_syncLock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            if (_windowTracker != null)
            {
                _windowTracker.ActiveWindowChanged -= OnActiveWindowChanged;
            }

            Close();

            if (_overlayWindow is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[OverlayService] Ошибка при освобождении IOverlayWindow: {ex.Message}");
                }
            }
        }

        GC.SuppressFinalize(this);
    }
}
