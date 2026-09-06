using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using UniversalMediaPlayer.Core.Enums;
using UniversalMediaPlayer.Core.Models;
using UniversalMediaPlayer.Playback.Native;

namespace UniversalMediaPlayer.Playback;

public sealed class MpvPlaybackEngine : IPlaybackEngine
{
    private nint _handle;
    private readonly object _lock = new();
    private readonly SemaphoreSlim _openLock = new(1, 1);
    private CancellationTokenSource? _cts;
    private Task? _eventLoopTask;

    public bool IsInitialized => _handle != 0;
    public bool IsPlaying => State == PlaybackState.Playing;
    public PlaybackState State { get; private set; } = PlaybackState.Stopped;
    public double CurrentPositionSeconds { get; private set; }
    public double DurationSeconds { get; private set; }
    public MediaPackage? CurrentPackage { get; private set; }
    public IReadOnlyList<MediaTrack> ActiveTracks { get; private set; } = [];

    public event Action<bool>? PlaybackStateChanged;
    public event Action<PlaybackState>? StateChanged;
    public event Action<double, double>? TimeUpdated;
    public event Action<IReadOnlyList<MediaTrack>>? TracksChanged;

    private void SetState(PlaybackState newState)
    {
        if (State != newState)
        {
            var oldPlaying = IsPlaying;
            State = newState;
            StateChanged?.Invoke(newState);
            var newPlaying = IsPlaying;
            if (oldPlaying != newPlaying)
            {
                PlaybackStateChanged?.Invoke(newPlaying);
            }
        }
    }

    public Task InitializeAsync(nint windowHandle = 0, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_handle != 0) return Task.CompletedTask;

            _handle = LibMpvNative.mpv_create();
            if (_handle == 0)
            {
                throw new InvalidOperationException("Failed to allocate mpv_handle via mpv_create().");
            }

            // Set pre-initialization options
            if (windowHandle != 0)
            {
                SetOptionStringUnsafe("wid", windowHandle.ToString());
                SetOptionStringUnsafe("vo", "gpu-next,gpu");
                SetOptionStringUnsafe("gpu-context", "d3d11");
                SetOptionStringUnsafe("hwdec", "d3d11va,auto-safe");
                SetOptionStringUnsafe("d3d11-exclusive-fs", "no");
                SetOptionStringUnsafe("force-window", "yes");
                SetOptionStringUnsafe("hidpi-window-scale", "no");
                SetOptionStringUnsafe("input-vo-keyboard", "no");
                SetOptionStringUnsafe("input-default-bindings", "no");
                SetOptionStringUnsafe("osc", "no");
                SetOptionStringUnsafe("osd-bar", "no");
                SetOptionStringUnsafe("input-ipc-server", @"\\.\pipe\ump-mpv-pipe");
            }
            else
            {
                // Headless mode for unit tests and verification
                SetOptionStringUnsafe("vo", "null");
                SetOptionStringUnsafe("ao", "null");
            }

            // Keep player alive when file reaches EOF
            SetOptionStringUnsafe("keep-open", "yes");
            SetOptionStringUnsafe("hr-seek", "yes");
            SetOptionStringUnsafe("audio-file-auto", "no");
            SetOptionStringUnsafe("sub-auto", "no");
            SetOptionStringUnsafe("pause", "yes");

            var err = LibMpvNative.mpv_initialize(_handle);
            if (err < 0)
            {
                var errMsg = GetErrorString(err);
                LibMpvNative.mpv_terminate_destroy(_handle);
                _handle = 0;
                throw new InvalidOperationException($"Failed to initialize mpv: {errMsg} (code {err})");
            }

            // Observe properties for deterministic state tracking
            ObservePropertyUnsafe(1, "pause");
            ObservePropertyUnsafe(2, "playback-time");
            ObservePropertyUnsafe(3, "duration");
            ObservePropertyUnsafe(4, "eof-reached");

