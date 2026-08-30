# NovaTab — Visual Bookmark Manager Specification
**Version:** 1.4.1 (Bugfixes: Core Bookmark Loader & Mutex, Safe Folder Creation, Russian Quotes Localization & Select Dropdown Theming)  
**Target Platform:** Google Chrome / Chromium-based Browsers (Manifest V3)  
**Design Philosophy:** Ultra-modern, responsive Glassmorphism dashboard with hardware-accelerated backdrop blur, pure Native CSS3 design system, custom high-performance video & image wallpaper engine, and board-based category organization.

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
        | • Active tab metadata capture   |               | • Dynamic Quotes Module         |
        | • Automatic target folder seed  |               | • Video Wallpapers & Overlay    |
        | • Action badge visual feedback  |               | • Top Board Pills Drag-and-Drop |
        +---------------------------------+               | • Responsive Masonry Cards Grid |
                                                          | • Native IndexedDB (NovaTabDB)  |
                                                          | • Right Floating Tool Dock      |
                                                          | • Live Search Palette (Up/Down) |
                                                          | • Real-time Chrome Event Sync   |
                                                          | • Concurrency Mutex Lock        |
                                                          | • Safe Parent Hierarchy Select  |
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
| `.bg-video-layer` | `position: fixed; inset: 0; width: 100vw; height: 100vh; object-fit: cover; z-index: -2; pointer-events: none;` | Background HTML5 video layer for high-performance looping video wallpapers. |
| `.bg-overlay-layer` | `position: fixed; inset: 0; width: 100vw; height: 100vh; z-index: -1; pointer-events: none; background-color: rgba(0, 0, 0, var(--overlay-opacity, 0.30)); transition: background-color var(--transition-fast);` | Adjustable dimming layer ensuring Glassmorphism readability over bright backgrounds. |
| `.quote-container` | `display: flex; flex-direction: column; align-items: center; justify-content: center; text-align: center; margin: 0 auto 26px auto; max-width: 820px; padding: 12px 24px; border-radius: var(--radius-lg); cursor: pointer;` | Centered dynamic inspirational quote capsule. |
| `.glass-panel` | `background: var(--glass-bg-panel); backdrop-filter: blur(16px); -webkit-backdrop-filter: blur(16px); border: 1px solid var(--glass-border);` | Floating toolbar dock, top navbar capsule, modal backgrounds. |
| `.glass-card` | `border-radius: var(--radius-lg); background: var(--glass-bg-card); backdrop-filter: blur(16px); border: 1px solid var(--glass-border); padding: 16px;` with hover lift `-2px` and purple ambient glow. | Category/folder bookmark cards in the masonry grid. |
| `.glass-pill` | `background: var(--glass-bg-pill); backdrop-filter: blur(12px); -webkit-backdrop-filter: blur(12px); border: 1px solid var(--glass-border-subtle); border-radius: 9999px;` | Top nav board tabs, filter chips, secondary buttons. |
| `.glass-pill.dragging` | `opacity: 0.4; transform: scale(0.95); border-style: dashed;` | Visual feedback for category pill being dragged. |
| `.glass-pill.drag-over` | `border-color: #8b5cf6 !important; box-shadow: 0 0 12px rgba(139, 92, 246, 0.6) !important; transform: scale(1.05);` | Visual drop indicator for category pill target. |
| `.glass-pill-active` | `background: linear-gradient(135deg, rgba(139, 92, 246, 0.75), rgba(99, 102, 241, 0.75)); backdrop-filter: blur(12px); border: 1px solid var(--glass-border-active); box-shadow: 0 0 15px rgba(139, 92, 246, 0.35);` | Currently selected board/filter pill and primary buttons. |
| `select, .form-select` | `background-color: #1A1D29 !important; color: #FFFFFF !important; border: 1px solid rgba(255,255,255,0.15) !important; color-scheme: dark !important;` with dark option items (`#161922`). | High-contrast dark select menus with zero white edge artifacts. |
| `.cards-masonry-grid` | `display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 20px; align-items: start;` | Dynamic responsive bookmark cards layout. |
| `.floating-toolbar` | `position: fixed; right: 24px; top: 50%; transform: translateY(-50%); z-index: 50; border-radius: 9999px; padding: 12px 8px;` | Vertical quick-action dock on the right viewport edge. |
| `.floating-bg-btn` | `position: fixed; bottom: 24px; left: 24px; z-index: 50; width: 48px; height: 48px; border-radius: 50%; display: flex; align-items: center; justify-content: center;` | Bottom-left instant wallpaper changer button. |
| `.modal-overlay` & `.modal-box` | Fullscreen frosted overlay with scale-animated glassmorphic modal box and responsive sizes (`.modal-box-wide`). | Search palette, bookmark CRUD, folder creator, and settings dialogs. |
| `.glass-slider` | Custom gradient-thumb range slider for background dimming and transparency adjustment. | Settings modal overlay opacity control. |
| `.custom-scrollbar` | `scrollbar-width: thin; scrollbar-color: rgba(255, 255, 255, 0.2) transparent;` | Minimal unobtrusive scrollbar for viewport and search results. |

