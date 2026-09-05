# ADR 0009: Show Preferences, Episodic Continuity & Watch History Architecture

- **Status:** Accepted
- **Date:** 2026-09-05
- **Deciders:** UniversalMediaPlayer Architecture Team
- **Milestone:** Phase 9 (MVP-5)

---

## 1. Context

Desktop media players traditionally fail at episodic media awareness:
1. When watching a multi-episode anime or TV series with external audio (e.g. Russian dub) or external subtitles, users are forced to repeatedly open track menus and manually select their preferred tracks on every single episode.
2. When users pause or close the player, playback positions are either lost or tied directly to absolute file paths, breaking continuity if files are moved between drives or folders.
3. Media players often either lack episodic transition prompts or present intrusive popups that interrupt the viewing flow.

Universal Media Player requires a robust, local-first episodic continuity system that remembers the user's audio and subtitle choices per show, predicts track matching across consecutive episodes, persists resume positions without blocking the playback engine, and provides a fluid Light Alloy-style episodic experience.

---

## 2. Decision

We implement a decoupled, multi-tier architecture for episodic continuity, track preference resolution, and watch history:

### 2.1 Core Domain Models (UniversalMediaPlayer.Core)
- **ShowPreferences**: Independent Core record holding ShowId, PreferredAudioLanguage, PreferredSubtitleLanguage, PreferredAudioTrack, PreferredSubtitleTrack, AutoNextEpisode, and SubtitleEnabled.
- **TrackPreference**: Structural descriptor capturing language, title/release group stem, codec, channels, origin (embedded vs external), and subtitle format.
- **WatchHistoryItem**: Playback history record capturing ShowId, season/episode numbers, normalized file path, position, duration, UTC timestamp, and completion status.
- **ShowIdentityResolver**: Deterministic, drive-independent resolver converting file paths and EpisodeInfo into canonical show identities (e.g. D:\Anime\Attack\ and E:\Media\Attack\ both resolve to "attack on titan").

### 2.2 Persistence Engine (UniversalMediaPlayer.Persistence)
Following ADR 0006:
- **JsonShowPreferencesStore (IShowPreferencesStore)**:
  - Persists per-show preferences in %LOCALAPPDATA%/UniversalMediaPlayer/config/show_preferences.json.
  - Versioned schema ("version": 1).
  - Thread-safe, atomic write strategy: writes to temporary .tmp file, flushes to disk, and executes atomic File.Move(temp, target, overwrite: true) with shared-read and retry handling for Windows file locks.
- **SqliteWatchHistoryStore (IWatchHistoryStore)**:
  - Persists playback progress and completion states in %LOCALAPPDATA%/UniversalMediaPlayer/data/history.db.
  - Configured with PRAGMA journal_mode=WAL;, PRAGMA busy_timeout=5000;, and PRAGMA foreign_keys=ON;.
  - Short-lived, pooled SqliteConnection instances per query/command.
  - Automatically calculates episode completion when position / duration >= 0.90.
  - Provides indexed queries for GetContinueWatchingAsync (recent items with > 15s watched and > 15s remaining).

### 2.3 Continuity & Preference Resolution (UniversalMediaPlayer.Discovery)
- **PreferredTrackResolver**:
  - Deterministic multi-tier track matching algorithm:
    1. **Exact Track Match**: Evaluates release group, title, codec, channels, origin, and language. Matches with MatchConfidence.High.
    2. **Preferred Language Match**: Matches canonical language code (ISO 639-1) with intelligent tie-breaking (preferring external audio, ASS subtitle format, higher channel counts).
    3. **Audio Fallback**: Falls back to backend default or first available track with explicit explanation.
    4. **Subtitle State Resolution**: Respects SubtitleEnabled = false (explicitly disabled) as a first-class user preference; reports clear fallback explanation if preferred subtitle language is absent.
- **EpisodeNavigator**:
  - Discovers and correlates sibling video files using EpisodeParser.
  - Filters by show identity, sorts by season/episode numbers or natural file order, and provides boundary-safe FindNextEpisode and FindPreviousEpisode.
- **EpisodicContinuityService**:
  - Orchestrates PreparePlaybackAsync returning a complete PlaybackPreparationPlan prior to user-visible playback.
  - Exposes SaveAudioPreferenceAsync and SaveSubtitlePreferenceAsync immediately upon user track selection.

### 2.4 Playback History Tracking & Race Protection (UniversalMediaPlayer.Core.Services)
- **PlaybackHistoryTracker**:
  - Non-blocking, throttled position persistence (writes at most every 5 seconds during linear playback).
  - Forces immediate persistence on pause, seek, stop, or file transition.
  - Enforces session correlation IDs: when transitioning from File A to File B, File A's session is terminated and late events from A are discarded, preventing race conditions or cross-file state pollution.

### 2.5 UI & Interaction (UniversalMediaPlayer.UI & UniversalMediaPlayer.App)
- **Resume Prompt**: Non-intrusive floating card asking "Continue watching from XX:XX?" with [ Resume ] and [ Start from beginning ].
- **Auto-Next Episode**: 5-second countdown banner ("Next Episode: ... Playing in 5s... [Play Now] [Cancel]") triggered upon reaching 95% of duration when enabled.
- **Continue Watching Card**: Displayed in the empty state when no media is playing, allowing one-click resumption.
- **Track Selector Indicators**: Visual (Preferred) badge next to preferred audio and subtitle streams.
- **Keyboard Shortcuts**: PageDown / PageUp mapped to Next / Previous Episode without conflicting with existing transport hotkeys.

---

## 3. Consequences

### Positive
- **Effortless Episodic Continuity:** Users select audio and subtitles once; all subsequent episodes automatically apply matching tracks.
- **Data Integrity & Crash Safety:** Atomic JSON writes and SQLite WAL transactions prevent corrupted configs even on hard process termination.
- **Decoupled Architecture:** Core domain models have zero dependencies on WinUI 3 or SQLite; ViewModels remain headless and 100% testable.
- **Zero Playback Interruption:** All SQLite transactions occur on background worker threads with connection pooling and busy timeouts.

### Negative
- Requires maintaining two storage files (show_preferences.json and history.db), but this cleanly separates configuration from high-frequency transactional data as intended by ADR 0006.
