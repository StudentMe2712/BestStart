# NovaTab — Aesthetic Glassmorphism Dashboard Specification (Markmez 1:1 Master Architecture)

**Version:** 2.0.0 (Markmez 1:1 Master Cloning Specification)  
**Target Platform:** Google Chrome / Chromium-based Browsers (Manifest V3)  
**Design Philosophy:** Pixel-perfect Markmez Master Template replication. Ultra-modern responsive Glassmorphism dashboard with hardware-accelerated backdrop blur (`--board-blur: 16px`), CSS tokenized glassmorphism (`.board, .glass-panel`), floating pill topbar (dynamic category tabs + weather & clock), scrollable multi-column category boards (`.boards-columns` -> `.board-column` -> `.board`), floating right capsule dock (`#sidebar`), full multi-modal overlay system (`#search-overlay`, `#wp-overlay`, `#widgets-overlay`, `#trash-overlay`, `#settings-overlay`), and native IndexedDB lively video/image background engine.

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
|                                 NovaTab Master Dashboard (Markmez 1:1)                            |
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
        | • Global shortcut Ctrl+Shift+Y  |               | • Topbar: Dynamic Nav & Widgets |
        | • Active tab metadata capture   |               |   - Left: #pagesNav pills + (+) |
        | • Automatic target folder seed  |               |   - Right: Weather & Live Clock |
        | • Action badge visual feedback  |               | • Dynamic Russian Quote Capsule |
        +---------------------------------+               | • #boardsColumns & .board Cards |
                                                          | • Right Floating Dock (#sidebar)|
                                                          | • IndexedDB Video & Image Engine|
                                                          | • 5-Overlay System & Hotkeys    |
                                                          | • Concurrency Mutex & Tree Sync |
                                                          +---------------------------------+
                                                                           |
                                                                           v
                                                          +---------------------------------+
                                                          |  Chrome Storage & Bookmarks API |
                                                          +---------------------------------+
```

---

## 2. Visual System & Pure Native CSS3 Design Tokens

NovaTab employs a multi-tiered glassmorphism visual hierarchy powered by standard CSS `backdrop-filter: blur(var(--board-blur))` and alpha-channel RGBA borders.

### 2.1 CSS Utility Tokens (`style.css`)

```css
:root {
  --board-rgb: 255, 255, 255;
  --board-alpha: 0.15;
  --board-blur: 16px;
  --board-border: rgba(255, 255, 255, 0.18);
  --board-outline-theme-color: rgba(255, 255, 255, 0.750);
  --accent-color: #3a7892;
  --accent-color-hover: #4a91b0;
  --board-hover-bg: rgba(255, 255, 255, 0.08);
  --overlay-opacity: 0.25;
  --text-primary: #ffffff;
  --text-secondary: rgba(255, 255, 255, 0.75);
  --text-muted: rgba(255, 255, 255, 0.5);
  --text-dim: rgba(255, 255, 255, 0.35);

  --ui-modal: rgba(24, 26, 36, 0.88);
  --ui-border: rgba(255, 255, 255, 0.1);
  --ui-fill: rgba(255, 255, 255, 0.06);
  --ui-fill-hover: rgba(255, 255, 255, 0.12);
  --ui-text: rgba(255, 255, 255, 0.92);
  --ui-text-secondary: rgba(255, 255, 255, 0.65);
  --board-w: 250px;

  --radius-board: 20px;
  --radius-pill: 100px;
  --radius-sm: 8px;
  --radius-md: 12px;
  --radius-lg: 16px;

  --transition-smooth: all 0.3s cubic-bezier(0.16, 1, 0.3, 1);
  --transition-fast: all 0.15s ease;
}
```

### 2.2 Glassmorphic Foundations

```css
.board, .glass-panel {
  background: rgba(var(--board-rgb), var(--board-alpha));
  backdrop-filter: blur(var(--board-blur));
  -webkit-backdrop-filter: blur(var(--board-blur));
  border: 1px solid var(--board-border);
  border-radius: var(--radius-board);
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.12);
  color: var(--text-primary);
  transition: var(--transition-smooth);
}
.board:hover {
  border-color: rgba(255, 255, 255, 0.28);
}
```

---

## 3. DOM Skeleton & Component Structure

### 3.1 Background Layers
- `#video-bg`: HTML5 `<video>` layer for looped video wallpapers (`autoplay loop muted playsinline`).
- `#photo-bg`: High-resolution background photo layer (`background-size: cover; background-position: center;`).
- `#bg-overlay`: Dynamic dimming layer with `--overlay-opacity` control.

### 3.2 Topbar (`.topbar`)
- **Left (`.pages-nav` / `#pagesNav`):** Frosted glass pill containing dynamic page tabs (`"✦ Home"`, custom boards, and `+` add board button `#btnAddBoard`). Includes HTML5 Drag & Drop reordering and hover delete crosses (`×`).
- **Right (`.top-widgets` / `#topWidgets`):** Frosted glass pill containing Weather (`#widgetWeather`) + divider + Clock (`#clockDate`, `#clockTime`).

