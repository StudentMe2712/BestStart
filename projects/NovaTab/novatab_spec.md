# NovaTab — Visual Bookmark Manager Specification
**Version:** 1.2.0 (100% Manifest V3 CSP Compliant & Native CSS3 Architecture)  
**Target Platform:** Google Chrome / Chromium-based Browsers (Manifest V3)  
**Design Philosophy:** Ultra-modern, responsive Glassmorphism dashboard with hardware-accelerated backdrop blur, pure Native CSS3 design system, custom high-performance wallpaper engine, and board-based category organization.

---

## 1. Architectural Overview & Manifest V3 CSP Compliance

NovaTab overrides the default browser "New Tab" page (`chrome_url_overrides: { "newtab": "index.html" }`), transforming it into an aesthetic, distraction-free visual hub with layered glass surfaces and reactive bookmarks management.

### 1.1 Manifest V3 Content Security Policy (CSP) Architecture

NovaTab is strictly engineered to comply with Chrome Manifest V3 Content Security Policy rules:

| CSP Rule | NovaTab Implementation & Compliance Strategy |
| :--- | :--- |
| **Zero Remote Scripts** | All external CDNs (including `cdn.tailwindcss.com`) are completely eliminated. The extension operates 100% offline with zero external script fetching. |
| **Zero Inline Scripts** | All `<script>...</script>` tags in `index.html` were removed. The single entry point script is `<script src="app.js"></script>` loaded at the bottom of `<body>`. |
| **Zero Inline Event Handlers** | No `onclick`, `onchange`, `onerror`, or other inline event handler attributes exist in `index.html` or dynamically generated DOM strings. All interactions use `addEventListener` or event delegation. |
| **Zero Dynamic Code Evaluation** | Strictly NO `eval()`, NO `new Function()`, and NO string-evaluated timers (`setTimeout("...", ms)`). All asynchronous timers execute native callback closures. |
| **Zero External Fonts / Stylesheets** | `@import` of Google Fonts was replaced with native modern system font stacks (`system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, ...`) for instantaneous render with zero network delay. |

```
+---------------------------------------------------------------------------------------------------+
|                                        NovaTab Extension (Mv3)                                    |
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
        | • Global shortcut Ctrl+Shift+Y  |               | • 100% Native CSS3 Layout       |
        | • Active tab metadata capture   |               | • Top Board Pills Navigation    |
        | • Automatic target folder seed  |               | • Responsive Masonry Cards Grid |
        | • Action badge visual feedback  |               | • Canvas WebP Wallpaper Engine  |
        +---------------------------------+               | • Right Floating Tool Dock      |
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

NovaTab employs a multi-tiered glassmorphism visual hierarchy powered by standard CSS `backdrop-filter: blur(...)` and alpha-channel RGBA borders, defined cleanly via CSS Custom Properties in `:root`.

### 2.1 CSS Utility Tokens (`style.css`)

| Class | Properties & Aesthetics | Use Case |
| :--- | :--- | :--- |
| `body` | `width: 100vw; height: 100vh; overflow: hidden; margin: 0; padding: 0; background-size: cover; background-position: center; background-repeat: no-repeat; background-attachment: fixed;` with default cyberpunk aurora mesh gradient. | Base canvas for wallpaper and gradient rendering. |
| `.glass-panel` | `background: var(--glass-bg-panel); backdrop-filter: blur(16px); -webkit-backdrop-filter: blur(16px); border: 1px solid var(--glass-border);` | Floating toolbar dock, top navbar capsule, modal backgrounds. |
| `.glass-card` | `border-radius: var(--radius-lg); background: var(--glass-bg-card); backdrop-filter: blur(16px); border: 1px solid var(--glass-border); padding: 16px;` with hover lift `-2px` and purple ambient glow. | Category/folder bookmark cards in the masonry grid. |
| `.glass-pill` | `background: var(--glass-bg-pill); backdrop-filter: blur(12px); -webkit-backdrop-filter: blur(12px); border: 1px solid var(--glass-border-subtle); border-radius: 9999px;` | Top nav board tabs, filter chips, secondary buttons. |
| `.glass-pill-active` | `background: linear-gradient(135deg, rgba(139, 92, 246, 0.75), rgba(99, 102, 241, 0.75)); backdrop-filter: blur(12px); border: 1px solid var(--glass-border-active); box-shadow: 0 0 15px rgba(139, 92, 246, 0.35);` | Currently selected board/filter pill and primary buttons. |
| `.cards-masonry-grid` | `display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 20px; align-items: start;` | Dynamic responsive bookmark cards layout. |
| `.floating-toolbar` | `position: fixed; right: 24px; top: 50%; transform: translateY(-50%); z-index: 50; border-radius: 9999px; padding: 12px 8px;` | Vertical quick-action dock on the right viewport edge. |
| `.floating-bg-btn` | `position: fixed; bottom: 24px; left: 24px; z-index: 50; width: 48px; height: 48px; border-radius: 50%; display: flex; align-items: center; justify-content: center;` | Bottom-left instant wallpaper changer button. |
| `.modal-overlay` & `.modal-box` | Fullscreen frosted overlay with scale-animated glassmorphic modal box and responsive sizes (`.modal-box-wide`). | Search palette, bookmark CRUD, folder creator, and settings dialogs. |
| `.custom-scrollbar` | `scrollbar-width: thin; scrollbar-color: rgba(255, 255, 255, 0.2) transparent;` | Minimal unobtrusive scrollbar for viewport and search results. |

---

## 3. Custom Background Processing Pipeline

To ensure instantaneous loading without hitting `chrome.storage.local` storage quota limits (5MB–10MB), custom user wallpapers are processed through an in-memory client-side compression pipeline:

```
[ User Selects Image ]
          │
          ▼
