# ADR 0003: Desktop GUI Framework Selection (WinUI 3 / Windows App SDK)

- **Status:** Accepted
- **Date:** 2026-09-05
- **Deciders:** UniversalMediaPlayer Architecture Team

---

## 1. Context

The user experience (UX) for Universal Media Player is guided by the philosophy of **Light Alloy 4.11.2**:
- Instant startup (< 200 ms).
- Keyboard-first interaction model.
- Minimalist, distraction-free interface (video content takes center stage).
- Compact, high-contrast on-screen display (OSD) and contextual track selector.
- High visual fidelity on Windows 10 and Windows 11 (supporting High-DPI scaling, dark/light themes, and smooth animations).

We must choose a GUI framework that provides modern Windows styling while allowing low-latency video hosting and custom controls without bloat.

---

## 2. Decision

We choose **WinUI 3 (Windows App SDK)** with **C# / .NET 8+** as the desktop presentation framework.

### Key Architectural Guidelines:
1. **Windowing & Hosting:**
   - The main window (`MainWindow`) hosts the video surface in a dedicated container.
   - For MVP, the window handle (`HWND`) of a borderless child hosting panel is passed directly to libmpv's `wid` option.
   - All playback controls, timeline scrubbers, and OSD notifications are implemented as lightweight XAML controls that overlay or dock to the video area.
2. **Auto-Hiding Controls:**
   - Controls automatically fade out after 2.5 seconds of mouse inactivity during playback.
   - Mouse movement or keyboard activity instantly reveals the minimalist control bar.
3. **Contextual Track Selector:**
   - Track selection is presented as a floating contextual flyout or sleek sidebar panel rather than a full-screen dashboard or complex modal dialog.
   - Tracks are grouped into Audio and Subtitle categories with visual badges (`[External]`, `[Embedded]`, `5.1`, `MKA`, `ASS`).
4. **Keyboard Shortcuts:**
   - All common operations (Space: Play/Pause, Left/Right: Seek 5s, Up/Down: Volume, F: Fullscreen, M: Mute, A: Cycle Audio, S: Cycle Subtitles) are routed through a dedicated `KeyboardCommandRouter`.

---

## 3. Alternatives Considered

- **WPF (.NET 8/9):**
  - *Pros:* Mature, vast community ecosystem.
  - *Cons:* Notorious "Airspace" issue (HWND controls render on top of WPF XAML elements, making transparent overlays difficult without complex D3DImage interop); dated default controls.
- **Windows Forms:**
  - *Pros:* Very fast startup, zero airspace issues when embedding HWND.
  - *Cons:* Primitive UI rendering, poor DPI scaling on multi-monitor setups, lacks modern Windows 11 fluent design aesthetic.
- **Avalonia UI:**
  - *Pros:* Cross-platform, modern XAML.
  - *Cons:* Cross-platform abstractions add unnecessary overhead when target platform is strictly Windows 10/11 native.

---

## 4. Consequences

### Positive:
- Native Windows 11 look and feel (Mica material, modern typography, fluent icons).
- Robust High-DPI and multi-monitor scaling handled automatically by Windows App SDK.
- Clean MVVM separation using `CommunityToolkit.Mvvm`.

### Negative:
- Windows App SDK deployment dependencies (must bundle Windows App Runtime or install via MSIX / self-contained deployment).
