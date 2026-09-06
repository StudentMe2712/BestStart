# ADR 0010: Native Child HWND Input Routing, Light Alloy Playback Ergonomics, and Windows Shell Integration

- **Status:** Accepted
- **Date:** 2026-09-05
- **Deciders:** UniversalMediaPlayer Architecture Team
- **Milestone:** Phase 9 (Ergonomics & Shell Integration Bug Fixes)

---

## 1. Context

During manual and integration testing of UniversalMediaPlayer under real Windows 11/10 desktop environments, several critical ergonomics and interaction defects were identified:
1. **Audio Icon Wave Clipping:** The volume speaker icon was clipped on the right due to default button padding constraints.
2. **Mouse Wheel Inoperability:** The interface could not be scrolled using the mouse wheel because controls lacked a ScrollViewer and the native video child HWND intercepted mouse wheel messages.
3. **Timeline Scrubbing Discrepancies:** Scrubbing the slider displayed raw unformatted floating point values (e.g. 1220.3) in the thumb tooltip instead of structured MM:SS or H:MM:SS timecodes.
4. **Child HWND Input Sinkhole:** The native Win32 static window hosting libmpv's Direct3D swapchain consumed all Win32 mouse clicks, double-clicks, and keyboard messages.
5. **Redundant Top Title Bar:** An internal XAML top header ('Universal Media Player') and white divider line cluttered the viewing area.
6. **Shell Icon Absence:** The executable lacked a production multi-resolution Windows icon (.ico) for the .exe file, Taskbar, and Alt+Tab application switcher.

---

## 2. Decision

### 2.1 Native Child Window Subclassing & Input Routing
To resolve the Win32 airspace sinkhole without compromising libmpv swapchain performance:
- The video child HWND is subclassed via SetWindowLongPtr with GWLP_WNDPROC pointing to VideoHostWndProc.
- WM_LBUTTONDBLCLK and double-click timing via GetDoubleClickTime() are dispatched to ToggleFullscreen().
- WM_MOUSEWHEEL events are intercepted and redirected to HandleMouseWheel(delta), which smoothly scrolls BottomScrollViewer.
- WM_MOUSEMOVE coordinates are converted from Win32 physical pixels to WinUI DIPs; when pointer enters the bottom playback zone (within 180-200 DIPs from the bottom edge), ShowControls() and ResetAutoHideTimer() are triggered immediately.
- WM_KEYDOWN and WM_SYSKEYDOWN messages are forwarded to the parent WinUI window HWND via PostMessage, ensuring unified shortcut routing (Space, F, M, Left/Right, Esc).

### 2.2 Strict Timecode Formatting Standards
- FormatHelper.FormatTimecode strictly implements:
  * Under 1 hour: MM:SS (e.g. 00:05, 12:20, 20:20).
  * 1 hour or more: H:MM:SS (e.g. 1:01:05, 2:05:15).
- A dedicated XAML value converter TimecodeValueConverter (IValueConverter) is attached to TimelineSlider.ThumbToolTipValueConverter, guaranteeing that slider thumb tooltips during dragging display clean timecodes without raw floating-point numbers.

### 2.3 Light Alloy Transport Ergonomics
- Left and Right arrow keys seek exactly +-10 seconds with bounds clamping: target = Math.Clamp(current +- 10, 0, duration).
- Floating On-Screen Display (OSD) badge appears in the top-right corner with formatted text '+10 секунд' or '−10 секунд', auto-dismissing after 1.5 seconds.
- Seeking and keyboard shortcuts respect active text focus (ignored when typing in TextBox or search inputs).

### 2.4 Unified Micro Control Bar & Clean Visual Surface
- Redundant XAML top bar and white dividing lines are removed. The clean client area renders edge-to-edge video canvas, while the window title is set directly on AppWindow.Title.
- The bottom area is consolidated into a single unified dark surface (#161618) containing metadata, timeline scrubber, and transport controls wrapped in a ScrollViewer (BottomScrollViewer).
- Controls automatically hide after 5 seconds of playback inactivity, unless the pointer is hovering over controls, a slider is being dragged, a track selector flyout is open, or an episodic confirmation card is pending.

### 2.5 Windows Shell Icon Pipeline
- Generated standard multi-resolution .ico containing 16x16, 20x20, 24x24, 32x32, 48x48, 64x64, 128x128, and 256x256 image formats from icon.jpg.
- Linked into UniversalMediaPlayer.App.csproj via <ApplicationIcon>Assets\app_icon.ico</ApplicationIcon>.
- Set at runtime via AppWindow.SetIcon(iconPath) and Win32 WM_SETICON (ICON_BIG and ICON_SMALL) to ensure instant rendering across Title Bar, Taskbar, and Alt+Tab switcher.

---

## 3. Consequences

### Positive
- Fully natural media player ergonomics matching Light Alloy standards.
- Video playback remains fluid with zero DirectComposition or Win32 message overhead.
- Volume speaker glyph and all UI icons display completely without clipping or distortions across all display scalings.
- Dragging slider displays precise timecodes matching the player timecode readout.
- Clean, immersive viewing experience with zero UI clutter.

### Verification
- 147 unit, integration, and UI workflow tests passing in dotnet test -c Release.
- Automated runtime UI verification suite executing on the live app with real video files (tests/TestData/Anime/S01E01.mkv), validating OSD seek notifications, mouse wheel scrolling, fullscreen toggle, and auto-hide behavior with pixel-perfect screenshots.
