/**
 * NovaTab — Visual Bookmark Manager: Glassmorphism Edition Core
 * Full-featured visual new tab extension with custom wallpaper engine,
 * folder-based category glass cards, top board pills, and instant search.
 * 
 * 100% Manifest V3 CSP Compliant:
 * - Zero external CDN runtime dependencies
 * - Zero inline scripts and zero inline HTML event handlers
 * - Safe DOM event binding & delegated event listeners
 */

(() => {
  'use strict';

  // --- 1. APPLICATION STATE ---
  const state = {
    isExtension: typeof chrome !== 'undefined' && Boolean(chrome.bookmarks),
    rawTree: [],
    allBookmarks: [],
    allFolders: [],
    folderMap: new Map(),
    bookmarksByFolder: new Map(),
    activeBoardId: 'all', // 'all' or folder ID
    viewMode: 'grid', // 'grid' | 'list'
    searchQuery: '',
    currentSearchResults: [],
    selectedSearchIndex: -1,
    editingBookmarkId: null
  };

  // --- 2. RICH MOCK DATA FOR LOCAL / STANDALONE PREVIEW ---
  const MOCK_BOOKMARK_TREE = [
    {
      id: '0',
      title: 'Root',
      children: [
        {
          id: '1',
          title: 'Панель закладок',
          children: [
            {
              id: 'cat-streaming',
              title: '📺 Стриминг & Видео',
              children: [
                { id: 'str-1', title: 'YouTube — Видеохостинг и стримы', url: 'https://youtube.com', dateAdded: Date.now() - 1000 * 60 * 60 * 2 },
                { id: 'str-2', title: 'Twitch — Live Game Streaming', url: 'https://twitch.tv', dateAdded: Date.now() - 1000 * 60 * 60 * 8 },
                { id: 'str-3', title: 'Netflix — Кино и сериалы онлайн', url: 'https://netflix.com', dateAdded: Date.now() - 1000 * 60 * 60 * 24 },
                { id: 'str-4', title: 'Spotify Web Player — Музыка', url: 'https://open.spotify.com', dateAdded: Date.now() - 1000 * 60 * 60 * 48 },
                { id: 'str-5', title: 'Кинопоиск — Фильмы и премьеры', url: 'https://kinopoisk.ru', dateAdded: Date.now() - 1000 * 60 * 60 * 72 }
              ]
            },
            {
              id: 'cat-gaming',
              title: '🎮 Гейминг & Сообщества',
              children: [
                { id: 'gam-1', title: 'Steam Community — Магазин и хаб', url: 'https://steamcommunity.com', dateAdded: Date.now() - 1000 * 60 * 60 * 12 },
                { id: 'gam-2', title: 'Discord Web — Чаты и сообщества', url: 'https://discord.com/app', dateAdded: Date.now() - 1000 * 60 * 60 * 20 },
                { id: 'gam-3', title: 'Reddit /r/gaming — Игровые обсуждения', url: 'https://reddit.com/r/gaming', dateAdded: Date.now() - 1000 * 60 * 60 * 36 },
                { id: 'gam-4', title: 'IGN — Новости игр и рецензии', url: 'https://ign.com', dateAdded: Date.now() - 1000 * 60 * 60 * 80 }
              ]
            },
            {
              id: 'cat-dev',
              title: '💻 Разработка & Код',
              children: [
                { id: 'dev-1', title: 'GitHub — Where the world builds software', url: 'https://github.com', dateAdded: Date.now() - 1000 * 60 * 60 * 1 },
                { id: 'dev-2', title: 'Stack Overflow — Q&A for Developers', url: 'https://stackoverflow.com', dateAdded: Date.now() - 1000 * 60 * 60 * 15 },
                { id: 'dev-3', title: 'MDN Web Docs — JavaScript, CSS & HTML', url: 'https://developer.mozilla.org', dateAdded: Date.now() - 1000 * 60 * 60 * 50 },
                { id: 'dev-4', title: 'Tailwind CSS Documentation', url: 'https://tailwindcss.com', dateAdded: Date.now() - 1000 * 60 * 60 * 90 },
                { id: 'dev-5', title: 'npm — JavaScript Package Registry', url: 'https://npmjs.com', dateAdded: Date.now() - 1000 * 60 * 60 * 110 }
              ]
            },
            {
              id: 'cat-ai',
              title: '🤖 Искусственный Интеллект',
              children: [
                { id: 'ai-1', title: 'Claude by Anthropic — AI Research', url: 'https://claude.ai', dateAdded: Date.now() - 1000 * 60 * 60 * 5 },
                { id: 'ai-2', title: 'ChatGPT by OpenAI — Assistant', url: 'https://chatgpt.com', dateAdded: Date.now() - 1000 * 60 * 60 * 18 },
                { id: 'ai-3', title: 'Hugging Face — Open-source ML Community', url: 'https://huggingface.co', dateAdded: Date.now() - 1000 * 60 * 60 * 60 },
                { id: 'ai-4', title: 'Perplexity AI — Answer Engine', url: 'https://perplexity.ai', dateAdded: Date.now() - 1000 * 60 * 60 * 120 }
              ]
            },
            {
              id: 'cat-news',
              title: '📰 Новости технологий',
              children: [
                { id: 'news-1', title: 'Hacker News — Tech & Startups', url: 'https://news.ycombinator.com', dateAdded: Date.now() - 1000 * 60 * 60 * 6 },
                { id: 'news-2', title: 'The Verge — Technology, Science & Art', url: 'https://theverge.com', dateAdded: Date.now() - 1000 * 60 * 60 * 30 },
                { id: 'news-3', title: 'Habr — Русскоязычное IT сообщество', url: 'https://habr.com', dateAdded: Date.now() - 1000 * 60 * 60 * 70 },
                { id: 'news-4', title: 'TechCrunch — Startup and Tech News', url: 'https://techcrunch.com', dateAdded: Date.now() - 1000 * 60 * 60 * 140 }
              ]
            },
            {
              id: 'cat-design',
              title: '🎨 Дизайн & Ресурсы',
              children: [
                { id: 'des-1', title: 'Figma: The Collaborative Interface Tool', url: 'https://figma.com', dateAdded: Date.now() - 1000 * 60 * 60 * 10 },
                { id: 'des-2', title: 'Dribbble — Top Designer Showcase', url: 'https://dribbble.com', dateAdded: Date.now() - 1000 * 60 * 60 * 45 },
                { id: 'des-3', title: 'Mobbin — UI & UX Design Patterns', url: 'https://mobbin.com', dateAdded: Date.now() - 1000 * 60 * 60 * 100 },
                { id: 'des-4', title: 'Google Fonts — Free Web Typography', url: 'https://fonts.google.com', dateAdded: Date.now() - 1000 * 60 * 60 * 160 }
              ]
            },
            {
              id: 'cat-daily',
              title: '⚡ Повседневные сервисы',
              children: [
                { id: 'day-1', title: 'Google Drive — Cloud Workspace', url: 'https://drive.google.com', dateAdded: Date.now() - 1000 * 60 * 60 * 14 },
                { id: 'day-2', title: 'Notion — All-in-one Workspace', url: 'https://notion.so', dateAdded: Date.now() - 1000 * 60 * 60 * 32 },
                { id: 'day-3', title: 'Telegram Web — Messenger', url: 'https://web.telegram.org', dateAdded: Date.now() - 1000 * 60 * 60 * 65 },
                { id: 'day-4', title: 'Gmail: Private and Secure Email', url: 'https://mail.google.com', dateAdded: Date.now() - 1000 * 60 * 60 * 115 }
              ]
            }
          ]
        }
      ]
    }
  ];

  // --- 3. DOM ELEMENTS CACHE ---
  const elements = {
    // Top Nav Boards
    boardsPillsWrapper: document.getElementById('boards-pills-wrapper'),
    btnAddBoard: document.getElementById('btn-add-board'),

    // Main Viewport & Cards
    mainViewport: document.getElementById('main-viewport'),
    cardsContainer: document.getElementById('cards-container'),
    emptyState: document.getElementById('empty-state'),
    emptyStateTitle: document.getElementById('empty-state-title'),
    emptyStateDesc: document.getElementById('empty-state-desc'),
    btnEmptyAdd: document.getElementById('btn-empty-add'),

    // Floating Toolbar
    toolbarSearchBtn: document.getElementById('toolbar-search-btn'),
    toolbarAddBtn: document.getElementById('toolbar-add-btn'),
    toolbarFolderBtn: document.getElementById('toolbar-folder-btn'),
    toolbarViewBtn: document.getElementById('toolbar-view-btn'),
    viewIconGrid: document.getElementById('view-icon-grid'),
    viewIconList: document.getElementById('view-icon-list'),
    tooltipViewMode: document.getElementById('tooltip-view-mode'),
    toolbarRandomBtn: document.getElementById('toolbar-random-btn'),
    toolbarSettingsBtn: document.getElementById('toolbar-settings-btn'),

    // Floating Background Button & Input
    bgChangeBtn: document.getElementById('bg-change-btn'),
    bgFileInput: document.getElementById('bg-file-input'),

    // Search Modal
    searchModal: document.getElementById('search-modal'),
    searchModalInput: document.getElementById('search-modal-input'),
    searchResultsList: document.getElementById('search-results-list'),
    searchModalClose: document.getElementById('search-modal-close'),
    searchMatchCount: document.getElementById('search-match-count'),

    // Add / Edit Bookmark Modal
    bookmarkModal: document.getElementById('bookmark-modal'),
    modalTitle: document.getElementById('modal-title'),
    modalBtnClose: document.getElementById('modal-btn-close'),
    modalBtnCancel: document.getElementById('modal-btn-cancel'),
    bookmarkForm: document.getElementById('bookmark-form'),
    modalBookmarkId: document.getElementById('modal-bookmark-id'),
    modalBookmarkTitle: document.getElementById('modal-bookmark-title'),
    modalBookmarkUrl: document.getElementById('modal-bookmark-url'),
    modalBookmarkFolder: document.getElementById('modal-bookmark-folder'),

    // Add Folder Modal
    folderModal: document.getElementById('folder-modal'),
    folderForm: document.getElementById('folder-form'),
    folderTitleInput: document.getElementById('folder-title-input'),
    folderParentSelect: document.getElementById('folder-parent-select'),
    folderModalClose: document.getElementById('folder-modal-close'),
    folderBtnCancel: document.getElementById('folder-btn-cancel'),

    // Settings Modal
    settingsModal: document.getElementById('settings-modal'),
    settingsModalClose: document.getElementById('settings-modal-close'),
    settingsBtnDone: document.getElementById('settings-btn-done'),
    settingsUploadBgBtn: document.getElementById('settings-upload-bg-btn'),
    settingsResetBgBtn: document.getElementById('settings-reset-bg-btn'),
    settingsStatBookmarks: document.getElementById('settings-stat-bookmarks'),
    settingsStatFolders: document.getElementById('settings-stat-folders'),

    // Toast Container
    toastContainer: document.getElementById('toast-container')
  };

  // --- 4. UTILITIES ---
  function findNodeInTree(nodes, id) {
    if (!nodes || !Array.isArray(nodes)) return null;
    for (const node of nodes) {
      if (node.id === id) return node;
      if (node.children && node.children.length > 0) {
        const found = findNodeInTree(node.children, id);
        if (found) return found;
      }
    }
    return null;
  }

  function removeNodeFromTree(nodes, id) {
    if (!nodes || !Array.isArray(nodes)) return false;
    for (let i = 0; i < nodes.length; i++) {
      if (nodes[i].id === id) {
        nodes.splice(i, 1);
        return true;
      }
      if (nodes[i].children && nodes[i].children.length > 0) {
        if (removeNodeFromTree(nodes[i].children, id)) return true;
      }
    }
    return false;
  }

  function extractHostname(url) {
    try {
      if (!url) return '';
      const parsed = new URL(url);
      return parsed.hostname.replace(/^www\./, '');
    } catch {
      return url || '';
    }
  }

  function hashStringColor(str) {
    let hash = 0;
    for (let i = 0; i < str.length; i++) {
      hash = str.charCodeAt(i) + ((hash << 5) - hash);
    }
    const hue = Math.abs(hash % 360);
    return `hsl(${hue}, 65%, 50%)`;
  }

  function escapeHtml(str) {
    if (!str) return '';
    return String(str)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }

  function getFaviconUrl(pageUrl) {
    if (state.isExtension && chrome.runtime?.id) {
      return `chrome-extension://${chrome.runtime.id}/_favicon/?pageUrl=${encodeURIComponent(pageUrl)}&size=16`;
    }
    const host = extractHostname(pageUrl);
    return `https://www.google.com/s2/favicons?domain=${encodeURIComponent(host)}&sz=32`;
  }

  function showToast(message, type = 'success') {
    const toast = document.createElement('div');
    toast.className = `toast ${type === 'success' ? 'toast-success' : 'toast-error'}`;
    toast.innerHTML = `
      <span class="toast-icon">${type === 'success' ? '✓' : '⚠️'}</span>
      <span class="toast-message">${escapeHtml(message)}</span>
    `;
    elements.toastContainer.appendChild(toast);

    setTimeout(() => {
      toast.style.opacity = '0';
      toast.style.transform = 'translateY(10px)';
      toast.style.transition = 'all 0.2s ease';
      setTimeout(() => toast.remove(), 250);
    }, 3200);
  }

  function openUrl(url) {
    if (!url) return;
    if (state.isExtension && chrome.tabs?.create) {
      chrome.tabs.create({ url });
    } else {
      window.open(url, '_blank', 'noopener,noreferrer');
    }
  }

  // --- 5. CUSTOM BACKGROUND COMPRESSION & STORAGE ENGINE ---

  function applyBackground(dataUrl) {
    if (dataUrl) {
      document.body.style.backgroundImage = `url("${dataUrl}")`;
    } else {
      document.body.style.backgroundImage = '';
    }
  }

  async function loadSavedBackground() {
    try {
      if (state.isExtension && chrome.storage?.local) {
        const stored = await chrome.storage.local.get(['customBackground', 'viewMode']);
        if (stored.customBackground) {
          applyBackground(stored.customBackground);
        }
        if (stored.viewMode) {
          state.viewMode = stored.viewMode;
        }
      } else {
        const savedBg = localStorage.getItem('novatab_customBackground');
        if (savedBg) applyBackground(savedBg);
        const savedView = localStorage.getItem('novatab_viewMode');
        if (savedView) state.viewMode = savedView;
      }
    } catch (e) {
      console.warn('[NovaTab] Failed loading background from storage:', e);
    }
    updateViewModeUI();
  }

  function handleBgFileSelect(e) {
    const file = e.target.files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = (event) => {
      const img = new Image();
      img.onload = () => {
        // High quality scale down to max 1920x1080 maintaining aspect ratio
        let width = img.width;
        let height = img.height;
        const maxW = 1920;
        const maxH = 1080;

        if (width > maxW || height > maxH) {
          const ratio = Math.min(maxW / width, maxH / height);
          width = Math.round(width * ratio);
          height = Math.round(height * ratio);
        }

        const canvas = document.createElement('canvas');
        canvas.width = width;
        canvas.height = height;
        const ctx = canvas.getContext('2d');
        ctx.drawImage(img, 0, 0, width, height);

        let dataUrl;
        try {
          dataUrl = canvas.toDataURL('image/webp', 0.8);
        } catch {
          dataUrl = canvas.toDataURL('image/jpeg', 0.85);
        }

        // Store compressed background
        if (state.isExtension && chrome.storage?.local) {
          chrome.storage.local.set({ customBackground: dataUrl }, () => {
            applyBackground(dataUrl);
            showToast('Пользовательский фон успешно сохранен!');
          });
        } else {
          try {
            localStorage.setItem('novatab_customBackground', dataUrl);
            applyBackground(dataUrl);
            showToast('Пользовательский фон успешно сохранен!');
          } catch (storageErr) {
            console.warn('[NovaTab] LocalStorage quota exceeded:', storageErr);
            applyBackground(dataUrl);
            showToast('Фон применен на текущую сессию');
          }
        }
      };
      img.src = event.target.result;
    };
    reader.readAsDataURL(file);
    // Reset file input value so user can re-select same file if needed
    e.target.value = '';
  }

  function resetBackground() {
    if (state.isExtension && chrome.storage?.local) {
      chrome.storage.local.remove('customBackground', () => {
        applyBackground(null);
        showToast('Фон сброшен по умолчанию');
      });
    } else {
      localStorage.removeItem('novatab_customBackground');
      applyBackground(null);
      showToast('Фон сброшен по умолчанию');
    }
  }

  // --- 6. BOOKMARK PARSING & HIERARCHY ---

  function parseBookmarkNodes(nodes, parentPath = [], parentId = null) {
    for (const node of nodes) {
      if (node.url) {
        // Bookmark item
        const hostname = extractHostname(node.url);
        const folderName = parentPath[parentPath.length - 1] || 'Избранное';
        const bookmark = {
          id: String(node.id),
          parentId: String(parentId || node.parentId || '1'),
          title: node.title || hostname || 'Без названия',
          url: node.url,
          dateAdded: node.dateAdded || Date.now(),
          hostname,
          folderName,
          folderPath: [...parentPath]
        };
        state.allBookmarks.push(bookmark);

        if (!state.bookmarksByFolder.has(bookmark.parentId)) {
          state.bookmarksByFolder.set(bookmark.parentId, []);
        }
        state.bookmarksByFolder.get(bookmark.parentId).push(bookmark);
      } else if (node.children || (!node.url && node.title !== undefined)) {
        // Folder node
        const isRootWrapper = node.id === '0' || node.title === 'Root' || node.title === '';
        const currentPath = isRootWrapper ? parentPath : [...parentPath, node.title];

        if (!isRootWrapper) {
          const folderObj = {
            id: String(node.id),
            title: node.title || 'Папка',
            parentId: String(parentId || node.parentId || '0'),
            path: currentPath,
            depth: currentPath.length - 1,
            childrenFolderIds: []
          };
          state.allFolders.push(folderObj);
          state.folderMap.set(folderObj.id, folderObj);

          if (parentId && state.folderMap.has(parentId)) {
            state.folderMap.get(parentId).childrenFolderIds.push(folderObj.id);
          }
        }

        if (node.children && node.children.length > 0) {
          parseBookmarkNodes(node.children, currentPath, isRootWrapper ? null : String(node.id));
        }
      }
    }
  }

  async function loadBookmarks() {
    state.allBookmarks = [];
    state.allFolders = [];
    state.folderMap.clear();
    state.bookmarksByFolder.clear();

    try {
      if (state.isExtension) {
        const tree = await chrome.bookmarks.getTree();
        state.rawTree = tree;
        parseBookmarkNodes(tree);
      } else {
        state.rawTree = MOCK_BOOKMARK_TREE;
        parseBookmarkNodes(MOCK_BOOKMARK_TREE);
      }
    } catch (err) {
      console.error('[NovaTab] Failed loading bookmarks tree:', err);
      showToast('Ошибка чтения закладок', 'error');
    }

    renderBoardsPills();
    populateFolderSelectDropdowns();
    renderCardsView();
    updateStatsDisplay();
  }

  // --- 7. RENDERING BOARDS (TOP NAV PILLS) ---

  function renderBoardsPills() {
    if (!elements.boardsPillsWrapper) return;
    elements.boardsPillsWrapper.innerHTML = '';

    // 1. "✦ Все доски" Master Pill
    const allPill = document.createElement('button');
    allPill.className = `glass-pill ${state.activeBoardId === 'all' ? 'glass-pill-active' : ''}`;
    allPill.setAttribute('data-board', 'all');
    allPill.innerHTML = `<span>✦</span><span>Все доски</span>`;
    allPill.addEventListener('click', () => selectBoard('all'));
    elements.boardsPillsWrapper.appendChild(allPill);

    // 2. Filter, deduplicate, and render valid user category folders
    const ignoredRootIds = new Set(['0', '1', '2', 'mobile']);
    const seenIds = new Set();
    const displayFolders = state.allFolders.filter(f => {
      if (!f || !f.id || ignoredRootIds.has(String(f.id))) return false;
      if (!f.title || !f.title.trim()) return false;
      if (seenIds.has(String(f.id))) return false;
      seenIds.add(String(f.id));
      return true;
    });

    displayFolders.forEach(folder => {
      const bCount = (state.bookmarksByFolder.get(folder.id) || []).length;
      const isActive = state.activeBoardId === folder.id;

      const pill = document.createElement('button');
      pill.className = `glass-pill ${isActive ? 'glass-pill-active' : ''}`;
      pill.setAttribute('data-board', folder.id);

      pill.innerHTML = `
        <span class="pill-title" title="${escapeHtml(folder.title)}">${escapeHtml(folder.title)}</span>
        <span class="pill-count">${bCount}</span>
        <span class="delete-board-btn" title="Удалить папку «${escapeHtml(folder.title)}»">×</span>
      `;

      const deleteBtn = pill.querySelector('.delete-board-btn');
      if (deleteBtn) {
        deleteBtn.addEventListener('click', (event) => {
          event.stopPropagation();
          event.preventDefault();
          deleteCategoryFolder(folder);
        });
      }

      pill.addEventListener('click', () => selectBoard(folder.id));
      elements.boardsPillsWrapper.appendChild(pill);
    });
  }

  function selectBoard(boardId) {
    state.activeBoardId = boardId;
    renderBoardsPills();
    renderCardsView();
  }

  // --- 8. RENDERING CATEGORY GLASS CARDS ---

  function renderCardsView() {
    elements.cardsContainer.innerHTML = '';

    // Determine which folders to render cards for
    let targetFolders = [];

    if (state.activeBoardId === 'all') {
      // Find all folders that have bookmarks or subfolders
      targetFolders = state.allFolders.filter(f => {
        const directBookmarks = state.bookmarksByFolder.get(f.id) || [];
        return directBookmarks.length > 0 || (f.depth === 1);
      });

      // Also check if there are loose bookmarks directly on root parent (id: '1')
      const rootBookmarks = state.bookmarksByFolder.get('1') || [];
      if (rootBookmarks.length > 0 && !targetFolders.some(f => f.id === '1')) {
        targetFolders.unshift({
          id: '1',
          title: '⭐ Быстрый доступ',
          path: ['Панель закладок'],
          depth: 0,
          childrenFolderIds: []
        });
      }
    } else {
      const selected = state.folderMap.get(state.activeBoardId);
      if (selected) {
        targetFolders = [selected];
        if (selected.childrenFolderIds && selected.childrenFolderIds.length > 0) {
          selected.childrenFolderIds.forEach(cid => {
            const childF = state.folderMap.get(cid);
            if (childF) targetFolders.push(childF);
          });
        }
      }
    }

    if (targetFolders.length === 0 && state.allBookmarks.length === 0) {
      elements.cardsContainer.classList.add('hidden');
      elements.emptyState.classList.remove('hidden');
      return;
    }

    elements.cardsContainer.classList.remove('hidden');
    elements.emptyState.classList.add('hidden');

    const fragment = document.createDocumentFragment();

    targetFolders.forEach(folder => {
      const bookmarks = state.bookmarksByFolder.get(folder.id) || [];
      const cardEl = createCategoryCard(folder, bookmarks);
      fragment.appendChild(cardEl);
    });

    elements.cardsContainer.appendChild(fragment);
  }

  function createCategoryCard(folder, bookmarks) {
    const card = document.createElement('div');
    card.className = 'glass-card';
    card.setAttribute('data-folder-id', folder.id);

    // Card Header
    const header = document.createElement('div');
    header.className = 'card-header-bar';
    const isRootFolder = folder.id === '0' || folder.id === '1' || folder.id === '2' || folder.id === 'mobile';
    header.innerHTML = `
      <div class="card-title-group">
        <span class="card-title-text" title="${escapeHtml(folder.title)}">${escapeHtml(folder.title)}</span>
        <span class="card-badge">${bookmarks.length}</span>
      </div>
      <div class="card-header-actions">
        <button class="card-icon-btn btn-quick-add" title="Добавить закладку в «${escapeHtml(folder.title)}»">
          <svg class="icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2">
            <line x1="12" y1="5" x2="12" y2="19"></line>
            <line x1="5" y1="12" x2="19" y2="12"></line>
          </svg>
        </button>
        ${!isRootFolder ? `
        <button class="card-icon-btn btn-delete-card" title="Удалить категорию «${escapeHtml(folder.title)}»">
          <svg class="icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2">
            <polyline points="3 6 5 6 21 6"></polyline>
            <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path>
          </svg>
        </button>
        ` : ''}
      </div>
    `;

    header.querySelector('.btn-quick-add').addEventListener('click', (e) => {
      e.stopPropagation();
      openAddModal(folder.id);
    });

    const deleteFolderBtn = header.querySelector('.btn-delete-card');
    if (deleteFolderBtn) {
      deleteFolderBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        deleteCategoryFolder(folder);
      });
    }

    card.appendChild(header);

    // Bookmark Items Container
    const itemsList = document.createElement('div');
    itemsList.className = 'bookmark-list';

    if (bookmarks.length === 0) {
      itemsList.innerHTML = `
        <div class="bookmark-empty-hint">
          Нет закладок в этой категории
        </div>
      `;
    } else {
      bookmarks.forEach(bm => {
        const itemEl = createBookmarkRow(bm);
        itemsList.appendChild(itemEl);
      });
    }

    card.appendChild(itemsList);
    return card;
  }

  function createBookmarkRow(bookmark) {
    const row = document.createElement('div');
    row.className = 'bookmark-item';
    row.setAttribute('data-bookmark-id', bookmark.id);

    const faviconSrc = getFaviconUrl(bookmark.url);
    const hostColor = hashStringColor(bookmark.hostname || 'site');
    const firstLetter = (bookmark.title || bookmark.hostname || 'N').charAt(0).toUpperCase();

    row.innerHTML = `
      <div class="bookmark-left">
        <div class="favicon-wrapper">
          <img 
            class="favicon-img" 
            src="${escapeHtml(faviconSrc)}" 
            alt="${escapeHtml(bookmark.hostname)}"
            loading="lazy"
          >
          <div class="bookmark-favicon-fallback hidden" style="background: ${hostColor}">
            ${escapeHtml(firstLetter)}
          </div>
        </div>
        <div class="bookmark-info">
          <span class="bookmark-title" title="${escapeHtml(bookmark.title)}">${escapeHtml(bookmark.title)}</span>
          <span class="bookmark-domain" title="${escapeHtml(bookmark.hostname)}">${escapeHtml(bookmark.hostname)}</span>
        </div>
      </div>

      <div class="bookmark-actions">
        <button class="item-action-btn action-open" title="Открыть в новой вкладке">
          <svg class="icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2">
            <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"></path>
            <polyline points="15 3 21 3 21 9"></polyline>
            <line x1="10" y1="14" x2="21" y2="3"></line>
          </svg>
        </button>
        <button class="item-action-btn action-copy" title="Скопировать ссылку">
          <svg class="icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2">
            <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
            <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
          </svg>
        </button>
        <button class="item-action-btn action-edit" title="Редактировать">
          <svg class="icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2">
            <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"></path>
            <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"></path>
          </svg>
        </button>
        <button class="item-action-btn btn-delete action-delete" title="Удалить">
          <svg class="icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2">
            <polyline points="3 6 5 6 21 6"></polyline>
            <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path>
          </svg>
        </button>
      </div>
    `;

    // Favicon Fallback Event
    const imgEl = row.querySelector('.favicon-img');
    const fallbackEl = row.querySelector('.bookmark-favicon-fallback');
    imgEl.addEventListener('error', () => {
      imgEl.classList.add('hidden');
      fallbackEl.classList.remove('hidden');
    });

    // Primary click -> open URL
    row.addEventListener('click', (e) => {
      if (e.target.closest('.bookmark-actions')) return;
      openUrl(bookmark.url);
    });

    // Action handlers
    row.querySelector('.action-open').addEventListener('click', (e) => {
      e.stopPropagation();
      openUrl(bookmark.url);
    });

    row.querySelector('.action-copy').addEventListener('click', async (e) => {
      e.stopPropagation();
      try {
        await navigator.clipboard.writeText(bookmark.url);
        showToast('Ссылка скопирована в буфер обмена');
      } catch {
        showToast('Не удалось скопировать', 'error');
      }
    });

    row.querySelector('.action-edit').addEventListener('click', (e) => {
      e.stopPropagation();
      openEditModal(bookmark);
    });

    row.querySelector('.action-delete').addEventListener('click', (e) => {
      e.stopPropagation();
      deleteBookmark(bookmark);
    });

    return row;
  }

  // --- 9. SEARCH PALETTE MODAL ENGINE ---

  function openSearchModal() {
    elements.searchModal.classList.add('open');
    elements.searchModalInput.value = '';
    elements.searchModalInput.focus();
    state.selectedSearchIndex = 0;
    renderSearchResults('');
  }

  function closeSearchModal() {
    elements.searchModal.classList.remove('open');
    state.selectedSearchIndex = -1;
    state.currentSearchResults = [];
  }

  function updateSearchSelectionVisuals() {
    const items = elements.searchResultsList.querySelectorAll('.search-result-item');
    items.forEach((item, idx) => {
      if (idx === state.selectedSearchIndex) {
        item.classList.add('selected');
        item.scrollIntoView({ block: 'nearest' });
      } else {
        item.classList.remove('selected');
      }
    });
  }

  function renderSearchResults(query) {
    const q = query.trim().toLowerCase();
    elements.searchResultsList.innerHTML = '';

    let matches = [];
    if (!q) {
      // Show recent 15 bookmarks when query is empty
      matches = [...state.allBookmarks].sort((a, b) => (b.dateAdded || 0) - (a.dateAdded || 0)).slice(0, 15);
    } else {
      matches = state.allBookmarks.filter(b => {
        return (
          b.title.toLowerCase().includes(q) ||
          b.url.toLowerCase().includes(q) ||
          b.hostname.toLowerCase().includes(q) ||
          b.folderName.toLowerCase().includes(q)
        );
      });
    }

    state.currentSearchResults = matches;
    state.selectedSearchIndex = matches.length > 0 ? 0 : -1;

    elements.searchMatchCount.textContent = `${matches.length} ${q ? 'совпадений' : 'недавних закладок'}`;

    if (matches.length === 0) {
      elements.searchResultsList.innerHTML = `
        <div class="bookmark-empty-hint">
          По запросу «${escapeHtml(query)}» ничего не найдено
        </div>
      `;
      return;
    }

    matches.forEach((bm, idx) => {
      const item = document.createElement('div');
      item.className = `search-result-item ${idx === 0 ? 'selected' : ''}`;
      item.setAttribute('data-url', bm.url);

      const faviconSrc = getFaviconUrl(bm.url);

      item.innerHTML = `
        <div class="search-result-icon">
          <img src="${escapeHtml(faviconSrc)}" alt="" loading="lazy">
        </div>
        <div class="search-result-info">
          <span class="search-result-title">${escapeHtml(bm.title)}</span>
          <span class="search-result-url">${escapeHtml(bm.url)}</span>
        </div>
        <span class="search-result-badge">${escapeHtml(bm.folderName)}</span>
      `;

      item.addEventListener('mouseenter', () => {
        state.selectedSearchIndex = idx;
        updateSearchSelectionVisuals();
      });

      item.addEventListener('click', () => {
        openUrl(bm.url);
        closeSearchModal();
      });

      elements.searchResultsList.appendChild(item);
    });
  }

  // --- 10. CRUD MODALS & ACTIONS ---

  function populateFolderSelectDropdowns() {
    elements.modalBookmarkFolder.innerHTML = '';
    elements.folderParentSelect.innerHTML = '';

    if (state.allFolders.length === 0) {
      const opt = document.createElement('option');
      opt.value = '1';
      opt.textContent = 'Панель закладок';
      elements.modalBookmarkFolder.appendChild(opt);
      elements.folderParentSelect.appendChild(opt.cloneNode(true));
      return;
    }

    state.allFolders.forEach(folder => {
      const opt1 = document.createElement('option');
      opt1.value = folder.id;
      const indent = '— '.repeat(folder.depth);
      opt1.textContent = `${indent}${folder.title}`;
      elements.modalBookmarkFolder.appendChild(opt1);

      const opt2 = opt1.cloneNode(true);
      elements.folderParentSelect.appendChild(opt2);
    });
  }

  function openAddModal(targetFolderId = null) {
    state.editingBookmarkId = null;
    elements.modalTitle.textContent = 'Добавить закладку';
    elements.modalBookmarkId.value = '';
    elements.modalBookmarkTitle.value = '';
    elements.modalBookmarkUrl.value = '';

    if (targetFolderId && state.folderMap.has(targetFolderId)) {
      elements.modalBookmarkFolder.value = targetFolderId;
    } else if (state.activeBoardId !== 'all' && state.folderMap.has(state.activeBoardId)) {
      elements.modalBookmarkFolder.value = state.activeBoardId;
    } else if (state.allFolders.length > 0) {
      elements.modalBookmarkFolder.value = state.allFolders[0].id;
    }

    elements.bookmarkModal.classList.add('open');
    elements.modalBookmarkTitle.focus();
  }

  function openEditModal(bookmark) {
    state.editingBookmarkId = bookmark.id;
    elements.modalTitle.textContent = 'Редактировать закладку';
    elements.modalBookmarkId.value = bookmark.id;
    elements.modalBookmarkTitle.value = bookmark.title;
    elements.modalBookmarkUrl.value = bookmark.url;
    elements.modalBookmarkFolder.value = bookmark.parentId || (state.allFolders[0]?.id || '1');

    elements.bookmarkModal.classList.add('open');
    elements.modalBookmarkTitle.focus();
  }

  function closeBookmarkModal() {
    elements.bookmarkModal.classList.remove('open');
    elements.bookmarkForm.reset();
    state.editingBookmarkId = null;
  }

  async function handleBookmarkFormSubmit(e) {
    e.preventDefault();
    const title = elements.modalBookmarkTitle.value.trim();
    let url = elements.modalBookmarkUrl.value.trim();
    const parentId = elements.modalBookmarkFolder.value || '1';

    if (!url) return;
    if (!/^https?:\/\//i.test(url)) {
      url = 'https://' + url;
    }

    try {
      if (state.editingBookmarkId) {
        if (state.isExtension) {
          await chrome.bookmarks.update(state.editingBookmarkId, { title, url });
          const existing = state.allBookmarks.find(b => b.id === state.editingBookmarkId);
          if (existing && existing.parentId !== parentId) {
            await chrome.bookmarks.move(state.editingBookmarkId, { parentId });
          }
        } else {
          const bm = state.allBookmarks.find(b => b.id === state.editingBookmarkId);
          if (bm) {
            bm.title = title;
            bm.url = url;
            bm.parentId = parentId;
            bm.hostname = extractHostname(url);
            bm.folderName = state.folderMap.get(parentId)?.title || 'Папка';
          }
        }
        showToast('Закладка успешно обновлена');
      } else {
        if (state.isExtension) {
          await chrome.bookmarks.create({
            parentId,
            title: title || extractHostname(url),
            url
          });
        } else {
          const newBm = {
            id: 'mock-' + Date.now(),
            parentId,
            title: title || extractHostname(url),
            url,
            dateAdded: Date.now(),
            hostname: extractHostname(url),
            folderName: state.folderMap.get(parentId)?.title || 'Папка'
          };
          state.allBookmarks.unshift(newBm);
          if (!state.bookmarksByFolder.has(parentId)) {
            state.bookmarksByFolder.set(parentId, []);
          }
          state.bookmarksByFolder.get(parentId).unshift(newBm);

          const targetFolderNode = findNodeInTree(MOCK_BOOKMARK_TREE, parentId);
          if (targetFolderNode) {
            targetFolderNode.children = targetFolderNode.children || [];
            targetFolderNode.children.unshift(newBm);
          }
        }
        showToast('Закладка добавлена');
      }

      closeBookmarkModal();
      await loadBookmarks();
    } catch (err) {
      console.error('[NovaTab] Bookmark save error:', err);
      showToast('Ошибка при сохранении закладки', 'error');
    }
  }

  async function deleteBookmark(bookmark) {
    const confirmDelete = window.confirm(`Удалить закладку «${bookmark.title}»?`);
    if (!confirmDelete) return;

    try {
      if (state.isExtension) {
        await chrome.bookmarks.remove(bookmark.id);
      } else {
        removeNodeFromTree(MOCK_BOOKMARK_TREE, bookmark.id);
        state.allBookmarks = state.allBookmarks.filter(b => b.id !== bookmark.id);
        const folderList = state.bookmarksByFolder.get(bookmark.parentId);
        if (folderList) {
          state.bookmarksByFolder.set(bookmark.parentId, folderList.filter(b => b.id !== bookmark.id));
        }
      }
      showToast('Закладка удалена');
      await loadBookmarks();
    } catch (err) {
      console.error('[NovaTab] Delete error:', err);
      showToast('Ошибка удаления', 'error');
    }
  }

  async function deleteCategoryFolder(folder) {
    if (!folder || !folder.id) return;
    if (folder.id === '0' || folder.id === '1' || folder.id === '2' || folder.id === 'mobile') {
      showToast('Корневую папку нельзя удалить', 'error');
      return;
    }

    if (!window.confirm('Вы уверены, что хотите удалить эту папку и все её закладки?')) {
      return;
    }

    try {
      if (state.isExtension) {
        if (chrome.bookmarks?.removeTree) {
          await chrome.bookmarks.removeTree(folder.id);
        } else if (chrome.bookmarks?.remove) {
          await chrome.bookmarks.remove(folder.id);
        }
      } else {
        removeNodeFromTree(MOCK_BOOKMARK_TREE, folder.id);
        state.allFolders = state.allFolders.filter(f => f.id !== folder.id);
        state.folderMap.delete(folder.id);
        state.bookmarksByFolder.delete(folder.id);
        state.allBookmarks = state.allBookmarks.filter(b => b.parentId !== folder.id);
      }

      if (state.activeBoardId === folder.id) {
        state.activeBoardId = 'all';
      }

      showToast(`Папка «${folder.title}» удалена`);
      await loadBookmarks();
    } catch (err) {
      console.error('[NovaTab] Delete folder error:', err);
      showToast('Ошибка при удалении папки', 'error');
    }
  }

  // --- 11. FOLDER CREATION MODAL ---

  function openFolderModal() {
    elements.folderTitleInput.value = '';
    elements.folderModal.classList.add('open');
    elements.folderTitleInput.focus();
  }

  function closeFolderModal() {
    elements.folderModal.classList.remove('open');
  }

  async function handleFolderFormSubmit(e) {
    e.preventDefault();
    const title = elements.folderTitleInput.value.trim();
    const parentId = elements.folderParentSelect.value || '1';

    if (!title) return;

    try {
      if (state.isExtension) {
        await chrome.bookmarks.create({
          parentId,
          title
        });
      } else {
        const newFolderId = 'mock-folder-' + Date.now();
        const folderObj = {
          id: newFolderId,
          title,
          parentId,
          path: [title],
          depth: 1,
          childrenFolderIds: []
        };
        state.allFolders.push(folderObj);
        state.folderMap.set(newFolderId, folderObj);
        state.bookmarksByFolder.set(newFolderId, []);

        const parentNode = findNodeInTree(MOCK_BOOKMARK_TREE, parentId) || (MOCK_BOOKMARK_TREE[0]?.children?.[0]);
        if (parentNode) {
          parentNode.children = parentNode.children || [];
          parentNode.children.push({ id: newFolderId, title, children: [] });
        }
      }
      showToast(`Папка «${title}» создана!`);
      closeFolderModal();
      await loadBookmarks();
    } catch (err) {
      console.error('[NovaTab] Create folder error:', err);
      showToast('Ошибка создания папки', 'error');
    }
  }

  // --- 12. SETTINGS & VIEW TOGGLE ---

  function openSettingsModal() {
    updateStatsDisplay();
    elements.settingsModal.classList.add('open');
  }

  function closeSettingsModal() {
    elements.settingsModal.classList.remove('open');
  }

  function updateStatsDisplay() {
    elements.settingsStatBookmarks.textContent = state.allBookmarks.length;
    elements.settingsStatFolders.textContent = state.allFolders.length;
  }

  function toggleViewMode() {
    state.viewMode = state.viewMode === 'grid' ? 'list' : 'grid';
    if (state.isExtension && chrome.storage?.local) {
      chrome.storage.local.set({ viewMode: state.viewMode });
    } else {
      localStorage.setItem('novatab_viewMode', state.viewMode);
    }
    updateViewModeUI();
  }

  function updateViewModeUI() {
    if (state.viewMode === 'list') {
      elements.cardsContainer.classList.add('list-layout');
      elements.viewIconGrid.classList.add('hidden');
      elements.viewIconList.classList.remove('hidden');
      elements.tooltipViewMode.textContent = 'Вид: Список (переключить на сетку)';
    } else {
      elements.cardsContainer.classList.remove('list-layout');
      elements.viewIconGrid.classList.remove('hidden');
      elements.viewIconList.classList.add('hidden');
      elements.tooltipViewMode.textContent = 'Вид: Сетка (переключить на список)';
    }
  }

  function jumpToRandomBookmark() {
    if (state.allBookmarks.length === 0) {
      showToast('Нет доступных закладок для перехода', 'error');
      return;
    }
    const randomIndex = Math.floor(Math.random() * state.allBookmarks.length);
    const chosen = state.allBookmarks[randomIndex];
    showToast(`Открываем: ${chosen.title}`);
    setTimeout(() => openUrl(chosen.url), 200);
  }

  // --- 13. EVENT LISTENERS INITIALIZATION ---

  function setupEventListeners() {
    // Horizontal wheel scrolling on board pills
    if (elements.boardsPillsWrapper) {
      elements.boardsPillsWrapper.addEventListener('wheel', (e) => {
        e.preventDefault();
        elements.boardsPillsWrapper.scrollLeft += e.deltaY;
      }, { passive: false });
    }

    // Background Wallpaper Button
    elements.bgChangeBtn.addEventListener('click', () => {
      elements.bgFileInput.click();
    });

    // Right click on floating bg button -> Reset wallpaper
    elements.bgChangeBtn.addEventListener('contextmenu', (e) => {
      e.preventDefault();
      resetBackground();
    });

    elements.bgFileInput.addEventListener('change', handleBgFileSelect);

    // Floating Toolbar actions
    elements.toolbarSearchBtn.addEventListener('click', openSearchModal);
    elements.toolbarAddBtn.addEventListener('click', () => openAddModal());
    elements.toolbarFolderBtn.addEventListener('click', openFolderModal);
    elements.btnAddBoard.addEventListener('click', openFolderModal);
    elements.toolbarViewBtn.addEventListener('click', toggleViewMode);
    elements.toolbarRandomBtn.addEventListener('click', jumpToRandomBookmark);
    elements.toolbarSettingsBtn.addEventListener('click', openSettingsModal);

    // Empty state add button
    elements.btnEmptyAdd.addEventListener('click', () => openAddModal());

    // Search Modal events
    elements.searchModalClose.addEventListener('click', closeSearchModal);
    elements.searchModal.addEventListener('click', (e) => {
      if (e.target === elements.searchModal) closeSearchModal();
    });
    elements.searchModalInput.addEventListener('input', (e) => {
      renderSearchResults(e.target.value);
    });

    // Search Modal Keyboard Navigation (Up, Down, Enter, Escape)
    elements.searchModalInput.addEventListener('keydown', (e) => {
      if (e.key === 'ArrowDown') {
        e.preventDefault();
        if (state.currentSearchResults.length > 0) {
          state.selectedSearchIndex = (state.selectedSearchIndex + 1) % state.currentSearchResults.length;
          updateSearchSelectionVisuals();
        }
      } else if (e.key === 'ArrowUp') {
        e.preventDefault();
        if (state.currentSearchResults.length > 0) {
          state.selectedSearchIndex = (state.selectedSearchIndex - 1 + state.currentSearchResults.length) % state.currentSearchResults.length;
          updateSearchSelectionVisuals();
        }
      } else if (e.key === 'Enter') {
        e.preventDefault();
        if (state.currentSearchResults.length > 0 && state.selectedSearchIndex >= 0) {
          const selected = state.currentSearchResults[state.selectedSearchIndex];
          if (selected) {
            openUrl(selected.url);
            closeSearchModal();
          }
        }
      }
    });

    // Global Keydown shortcuts
    window.addEventListener('keydown', (e) => {
      // Press '/' to open fast search modal if not in another modal or input
      if (
        e.key === '/' && 
        !elements.searchModal.classList.contains('open') && 
        !elements.bookmarkModal.classList.contains('open') && 
        !elements.folderModal.classList.contains('open') &&
        !elements.settingsModal.classList.contains('open') &&
        document.activeElement.tagName !== 'INPUT' &&
        document.activeElement.tagName !== 'TEXTAREA'
      ) {
        e.preventDefault();
        openSearchModal();
      }
      // Press 'Escape' to close all modals
      if (e.key === 'Escape') {
        closeSearchModal();
        closeBookmarkModal();
        closeFolderModal();
        closeSettingsModal();
      }
    });

    // Bookmark Modal events
    elements.modalBtnClose.addEventListener('click', closeBookmarkModal);
    elements.modalBtnCancel.addEventListener('click', closeBookmarkModal);
    elements.bookmarkModal.addEventListener('click', (e) => {
      if (e.target === elements.bookmarkModal) closeBookmarkModal();
    });
    elements.bookmarkForm.addEventListener('submit', handleBookmarkFormSubmit);

    // Folder Modal events
    elements.folderModalClose.addEventListener('click', closeFolderModal);
    elements.folderBtnCancel.addEventListener('click', closeFolderModal);
    elements.folderModal.addEventListener('click', (e) => {
      if (e.target === elements.folderModal) closeFolderModal();
    });
    elements.folderForm.addEventListener('submit', handleFolderFormSubmit);

    // Settings Modal events
    elements.settingsModalClose.addEventListener('click', closeSettingsModal);
    elements.settingsBtnDone.addEventListener('click', closeSettingsModal);
    elements.settingsModal.addEventListener('click', (e) => {
      if (e.target === elements.settingsModal) closeSettingsModal();
    });
    elements.settingsUploadBgBtn.addEventListener('click', () => {
      elements.bgFileInput.click();
    });
    elements.settingsResetBgBtn.addEventListener('click', resetBackground);

    // Chrome Live Bookmarks Reactive Sync
    if (state.isExtension) {
      chrome.bookmarks.onCreated.addListener(() => loadBookmarks());
      chrome.bookmarks.onRemoved.addListener(() => loadBookmarks());
      chrome.bookmarks.onChanged.addListener(() => loadBookmarks());
      chrome.bookmarks.onMoved.addListener(() => loadBookmarks());
    }
  }

  // --- 14. BOOTSTRAP INITIALIZATION ---
  async function init() {
    console.log(`[NovaTab] Initializing Glassmorphism Engine (Extension mode: ${state.isExtension})`);
    setupEventListeners();
    await loadSavedBackground();
    await loadBookmarks();
  }

  document.addEventListener('DOMContentLoaded', init);
})();
