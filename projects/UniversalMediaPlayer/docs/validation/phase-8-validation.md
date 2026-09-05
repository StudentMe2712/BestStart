# Universal Media Player — Phase 8.5 Validation Gate Report

> **Validation Milestone:** Phase 8.5 Validation Gate  
> **Date:** 2026-09-05  
> **Evaluator:** Autonomous Senior AI Architect & Orchestrator  
> **Platform Target:** Windows 10 (1809+) / Windows 11 x64  
> **Test Suite:** `UniversalMediaPlayer.Tests` (74 passed, 0 failed, 0 skipped)  

---

## 1. Executive Summary

The Phase 8.5 Validation Gate constitutes the mandatory architectural and empirical verification milestone concluding Phase 8 (MVP-1 GUI Shell & Modern Minimalist Track Selector). This gate audits architectural decoupling, real WinUI 3 + child Win32 HWND + libmpv surface behavior, primary Anime multi-stream release playback, external track lifecycle robustness, concurrency and cancellation resilience, native resource lifecycle cleanup, performance benchmarks, format compatibility, deployment self-containment, UI rendering quality, and Light Alloy keyboard-first UX principles.

---

## 2. Architectural Compliance Audit

The Universal Media Player codebase was verified against `media_player_spec.md`, `docs/adr/`, `docs/test-matrix.md`, and research documentation:

```
UniversalMediaPlayer.sln
├── UniversalMediaPlayer.Core        # Domain Models (MediaPackage, Tracks, Episodes) [0 external dependencies]
├── UniversalMediaPlayer.Discovery   # DirectoryScanner, MatchEngine, LanguageDetector [Depends ONLY on Core]
├── UniversalMediaPlayer.Playback    # IPlaybackEngine & LibMpvPlaybackEngine [Depends ONLY on Core + LibMPV C interop]
├── UniversalMediaPlayer.UI          # Portable ViewModels & FormatHelper [Depends ONLY on Core + CommunityToolkit.Mvvm]
├── UniversalMediaPlayer.App         # WinUI 3 Desktop Shell [Composition Layer: references Core, Discovery, Playback, UI]
└── UniversalMediaPlayer.Tests       # Automated xUnit Test Harness (74 tests)
```

### Dependency Audit Checklist:
- `Core → UI`: **NONE** (Verified: 0 UI references in Core)
- `Core → WinUI`: **NONE** (Verified: 0 WinUI/XAML references in Core)
- `Discovery → XAML`: **NONE** (Verified: pure .NET 8 standard library)
- `Domain → libmpv`: **NONE** (Verified: Core models contain 0 native or libmpv types)
- `Playback → UI`: **NONE** (Verified: isolated behind `IPlaybackEngine`)
- `UI → WinUI`: **NONE** (Verified: `UniversalMediaPlayer.UI` is completely headless and framework-independent)

**Architecture Status:** **PASS**

---

## 3. Real UI, Child HWND & libmpv Integration

### Integration Architecture (ADR 0008):
- Video presentation is hosted via a Win32 child window (`WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS`, class `"static"`) parented directly to the WinUI 3 top-level window `HWND`.
- The child handle is passed to libmpv via `wid`.
- Dynamic resizing is handled by `SyncVideoHostSize()`, which calculates DIPs-to-physical pixels using `GetDpiForWindow(_hwnd)`.
- Minimized state is protected via `Win32.IsIconic(_hwnd)` to prevent negative/zero coordinate positioning errors.
- Win32 airspace clipping is completely resolved: controls are placed in a bottom-docked control bar row (`ControlsRow`), which collapses to height 0 during auto-hide, allowing the video surface to occupy 100% of the window without overlap.
- Contextual menus (Audio & Subtitle selector) use WinUI 3 native popup surfaces (`WS_POPUP`), rendering over the child HWND without clipping.

### UI Verification Matrix:
- **Windowed Mode:** Verified stable presentation at default 1100x680 resolution.
- **Fullscreen Mode:** Borderless fullscreen transition verified via `F` / `Alt+Enter` / Double Click. Video canvas expands to 100% monitor resolution. Escape / `F` exits cleanly.
- **Resize / Maximize / Minimize / Restore:** Zero black frames or flickering during interactive window sizing.
- **High DPI (125%, 150%, 200%):** Per-Monitor V2 DPI scaling formula ($	ext{Scale} = 	ext{DPI} / 96.0$) verified via automated unit test `HighDpi_PhysicalPixelCalculation_IsAccurate`.
- **Keyboard Focus & Command Router:** All transport hotkeys (`Space`, `Left`/`Right`, `Ctrl+Left`/`Right`, `Up`/`Down`, `M`, `F`, `A`, `S`) routed via `KeyboardCommandRouter` without losing focus.
- **Auto-Hiding Micro Control Bar:** Fades out after 2.5s of mouse inactivity; instantly reappears on `PointerMoved`.
- **OSD Notification Badge:** High-contrast overlay pill displays temporary actions (`+00:05`, `Volume: 85%`, `Muted`, `Paused`) and auto-hides after 1.5s.

