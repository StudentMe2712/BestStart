# ADR 0006: Multi-Tier Local Persistence Architecture

- **Status:** Accepted
- **Date:** 2026-09-05
- **Deciders:** UniversalMediaPlayer Architecture Team

---

## 1. Context

The player must persist state across application restarts:
1. Global configuration (audio output device, default volume, hardware decode preferences, UI theme, keyboard bindings).
2. Per-show and per-series preferences (preferred audio language, preferred subtitle language, subtitle visibility).
3. Playback resume history (last file played, exact seek position in seconds, completion status).

All persistence must be **100% local-first**, non-blocking, resistant to sudden power loss or process termination, and adhere strictly to user privacy (no cloud sync, no telemetry).

---

## 2. Decision

We implement a **Two-Tier Storage Architecture**:

### Tier 1: Lightweight JSON Configuration
- **Target Data:** Application settings (`settings.json`) and per-show track preferences (`show_preferences.json`).
- **Location:** `%LOCALAPPDATA%/UniversalMediaPlayer/config/`.
- **Implementation:**
  - Strongly typed C# records serialized using `System.Text.Json`.
  - Atomic write strategy: Write to temporary file (`settings.json.tmp`) followed by atomic filesystem replacement (`File.Move` with overwrite) to guarantee zero file corruption on crash.
  - Case-insensitive dictionary keys for show titles.

### Tier 2: SQLite Local Database
- **Target Data:** Playback history, episode resume timestamps, duration cache, and media metadata hashes.
- **Location:** `%LOCALAPPDATA%/UniversalMediaPlayer/data/history.db`.
- **Implementation:**
  - Embedded `Microsoft.Data.Sqlite` in WAL (Write-Ahead Logging) mode.
  - Schema:
    - `PlaybackHistory` (FileHash, FilePath, Title, Season, Episode, LastPositionSeconds, DurationSeconds, LastWatchedUtc, CompletedFlag).
  - High performance: Fast indexed lookups (< 1 ms); non-blocking background writes on playback pause or stop.

---

## 3. Consequences

### Positive:
- Clean human-readable JSON for user settings that power users can inspect or back up.
- Fast, ACID-compliant SQLite storage for watch history that scales seamlessly to tens of thousands of media files.
- Completely isolated from network dependencies.

### Negative:
- Adds SQLite native interop dependency (`e_sqlite3.dll`), which is standard and lightweight in .NET.
