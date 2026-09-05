using System.Diagnostics;
using System.Windows.Automation;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;

namespace Stepwise.WindowsIntegration.Automation;

/// <summary>
/// Реализация <see cref="ITargetResolver"/> на базе Microsoft UI Automation и Win32.
/// Разрешает целевой элемент для кликов мыши по экранным координатам, а для клавиатурных событий — через фокус ввода.
/// Никогда не выбрасывает необработанных исключений, гарантируя надежный fallback до контекста активного окна.
/// </summary>
public sealed class UIATargetResolver : ITargetResolver
{
    private readonly IUIAutomationService _uiaService;
    private readonly IActiveWindowTracker? _windowTracker;
    private readonly Func<AutomationElement?> _focusedElementProvider;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="UIATargetResolver"/>.
    /// </summary>
    /// <param name="uiaService">Сервис UI Automation для инспекции по координатам.</param>
    /// <param name="windowTracker">Опциональный трекер активного окна для дополнительного контекста.</param>
    /// <param name="focusedElementProvider">Опциональный провайдер сфокусированного элемента (для тестов).</param>
    public UIATargetResolver(
        IUIAutomationService uiaService,
        IActiveWindowTracker? windowTracker = null,
        Func<AutomationElement?>? focusedElementProvider = null)
    {
        _uiaService = uiaService ?? throw new ArgumentNullException(nameof(uiaService));
        _windowTracker = windowTracker;
        _focusedElementProvider = focusedElementProvider ?? (() => AutomationElement.FocusedElement);
    }

    private readonly object _stateLock = new();
    private ElementInfo? _lastResolvedElement;

    /// <inheritdoc />
    public Task<ElementInfo> ResolveTargetAsync(
        SemanticAction action,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var effectiveContext = GetEffectiveContext(action.Context);

            if (action.X.HasValue && action.Y.HasValue)
            {
                var element = _uiaService.InspectElementAt(action.X.Value, action.Y.Value);
                if (element != null && element != ElementInfo.Unknown)
                {
                    lock (_stateLock)
                    {
                        _lastResolvedElement = element;
                    }
                    return Task.FromResult(element);
                }

                return Task.FromResult(CreateFallbackFromContext(effectiveContext));
            }

            var focusedElementInfo = ResolveFocusedElement(effectiveContext);
            if (focusedElementInfo != null &&
                focusedElementInfo != ElementInfo.Unknown &&
                focusedElementInfo.ControlType != "WindowControl")
            {
                lock (_stateLock)
                {
                    _lastResolvedElement = focusedElementInfo;
                }
                return Task.FromResult(focusedElementInfo);
            }

            lock (_stateLock)
            {
                if (_lastResolvedElement != null &&
                    (effectiveContext.ProcessId == 0 ||
                     _lastResolvedElement.ProcessId == effectiveContext.ProcessId ||
                     string.Equals(_lastResolvedElement.ProcessName, effectiveContext.ProcessName, StringComparison.OrdinalIgnoreCase)))
                {
                    return Task.FromResult(_lastResolvedElement);
                }
            }

            return Task.FromResult(focusedElementInfo ?? CreateFallbackFromContext(effectiveContext));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UIATargetResolver] Ошибка при разрешении целевого элемента: {ex.Message}");
            return Task.FromResult(CreateFallbackFromContext(action.Context));
        }
    }

    private ElementInfo ResolveFocusedElement(WindowContext effectiveContext)
    {
        try
        {
            var focused = _focusedElementProvider();
            if (focused != null && focused != AutomationElement.RootElement)
            {
                var info = UIAutomationService.ExtractElementInfoFromUia(focused, 0, 0, effectiveContext);
                // Если контекст действия задан, проверяем, что сфокусированный элемент относится к тому же процессу
                if (effectiveContext.ProcessId > 0 && info.ProcessId > 0 && info.ProcessId != effectiveContext.ProcessId)
                {
                    return CreateFallbackFromContext(effectiveContext);
                }

                return info;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UIATargetResolver] Не удалось получить сфокусированный элемент UIA: {ex.Message}");
        }

        return CreateFallbackFromContext(effectiveContext);
    }

    private WindowContext GetEffectiveContext(WindowContext? context)
    {
        if (context != null && context != WindowContext.Empty)
        {
            return context;
        }

        if (_windowTracker != null)
        {
            var activeWin = _windowTracker.GetActiveWindow();
            if (activeWin != null)
            {
                return WindowContext.FromActiveWindowInfo(activeWin);
            }
        }

        return context ?? WindowContext.Empty;
    }

    internal static ElementInfo CreateFallbackFromContext(WindowContext? context)
    {
        if (context == null || context == WindowContext.Empty)
        {
            return ElementInfo.Unknown;
        }

        return new ElementInfo(
            Name: string.Empty,
            ControlType: "WindowControl",
            AutomationId: string.Empty,
            ClassName: string.Empty,
            ProcessName: string.IsNullOrEmpty(context.ProcessName) ? "Unknown" : context.ProcessName,
            ProcessId: context.ProcessId,
            WindowTitle: context.WindowTitle ?? string.Empty,
            WindowHandle: context.WindowHandle,
            BoundingRectangle: context.Bounds,
            FrameworkId: "Win32",
            IsPassword: false
        );
    }
}
