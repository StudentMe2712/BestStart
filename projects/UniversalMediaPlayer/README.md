# Universal Media Player

A modern, lightweight, local-first Windows media player engineered for maximum playback compatibility across modern, legacy, and exotic media formats, featuring intelligent understanding of multi-file media releases.

---

## Overview

Traditional media players treat media playback as a simple **File → Play** operation. In contrast, **Universal Media Player** treats playback as a **Media Release Experience**:
```
FILE
  ↓
UNDERSTAND MEDIA
  ↓
BUILD MEDIA PACKAGE
  ↓
SELECT BEST TRACKS
  ↓
PLAY
```

When you open a video file (e.g. `Show_S01E01.mkv`), Universal Media Player automatically discovers and binds:
- External high-fidelity audio tracks (`Show_S01E01.RU.mka`, `.ac3`, `.flac`)
- External styled subtitles (`Show_S01E01.RU.ass`, `.srt`)
- Neighboring font packages (`fonts/*.ttf`, `fonts/*.otf`) without polluting Windows system font registries
- Sibling sequential episodes (`S01E02.mkv`, `S01E03.mkv`) for continuous playlist navigation

---

## Why It Exists

Modern users face an unsatisfying compromise:
1. **Classic Windows Players (e.g. MPC-BE, Light Alloy):** Familiar, lightweight, and keyboard-friendly, but tied to legacy DirectShow filters, fragile COM merit systems, and lack automatic recognition of complex external fansub/scene release packages.
2. **Minimalist Wrappers (e.g. mpv.net, vanilla mpv):** Excellent video rendering via `libmpv`, but primitive GUI interactions, raw text-file configs (`mpv.conf`), lack intelligent multi-file pairing, and suffer from Win32 airspace issues.
3. **Heavy Media Centers (Kodi, Plex):** Slow, resource-heavy, reliant on cloud scraping and database indexing, and poorly suited for quickly opening a file from File Explorer.

**Universal Media Player** combines the instant cold-start and keyboard-first minimalism of **Light Alloy 4.11.2** with the rendering power of **`libmpv`** and a proprietary **Media Discovery & Matching Engine**.

---

## Core Features

- **Instantaneous Startup:** Cold start in `< 150 ms`, playback starting in `< 100 ms`.
- **Media Package Model:** Unifies video, external audio, external subtitles, fonts, and episode identities into a single aggregate root.
- **Deterministic Score-Based Matching:** Robust regex tokenizer and Levenshtein/Jaccard scoring engine that pairs external tracks while fatally gating false positives.
- **In-Memory Font Sandboxing:** Dynamically loads adjacent subtitle fonts directly into the `libass` font cache, ensuring authentic typesetting without modifying Windows system fonts.
- **Episodic Continuity:** Automatically groups sibling episodes and carries forward your preferred audio and subtitle choices to subsequent episodes.
- **Resume Playback:** Caches playback positions locally in a crash-resilient SQLite database.
- **Keyboard-First Control:** Every function mapped to intuitive, single-key or standard shortcuts (`Space`, `Left`/`Right`, `Ctrl+Arrows`, `F`, `M`, `A`, `S`).
- **100% Local-First & Private:** Zero cloud dependencies, zero telemetry, zero background network calls.

---

## Architecture Overview

Universal Media Player follows a clean layered architecture with strict decoupling between the presentation layer and media engines:

```
src/
├── UniversalMediaPlayer.App/         # WinUI 3 (Windows App SDK) Presentation Shell & Windowing
├── UniversalMediaPlayer.UI/          # Decoupled UI Layer (ViewModels, Formatting, Shortcut Router)
├── UniversalMediaPlayer.Core/        # Pure Domain Models (MediaPackage, MediaItem, Tracks, Episode)
├── UniversalMediaPlayer.Discovery/   # DirectoryScanner, FilenameParser, EpisodeParser, MatchEngine
├── UniversalMediaPlayer.Playback/    # IPlaybackEngine interface and LibMpvPlaybackEngine adapter
├── UniversalMediaPlayer.Persistence/ # Multi-tier storage (JSON settings + SQLite history)
└── UniversalMediaPlayer.Tests/       # Comprehensive unit, integration, and UI workflow test suite
```

### Architectural Guarantees:
- **Core has zero UI dependencies:** The business logic and media models can be tested completely headless.
- **Engine Abstraction:** Playback is abstracted behind the `IPlaybackEngine` interface, enabling engine replacement or testing with mocks.
- **Single Source of Truth:** All technical contracts, requirements, and roadmaps are governed by [`media_player_spec.md`](media_player_spec.md).

---

## Playback Engine & Compatibility

