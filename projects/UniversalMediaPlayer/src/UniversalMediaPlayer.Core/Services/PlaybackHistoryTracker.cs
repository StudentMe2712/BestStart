namespace UniversalMediaPlayer.Core.Services;

using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalMediaPlayer.Core.Models;
using UniversalMediaPlayer.Core.Persistence;

public sealed class PlaybackHistoryTracker : IDisposable, IAsyncDisposable
{
    private readonly IWatchHistoryStore _historyStore;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private PlaybackSession? _currentSession;
    private Guid _currentSessionId = Guid.Empty;
    private int _saveCount;
    private bool _disposed;

    public PlaybackHistoryTracker(IWatchHistoryStore historyStore)
    {
        _historyStore = historyStore ?? throw new ArgumentNullException(nameof(historyStore));
    }

    public Guid CurrentSessionId => _currentSessionId;
    public string? CurrentFilePath => _currentSession?.FilePath;
    public string? CurrentShowId => _currentSession?.ShowId;
    public double CurrentPosition => _currentSession?.CurrentPosition ?? 0;
    public double Duration => _currentSession?.Duration ?? 0;
    public double? LastSavedPosition => _currentSession?.LastSavedPosition;
    public bool IsCompleted => _currentSession?.Completed ?? false;
    public bool IsActive => _currentSession?.IsActive ?? false;
    public int SaveCount => _saveCount;

