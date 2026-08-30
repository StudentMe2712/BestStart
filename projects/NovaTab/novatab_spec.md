# NovaTab — Aesthetic Glassmorphism Dashboard Specification
**Version:** 2.0.0 (Aesthetic Glassmorphism Dashboard 1:1 Transformation)  
**Target Platform:** Google Chrome / Chromium-based Browsers (Manifest V3)  
**Design Philosophy:** Ultra-modern, responsive Glassmorphism dashboard with hardware-accelerated backdrop blur (`blur(24px)`), pure Native CSS3 design system, top floating capsules (Navigation Pills, Global Search Bar, Time & Weather Widget), open category cards grid, and custom video/image wallpaper engine.

---

## 1. Architectural Overview & Manifest V3 CSP Compliance

NovaTab overrides the default browser "New Tab" page (`chrome_url_overrides: { "newtab": "index.html" }`), transforming it into an aesthetic, distraction-free visual dashboard with layered frosted glass surfaces and reactive bookmarks management.

### 1.1 Manifest V3 Content Security Policy (CSP) Architecture

NovaTab is strictly engineered to comply with Chrome Manifest V3 Content Security Policy rules:

| CSP Rule | NovaTab Implementation & Compliance Strategy |
| :--- | :--- |
| **Zero Remote Scripts** | All external CDNs are completely eliminated. The extension operates 100% offline with zero external script fetching. |
| **Zero Inline Scripts** | All `<script>...</script>` tags in `index.html` were removed. The single entry point script is `<script src="app.js"></script>` loaded at the bottom of `<body>`. |
| **Zero Inline Event Handlers** | No `onclick`, `onchange`, `onerror`, or other inline event handler attributes exist in `index.html` or dynamically generated DOM strings. All interactions use `addEventListener` or event delegation. |
| **Zero Dynamic Code Evaluation** | Strictly NO `eval()`, NO `new Function()`, and NO string-evaluated timers (`setTimeout("...", ms)`). All asynchronous timers execute native callback closures. |
| **Zero External Fonts / Stylesheets** | System font stacks (`system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif`) for instantaneous render with zero network delay. |

```
+---------------------------------------------------------------------------------------------------+
|                                        NovaTab Dashboard (Mv3)                                    |
+---------------------------------------------------------------------------------------------------+
|  Manifest Configuration: permissions: ["bookmarks", "storage", "tabs", "favicon"]                 |
|  Overrides: { "newtab": "index.html" } | Service Worker: background.js                           |
+---------------------------------------------------------------------------------------------------+
                                                  |
                         +------------------------+------------------------+
                         |                                                 |
                         v                                                 v
        +---------------------------------+               +---------------------------------+
        |    Background Service Worker    |               |    Glassmorphic SPA Viewport    |
        |        (background.js)          |               |      (index.html / app.js)      |
        +---------------------------------+               +---------------------------------+
        | • Global shortcut Ctrl+Shift+Y  |               | • Top Bar: 3 Floating Capsules  |
        | • Active tab metadata capture   |               |   - Left: Navigation Pills      |
        | • Automatic target folder seed  |               |   - Center: Search Bar          |
        | • Action badge visual feedback  |               |   - Right: Time & Weather       |
        +---------------------------------+               | • Dynamic Russian Quotes Capsule|
                                                          | • Open Category Glass Cards     |
                                                          | • Native IndexedDB (NovaTabDB)  |
                                                          | • Bottom Floating Gear & Bg Btns|
                                                          | • Live Search Palette (Up/Down) |
                                                          | • Real-time Chrome Event Sync   |
                                                          +---------------------------------+
                                                                           |
                                                                           v
                                                          +---------------------------------+
                                                          |  Chrome Storage & Bookmarks API |
                                                          +---------------------------------+
```

---

## 2. Visual System & Pure Native CSS3 Design Tokens

NovaTab employs a multi-tiered glassmorphism visual hierarchy powered by standard CSS `backdrop-filter: blur(24px)` and alpha-channel RGBA borders.

### 2.1 CSS Utility Tokens (`style.css`)

