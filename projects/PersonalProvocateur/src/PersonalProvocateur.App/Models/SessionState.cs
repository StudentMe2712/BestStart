namespace PersonalProvocateur.App.Models;

/// <summary>
/// High-level session states of the application runtime.
/// </summary>
public enum SessionState
{
    BackgroundIdle,
    Paused,
    MascotOverlayActive,
    VideoPlaying
}
