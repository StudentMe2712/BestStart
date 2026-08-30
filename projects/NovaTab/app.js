/**
 * NovaTab — Visual Bookmark Manager Core Application
 * Handles bookmark indexing, search, folder tree navigation, CRUD actions, and reactive sync.
 */

(() => {
  'use strict';

  // --- 1. STATE MANAGEMENT ---
  const state = {
    isExtension: typeof chrome !== 'undefined' && !!chrome.bookmarks,
    rawTree: [],
    allBookmarks: [],
    allFolders: [],
    folderMap: new Map(),
    bookmarksByFolder: new Map(),
    activeView: 'all', // 'all', 'recent', or folder ID
    activeFolderName: 'Все закладки',
    activeFolderPath: ['Главная', 'Все закладки'],
    searchQuery: '',
    viewMode: 'grid', // 'grid' | 'list'
    sortBy: 'dateAdded-desc',
    editingBookmarkId: null
  };

  // --- 2. MOCK DATA FOR LOCAL / STANDALONE TESTING ---
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
              id: '10',
              title: 'GitHub: Let’s build from here',
              url: 'https://github.com',
              dateAdded: Date.now() - 1000 * 60 * 60 * 2
            },
            {
              id: '11',
              title: 'Hacker News — Tech & Startup Headlines',
              url: 'https://news.ycombinator.com',
              dateAdded: Date.now() - 1000 * 60 * 60 * 18
            },
            {
              id: '12',
              title: 'Figma: Collaborative Design Tool',
              url: 'https://figma.com',
              dateAdded: Date.now() - 1000 * 60 * 60 * 48
            },
            {
              id: '2',
              title: 'Разработка & Архитектура',
              children: [
                {
                  id: '20',
                  title: 'MDN Web Docs — JavaScript, CSS, HTML',
                  url: 'https://developer.mozilla.org',
                  dateAdded: Date.now() - 1000 * 60 * 60 * 24 * 3
                },
                {
                  id: '21',
                  title: 'Tailwind CSS — Rapid UI Styling Documentation',
                  url: 'https://tailwindcss.com',
                  dateAdded: Date.now() - 1000 * 60 * 60 * 24 * 5
                },
                {
                  id: '22',
                  title: 'Chrome Extensions Manifest V3 Guide',
                  url: 'https://developer.chrome.com/docs/extensions/mv3/',
                  dateAdded: Date.now() - 1000 * 60 * 60 * 24 * 7
                },
                {
                  id: '23',
                  title: 'Stack Overflow — Where Developers Learn & Share',
                  url: 'https://stackoverflow.com',
                  dateAdded: Date.now() - 1000 * 60 * 60 * 24 * 12
                }
              ]
            },
            {
              id: '3',
              title: 'Искусственный интеллект',
              children: [
                {
                  id: '30',
                  title: 'Anthropic — Claude AI Research & Assistant',
                  url: 'https://anthropic.com',
                  dateAdded: Date.now() - 1000 * 60 * 60 * 24 * 2
                },
                {
                  id: '31',
                  title: 'Hugging Face — The AI community building the future',
                  url: 'https://huggingface.co',
                  dateAdded: Date.now() - 1000 * 60 * 60 * 24 * 8
                },
                {
                  id: '32',
                  title: 'OpenAI Platform & Documentation',
                  url: 'https://platform.openai.com',
                  dateAdded: Date.now() - 1000 * 60 * 60 * 24 * 14
                }
              ]
            },
            {
              id: '4',
              title: 'Дизайн и Вдохновение',
              children: [
                {
                  id: '40',
                  title: 'Dribbble — Discover the World’s Top Designers',
                  url: 'https://dribbble.com',
                  dateAdded: Date.now() - 1000 * 60 * 60 * 24 * 10
                },
                {
                  id: '41',
                  title: 'Lumi List — Visual Bookmarking & Link Inspo',
                  url: 'https://lumilist.com',
                  dateAdded: Date.now() - 1000 * 60 * 60 * 24 * 15
                }
              ]
            }
          ]
        },
        {
          id: '5',
          title: 'Другие закладки',
          children: [
            {
              id: '50',
              title: 'YouTube — Video Streaming Platform',
              url: 'https://youtube.com',
              dateAdded: Date.now() - 1000 * 60 * 60 * 24 * 20
            },
            {
              id: '51',
              title: 'Reddit: Dive into anything',
              url: 'https://reddit.com',
              dateAdded: Date.now() - 1000 * 60 * 60 * 24 * 25
            }
          ]
        }
      ]
    }
  ];

  // --- 3. DOM ELEMENTS ---
  const elements = {
    // Navigation
    navAll: document.getElementById('nav-all'),
    navRecent: document.getElementById('nav-recent'),
    folderTreeContainer: document.getElementById('folder-tree-container'),
    btnRefreshTree: document.getElementById('btn-refresh-tree'),
    badgeAllCount: document.getElementById('badge-all-count'),
    badgeRecentCount: document.getElementById('badge-recent-count'),
    statTotalBookmarks: document.getElementById('stat-total-bookmarks'),
    statTotalFolders: document.getElementById('stat-total-folders'),

    // Main Header
    headerTitle: document.getElementById('header-view-title'),
    headerBreadcrumb: document.getElementById('header-breadcrumb-path'),
    searchInput: document.getElementById('search-input'),
    sortSelect: document.getElementById('sort-select'),
    btnViewGrid: document.getElementById('btn-view-grid'),
    btnViewList: document.getElementById('btn-view-list'),
    btnAddBookmark: document.getElementById('btn-add-bookmark'),

    // Content Viewport
    bookmarksContainer: document.getElementById('bookmarks-container'),
    emptyState: document.getElementById('empty-state'),
    emptyStateTitle: document.getElementById('empty-state-title'),
    emptyStateDesc: document.getElementById('empty-state-desc'),
    btnEmptyAdd: document.getElementById('btn-empty-add'),

    // Modal
    bookmarkModal: document.getElementById('bookmark-modal'),
    modalTitle: document.getElementById('modal-title'),
    modalBtnClose: document.getElementById('modal-btn-close'),
    modalBtnCancel: document.getElementById('modal-btn-cancel'),
    bookmarkForm: document.getElementById('bookmark-form'),
    modalBookmarkId: document.getElementById('modal-bookmark-id'),
    modalBookmarkTitle: document.getElementById('modal-bookmark-title'),
    modalBookmarkUrl: document.getElementById('modal-bookmark-url'),
    modalBookmarkFolder: document.getElementById('modal-bookmark-folder'),

    // Toast
    toastContainer: document.getElementById('toast-container')
  };

  // --- 4. UTILITIES ---
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
    return `hsl(${hue}, 70%, 45%)`;
  }

  function formatRelativeDate(timestamp) {
    if (!timestamp) return '';
    const now = Date.now();
    const diffMs = now - timestamp;
    const diffMinutes = Math.floor(diffMs / (1000 * 60));
    const diffHours = Math.floor(diffMs / (1000 * 60 * 60));
    const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

    if (diffMinutes < 1) return 'Только что';
    if (diffMinutes < 60) return `${diffMinutes} мин назад`;
    if (diffHours < 24) return `${diffHours} ч назад`;
    if (diffDays === 1) return 'Вчера';
    if (diffDays < 7) return `${diffDays} дн назад`;
    if (diffDays < 30) return `${Math.floor(diffDays / 7)} нед назад`;

    const d = new Date(timestamp);
    return d.toLocaleDateString('ru-RU', { day: 'numeric', month: 'short', year: 'numeric' });
  }

  function showToast(message, type = 'success') {
    const toast = document.createElement('div');
    toast.className = `toast ${type === 'success' ? 'toast-success' : 'toast-error'}`;
    toast.innerHTML = `
      <span>${type === 'success' ? '✓' : '⚠️'}</span>
      <span>${escapeHtml(message)}</span>
    `;
    elements.toastContainer.appendChild(toast);

    setTimeout(() => {
      toast.style.opacity = '0';
      toast.style.transform = 'translateY(10px)';
      toast.style.transition = 'all 0.2s ease';
      setTimeout(() => toast.remove(), 250);
    }, 3200);
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
      return `chrome-extension://${chrome.runtime.id}/_favicon/?pageUrl=${encodeURIComponent(pageUrl)}&size=32`;
    }
    const host = extractHostname(pageUrl);
    return `https://www.google.com/s2/favicons?domain=${encodeURIComponent(host)}&sz=64`;
  }

  // --- 5. BOOKMARKS TREE PARSING ---
  function parseBookmarkNodes(nodes, parentPath = [], parentId = null) {
    for (const node of nodes) {
      if (node.url) {
        // Bookmark item
        const hostname = extractHostname(node.url);
        const folderName = parentPath[parentPath.length - 1] || 'Панель закладок';
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
        // Folder item (skip root '0' container wrapper if title is empty or 'root')
        const isRootWrapper = node.id === '0' || node.title === 'Root' || node.title === '';
        const currentPath = isRootWrapper ? parentPath : [...parentPath, node.title];

        if (!isRootWrapper) {
          const folderObj = {
            id: String(node.id),
            title: node.title,
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

  // Helper to count bookmarks recursively inside a folder and its subfolders
  function getFolderRecursiveCount(folderId) {
    let count = (state.bookmarksByFolder.get(folderId) || []).length;
    const folder = state.folderMap.get(folderId);
    if (folder && folder.childrenFolderIds) {
      for (const childId of folder.childrenFolderIds) {
        count += getFolderRecursiveCount(childId);
      }
    }
    return count;
  }

  // --- 6. DATA LOADING & SYNC ---
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
        console.warn('[NovaTab] Standalone mode: Using mock bookmark tree.');
        state.rawTree = MOCK_BOOKMARK_TREE;
        parseBookmarkNodes(MOCK_BOOKMARK_TREE);
      }
    } catch (err) {
      console.error('[NovaTab] Failed loading bookmarks:', err);
      showToast('Ошибка загрузки закладок', 'error');
    }

    updateSidebarCounters();
    renderFolderTree();
    populateFolderSelectDropdown();
    renderCurrentView();
  }

  // --- 7. RENDERING COMPONENTS ---

  function updateSidebarCounters() {
    const totalBookmarks = state.allBookmarks.length;
    const totalFolders = state.allFolders.length;

    elements.badgeAllCount.textContent = totalBookmarks;
    elements.badgeRecentCount.textContent = Math.min(totalBookmarks, 40);
    elements.statTotalBookmarks.textContent = totalBookmarks;
    elements.statTotalFolders.textContent = totalFolders;
  }

  function renderFolderTree() {
    elements.folderTreeContainer.innerHTML = '';

    if (state.allFolders.length === 0) {
      elements.folderTreeContainer.innerHTML = `
        <div class="px-3 py-2 text-xs text-gray-500">Нет папок</div>
      `;
      return;
    }

    state.allFolders.forEach(folder => {
      const count = getFolderRecursiveCount(folder.id);
      const isActive = state.activeView === folder.id;
      const indentPx = Math.max(0, folder.depth * 14);

      const item = document.createElement('div');
      item.className = `nav-item ${isActive ? 'active' : ''}`;
      item.style.paddingLeft = `${12 + indentPx}px`;
      item.setAttribute('data-folder-id', folder.id);

      item.innerHTML = `
        <div class="nav-item-left">
          <span class="nav-icon text-sm">${folder.depth > 0 ? '↳ 📁' : '📁'}</span>
          <span class="truncate" title="${escapeHtml(folder.title)}">${escapeHtml(folder.title)}</span>
        </div>
        <span class="nav-badge">${count}</span>
      `;

      item.addEventListener('click', () => {
        selectView(folder.id, folder.title, folder.path);
      });

      elements.folderTreeContainer.appendChild(item);
    });
  }

  function populateFolderSelectDropdown() {
    elements.modalBookmarkFolder.innerHTML = '';

    if (state.allFolders.length === 0) {
      const opt = document.createElement('option');
      opt.value = '1';
      opt.textContent = 'Панель закладок';
      elements.modalBookmarkFolder.appendChild(opt);
      return;
    }

    state.allFolders.forEach(folder => {
      const opt = document.createElement('option');
      opt.value = folder.id;
      const indent = '— '.repeat(folder.depth);
      opt.textContent = `${indent}${folder.title}`;
      elements.modalBookmarkFolder.appendChild(opt);
    });
  }

  function selectView(viewKey, title, pathArray = []) {
    state.activeView = viewKey;
    state.searchQuery = '';
    elements.searchInput.value = '';

    // Update active class on nav elements
    elements.navAll.classList.toggle('active', viewKey === 'all');
    elements.navRecent.classList.toggle('active', viewKey === 'recent');

    document.querySelectorAll('#folder-tree-container .nav-item').forEach(el => {
      const folderId = el.getAttribute('data-folder-id');
      el.classList.toggle('active', folderId === viewKey);
    });

    if (viewKey === 'all') {
      state.activeFolderName = 'Все закладки';
      state.activeFolderPath = ['Главная', 'Все сохраненные страницы'];
    } else if (viewKey === 'recent') {
      state.activeFolderName = 'Недавние закладки';
      state.activeFolderPath = ['Главная', 'Последние добавленные'];
    } else {
      state.activeFolderName = title || 'Папка';
      state.activeFolderPath = ['Главная', ...(pathArray || [state.activeFolderName])];
    }

    renderCurrentView();
  }

  // Filter and Sort bookmarks
  function getFilteredBookmarks() {
    let list = [];

    if (state.activeView === 'all') {
      list = [...state.allBookmarks];
    } else if (state.activeView === 'recent') {
      list = [...state.allBookmarks].sort((a, b) => (b.dateAdded || 0) - (a.dateAdded || 0)).slice(0, 40);
    } else {
      // Specific folder view (includes subfolders)
      const targetFolderIds = new Set();
      function collectIds(fid) {
        targetFolderIds.add(fid);
        const f = state.folderMap.get(fid);
        if (f && f.childrenFolderIds) {
          f.childrenFolderIds.forEach(collectIds);
        }
      }
      collectIds(state.activeView);

      list = state.allBookmarks.filter(b => targetFolderIds.has(b.parentId));
    }

    // Apply Search Filter if any
    const query = state.searchQuery.trim().toLowerCase();
    if (query) {
      list = list.filter(b => {
        const titleMatch = b.title.toLowerCase().includes(query);
        const urlMatch = b.url.toLowerCase().includes(query);
        const hostMatch = b.hostname.toLowerCase().includes(query);
        const folderMatch = b.folderName.toLowerCase().includes(query);
        return titleMatch || urlMatch || hostMatch || folderMatch;
      });
    }

    // Apply Sort (if not in recent view with no active search)
    const [sortField, sortOrder] = state.sortBy.split('-');
    list.sort((a, b) => {
      let valA = a[sortField] || '';
      let valB = b[sortField] || '';

      if (sortField === 'title' || sortField === 'domain' || sortField === 'hostname') {
        valA = String(valA).toLowerCase();
        valB = String(valB).toLowerCase();
        return sortOrder === 'asc' ? valA.localeCompare(valB) : valB.localeCompare(valA);
      }

      if (sortField === 'dateAdded') {
        return sortOrder === 'asc' ? (valA - valB) : (valB - valA);
      }

      return 0;
    });

    return list;
  }

  function renderCurrentView() {
    // Update Header Text
    elements.headerTitle.innerHTML = `<span>${escapeHtml(state.activeFolderName)}</span>`;
    elements.headerBreadcrumb.innerHTML = `<span>${state.activeFolderPath.map(escapeHtml).join(' &rsaquo; ')}</span>`;

    const bookmarks = getFilteredBookmarks();
    elements.bookmarksContainer.innerHTML = '';

    // Handle view mode class
    if (state.viewMode === 'list') {
      elements.bookmarksContainer.classList.add('list-view');
      elements.btnViewList.classList.add('active');
      elements.btnViewGrid.classList.remove('active');
    } else {
      elements.bookmarksContainer.classList.remove('list-view');
      elements.btnViewGrid.classList.add('active');
      elements.btnViewList.classList.remove('active');
    }

    if (bookmarks.length === 0) {
      elements.bookmarksContainer.classList.add('hidden');
      elements.emptyState.classList.remove('hidden');

      if (state.searchQuery) {
        elements.emptyStateTitle.textContent = 'Ничего не найдено';
        elements.emptyStateDesc.textContent = `По запросу «${escapeHtml(state.searchQuery)}» совпадений не обнаружено. Попробуйте изменить ключевые слова.`;
        elements.btnEmptyAdd.classList.add('hidden');
      } else {
        elements.emptyStateTitle.textContent = 'Здесь пока пусто';
        elements.emptyStateDesc.textContent = 'В этой категории еще нет сохраненных закладок. Нажмите кнопку ниже, чтобы добавить первую!';
        elements.btnEmptyAdd.classList.remove('hidden');
      }
      return;
    }

    elements.bookmarksContainer.classList.remove('hidden');
    elements.emptyState.classList.add('hidden');

    // Render Cards
    const fragment = document.createDocumentFragment();
    bookmarks.forEach(bm => {
      const card = createBookmarkCardElement(bm);
      fragment.appendChild(card);
    });

    elements.bookmarksContainer.appendChild(fragment);
  }

  function createBookmarkCardElement(bookmark) {
    const card = document.createElement('div');
    card.className = 'bookmark-card';
    card.setAttribute('data-id', bookmark.id);

    const faviconSrc = getFaviconUrl(bookmark.url);
    const hostColor = hashStringColor(bookmark.hostname || 'site');
    const firstLetter = (bookmark.title || bookmark.hostname || 'N').charAt(0).toUpperCase();

    card.innerHTML = `
      <div class="card-top">
        <div class="favicon-wrapper">
          <img 
            class="favicon-img" 
            src="${escapeHtml(faviconSrc)}" 
            alt="${escapeHtml(bookmark.hostname)}"
            loading="lazy"
          >
          <div class="favicon-fallback hidden" style="background: ${hostColor}">
            ${escapeHtml(firstLetter)}
          </div>
        </div>
        <span class="domain-badge" title="${escapeHtml(bookmark.hostname)}">
          ${escapeHtml(bookmark.hostname)}
        </span>
      </div>

      <div class="card-body">
        <h3 class="bookmark-title" title="${escapeHtml(bookmark.title)}">
          ${escapeHtml(bookmark.title)}
        </h3>
        <span class="bookmark-url" title="${escapeHtml(bookmark.url)}">
          ${escapeHtml(bookmark.url)}
        </span>
      </div>

      <div class="card-bottom">
        <span class="bookmark-date">${formatRelativeDate(bookmark.dateAdded)}</span>
        <div class="card-actions">
          <button class="card-action-btn action-open" title="Открыть в новой вкладке">
            <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"></path>
              <polyline points="15 3 21 3 21 9"></polyline>
              <line x1="10" y1="14" x2="21" y2="3"></line>
            </svg>
          </button>
          <button class="card-action-btn action-copy" title="Скопировать ссылку">
            <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
              <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
              <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
            </svg>
          </button>
          <button class="card-action-btn action-edit" title="Редактировать">
            <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"></path>
              <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"></path>
            </svg>
          </button>
          <button class="card-action-btn delete-btn action-delete" title="Удалить">
            <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
              <polyline points="3 6 5 6 21 6"></polyline>
              <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path>
            </svg>
          </button>
        </div>
      </div>
    `;

    // Favicon Fallback Event
    const imgEl = card.querySelector('.favicon-img');
    const fallbackEl = card.querySelector('.favicon-fallback');
    imgEl.addEventListener('error', () => {
      imgEl.classList.add('hidden');
      fallbackEl.classList.remove('hidden');
    });

    // Card Primary Click (Open link)
    card.addEventListener('click', (e) => {
      if (e.target.closest('.card-actions')) return;
      openUrl(bookmark.url);
    });

    // Action handlers
    card.querySelector('.action-open').addEventListener('click', (e) => {
      e.stopPropagation();
      openUrl(bookmark.url);
    });

    card.querySelector('.action-copy').addEventListener('click', async (e) => {
      e.stopPropagation();
      try {
        await navigator.clipboard.writeText(bookmark.url);
        showToast('Ссылка скопирована в буфер обмена');
      } catch {
        showToast('Не удалось скопировать ссылку', 'error');
      }
    });

    card.querySelector('.action-edit').addEventListener('click', (e) => {
      e.stopPropagation();
      openEditModal(bookmark);
    });

    card.querySelector('.action-delete').addEventListener('click', (e) => {
      e.stopPropagation();
      deleteBookmark(bookmark);
    });

    return card;
  }

  function openUrl(url) {
    if (state.isExtension && chrome.tabs?.create) {
      chrome.tabs.create({ url });
    } else {
      window.open(url, '_blank', 'noopener,noreferrer');
    }
  }

  // --- 8. MODAL & CRUD ACTIONS ---

  function openAddModal() {
    state.editingBookmarkId = null;
    elements.modalTitle.textContent = 'Добавить закладку';
    elements.modalBookmarkId.value = '';
    elements.modalBookmarkTitle.value = '';
    elements.modalBookmarkUrl.value = '';

    // Set default selected folder
    if (state.activeView !== 'all' && state.activeView !== 'recent' && state.folderMap.has(state.activeView)) {
      elements.modalBookmarkFolder.value = state.activeView;
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

  function closeModal() {
    elements.bookmarkModal.classList.remove('open');
    elements.bookmarkForm.reset();
    state.editingBookmarkId = null;
  }

  async function handleFormSubmit(e) {
    e.preventDefault();
    const title = elements.modalBookmarkTitle.value.trim();
    let url = elements.modalBookmarkUrl.value.trim();
    const parentId = elements.modalBookmarkFolder.value;

    if (!url) return;
    if (!/^https?:\/\//i.test(url)) {
      url = 'https://' + url;
    }

    try {
      if (state.editingBookmarkId) {
        // Edit existing bookmark
        if (state.isExtension) {
          await chrome.bookmarks.update(state.editingBookmarkId, { title, url });
          // If parent folder changed, move it
          const existing = state.allBookmarks.find(b => b.id === state.editingBookmarkId);
          if (existing && existing.parentId !== parentId) {
            await chrome.bookmarks.move(state.editingBookmarkId, { parentId });
          }
        } else {
          // Mock mode edit
          const bm = state.allBookmarks.find(b => b.id === state.editingBookmarkId);
          if (bm) {
            bm.title = title;
            bm.url = url;
            bm.parentId = parentId;
            bm.hostname = extractHostname(url);
          }
        }
        showToast('Закладка успешно обновлена');
      } else {
        // Add new bookmark
        if (state.isExtension) {
          await chrome.bookmarks.create({
            parentId: parentId || '1',
            title: title || extractHostname(url),
            url
          });
        } else {
          // Mock mode add
          const newBm = {
            id: String(Date.now()),
            parentId: parentId || '1',
            title: title || extractHostname(url),
            url,
            dateAdded: Date.now(),
            hostname: extractHostname(url),
            folderName: state.folderMap.get(parentId)?.title || 'Папка'
          };
          state.allBookmarks.unshift(newBm);
        }
        showToast('Закладка добавлена');
      }

      closeModal();
      await loadBookmarks();
    } catch (err) {
      console.error('[NovaTab] CRUD error:', err);
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
        state.allBookmarks = state.allBookmarks.filter(b => b.id !== bookmark.id);
      }
      showToast('Закладка удалена');
      await loadBookmarks();
    } catch (err) {
      console.error('[NovaTab] Delete error:', err);
      showToast('Ошибка при удалении закладки', 'error');
    }
  }

  // --- 9. EVENT LISTENERS & PERSISTENCE ---

  async function initPreferences() {
    if (state.isExtension && chrome.storage?.local) {
      const stored = await chrome.storage.local.get(['viewMode', 'sortBy']);
      if (stored.viewMode) state.viewMode = stored.viewMode;
      if (stored.sortBy) state.sortBy = stored.sortBy;
    } else {
      const localView = localStorage.getItem('novatab_viewMode');
      const localSort = localStorage.getItem('novatab_sortBy');
      if (localView) state.viewMode = localView;
      if (localSort) state.sortBy = localSort;
    }

    elements.sortSelect.value = state.sortBy;
  }

  function setupEventListeners() {
    // Sidebar view selectors
    elements.navAll.addEventListener('click', () => selectView('all'));
    elements.navRecent.addEventListener('click', () => selectView('recent'));
    elements.btnRefreshTree.addEventListener('click', () => loadBookmarks());

    // Search bar
    elements.searchInput.addEventListener('input', (e) => {
      state.searchQuery = e.target.value;
      renderCurrentView();
    });

    // Keyboard shortcut for search
    window.addEventListener('keydown', (e) => {
      // Press '/' to search
      if (e.key === '/' && document.activeElement !== elements.searchInput && !elements.bookmarkModal.classList.contains('open')) {
        e.preventDefault();
        elements.searchInput.focus();
        elements.searchInput.select();
      }
      // Press 'Escape' to clear search or close modal
      if (e.key === 'Escape') {
        if (elements.bookmarkModal.classList.contains('open')) {
          closeModal();
        } else if (document.activeElement === elements.searchInput) {
          elements.searchInput.value = '';
          state.searchQuery = '';
          elements.searchInput.blur();
          renderCurrentView();
        }
      }
    });

    // Sort selector
    elements.sortSelect.addEventListener('change', (e) => {
      state.sortBy = e.target.value;
      if (state.isExtension && chrome.storage?.local) {
        chrome.storage.local.set({ sortBy: state.sortBy });
      } else {
        localStorage.setItem('novatab_sortBy', state.sortBy);
      }
      renderCurrentView();
    });

    // View mode buttons
    elements.btnViewGrid.addEventListener('click', () => {
      state.viewMode = 'grid';
      if (state.isExtension && chrome.storage?.local) {
        chrome.storage.local.set({ viewMode: 'grid' });
      } else {
        localStorage.setItem('novatab_viewMode', 'grid');
      }
      renderCurrentView();
    });

    elements.btnViewList.addEventListener('click', () => {
      state.viewMode = 'list';
      if (state.isExtension && chrome.storage?.local) {
        chrome.storage.local.set({ viewMode: 'list' });
      } else {
        localStorage.setItem('novatab_viewMode', 'list');
      }
      renderCurrentView();
    });

    // Add buttons
    elements.btnAddBookmark.addEventListener('click', openAddModal);
    elements.btnEmptyAdd.addEventListener('click', openAddModal);

    // Modal close handlers
    elements.modalBtnClose.addEventListener('click', closeModal);
    elements.modalBtnCancel.addEventListener('click', closeModal);
    elements.bookmarkModal.addEventListener('click', (e) => {
      if (e.target === elements.bookmarkModal) closeModal();
    });

    // Form submit
    elements.bookmarkForm.addEventListener('submit', handleFormSubmit);

    // Chrome Live Bookmarks Reactive Sync
    if (state.isExtension) {
      chrome.bookmarks.onCreated.addListener(() => loadBookmarks());
      chrome.bookmarks.onRemoved.addListener(() => loadBookmarks());
      chrome.bookmarks.onChanged.addListener(() => loadBookmarks());
      chrome.bookmarks.onMoved.addListener(() => loadBookmarks());
    }
  }

  // --- 10. INITIALIZATION ---
  async function init() {
    console.log(`[NovaTab] Initializing app (Extension mode: ${state.isExtension})`);
    await initPreferences();
    setupEventListeners();
    await loadBookmarks();
  }

  document.addEventListener('DOMContentLoaded', init);
})();
