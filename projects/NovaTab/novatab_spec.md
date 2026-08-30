# NovaTab — Visual Bookmark Manager Specification
**Version:** 1.1.0 (Glassmorphism & Custom Background Architecture)  
**Target Platform:** Google Chrome / Chromium-based Browsers (Manifest V3)  
**Design Philosophy:** Ultra-modern, responsive Glassmorphism dashboard with hardware-accelerated backdrop blur, custom high-performance wallpaper engine, and board-based category organization.

---

## 1. Architectural Overview

NovaTab overrides the default browser "New Tab" page (`chrome_url_overrides: { "newtab": "index.html" }`), transforming it into an aesthetic, distraction-free visual hub with layered glass surfaces and reactive bookmarks management.

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
        | • Global shortcut Ctrl+Shift+Y  |               | • Top Board Pills Navigation    |
        | • Active tab metadata capture   |               | • Responsive Category Cards     |
        | • Automatic target folder seed  |               | • Custom Canvas WebP Engine     |
        | • Action badge visual feedback  |               | • Right Floating Tool Dock      |
        +---------------------------------+               | • Instant Live Search Modal     |
                                                          | • Real-time Chrome Event Sync   |
                                                          +---------------------------------+
                                                                           |
                                                                           v
                                                          +---------------------------------+
                                                          |  Chrome Storage & Bookmarks API |
                                                          +---------------------------------+
```

---

## 2. Visual System & Glassmorphism Design Tokens

NovaTab employs a multi-tiered glassmorphism visual hierarchy powered by standard CSS `backdrop-filter: blur(...)` and alpha-channel RGBA borders.

### 2.1 CSS Utility Tokens (`style.css`)

| Class | Properties & Aesthetics | Use Case |
| :--- | :--- | :--- |
| `body` | `width: 100vw; height: 100vh; overflow: hidden; margin: 0; padding: 0; background-size: cover; background-position: center; background-repeat: no-repeat; background-attachment: fixed;` with default cyberpunk aurora mesh gradient. | Base canvas for wallpaper and gradient rendering. |
| `.glass-pill` | `background: rgba(255, 255, 255, 0.1); backdrop-filter: blur(12px); -webkit-backdrop-filter: blur(12px); border: 1px solid rgba(255, 255, 255, 0.08); border-radius: 9999px;` | Top nav board tabs, modal action buttons, filter chips. |
| `.glass-pill-active` | `background: linear-gradient(135deg, rgba(139, 92, 246, 0.65), rgba(99, 102, 241, 0.65)); backdrop-filter: blur(12px); border: 1px solid rgba(167, 139, 250, 0.35); box-shadow: 0 0 15px rgba(139, 92, 246, 0.3);` | Currently selected board/filter pill. |
| `.glass-panel` | `background: rgba(0, 0, 0, 0.4); backdrop-filter: blur(16px); -webkit-backdrop-filter: blur(16px); border: 1px solid rgba(255, 255, 255, 0.1);` | Floating toolbar dock, top navbar capsule, modal backgrounds. |
| `.glass-card` | `border-radius: 16px; background: rgba(0, 0, 0, 0.42); backdrop-filter: blur(15px); -webkit-backdrop-filter: blur(15px); border: 1px solid rgba(255, 255, 255, 0.1); padding: 18px;` with hover lift `-2px` and purple ambient glow. | Category/folder bookmark cards in the masonry grid. |
| `.floating-toolbar` | `position: fixed; right: 24px; top: 50%; transform: translateY(-50%); z-index: 50; border-radius: 9999px; padding: 12px 8px;` | Vertical quick-action dock on the right viewport edge. |
| `.floating-bg-btn` | `position: fixed; bottom: 24px; left: 24px; z-index: 50; width: 48px; height: 48px; border-radius: 50%; display: flex; align-items: center; justify-content: center;` | Bottom-left instant wallpaper changer button. |
| `.custom-scrollbar` | `scrollbar-width: thin; scrollbar-color: rgba(255, 255, 255, 0.2) transparent;` | Minimal unobtrusive scrollbar for viewport and lists. |

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
- Right-clicking (contextmenu) on the `#bg-change-btn` or selecting "Reset to Default" clears the custom background key from storage and restores the default cyber-neon mesh gradient.