---

## 3. Dynamic Quotes Module (Strict Russian Localization)

Positioned centered at the top of the main scrollable viewport above the category cards grid, the Quotes Module provides inspiring, philosophical, dark academia, and lo-fi quotes translated into Russian:

- **Curated Russian Quote Bank:**
  1. *«Вы живете только один раз, но если вы все сделаете правильно, одного раза достаточно.»* — Мэй Уэст
  2. *«Чем тише ты становишься, тем больше начинаешь слышать.»* — Рам Дасс
  3. *«Кораблю безопаснее в порту, но он не для того строился.»* — Грейс Хоппер
  4. *«Никогда не поздно стать тем, кем ты мог бы быть.»* — Джордж Элиот
  5. *«Простота — это душа эффективности.»* — Остин Фриман
  6. *«Секрет того, чтобы двигаться вперед — это начать.»* — Марк Твен
  7. *«Не тратьте время на споры о том, каким должен быть хороший человек. Будьте им.»* — Марк Аврелий
  8. *«Мутная вода лучше всего очищается, если оставить ее в покое.»* — Алан Уоттс
  9. *«Мы чаще страдаем в воображении, чем в реальности.»* — Сенека
  10. *«Все, что ты можешь вообразить — реально.»* — Пабло Пикассо
  11. *«Оставайтесь голодными, оставайтесь безрассудными.»* — Стив Джобс
  12. *«Делай, что можешь, с тем, что имеешь, там, где ты есть.»* — Теодор Рузвельт
- **Interaction & Animation:** On initial load (`DOMContentLoaded` / `init`), a random quote is rendered. Clicking anywhere on the `.quote-container` smoothly rolls another random quote (with anti-repetition guarantee) using subtle CSS fade and translateY animations.

---

## 4. Lively Video Wallpapers & Native IndexedDB Storage Engine

To support large video files and high-resolution images without quota restrictions, NovaTab implements a native IndexedDB storage engine (`WallpaperDB`):

### 4.1 Database Architecture (`WallpaperDB`)
- **Database Name:** `NovaTabDB` (Version `1`)
- **Object Store:** `wallpapers`
- **Key:** `'activeWallpaper'`
- **Record Schema:** `{ blob: Blob/File, type: 'video' | 'image', name: string, updatedAt: number }`
- **Methods:**
  - `init()`: Opens IndexedDB connection with auto-upgraded schema.
  - `save(blob, type, name)`: Asynchronously stores media blob.
  - `get()`: Retrieves active wallpaper record.
  - `clear()`: Deletes stored wallpaper and releases storage.

### 4.2 Wallpaper Processing Pipeline
- **Video Uploads (`video/mp4`, `video/webm`, `video/ogg`):**
  1. Stored directly as a binary `Blob`/`File` in `NovaTabDB`.
  2. Object URL generated via `URL.createObjectURL(blob)`.
  3. Bound to `#bg-video` layer (`autoplay`, `loop`, `muted`, `playsinline`), unhidden and played.
  4. Static background reset (`document.body.style.backgroundImage = 'none'`).
  5. Storage metadata updated (`{ wallpaperType: 'video' }`).
- **Image Uploads (`image/*`):**
  1. Canvas aspect-ratio downscale to max 1920x1080.
  2. WebP 80% compression (fallback JPEG 85%).
  3. Stored in IndexedDB and cached in `chrome.storage.local`.
  4. Video stopped, hidden, and previous object URLs revoked via `URL.revokeObjectURL()`.
  5. Applied to `document.body.style.backgroundImage`.
- **Adjustable Dimming Overlay & Glassmorphism Slider:**
  - `#bg-overlay` layer with `--overlay-opacity` CSS Custom Property (default `0.30`, range `0`–`0.85`).
  - Real-time interactive slider in Settings modal updates dimming and persists preference in storage.
- **Reset Mechanism:**
  - Clears `WallpaperDB`, stops/hides video, revokes object URLs, removes background image, and restores default cyberpunk aurora neon mesh gradient.

---

## 5. Drag-and-Drop Category Sorting

Users can reorder category board tabs directly in the top navigation bar via intuitive HTML5 drag-and-drop:

