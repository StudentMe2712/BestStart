using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using PersonalProvocateur.App.WindowsInterop;

namespace PersonalProvocateur.App.Services;

/// <summary>
/// Abstraction representing an individual audio session (for unit testability and COM isolation).
/// </summary>
public interface IAudioSessionItem : IDisposable
{
    /// <summary>
    /// Gets the process ID associated with this audio session (0 for system sounds).
    /// </summary>
    uint ProcessId { get; }

    /// <summary>
    /// Gets the current master volume level (0.0 to 1.0).
    /// </summary>
    float GetMasterVolume();

    /// <summary>
    /// Sets the master volume level (0.0 to 1.0).
    /// </summary>
    void SetMasterVolume(float level);

    /// <summary>
    /// Gets the current mute status.
    /// </summary>
    bool GetMute();

    /// <summary>
    /// Sets the mute status.
    /// </summary>
    void SetMute(bool isMuted);
}

/// <summary>
/// Production implementation of <see cref="IAudioSessionItem"/> wrapping native Windows WASAPI COM interfaces.
/// </summary>
public sealed class ComAudioSessionItem : IAudioSessionItem
{
    private readonly IAudioSessionControl _control;
    private readonly IAudioSessionControl2? _control2;
    private readonly ISimpleAudioVolume? _volume;
    private bool _disposed;

    public uint ProcessId { get; }

    public ComAudioSessionItem(IAudioSessionControl control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
        _control2 = control as IAudioSessionControl2;
        _volume = control as ISimpleAudioVolume;

        uint pid = 0;
        if (_control2 != null)
        {
            try
            {
                _control2.GetProcessId(out pid);
            }
            catch
            {
                pid = 0;
            }
        }
        ProcessId = pid;
    }

    public float GetMasterVolume()
    {
        if (_volume == null) return 1.0f;
        try
        {
            _volume.GetMasterVolume(out float level);
            return level;
        }
        catch
        {
            return 1.0f;
        }
    }

    public void SetMasterVolume(float level)
    {
        if (_volume == null) return;
        try
        {
            Guid empty = Guid.Empty;
            _volume.SetMasterVolume(Math.Clamp(level, 0.0f, 1.0f), ref empty);
        }
        catch
        {
        }
    }

    public bool GetMute()
    {
        if (_volume == null) return false;
        try
        {
            _volume.GetMute(out bool isMuted);
            return isMuted;
        }
        catch
        {
            return false;
        }
    }

    public void SetMute(bool isMuted)
    {
        if (_volume == null) return;
        try
        {
            Guid empty = Guid.Empty;
            _volume.SetMute(isMuted, ref empty);
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            Marshal.ReleaseComObject(_control);
        }
        catch
        {
        }
    }
}

/// <summary>
/// Service managing system-wide background audio ducking, muting, or media pausing during video playback.
/// Uses zero external dependencies via native Windows Core Audio WASAPI COM interfaces.
/// </summary>
public class AudioDuckingService : IAudioDuckingService
{
    private readonly Func<IEnumerable<IAudioSessionItem>> _sessionProvider;
    private readonly Action _mediaPauseSender;
    private readonly uint _currentProcessId;
    private readonly object _lock = new();

    private readonly List<SavedSessionState> _savedSessions = new();
    private bool _isDucked;
    private bool _mediaWasPaused;
    private bool _disposed;

    private sealed class SavedSessionState
    {
        public IAudioSessionItem Session { get; }
        public float OriginalVolume { get; }
        public bool OriginalMute { get; }
        public bool ModifiedVolume { get; set; }
        public bool ModifiedMute { get; set; }

        public SavedSessionState(IAudioSessionItem session, float originalVolume, bool originalMute)
        {
            Session = session;
            OriginalVolume = originalVolume;
            OriginalMute = originalMute;
        }
    }