---

## 4. UI Layout & Component Architecture

### 4.1 Top Nav (Boards / Tabs)
- Centered at the top: `fixed top-5 left-1/2 -translate-x-1/2`.
- Contains "✦ Все доски" master pill plus dynamic pills for top bookmark folders (e.g. `📺 Стриминг`, `🎮 Гейминг`, `💻 Разработка`, `🤖 AI Platforms`).
- "+ Новая доска" quick creator button to instantiate new bookmark categories.

### 4.2 Main Viewport & Category Cards
- `height: calc(100vh - 80px); margin-top: 80px; overflow-y: auto; padding: 24px 80px 40px 40px;`
- Responsive dynamic columns (`.cards-masonry-grid` / `.cards-masonry-grid.list-layout`).
- Inside each `.glass-card`:
  - **Header:** Folder title, item counter badge, and quick "+" add bookmark button.
  - **Bookmark Rows:** 16px Favicon (with Google S2 & letter avatar fallback), title, domain label, and hover actions dock (open, copy link, edit, delete).

### 4.3 Right Floating Toolbar Dock
- Fixed vertical capsule at `right: 24px, top: 50%`.
- Glass buttons with hover tooltips:
  1. **Search (`/`):** Launches the fuzzy search modal.
  2. **Add (`+`):** Opens bookmark creation modal.
  3. **Folder (`📁`):** Opens folder / board creation modal.
  4. **View Toggle (`🔲`):** Switches between multi-column grid and single-column list.
  5. **Random Bookmark (`⚡`):** Picks and opens a random bookmark from the library.
  6. **Settings (`⚙️`):** Opens the NovaTab settings modal (wallpaper manager, statistics).

### 4.4 Live Search Modal
- Triggered globally via `/` key or toolbar search icon.
- Real-time search across titles, URLs, domains, and folder names.
- Instant keyboard navigation and click-to-open.

---

## 5. Favicon Resolution Strategy

1. **Tier 1 (Chrome Mv3 Context):**
   `chrome-extension://${chrome.runtime.id}/_favicon/?pageUrl=${encodeURIComponent(url)}&size=16`
2. **Tier 2 (Google S2 Favicon API):**
   `https://www.google.com/s2/favicons?domain=${encodeURIComponent(hostname)}&sz=32`
3. **Tier 3 (Client Fallback):**
   Generated circular letter avatar with consistent domain-hashed color palette (`hsl(hue, 65%, 50%)`).

---

## 6. Implementation Checklist & Status

- [x] **Glassmorphism CSS Architecture (`style.css`):** Full implementation of `.glass-pill`, `.glass-pill-active`, `.glass-panel`, `.glass-card`, `.floating-toolbar`, `.floating-bg-btn`, custom scrollbars, and default cyberpunk gradient.
- [x] **Modern SPA Layout (`index.html`):** Tailwind CSS integration, centered top board pills, category cards grid, right floating toolbar, left bottom wallpaper button, search modal, CRUD modals, and toast notifications.
- [x] **Wallpaper Engine & Compression (`app.js`):** Canvas downscaling to 1920x1080, WebP 80% compression, `chrome.storage.local` persistence, right-click reset.
- [x] **Dynamic Chrome Bookmarks Parser (`app.js`):** Tree crawling, folder grouping into cards, rich standalone mock data fallback, live reactive listeners (`onCreated`, `onRemoved`, `onChanged`, `onMoved`).
- [x] **Interactive Controls & Hotkeys:** Global `/` search shortcut, Escape key modal dismiss, board switching, bookmark CRUD, view mode toggle.
