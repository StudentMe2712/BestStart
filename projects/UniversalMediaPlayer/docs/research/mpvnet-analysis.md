# mpv.net Architectural Post-Mortem & Strategic Analysis

- **Document Target**: `UniversalMediaPlayer` Core Architecture Team
- **Author**: Senior Media Technology Researcher & Systems Engineer
- **Status**: Complete Architecture & Engineering Reference
- **Subject Under Review**: `mpvnet-player/mpv.net` (Versions 5.x / 6.x / 7.x)
- **Reference Codebase**: [mpvnet-player/mpv.net](https://github.com/mpvnet-player/mpv.net) (C# / .NET 6/8 / WinForms / WPF / libmpv)

---

## 1. Executive Summary & Historical Context

### 1.1 The Genesis of mpv.net
`mpv` is renowned in the multimedia engineering landscape for its minimalist design, exceptional video rendering quality (via `libplacebo` and Direct3D 11/OpenGL/Vulkan), and low resource overhead. However, on the Windows platform, vanilla `mpv` deliberately lacks a traditional Graphical User Interface (GUI). It presents no native menu bar, no standard context menu, no intuitive settings dialog, and relies on an in-video On-Screen Controller (OSC) drawn via Lua and `libass`.

In 2018, Frank Bicking (`stax76`), a veteran Windows multimedia developer known for StaxRip, initiated `mpv.net`. The design objective was clear: **Create a modern, native Windows desktop player that wraps `libmpv` without sacrificing mpv's command-line scriptability, performance, or configuration architecture.**

```
+-----------------------------------------------------------------------------+
|                               mpv.net Evolution                             |
|                                                                             |
|  2018 - 2020: WinForms Genesis                                              |
|  .NET Framework 4.5/4.8 | Pure WinForms Window | libmpv HWND Embedding     |
|                                                                             |
|  2021 - 2023: The WPF Refactor                                              |
|  .NET Core 3.1 -> .NET 6/7 | WPF Shell + WindowsFormsHost/HwndHost          |
|                                                                             |
|  2023 - Present: Modernization & Maintenance                                 |
|  .NET 8 LTS | Dark/Light Theming | Command Palette | Community Maintenance   |
+-----------------------------------------------------------------------------+
```

### 1.2 Core Architectural Thesis
`mpv.net` took the architectural approach of a **thin managed wrapper**:
1. It hosts `mpv-2.dll` inside a managed .NET runtime via a C# P/Invoke bridge.
2. It embeds the video output surface using the native Win32 window handle (`wid`).
3. It unifies input hotkeys, menus, and user actions through a customized `input.conf` syntax.
4. It delegates all heavy media demuxing, decoding, rendering, and audio output to `libmpv`.

While this strategy enabled rapid feature velocity and native Windows responsiveness, it also inherited architectural debt, specifically the **Win32 Airspace Problem**, fragile font isolation, and a total absence of media release intelligence.

---

## 2. Architecture & Tech Stack Decomposition

The mpv.net software architecture is structured across four primary layers: the Presentation Layer, the Core Controller, the libmpv Interop Bridge, and the unmanaged multimedia engine.

```
+-----------------------------------------------------------------------------------+
|                                mpv.net Architecture                               |
+-----------------------------------------------------------------------------------+
| [PRESENTATION LAYER]                                                              |
|   - WPF MainWindow / MetroWindow (XAML Chrome, Titlebar, Theme Engine)            |
|   - WindowsFormsHost / HwndHost (Container for Win32 Video Surface)              |
|   - WinForms ContextMenuStrip (Populated dynamically from input.conf)             |
|   - WPF Command Palette Window (Fuzzy-search command dispatcher)                  |
+-----------------------------------------------------------------------------------+
                                         |
                                         v
+-----------------------------------------------------------------------------------+
| [MANAGED CONTROLLER LAYER (.NET 8 C#)]                                            |
|   - Core Player Controller (`Player.cs`)                                          |
|   - Configuration Manager (`mpvnet.conf` parser & serializer)                     |
|   - Input / Menu Parser (`input.conf` lexer, hotkey binder)                       |
|   - Scripting Bridge (`mpv.net.lua` IPC handler)                                  |
+-----------------------------------------------------------------------------------+
                                         |
                                         v (P/Invoke / Unmanaged Pointers)
+-----------------------------------------------------------------------------------+
| [LIBMPV INTEROP LAYER (`libmpv.cs`)]                                              |
|   - C# Signatures: mpv_create, mpv_initialize, mpv_command, mpv_set_property      |
|   - Event Pump: Native wakeup callback -> ThreadPool event loop                   |
|   - Marshaling: UTF-8 C-strings <-> System.String, mpv_node <-> Managed Object     |
+-----------------------------------------------------------------------------------+
                                         |
                                         v (C ABI)
+-----------------------------------------------------------------------------------+
| [NATIVE UNMANAGED ENGINE (`mpv-2.dll` + FFmpeg + libass + libplacebo)]            |
|   - Demuxer / Decoders / Filters (FFmpeg)                                         |
|   - Subtitle Engine (libass / FreeType / HarfBuzz)                                |
|   - Video Render Output (D3D11 / Direct3D 11 Video Acceleration)                  |
|   - Audio Engine (WASAPI Shared / Exclusive)                                      |
+-----------------------------------------------------------------------------------+
```

### 2.1 C# P/Invoke Interop Bridge (`libmpv.cs`)
The interop layer marshals data between managed CLR memory and native C heap allocations.

```csharp
namespace MpvNet.API
{
    using System;
    using System.Runtime.InteropServices;

    public enum mpv_format
    {
        MPV_FORMAT_NONE = 0,
        MPV_FORMAT_STRING = 1,
        MPV_FORMAT_OSD_STRING = 2,
        MPV_FORMAT_FLAG = 3,
        MPV_FORMAT_INT64 = 4,
        MPV_FORMAT_DOUBLE = 5,
        MPV_FORMAT_NODE = 6,
        MPV_FORMAT_NODE_ARRAY = 7,
        MPV_FORMAT_NODE_MAP = 8,
        MPV_FORMAT_BYTE_ARRAY = 9
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct mpv_event
    {
        public mpv_event_id event_id;
        public int error;
        public ulong reply_userdata;
        public IntPtr data;
    }

    public static class libmpv
    {
        private const string DllName = "mpv-2.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mpv_create();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mpv_initialize(IntPtr ctx);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mpv_destroy(IntPtr ctx);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mpv_command(IntPtr ctx, [In] IntPtr[] args);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mpv_command_async(IntPtr ctx, ulong reply_userdata, [In] IntPtr[] args);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mpv_set_property(IntPtr ctx, byte[] name, mpv_format format, ref long data);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mpv_set_property_string(IntPtr ctx, byte[] name, byte[] data);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mpv_get_property(IntPtr ctx, byte[] name, mpv_format format, out double data);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mpv_observe_property(IntPtr ctx, ulong reply_userdata, byte[] name, mpv_format format);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mpv_wait_event(IntPtr ctx, double timeout);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mpv_set_wakeup_callback(IntPtr ctx, mpv_wakeup_callback cb, IntPtr d);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void mpv_wakeup_callback(IntPtr d);
    }
}
```

#### Managed Marshaling Overhead & String Allocations
- In early versions, mpv.net repeatedly marshaled C# UTF-16 strings to native ANSI/UTF-8 byte arrays using `Marshal.StringToHGlobalAnsi` or `Encoding.UTF8.GetBytes`.
- In high-frequency telemetry tracking (such as observing `time-pos` at 60Hz), this triggered steady GC Gen 0 pressure.
- Modern iterations mitigated this by caching zero-terminated static byte arrays for frequent property keys (e.g., `pause`, `time-pos`, `volume`, `mute`).

---

## 3. Embedding Technique: Win32 HWND & The Airspace Problem

### 3.1 How mpv.net Embeds libmpv
mpv.net embeds libmpv using the native Win32 window handle option: `wid`.

```
+-----------------------------------------------------------------------------+
| WPF MainWindow (Visual Composition Tree)                                   |
|                                                                             |
|   +---------------------------------------------------------------------+   |
|   | WindowsFormsHost (HwndHost Adapter)                                 |   |
|   |                                                                     |   |
|   |   +-------------------------------------------------------------+   |   |
|   |   | WinForms Panel (Native Win32 HWND)                          |   |   |
|   |   | Style: WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN              |   |   |
|   |   |                                                             |   |   |
|   |   |   +-----------------------------------------------------+   |   |   |
|   |   |   | libmpv Video Output Window (Child HWND)             |   |   |   |
|   |   |   | (Rendered via Direct3D 11 Surface)                  |   |   |   |
|   |   |   +-----------------------------------------------------+   |   |   |
|   |   +-------------------------------------------------------------+   |   |
|   +---------------------------------------------------------------------+   |
+-----------------------------------------------------------------------------+
```

1. **Host Control Creation**: A WinForms `Panel` (or custom `Control`) is hosted inside the WPF visual tree using `WindowsFormsHost`.
2. **Handle Assignment**: The Win32 handle (`panel.Handle`) is extracted.
3. **Setting `wid`**: Prior to `mpv_initialize`, mpv.net executes:
   ```csharp
   long wid = panel.Handle.ToInt64();
   libmpv.mpv_set_option_string(mpvHandle, "wid", wid.ToString());
   ```
4. **Window Resizing**: When WPF layout changes or the window is dragged, the `WindowsFormsHost` automatically triggers Win32 `MoveWindow` / `SetWindowPos` to resize the child HWND.

---

### 3.2 The Airspace Problem: The Critical Flaw of HWND Embedding

The fundamental architectural obstacle encountered by mpv.net is the **WPF Airspace Problem**.

```
+-----------------------------------------------------------------------------+
|                          The Win32 Airspace Conflict                        |
+-----------------------------------------------------------------------------+

      [WPF Visual Tree]                              [Win32 Desktop Manager]
      Rendered via DirectX Surface                    Manages Native Windows
      into a single top-level HWND                   (Z-Order / Clipping Rects)
              |                                                 |
              |                                                 |
              v                                                 v
    +-------------------+                             +-------------------+
    | WPF UI Elements:  |                             | Child HWND        |
    | - Modern Slider   |   CANNOT OVERLAY            | (libmpv Video)    |
    | - Audio Menu      | ==========================> | Always renders on |
    | - Flyout Tooltips |   (Clipped out by Win32     | top of WPF canvas |
    | - Media Controls  |    window manager)          |                   |
    +-------------------+                             +-------------------+
```

#### Technical Mechanics of the Airspace Conflict
1. **Separation of Composition Domains**: WPF renders its entire scene graph into a Direct3D texture composed within a single top-level window. A Win32 child window (`HWND`), however, is an independent surface managed directly by the Windows Desktop Window Manager (DWM).
2. **HWND Z-Order Invariant**: On Windows, a child HWND **always renders on top of the host window's DirectX content** within its bounding rectangle. It is mathematically impossible in standard Win32 composition to render a WPF element inside the WPF DirectX composition tree and have it appear *above* a child HWND occupying that same visual space.

#### mpv.net's Workarounds and Their Consequences

| UI Component | mpv.net Workaround Strategy | Visual & Architectural Compromise |
| :--- | :--- | :--- |
| **On-Screen Controller (OSC)** | Retained vanilla mpv's Lua OSC (`osc.lua` rendered via `libass`). | The OSC looks dated, cannot use modern Fluent/XAML controls, and cannot smoothly animate with WPF's rendering clock. |
| **Context Menus** | Rendered via WinForms `ContextMenuStrip` or WPF `ContextMenu` with `AllowsTransparency=False`. | Menus appear as separate top-level popup HWNDs. When menus open, mouse tracking over the video halts. |
| **Tooltips & Track Popups** | Separate WPF popup windows (`WS_EX_TOPMOST | WS_EX_TOOLWINDOW`). | Visual tearing, lack of shadow blending against video, delayed positioning during window moves. |
| **Command Palette** | Modal dialog window spawned as a distinct top-level HWND centered over the player. | Disrupts immersion; creates multiple taskbar/window focus transitions. |

---

## 4. Input Handling & Command System

The input architecture of mpv.net is one of its most distinctive achievements. Instead of maintaining separate configuration files for keyboard bindings and user-interface menus, mpv.net unified them into `input.conf`.

### 4.1 The Unified `input.conf` Menu Engine

In vanilla mpv, `input.conf` binds keys to commands:
```ini
SPACE cycle pause
f cycle fullscreen
```

mpv.net expanded this syntax using `# menu:` trailing directives:
```ini
# mpv.net input.conf syntax
SPACE       cycle pause                  # menu: Play/Pause
f           cycle fullscreen             # menu: View > Toggle Fullscreen
ctrl+o      loadfile                     # menu: File > Open File...
ctrl+v      loadfile "${clipboard}"      # menu: File > Open Clipboard

# Dynamic track selection menus
_           ignore                       # menu: Audio > Select Track > $audio-tracks
_           ignore                       # menu: Subtitle > Select Track > $subtitle-tracks

# Advanced controls
ctrl+h      cycle-values hwdec auto no   # menu: Video > Toggle HW Acceleration
[           multiply speed 1/1.1         # menu: Playback > Speed > Slower
]           multiply speed 1.1           # menu: Playback > Speed > Faster
```

```
+-------------------------------------------------------------------------+
| mpv.net Dynamic Menu Generation Pipeline                                |
|                                                                         |
|  1. Parse input.conf on player launch                                   |
|  2. Tokenize key, command, and '# menu:' path                           |
|  3. Build hierarchical Win32 / WinForms Menu Tree                       |
|  4. Resolve dynamic tokens ($audio-tracks, $subtitle-tracks, $chapters) |
|  5. Bind Menu Click -> Execute libmpv command                           |
+-------------------------------------------------------------------------+
```

### 4.2 Dynamic Macro Token Substitution
mpv.net recognizes dynamic tokens inside `input.conf` to generate context submenus:
- `$audio-tracks`: Queries the `track-list` property in `libmpv`, filters tracks where `type == "audio"`, and generates radio-button menu items for each language/title.
- `$subtitle-tracks`: Dynamically generates subtitle selection entries, including "Disable Subtitles" and external tracks.
- `$chapters`: Dynamically builds a chapter seek index based on the `chapter-list` property.

### 4.3 The Command Palette
Inspired by modern code editors (VS Code, Sublime Text), mpv.net introduced a searchable Command Palette (bound by default to `grave` or `ctrl+p`):
1. **Introspection**: On boot, mpv.net queries `libmpv` for the complete list of built-in commands (`mpv_get_property` on `command-list`).
2. **Script Discovery**: It parses loaded Lua and JavaScript scripts for custom `script-message` handlers.
3. **Fuzzy Search**: The user types queries (e.g., "subs", "delay", "hwdec"), and a weighted Levenshtein/substring matching algorithm filters commands in real-time.
4. **Execution**: Selecting an entry instantly formats and dispatches the command string to `mpv_command_async`.

---

## 5. Configuration Architecture: `mpvnet.conf` vs. `mpv.conf`

mpv.net splits player state across two primary configuration files:

```
+------------------------------------+      +------------------------------------+
|             mpv.conf               |      |            mpvnet.conf             |
|   (Direct Passthrough to Engine)   |      |      (Player Shell & Wrapper)      |
+------------------------------------+      +------------------------------------+
| # Hardware Decoding                |      | # Window Sizing & Positioning      |
| hwdec=d3d11va                      |      | start-size = session-remember      |
| vo=gpu-next                        |      | ui-theme = dark                    |
|                                    |      |                                    |
| # Audio Profile                    |      | # Menu & UI Behavior               |
| ao=wasapi                          |      | auto-load-folder = yes             |
| wasapi-exclusive=no                |      | recent-count = 15                  |
|                                    |      | command-palette-font-size = 14     |
| # Subtitle Styling                 |      |                                    |
| sub-font="Segoe UI"                |      | # Update Channel                   |
| sub-font-size=48                   |      | check-for-updates = monthly        |
+------------------------------------+      +------------------------------------+
```

### 5.1 Startup Execution Sequence & Synchronization Hazards
The initialization sequence in mpv.net must balance two configuration layers:

```
[mpv.net Process Start]
         |
         v
[Read mpvnet.conf] --------> Configure Host Window (Size, Position, Theme, Dpi)
         |
         v
[mpv_create()]
         |
         v
[Pass "wid" and basic flags]
         |
         v
[Read & Apply mpv.conf] ---> mpv processes internal options
         |
         v
[mpv_initialize()] --------> Runtime Engine Activated
         |
         v
[Apply Deferred Options] --> Properties that require active runtime
```

#### Configuration Synchronization Hazards
1. **Option Clashing**: If a user sets `volume=80` in `mpv.conf` and `volume=50` in `mpvnet.conf`, the property value depends on execution order and can cause race conditions.
2. **Missing Validation**: If an invalid flag is written to `mpv.conf` (e.g., `gpu-api=dx12`), `libmpv` logs an unmanaged error. Because mpv.net does not validate `mpv.conf` with a strict schema parser, initialization can silently fail or drop down to a software rendering fallback without notifying the user.
3. **Absence of a First-Class Settings UI**: mpv.net provides an "Edit mpv.conf" button that launches Windows Notepad. This poses an immediate barrier to mainstream users unfamiliar with CLI-style key-value directives.

---

## 6. Deep Architectural Post-Mortem: Strengths & Weaknesses

### 6.1 What mpv.net Got Right

```
  +-----------------------------------------------------------------------+
  |                        mpv.net Core Strengths                         |
  +-----------------------------------------------------------------------+
  | [x] Native Windows Responsiveness: Ultra-fast launch (<250ms),        |
  |     zero bloat, low memory baseline (60-80 MB).                       |
  | [x] Scriptability: Complete compatibility with mpv Lua/JS ecosystem   |
  |     (autoload.lua, sponsorblock.lua, quality-menu.lua work).          |
  | [x] Context Menu & Command Palette: Brilliant mapping of input.conf   |
  |     to a navigable, keyboard-searchable GUI menu.                     |
  | [x] Pure Portability: Zero registry dependencies; fully functional    |
  |     as a self-contained portable folder structure.                    |
  +-----------------------------------------------------------------------+
```

---

### 6.2 Where mpv.net Struggled / Architectural Shortcomings

#### 1. The Airspace Compromise & UI Stagnation
Because mpv.net stayed committed to Win32 `wid` embedding, it could never build a modern, cohesive desktop UI. Modern video players (like Infuse on macOS/AppleTV or modern WinUI 3 video apps) blend translucent controls, HDR-aware scrub bars, animated volume indicators, and chapter preview thumbnails directly over the video frames. mpv.net was permanently restricted to mpv's internal Lua OSC or discordant popup windows.

#### 2. Lack of Intelligent Media Release Parsing
mpv.net operates purely at the single-file level. It lacks any understanding of multi-file release structures common in high-definition media distribution (Scene, P2P, Anime fansubs).

```
Typical Anime / TV Show Release Structure:
D:\Anime\Sousou_no_Frieren_S01\
├── [SubsPlease] Sousou no Frieren - 01 (1080p) [9A4B8921].mkv
├── [SubsPlease] Sousou no Frieren - 02 (1080p) [3C2E11A8].mkv
├── Audio\
│   ├── [Studio_Band] Frieren - 01 [RUS_Dub_Flac].mka
│   └── [Studio_Band] Frieren - 02 [RUS_Dub_Flac].mka
├── Subtitles\
│   ├── Frieren_01_Signs_Songs.ass
│   ├── Frieren_01_Full_Dialogue.ass
│   ├── Frieren_02_Signs_Songs.ass
│   └── Frieren_02_Full_Dialogue.ass
└── Fonts\
    ├── ShinGoPro-Bold.otf
    └── ComicSansAlt.ttf
```

**How mpv.net handles this directory**:
- mpv.net relies entirely on vanilla mpv's naive fuzzy matching (`--sub-auto=fuzzy`, `--audio-file-auto=fuzzy`).
- **Failure**: mpv's fuzzy matcher fails when audio tracks live inside an `Audio\` subfolder with differing naming prefixes (`[Studio_Band]` vs `[SubsPlease]`).
- The user is forced to manually drag and drop external `.mka` and `.ass` files onto the window for **every single episode**.

#### 3. Subtitle Font Isolation Deficit
mpv.net has no dynamic font sandboxing:
- When playing external ASS subtitles located in `Subtitles\`, the required fonts in `Fonts\` are ignored unless the user manually configured a global `--sub-fonts-dir` in `mpv.conf`.
- If two different anime releases require conflicting versions of the same font family (e.g. customized glyph edits in fansub releases), mpv.net has no isolation mechanism, resulting in mangled subtitle rendering.

#### 4. The Configuration UX Barrier
mpv.net caters heavily to the power user who is comfortable editing text files. For a broader audience, the separation of `mpvnet.conf` and `mpv.conf`, combined with the absence of visual sliders for color grading, tone mapping, equalizer, and hardware decoder selection, remains a major usability barrier.

---

## 7. Comparative Architectural Matrix

| Architectural Dimension | Vanilla mpv (Windows) | mpv.net | UniversalMediaPlayer Target Architecture |
| :--- | :--- | :--- | :--- |
| **GUI Framework** | None (OSC via Lua/libass) | C# WinForms / WPF Hybrid | **WinUI 3 / DirectComposition / DirectX 11 Native** |
| **Video Embedding** | Native Win32 Window | Child HWND via `wid` | **`mpv_render_context` (Direct3D 11 Surface)** |
| **Airspace Problem** | N/A | **Severe** (Clipped UI, floating popups) | **Completely Eliminated** (Zero-copy surface blending) |
| **Custom UI Overlay** | Impossible (Lua OSC only) | Clunky (External dialogs) | **Fluid Modern XAML / Direct2D Hardware Controls** |
| **Input System** | `input.conf` | `input.conf` + Menu Engine | **Dual-Engine: Unified Bindings + Visual Key Re-mapper** |
| **Release Parsing** | None (Single file) | None (Single file) | **Intelligent Lexer (Scene/Anime/P2P, Regex, Hash matching)** |
| **External Track Pairing** | Naive string fuzzy matching | Naive string fuzzy matching | **Semantic Batch Pairing (Audio, Subtitles, Commentary)** |
| **Font Management** | Static `--sub-fonts-dir` | Static `--sub-fonts-dir` | **Dynamic Per-Series Ephemeral Font Sandboxing** |
| **Configuration Model** | Monolithic `mpv.conf` | Two-tier text configs | **Unified Reactive Settings Engine + mpv.conf 2-Way Sync** |

---

## 8. Strategic Blueprint & Key Takeaways for UniversalMediaPlayer

The analysis of `mpvnet-player/mpv.net` provides an invaluable architectural roadmap for `UniversalMediaPlayer`. By preserving mpv.net's triumphs while rectifying its structural bottlenecks, `UniversalMediaPlayer` can establish itself as the premier Windows desktop media engine.

```
+-----------------------------------------------------------------------------------------------+
|                      UniversalMediaPlayer Next-Gen Architecture Blueprint                     |
+-----------------------------------------------------------------------------------------------+
|                                                                                               |
|  [PRESENTATION LAYER]                                                                         |
|  +-----------------------------------------------------------------------------------------+  |
|  | Modern Hardware-Accelerated UI (WinUI 3 / DirectComposition)                            |  |
|  | - Transparent Fluent Scrub Bar, Audio Equalizer Panels, Chapter Carousel Thumbnails    |  |
|  | - Seamless Overlay directly over video frames with zero clipping (No Airspace Issues)   |  |
|  +-----------------------------------------------------------------------------------------+  |
|                                            |                                                  |
|                                            v Direct Surface Blending                          |
|  [RENDERING PIPELINE]                                                                         |
|  +-----------------------------------------------------------------------------------------+  |
|  | Direct3D 11 Render Context (mpv_render_context)                                         |  |
|  | - DXGI Flip Model (DXGI_SWAP_EFFECT_FLIP_DISCARD)                                        |  |
|  | - Zero-Copy GPU Rendering (hwdec=d3d11va)                                              |  |
|  | - Display-Sync & HDR Color Management (libplacebo / vo=gpu-next)                         |  |
|  +-----------------------------------------------------------------------------------------+  |
|                                            |                                                  |
|                                            v                                                  |
|  [MEDIA INTELLIGENCE ENGINE]                                                                  |
|  +-----------------------------------------------------------------------------------------+  |
|  | Multi-File Release Analyzer & Heuristic Matcher                                         |  |
|  | - Automatic discovery of Audio/, Subtitles/, and Fonts/ sibling directories             |  |
|  | - Season/Episode regex extraction (S01E02, 02v2, [Hash])                                |  |
|  | - Dynamic Injection via audio-add, sub-add, and isolated --sub-fonts-dir                   |  |
|  +-----------------------------------------------------------------------------------------+  |
|                                            |                                                  |
|                                            v                                                  |
|  [CORE INTEROP & REACTIVE STATE]                                                              |
|  +-----------------------------------------------------------------------------------------+  |
|  | Two-Way Reactive Property Bridge (mpv_observe_property -> MVVM Observable Models)       |  |
|  | Non-blocking Asynchronous Command Queue (mpv_command_async)                             |  |
|  +-----------------------------------------------------------------------------------------+  |
+-----------------------------------------------------------------------------------------------+
```

### 8.1 Pillar 1: Total Eradication of the Airspace Problem
`UniversalMediaPlayer` must reject `wid` HWND embedding.
- Implement the **Direct3D 11 Render Context (`mpv_render_context`)**.
- Share the Direct3D 11 swapchain with the UI composition engine (DirectComposition or WinUI 3 `SwapChainPanel`).
- This allows full hardware-accelerated XAML controls, volume indicators, and subtitle styling dialogs to float seamlessly over high-bitrate 4K HDR video with buttery 120Hz/144Hz desktop animations.

### 8.2 Pillar 2: The Dedicated Media Intelligence Engine
Where both vanilla mpv and mpv.net leave users stranded, `UniversalMediaPlayer` will introduce a native Media Intelligence Engine:
1. **Directory Tree Scanning**: When an episode file is opened, the player asynchronously scans the parent directory and sibling folders (`Audio\`, `Subs\`, `Subtitles\`, `Dub\`, `Fonts\`).
2. **Heuristic Episode Resolution**:
   - Parses season/episode numbers using robust regex patterns: `(?i)(?:s(?<season>\d+)[.ex_-]?)?e(?<episode>\d+)`.
   - Recognizes anime release naming conventions: `[Group] Title - 04 [1080p] [CRC32].mkv`.
3. **Automated Dynamic Injection**:
   - Automatically injects matching external audio tracks via `audio-add`.
   - Automatically injects matching external subtitles via `sub-add`.
   - Dynamically injects the release's dedicated font folder via `--sub-fonts-dir`.

### 8.3 Pillar 3: Ephemeral Subtitle & Font Sandboxing
To guarantee 100% faithful ASS/SSA subtitle typesetting without host OS pollution:
- Isolate fonts on a per-release basis.
- Never call Win32 GDI `AddFontResourceEx`.
- Point `libmpv`'s `sub-fonts-dir` property dynamically to the active media font repository.
- Support embedded Matroska font extraction directly in memory.

### 8.4 Pillar 4: Reactive MVVM State Synchronization
Replace stringly-typed messaging with a reactive view model layer:
- Observe key properties (`time-pos`, `duration`, `pause`, `volume`, `track-list`, `hwdec-current`, `video-params`) via `mpv_observe_property`.
- Marshall property change notifications off the native thread pump and dispatch them into a strongly-typed MVVM state container.
- Enable instant two-way binding: dragging a UI slider immediately dispatches `seek`, while engine timecode updates smoothly refresh the UI time label.

### 8.5 Pillar 5: Hybrid Configuration Engine
Bridge the gap between CLI power users and modern GUI enthusiasts:
- Build an intuitive, visual settings UI with real-time sliders for brightness, contrast, saturation, audio equalizer, and hardware decoder selection.
- Implement a **two-way declarative serializer**: modifying a setting in the GUI writes clean, standardized directives to `mpv.conf`, allowing power users to inspect and edit the configuration in their text editor of choice without breaking the player.
