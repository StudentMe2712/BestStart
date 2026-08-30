/**
 * NovaTab — Aesthetic Glassmorphism Dashboard Core Engine
 * 100% Manifest V3 CSP Compliant:
 * - Zero remote scripts / external dependencies
 * - Zero inline event handlers / scripts
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
    searchQuery: '',
    currentSearchResults: [],
    selectedSearchIndex: -1,
    editingBookmarkId: null,
    draggedFolderId: null
  };

  // --- 2. DYNAMIC QUOTES MODULE DATA ---
  const QUOTES = [
    { text: 'Вы живете только один раз, но если вы все сделаете правильно, одного раза достаточно.', author: 'Мэй Уэст' },
    { text: 'Чем тише ты становишься, тем больше начинаешь слышать.', author: 'Рам Дасс' },
    { text: 'Кораблю безопаснее в порту, но он не для того строился.', author: 'Грейс Хоппер' },
    { text: 'Никогда не поздно стать тем, кем ты мог бы быть.', author: 'Джордж Элиот' },
    { text: 'Простота — это душа эффективности.', author: 'Остин Фриман' },
    { text: 'Секрет того, чтобы двигаться вперед — это начать.', author: 'Марк Твен' },
    { text: 'Не тратьте время на споры о том, каким должен быть хороший человек. Будьте им.', author: 'Марк Аврелий' },
    { text: 'Мутная вода лучше всего очищается, если оставить ее в покое.', author: 'Алан Уоттс' },
    { text: 'Мы чаще страдаем в воображении, чем в реальности.', author: 'Сенека' },
    { text: 'Все, что ты можешь вообразить — реально.', author: 'Пабло Пикассо' },
    { text: 'Оставайтесь голодными, оставайтесь безрассудными.', author: 'Стив Джобс' },
    { text: 'Делай, что можешь, с тем, что имеешь, там, где ты есть.', author: 'Теодор Рузвельт' }
  ];

  let currentQuoteIndex = -1;

  function renderRandomQuote() {
    if (!elements.quoteContainer || !elements.quoteText || !elements.quoteAuthor) return;
    if (QUOTES.length === 0) return;

    let newIndex;
    do {
      newIndex = Math.floor(Math.random() * QUOTES.length);
    } while (QUOTES.length > 1 && newIndex === currentQuoteIndex);
    currentQuoteIndex = newIndex;
    const quote = QUOTES[newIndex];

    elements.quoteContainer.style.opacity = '0';
    elements.quoteContainer.style.transform = 'translateY(-4px)';

    setTimeout(() => {
      elements.quoteText.textContent = `«${quote.text}»`;
      elements.quoteAuthor.textContent = `— ${quote.author}`;
      elements.quoteContainer.style.opacity = '1';
      elements.quoteContainer.style.transform = 'translateY(0)';
    }, 150);
  }

  // --- 3. RICH MOCK DATA FOR LOCAL / STANDALONE PREVIEW ---
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
              id: 'cat-ai',
              title: '🤖 AI',
              children: [
                { id: 'ai-1', title: 'ChatGPT by OpenAI', url: 'https://chatgpt.com', dateAdded: Date.now() - 1000 * 60 * 60 * 1 },
                { id: 'ai-2', title: 'Claude by Anthropic', url: 'https://claude.ai', dateAdded: Date.now() - 1000 * 60 * 60 * 4 },
                { id: 'ai-3', title: 'Perplexity AI Answer Engine', url: 'https://perplexity.ai', dateAdded: Date.now() - 1000 * 60 * 60 * 12 },
                { id: 'ai-4', title: 'Midjourney Prompt Hub', url: 'https://midjourney.com', dateAdded: Date.now() - 1000 * 60 * 60 * 24 },
                { id: 'ai-5', title: 'Hugging Face ML Community', url: 'https://huggingface.co', dateAdded: Date.now() - 1000 * 60 * 60 * 48 }
              ]
            },
            {
              id: 'cat-work',
              title: '💼 Work',
              children: [
                { id: 'work-1', title: 'Notion — All-in-one Workspace', url: 'https://notion.so', dateAdded: Date.now() - 1000 * 60 * 60 * 2 },
                { id: 'work-2', title: 'Google Drive Cloud Storage', url: 'https://drive.google.com', dateAdded: Date.now() - 1000 * 60 * 60 * 8 },
                { id: 'work-3', title: 'Figma Interface Design Tool', url: 'https://figma.com', dateAdded: Date.now() - 1000 * 60 * 60 * 16 },
                { id: 'work-4', title: 'Jira Software & Agile Sprint', url: 'https://jira.atlassian.com', dateAdded: Date.now() - 1000 * 60 * 60 * 30 },
                { id: 'work-5', title: 'Slack Workspace Chat', url: 'https://slack.com', dateAdded: Date.now() - 1000 * 60 * 60 * 60 }
              ]
            },
            {
              id: 'cat-finance',
              title: '💳 Finance',
              children: [
                { id: 'fin-1', title: 'Kaspi.kz — Банк и платежи', url: 'https://kaspi.kz', dateAdded: Date.now() - 1000 * 60 * 60 * 5 },
                { id: 'fin-2', title: 'TradingView Financial Charts', url: 'https://tradingview.com', dateAdded: Date.now() - 1000 * 60 * 60 * 14 },
                { id: 'fin-3', title: 'Binance Crypto Exchange', url: 'https://binance.com', dateAdded: Date.now() - 1000 * 60 * 60 * 28 },
                { id: 'fin-4', title: 'CoinMarketCap Crypto Tracker', url: 'https://coinmarketcap.com', dateAdded: Date.now() - 1000 * 60 * 60 * 70 },
                { id: 'fin-5', title: 'Bloomberg Global Markets', url: 'https://bloomberg.com', dateAdded: Date.now() - 1000 * 60 * 60 * 95 }
              ]
            },
            {
              id: 'cat-social',
              title: '🌐 Social',
              children: [
                { id: 'soc-1', title: 'Telegram Web Messenger', url: 'https://web.telegram.org', dateAdded: Date.now() - 1000 * 60 * 60 * 3 },
                { id: 'soc-2', title: 'Twitter / X Feed', url: 'https://x.com', dateAdded: Date.now() - 1000 * 60 * 60 * 10 },
                { id: 'soc-3', title: 'Reddit Frontpage Discussions', url: 'https://reddit.com', dateAdded: Date.now() - 1000 * 60 * 60 * 20 },
                { id: 'soc-4', title: 'Discord Web App & Chats', url: 'https://discord.com/app', dateAdded: Date.now() - 1000 * 60 * 60 * 40 },
                { id: 'soc-5', title: 'LinkedIn Professional Network', url: 'https://linkedin.com', dateAdded: Date.now() - 1000 * 60 * 60 * 80 }
              ]
            },
            {
              id: 'cat-dev',
              title: '💻 Dev',
              children: [
                { id: 'dev-1', title: 'GitHub — Where developers build', url: 'https://github.com', dateAdded: Date.now() - 1000 * 60 * 60 * 1 },
                { id: 'dev-2', title: 'Stack Overflow Q&A for Devs', url: 'https://stackoverflow.com', dateAdded: Date.now() - 1000 * 60 * 60 * 7 },
                { id: 'dev-3', title: 'MDN Web Docs Reference', url: 'https://developer.mozilla.org', dateAdded: Date.now() - 1000 * 60 * 60 * 22 },
                { id: 'dev-4', title: 'Tailwind CSS Documentation', url: 'https://tailwindcss.com', dateAdded: Date.now() - 1000 * 60 * 60 * 50 },
                { id: 'dev-5', title: 'npm — JavaScript Registry', url: 'https://npmjs.com', dateAdded: Date.now() - 1000 * 60 * 60 * 100 }
              ]
            },
            {
              id: 'cat-streaming',
              title: '📺 Streaming',
              children: [
                { id: 'str-1', title: 'YouTube — Video & Music', url: 'https://youtube.com', dateAdded: Date.now() - 1000 * 60 * 60 * 2 },
                { id: 'str-2', title: 'Twitch — Live Game Streams', url: 'https://twitch.tv', dateAdded: Date.now() - 1000 * 60 * 60 * 9 },
                { id: 'str-3', title: 'Netflix — Movies & Series', url: 'https://netflix.com', dateAdded: Date.now() - 1000 * 60 * 60 * 25 },
                { id: 'str-4', title: 'Spotify Web Music Player', url: 'https://open.spotify.com', dateAdded: Date.now() - 1000 * 60 * 60 * 55 },
                { id: 'str-5', title: 'Кинопоиск — Фильмы и премьеры', url: 'https://kinopoisk.ru', dateAdded: Date.now() - 1000 * 60 * 60 * 90 }
              ]
            }
          ]
        }
      ]
    }
  ];

  // --- 4. DOM ELEMENTS CACHE ---
  const elements = {
    // Background Layers & Dimming
    bgVideo: document.getElementById('bg-video'),
    bgOverlay: document.getElementById('bg-overlay'),

    // Top Bar Floating Blocks
    boardsPillsWrapper: document.getElementById('boards-pills-wrapper'),
    btnAddBoard: document.getElementById('btn-add-board'),
    globalSearchInput: document.getElementById('global-search-input'),
    searchGoogleBtn: document.getElementById('search-google-btn'),
    widgetLocation: document.getElementById('widget-location'),
    widgetWeatherIcon: document.getElementById('widget-weather-icon'),
    widgetTemp: document.getElementById('widget-temp'),
    widgetDate: document.getElementById('widget-date'),
    widgetTime: document.getElementById('widget-time'),

    // Main Viewport & Cards Container
    mainViewport: document.getElementById('main-viewport'),
    quoteContainer: document.getElementById('quote-container'),
    quoteText: document.getElementById('quote-text'),
    quoteAuthor: document.getElementById('quote-author'),
    cardsContainer: document.getElementById('cards-container'),
    emptyState: document.getElementById('empty-state'),
    emptyStateTitle: document.getElementById('empty-state-title'),
    emptyStateDesc: document.getElementById('empty-state-desc'),
    btnEmptyAdd: document.getElementById('btn-empty-add'),

    // Floating Bottom Controls
    bgChangeBtn: document.getElementById('bg-change-btn'),
    bgFileInput: document.getElementById('bg-file-input'),
    floatingSettingsBtn: document.getElementById('floating-settings-btn'),

    // Search Palette Modal
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
    overlayOpacitySlider: document.getElementById('overlay-opacity-slider'),
    overlayOpacityValue: document.getElementById('overlay-opacity-value'),
    settingsStatBookmarks: document.getElementById('settings-stat-bookmarks'),
    settingsStatFolders: document.getElementById('settings-stat-folders'),

    // Toast Container
    toastContainer: document.getElementById('toast-container')
  };

  // --- 5. NATIVE INDEXEDDB WALLPAPER ENGINE ---
  const WallpaperDB = {
    db: null,

    async init() {
      if (this.db) return this.db;
      return new Promise((resolve, reject) => {
        const req = indexedDB.open('NovaTabDB', 1);
        req.onupgradeneeded = (e) => {
          const db = e.target.result;
          if (!db.objectStoreNames.contains('wallpapers')) {
            db.createObjectStore('wallpapers');
          }
        };
        req.onsuccess = (e) => {
          this.db = e.target.result;
          resolve(this.db);
        };
        req.onerror = (e) => {
          console.error('[WallpaperDB] Open error:', e);
          reject(e);
        };
      });
    },

    async save(blob, type, name = '') {
      try {
        const db = await this.init();
        return new Promise((resolve, reject) => {
          const tx = db.transaction('wallpapers', 'readwrite');
          const store = tx.objectStore('wallpapers');
          const record = { blob, type, name, updatedAt: Date.now() };
          const req = store.put(record, 'activeWallpaper');
          req.onsuccess = () => resolve(true);
          req.onerror = (e) => reject(e);
        });
      } catch (e) {
        console.error('[WallpaperDB] Save error:', e);
        throw e;
      }
    },

    async get() {
      try {
        const db = await this.init();
        return new Promise((resolve, reject) => {
          const tx = db.transaction('wallpapers', 'readonly');
          const store = tx.objectStore('wallpapers');
          const req = store.get('activeWallpaper');
          req.onsuccess = (e) => resolve(e.target.result || null);
          req.onerror = (e) => reject(e);
        });
      } catch (e) {
        console.error('[WallpaperDB] Get error:', e);
        return null;
      }
    },

    async clear() {
      try {
        const db = await this.init();
        return new Promise((resolve, reject) => {
          const tx = db.transaction('wallpapers', 'readwrite');
          const store = tx.objectStore('wallpapers');
          const req = store.delete('activeWallpaper');
          req.onsuccess = () => resolve(true);
          req.onerror = (e) => reject(e);
        });
      } catch (e) {
        console.error('[WallpaperDB] Clear error:', e);
      }
    }
  };

  // --- 6. UTILITIES ---
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
      return `chrome-extension://${chrome.runtime.id}/_favicon/?pageUrl=${encodeURIComponent(pageUrl)}&size=32`;
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

  function isRootOrSystemFolder(folder) {
    if (!folder) return true;
    const fid = String(folder.id);
    const sysIds = new Set(['0', '1', '2', 'root', 'mobile', 'other', 'synced']);
    if (sysIds.has(fid)) return true;
    if (folder.isSystem) return true;
    if (folder.parentId === '0' || !folder.parentId) return true;
    return false;
  }

  // --- 7. LIVE CLOCK & WEATHER WIDGET ENGINE ---
  function updateLiveClockAndDate() {
    const now = new Date();
    if (elements.widgetTime) {
      const h = String(now.getHours()).padStart(2, '0');
      const m = String(now.getMinutes()).padStart(2, '0');
      elements.widgetTime.textContent = `${h}:${m}`;
    }
    if (elements.widgetDate) {
      const weekdays = ['Вс', 'Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб'];
      const months = ['янв', 'фев', 'мар', 'апр', 'мая', 'июн', 'июл', 'авг', 'сен', 'окт', 'ноя', 'дек'];
      const w = weekdays[now.getDay()];
      const d = now.getDate();
      const m = months[now.getMonth()];
      elements.widgetDate.textContent = `${w}, ${d} ${m}`;
    }
  }

  // --- 8. WALLPAPER ENGINE (VIDEO & IMAGE + INDEXEDDB) ---
  let currentVideoBlobUrl = null;
  let currentImageBlobUrl = null;

  function stopAndHideVideo() {
    if (elements.bgVideo) {
      elements.bgVideo.pause();
      elements.bgVideo.removeAttribute('src');
      elements.bgVideo.load();
      elements.bgVideo.classList.add('hidden');
    }
    if (currentVideoBlobUrl) {
      URL.revokeObjectURL(currentVideoBlobUrl);
      currentVideoBlobUrl = null;
    }
  }

  function setVideoWallpaper(blob) {
    stopAndHideVideo();
    if (currentImageBlobUrl) {
      URL.revokeObjectURL(currentImageBlobUrl);
      currentImageBlobUrl = null;
    }
    currentVideoBlobUrl = URL.createObjectURL(blob);
    if (elements.bgVideo) {
      elements.bgVideo.src = currentVideoBlobUrl;
      elements.bgVideo.classList.remove('hidden');
      elements.bgVideo.play().catch(err => console.warn('[NovaTab] Video playback notification:', err));
    }
    document.body.style.backgroundImage = 'none';
  }

  function applyImageBackground(dataOrBlobUrl) {
    stopAndHideVideo();
    if (dataOrBlobUrl) {
      document.body.style.backgroundImage = `url("${dataOrBlobUrl}")`;
    } else {
      document.body.style.backgroundImage = '';
    }
  }

  function setOverlayOpacity(val) {
    const num = Math.max(0, Math.min(0.85, parseFloat(val) || 0.30));
    document.documentElement.style.setProperty('--overlay-opacity', num.toString());
    if (elements.overlayOpacitySlider) {
      elements.overlayOpacitySlider.value = num.toString();
    }
    if (elements.overlayOpacityValue) {
      elements.overlayOpacityValue.textContent = `${Math.round(num * 100)}%`;
    }
  }

  function handleOverlayOpacityChange(e) {
    const val = parseFloat(e.target.value);
    setOverlayOpacity(val);
    if (state.isExtension && chrome.storage?.local) {
      chrome.storage.local.set({ overlayOpacity: val });
    } else {
      localStorage.setItem('novatab_overlayOpacity', val.toString());
    }
  }

  async function loadSavedBackground() {
    try {
      // 1. Try loading from native IndexedDB
      const savedWallpaper = await WallpaperDB.get();
      if (savedWallpaper && savedWallpaper.blob) {
        if (savedWallpaper.type === 'video' || (savedWallpaper.blob.type && savedWallpaper.blob.type.startsWith('video/'))) {
          setVideoWallpaper(savedWallpaper.blob);
        } else {
          if (currentImageBlobUrl) URL.revokeObjectURL(currentImageBlobUrl);
          currentImageBlobUrl = URL.createObjectURL(savedWallpaper.blob);
          applyImageBackground(currentImageBlobUrl);
        }
      } else {
        // Fallback to storage
        if (state.isExtension && chrome.storage?.local) {
          const stored = await chrome.storage.local.get(['customBackground', 'wallpaperType']);
          if (stored.customBackground && stored.wallpaperType !== 'video') {
            applyImageBackground(stored.customBackground);
          }
        } else {
          const savedBg = localStorage.getItem('novatab_customBackground');
          const savedType = localStorage.getItem('novatab_wallpaperType');
          if (savedBg && savedType !== 'video') {
            applyImageBackground(savedBg);
          }
        }
      }

      // 2. Load Overlay Opacity
      let overlayOpacity = 0.30;
      if (state.isExtension && chrome.storage?.local) {
        const stored = await chrome.storage.local.get(['overlayOpacity']);
        if (stored.overlayOpacity !== undefined) overlayOpacity = parseFloat(stored.overlayOpacity);
      } else {
        const savedOpacity = localStorage.getItem('novatab_overlayOpacity');
        if (savedOpacity !== null) overlayOpacity = parseFloat(savedOpacity);
      }
      setOverlayOpacity(overlayOpacity);

    } catch (e) {
      console.warn('[NovaTab] Failed loading background from storage/IndexedDB:', e);
    }
  }

  async function handleBgFileSelect(e) {
    const file = e.target.files?.[0];
    if (!file) return;

    if (file.type.startsWith('video/')) {
      try {
        await WallpaperDB.save(file, 'video', file.name);
        setVideoWallpaper(file);
        if (state.isExtension && chrome.storage?.local) {
          chrome.storage.local.set({ wallpaperType: 'video' });
          chrome.storage.local.remove('customBackground');
        } else {
          localStorage.setItem('novatab_wallpaperType', 'video');
          localStorage.removeItem('novatab_customBackground');
        }
        showToast('Видео-обои установлены!');
      } catch (err) {
        console.error('[NovaTab] Failed saving video wallpaper:', err);
        showToast('Ошибка сохранения видео', 'error');
      }
    } else if (file.type.startsWith('image/')) {
      const reader = new FileReader();
      reader.onload = (event) => {
        const img = new Image();
        img.onload = () => {
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

          canvas.toBlob(async (blob) => {
            if (blob) {
              await WallpaperDB.save(blob, 'image', file.name);
            }
          }, 'image/webp', 0.8);

          if (state.isExtension && chrome.storage?.local) {
            chrome.storage.local.set({ customBackground: dataUrl, wallpaperType: 'image' }, () => {
              applyImageBackground(dataUrl);
              showToast('Пользовательский фон успешно сохранен!');
            });
          } else {
            try {
              localStorage.setItem('novatab_customBackground', dataUrl);
              localStorage.setItem('novatab_wallpaperType', 'image');
              applyImageBackground(dataUrl);
              showToast('Пользовательский фон успешно сохранен!');
            } catch (storageErr) {
              console.warn('[NovaTab] LocalStorage quota exceeded:', storageErr);
              applyImageBackground(dataUrl);
              showToast('Фон применен на текущую сессию');
            }
          }
        };
        img.src = event.target.result;
      };
      reader.readAsDataURL(file);
    } else {
      showToast('Неподдерживаемый формат файла', 'error');
    }
    e.target.value = '';
  }

  async function resetBackground() {
    stopAndHideVideo();
    if (currentImageBlobUrl) {
      URL.revokeObjectURL(currentImageBlobUrl);
      currentImageBlobUrl = null;
    }
    await WallpaperDB.clear();
    if (state.isExtension && chrome.storage?.local) {
      chrome.storage.local.remove(['customBackground', 'wallpaperType'], () => {
        applyImageBackground(null);
        showToast('Фон сброшен по умолчанию');
      });
    } else {
      localStorage.removeItem('novatab_customBackground');
      localStorage.removeItem('novatab_wallpaperType');
      applyImageBackground(null);
      showToast('Фон сброшен по умолчанию');
    }
  }

  // --- 9. BOOKMARK TREE PARSER & LOADER ---
  let isLoadingBookmarks = false;
  let pendingBookmarkReload = false;

  function parseBookmarkNodes(nodes, parentPath = [], parentId = null, depth = 0, collections = null) {
    if (!collections) {
      collections = {
        tempAllBookmarks: [],
        tempAllFolders: [],
        tempFolderMap: new Map(),
        tempBookmarksByFolder: new Map()
      };
    }

    if (!nodes || !Array.isArray(nodes)) return collections;

    for (const node of nodes) {
      if (!node) continue;
      const nodeId = String(node.id);

      if (node.url) {
        // It is a bookmark node
        const bm = {
          id: nodeId,
          parentId: String(node.parentId || parentId || '1'),
          title: node.title || extractHostname(node.url) || 'Без названия',
          url: node.url,
          dateAdded: node.dateAdded || Date.now(),
          hostname: extractHostname(node.url),
          folderName: (parentPath && parentPath.length > 0) ? parentPath[parentPath.length - 1] : 'Панель закладок'
        };
        collections.tempAllBookmarks.push(bm);
        const pId = bm.parentId;
        if (!collections.tempBookmarksByFolder.has(pId)) {
          collections.tempBookmarksByFolder.set(pId, []);
        }
        collections.tempBookmarksByFolder.get(pId).push(bm);
      } else {
        // It is a folder node
        const title = node.title || (nodeId === '0' ? 'Root' : (nodeId === '1' ? 'Панель закладок' : (nodeId === '2' ? 'Другие закладки' : 'Папка')));
        const isRootWrapper = (nodeId === '0' || title === 'Root');
        const currentPath = isRootWrapper ? [] : [...parentPath, title];
        
        let nextDepth = depth;
        if (!isRootWrapper) {
          if (parentId === '0' || !parentId) {
            nextDepth = 0;
          } else {
            nextDepth = depth;
          }
        }

        const childFolderIds = [];
        if (node.children && Array.isArray(node.children)) {
          for (const child of node.children) {
            if (!child.url) {
              childFolderIds.push(String(child.id));
            }
          }
        }

        const folderObj = {
          id: nodeId,
          title: title,
          parentId: node.parentId ? String(node.parentId) : (parentId ? String(parentId) : null),
          path: currentPath,
          depth: nextDepth,
          childrenFolderIds: childFolderIds,
          isSystem: isRootWrapper || nodeId === '1' || nodeId === '2' || nodeId === 'mobile' || parentId === '0' || !parentId
        };

        collections.tempAllFolders.push(folderObj);
        collections.tempFolderMap.set(nodeId, folderObj);
        if (!collections.tempBookmarksByFolder.has(nodeId)) {
          collections.tempBookmarksByFolder.set(nodeId, []);
        }

        if (node.children && Array.isArray(node.children) && node.children.length > 0) {
          parseBookmarkNodes(
            node.children,
            currentPath,
            nodeId,
            isRootWrapper ? 0 : (parentId === '0' || !parentId ? 1 : nextDepth + 1),
            collections
          );
        }
      }
    }

    return collections;
  }

  async function loadBookmarks() {
    if (isLoadingBookmarks) {
      pendingBookmarkReload = true;
      return;
    }
    isLoadingBookmarks = true;

    try {
      let tree = [];
      if (state.isExtension && chrome.bookmarks?.getTree) {
        tree = await chrome.bookmarks.getTree();
      } else {
        tree = MOCK_BOOKMARK_TREE;
      }

      const collections = {
        tempAllBookmarks: [],
        tempAllFolders: [],
        tempFolderMap: new Map(),
        tempBookmarksByFolder: new Map()
      };

      parseBookmarkNodes(tree, [], null, 0, collections);

      // Atomically update state
      state.rawTree = tree;
      state.allBookmarks = collections.tempAllBookmarks;
      state.allFolders = collections.tempAllFolders;
      state.folderMap = collections.tempFolderMap;
      state.bookmarksByFolder = collections.tempBookmarksByFolder;

      populateFolderSelectDropdowns();
      renderBoardsPills();
      renderCardsView();
      updateStatsDisplay();
    } catch (err) {
      console.error('[NovaTab] Failed to load bookmarks:', err);
      showToast('Ошибка загрузки закладок', 'error');
    } finally {
      isLoadingBookmarks = false;
      if (pendingBookmarkReload) {
        pendingBookmarkReload = false;
        await loadBookmarks();
      }
    }
  }

  // --- 10. RENDERING BOARDS (TOP NAV PILLS & DRAG AND DROP) ---
  function renderBoardsPills() {
    if (!elements.boardsPillsWrapper) return;
    elements.boardsPillsWrapper.innerHTML = '';

    // 1. "✦ Home" Master Pill (Non-draggable)
    const allPill = document.createElement('button');
    allPill.className = `nav-pill-btn ${state.activeBoardId === 'all' ? 'active' : ''}`;
    allPill.setAttribute('data-board', 'all');
    allPill.innerHTML = `<span>✦</span><span>Home</span>`;
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
      const canDelete = !isRootOrSystemFolder(folder);

      const pill = document.createElement('button');
      pill.className = `nav-pill-btn ${isActive ? 'active' : ''}`;
      pill.setAttribute('data-board', folder.id);
      pill.setAttribute('draggable', 'true');

      pill.innerHTML = `
        <span class="pill-title" title="${escapeHtml(folder.title)}">${escapeHtml(folder.title)}</span>
        <span class="pill-count">${bCount}</span>
        ${canDelete ? `<span class="delete-board-btn" title="Удалить категорию «${escapeHtml(folder.title)}»">×</span>` : ''}
      `;

      // Drag and Drop listeners for pill reordering
      pill.addEventListener('dragstart', (e) => {
        state.draggedFolderId = folder.id;
        e.dataTransfer.setData('text/plain', folder.id);
        e.dataTransfer.effectAllowed = 'move';
        pill.classList.add('dragging');
      });

      pill.addEventListener('dragover', (e) => {
        e.preventDefault();
        e.dataTransfer.dropEffect = 'move';
        if (state.draggedFolderId && state.draggedFolderId !== folder.id) {
          pill.classList.add('drag-over');
        }
      });

      pill.addEventListener('dragleave', () => {
        pill.classList.remove('drag-over');
      });

      pill.addEventListener('dragend', () => {
        state.draggedFolderId = null;
        if (elements.boardsPillsWrapper) {
          elements.boardsPillsWrapper.querySelectorAll('.nav-pill-btn').forEach(p => {
            p.classList.remove('dragging', 'drag-over');
          });
        }
      });

      pill.addEventListener('drop', async (e) => {
        e.preventDefault();
        pill.classList.remove('drag-over');
        const draggedId = e.dataTransfer.getData('text/plain') || state.draggedFolderId;
        const targetId = folder.id;

        if (!draggedId || draggedId === targetId) return;

        const draggedFolder = state.folderMap.get(draggedId);
        const targetFolder = state.folderMap.get(targetId);
        if (!draggedFolder || !targetFolder) return;

        try {
          if (state.isExtension) {
            const parentId = targetFolder.parentId || '1';
            const children = await chrome.bookmarks.getChildren(parentId);
            const targetIndex = children.findIndex(c => c.id === targetId);
            if (targetIndex !== -1) {
              await chrome.bookmarks.move(draggedId, { parentId, index: targetIndex });
            }
          } else {
            // Reorder in local mock mode
            const draggedIdx = state.allFolders.findIndex(f => f.id === draggedId);
            const targetIdx = state.allFolders.findIndex(f => f.id === targetId);
            if (draggedIdx !== -1 && targetIdx !== -1) {
              const [removed] = state.allFolders.splice(draggedIdx, 1);
              state.allFolders.splice(targetIdx, 0, removed);
            }

            const parentNode = findNodeInTree(MOCK_BOOKMARK_TREE, targetFolder.parentId || '1') || (MOCK_BOOKMARK_TREE[0]?.children?.[0]);
            if (parentNode && parentNode.children) {
              const mDraggedIdx = parentNode.children.findIndex(c => c.id === draggedId);
              const mTargetIdx = parentNode.children.findIndex(c => c.id === targetId);
              if (mDraggedIdx !== -1 && mTargetIdx !== -1) {
                const [removed] = parentNode.children.splice(mDraggedIdx, 1);
                parentNode.children.splice(mTargetIdx, 0, removed);
              }
            }
          }

          showToast('Порядок категорий обновлен!');
          await loadBookmarks();
        } catch (err) {
          console.error('[NovaTab] Failed to reorder folder:', err);
          showToast('Ошибка при перемещении категории', 'error');
        }
      });

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

  // --- 11. RENDERING CATEGORY GLASS CARDS ---
  function renderCardsView() {
    elements.cardsContainer.innerHTML = '';

    // Determine which folders to render cards for
    let rawFolders = [];

    if (state.activeBoardId === 'all') {
      // Find all folders that have bookmarks or subfolders (excluding system roots)
      rawFolders = state.allFolders.filter(f => {
        if (isRootOrSystemFolder(f) && (f.id === '0' || f.id === '1' || f.id === '2' || f.id === 'mobile')) {
          return false;
        }
        const directBookmarks = state.bookmarksByFolder.get(f.id) || [];
        return directBookmarks.length > 0 || (f.depth === 1);
      });

      // Check if there are loose bookmarks directly on root parent (id: '1')
      const rootBookmarks = state.bookmarksByFolder.get('1') || [];
      if (rootBookmarks.length > 0) {
        rawFolders.unshift({
          id: '1',
          title: '⭐ Быстрый доступ',
          path: ['Панель закладок'],
          depth: 0,
          childrenFolderIds: [],
          isSystem: true
        });
      }
    } else {
      const selected = state.folderMap.get(state.activeBoardId);
      if (selected) {
        rawFolders.push(selected);
        if (selected.childrenFolderIds && selected.childrenFolderIds.length > 0) {
          selected.childrenFolderIds.forEach(cid => {
            const childF = state.folderMap.get(cid);
            if (childF) rawFolders.push(childF);
          });
        }
      }
    }

    // Strictly deduplicate targetFolders by folder ID
    const seenCardFolderIds = new Set();
    const targetFolders = [];
    for (const folder of rawFolders) {
      if (!folder || !folder.id) continue;
      const fid = String(folder.id);
      if (!seenCardFolderIds.has(fid)) {
        seenCardFolderIds.add(fid);
        targetFolders.push(folder);
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
    card.className = 'glass-panel category-card';
    card.setAttribute('data-folder-id', folder.id);

    // Card Header
    const header = document.createElement('div');
    header.className = 'card-header-bar';
    const canDeleteFolder = !isRootOrSystemFolder(folder);
    header.innerHTML = `
      <div class="card-title-group">
        <h3 class="card-category-title" title="${escapeHtml(folder.title)}">${escapeHtml(folder.title)}</h3>
        <span class="card-badge">${bookmarks.length}</span>
      </div>
      <div class="card-header-actions">
        <button class="card-icon-btn btn-quick-add" title="Добавить закладку в «${escapeHtml(folder.title)}»">
          <svg class="icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2">
            <line x1="12" y1="5" x2="12" y2="19"></line>
            <line x1="5" y1="12" x2="19" y2="12"></line>
          </svg>
        </button>
        ${canDeleteFolder ? `
        <button class="card-icon-btn btn-delete-card" title="Удалить категорию «${escapeHtml(folder.title)}»">
          <svg class="icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2">
            <polyline points="3 6 5 6 21 6"></polyline>
            <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path>
          </svg>
        </button>
        ` : (bookmarks.length > 0 ? `
        <button class="card-icon-btn btn-clear-card" title="Очистить все закладки из «${escapeHtml(folder.title)}»">
          <svg class="icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2">
            <polyline points="3 6 5 6 21 6"></polyline>
            <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path>
            <line x1="10" y1="11" x2="10" y2="17"></line>
            <line x1="14" y1="11" x2="14" y2="17"></line>
          </svg>
        </button>
        ` : '')}
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

    const clearFolderBtn = header.querySelector('.btn-clear-card');
    if (clearFolderBtn) {
      clearFolderBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        clearCategoryBookmarks(folder, bookmarks);
      });
    }

    card.appendChild(header);

    // Bookmark Items Container
    const itemsList = document.createElement('div');
    itemsList.className = 'category-bookmarks-list';

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
    row.className = 'bookmark-row-item';
    row.setAttribute('data-bookmark-id', bookmark.id);

    const faviconSrc = getFaviconUrl(bookmark.url);
    const hostColor = hashStringColor(bookmark.hostname || 'site');
    const firstLetter = (bookmark.title || bookmark.hostname || 'N').charAt(0).toUpperCase();

    row.innerHTML = `
      <div class="bookmark-row-left">
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
        <span class="bookmark-row-title" title="${escapeHtml(bookmark.title)}">${escapeHtml(bookmark.title)}</span>
      </div>

      <div class="bookmark-row-actions">
        <button class="bookmark-row-action-btn action-edit" title="Редактировать">
          <svg class="icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2">
            <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"></path>
            <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"></path>
          </svg>
        </button>
        <button class="bookmark-row-action-btn btn-delete action-delete" title="Удалить">
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
      if (e.target.closest('.bookmark-row-actions')) return;
      openUrl(bookmark.url);
    });

    // Action handlers
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

  // --- 12. SEARCH PALETTE MODAL ENGINE ---
  function openSearchModal(initialQuery = '') {
    elements.searchModal.classList.add('open');
    elements.searchModalInput.value = initialQuery;
    elements.searchModalInput.focus();
    state.selectedSearchIndex = 0;
    renderSearchResults(initialQuery);
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

  // --- 13. CRUD MODALS & ACTIONS ---
  function populateFolderSelectDropdowns() {
    if (!elements.folderParentSelect || !elements.modalBookmarkFolder) return;
    elements.folderParentSelect.innerHTML = '';
    elements.modalBookmarkFolder.innerHTML = '';

    const validFolders = state.allFolders.filter(f => f && f.id !== '0' && f.title !== 'Root');

    if (validFolders.length === 0) {
      const opt = document.createElement('option');
      opt.value = '1';
      opt.textContent = 'Панель закладок';
      elements.modalBookmarkFolder.appendChild(opt);
      elements.folderParentSelect.appendChild(opt.cloneNode(true));
      return;
    }

    validFolders.forEach(folder => {
      const opt1 = document.createElement('option');
      opt1.value = String(folder.id);
      const indent = '— '.repeat(folder.depth || 0);
      opt1.textContent = `${indent}${folder.title}`;
      elements.modalBookmarkFolder.appendChild(opt1);

      const opt2 = opt1.cloneNode(true);
      elements.folderParentSelect.appendChild(opt2);
    });

    const defaultFolderId = validFolders.some(f => String(f.id) === '1') ? '1' : String(validFolders[0].id);
    elements.folderParentSelect.value = defaultFolderId;
    elements.modalBookmarkFolder.value = defaultFolderId;
  }

  function openAddModal(targetFolderId = null) {
    populateFolderSelectDropdowns();
    state.editingBookmarkId = null;
    elements.modalTitle.textContent = 'Добавить закладку';
    elements.modalBookmarkId.value = '';
    elements.modalBookmarkTitle.value = '';
    elements.modalBookmarkUrl.value = '';

    if (targetFolderId && state.folderMap.has(String(targetFolderId))) {
      elements.modalBookmarkFolder.value = String(targetFolderId);
    } else if (state.activeBoardId !== 'all' && state.folderMap.has(String(state.activeBoardId))) {
      elements.modalBookmarkFolder.value = String(state.activeBoardId);
    } else {
      elements.modalBookmarkFolder.value = '1';
      if (!elements.modalBookmarkFolder.value && state.allFolders.length > 0) {
        const valid = state.allFolders.filter(f => f && f.id !== '0' && f.title !== 'Root');
        if (valid.length > 0) elements.modalBookmarkFolder.value = String(valid[0].id);
      }
    }

    elements.bookmarkModal.classList.add('open');
    elements.modalBookmarkTitle.focus();
  }

  function openEditModal(bookmark) {
    populateFolderSelectDropdowns();
    state.editingBookmarkId = bookmark.id;
    elements.modalTitle.textContent = 'Редактировать закладку';
    elements.modalBookmarkId.value = bookmark.id;
    elements.modalBookmarkTitle.value = bookmark.title;
    elements.modalBookmarkUrl.value = bookmark.url;
    elements.modalBookmarkFolder.value = bookmark.parentId || (state.allFolders.find(f => f.id !== '0')?.id || '1');

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
    if (!folder || !folder.id || isRootOrSystemFolder(folder)) {
      showToast('Системную папку браузера нельзя удалить', 'info');
      return;
    }

    if (!window.confirm(`Вы уверены, что хотите удалить категорию «${folder.title}» и все её закладки?`)) {
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

      showToast(`Категория «${folder.title}» удалена`);
      await loadBookmarks();
    } catch (err) {
      console.error('[NovaTab] Delete folder error:', err);
      const isRootError = err?.message && (err.message.includes('root') || err.message.includes('modify'));
      showToast(isRootError ? 'Системную папку браузера нельзя удалить' : 'Ошибка при удалении категории', 'error');
    }
  }

  async function clearCategoryBookmarks(folder, bookmarks) {
    if (!folder || !bookmarks || bookmarks.length === 0) return;

    if (!window.confirm(`Очистить все ${bookmarks.length} закладок из категории «${folder.title}»?`)) {
      return;
    }

    try {
      if (state.isExtension) {
        for (const bm of bookmarks) {
          try {
            await chrome.bookmarks.remove(bm.id);
          } catch (bmErr) {
            console.warn(`[NovaTab] Failed to remove bookmark ${bm.id}:`, bmErr);
          }
        }
      } else {
        bookmarks.forEach(bm => {
          removeNodeFromTree(MOCK_BOOKMARK_TREE, bm.id);
        });
        const bookmarkIds = new Set(bookmarks.map(b => b.id));
        state.allBookmarks = state.allBookmarks.filter(b => !bookmarkIds.has(b.id));
        state.bookmarksByFolder.set(folder.id, []);
      }

      showToast(`Закладки из «${folder.title}» очищены`);
      await loadBookmarks();
    } catch (err) {
      console.error('[NovaTab] Clear category bookmarks error:', err);
      showToast('Ошибка при очистке закладок', 'error');
    }
  }

  // --- 14. FOLDER CREATION MODAL ---
  function openFolderModal() {
    populateFolderSelectDropdowns();
    const validFolders = state.allFolders.filter(f => f && f.id !== '0' && f.title !== 'Root');
    const defaultId = validFolders.some(f => String(f.id) === '1') ? '1' : (validFolders[0] ? String(validFolders[0].id) : '1');
    elements.folderParentSelect.value = defaultId;
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
    let parentId = elements.folderParentSelect.value;
    if (!parentId || !state.folderMap.has(String(parentId))) {
      parentId = '1';
    }

    if (!title) return;

    try {
      if (state.isExtension) {
        await chrome.bookmarks.create({
          parentId: String(parentId),
          title
        });
      } else {
        const newFolderId = 'mock-folder-' + Date.now();
        const parentFolder = state.folderMap.get(String(parentId));
        const folderObj = {
          id: newFolderId,
          title,
          parentId: String(parentId),
          path: parentFolder ? [...parentFolder.path, title] : [title],
          depth: parentFolder ? (parentFolder.depth + 1) : 1,
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
      showToast(`Категория «${title}» создана!`);
      closeFolderModal();
      await loadBookmarks();
    } catch (err) {
      console.error('[NovaTab] Create folder error:', err);
      showToast('Ошибка создания категории', 'error');
    }
  }

  // --- 15. SETTINGS MODAL ---
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

  // --- 16. EVENT LISTENERS INITIALIZATION ---
  function setupEventListeners() {
    // Dynamic Quote Module Click Event -> Roll new quote
    if (elements.quoteContainer) {
      elements.quoteContainer.addEventListener('click', renderRandomQuote);
    }

    // Top Search Input Enter key -> Direct Google Search redirect
    if (elements.globalSearchInput) {
      elements.globalSearchInput.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
          const query = elements.globalSearchInput.value.trim();
          if (query) {
            window.location.href = 'https://www.google.com/search?q=' + encodeURIComponent(query);
          }
        }
      });
    }

    // Top Search Google Button click -> Redirect to Google Search
    if (elements.searchGoogleBtn) {
      elements.searchGoogleBtn.addEventListener('click', () => {
        const query = elements.globalSearchInput.value.trim();
        if (query) {
          window.location.href = 'https://www.google.com/search?q=' + encodeURIComponent(query);
        } else {
          elements.globalSearchInput.focus();
        }
      });
    }

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

    // Floating Gear button -> Open Settings Modal
    if (elements.floatingSettingsBtn) {
      elements.floatingSettingsBtn.addEventListener('click', openSettingsModal);
    }

    // Add board button in top bar
    if (elements.btnAddBoard) {
      elements.btnAddBoard.addEventListener('click', openFolderModal);
    }

    // Empty state add button
    if (elements.btnEmptyAdd) {
      elements.btnEmptyAdd.addEventListener('click', () => openAddModal());
    }

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
      // Press '/' to open fast search palette if not in another modal or input
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

    // Settings Opacity Slider listener
    if (elements.overlayOpacitySlider) {
      elements.overlayOpacitySlider.addEventListener('input', handleOverlayOpacityChange);
    }

    // Chrome Live Bookmarks Reactive Sync
    if (state.isExtension) {
      chrome.bookmarks.onCreated.addListener(() => loadBookmarks());
      chrome.bookmarks.onRemoved.addListener(() => loadBookmarks());
      chrome.bookmarks.onChanged.addListener(() => loadBookmarks());
      chrome.bookmarks.onMoved.addListener(() => loadBookmarks());
    }
  }

  // --- 17. BOOTSTRAP INITIALIZATION ---
  async function init() {
    console.log(`[NovaTab] Initializing Aesthetic Glassmorphism Dashboard (Extension mode: ${state.isExtension})`);
    updateLiveClockAndDate();
    setInterval(updateLiveClockAndDate, 1000);
    renderRandomQuote();
    setupEventListeners();
    await loadSavedBackground();
    await loadBookmarks();
  }

  document.addEventListener('DOMContentLoaded', init);
})();

