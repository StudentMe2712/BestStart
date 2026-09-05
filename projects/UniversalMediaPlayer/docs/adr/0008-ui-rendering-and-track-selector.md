# ADR 0008: UI Rendering Integration & Modern Minimalist Track Selector UX

- **Status:** Accepted
- **Date:** 2026-09-05
- **Deciders:** UniversalMediaPlayer Architecture Team
- **Milestone:** Phase 8 (MVP-1)

---

## 1. Context

In Phase 8 (MVP-1), Universal Media Player transitioned from headless/technical proof validation (MVP-0) to providing a modern minimalist user interface governed by Light Alloy ergonomics and Windows 11 Fluent Design principles.

Key engineering challenges addressed:
1. **Video Surface Hosting & Airspace Resilience:** Hosting `libmpv`'s Direct3D 11 swapchain inside a WinUI 3 (Windows App SDK) desktop window without airspace rendering artifacts, black frame glitches, or Z-order fighting.
2. **UI & Core Decoupling:** Ensuring presentation logic strictly orchestrates interactions without polluting the Core domain, Discovery engine, or Playback engine.
3. **Contextual Track Selector & Badges:** Presenting external and embedded audio/subtitles with clear visual badges (`[External]`, `[Embedded]`, format, channel count, language flags).
4. **Auto-Hiding Controls & Keyboard Centralization:** Providing distraction-free viewing with an auto-hiding micro control bar and a centralized keyboard command routing architecture.

---

## 2. Decision

### 2.1 Video Surface Hosting Architecture
- The main window (`MainWindow`) creates a dedicated Win32 child window (`WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS`, class `"static"`) parented to the WinUI 3 window `HWND`.
- The child window handle is supplied to `libmpv`'s `wid` option.
- When the window resizes, `SyncVideoHostSize()` dynamically tracks the container bounds and applies Win32 `MoveWindow` adjusted for Per-Monitor V2 DPI scaling.
- To avoid Win32 airspace occlusion, the micro control bar docks dynamically at the bottom, and popups/flyouts (Track Selector, Context Menus) utilize WinUI 3 native popup window surfaces (`WS_POPUP`). When controls auto-hide during playback, the video surface expands to occupy 100% of the window area.

### 2.2 Layered Architectural Separation
```
UniversalMediaPlayer.sln
├── UniversalMediaPlayer.Core        # Domain Models (MediaPackage, Tracks, Episodes)
├── UniversalMediaPlayer.Discovery   # DirectoryScanner, MatchEngine, LanguageDetector
├── UniversalMediaPlayer.Playback    # IPlaybackEngine abstraction & LibMpvPlaybackEngine
├── UniversalMediaPlayer.UI          # Portable ViewModels, FormatHelper, KeyboardCommandRouter
├── UniversalMediaPlayer.App         # WinUI 3 Desktop Shell (XAML, Win32 Hosting, Windowing)
└── UniversalMediaPlayer.Tests       # Automated Unit, Integration & UI Workflow Test Suite
```
- `UniversalMediaPlayer.UI` is completely headless (.NET 8 standard), enabling automated testing of view models, formatting, and shortcut routing without launching a graphical environment.
- `UniversalMediaPlayer.App` strictly binds XAML views to `UniversalMediaPlayer.UI` ViewModels and delegates transport commands to `IPlaybackEngine`.

### 2.3 Contextual Track Selector
- Reusable `TrackSelectorViewModel` and `TracksMenuFlyout` categorizing streams into Audio and Subtitles.
- Direct binding to domain `AudioTrack` and `SubtitleTrack` models.
- Seamless mid-stream switching via `SelectAudioTrackAsync` and `SelectSubtitleTrackAsync` without reloading the media file or resetting playback position.
- Dedicated "Subtitles Off" toggle.

### 2.4 Centralized Keyboard Command Routing
- Centralized `KeyboardCommandRouter` mapping keys (`Space`, `Arrows`, `F`, `M`, `Enter`, `Escape`, `A`, `S`) to abstract `PlayerAction` commands.
- High-contrast, auto-hiding On-Screen Display (OSD) overlay badges providing immediate visual confirmation.

---

## 3. Consequences

### Positive
- Zero airspace or DirectComposition flickering during window resize, transport operations, or fullscreen transitions.
- High testability: UI state, formatting, and keyboard shortcuts can be verified in automated unit test runs.
- Instantaneous file loading and background non-blocking directory discovery.
- Fully compliant with Light Alloy keyboard-first UX guidelines.

### Negative
- Unpackaged Windows App SDK deployment requires bundling Windows App SDK runtime assets or self-contained build configuration.
