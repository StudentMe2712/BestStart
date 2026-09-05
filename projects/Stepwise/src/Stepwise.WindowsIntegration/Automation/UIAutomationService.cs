using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Stepwise.WindowsIntegration.Native;

namespace Stepwise.WindowsIntegration.Automation;

/// <summary>
/// Сервис инспекции элементов интерфейса Windows на базе Microsoft UI Automation с отказоустойчивым Win32 Fallback.
/// Обеспечивает надежное извлечение всех 11 свойств ElementInfo с защитой от COM-исключений,
/// вычислением реальных оконных границ при пустом BoundingRectangle и устойчивостью к исчезающим элементам.
/// </summary>
public sealed class UIAutomationService : IUIAutomationService
{
    /// <inheritdoc />
    public ElementInfo InspectElementAt(int x, int y)
    {
        return InspectElementAt(x, y, null);
    }

    /// <summary>
    /// Извлекает метаданные элемента UI по экранным координатам с учетом контекста окна для fallback.
    /// </summary>
    /// <param name="x">Координата X на экране.</param>
    /// <param name="y">Координата Y на экране.</param>
    /// <param name="fallbackContext">Опциональный контекст окна.</param>
    /// <returns>Метаданные элемента <see cref="ElementInfo"/>.</returns>
    public ElementInfo InspectElementAt(int x, int y, WindowContext? fallbackContext)
    {
        try
        {
            var uiaPoint = new System.Windows.Point(x, y);
            var element = AutomationElement.FromPoint(uiaPoint);

            if (element != null)
            {
                var info = ExtractElementInfoFromUia(element, x, y, fallbackContext);
                if (info != null && info != ElementInfo.Unknown)
                {
                    return info;
                }
            }
        }
        catch (ElementNotAvailableException)
        {
            // Элемент исчез или закрылся в момент клика — переходим к Win32 fallback
        }
        catch (COMException ex)
        {
            Debug.WriteLine($"[UIAutomationService] COM-исключение при UIA инспекции: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"[UIAutomationService] Недопустимая операция при UIA инспекции: {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UIAutomationService] Предупреждение при UIA инспекции: {ex.Message}");
        }

        return FallbackWin32Inspection(x, y, fallbackContext);
    }

    internal static ElementInfo ExtractElementInfoFromUia(
        AutomationElement element,
        int x = 0,
        int y = 0,
        WindowContext? fallbackContext = null)
    {
        ArgumentNullException.ThrowIfNull(element);

        string name = string.Empty;
        string controlType = "Unknown";
        string automationId = string.Empty;
        string className = string.Empty;
        int processId = 0;
        BoundingBox boundingBox = BoundingBox.Empty;
        bool isPassword = false;
        string frameworkId = "Unknown";
        nint windowHandle = nint.Zero;

        bool hasReadAnyUiaProperty = false;

        // 1. Name
        try
        {
            name = element.Current.Name ?? string.Empty;
            hasReadAnyUiaProperty = true;
        }
        catch (ElementNotAvailableException) { }
        catch (COMException) { }
        catch (InvalidOperationException) { }
        catch (Exception) { }

        if (string.IsNullOrEmpty(name))
        {
            try
            {
                var val = element.GetCurrentPropertyValue(AutomationElement.NameProperty);
                if (val is string s)
                {
                    name = s;
                    hasReadAnyUiaProperty = true;
                }
            }
            catch { }
        }

        // 2. ControlType
        try
        {
            var ct = element.Current.ControlType;
            if (ct != null)
            {
                controlType = ct.ProgrammaticName?.Replace("ControlType.", string.Empty) ?? "Unknown";
                hasReadAnyUiaProperty = true;
            }
        }
        catch (ElementNotAvailableException) { }
        catch (COMException) { }
        catch (InvalidOperationException) { }
        catch (Exception) { }

        if (controlType == "Unknown")
        {
            try
            {
                var val = element.GetCurrentPropertyValue(AutomationElement.ControlTypeProperty);
                if (val is ControlType ct)
                {
                    controlType = ct.ProgrammaticName?.Replace("ControlType.", string.Empty) ?? "Unknown";
                    hasReadAnyUiaProperty = true;
                }
            }
            catch { }
        }

        // 3. AutomationId (НЕ фабриковать при отсутствии!)
        try
        {
            automationId = element.Current.AutomationId ?? string.Empty;
            hasReadAnyUiaProperty = true;
        }
        catch (ElementNotAvailableException) { }
        catch (COMException) { }
        catch (InvalidOperationException) { }
        catch (Exception) { }

        if (string.IsNullOrEmpty(automationId))
        {
            try
            {
                var val = element.GetCurrentPropertyValue(AutomationElement.AutomationIdProperty);
                if (val is string aid)
                {
                    automationId = aid;
                    hasReadAnyUiaProperty = true;
                }
            }
            catch { }
        }

        // 4. ClassName
        try
        {
            className = element.Current.ClassName ?? string.Empty;
            hasReadAnyUiaProperty = true;
        }
        catch (ElementNotAvailableException) { }
        catch (COMException) { }
        catch (InvalidOperationException) { }
        catch (Exception) { }

        if (string.IsNullOrEmpty(className))
        {
            try
            {
                var val = element.GetCurrentPropertyValue(AutomationElement.ClassNameProperty);
                if (val is string cName)
                {
                    className = cName;
                    hasReadAnyUiaProperty = true;
                }
            }
            catch { }
        }

        // 5. ProcessId
        try
        {
            processId = element.Current.ProcessId;
            if (processId > 0)
            {
                hasReadAnyUiaProperty = true;
            }
        }
        catch (ElementNotAvailableException) { }
        catch (COMException) { }
        catch (InvalidOperationException) { }
        catch (Exception) { }

        if (processId <= 0)
        {
            try
            {
                var val = element.GetCurrentPropertyValue(AutomationElement.ProcessIdProperty);
                if (val is int pid && pid > 0)
                {
                    processId = pid;
                    hasReadAnyUiaProperty = true;
                }
            }
            catch { }
        }

        // 6. BoundingRectangle
        try
        {
            var rect = element.Current.BoundingRectangle;
            if (!rect.IsEmpty && rect.Width > 0 && rect.Height > 0 && !double.IsInfinity(rect.X) && !double.IsInfinity(rect.Y))
            {
                boundingBox = new BoundingBox(rect.X, rect.Y, rect.Width, rect.Height);
                hasReadAnyUiaProperty = true;
            }
        }
        catch (ElementNotAvailableException) { }
        catch (COMException) { }
        catch (InvalidOperationException) { }
        catch (Exception) { }

        if (boundingBox.IsEmpty)
        {
            try
            {
                var val = element.GetCurrentPropertyValue(AutomationElement.BoundingRectangleProperty);
                if (val is System.Windows.Rect rect && !rect.IsEmpty && rect.Width > 0 && rect.Height > 0 && !double.IsInfinity(rect.X) && !double.IsInfinity(rect.Y))
                {
                    boundingBox = new BoundingBox(rect.X, rect.Y, rect.Width, rect.Height);
                    hasReadAnyUiaProperty = true;
                }
            }
            catch { }
        }

        // 7. IsPassword
        try
        {
            isPassword = element.Current.IsPassword;
            hasReadAnyUiaProperty = true;
        }
        catch (ElementNotAvailableException) { }
        catch (COMException) { }
        catch (InvalidOperationException) { }
        catch (Exception) { }

        if (!isPassword)
        {
            try
            {
                var val = element.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty);
                if (val is bool b)
                {
                    isPassword = b;
                    hasReadAnyUiaProperty = true;
                }
            }
            catch { }
        }

        // 8. FrameworkId
        try
        {
            frameworkId = element.Current.FrameworkId ?? "Unknown";
            hasReadAnyUiaProperty = true;
        }
        catch (ElementNotAvailableException) { }
        catch (COMException) { }
        catch (InvalidOperationException) { }
        catch (Exception) { }

        if (string.IsNullOrEmpty(frameworkId) || frameworkId == "Unknown")
        {
            try
            {
                var val = element.GetCurrentPropertyValue(AutomationElement.FrameworkIdProperty);
                if (val is string fid && !string.IsNullOrEmpty(fid))
                {
                    frameworkId = fid;
                    hasReadAnyUiaProperty = true;
                }
            }
            catch { }
        }

        // 9. WindowHandle
        try
        {
            int rawHwnd = element.Current.NativeWindowHandle;
            if (rawHwnd != 0)
            {
                windowHandle = (nint)(uint)rawHwnd;
                hasReadAnyUiaProperty = true;
            }
        }
        catch (ElementNotAvailableException) { }
        catch (COMException) { }
        catch (InvalidOperationException) { }
        catch (Exception) { }

        if (windowHandle == nint.Zero)
        {
            try
            {
                var val = element.GetCurrentPropertyValue(AutomationElement.NativeWindowHandleProperty);
                if (val is int rawHwnd && rawHwnd != 0)
                {
                    windowHandle = (nint)(uint)rawHwnd;
                    hasReadAnyUiaProperty = true;
                }
            }
            catch { }
        }

        // Если ни одно свойство UIA прочитать не удалось (элемент исчез в момент обращения),
        // переходим к Win32 / контекстному fallback без падения
        if (!hasReadAnyUiaProperty)
        {
            if (x != 0 || y != 0)
            {
                return FallbackWin32Inspection(x, y, fallbackContext);
            }
            if (fallbackContext != null && fallbackContext != WindowContext.Empty)
            {
                return UIATargetResolver.CreateFallbackFromContext(fallbackContext);
            }
            return ElementInfo.Unknown;
        }

        // Разрешение WindowHandle, если UIA вернул 0 (например, для windowless WPF/WinUI контролов)
        if (windowHandle == nint.Zero || !NativeMethods.IsWindow(windowHandle))
        {
            if (x != 0 || y != 0)
            {
                var pt = new NativeMethods.POINT { X = x, Y = y };
                var ptHwnd = NativeMethods.WindowFromPoint(pt);
                if (ptHwnd != nint.Zero && NativeMethods.IsWindow(ptHwnd))
                {
                    windowHandle = ptHwnd;
                }
            }

            if ((windowHandle == nint.Zero || !NativeMethods.IsWindow(windowHandle)) &&
                fallbackContext != null && fallbackContext.WindowHandle != 0)
            {
                var ctxHwnd = (nint)fallbackContext.WindowHandle;
                if (NativeMethods.IsWindow(ctxHwnd))
                {
                    windowHandle = ctxHwnd;
                }
                else if (windowHandle == nint.Zero)
                {
                    windowHandle = ctxHwnd;
                }
            }
        }

        var rootWindowHandle = GetRootWindowHandle(windowHandle);
        var effectiveHandle = (rootWindowHandle != nint.Zero && NativeMethods.IsWindow(rootWindowHandle)) ? rootWindowHandle : windowHandle;

        // Разрешение заголовка окна
        string windowTitle = GetWindowTitle(effectiveHandle);
        if (string.IsNullOrEmpty(windowTitle) && windowHandle != effectiveHandle && windowHandle != nint.Zero)
        {
            windowTitle = GetWindowTitle(windowHandle);
        }
        if (string.IsNullOrEmpty(windowTitle) && fallbackContext != null && !string.IsNullOrEmpty(fallbackContext.WindowTitle))
        {
            windowTitle = fallbackContext.WindowTitle;
        }

        // Разрешение ProcessId
        if (processId <= 0 && windowHandle != nint.Zero && NativeMethods.IsWindow(windowHandle))
        {
            NativeMethods.GetWindowThreadProcessId(windowHandle, out var pid);
            processId = (int)pid;
        }
        if (processId <= 0 && fallbackContext != null && fallbackContext.ProcessId > 0)
        {
            processId = fallbackContext.ProcessId;
        }

        // Разрешение ProcessName с защитой от исключений при закрытом процессе
        string processName = GetProcessNameById(processId, fallbackContext);

        // Разрешение ClassName, если UIA не вернул его
        if (string.IsNullOrEmpty(className) && windowHandle != nint.Zero && NativeMethods.IsWindow(windowHandle))
        {
            className = GetWindowClassName(windowHandle);
        }

        // Извлечение реальных границ BoundingRectangle: если UIA-элемент возвращает пустой прямоугольник,
        // но известен WindowHandle, использовать Win32 GetWindowRect для получения оконных границ
        if (boundingBox.IsEmpty)
        {
            if (windowHandle != nint.Zero && NativeMethods.IsWindow(windowHandle))
            {
                boundingBox = GetWindowBounds(windowHandle);
            }
            if (boundingBox.IsEmpty && effectiveHandle != nint.Zero && effectiveHandle != windowHandle && NativeMethods.IsWindow(effectiveHandle))
            {
                boundingBox = GetWindowBounds(effectiveHandle);
            }
            if (boundingBox.IsEmpty && fallbackContext != null && !fallbackContext.Bounds.IsEmpty)
            {
                boundingBox = fallbackContext.Bounds;
            }
        }

        return new ElementInfo(
            Name: name,
            ControlType: controlType,
            AutomationId: automationId,
            ClassName: className,
            ProcessName: processName,
            ProcessId: processId,
            WindowTitle: windowTitle,
            WindowHandle: (long)effectiveHandle,
            BoundingRectangle: boundingBox,
            FrameworkId: frameworkId,
            IsPassword: isPassword
        );
    }

