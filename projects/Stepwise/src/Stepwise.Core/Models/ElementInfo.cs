namespace Stepwise.Core.Models;

/// <summary>
/// Информация об элементе пользовательского интерфейса, полученная через Microsoft UI Automation.
/// </summary>
public sealed record ElementInfo(
    string Name,
    string ControlType,
    string AutomationId,
    string ClassName,
    string ProcessName,
    int ProcessId,
    string WindowTitle,
    long WindowHandle,
    BoundingBox BoundingRectangle
)
{
    public static ElementInfo Unknown => new(
        Name: string.Empty,
        ControlType: "Unknown",
        AutomationId: string.Empty,
        ClassName: string.Empty,
        ProcessName: "Unknown",
        ProcessId: 0,
        WindowTitle: string.Empty,
        WindowHandle: 0,
        BoundingRectangle: BoundingBox.Empty
    );
}
