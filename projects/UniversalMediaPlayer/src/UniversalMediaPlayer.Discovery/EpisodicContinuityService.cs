namespace UniversalMediaPlayer.Discovery;

using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalMediaPlayer.Core.Models;
using UniversalMediaPlayer.Core.Persistence;

public sealed class EpisodicContinuityService
{
    private readonly IShowPreferencesStore _preferencesStore;
    private readonly IWatchHistoryStore _historyStore;

    public EpisodicContinuityService(
        IShowPreferencesStore preferencesStore,
        IWatchHistoryStore historyStore)
    {
        _preferencesStore = preferencesStore ?? throw new ArgumentNullException(nameof(preferencesStore));
        _historyStore = historyStore ?? throw new ArgumentNullException(nameof(historyStore));
    }

    public async Task<PlaybackPreparationPlan> PreparePlaybackAsync(
        MediaPackage package,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(package);

        // 1. Resolves showId via ShowIdentityResolver.ResolveShowId(package)
        var showId = ShowIdentityResolver.ResolveShowId(package);

        // 2. Loads ShowPreferences from store
        var preferences = await _preferencesStore.GetPreferencesAsync(showId, ct).ConfigureAwait(false);

        // 3. Resolves audio track using PreferredTrackResolver.ResolveAudioTrack
        var audioResolution = PreferredTrackResolver.ResolveAudioTrack(preferences, package.AudioTracks);

        // 4. Resolves subtitle track using PreferredTrackResolver.ResolveSubtitleTrack
        var subtitleResolution = PreferredTrackResolver.ResolveSubtitleTrack(preferences, package.SubtitleTracks);

        // 5. Determines subtitle visibility: if preferences specify SubtitleEnabled == false or resolved subtitle is null, visibility is false; if subtitle is selected, visibility is true.
        bool subtitleVisible = (preferences?.SubtitleEnabled != false) && (subtitleResolution?.SelectedTrack != null);

        // 6. Checks history from IWatchHistoryStore.GetByFilePathAsync(package.PrimaryVideo.FilePath, ct)
        WatchHistoryItem? resumeHistory = null;
        if (package.PrimaryVideo != null && !string.IsNullOrWhiteSpace(package.PrimaryVideo.FilePath))
        {
            resumeHistory = await _historyStore.GetByFilePathAsync(package.PrimaryVideo.FilePath, ct).ConfigureAwait(false);
        }

        // 7. Returns the complete PlaybackPreparationPlan
        return new PlaybackPreparationPlan
        {
            ShowId = showId,
            Preferences = preferences,
            AudioResolution = audioResolution,
            SubtitleResolution = subtitleResolution,
            SubtitleVisible = subtitleVisible,
            ResumeHistory = resumeHistory
        };
    }

    public Task SaveAudioPreferenceAsync(
        MediaPackage package,
        AudioTrack audioTrack,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(audioTrack);

        var showId = ShowIdentityResolver.ResolveShowId(package);
        return SaveAudioPreferenceAsync(showId, audioTrack, ct);
    }

    public async Task SaveAudioPreferenceAsync(
        string showId,
        AudioTrack audioTrack,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(showId);
        ArgumentNullException.ThrowIfNull(audioTrack);

        var existing = await _preferencesStore.GetPreferencesAsync(showId, ct).ConfigureAwait(false);
        var basePref = existing ?? new ShowPreferences { ShowId = showId };

        var trackPref = new TrackPreference
        {
            Language = audioTrack.Language,
            Title = !string.IsNullOrWhiteSpace(audioTrack.Title) ? audioTrack.Title : null,
            Codec = !string.IsNullOrWhiteSpace(audioTrack.Codec) ? audioTrack.Codec : null,
            Channels = audioTrack.Channels,
            Origin = audioTrack.Origin
        };

        var updated = basePref with
        {
            PreferredAudioLanguage = audioTrack.Language,
            PreferredAudioTrack = trackPref
        };

        await _preferencesStore.SavePreferencesAsync(updated, ct).ConfigureAwait(false);
    }

    public Task SaveSubtitlePreferenceAsync(
        MediaPackage package,
        SubtitleTrack? subtitleTrack,
        bool subtitleEnabled,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        var showId = ShowIdentityResolver.ResolveShowId(package);
        return SaveSubtitlePreferenceAsync(showId, subtitleTrack, subtitleEnabled, ct);
    }

    public async Task SaveSubtitlePreferenceAsync(
        string showId,
        SubtitleTrack? subtitleTrack,
        bool subtitleEnabled,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(showId);

        var existing = await _preferencesStore.GetPreferencesAsync(showId, ct).ConfigureAwait(false);
        var basePref = existing ?? new ShowPreferences { ShowId = showId };

        ShowPreferences updated;
        if (!subtitleEnabled || subtitleTrack == null)
        {
            updated = basePref with
            {
                SubtitleEnabled = false
            };
        }
        else
        {
            var trackPref = new TrackPreference
            {
                Language = subtitleTrack.Language,
                Title = !string.IsNullOrWhiteSpace(subtitleTrack.Title) ? subtitleTrack.Title : null,
                Format = subtitleTrack.Format,
                Origin = subtitleTrack.Origin
            };

            updated = basePref with
            {
                SubtitleEnabled = true,
                PreferredSubtitleLanguage = subtitleTrack.Language,
                PreferredSubtitleTrack = trackPref
            };
        }

        await _preferencesStore.SavePreferencesAsync(updated, ct).ConfigureAwait(false);
    }

    public async Task SetAutoNextEpisodePreferenceAsync(
        string showId,
        bool autoNext,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(showId);

        var existing = await _preferencesStore.GetPreferencesAsync(showId, ct).ConfigureAwait(false);
        var basePref = existing ?? new ShowPreferences { ShowId = showId };

        var updated = basePref with
        {
            AutoNextEpisode = autoNext
        };

        await _preferencesStore.SavePreferencesAsync(updated, ct).ConfigureAwait(false);
    }

    public Task SetAutoNextEpisodePreferenceAsync(
        MediaPackage package,
        bool autoNext,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        var showId = ShowIdentityResolver.ResolveShowId(package);
        return SetAutoNextEpisodePreferenceAsync(showId, autoNext, ct);
    }
}
