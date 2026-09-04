namespace Stepwise.Core.Models;

/// <summary>
/// Атомарный шаг интерактивной инструкции.
/// </summary>
public sealed record Step(
    Guid Id,
    int SequenceIndex,
    DateTime Timestamp,
    ActionType Action,
    double ClickX,
    double ClickY,
    ElementInfo TargetElement,
    string? ScreenshotPath = null,
    string? Title = null,
    string? Description = null,
    Dictionary<string, string>? Metadata = null
);