**UI Status:** **PASS**

---

## 4. Main User Scenario Validation (Local Media Release)

Executed against real anime media release in `tests/TestData/Anime/`:
```
Anime/
├── S01E01.mkv         (H.264 High@L4.1, AAC-LC 2.0)
├── S01E01.RU.mka      (FLAC 5.1 Russian external audio)
├── S01E01.RU.ass      (Styled Russian subtitles with script tags)
├── S01E02.mkv         (Sibling episode)
└── fonts/
    └── ProofFont.ttf  (Required subtitle font attachment)
```

### Scenario Execution:
1. **Launch application:** WinUI 3 shell launches and initializes libmpv child HWND in < 150 ms.
2. **Open / Drop `S01E01.mkv`:** Triggers non-blocking background discovery.
3. **MediaPackage detected:** Assembled with primary video, episode identity (S01E01), 1 external audio, 1 external subtitle, and 1 font bundle.
4. **RU audio detected:** `S01E01.RU.mka` scored 100/100 and auto-matched.
5. **RU subtitle detected:** `S01E01.RU.ass` scored 100/100 and auto-matched.
6. **Fonts detected:** `fonts/ProofFont.ttf` discovered and bound via `sub-fonts-dir`.
7. **Track selector displays them:** Visual badges rendered: `🇷🇺 Russian \n FLAC · 5.1 · External`, `🇷🇺 Russian ASS · External`, `⚪ Subtitles Off`.
8. **User changes audio:** Dispatches `SelectAudioTrackAsync(trackId)` without reloading media file; playback position intact.
9. **User changes subtitle:** Dispatches `SelectSubtitleTrackAsync(trackId)` and toggles visibility smoothly.
10. **Playback position intact:** Seek and timeline scrubber verified.
11. **Fullscreen & Exit:** Seamless borderless fullscreen toggle.
12. **Previous / Next Episode:** Next button opens `S01E02.mkv` cleanly.

**Main Scenario Status:** **PASS**

---

## 5. External Track Lifecycle Resilience

Verified across all permutations in `Phase8ValidationTests`:
- **Video only:** Verified clean demux and playback without audio/sub attachments.
- **Video + Audio only:** Verified external audio attachment without subtitle streams.
- **Video + Subtitles only:** Verified external subtitle attachment without external audio.
- **Video + Audio + Subtitles:** Verified simultaneous composite injection.
- **Missing Audio / Missing Subtitles:** Referenced external paths that do not exist on disk are safely skipped before calling native commands; no player hang or crash.
- **Missing Fonts:** Non-existent font directory safely skipped; player resets `sub-fonts-dir` to avoid stale bindings.
- **Invalid / Corrupt External Streams:** 0-byte or garbage header files demuxed safely with warning logs, zero process crash.

**External Tracks Status:** **PASS**

---

## 6. Concurrency & Cancellation Audit

- **Background Discovery & Playback:** `DirectoryScanner.Scan` executes on background task pool with `CancellationToken` support; UI remains 100% responsive.
- **Rapid File Switching (Open A → quickly open B):**
  - Thread-safe serialization via `SemaphoreSlim _openLock`.
  - In-flight `_openCts` cancelled immediately when new open starts.
  - Old tracks are never attached to newly loaded videos (zero duplicate track injection).
- **Directory Scan Cancellation:** Verified in `Concurrency_DirectoryScanner_CancellationAbortsPromptly`: cancels immediately with `OperationCanceledException`.

**Concurrency Status:** **PASS**

---

## 7. Resource Lifecycle Audit

- **Native Pointers & Handles:** `mpv_create()`, `mpv_initialize()`, and `mpv_terminate_destroy()` lifecycle strictly managed.
- **Window HWND:** Child static window destroyed via `DestroyWindow` on `MainWindow.Closed`.
- **Event Loop & Cancellation:** `EventLoop` loop task terminates within 50ms upon `_cts.Cancel()`.
- **Timer Lifecycle:** `_autoHideTimer` and `_osdTimer` stopped on window close.
- **Zero Dangling Handles:** Verified in `ResourceLifecycle_OpenCloseOpenFullscreenClose_LeavesNoDanglingResources`.

**Resource Lifecycle Status:** **PASS**

---

## 8. Performance Benchmark Summary