| Class | Properties & Aesthetics | Use Case |
| :--- | :--- | :--- |
| `body` | `width: 100vw; height: 100vh; overflow: hidden; margin: 0; padding: 0; display: flex; flex-direction: column; box-sizing: border-box; font-family: system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;` | Base flex container for top bar and main content. |
| `.bg-video-layer` | `position: fixed; inset: 0; width: 100vw; height: 100vh; object-fit: cover; z-index: -2; pointer-events: none;` | Background HTML5 video layer for high-performance looping video wallpapers. |
| `.bg-overlay-layer` | `position: fixed; inset: 0; width: 100vw; height: 100vh; z-index: -1; pointer-events: none; background-color: rgba(0, 0, 0, var(--overlay-opacity, 0.30)); transition: background-color var(--transition-fast);` | Adjustable dimming layer ensuring Glassmorphism readability over bright backgrounds. |
| `.top-bar` | `display: flex; justify-content: space-between; align-items: center; padding: 24px 40px; width: 100%; box-sizing: border-box; gap: 20px; z-index: 40;` | Top horizontal floating capsules bar. |
| `.glass-panel` | `background: rgba(0, 0, 0, 0.15) !important; backdrop-filter: blur(24px) !important; -webkit-backdrop-filter: blur(24px) !important; border: 1px solid rgba(255, 255, 255, 0.08) !important; border-radius: 24px; box-shadow: 0 8px 32px rgba(0, 0, 0, 0.1) !important; color: #FFFFFF; transition: all 0.3s ease;` | Unified glass panel foundation for capsules, category cards, buttons, and modals. |
| `.top-nav-block` | `border-radius: 100px !important; padding: 4px 8px; display: flex; align-items: center; gap: 4px; max-width: 42vw;` | Left floating navigation pills capsule. |
| `.nav-pill-btn` | `padding: 6px 16px; border-radius: 100px; font-size: 13px; font-weight: 500; cursor: pointer; transition: all 0.3s ease; background: transparent; border: none; color: rgba(255,255,255,0.8);` | Category/board filter pill. |
| `.nav-pill-btn.active` | `background: rgba(0, 0, 0, 0.40) !important; color: #FFFFFF !important; font-weight: 600;` | Currently active category/board pill. |
| `.top-search-block` | `border-radius: 100px !important; padding: 8px 16px; width: 420px; max-width: 35vw; display: flex; align-items: center; gap: 10px;` | Center floating search bar capsule with Google redirect on Enter. |
| `.top-widget-block` | `border-radius: 100px !important; padding: 8px 20px; display: flex; align-items: center; gap: 10px; font-size: 13px; font-weight: 500; white-space: nowrap;` | Right floating time & weather widget capsule. |
| `.main-content-viewport` | `flex: 1; display: flex; flex-direction: column; align-items: center; padding: 10px 40px 40px; overflow-y: auto; width: 100%; box-sizing: border-box; gap: 20px;` | Scrollable viewport hosting quote and category cards. |
| `.quote-container` | `display: flex; flex-direction: column; align-items: center; justify-content: center; text-align: center; margin: 0 auto; max-width: 820px; padding: 8px 20px; border-radius: 16px; cursor: pointer;` | Centered dynamic inspirational quote capsule. |
| `.cards-container` | `display: flex; gap: 24px; justify-content: center; align-items: flex-start; flex-wrap: wrap; width: 100%; max-width: 1600px;` | Flex wrap container for vertical category cards. |
| `.category-card` | `width: 240px; min-width: 220px; max-width: 260px; padding: 20px; border-radius: 24px !important; display: flex; flex-direction: column; gap: 14px;` | Open category cards (AI, Work, Finance, Social, Dev, Streaming). |
| `.bookmark-row-item` | `display: flex; align-items: center; justify-content: space-between; gap: 8px; text-decoration: none; color: #FFFFFF; padding: 4px 0; border-radius: 6px; cursor: pointer; transition: all 0.2s ease;` | Bookmark row with 16x16 favicon, 14px title (`opacity: 0.8` -> `1.0`), and hover action dock. |
| `.floating-gear-btn` | `position: fixed; right: 28px; bottom: 28px; width: 42px; height: 42px; border-radius: 50% !important; z-index: 50; display: flex; align-items: center; justify-content: center;` | Bottom-right floating gear button with rotation hover effect. |
| `.floating-bg-btn` | `position: fixed; left: 28px; bottom: 28px; width: 42px; height: 42px; border-radius: 50% !important; z-index: 50; display: flex; align-items: center; justify-content: center;` | Bottom-left floating wallpaper upload button. |
| `.modal-overlay` & `.modal-box` | Fullscreen frosted overlay with scale-animated glassmorphic modal box. | Search palette, bookmark CRUD, category creator, and settings dialogs. |

