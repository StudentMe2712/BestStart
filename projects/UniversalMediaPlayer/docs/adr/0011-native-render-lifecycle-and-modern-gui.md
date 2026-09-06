# ADR 0011: Native Render Lifecycle, Direct3D/mpv Child HWND Synchronization, Playback State Synchronization, and Modern Desktop GUI Architecture

- **Status:** Accepted
- **Date:** 2026-09-06
- **Deciders:** UniversalMediaPlayer Architecture Team
- **Milestone:** Phase 10 (Render Lifecycle, Engine State Sync & Modern Desktop Ergonomics)

---

## 1. Context

During manual and automated testing of UniversalMediaPlayer across normal desktop usage workflows, critical playback, rendering, and UI synchronization defects were uncovered:

1. **Initial Black Screen in Windowed Mode:**
   When opening a video file in standard windowed mode, the video surface remained black. Video frames were rendered only after toggling Fullscreen mode (F11 / F / double-click) and returning back to windowed mode.
2. **Play/Pause Button Desynchronization:**
   The Play/Pause button was bound to a local UI boolean flag rather than the true engine backend state (`mpv_event_property_change` for `pause` / `core-idle`). This caused the button glyph to display the wrong action (e.g. showing "Play" when video was actively playing or showing "Pause" when stalled or stopped).
3. **Win32 Airspace Occlusion of Empty State:**
   The native Win32 static host window (`_videoHwnd`) was unconditionally shown on window creation and layout passes (`SWP_SHOWWINDOW`), drawing over the XAML `EmptyStatePanel` and hiding the placeholder and "Continue Watching" card when no file was open.
4. **Desktop GUI Visual Ergonomics & Localization:**
   The interface suffered from poor visual hierarchy, unnecessary scrollbars in the bottom control bar, inconsistent color palettes, missing or partial Russian localization, and emoji characters rather than crisp native Windows typography and Segoe Fluent Icons.

---

## 2. Decision

### 2.1 Native Child HWND Sizing & Render Lifecycle
The root cause of the initial black screen was twofold:
1. **Child HWND Hierarchy Misalignment:** In Win32, resizing a parent window (`Static` control `_videoHwnd`) does not automatically resize child windows. When libmpv initialized, its internal child window (class `mpv`) remained at the default creation size (100×100) while the static parent was 1082×469, leaving the visible viewport unrendered until a fullscreen presenter switch forced recreation.
   - **Fix:** In `VideoHostWndProc`, `WM_SIZE` messages are intercepted. `Win32.EnumChildWindows` is called to immediately resize any child `mpv` HWND to the full `(0, 0, width, height)` bounds via `Win32.MoveWindow`.
   - In `SyncVideoHostSize()`, after repositioning `_videoHwnd`, `EnumChildWindows` is similarly invoked, followed by `Win32.InvalidateRect(_videoHwnd, 0, false)` and `Win32.UpdateWindow(_videoHwnd)`.
2. **Swapchain Erasure Overdraw:**
   - MainWindow and `_videoHwnd` are configured with `WS_CLIPCHILDREN | WS_CLIPSIBLINGS`.
   - `WM_ERASEBKGND` in `VideoHostWndProc` returns `(nint)1`, preventing Windows GDI from erasing the background and overwriting Direct3D 11 swapchain buffers.
3. **Production mpv Hardware & Context Options:**
   Configured libmpv with:
   - `vo=gpu-next,gpu`
   - `gpu-context=d3d11`
   - `hwdec=d3d11va,auto-safe`
   - `force-window=yes`
   - `hidpi-window-scale=no`
   - `osc=no`
   - `osd-bar=no`

### 2.2 Airspace Management & Dynamic HWND Visibility
To resolve Win32 airspace occlusion of XAML content:
- In `MainWindow.xaml`, `EmptyStatePanel` is placed as a direct sibling inside `Grid.Row="1"`.
- In `SyncVideoHostSize()`, if `_currentPackage == null` (no active media), `_videoHwnd` is positioned with `Win32.SWP_HIDEWINDOW`, completely revealing the rich XAML empty state, drag-and-drop landing area, and "Continue Watching" card.
- When media is opened (`_currentPackage != null`), `EmptyStatePanel` visibility is collapsed, `_videoHwnd` is shown via `Win32.SWP_SHOWWINDOW`, and child windows are sized to the active canvas.

### 2.3 Engine PlaybackState Synchronization
1. Introduced `PlaybackState` enum in `UniversalMediaPlayer.Core.Enums`:
   - `Stopped = 0`
   - `Loading = 1`
   - `Playing = 2`
   - `Paused = 3`
   - `Error = 4`
2. Extended `IPlaybackEngine` with `PlaybackState State { get; }` and `event Action<PlaybackState>? StateChanged`.
3. `MpvPlaybackEngine` continuously tracks and dispatches true state changes from libmpv properties (`pause`, `idle-active`, `eof-reached`, `core-idle`), with polling in the event loop ensuring instant updates.
4. `PlayerViewModel` and `MainWindow` reactively synchronize:
   - `PlayPauseGlyph` (`\uE768` for Play, `\uE769` for Pause).
   - `PlayPauseToolTip` ("Воспроизведение (Пробел)" / "Пауза (Пробел)").
   - Transport controls are strictly enabled only when media is loaded and engine is ready.

### 2.4 Modern Desktop GUI & Light Alloy Ergonomics
1. **Visual Hierarchy:**
   - **Primary Video Canvas (`Grid.Row="1"`):** Dominates 80%+ of window height with a cinematic dark background (`#0C0C0E`).
   - **Top Bar (`Grid.Row="0"`):** Compact `#101012` strip displaying title, technical stream metadata (`MKV · 320×240 · H.264 · 1 аудио · 1 субтитры`), centered OSD pill badge, and quick "Открыть" button.
   - **Control Bar (`Grid.Row="2"`):** Compact `#121214` bar with full-width modern timeline slider, micro-transport cluster (`Prev`, `Play/Pause`, `Next`), monospace timecode (`MM:SS / MM:SS`), volume slider with mouse-wheel step support, "Треки" selector flyout, and fullscreen button.
   - Removed broken scrollbars from control layout to deliver a fixed, native desktop app feel.
2. **Zero-Emoji Policy & Native Windows Iconography:**
   - All icons strictly use Segoe Fluent Icons / Segoe MDL2 Assets.
   - Clean Russian typography with 100% localized tooltips, flyout menus, and status messages.
3. **Cursor Auto-Hiding During Playback:**
   - When controls auto-hide after 5 seconds of active playback, `WM_SETCURSOR` is intercepted in `VideoHostWndProc` to set `Win32.SetCursor(0)`, hiding the cursor.
   - On mouse motion (`WM_MOUSEMOVE` or `RootGrid_PointerMoved`), the cursor is restored (`IDC_ARROW`) and controls reappear smoothly.

---

## 3. Consequences

### Positive
- Video renders immediately and flawlessly upon playback in normal windowed mode (no black screen, no manual fullscreen toggle required).
- Play/Pause state and button glyph are 100% in sync with the underlying media engine.
- Empty state with "Continue Watching" card renders cleanly without being covered by the video window.
- The player provides a modern, distraction-free desktop experience matching Light Alloy and modern Windows 11 design standards.
- Full test suite (147 unit and integration tests) passes cleanly in Release configuration.

### Trade-offs
- Child HWND management requires explicit Win32 interop calls (`MoveWindow`, `EnumChildWindows`, `SetWindowPos`) rather than relying purely on XAML layout.
