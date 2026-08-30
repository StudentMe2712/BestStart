# NovaTab — Visual Bookmark Manager Specification
**Version:** 1.0.0 (Stage 1 Core MVP)  
**Target Platform:** Google Chrome / Chromium-based Browsers (Manifest V3)  
**Design Philosophy:** Visual, aesthetic, and lightning-fast bookmark dashboard inspired by Lumi List & modern desktop productivity spaces.

---

## 1. Architectural Overview

NovaTab replaces the default browser "New Tab" page (`chrome_url_overrides: { "newtab": "index.html" }`) with a clean, high-performance visual dashboard for organizing, searching, and managing web bookmarks.

```
+---------------------------------------------------------------------------------------+
|                                    NovaTab Extension                                  |
+---------------------------------------------------------------------------------------+
|  Manifest V3 Configuration (manifest.json)                                            |
|  - permissions: ["bookmarks", "storage", "tabs", "favicon"]                           |
|  - chrome_url_overrides: { "newtab": "index.html" }                                   |
|  - background: { "service_worker": "background.js" }                                  |
|  - commands: { "save-current-tab": "Ctrl+Shift+Y" / "Command+Shift+Y" }               |
+---------------------------------------------------------------------------------------+
|                                           |                                           |
|                                           v                                           |
|  +------------------------------------+       +------------------------------------+  |
|  |     Background Service Worker      |       |      Newtab SPA (Single Page App)  |  |
|  |        (background.js)             |       |        (index.html / app.js)       |  |
|  |------------------------------------|       |------------------------------------|  |
|  | • Global shortcut handler          |       | • Real-time Bookmarks Parser       |  |
|  | • Active tab detector              |       | • Dynamic Folder Tree Navigator    |  |
|  | • Default folder resolver          |       | • Instant Fuzzy Search Engine      |  |
|  | • Quick bookmark creation          |       | • Multi-level Grid / List Views    |  |
|  | • Action badge & feedback toast    |       | • Favicon Cache & Fallback Engine  |  |
|  | • Initial storage configuration    |       | • CRUD Modal & Action Controllers  |  |
|  +------------------------------------+       +------------------------------------+  |
|                                           |                                           |
|                                           v                                           |
|  +---------------------------------------------------------------------------------+  |
|  |                              Chrome Extensions Platform API                     |  |
|  |  chrome.bookmarks  |  chrome.storage.local  |  chrome.tabs  |  chrome.commands  |  |
|  +---------------------------------------------------------------------------------+  |
+---------------------------------------------------------------------------------------+
```

---

## 2. Component Architecture

### 2.1 Background Service Worker (`background.js`)
- **Lifecycle & Setup:** Listens to `chrome.runtime.onInstalled` to seed initial user preferences (`theme: 'dark'`, `viewMode: 'grid'`, `sortBy: 'dateAdded-desc'`, `defaultFolder: 'NovaTab'`).
- **Global Command Controller:** Intercepts `chrome.commands.onCommand` (`save-current-tab`).
- **Active Tab Resolution:** Queries active tab via `chrome.tabs.query({ active: true, currentWindow: true })`.
- **URL Sanity Filter:** Excludes internal/restricted URLs (`chrome://`, `edge://`, `chrome-extension://`, `about:`, `view-source:`).
- **Target Folder Manager:** Locates or automatically creates a dedicated "NovaTab" folder in the Bookmarks Bar or Other Bookmarks hierarchy.
- **Visual Feedback:** Updates extension action badge (`chrome.action.setBadgeText({ text: '✓' })`, `#6366F1` background, auto-clearing after 2.0s).

### 2.2 NewTab SPA Viewport (`index.html` & `style.css`)
- **Design System:** Deep slate & obsidian theme (`#0F1117` base, `#161922` sidebar, `#1E2230` card surfaces, `#2A3045` borders, `#6366F1` Indigo & `#8B5CF6` Purple accents).
- **Responsive Layout:** Fixed/collapsible left sidebar, sticky top search/filter header, responsive auto-fill bookmark card grid.
- **Card Aesthetics:** 
  - Domain badge (e.g. `github.com`, `figma.com`).
  - High-res favicon with multi-tiered fallback.
  - Truncated multi-line title and descriptive URL preview.
  - Hover action dock: Open in new tab, Copy link, Edit modal, Delete with confirmation.
  - Relative / formatted date indicator.