    public async Task TrackMediaAsync(
        string filePath,
        string showId,
        int? seasonNumber = null,
        int? episodeNumber = null,
        double duration = 0,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(showId);
        ThrowIfDisposed();

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // 1. End previous session and flush pending unsaved state
            if (_currentSession != null && _currentSession.IsActive)
            {
                _currentSession.IsActive = false;
                if (_currentSession.HasUnsavedChanges)
                {
                    await SaveSessionSafeAsync(_currentSession, ct).ConfigureAwait(false);
                }
            }

            // 2. Generate new session correlation ID
            var newSessionId = Guid.NewGuid();
            _currentSessionId = newSessionId;

            // 3. Inspect existing history for this file
            var existing = await _historyStore.GetByFilePathAsync(filePath, ct).ConfigureAwait(false);

            double initialPosition = 0;
            double effectiveDuration = duration;
            bool isCompleted = false;

            if (existing != null)
            {
                if (effectiveDuration <= 0 && existing.DurationSeconds > 0)
                {
                    effectiveDuration = existing.DurationSeconds;
                }

                if (existing.Completed)
                {
                    // Completed items reset position to 0 upon start/reopen according to Phase 9 requirements.
                    initialPosition = 0;
                    isCompleted = false;

                    var resetItem = existing with
                    {
                        PositionSeconds = 0,
                        Completed = false,
                        LastPlayedUtc = DateTime.UtcNow
                    };
                    await _historyStore.RecordPositionAsync(resetItem, ct).ConfigureAwait(false);
                }
                else
                {
                    initialPosition = existing.PositionSeconds;
                    isCompleted = false;
                }
            }

            var session = new PlaybackSession(
                newSessionId,
                filePath,
                showId,
                seasonNumber,
                episodeNumber,
                initialPosition,
                effectiveDuration,
                isCompleted);

            if (existing != null && existing.Completed)
            {
                session.LastSavedPosition = 0;
            }
            else if (existing != null)
            {
                session.LastSavedPosition = existing.PositionSeconds;
            }
            else
            {
                session.LastSavedPosition = null;
            }

            _currentSession = session;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task TrackMediaAsync(MediaPackage package, double duration = 0, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        var showId = ShowIdentityResolver.ResolveShowId(package);
        var filePath = package.PrimaryVideo.FilePath;
        var seasonNumber = package.Episode?.SeasonNumber;
        var episodeNumber = package.Episode?.EpisodeNumber;

        return TrackMediaAsync(filePath, showId, seasonNumber, episodeNumber, duration, ct);
    }

    public void TrackMedia(
        string filePath,
        string showId,
        int? seasonNumber = null,
        int? episodeNumber = null,
        double duration = 0)
    {
        TrackMediaAsync(filePath, showId, seasonNumber, episodeNumber, duration).GetAwaiter().GetResult();
    }

    public void TrackMedia(MediaPackage package, double duration = 0)
    {
        TrackMediaAsync(package, duration).GetAwaiter().GetResult();
    }

    public Task OnMediaOpenedAsync(MediaPackage package, double duration = 0, CancellationToken ct = default) =>
        TrackMediaAsync(package, duration, ct);

    public Task OnMediaOpenedAsync(
        string filePath,
        string showId,
        int? seasonNumber = null,
        int? episodeNumber = null,
        double duration = 0,
        CancellationToken ct = default) =>
        TrackMediaAsync(filePath, showId, seasonNumber, episodeNumber, duration, ct);

    public void OnMediaOpened(MediaPackage package, double duration = 0) =>
        TrackMedia(package, duration);

    public void OnMediaOpened(
        string filePath,
        string showId,
        int? seasonNumber = null,
        int? episodeNumber = null,
        double duration = 0) =>
        TrackMedia(filePath, showId, seasonNumber, episodeNumber, duration);

    public async Task UpdatePositionAsync(
        double position,
        double? duration = null,
        string? filePath = null,
        Guid? sessionId = null,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        PlaybackSession? sessionToSave = null;
        try
        {
            var session = _currentSession;
            if (session == null || !session.IsActive)
            {
                return;
            }

            // Race protection: discard updates for obsolete sessions or other files
            if (sessionId.HasValue && sessionId.Value != session.SessionId)
            {
                return;
            }

            if (!string.IsNullOrEmpty(filePath) && !string.Equals(filePath, session.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            session.CurrentPosition = Math.Max(0, position);
            if (duration.HasValue && duration.Value > 0)
            {
                session.Duration = duration.Value;
            }

            // Threshold: Automatically sets Completed = true when duration > 0 && (position / duration) >= 0.90
            bool becameCompleted = false;
            if (!session.Completed && session.Duration > 0 && (session.CurrentPosition / session.Duration) >= 0.90)
            {
                session.Completed = true;
                becameCompleted = true;
            }

            // Throttling rule:
            // Save position when Math.Abs(position - lastSavedPosition) >= 5.0 seconds during playback,
            // or when first observed (LastSavedPosition == null),
            // or when crossing the 90% completion mark.
            bool shouldSave = becameCompleted ||
                              !session.LastSavedPosition.HasValue ||
                              Math.Abs(session.CurrentPosition - session.LastSavedPosition.Value) >= 5.0;

            if (shouldSave)
            {
                sessionToSave = session;
            }
        }
        finally
        {
            _lock.Release();
        }

        if (sessionToSave != null)
        {
            await SaveSessionSafeAsync(sessionToSave, ct).ConfigureAwait(false);
        }
    }

    public void OnPositionChanged(
        double position,
        double? duration = null,
        string? filePath = null,
        Guid? sessionId = null)
    {
        UpdatePositionAsync(position, duration, filePath, sessionId).GetAwaiter().GetResult();
    }

    public void UpdatePosition(
        double position,
        double? duration = null,
        string? filePath = null,
        Guid? sessionId = null)
    {
        UpdatePositionAsync(position, duration, filePath, sessionId).GetAwaiter().GetResult();
    }

    public void OnPositionUpdate(
        double position,
        double? duration = null,
        string? filePath = null,
        Guid? sessionId = null)
    {
        UpdatePositionAsync(position, duration, filePath, sessionId).GetAwaiter().GetResult();
    }

    public async Task OnPauseAsync(
        string? filePath = null,
        Guid? sessionId = null,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        PlaybackSession? sessionToSave = null;
        try
        {
            var session = _currentSession;
            if (session == null || !session.IsActive)
            {
                return;
            }

            if (sessionId.HasValue && sessionId.Value != session.SessionId)
            {
                return;
            }

            if (!string.IsNullOrEmpty(filePath) && !string.Equals(filePath, session.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            sessionToSave = session;
        }
        finally
        {
            _lock.Release();
        }

        if (sessionToSave != null)
        {
            await SaveSessionSafeAsync(sessionToSave, ct).ConfigureAwait(false);
        }
    }

    public void OnPause(string? filePath = null, Guid? sessionId = null)
    {
        OnPauseAsync(filePath, sessionId).GetAwaiter().GetResult();
    }

    public async Task OnSeekAsync(
        double? newPosition = null,
        string? filePath = null,
        Guid? sessionId = null,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        PlaybackSession? sessionToSave = null;
        try
        {
            var session = _currentSession;
            if (session == null || !session.IsActive)
            {
                return;
            }

            if (sessionId.HasValue && sessionId.Value != session.SessionId)
            {
                return;
            }

            if (!string.IsNullOrEmpty(filePath) && !string.Equals(filePath, session.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (newPosition.HasValue)
            {
                session.CurrentPosition = Math.Max(0, newPosition.Value);
            }

            if (!session.Completed && session.Duration > 0 && (session.CurrentPosition / session.Duration) >= 0.90)
            {
                session.Completed = true;
            }

            sessionToSave = session;
        }
        finally
        {
            _lock.Release();
        }

        if (sessionToSave != null)
        {
            await SaveSessionSafeAsync(sessionToSave, ct).ConfigureAwait(false);
        }
    }

    public void OnSeek(double? newPosition = null, string? filePath = null, Guid? sessionId = null)
    {
        OnSeekAsync(newPosition, filePath, sessionId).GetAwaiter().GetResult();
    }

    public async Task OnStopAsync(
        string? filePath = null,
        Guid? sessionId = null,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        PlaybackSession? sessionToSave = null;
        try
        {
            var session = _currentSession;
            if (session == null || !session.IsActive)
            {
                return;
            }

            if (sessionId.HasValue && sessionId.Value != session.SessionId)
            {
                return;
            }

            if (!string.IsNullOrEmpty(filePath) && !string.Equals(filePath, session.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            sessionToSave = session;
        }
        finally
        {
            _lock.Release();
        }

        if (sessionToSave != null)
        {
            await SaveSessionSafeAsync(sessionToSave, ct).ConfigureAwait(false);
        }
    }

    public void OnStop(string? filePath = null, Guid? sessionId = null)
    {
        OnStopAsync(filePath, sessionId).GetAwaiter().GetResult();
    }

    public async Task OnMediaChangingAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        PlaybackSession? sessionToSave = null;
        try
        {
            if (_currentSession != null && _currentSession.IsActive)
            {
                _currentSession.IsActive = false;
                sessionToSave = _currentSession;
                _currentSession = null;
            }
        }
        finally
        {
            _lock.Release();
        }

        if (sessionToSave != null)
        {
            await SaveSessionSafeAsync(sessionToSave, ct).ConfigureAwait(false);
        }
    }

    public void OnMediaChanging()
    {
        OnMediaChangingAsync().GetAwaiter().GetResult();
    }

    private async Task SaveSessionSafeAsync(PlaybackSession session, CancellationToken ct)
    {
        try
        {
            var item = new WatchHistoryItem
            {
                ShowId = session.ShowId,
                SeasonNumber = session.SeasonNumber,
                EpisodeNumber = session.EpisodeNumber,
                FilePath = session.FilePath,
                PositionSeconds = session.CurrentPosition,
                DurationSeconds = session.Duration,
                Completed = session.Completed,
                LastPlayedUtc = DateTime.UtcNow
            };

            await _historyStore.RecordPositionAsync(item, ct).ConfigureAwait(false);

            session.LastSavedPosition = session.CurrentPosition;
            Interlocked.Increment(ref _saveCount);
        }
        catch (Exception)
        {
            // Crash-safe: history recording failures must never crash the player
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _lock.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            try
            {
                await OnMediaChangingAsync().ConfigureAwait(false);
            }
            catch
            {
                // Best effort on disposal
            }
            _lock.Dispose();
        }
    }

    private sealed class PlaybackSession
    {
        public Guid SessionId { get; }
        public string FilePath { get; }
        public string ShowId { get; }
        public int? SeasonNumber { get; }
        public int? EpisodeNumber { get; }
        public double CurrentPosition { get; set; }
        public double Duration { get; set; }
        public double? LastSavedPosition { get; set; }
        public bool Completed { get; set; }
        public bool IsActive { get; set; }

        public bool HasUnsavedChanges =>
            !LastSavedPosition.HasValue || Math.Abs(CurrentPosition - LastSavedPosition.Value) > 0.001;

        public PlaybackSession(
            Guid sessionId,
            string filePath,
            string showId,
            int? seasonNumber,
            int? episodeNumber,
            double initialPosition,
            double duration,
            bool completed)
        {
            SessionId = sessionId;
            FilePath = filePath;
            ShowId = showId;
            SeasonNumber = seasonNumber;
            EpisodeNumber = episodeNumber;
            CurrentPosition = initialPosition;
            Duration = duration;
            Completed = completed;
            IsActive = true;
        }
    }
}
