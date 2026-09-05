using System.Diagnostics;
using System.Text;
using System.Windows.Automation;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Stepwise.WindowsIntegration.Native;

namespace Stepwise.WindowsIntegration.Automation;

/// <summary>
/// Сервис инспекции элементов интерфейса Windows на базе Microsoft UI Automation с отказоустойчивым Win32 Fallback.
/// </summary>
public sealed class UIAutomationService : IUIAutomationService
{
    public ElementInfo InspectElementAt(int x, int y)
    {
        try
        {
            var uiaPoint = new System.Windows.Point(x, y);
            var element = AutomationElement.FromPoint(uiaPoint);

            if (element != null)
            {
                return ExtractElementInfoFromUia(element, x, y);
            }
        }
        catch (ElementNotAvailableException)
        {
            // Элемент исчез или закрылся в момент клика — переходим к Win32 fallback
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UIAutomationService] Предупреждение при UIA инспекции: {ex.Message}");
        }

        return FallbackWin32Inspection(x, y);
    }

    internal static ElementInfo ExtractElementInfoFromUia(
        AutomationElement element,
        int x = 0,
        int y = 0,
        WindowContext? fallbackContext = null)
    {
        string name = string.Empty;
        string controlType = "Unknown";
        string automationId = string.Empty;
        string className = string.Empty;
        int processId = 0;
        BoundingBox boundingBox = BoundingBox.Empty;

        try { name = element.Current.Name ?? string.Empty; } catch { }
        try { controlType = element.Current.ControlType?.ProgrammaticName?.Replace("ControlType.", string.Empty) ?? "Unknown"; } catch { }
        try { automationId = element.Current.AutomationId ?? string.Empty; } catch { }
        try { className = element.Current.ClassName ?? string.Empty; } catch { }
        try { processId = element.Current.ProcessId; } catch { }
        try
        {
            var rect = element.Current.BoundingRectangle;
            if (!rect.IsEmpty)
            {
                boundingBox = new BoundingBox(rect.X, rect.Y, rect.Width, rect.Height);
            }
            else if (fallbackContext != null && !fallbackContext.Bounds.IsEmpty)
            {
                boundingBox = fallbackContext.Bounds;
            }
        }
        catch
        {
            if (fallbackContext != null && !fallbackContext.Bounds.IsEmpty)
            {
                boundingBox = fallbackContext.Bounds;
            }
        }

        bool isPassword = false;
        try
        {
            isPassword = element.Current.IsPassword;
        }
        catch
        {
            try
            {
                var val = element.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty);
                if (val is bool b)
                {
                    isPassword = b;
                }
            }
            catch { }
        }

        string frameworkId = "Unknown";
        try { frameworkId = element.Current.FrameworkId ?? "Unknown"; } catch { }

        // Извлекаем имя процесса
        string processName = GetProcessNameById(processId);

        // Извлекаем дескриптор и заголовок окна верхнего уровня
        nint windowHandle = nint.Zero;
        try { windowHandle = (nint)(uint)element.Current.NativeWindowHandle; } catch { }

        if (windowHandle == nint.Zero || !NativeMethods.IsWindow(windowHandle))
        {
            if (x != 0 || y != 0)
            {
                var pt = new NativeMethods.POINT { X = x, Y = y };
                windowHandle = NativeMethods.WindowFromPoint(pt);
            }
            else if (fallbackContext != null && fallbackContext.WindowHandle != 0)
            {
                windowHandle = (nint)fallbackContext.WindowHandle;
            }
        }

        var rootWindowHandle = GetRootWindowHandle(windowHandle);
        var effectiveHandle = rootWindowHandle != nint.Zero ? rootWindowHandle : windowHandle;
        string windowTitle = GetWindowTitle(effectiveHandle);
        if (string.IsNullOrEmpty(windowTitle) && fallbackContext != null && !string.IsNullOrEmpty(fallbackContext.WindowTitle))
        {
            windowTitle = fallbackContext.WindowTitle;
        }

        if (processId <= 0 && fallbackContext != null && fallbackContext.ProcessId > 0)
        {
            processId = fallbackContext.ProcessId;
        }

        if ((processName == "Unknown" || string.IsNullOrEmpty(processName)) &&
            fallbackContext != null && !string.IsNullOrEmpty(fallbackContext.ProcessName))
        {
            processName = fallbackContext.ProcessName;
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

    private ElementInfo FallbackWin32Inspection(int x, int y)
    {
        var pt = new NativeMethods.POINT { X = x, Y = y };
        var hwnd = NativeMethods.WindowFromPoint(pt);

        if (hwnd == nint.Zero)
        {
            return ElementInfo.Unknown;
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        int processId = (int)pid;
        string processName = GetProcessNameById(processId);

        var rootHwnd = GetRootWindowHandle(hwnd);
        var effectiveHwnd = rootHwnd != nint.Zero ? rootHwnd : hwnd;
        string windowTitle = GetWindowTitle(effectiveHwnd);
        string className = GetWindowClassName(hwnd);

        return new ElementInfo(
            Name: string.Empty,
            ControlType: "WindowControl",
            AutomationId: string.Empty,
            ClassName: className,
            ProcessName: processName,
            ProcessId: processId,
            WindowTitle: windowTitle,
            WindowHandle: (long)effectiveHwnd,
            BoundingRectangle: BoundingBox.Empty,
            FrameworkId: "Win32",
            IsPassword: false
        );
    }

    private static string GetProcessNameById(int processId)
    {
        if (processId <= 0)
        {
            return "Unknown";
        }

        try
        {
            using var proc = Process.GetProcessById(processId);
            return proc.ProcessName;
        }
        catch
        {
            return "Unknown";
        }
    }

    private static nint GetRootWindowHandle(nint hwnd)
    {
        if (hwnd == nint.Zero)
        {
            return nint.Zero;
        }

        var root = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
        return root != nint.Zero ? root : hwnd;
    }

    private static string GetWindowTitle(nint hwnd)
    {
        if (hwnd == nint.Zero)
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

    private static string GetWindowClassName(nint hwnd)
    {
        if (hwnd == nint.Zero)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }
}