### 2.3 Real-Time Bookmark Parser & Sync Engine (`app.js`)
- **Tree Crawler:** Fetches hierarchy via `chrome.bookmarks.getTree()` and builds an indexed flat representation with bidirectional parent-child references.
- **Standalone Development Fallback:** Detects if running outside extension sandbox (`chrome?.bookmarks`); auto-populates a rich mock bookmark tree for local debugging and browser preview.
- **Live Event Reactor:** Automatically listens to `chrome.bookmarks.onCreated`, `chrome.bookmarks.onRemoved`, `chrome.bookmarks.onChanged`, and `chrome.bookmarks.onMoved` to synchronize the UI without page reloads.

### 2.4 Favicon Subsystem
- **Primary Source:** Chrome Mv3 Favicon API:
  `chrome-extension://${chrome.runtime.id}/_favicon/?pageUrl=${encodeURIComponent(url)}&size=32`
- **Secondary Source:** Google S2 Favicon service fallback:
  `https://www.google.com/s2/favicons?domain=${hostname}&sz=64`
- **Tertiary Source:** In-memory SVG Letter Avatar generator styled with domain-hashed color gradients.

---

## 3. Stage 1 Execution Checklist

- [x] **Project Directory & Asset Structure:** Initialized `projects/NovaTab` with `icons/` folder and generated PNG icons (16, 32, 48, 128px) plus `icon.svg`.
- [x] **Architecture Specification:** Created comprehensive `novatab_spec.md` with complete technical blueprint.
- [x] **Manifest V3 Configuration:** Created `manifest.json` with permissions (`bookmarks`, `storage`, `tabs`, `favicon`), newtab override, service worker, commands, and icons.
- [x] **Background Service Worker:** Implemented `background.js` with global `save-current-tab` hotkey listener (`Ctrl+Shift+Y` / `Cmd+Shift+Y`), URL filter, automatic folder creation, and action badge feedback.
- [x] **Modern SPA Interface:** Implemented `index.html` featuring sidebar navigation ("All Bookmarks", "Recent", "Browser Folders", stats counter), top action bar, breadcrumbs, search bar, and edit/add modal.
- [x] **Dark Aesthetic Theme:** Implemented `style.css` with dark palette, custom scrollbars, card hover lift effects, and responsive utilities.
- [x] **Application Controller:** Implemented `app.js` with tree traversal, search filter, view toggle, folder navigation, CRUD operations, standalone mock mode, and real-time Chrome bookmarks event listeners.

---

## 4. Data Structures & API Contracts

### Bookmark Node Hierarchy
```typescript
interface NovaBookmarkItem {
  id: string;
  parentId?: string;
  title: string;
  url?: string;
  dateAdded?: number;
  dateGroupModified?: number;
  children?: NovaBookmarkItem[];
  // Computed / UI properties
  hostname?: string;
  folderPath?: string[];
  isFolder: boolean;
}
```

### Chrome API Integration Mapping

| API Method | Trigger / Purpose | Expected Behavior |
| :--- | :--- | :--- |
| `chrome.bookmarks.getTree()` | App init & full sync | Traverses bookmark tree root downwards. |
| `chrome.bookmarks.getRecent(n)` | "Recent" tab view | Fetches latest $n$ created bookmarks across all folders. |
| `chrome.bookmarks.create(bookmark)` | Add modal & Hotkey | Inserts new bookmark into target folder. |
| `chrome.bookmarks.update(id, changes)` | Edit modal | Updates title / URL of existing bookmark. |
| `chrome.bookmarks.remove(id)` | Delete action | Removes bookmark from browser database. |
| `chrome.storage.local.get/set` | Preference persistence | Stores user view mode, sort preferences, and active theme. |
| `chrome.commands.onCommand` | Keyboard shortcut | Triggers background capture of active tab. |
| `chrome.action.setBadgeText()` | Post-save confirmation | Displays '✓' badge on browser action icon. |

---

## 5. Subsequent Stages Roadmap

### Stage 2: Organization & Custom Collections
- Custom user-defined tags (#work, #dev, #design) with multi-tag filtering.
- Visual collection pinning & custom color coding for folders.
- Drag-and-drop bookmark reordering and folder transfer.

### Stage 3: Rich Previews & AI Summarization
- Automatic OpenGraph metadata and image thumbnail extraction.
- Client-side AI page summarization and keyword tagging.
- Broken link detector and duplicate bookmark cleaner.

### Stage 4: Cloud Sync & Customization
- Encrypted cloud backup and cross-browser export (JSON, HTML, CSV).
- Custom wallpaper engine (Unsplash integration, live gradients, blur effects).
- Customizable hotkey palette and omnibox instant search command (`nb <query>`).