            _cts = new CancellationTokenSource();
            _eventLoopTask = Task.Run(() => EventLoop(_cts.Token));
        }

        return Task.CompletedTask;
    }

    public async Task OpenAsync(MediaPackage package, CancellationToken ct = default)
    {
        EnsureInitialized();
        await _openLock.WaitAsync(ct);
        try
        {
            ct.ThrowIfCancellationRequested();
            SetState(PlaybackState.Loading);
            CurrentPackage = package;

            // Ensure playback does not race to EOF before attaching external tracks
            await PauseAsync();

            // 1. If package contains a fonts directory, bind sub-fonts-dir; otherwise clear it
            if (package.Fonts is { HasFonts: true } && Directory.Exists(package.Fonts.FontsDirectory))
            {
                await SetPropertyAsync("sub-fonts-dir", package.Fonts.FontsDirectory);
            }
            else
            {
                await SetPropertyAsync("sub-fonts-dir", "");
            }

            ct.ThrowIfCancellationRequested();

            // 2. Load primary video file
            var normalizedVideo = package.PrimaryVideo.FilePath.Replace('\\', '/');
            await SendCommandAsync("loadfile", normalizedVideo, "replace");

            // Wait for the primary video file to be loaded in mpv demuxer
            var timeout = DateTime.UtcNow.AddSeconds(2.0);
            while (DateTime.UtcNow < timeout && !ct.IsCancellationRequested)
            {
                var duration = await GetPropertyAsync("duration");
                if (!string.IsNullOrEmpty(duration))
                {
                    break;
                }
                await Task.Delay(25, ct);
            }

            ct.ThrowIfCancellationRequested();

            // 3. Attach external audio tracks (only if file actually exists on disk)
            foreach (var audio in package.AudioTracks.Where(a => a.IsExternal && !string.IsNullOrEmpty(a.ExternalFilePath) && File.Exists(a.ExternalFilePath)))
            {
                ct.ThrowIfCancellationRequested();
                var normAudio = audio.ExternalFilePath!.Replace('\\', '/');
                await SendCommandAsync("audio-add", normAudio);
            }

            // 4. Attach external subtitle tracks (only if file actually exists on disk)
            foreach (var sub in package.SubtitleTracks.Where(s => s.IsExternal && !string.IsNullOrEmpty(s.ExternalFilePath) && File.Exists(s.ExternalFilePath)))
            {
                ct.ThrowIfCancellationRequested();
                var normSub = sub.ExternalFilePath!.Replace('\\', '/');
                await SendCommandAsync("sub-add", normSub);
            }

            // 5. Wait for asynchronous track registration to complete
            var hasExternalAudio = package.AudioTracks.Any(a => a.IsExternal && File.Exists(a.ExternalFilePath));
            var hasExternalSub = package.SubtitleTracks.Any(s => s.IsExternal && File.Exists(s.ExternalFilePath));

            var trackWaitTimeout = DateTime.UtcNow.AddSeconds(1.5);
            while (DateTime.UtcNow < trackWaitTimeout && !ct.IsCancellationRequested)
            {
                await RefreshTrackListAsync();
                var audioLoaded = !hasExternalAudio || ActiveTracks.Any(t => t is AudioTrack && t.Origin == TrackOrigin.External);
                var subLoaded = !hasExternalSub || ActiveTracks.Any(t => t is SubtitleTrack && t.Origin == TrackOrigin.External);
                if (audioLoaded && subLoaded)
                {
                    break;
                }
                await Task.Delay(25, ct);
            }
        }
        finally
        {
            _openLock.Release();
        }
    }

    public async Task PlayAsync()
    {
        EnsureInitialized();
        await SetPropertyAsync("pause", "no");
        SetState(PlaybackState.Playing);
    }

    public async Task PauseAsync()
    {
        EnsureInitialized();
        await SetPropertyAsync("pause", "yes");
        SetState(PlaybackState.Paused);
    }

    public async Task StopAsync()
    {
        EnsureInitialized();
        await SendCommandAsync("stop");
        SetState(PlaybackState.Stopped);
    }

    public Task SeekAsync(double seconds, bool relative = true)
    {
        EnsureInitialized();
        var mode = relative ? "relative" : "absolute";
        return SendCommandAsync("seek", seconds.ToString(System.Globalization.CultureInfo.InvariantCulture), mode);
    }

    public Task SetVolumeAsync(int volume)
    {
        EnsureInitialized();
        return SetPropertyAsync("volume", Math.Clamp(volume, 0, 150).ToString());
    }

    public Task SetFullscreenAsync(bool fullscreen)
    {
        EnsureInitialized();
        return SetPropertyAsync("fullscreen", fullscreen ? "yes" : "no");
    }

    public async Task ToggleFullscreenAsync()
    {
        EnsureInitialized();
        var fs = await GetPropertyAsync("fullscreen");
        await SetFullscreenAsync(fs != "yes");
    }

    public Task SelectAudioTrackAsync(int trackId)
    {
        EnsureInitialized();
        return SetPropertyAsync("aid", trackId.ToString());
    }

    public Task SelectSubtitleTrackAsync(int trackId)
    {
        EnsureInitialized();
        return SetPropertyAsync("sid", trackId.ToString());
    }

    public Task SetSubtitleVisibilityAsync(bool visible)
    {
        EnsureInitialized();
        return SetPropertyAsync("sub-visibility", visible ? "yes" : "no");
    }

    public Task SetPropertyAsync(string property, string value)
    {
        EnsureInitialized();
        lock (_lock)
        {
            SetPropertyStringUnsafe(property, value);
        }
        return Task.CompletedTask;
    }

    public Task<string?> GetPropertyAsync(string property)
    {
        EnsureInitialized();
        lock (_lock)
        {
            return Task.FromResult(GetPropertySync(property));
        }
    }

    public Task SendCommandAsync(params string[] args)
    {
        EnsureInitialized();
        lock (_lock)
        {
            SendCommandUnsafe(args);
        }
        return Task.CompletedTask;
    }

    public async Task RefreshTrackListAsync()
    {
        var trackListJson = await GetPropertyAsync("track-list");
        if (string.IsNullOrEmpty(trackListJson)) return;

        try
        {
            using var doc = JsonDocument.Parse(trackListJson);
            var tracks = new List<MediaTrack>();

            foreach (var elem in doc.RootElement.EnumerateArray())
            {
                var type = elem.GetProperty("type").GetString();
                var id = elem.GetProperty("id").GetInt32();
                var title = elem.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                var lang = elem.TryGetProperty("lang", out var l) ? l.GetString() ?? "und" : "und";
                var isExternal = elem.TryGetProperty("external", out var ext) && ext.GetBoolean();
                var codec = elem.TryGetProperty("codec", out var c) ? c.GetString() ?? "" : "";
                var isSelected = elem.TryGetProperty("selected", out var sel) && sel.GetBoolean();
                var src = elem.TryGetProperty("src", out var s) ? s.GetString() : null;

                if (type == "audio")
                {
                    var channels = elem.TryGetProperty("demux-channel-count", out var ch) ? ch.GetInt32() : 2;
                    tracks.Add(new AudioTrack
                    {
                        Id = id,
                        Title = string.IsNullOrEmpty(title) ? $"Audio Track #{id}" : title,
                        Language = lang,
                        Origin = isExternal ? TrackOrigin.External : TrackOrigin.Embedded,
                        ExternalFilePath = isExternal ? src : null,
                        Codec = codec,
                        Channels = channels,
                        IsSelected = isSelected
                    });
                }
                else if (type == "sub")
                {
                    var format = codec.ToLowerInvariant() switch
                    {
                        "ass" => SubtitleFormat.ASS,
                        "ssa" => SubtitleFormat.SSA,
                        "subrip" => SubtitleFormat.SRT,
                        "webvtt" => SubtitleFormat.VTT,
                        "hdmv_pgs_subtitle" => SubtitleFormat.PGS,
                        _ => SubtitleFormat.Unknown
                    };

                    tracks.Add(new SubtitleTrack
                    {
                        Id = id,
                        Title = string.IsNullOrEmpty(title) ? $"Subtitle #{id}" : title,
                        Language = lang,
                        Origin = isExternal ? TrackOrigin.External : TrackOrigin.Embedded,
                        ExternalFilePath = isExternal ? src : null,
                        Codec = codec,
                        Format = format,
                        IsSelected = isSelected
                    });
                }
            }

            ActiveTracks = tracks;
            TracksChanged?.Invoke(tracks);
        }
        catch
        {
            // Ignore track json parsing transient errors
        }
    }

    private void EventLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _handle != 0)
        {
            var eventPtr = LibMpvNative.mpv_wait_event(_handle, 0.05);
            if (eventPtr != 0)
            {
                var eventId = Marshal.ReadInt32(eventPtr);
                if (eventId == 6) // MPV_EVENT_START_FILE
                {
                    SetState(PlaybackState.Loading);
                }
                else if (eventId == 8) // MPV_EVENT_FILE_LOADED
                {
                    var paused = GetPropertySync("pause");
                    SetState(paused == "no" ? PlaybackState.Playing : PlaybackState.Paused);
                }
                else if (eventId == 7) // MPV_EVENT_END_FILE
                {
                    SetState(PlaybackState.Stopped);
                }
                else if (eventId == 22) // MPV_EVENT_PROPERTY_CHANGE
                {
                    var dataPtr = Marshal.ReadIntPtr(eventPtr, 16);
                    if (dataPtr != 0)
                    {
                        var propNamePtr = Marshal.ReadIntPtr(dataPtr, 0);
                        var propName = propNamePtr != 0 ? Marshal.PtrToStringUTF8(propNamePtr) : null;
                        var valPtr = Marshal.ReadIntPtr(dataPtr, 16);

                        if (propName == "pause" && valPtr != 0)
                        {
                            var pauseVal = Marshal.PtrToStringUTF8(valPtr);
                            if (pauseVal == "no")
                            {
                                SetState(PlaybackState.Playing);
                            }
                            else if (pauseVal == "yes" && State != PlaybackState.Stopped && State != PlaybackState.Loading)
                            {
                                SetState(PlaybackState.Paused);
                            }
                        }
                        else if (propName == "eof-reached" && valPtr != 0)
                        {
                            var eofVal = Marshal.PtrToStringUTF8(valPtr);
                            if (eofVal == "yes")
                            {
                                SetState(PlaybackState.Paused);
                            }
                        }
                    }
                }
            }

            // Always synchronize playback time and duration smoothly if media is active
            if (State == PlaybackState.Playing || State == PlaybackState.Paused)
            {
                var posStr = GetPropertySync("playback-time");
                var durStr = GetPropertySync("duration");

                if (double.TryParse(posStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var pos))
                {
                    CurrentPositionSeconds = pos;
                }
                if (double.TryParse(durStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dur))
                {
                    DurationSeconds = dur;
                }

                if (posStr != null || durStr != null)
                {
                    TimeUpdated?.Invoke(CurrentPositionSeconds, DurationSeconds);
                }
            }
        }
    }

    private unsafe string? GetPropertySync(string property)
    {
        if (_handle == 0) return null;
        fixed (byte* pProp = Encoding.UTF8.GetBytes(property + '\0'))
        {
            var ptr = LibMpvNative.mpv_get_property_string(_handle, pProp);
            if (ptr == null) return null;
            try { return Marshal.PtrToStringUTF8((nint)ptr); }
            finally { LibMpvNative.mpv_free(ptr); }
        }
    }

    private unsafe void SetPropertyStringUnsafe(string name, string data)
    {
        fixed (byte* pName = Encoding.UTF8.GetBytes(name + '\0'))
        fixed (byte* pData = Encoding.UTF8.GetBytes(data + '\0'))
        {
            LibMpvNative.mpv_set_property_string(_handle, pName, pData);
        }
    }

    private unsafe void SetOptionStringUnsafe(string name, string data)
    {
        fixed (byte* pName = Encoding.UTF8.GetBytes(name + '\0'))
        fixed (byte* pData = Encoding.UTF8.GetBytes(data + '\0'))
        {
            LibMpvNative.mpv_set_option_string(_handle, pName, pData);
        }
    }

    private unsafe void ObservePropertyUnsafe(ulong replyUserdata, string name)
    {
        fixed (byte* pName = Encoding.UTF8.GetBytes(name + '\0'))
        {
            LibMpvNative.mpv_observe_property(_handle, replyUserdata, pName, LibMpvNative.MPV_FORMAT_STRING);
        }
    }

    private unsafe void SendCommandUnsafe(string[] args)
    {
        var utf8Bytes = new byte*[args.Length + 1];
        var allocs = new List<nint>();

        try
        {
            for (int i = 0; i < args.Length; i++)
            {
                var bytes = Encoding.UTF8.GetBytes(args[i] + '\0');
                var unmanaged = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, unmanaged, bytes.Length);
                allocs.Add(unmanaged);
                utf8Bytes[i] = (byte*)unmanaged;
            }
            utf8Bytes[args.Length] = null;

            fixed (byte** pArgs = utf8Bytes)
            {
                var err = LibMpvNative.mpv_command(_handle, pArgs);
                if (err < 0)
                {
                    var msg = GetErrorString(err);
                    Console.Error.WriteLine($"[mpv error] Command '{string.Join(" ", args)}' failed: {msg} (code {err})");
                }
            }
        }
        finally
        {
            foreach (var ptr in allocs)
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }

    private static unsafe string GetErrorString(int error)
    {
        var ptr = LibMpvNative.mpv_error_string(error);
        return ptr != null ? Marshal.PtrToStringUTF8((nint)ptr) ?? "Unknown" : "Unknown";
    }

    private void EnsureInitialized()
    {
        if (_handle == 0)
        {
            throw new InvalidOperationException("MpvPlaybackEngine is not initialized.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        if (_eventLoopTask != null)
        {
            try { await _eventLoopTask; } catch { }
        }

        lock (_lock)
        {
            if (_handle != 0)
            {
                LibMpvNative.mpv_terminate_destroy(_handle);
                _handle = 0;
            }
        }

        _cts?.Dispose();
        _openLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