- **Draggable Elements:** All custom category folder pills (excluding the master "✦ Все доски" pill) are marked `draggable="true"`.
- **Drag Events:**
  - `dragstart`: Captures `state.draggedFolderId` and sets `dataTransfer.setData('text/plain', folder.id)`, applying `.dragging` class (dashed border, `0.4` opacity).
  - `dragover`: Prevents default, sets `dropEffect = 'move'`, and applies `.drag-over` class (purple glow highlight, `scale(1.05)`).
  - `dragleave`: Cleans up `.drag-over` styling.
  - `dragend`: Resets drag state and clears styling from all pills.
  - `drop`: Calculates dragged folder ID and drop target folder ID.
    - **Extension Mode:** Identifies target folder index in parent bookmark container and reorders via `chrome.bookmarks.move(draggedId, { parentId, index: targetIndex })`.
    - **Standalone / Mock Mode:** Reorders in `state.allFolders` and `MOCK_BOOKMARK_TREE`.
    - Automatically refreshes the UI via `loadBookmarks()` with toast feedback (`Порядок досок обновлен!`).

---

## 6. Concurrency Mutex & Folder Hierarchy Management

### 6.1 Mutex Reload Lock (`isLoadingBookmarks` & `pendingBookmarkReload`)
When user actions (e.g. deleting or moving bookmarks/folders) trigger an immediate `await loadBookmarks()` while Chrome simultaneously fires reactive lifecycle listeners (`chrome.bookmarks.onRemoved`, `chrome.bookmarks.onMoved`), the mutex ensures:
1. Only ONE asynchronous bookmarks tree traversal executes at any time.
2. Interleaved calls set `pendingBookmarkReload = true` and return immediately.
3. Upon completion of the active reload, the lock invokes the pending reload once, guaranteeing consistent state.

### 6.2 Atomic State Swapping & Multi-Level Folder Parsing
- `parseBookmarkNodes(nodes, parentPath, parentId, depth, collections)` recursively builds temporary local collections in memory:
  - `tempAllBookmarks` & `tempAllFolders`
  - `tempFolderMap` & `tempBookmarksByFolder`
- Atomically applies parsed collections to `state`.
- Recursively tracks folder tree depths to provide clean visual indentations (`'— '.repeat(folder.depth)`) in `<select>` dropdowns.

### 6.3 Safe Parent Folder Fallback for Modal Forms
- `populateFolderSelectDropdowns()` cleans and populates both `folderParentSelect` and `modalBookmarkFolder`, filtering out virtual root wrapper node (`id: '0'`).
- Sets default selection to `'1'` (Панель закладок) or the first available folder ID.
- `handleFolderFormSubmit()` validates `parentId` existence in `state.folderMap`, safely falling back to `'1'` before calling `chrome.bookmarks.create({ parentId, title })` to prevent `Error: Can't find parent bookmark for id`.

---

## 7. Implementation Checklist & Status

- [x] **100% Manifest V3 CSP Compliance:** Removed Tailwind CDN, zero inline handlers, zero remote dependencies, zero `eval()`, pure native event binding.
- [x] **Pure Native CSS3 Refactor (`style.css`):** Comprehensive CSS variables, glassmorphism design tokens (`.glass-panel`, `.glass-card`, `.glass-pill`, `.glass-pill-active`), responsive grid, modals, and toolbars.
- [x] **Select & Option Styling Fix (`style.css`):** Dark select boxes with `#1A1D29` / `#161922`, `color-scheme: dark`, purple focus rings, and zero white strips.
- [x] **Russian Literary Quotes Module (`index.html`, `style.css`, `app.js`):** 12 curated Russian translations of philosophical quotes, random roll on load and click, soft text shadow.
- [x] **Lively Video Wallpapers & Native IndexedDB Engine (`app.js`, `style.css`, `index.html`):** Native `NovaTabDB` IndexedDB store for video/image blobs, HTML5 video background layer, adjustable dimming overlay with Settings slider (`--overlay-opacity`).
- [x] **Drag-and-Drop Category Sorting (`app.js`, `style.css`):** HTML5 drag-and-drop on category pills with `chrome.bookmarks.move` integration and mock tree reordering.
- [x] **Concurrency Mutex & Core Loader (`app.js`):** Fully implemented `loadBookmarks()` and `parseBookmarkNodes()`, protected by `isLoadingBookmarks` & `pendingBookmarkReload` mutex lock.
- [x] **Safe Folder Creation & Parent Dropdown Validation (`app.js`):** Dynamic hierarchy population, root ID exclusion, and fallback to `'1'` to prevent `Can't find parent bookmark for id` errors.
- [x] **Root Container Clearing Capability (`app.js`):** Implemented `.btn-clear-card` and `clearCategoryBookmarks` to safely clear bookmarks from system/root categories via `chrome.bookmarks.remove(bm.id)`.
- [x] **Streamlined Hover-Only Bookmark Actions & Delete Feedback:** Streamlined bookmark row actions exclusively to Edit and Delete with red hover glow on `.item-action-btn.btn-delete:hover`.
- [x] **Interactive Controls & Hotkeys:** Global `/` search shortcut, search palette keyboard navigation (`↑`, `↓`, `Enter`, `Escape`), board filtering, bookmark CRUD, view mode toggle.