Universal Media Player uses **`libmpv`** (libmpv-2.dll) as its primary playback engine, delivering hardware-accelerated video decoding (Direct3D 11 Video Acceleration), HDR tone-mapping (`libplacebo`), WASAPI audio output, and high-fidelity subtitle rasterization (`libass`).

Supported format categories include:
- **Containers:** MKV, MP4, WebM, MOV, AVI, WMV, ASF, FLV, OGV, TS, M2TS, VOB, RMVB, 3GP
- **Video Codecs:** AV1, HEVC/H.265 (8/10/12-bit), AVC/H.264, VP9, VP8, MPEG-4 ASP (DivX, XviD), MPEG-2, MPEG-1, VC-1, WMV3, RealVideo, Theora, Cinepak, Indeo
- **Audio Codecs:** FLAC, ALAC, Dolby TrueHD, DTS-HD MA, PCM, Opus, Vorbis, AAC, AC-3, E-AC-3, MP3, WMA, Monkey's Audio (APE), WavPack
- **Subtitles:** ASS, SSA, SRT, WebVTT, SAMI, MicroDVD, PGS, VobSub (IDX/SUB)
- **HDR Standards:** HDR10, HDR10+, HLG, Dolby Vision (Profile 5, 7, 8)

*Detailed verification status and test criteria are tracked in [`docs/test-matrix.md`](docs/test-matrix.md).*

---

## Development Setup

### Prerequisites
- Windows 10 (version 1809+) or Windows 11
- .NET 8 or .NET 9 SDK (`dotnet --version` >= 8.0.400)
- Visual Studio 2022 (version 17.8+) or JetBrains Rider with .NET Desktop workloads
- Git for Windows

### Building the Project
```powershell
# Clone repository
git clone https://github.com/StudentMe2712/BestStart.git
cd BestStart/projects/UniversalMediaPlayer

# Restore dependencies
dotnet restore

# Build solution
dotnet build -c Debug

# Run tests
dotnet test
```

---

## Documentation & Architecture Decision Records (ADRs)

- [Master Specification (`media_player_spec.md`)](media_player_spec.md) — Single Source of Truth
- [Compatibility Test Matrix (`docs/test-matrix.md`)](docs/test-matrix.md)
- [Performance Benchmarks (`docs/performance.md`)](docs/performance.md) — Measured startup, discovery, and playback metrics
- [Phase 8.5 Validation Gate Report (`docs/validation/phase-8-validation.md`)](docs/validation/phase-8-validation.md) — Architectural & empirical audit (74/74 tests)
- **Research Reports (`docs/research/`):**
  - [Playback Backend Analysis](docs/research/backend-analysis.md)
  - [Light Alloy Analysis](docs/research/light-alloy-analysis.md)
  - [libmpv Deep Dive](docs/research/mpv-analysis.md)
  - [mpv.net Architectural Analysis](docs/research/mpvnet-analysis.md)
  - [MPC-BE Architectural Analysis](docs/research/mpcbe-analysis.md)
  - [Licensing Audit & Strategy](docs/research/licensing-analysis.md)
  - [Media Format Compatibility Analysis](docs/research/media-compatibility-analysis.md)
- **Architecture Decision Records (`docs/adr/`):**
  - [ADR 0001: Project Foundation](docs/adr/0001-project-foundation.md)
  - [ADR 0002: Primary Playback Backend (libmpv)](docs/adr/0002-playback-backend.md)
  - [ADR 0003: Desktop GUI Framework (WinUI 3)](docs/adr/0003-ui-framework.md)
  - [ADR 0004: MediaPackage Domain Model](docs/adr/0004-media-package.md)
  - [ADR 0005: Deterministic Matching Engine](docs/adr/0005-matching-engine.md)
  - [ADR 0006: Persistence Architecture](docs/adr/0006-persistence.md)
  - [ADR 0007: Legacy Backend Policy (MPC-BE)](docs/adr/0007-legacy-backend.md)
  - [ADR 0008: UI Rendering Integration & Track Selector UX](docs/adr/0008-ui-rendering-and-track-selector.md)
  - [ADR 0009: Show Preferences & Watch History](docs/adr/0009-show-preferences-and-watch-history.md)

---

## Licensing

- **Source Code:** Released under the [MIT License](LICENSE).
- **Binary Distributions:** Distributed under the **GNU General Public License v3.0 or later (GPL-3.0-or-later)** when packaged with prebuilt `libmpv` binaries incorporating GPL-licensed FFmpeg libraries.
- Clean-room development principles strictly enforced: no source code copied from MPC-BE (GPLv3) or Light Alloy (proprietary).
