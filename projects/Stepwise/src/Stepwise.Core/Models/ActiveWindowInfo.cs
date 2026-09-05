namespace Stepwise.Core.Models;

/// <summary>
/// Информация об активном (переднем) окне Windows, зафиксированном через WinEvent hook.
/// </summary>
public sealed record ActiveWindowInfo(
    long WindowHandle,
    int ProcessId,
    string ProcessName,
    string WindowTitle,
    BoundingBox Bounds,
    DateTime Timestamp
)
{
    public static ActiveWindowInfo Empty => new(
        WindowHandle: 0,
        ProcessId: 0,
        ProcessName: string.Empty,
        WindowTitle: string.Empty,
        Bounds: BoundingBox.Empty,
        Timestamp: DateTime.MinValue
    );
}
