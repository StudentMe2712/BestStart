namespace SelectCast.Prototype.Capture;

public enum CaptureStatus
{
    /// <summary>Text was selected and captured.</summary>
    Captured,
    /// <summary>Clipboard never changed within the timeout — no selection / app swallowed Ctrl+C.</summary>
    NoSelection,
    /// <summary>Clipboard changed but holds no text (image/files selected).</summary>
    NonText,
    /// <summary>Unexpected failure during capture.</summary>
    Error,
}

/// <param name="Status">Outcome of the capture attempt.</param>
/// <param name="Text">Captured text, when <see cref="CaptureStatus.Captured"/>.</param>
/// <param name="ElapsedMs">Measured clipboard latency (Ctrl+C → clipboard updated).</param>
/// <param name="Diagnostics">Step-by-step log for the Stage-0 report.</param>
public sealed record CaptureResult(
    CaptureStatus Status,
    string? Text,
    long ElapsedMs,
    string Diagnostics);