### 3.3 Main Area (`.boards-area` / `#boardsArea`)
- **Quotes Capsule (`#quoteBox`):** Centered dynamic inspirational quote in Russian (`#quoteText`, `#quoteAuthor`). Click to refresh with smooth fade transition.
- **Columns Grid (`.boards-columns` / `#boardsColumns`):** Responsive multi-column layout of category boards (`.board-column` -> `.board.glass-panel`).
- **Category Board (`.board`):** `.board-header` with category title, count badge, quick add `+` button, and delete/clear `🗑️` button. Contains `.bookmark-list` with `.bookmark-row` items.
- **Bookmark Row (`.bookmark-row`):** 16x16 favicon with letter fallback, 14px title with hover opacity transition, and hover-revealed edit `[✏️]` and delete `[🗑️]` buttons.
- **Empty State (`#emptyState`):** Displayed when no bookmarks exist in category.

### 3.4 Floating Sidebar Dock (`#sidebar`)
Fixed capsule dock on right center (`position: fixed; right: 24px; top: 50%; transform: translateY(-50%)`) containing:
1. `#sideSearch` (`data-id="search"`, title="Поиск (/)") -> Search SVG
2. `#mpWallpaper` (`data-id="wallpaper"`, title="Обои") -> Wallpaper SVG
3. `#sideWidgets` (`data-id="widgets"`, title="Виджеты") -> Widgets SVG
4. `#sideTrash` (`data-id="trash"`, title="Корзина") -> Trash SVG
5. `#settingsSideBtn` (`data-id="settings"`, title="Настройки") -> Gear SVG

### 3.5 Overlay Modal System (`.overlay`)
1. `#search-overlay`: Quick Google Search (`Enter` redirect) + Live instant bookmark search with Up/Down keyboard navigation.
2. `#wp-overlay`: Video & image wallpaper upload, presets, opacity slider, reset button.
3. `#widgets-overlay`: Widget status and toggles.
4. `#trash-overlay`: Category cleanup, empty categories auto-clean, and clear actions.
5. `#settingsOverlay`: Comprehensive 5-tab Glassmorphism Settings Modal (`.settings-modal` with tabs: General, Appearance & Glass, Wallpapers & Background, Hotkeys, and About NovaTab). Features live sliding switches (`.st-toggle`), custom sliders (`#stSliderBoardW`, `#stSliderBlur`, `#stSliderAlpha`, `#stSliderOverlay`), direct wallpaper management, and responsive persistence.
6. `#bookmark-overlay`: Add/Edit Bookmark modal with category picker.
7. `#folder-overlay`: Add Category/Board modal with parent folder picker.

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
  3. Bound to `#video-bg` layer (`autoplay`, `loop`, `muted`, `playsinline`).
  4. Photo background layer cleared.
- **Image Uploads (`image/*`):**
  1. Canvas aspect-ratio downscale to max 1920x1080.
  2. WebP 80% compression.
  3. Stored in IndexedDB and cached in storage.
  4. Applied to `#photo-bg.style.backgroundImage`.
- **Customizable Glassmorphism & Dimming:**
  - Live adjustment for `--board-w` (200px to 360px), `--board-blur` (4px to 32px), `--board-alpha` (0.05 to 0.50), and `--overlay-opacity` (0.0 to 0.85).
  - Automatically persisted in `chrome.storage.local` and `localStorage`.

---

## 5. Implementation Verification & Status

- [x] **100% Manifest V3 CSP Compliance:** Zero remote scripts, zero inline scripts/handlers, zero `eval()`.
- [x] **Markmez 1:1 CSS Engine (`style.css`):** `:root` design tokens (`--ui-modal`, `--ui-border`, `--ui-fill`, `--ui-text`, `--board-w`), reusable `.board, .glass-panel`, `#video-bg`, `#photo-bg`, `#bg-overlay`, `.topbar`, `.pages-nav`, `.top-widgets`, `.boards-area`, `.boards-columns`, `.board-column`, `.board`, `.bookmark-row`, `#sidebar`, and `.settings-overlay`.
- [x] **Exact Settings UI Integration (`index.html`):** Fully structured with `<div class="settings-overlay overlay hidden" id="settingsOverlay">` and clean `.settings-modal` containing all 5 navigation tabs (`general`, `appearance`, `wallpapers`, `hotkeys`, `about`), toggles (`showQuote`, `showWidgets`, `openNewTab`), and custom range sliders.
- [x] **Core Engine & Selectors (`app.js`):** Reactive tab switching, toggle synchronization, real-time CSS variable updates for board width and backdrop blur, IndexedDB wallpaper engine, concurrency mutex for bookmark loading, full category and bookmark CRUD, live search with keyboard navigation.
- [x] **Syntax Verification:** Passed `node -c projects/NovaTab/app.js` with 0 errors (exit code 0).