[ FileReader (readAsDataURL) ]
          │
          ▼
[ Image() Instance Loaded ]
          │
          ▼
[ Aspect-Ratio Preserving Downscale ]
  Max Width: 1920px, Max Height: 1080px
          │
          ▼
[ HTML5 2D Canvas Rasterization ]
          │
          ▼
[ Canvas Compression ]
  canvas.toDataURL('image/webp', 0.8)
  Fallback: canvas.toDataURL('image/jpeg', 0.85)
          │
          ▼
[ chrome.storage.local.set({ customBackground: dataUrl }) ]
  (Fallback to localStorage in standalone web mode)
          │
          ▼
[ Direct DOM Update: document.body.style.backgroundImage = 'url(...)']
```

### Reset Mechanism
- Right-clicking (`contextmenu`) on the `#bg-change-btn` or selecting "Сбросить градиент" in Settings clears the custom background key from storage and restores the default cyber-neon mesh gradient.

---

## 4. UI Layout & Component Architecture

### 4.1 Top Nav (Boards / Tabs)
- Centered at the top: `.top-nav-container.glass-panel`.
- Contains "✦ Все доски" master pill plus dynamic pills for top bookmark folders (e.g. `📺 Стриминг`, `🎮 Гейминг`, `💻 Разработка`, `🤖 AI Platforms`).
- "+ Новая доска" quick creator button to instantiate new bookmark categories.

### 4.2 Main Viewport & Category Cards
- `height: calc(100vh - 78px); margin-top: 78px; overflow-y: auto; padding: 24px 80px 48px 40px;`
- Responsive dynamic columns (`.cards-masonry-grid` / `.cards-masonry-grid.list-layout`).
- Inside each `.glass-card`:
  - **Header:** Folder title, item counter badge, and quick "+" add bookmark button.
  - **Bookmark Rows:** 16px Favicon (with Google S2 & letter avatar fallback), title, domain label, and hover actions dock (open, copy link, edit, delete).

### 4.3 Right Floating Toolbar Dock
- Fixed vertical capsule at `right: 24px, top: 50%`.
- Glass buttons with hover tooltips:
  1. **Search (`/`):** Launches the fuzzy search palette modal.
  2. **Add (`+`):** Opens bookmark creation modal.
  3. **Folder (`📁`):** Opens folder / board creation modal.
  4. **View Toggle (`🔲`):** Switches between multi-column grid and single-column list.
  5. **Random Bookmark (`⚡`):** Picks and opens a random bookmark from the library.
  6. **Settings (`⚙️`):** Opens the NovaTab settings modal (wallpaper manager, statistics).

### 4.4 Live Search Palette Modal
- Triggered globally via `/` key or toolbar search icon.
- Real-time search across titles, URLs, domains, and folder names.
- Full keyboard navigation:
  - `↑` / `↓` Arrow keys to navigate matches with active highlight.
  - `Enter` to open selected bookmark URL immediately.
  - `Escape` to close palette.

---

## 5. Favicon Resolution Strategy

1. **Tier 1 (Chrome Mv3 Context):**
   `chrome-extension://${chrome.runtime.id}/_favicon/?pageUrl=${encodeURIComponent(url)}&size=16`
2. **Tier 2 (Google S2 Favicon API):**
   `https://www.google.com/s2/favicons?domain=${encodeURIComponent(hostname)}&sz=32`
3. **Tier 3 (Client Fallback):**
   Generated letter avatar with consistent domain-hashed color palette (`hsl(hue, 65%, 50%)`).

---

## 6. Implementation Checklist & Status

- [x] **100% Manifest V3 CSP Compliance:** Removed Tailwind CDN and inline configuration, removed inline handlers, zero remote dependencies, zero `eval()`, pure native event binding.
- [x] **Pure Native CSS3 Refactor (`style.css`):** Comprehensive CSS variables, glassmorphism design tokens (`.glass-panel`, `.glass-card`, `.glass-pill`, `.glass-pill-active`), responsive grid, modals, and toolbars.
- [x] **Semantic HTML Architecture (`index.html`):** Pristine `<head>` with only required meta/title/link elements, semantic classes, and single bottom script tag.
- [x] **Wallpaper Engine & Compression (`app.js`):** Canvas downscaling to 1920x1080, WebP 80% compression, `chrome.storage.local` persistence, right-click reset.
- [x] **Dynamic Chrome Bookmarks Parser (`app.js`):** Tree crawling, folder grouping into cards, rich standalone mock data fallback, live reactive listeners (`onCreated`, `onRemoved`, `onChanged`, `onMoved`).
- [x] **Interactive Controls & Hotkeys:** Global `/` search shortcut, search palette keyboard navigation (`↑`, `↓`, `Enter`, `Escape`), board filtering, bookmark CRUD, view mode toggle.
