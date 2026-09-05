using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Stepwise.WindowsIntegration.Native;

namespace Stepwise.WindowsIntegration.Automation;

/// <summary>
/// Реализация <see cref="ITargetResolver"/> на базе Microsoft UI Automation и Win32.
/// Разрешает целевой элемент в соответствии с 4-уровневым контрактом:
/// 1. Валидный UIA-элемент по координатам (включая DragAndDrop и Scroll) или фокусу ввода.
/// 2. Частичный ElementInfo (без фабрикации фиктивных AutomationId или Name).
/// 3. Оконный уровень (Win32 WindowFromPoint / GetAncestor с реальными заголовком, классом, PID и границами).
/// 4. Контекст активного окна (action.Context) / ElementInfo.Unknown.
/// Никогда не выбрасывает необработанных исключений при исчезающих элементах или закрытых окнах.
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

            // 1. Семантические действия оконного уровня (WindowActivated, WindowClosed, ManualStep):
            // Разрешаются напрямую на основе контекста окна (action.Context)
            if (action.ActionType is SemanticActionType.WindowActivated
                or SemanticActionType.WindowClosed
                or SemanticActionType.ManualStep)
            {
                lock (_stateLock)
                {
                    // При закрытии окна или смене процесса сбрасываем кэш последнего элемента
                    if (action.ActionType is SemanticActionType.WindowClosed ||
                        (effectiveContext.ProcessId > 0 && _lastResolvedElement?.ProcessId != effectiveContext.ProcessId))
                    {
                        _lastResolvedElement = null;
                    }
                }

                return Task.FromResult(CreateFallbackFromContext(effectiveContext));
            }

            // 2. Мышиные и координатные действия (Clicks, DragAndDrop по startX/Y, Scroll по X/Y)
            if (action.X.HasValue && action.Y.HasValue)
            {
                ElementInfo element;
                if (_uiaService is UIAutomationService concreteUia)
                {
                    element = concreteUia.InspectElementAt(action.X.Value, action.Y.Value, effectiveContext);
                }
                else
                {
                    element = _uiaService.InspectElementAt(action.X.Value, action.Y.Value);
                }

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

            // 3. Клавиатурные действия (TextInput, KeyPress, Shortcut): фокус ввода
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

            // Проверяем кэшированный ранее элемент того же процесса
            lock (_stateLock)
            {
                if (_lastResolvedElement != null &&
                    _lastResolvedElement != ElementInfo.Unknown &&
                    (effectiveContext.ProcessId == 0 ||
                     _lastResolvedElement.ProcessId == effectiveContext.ProcessId ||
                     string.Equals(_lastResolvedElement.ProcessName, effectiveContext.ProcessName, StringComparison.OrdinalIgnoreCase)))
                {
                    return Task.FromResult(_lastResolvedElement);
                }
            }

            // 4. Оконный fallback / Контекст
            return Task.FromResult(focusedElementInfo != null && focusedElementInfo != ElementInfo.Unknown
                ? focusedElementInfo
                : CreateFallbackFromContext(effectiveContext));
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
                if (info == null || info == ElementInfo.Unknown)
                {
                    return CreateFallbackFromContext(effectiveContext);
                }

                // Если контекст действия задан, проверяем, что сфокусированный элемент относится к тому же процессу
                if (effectiveContext.ProcessId > 0 && info.ProcessId > 0 && info.ProcessId != effectiveContext.ProcessId)
                {
                    return CreateFallbackFromContext(effectiveContext);
                }

                return info;
            }
        }
        catch (ElementNotAvailableException ex)
        {
            Debug.WriteLine($"[UIATargetResolver] Сфокусированный элемент исчез: {ex.Message}");
        }
        catch (COMException ex)
        {
            Debug.WriteLine($"[UIATargetResolver] COM-исключение при получении фокуса: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"[UIATargetResolver] Недопустимая операция при получении фокуса: {ex.Message}");
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

        string className = string.Empty;
        var bounds = context.Bounds;
        string windowTitle = context.WindowTitle ?? string.Empty;
        int processId = context.ProcessId;
        string processName = string.IsNullOrEmpty(context.ProcessName) ? "Unknown" : context.ProcessName;

        if (context.WindowHandle != 0)
        {
            nint hwnd = (nint)context.WindowHandle;
            if (NativeMethods.IsWindow(hwnd))
            {
                try
                {
                    className = UIAutomationService.GetWindowClassName(hwnd);
                    if (bounds.IsEmpty)
                    {
                        bounds = UIAutomationService.GetWindowBounds(hwnd);
                    }
                    if (string.IsNullOrEmpty(windowTitle))
                    {
                        windowTitle = UIAutomationService.GetWindowTitle(hwnd);
                    }
                    if (processId <= 0)
                    {
                        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
                        processId = (int)pid;
                    }
                    if (processName == "Unknown" || string.IsNullOrEmpty(processName))
                    {
                        processName = UIAutomationService.GetProcessNameById(processId, context);
                    }
                }
                catch
                {
                    // Сохраняем значения контекста при любых сбоях Win32
                }
            }
        }

        return new ElementInfo(
            Name: string.Empty,
            ControlType: "WindowControl",
            AutomationId: string.Empty,
            ClassName: className,
            ProcessName: processName,
            ProcessId: processId,
            WindowTitle: windowTitle,
            WindowHandle: context.WindowHandle,
            BoundingRectangle: bounds,
            FrameworkId: "Win32",
            IsPassword: false
        );
    }
}