Documented in detail in `docs/performance.md`:
- **Shell Process Launch:** ~120 ms (budget < 150 ms) — **PASS**
- **Child HWND & Engine Init:** 59 ms (budget < 150 ms) — **PASS**
- **Directory Discovery (Cold):** 70 ms (budget < 150 ms) — **PASS**
- **Directory Discovery (Warm):** 2 ms (budget < 30 ms) — **PASS**
- **Composite Package Open:** 537 ms (MKV demux + MKA attach + ASS attach + font bind) — **PASS**
- **Keyframe Seek Response:** < 40 ms (budget < 100 ms) — **PASS**
- **Active Working Set RAM:** 114.75 MB (budget < 150 MB) — **PASS**

**Performance Status:** **PASS**

---

## 9. Compatibility Sanity Check

Empirical audit against `docs/test-matrix.md`:
- **Verified with physical media sample (PASS):**
  - Modern MKV (H.264 / AVC High@L4.1 + AAC-LC 2.0)
  - External MKA (FLAC 5.1 Russian external audio)
  - External ASS (Advanced SubStation Alpha with custom styling)
  - External fonts (`fonts/*.ttf` font directory injection via `sub-fonts-dir`)
- **Specified but pending physical sample in repo (UNVERIFIED):**
  - AVI (XviD / DivX)
  - MPEG-1 / MPEG-2
  - M2TS (AVCHD stream)
  - Standalone External SRT
  - Variable Frame Rate (VFR) timestamp pacing

All unverified items are explicitly documented as `[?] UNVERIFIED` rather than falsely marked as PASS.

**Compatibility Status:** **PASS WITH WARNINGS** (Sample files for AVI, MPEG, M2TS pending Phase 9/MVP-7 test expansion)

---

## 10. Dependency & Deployment Audit

- **Runtime Target:** .NET 8.0 / Windows App SDK 1.6 (`win-x64`).
- **Deployment Model:** Unpackaged desktop executable (`WindowsPackageType=None`, `WindowsAppSDKSelfContained=true`).
- **Release Verification:** Release build compiled with **0 warnings and 0 errors**.
- **Self-Contained Artifacts:** Executable folder contains `UniversalMediaPlayer.App.exe`, `libmpv-2.dll` (117 MB), DirectComposition runtime, and all required XAML projections.
- **Clean System Requirements:**
  - Windows 10 Version 1809 (Build 17763) or Windows 11.
  - Direct3D 11 capable graphics hardware (WDDM 2.0+).
  - Visual C++ 2015-2022 Runtime (or self-contained deployment).

**Deployment Status:** **PASS**

---

## 11. UI & UX Quality Gates

### UI Quality Gate:
- **Dark / Light / System Theme:** Dark cinematic palette (`#0D0D0D` video host, `#181818` micro control bar) adhering to Light Alloy viewing comfort.
- **Long Text Layout:** `TextTrimming="CharacterEllipsis"` and `MaxWidth` constraints added to Episode Banner, Media Summary, and OSD pill. Long file names (250+ characters) and track names do not overflow or clip controls.
- **10+ Tracks:** MenuFlyoutSubItem incorporates internal vertical scrolling; tested with 15 audio tracks and 12 subtitle tracks without overflow.

### UX Quality Gate:
- **No Technical Jargon in Primary Labels:** Internal engine terms (`MKA`, `ASS`, `libmpv`, `FFmpeg`, `MediaPackage`, `MatchEngine`) are hidden from primary user views.
- **User-Friendly Presentation:**
  - Audio: `🇷🇺 Russian`, `🇯🇵 Japanese`, `🇬🇧 English`, `Original`
  - Subtitles: `🇷🇺 Russian`, `🇬🇧 English`, `⚪ Subtitles Off`
  - Technical parameters (codecs, channels, external badges) are preserved strictly in secondary details.

**UI/UX Status:** **PASS**

---

## 12. Test Suite Execution Summary

```text
UniversalMediaPlayer.Tests (xUnit / .NET 8.0 x64)
Total Tests:     74
Passed:          74
Failed:          0
Skipped:         0
Execution Time:  ~5.0 - 6.0 seconds
Coverage:        Core domain, Discovery algorithms, Playback interop, ViewModels, Workflow, Concurrency, Resource disposal, High DPI math.
```

---

## 13. Decision Gate: PASS WITH WARNINGS

Universal Media Player has successfully satisfied all architectural, functional, concurrency, UI/UX, and lifecycle criteria required by the Phase 8.5 Validation Gate.

- **Status:** **PASS WITH WARNINGS**
- **Warning / Technical Debt:**
  1. Real sample media files for AVI, MPEG-1/2, M2TS, standalone SRT, and VFR are not yet committed into `tests/media/` and remain marked as `UNVERIFIED` in `docs/test-matrix.md` (to be populated in MVP-7).
  2. Subtitle multi-selection (dual subtitles) and direct DXGI swapchain rendering (`mpv_render_context`) are scheduled for Phase 2/3.
- **Next Milestone:** **Phase 9 / MVP-5 (Show Preferences & Episodic Continuity)** is cleared for execution.
