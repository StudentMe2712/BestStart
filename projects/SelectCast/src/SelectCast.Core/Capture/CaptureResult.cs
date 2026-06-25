namespace SelectCast.Core.Capture;

public enum CaptureStatus
{
    /// <summary>Text was selected and captured.</summary>
    Captured,
    /// <summary>Clipboard never changed within the timeout — no selection / app swallowed Ctrl+C.</summary>
    NoSelection,
    /// <summary>Clipboard changed but holds no text (image/files selected).</summary>
    NonText,
    /// <summary>Key injection was blocked (UIPI / input blocking) — SendInput delivered nothing.</summary>
    Blocked,
    /// <summary>Unexpected failure during capture.</summary>
    Error,
}

/// <param name="Status">Outcome of the capture attempt.</param>
/// <param name="Text">Captured text, when <see cref="CaptureStatus.Captured"/>.</param>
/// <param name="ElapsedMs">Measured clipboard latency (Ctrl+C → clipboard updated).</param>
/// <param name="Diagnostics">Step-by-step log, useful for diagnostics.</param>
public sealed record CaptureResult(
    CaptureStatus Status,
    string? Text,
    long ElapsedMs,
    string Diagnostics)
{
    /// <summary>True when capture produced usable text.</summary>
    public bool HasText => Status == CaptureStatus.Captured && !string.IsNullOrEmpty(Text);
}