    internal static ElementInfo FallbackWin32Inspection(int x, int y, WindowContext? fallbackContext = null)
    {
        var pt = new NativeMethods.POINT { X = x, Y = y };
        var hwnd = NativeMethods.WindowFromPoint(pt);

        if (hwnd == nint.Zero || !NativeMethods.IsWindow(hwnd))
        {
            if (fallbackContext != null && fallbackContext != WindowContext.Empty)
            {
                return UIATargetResolver.CreateFallbackFromContext(fallbackContext);
            }
            return ElementInfo.Unknown;
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        int processId = (int)pid;
        string processName = GetProcessNameById(processId, fallbackContext);

        var rootHwnd = GetRootWindowHandle(hwnd);
        var effectiveHwnd = (rootHwnd != nint.Zero && NativeMethods.IsWindow(rootHwnd)) ? rootHwnd : hwnd;
        string windowTitle = GetWindowTitle(effectiveHwnd);
        if (string.IsNullOrEmpty(windowTitle) && hwnd != effectiveHwnd)
        {
            windowTitle = GetWindowTitle(hwnd);
        }
        if (string.IsNullOrEmpty(windowTitle) && fallbackContext != null && !string.IsNullOrEmpty(fallbackContext.WindowTitle))
        {
            windowTitle = fallbackContext.WindowTitle;
        }

        string className = GetWindowClassName(hwnd);
        var bounds = GetWindowBounds(hwnd);
        if (bounds.IsEmpty && effectiveHwnd != hwnd)
        {
            bounds = GetWindowBounds(effectiveHwnd);
        }
        if (bounds.IsEmpty && fallbackContext != null && !fallbackContext.Bounds.IsEmpty)
        {
            bounds = fallbackContext.Bounds;
        }

        return new ElementInfo(
            Name: string.Empty,
            ControlType: "WindowControl",
            AutomationId: string.Empty,
            ClassName: className,
            ProcessName: processName,
            ProcessId: processId,
            WindowTitle: windowTitle,
            WindowHandle: (long)effectiveHwnd,
            BoundingRectangle: bounds,
            FrameworkId: "Win32",
            IsPassword: false
        );
    }

    internal static string GetProcessNameById(int processId, WindowContext? fallbackContext = null)
    {
        if (processId <= 0)
        {
            return !string.IsNullOrEmpty(fallbackContext?.ProcessName) ? fallbackContext.ProcessName : "Unknown";
        }

        try
        {
            using var proc = Process.GetProcessById(processId);
            return proc.ProcessName;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UIAutomationService] Не удалось получить имя процесса {processId}: {ex.Message}");
            if (fallbackContext != null && !string.IsNullOrEmpty(fallbackContext.ProcessName) &&
                (fallbackContext.ProcessId == processId || fallbackContext.ProcessId <= 0))
            {
                return fallbackContext.ProcessName;
            }
            return "Unknown";
        }
    }

    internal static nint GetRootWindowHandle(nint hwnd)
    {
        if (hwnd == nint.Zero || !NativeMethods.IsWindow(hwnd))
        {
            return nint.Zero;
        }

        var root = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
        return (root != nint.Zero && NativeMethods.IsWindow(root)) ? root : hwnd;
    }

    internal static string GetWindowTitle(nint hwnd)
    {
        if (hwnd == nint.Zero || !NativeMethods.IsWindow(hwnd))
        {
            return string.Empty;
        }

        int length = NativeMethods.GetWindowTextLength(hwnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    internal static string GetWindowClassName(nint hwnd)
    {
        if (hwnd == nint.Zero || !NativeMethods.IsWindow(hwnd))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    internal static BoundingBox GetWindowBounds(nint hwnd)
    {
        if (hwnd == nint.Zero || !NativeMethods.IsWindow(hwnd))
        {
            return BoundingBox.Empty;
        }

        if (NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            if (width > 0 && height > 0)
            {
                return new BoundingBox(rect.Left, rect.Top, width, height);
            }
        }

        return BoundingBox.Empty;
    }
}
