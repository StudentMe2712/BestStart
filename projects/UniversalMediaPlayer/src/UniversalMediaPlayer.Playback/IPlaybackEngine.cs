using UniversalMediaPlayer.Core.Models;

namespace UniversalMediaPlayer.Playback;

public interface IPlaybackEngine : IAsyncDisposable
{
    bool IsInitialized { get; }
    bool IsPlaying { get; }
    double CurrentPositionSeconds { get; }
    double DurationSeconds { get; }
    MediaPackage? CurrentPackage { get; }
    IReadOnlyList<MediaTrack> ActiveTracks { get; }

    Task InitializeAsync(nint windowHandle = 0, CancellationToken ct = default);
    Task OpenAsync(MediaPackage package, CancellationToken ct = default);
    Task PlayAsync();
    Task PauseAsync();
    Task StopAsync();
    Task SeekAsync(double seconds, bool relative = true);
    Task SetVolumeAsync(int volume);
    Task SetFullscreenAsync(bool fullscreen);
    Task ToggleFullscreenAsync();
    Task SelectAudioTrackAsync(int trackId);
    Task SelectSubtitleTrackAsync(int trackId);
    Task SetSubtitleVisibilityAsync(bool visible);
    Task SetPropertyAsync(string property, string value);
    Task<string?> GetPropertyAsync(string property);
    Task SendCommandAsync(params string[] args);

    event Action<bool>? PlaybackStateChanged;
    event Action<double, double>? TimeUpdated;
    event Action<IReadOnlyList<MediaTrack>>? TracksChanged;
}