    /// <summary>
    /// Gets whether background audio sessions are currently ducked or muted.
    /// </summary>
    public bool IsDucked
    {
        get
        {
            lock (_lock)
            {
                return _isDucked;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of <see cref="AudioDuckingService"/>.
    /// </summary>
    /// <param name="sessionProvider">Optional custom audio session provider for testing.</param>
    /// <param name="mediaPauseSender">Optional delegate for triggering media play/pause key.</param>
    /// <param name="currentProcessId">Optional current process ID override for testing.</param>
    public AudioDuckingService(
        Func<IEnumerable<IAudioSessionItem>>? sessionProvider = null,
        Action? mediaPauseSender = null,
        uint? currentProcessId = null)
    {
        _sessionProvider = sessionProvider ?? EnumerateRealAudioSessions;
        _mediaPauseSender = mediaPauseSender ?? SendDefaultMediaPlayPauseKey;
        _currentProcessId = currentProcessId ?? (uint)Process.GetCurrentProcess().Id;
    }

    /// <summary>
    /// Attenuates or mutes other running applications' audio sessions.
    /// </summary>
    /// <param name="mode">Ducking mode: "Mute", "Duck", "PauseMedia", or "Disabled".</param>
    /// <param name="duckLevel">Target volume level (0.0 to 1.0) when mode is "Duck".</param>
    public void DuckOtherApplications(string mode, float duckLevel = 0.15f)
    {
        if (string.Equals(mode, "Disabled", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        lock (_lock)
        {
            if (_isDucked || _disposed)
            {
                return;
            }

            _isDucked = true;
            bool isDuckMode = string.Equals(mode, "Duck", StringComparison.OrdinalIgnoreCase);
            bool isPauseMediaMode = string.Equals(mode, "PauseMedia", StringComparison.OrdinalIgnoreCase);

            if (isPauseMediaMode)
            {
                try
                {
                    _mediaPauseSender();
                    _mediaWasPaused = true;
                }
                catch
                {
                }
            }

            try
            {
                var sessions = _sessionProvider();
                foreach (var session in sessions)
                {
                    // Skip our own application's audio session so video playback is not silenced!
                    if (session.ProcessId == _currentProcessId && _currentProcessId != 0)
                    {
                        continue;
                    }

                    float origVol = session.GetMasterVolume();
                    bool origMute = session.GetMute();

                    var state = new SavedSessionState(session, origVol, origMute);

                    if (isDuckMode)
                    {
                        session.SetMasterVolume(duckLevel);
                        state.ModifiedVolume = true;
                    }
                    else
                    {
                        // "Mute" or "PauseMedia": mute the audio session
                        if (!origMute)
                        {
                            session.SetMute(true);
                            state.ModifiedMute = true;
                        }
                    }

                    _savedSessions.Add(state);
                }
            }
            catch
            {
                // Ensure COM errors do not affect video window playback
            }
        }
    }

    /// <summary>
    /// Restores previously modified audio sessions to their original volume and mute state.
    /// </summary>
    public void RestoreOtherApplications()
    {
        lock (_lock)
        {
            if (!_isDucked)
            {
                return;
            }

            foreach (var state in _savedSessions)
            {
                try
                {
                    if (state.ModifiedMute)
                    {
                        state.Session.SetMute(state.OriginalMute);
                    }

                    if (state.ModifiedVolume)
                    {
                        state.Session.SetMasterVolume(state.OriginalVolume);
                    }

                    state.Session.Dispose();
                }
                catch
                {
                }
            }

            _savedSessions.Clear();

            if (_mediaWasPaused)
            {
                try
                {
                    _mediaPauseSender();
                }
                catch
                {
                }
                _mediaWasPaused = false;
            }

            _isDucked = false;
        }
    }

    private static List<IAudioSessionItem> EnumerateRealAudioSessions()
    {
        var list = new List<IAudioSessionItem>();
        try
        {
            var enumeratorType = Type.GetTypeFromCLSID(CoreAudioGuids.MMDeviceEnumeratorClsid);
            if (enumeratorType == null)
            {
                return list;
            }

            var enumerator = Activator.CreateInstance(enumeratorType) as IMMDeviceEnumerator;
            if (enumerator == null)
            {
                return list;
            }

            int hr = enumerator.GetDefaultAudioEndpoint((int)EDataFlow.eRender, (int)ERole.eMultimedia, out var device);
            if (hr != 0 || device == null)
            {
                return list;
            }

            Guid iid = CoreAudioGuids.IAudioSessionManager2Iid;
            hr = device.Activate(ref iid, CoreAudioGuids.CLSCTX_ALL, IntPtr.Zero, out var managerObj);
            if (hr != 0 || managerObj is not IAudioSessionManager2 manager)
            {
                return list;
            }

            hr = manager.GetSessionEnumerator(out var sessionEnum);
            if (hr != 0 || sessionEnum == null)
            {
                return list;
            }

            sessionEnum.GetCount(out int count);
            for (int i = 0; i < count; i++)
            {
                if (sessionEnum.GetSession(i, out var session) == 0 && session != null)
                {
                    list.Add(new ComAudioSessionItem(session));
                }
            }
        }
        catch
        {
            // Suppress audio device enumeration exceptions on systems without sound hardware
        }

        return list;
    }

    private static void SendDefaultMediaPlayPauseKey()
    {
        NativeMethods.keybd_event(NativeMethods.VK_MEDIA_PLAY_PAUSE, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VK_MEDIA_PLAY_PAUSE, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            RestoreOtherApplications();
        }
    }
}