---

## 3. Top Floating Bar & Live Widgets

### 3.1 Left Floating Block: Navigation Pills
- Renders master `"✦ Home"` pill alongside user category pills (e.g. AI, Work, Finance, Social, Dev, Streaming) and the `+` Add Category button.
- Custom pills feature HTML5 Drag & Drop reordering and hover-revealed `.delete-board-btn` cross.

### 3.2 Center Floating Block: Search Bar
- Magnifying glass SVG icon on the left.
- `#global-search-input` text field with transparent background, white text, 14px font size.
- Google `"G"` icon on the right.
- On `Enter` key or Google icon click, redirects to `https://www.google.com/search?q=` + `encodeURIComponent(query)`.
- Live search modal remains accessible via `/` keyboard shortcut.

### 3.3 Right Floating Block: Time & Weather Widget
- Displays Location (`"Атырау"`), weather icon (`🌤️`), temperature (`"20°C"`), date in Russian (`"Вс, 30 авг"`), and live clock (`"13:00"`).
- Real-time time & date updates every 1,000ms.

---

## 4. Lively Video Wallpapers & Native IndexedDB Storage Engine

### 4.1 Database Architecture (`WallpaperDB`)
- **Database Name:** `NovaTabDB` (Version `1`)
- **Object Store:** `wallpapers`
- **Key:** `'activeWallpaper'`
- **Record Schema:** `{ blob: Blob/File, type: 'video' | 'image', name: string, updatedAt: number }`

### 4.2 Wallpaper Processing Pipeline
- **Video Uploads (`video/mp4`, `video/webm`, `video/ogg`):**
  1. Stored directly as a binary `Blob`/`File` in `NovaTabDB`.
  2. Object URL generated via `URL.createObjectURL(blob)`.
  3. Bound to `#bg-video` layer (`autoplay`, `loop`, `muted`, `playsinline`).
  4. Static background reset (`document.body.style.backgroundImage = 'none'`).
- **Image Uploads (`image/*`):**
  1. Canvas aspect-ratio downscale to max 1920x1080.
  2. WebP 80% compression.
  3. Stored in IndexedDB and cached in storage.
  4. Applied to `document.body.style.backgroundImage`.
- **Adjustable Dimming Overlay:**
  - `#bg-overlay` layer with `--overlay-opacity` CSS Custom Property (default `0.30`, range `0`–`0.85`).
  - Interactive slider in Settings modal updates dimming and persists preference.

---

## 5. Implementation Checklist & Status

- [x] **100% Manifest V3 CSP Compliance:** Removed all external CDNs, zero inline handlers, zero remote scripts, zero `eval()`.
- [x] **Aesthetic Glassmorphism Dashboard Layout (`index.html`, `style.css`):** Top bar with 3 floating glass capsules (Nav Pills, Search Bar, Time & Weather Widget), main viewport with open category cards, bottom floating gear and wallpaper buttons.
- [x] **Live Clock & Weather Engine (`app.js`):** 1-second interval timer updating `#widget-time` and `#widget-date` in Russian (`Вс, 30 авг`, `13:00`).
- [x] **Global Google Search Integration (`app.js`):** Direct Google Search redirection on Enter in `#global-search-input` + live search palette on `/`.
- [x] **Open Category Cards Grid (`app.js`, `style.css`):** Vertical `.glass-panel.category-card` (240px wide) with 16px bold title, 16x16 favicons, 14px titles (`opacity: 0.8` -> `1.0`), and hover action docks.
- [x] **Drag-and-Drop Category Sorting (`app.js`):** HTML5 drag-and-drop on category pills with `chrome.bookmarks.move` integration and mock tree reordering.
- [x] **IndexedDB Lively Video & Image Wallpapers (`app.js`):** Binary storage in `NovaTabDB` supporting large video wallpapers and WebP compressed images.
- [x] **Floating Settings & Controls (`app.js`, `style.css`):** Bottom-right `#floating-settings-btn` with 45deg rotation hover, bottom-left `#bg-change-btn`.

