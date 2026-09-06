namespace UniversalMediaPlayer.Core.Enums;

/// <summary>
/// Represents the deterministic runtime playback state of the media player engine.
/// </summary>
public enum PlaybackState
{
    Stopped = 0,
    Loading = 1,
    Playing = 2,
    Paused = 3,
    Error = 4
}
