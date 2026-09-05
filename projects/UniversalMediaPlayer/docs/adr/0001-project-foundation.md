# ADR 0001: Project Foundation & Clean Layered Architecture

- **Status:** Accepted
- **Date:** 2026-09-05
- **Deciders:** UniversalMediaPlayer Architecture Team

---

## 1. Context

Desktop media players on Windows fall into two distinct and flawed extremes:
1. **Monolithic Legacy Players (e.g. classic MPC-BE, Light Alloy):** Excellent keyboard control and Windows ecosystem familiarity, but tightly coupled to DirectShow filters, legacy Win32/Delphi GUI controls, fragile COM merit systems, and lack modern subtitle/font/episode release awareness.
2. **Minimalist wrappers (e.g. mpv.net, vanilla mpv):** Superior video rendering (libmpv, libplacebo, libass), but suffer from primitive GUI interactions, raw text-file configurations (`mpv.conf`, `input.conf`), lack intelligent release bundling (external audio, subtitles, fonts scattered in folders), and do not treat episodic releases as a cohesive package.

Universal Media Player aims to bridge this divide: providing maximum format compatibility, instantaneous startup, and a lightweight Windows-native UX, combined with a first-class proprietary **Media Package & Matching Engine**.

---

## 2. Decision

We establish the foundational architectural principles and project structure for Universal Media Player:

1. **Strict Decoupling of Concerns:**
   - **`UniversalMediaPlayer.Core`:** Pure business domain models (`MediaPackage`, `MediaItem`, `AudioTrack`, `SubtitleTrack`, `FontResource`, `EpisodeId`, `ShowPreferences`). Zero UI dependencies.
   - **`UniversalMediaPlayer.Discovery`:** Non-blocking file system scanner, filename normalization, tokenizers, episode parsers, language detectors, and the fuzzy score-based `MatchEngine`.
   - **`UniversalMediaPlayer.Playback`:** Backend abstraction (`IPlaybackEngine`), track synchronization, playback state machine, command dispatch, and engine adapters.
   - **`UniversalMediaPlayer.Persistence`:** Multi-tier storage (JSON for settings and show preferences; SQLite for watch history and positions).
   - **`UniversalMediaPlayer.App`:** Windows-native desktop presentation layer (WinUI 3 / XAML) adhering to MVVM.

2. **Technology Stack:**
   - Language: **C# 12 / 13** on **.NET 8 / 9** LTS/STS.
   - Platform: Windows 10 (version 1809+) and Windows 11 (x64 / ARM64).
   - Core Philosophy: Local-first, zero-telemetry, offline-first.

3. **Anti-Overengineering Standard:**
   - Keep classes cohesive and small (< 300 lines typical).
   - Strict avoidance of excessive abstract factories or unnecessary layers of indirection.

---

## 3. Alternatives Considered

- **C++ (Qt 6):** Cross-platform and fast, but adds large runtime DLLs (~60MB+), poor native Windows 11 mica/acrylic styling, and high GUI boilerplate compared to modern C#/.NET.
- **Rust (Slint / Tauri):** Excellent memory safety, but immature Windows desktop GUI ecosystems for rich video overlays, complex XAML styling, and DirectX swapchain integration.
- **Electron / Web Technologies:** Unacceptable startup latency (> 1.5s), bloated memory footprint (> 250MB idle), and lack of low-level swapchain synchronization with video frames.

---

## 4. Consequences

### Positive:
- Clean unit testing of core domain logic and matching algorithms without launching GUI or video hardware.
- High developer productivity with modern C# async/await, nullable reference types, and high-performance Span<T> parsing.
- Seamless Windows 11 aesthetics and accessibility support.

### Negative:
- Interop between managed .NET runtime and native C libraries (`libmpv-2.dll`) requires careful P/Invoke marshaling and unmanaged resource lifecycle tracking.
