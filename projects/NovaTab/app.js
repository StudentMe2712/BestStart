/**
 * NovaTab — Core Application Engine
 * Pure Vanilla JS, Manifest V3 CSP Compliant
 */

(function () {
  'use strict';

  // --- Constants & Defaults ---
  const STORAGE_KEY = 'novatab_state';
  const FALLBACK_FAVICON = "data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' width='16' height='16' viewBox='0 0 24 24' fill='none' stroke='%23666' stroke-width='2'><circle cx='12' cy='12' r='10'/><line x1='2' y1='12' x2='22' y2='12'/><path d='M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10z'/></svg>";

  const DEFAULT_STATE = {
    activeTabId: 'main',
    tabs: [
      {
        id: 'main',
        title: 'Главная',
        boards: [
          {
            id: 'test',
            title: 'test',
            links: [
              {
                id: 'link-1',
                title: 'old.yummyani.me',
                url: 'https://old.yummyani.me'
              }
            ]
          }
        ]
      }
    ]
  };

  // --- Application State ---
  let appState = JSON.parse(JSON.stringify(DEFAULT_STATE));
  let contextMenuTarget = null; // { type: 'link'|'board'|'tab', tabId, boardId, linkId, url, title }
  let modalAction = null; // callback (data) => void

  // --- DOM Elements ---
  const tabsGroup = document.getElementById('tabsGroup');
  const addTabBtn = document.getElementById('addTabBtn');
  const searchInput = document.getElementById('searchInput');
  const boardsGrid = document.getElementById('boardsGrid');
  const addBoardPlaceholder = document.getElementById('addBoardPlaceholder');
  const contextMenu = document.getElementById('contextMenu');
  const itemModal = document.getElementById('itemModal');
  const modalForm = document.getElementById('modalForm');
  const modalTitle = document.getElementById('modalTitle');
  const modalInputTitle = document.getElementById('modalInputTitle');
  const modalInputUrl = document.getElementById('modalInputUrl');
  const urlGroup = document.getElementById('urlGroup');
  const modalCancelBtn = document.getElementById('modalCancelBtn');
  const menuBtn = document.getElementById('menuBtn');
  const settingsBtn = document.getElementById('settingsBtn');
  const toast = document.getElementById('toast');

  // --- Utility Functions ---
  function generateId(prefix = 'id') {
    return `${prefix}-${Date.now()}-${Math.random().toString(36).substr(2, 6)}`;
  }

  function normalizeUrl(url) {
    if (!url) return '';
    let trimmed = url.trim();
    if (!/^https?:\/\//i.test(trimmed)) {
      trimmed = 'https://' + trimmed;
    }
    return trimmed;
  }

  function getDomain(url) {
    try {
      const parsed = new URL(normalizeUrl(url));
      return parsed.hostname;
    } catch {
      return url;
    }
  }

  function getFaviconUrl(url) {
    const domain = getDomain(url);
    if (!domain) return FALLBACK_FAVICON;
    return `https://www.google.com/s2/favicons?domain=${encodeURIComponent(domain)}&sz=32`;
  }

  let toastTimer = null;
  function showToast(message, duration = 2500) {
    if (!toast) return;
    toast.textContent = message;
    toast.classList.add('show');
    if (toastTimer) clearTimeout(toastTimer);
    toastTimer = setTimeout(() => {
      toast.classList.remove('show');
    }, duration);
  }

  // --- Storage Operations ---
  function loadState(callback) {
    if (typeof chrome !== 'undefined' && chrome.storage && chrome.storage.local) {
      chrome.storage.local.get([STORAGE_KEY], (result) => {
        if (result && result[STORAGE_KEY]) {
          appState = result[STORAGE_KEY];
        } else {
          appState = JSON.parse(JSON.stringify(DEFAULT_STATE));
        }
        callback();
      });
    } else {
      try {
        const stored = localStorage.getItem(STORAGE_KEY);
        if (stored) {
          appState = JSON.parse(stored);
        } else {
          appState = JSON.parse(JSON.stringify(DEFAULT_STATE));
        }
      } catch (e) {
        console.error('Error loading state from localStorage', e);
        appState = JSON.parse(JSON.stringify(DEFAULT_STATE));
      }
      callback();
    }
  }

  function saveState() {
    if (typeof chrome !== 'undefined' && chrome.storage && chrome.storage.local) {
      chrome.storage.local.set({ [STORAGE_KEY]: appState });
    } else {
      try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(appState));
      } catch (e) {
        console.error('Error saving state to localStorage', e);
      }
    }
  }

  function getActiveTab() {
    let tab = appState.tabs.find((t) => t.id === appState.activeTabId);
    if (!tab && appState.tabs.length > 0) {
      tab = appState.tabs[0];
      appState.activeTabId = tab.id;
    }
    return tab;
  }

  // --- Rendering Functions ---

  function renderTabs() {
    if (!tabsGroup) return;

    // Remove existing tab elements (keep addTabBtn)
    const existingTabs = tabsGroup.querySelectorAll('.tab');
    existingTabs.forEach((t) => t.remove());

    appState.tabs.forEach((tab) => {
      const tabEl = document.createElement('div');
      tabEl.className = `tab ${tab.id === appState.activeTabId ? 'active' : ''}`;
      tabEl.dataset.tabId = tab.id;
      tabEl.textContent = tab.title;

      tabEl.addEventListener('click', () => {
        if (appState.activeTabId !== tab.id) {
          appState.activeTabId = tab.id;
          saveState();
          renderTabs();
          renderBoards();
        }
      });

      // Context menu for tab (rename / delete tab)
      tabEl.addEventListener('contextmenu', (e) => {
        e.preventDefault();
        e.stopPropagation();
        if (appState.tabs.length > 1) {
          showTabContextMenu(e, tab);
        }
      });

      tabsGroup.insertBefore(tabEl, addTabBtn);
    });
  }

  function renderBoards() {
    if (!boardsGrid) return;

    // Remove all existing board cards (keep addBoardPlaceholder)
    const existingCards = boardsGrid.querySelectorAll('.board-card');
    existingCards.forEach((c) => c.remove());

    const currentTab = getActiveTab();
    if (!currentTab || !currentTab.boards) return;

    currentTab.boards.forEach((board) => {
      const card = document.createElement('div');
      card.className = 'board-card';
      card.dataset.boardId = board.id;

      // Board Header
      const header = document.createElement('div');
      header.className = 'board-header';

      const title = document.createElement('div');
      title.className = 'board-title';
      title.textContent = board.title;
      title.title = board.title;

      const addBtn = document.createElement('button');
      addBtn.className = 'add-link-btn';
      addBtn.title = 'Добавить ссылку';
      addBtn.textContent = '+';
      addBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        openAddLinkModal(board.id);
      });

      header.appendChild(title);
      header.appendChild(addBtn);
      card.appendChild(header);

      // Links List
      const linksList = document.createElement('div');
      linksList.className = 'links-list';

      if (board.links && board.links.length > 0) {
        board.links.forEach((link) => {
          const item = document.createElement('div');
          item.className = 'link-item';
          item.dataset.url = link.url;
          item.dataset.title = link.title;
          item.dataset.linkId = link.id;
          item.dataset.boardId = board.id;

          const img = document.createElement('img');
          img.className = 'favicon';
          img.alt = '';
          img.src = getFaviconUrl(link.url);
          img.addEventListener('error', () => {
            img.src = FALLBACK_FAVICON;
          });

          const span = document.createElement('span');
          span.textContent = link.title || link.url;

          item.appendChild(img);
          item.appendChild(span);

          // Left Click: Open Link
          item.addEventListener('click', (e) => {
            e.preventDefault();
            if (link.url) {
              window.location.href = normalizeUrl(link.url);
            }
          });

          // Right Click: Context Menu
          item.addEventListener('contextmenu', (e) => {
            e.preventDefault();
            e.stopPropagation();
            openContextMenu(e, {
              type: 'link',
              tabId: currentTab.id,
              boardId: board.id,
              linkId: link.id,
              url: normalizeUrl(link.url),
              title: link.title || link.url
            });
          });

          linksList.appendChild(item);
        });
      }

      card.appendChild(linksList);

      // Board context menu (rename / delete board)
      card.addEventListener('contextmenu', (e) => {
        if (e.target.closest('.link-item')) return;
        e.preventDefault();
        e.stopPropagation();
        showBoardContextMenu(e, board);
      });

      boardsGrid.insertBefore(card, addBoardPlaceholder);
    });

    applySearchFilter();
  }

  // --- Context Menu Management ---

  function openContextMenu(e, targetData) {
    contextMenuTarget = targetData;

    // Show menu to measure dimensions
    contextMenu.style.display = 'block';
    contextMenu.style.visibility = 'hidden';

    const menuWidth = contextMenu.offsetWidth || 220;
    const menuHeight = contextMenu.offsetHeight || 180;

    let x = e.clientX;
    let y = e.clientY;

    // Viewport bounds check
    if (x + menuWidth > window.innerWidth - 10) {
      x = window.innerWidth - menuWidth - 10;
    }
    if (y + menuHeight > window.innerHeight - 10) {
      y = window.innerHeight - menuHeight - 10;
    }

    x = Math.max(10, x);
    y = Math.max(10, y);

    contextMenu.style.left = `${x}px`;
    contextMenu.style.top = `${y}px`;
    contextMenu.style.visibility = 'visible';
  }

  function hideContextMenu() {
    if (contextMenu) {
      contextMenu.style.display = 'none';
    }
    contextMenuTarget = null;
  }

  // Context Menu Actions Handler
  contextMenu.addEventListener('click', (e) => {
    const item = e.target.closest('.context-menu-item');
    if (!item || !contextMenuTarget) return;

    const action = item.dataset.action;
    const target = contextMenuTarget;
    hideContextMenu();

    if (target.type === 'link') {
      handleLinkAction(action, target);
    } else if (target.type === 'board') {
      handleBoardAction(action, target);
    } else if (target.type === 'tab') {
      handleTabAction(action, target);
    }
  });

  function handleLinkAction(action, target) {
    const url = normalizeUrl(target.url);

    switch (action) {
      case 'open-new-tab':
        window.open(url, '_blank');
        break;

      case 'open-incognito':
        if (typeof chrome !== 'undefined' && chrome.windows && chrome.windows.create) {
          chrome.windows.create({ incognito: true, url: url });
        } else {
          window.open(url, '_blank');
        }
        break;

      case 'edit':
        openEditLinkModal(target);
        break;

      case 'copy-url':
        if (navigator.clipboard && navigator.clipboard.writeText) {
          navigator.clipboard.writeText(url).then(() => {
            showToast('Адрес ссылки скопирован');
          }).catch(() => {
            showToast('Не удалось скопировать адрес');
          });
        } else {
          showToast('Буфер обмена недоступен');
        }
        break;

      case 'delete':
        deleteLink(target.tabId, target.boardId, target.linkId);
        break;
    }
  }

  function deleteLink(tabId, boardId, linkId) {
    const tab = appState.tabs.find((t) => t.id === tabId);
    if (!tab) return;
    const board = tab.boards.find((b) => b.id === boardId);
    if (!board) return;

    board.links = board.links.filter((l) => l.id !== linkId);
    saveState();
    renderBoards();
    showToast('Ссылка удалена');
  }

  function showBoardContextMenu(e, board) {
    // We can allow quick rename/delete for board
    openContextMenu(e, {
      type: 'board',
      tabId: appState.activeTabId,
      boardId: board.id,
      title: board.title
    });
  }

  function handleBoardAction(action, target) {
    if (action === 'delete') {
      const tab = appState.tabs.find((t) => t.id === target.tabId);
      if (!tab) return;
      tab.boards = tab.boards.filter((b) => b.id !== target.boardId);
      saveState();
      renderBoards();
      showToast('Доска удалена');
    } else if (action === 'edit') {
      openEditBoardModal(target);
    }
  }

  function showTabContextMenu(e, tab) {
    openContextMenu(e, {
      type: 'tab',
      tabId: tab.id,
      title: tab.title
    });
  }

  function handleTabAction(action, target) {
    if (action === 'delete') {
      if (appState.tabs.length <= 1) {
        showToast('Нельзя удалить единственную вкладку');
        return;
      }
      appState.tabs = appState.tabs.filter((t) => t.id !== target.tabId);
      if (appState.activeTabId === target.tabId) {
        appState.activeTabId = appState.tabs[0].id;
      }
      saveState();
      renderTabs();
      renderBoards();
      showToast('Вкладка удалена');
    } else if (action === 'edit') {
      openEditTabModal(target);
    }
  }

  // --- Modal Dialog Handling ---

  function openModal({ title, initialTitle = '', initialUrl = '', showUrl = true, onSave }) {
    modalTitle.textContent = title;
    modalInputTitle.value = initialTitle;
    modalInputUrl.value = initialUrl;

    if (showUrl) {
      urlGroup.style.display = 'flex';
      modalInputUrl.required = true;
    } else {
      urlGroup.style.display = 'none';
      modalInputUrl.required = false;
    }

    modalAction = onSave;

    if (typeof itemModal.showModal === 'function') {
      itemModal.showModal();
    } else {
      itemModal.setAttribute('open', '');
    }

    setTimeout(() => {
      modalInputTitle.focus();
      modalInputTitle.select();
    }, 50);
  }

  function closeModal() {
    if (typeof itemModal.close === 'function') {
      itemModal.close();
    } else {
      itemModal.removeAttribute('open');
    }
    modalAction = null;
  }

  modalCancelBtn.addEventListener('click', () => {
    closeModal();
  });

  // Close modal when clicking backdrop
  itemModal.addEventListener('click', (e) => {
    const rect = itemModal.getBoundingClientRect();
    const isInDialog =
      rect.top <= e.clientY &&
      e.clientY <= rect.top + rect.height &&
      rect.left <= e.clientX &&
      e.clientX <= rect.left + rect.width;
    if (!isInDialog) {
      closeModal();
    }
  });

  modalForm.addEventListener('submit', (e) => {
    e.preventDefault();
    const titleVal = modalInputTitle.value.trim();
    const urlVal = modalInputUrl.value.trim();

    if (!titleVal) return;

    if (modalAction) {
      modalAction({
        title: titleVal,
        url: urlVal ? normalizeUrl(urlVal) : ''
      });
    }

    closeModal();
  });

  // Modal Triggers
  function openAddLinkModal(boardId) {
    openModal({
      title: 'Добавить закладку',
      initialTitle: '',
      initialUrl: '',
      showUrl: true,
      onSave: ({ title, url }) => {
        const currentTab = getActiveTab();
        if (!currentTab) return;
        const board = currentTab.boards.find((b) => b.id === boardId);
        if (!board) return;

        if (!board.links) board.links = [];
        board.links.push({
          id: generateId('link'),
          title: title || url,
          url: url
        });

        saveState();
        renderBoards();
        showToast('Закладка добавлена');
      }
    });
  }

  function openEditLinkModal(target) {
    openModal({
      title: 'Изменить закладку',
      initialTitle: target.title,
      initialUrl: target.url,
      showUrl: true,
      onSave: ({ title, url }) => {
        const tab = appState.tabs.find((t) => t.id === target.tabId);
        if (!tab) return;
        const board = tab.boards.find((b) => b.id === target.boardId);
        if (!board) return;
        const link = board.links.find((l) => l.id === target.linkId);
        if (!link) return;

        link.title = title;
        link.url = url;

        saveState();
        renderBoards();
        showToast('Изменения сохранены');
      }
    });
  }

  function openAddBoardModal() {
    openModal({
      title: 'Новая доска',
      initialTitle: '',
      showUrl: false,
      onSave: ({ title }) => {
        const currentTab = getActiveTab();
        if (!currentTab) return;
        if (!currentTab.boards) currentTab.boards = [];

        currentTab.boards.push({
          id: generateId('board'),
          title: title,
          links: []
        });

        saveState();
        renderBoards();
        showToast(`Доска "${title}" создана`);
      }
    });
  }

  function openEditBoardModal(target) {
    openModal({
      title: 'Переименовать доску',
      initialTitle: target.title,
      showUrl: false,
      onSave: ({ title }) => {
        const tab = appState.tabs.find((t) => t.id === target.tabId);
        if (!tab) return;
        const board = tab.boards.find((b) => b.id === target.boardId);
        if (!board) return;

        board.title = title;
        saveState();
        renderBoards();
        showToast('Доска переименована');
      }
    });
  }

  function openAddTabModal() {
    openModal({
      title: 'Новая страница',
      initialTitle: '',
      showUrl: false,
      onSave: ({ title }) => {
        const newTab = {
          id: generateId('tab'),
          title: title,
          boards: []
        };
        appState.tabs.push(newTab);
        appState.activeTabId = newTab.id;

        saveState();
        renderTabs();
        renderBoards();
        showToast(`Вкладка "${title}" создана`);
      }
    });
  }

  function openEditTabModal(target) {
    openModal({
      title: 'Переименовать вкладку',
      initialTitle: target.title,
      showUrl: false,
      onSave: ({ title }) => {
        const tab = appState.tabs.find((t) => t.id === target.tabId);
        if (!tab) return;

        tab.title = title;
        saveState();
        renderTabs();
        showToast('Вкладка переименована');
      }
    });
  }

  // --- Search & Filter Handling ---

  function applySearchFilter() {
    if (!searchInput) return;
    const query = searchInput.value.trim().toLowerCase();
    const cards = boardsGrid.querySelectorAll('.board-card');

    cards.forEach((card) => {
      const links = card.querySelectorAll('.link-item');
      let visibleLinksCount = 0;

      links.forEach((link) => {
        const title = (link.dataset.title || '').toLowerCase();
        const url = (link.dataset.url || '').toLowerCase();

        if (!query || title.includes(query) || url.includes(query)) {
          link.classList.remove('hidden-by-search');
          visibleLinksCount++;
        } else {
          link.classList.add('hidden-by-search');
        }
      });

      if (query && visibleLinksCount === 0) {
        card.style.opacity = '0.35';
      } else {
        card.style.opacity = '1';
      }
    });
  }

  searchInput.addEventListener('input', applySearchFilter);

  searchInput.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') {
      const query = searchInput.value.trim();
      if (query) {
        window.location.href = `https://www.google.com/search?q=${encodeURIComponent(query)}`;
      }
    }
  });

  const engineIcon = document.querySelector('.engine-icon');
  if (engineIcon) {
    engineIcon.addEventListener('click', () => {
      const query = searchInput.value.trim();
      if (query) {
        window.location.href = `https://www.google.com/search?q=${encodeURIComponent(query)}`;
      } else {
        window.location.href = 'https://www.google.com';
      }
    });
  }

  // --- Global Event Listeners ---

  // Add Board Placeholder Click
  if (addBoardPlaceholder) {
    addBoardPlaceholder.addEventListener('click', () => {
      openAddBoardModal();
    });
  }

  // Add Tab Button Click
  if (addTabBtn) {
    addTabBtn.addEventListener('click', () => {
      openAddTabModal();
    });
  }

  // Close Context Menu on Document Click or Escape
  document.addEventListener('click', (e) => {
    if (!e.target.closest('#contextMenu')) {
      hideContextMenu();
    }
  });

  window.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
      hideContextMenu();
      closeModal();
    }
  });

  window.addEventListener('scroll', () => {
    hideContextMenu();
  }, { passive: true });

  window.addEventListener('resize', () => {
    hideContextMenu();
  });

  // FAB Menu Button Click
  if (menuBtn) {
    menuBtn.addEventListener('click', () => {
      showToast('NovaTab v2.0 • Минималистичный дашборд');
    });
  }

  // FAB Settings Button Click
  if (settingsBtn) {
    settingsBtn.addEventListener('click', () => {
      openSettingsAction();
    });
  }

  function openSettingsAction() {
    // Quick settings: export/import or reset
    const action = confirm('Сбросить состояние к стандартным настройкам по умолчанию?');
    if (action) {
      appState = JSON.parse(JSON.stringify(DEFAULT_STATE));
      saveState();
      renderTabs();
      renderBoards();
      showToast('Настройки сброшены к начальным');
    }
  }

  // --- Initializer ---
  function init() {
    loadState(() => {
      renderTabs();
      renderBoards();
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
