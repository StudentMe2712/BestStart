# ADR 0002: Primary Playback Backend Selection (libmpv)

- **Status:** Accepted
- **Date:** 2026-09-05
- **Deciders:** UniversalMediaPlayer Architecture Team

---

## 1. Context

A desktop media player requires a playback engine responsible for container demuxing, video/audio packet decoding, clock synchronization, subtitle rasterization, and GPU rendering. Writing a custom media engine from scratch is an anti-goal that would take years and deliver inferior compatibility compared to mature open-source solutions.

The primary backend must support:
- Exhaustive container and codec coverage (modern MKV/MP4/WebM, legacy AVI/WMV/RMVB, lossless audio, optical disc images).
- Flawless ASS/SSA subtitle rendering with external font injection.
- Dynamic external audio and subtitle track attachment at runtime without playback interruption.
- Hardware decoding (D3D11VA, NVDEC, QSV) and HDR tone-mapping (Dolby Vision, HDR10, HLG).
- Stable C API for hosting inside a Windows desktop window.

---

## 2. Decision

We select **`libmpv`** (version 0.38+ / libmpv-2.dll) as the **primary and default playback engine** for Universal Media Player.

### Architectural Implementation:
1. **Abstraction Boundary:** All interactions occur exclusively through the `IPlaybackEngine` interface. The presentation and discovery layers never call `libmpv` directly.
2. **C API Integration:**
   - Initialization: `mpv_create()`, `mpv_initialize()`.
   - Command Dispatch: `mpv_command()` and `mpv_command_async()`.
   - Property Synchronization: `mpv_observe_property()` polled asynchronously on a dedicated engine worker thread to avoid blocking the UI thread.
3. **External Track Loading:**
   - Audio tracks attached via `audio-add <filepath> auto <title> <lang>`.
   - Subtitles attached via `sub-add <filepath> auto <title> <lang>`.
   - Fonts loaded dynamically via the `sub-fonts-dir` property directed to the `MediaPackage` fonts directory.
4. **Rendering Path:**
   - Initial Phase (PoC / MVP): Native Win32 embedding via the `wid` window handle property.
   - Evolution (Phase 2): `mpv_render_context` with Direct3D 11 (`MPV_RENDER_API_TYPE_DXINTEROP` / DXGI swapchain) for seamless XAML overlay composition if required.

---

## 3. Alternatives Considered

- **DirectShow Graph (LAV Filters + MPC Video Renderer):**
  - *Pros:* Deep Windows legacy integration; standard in MPC-HC/BE.
  - *Cons:* Fragile COM registry registration; difficult to embed cleanly without COM DLL registration; complex external audio stream synchronizer; GPLv3 contamination.
- **Windows Media Foundation (MF):**
  - *Pros:* Native Windows component, zero third-party DLLs.
  - *Cons:* Abysmal container/codec coverage (no native MKV ASS styling, no FLAC/Opus in legacy containers, no external font loading); completely unsuitable for anime or scene releases.
- **LibVLC (VideoLAN):**
  - *Pros:* Mature, cross-platform.
  - *Cons:* Higher memory footprint; less flexible ASS subtitle rendering customization compared to libmpv's native libass pipeline; clunkier external track addition APIs.

---

## 4. Consequences

### Positive:
- Instant support for > 400 codecs and 100 container formats out of the box.
- Industry-leading ASS/SSA rendering fidelity via libass.
- In-memory font loading via `--sub-fonts-dir` eliminates Windows system font pollution.

### Negative:
- Packaging requirement: Must distribute `libmpv-2.dll` and its dependent C runtime libraries alongside the application.
- Licensing compliance: Must strictly adhere to LGPLv2.1+ or GPLv2+ terms depending on the compiled build flags of libmpv.
