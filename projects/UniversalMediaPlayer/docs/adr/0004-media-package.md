# ADR 0004: The MediaPackage Domain Model

- **Status:** Accepted
- **Date:** 2026-09-05
- **Deciders:** UniversalMediaPlayer Architecture Team

---

## 1. Context

Standard media players operate on a primitive **"Single File -> Play"** mental model. When a user opens `episode01.mkv`, the player merely opens that file and inspects its internal multiplexed streams.

However, in real-world media consumption (anime releases, scene torrents, web rips, remuxes), media is distributed as a **deconstructed release package**:
- A video stream container (`show_s01e01.mkv`).
- High-fidelity external audio tracks (`show_s01e01.RU.mka`, `show_s01e01.JP.mka`).
- Multi-language external styled subtitles (`show_s01e01.RU.ass`, `show_s01e01.EN.srt`).
- An adjacent font directory (`fonts/*.ttf`, `fonts/*.otf`) required for the ASS subtitles to render faithfully.
- Sequential sibling episodes (`show_s01e02.mkv`, etc.).

Users are forced to manually drag and drop audio files, select subtitle files, or even install fonts into Windows system folders.

---

## 2. Decision

We define **`MediaPackage`** as the fundamental domain entity of Universal Media Player:

```
┌─────────────────────────────────────────────────────────────┐
│                        MediaPackage                         │
├─────────────────────────────────────────────────────────────┤
│  PrimaryVideoFile:   show_s01e01.mkv                        │
│  SeriesIdentity:     "Attack on Titan", Season 1, Episode 1 │
│                                                             │
│  AudioTracks:                                               │
│    ├── [Embedded] #1: Japanese AAC 2.0 (id: 1)             │
│    └── [External] #2: Russian FLAC 5.1 (show_s01e01.RU.mka)│
│                                                             │
│  SubtitleTracks:                                            │
│    ├── [External] #1: Russian ASS (show_s01e01.RU.ass)     │
│    └── [External] #2: English SRT (show_s01e01.EN.srt)     │
│                                                             │
│  FontPackage:                                               │
│    ├── FontsDirectory: /path/to/fonts/                      │
│    └── FontFiles: [ "FontA.ttf", "FontB.otf", ... ]         │
│                                                             │
│  SiblingEpisodes:                                           │
│    ├── Previous: show_s01e00.mkv                            │
│    └── Next:     show_s01e02.mkv                            │
└─────────────────────────────────────────────────────────────┘
```

### Architectural Guarantees:
1. **Single Aggregate Root:** The player engine loads a `MediaPackage`, not an isolated video file.
2. **Unified Track Catalog:** Embedded and external tracks are presented in a unified list with clear origin badges (`[Embedded]` vs `[External]`).
3. **Font Lifecycle Management:** If a `FontPackage` exists, the player automatically configures `libmpv`'s `sub-fonts-dir` to the package fonts directory without touching system fonts.
4. **Episodic Continuity:** The package maintains references to previous and next episodes for seamless playlist transitions.

---

## 3. Consequences

### Positive:
- Solves the primary user pain point: opening a video file immediately loads all matching external audio, subtitles, and fonts automatically.
- Clean separation between file discovery logic and playback execution.
- Enables consistent preference persistence per show/season across multiple files.

### Negative:
- Media opening involves a quick directory discovery phase before starting playback (must complete within < 30ms to maintain instantaneous startup).
