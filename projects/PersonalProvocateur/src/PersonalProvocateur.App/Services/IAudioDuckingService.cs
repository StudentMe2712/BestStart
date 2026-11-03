using System;

namespace PersonalProvocateur.App.Services;

/// <summary>
/// Service managing system-wide background audio attenuation, muting, or pausing during video playback.
/// </summary>
public interface IAudioDuckingService : IDisposable
{
    /// <summary>
    /// Gets whether background audio sessions are currently ducked or muted.
    /// </summary>
    bool IsDucked { get; }

    /// <summary>
    /// Attenuates or mutes other running applications' audio sessions.
    /// </summary>
    /// <param name="mode">Ducking mode: "Mute", "Duck", "PauseMedia", or "Disabled".</param>
    /// <param name="duckLevel">Target volume level (0.0 to 1.0) when mode is "Duck".</param>
    void DuckOtherApplications(string mode, float duckLevel = 0.15f);

    /// <summary>
    /// Restores previously modified audio sessions to their original volume and mute state.
    /// </summary>
    void RestoreOtherApplications();
}
