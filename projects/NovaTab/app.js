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
  const sidebar = document.getElementById('sidebar');
  const menuSideBtn = document.getElementById('menuSideBtn');
  const settingsSideBtn = document.getElementById('settingsSideBtn');
  const sideSearch = document.getElementById('sideSearch');
  const mpWallpaper = document.getElementById('mpWallpaper');
  const sideWidgets = document.getElementById('sideWidgets');
  const sideImport = document.getElementById('sideImport');
  const sideTrash = document.getElementById('sideTrash');
  const toast = document.getElementById('toast');

  // Board Menu Elements
  const boardMenu = document.getElementById('boardMenu');
  const bmRename = document.getElementById('bmRename');
  const bmOpenAll = document.getElementById('bmOpenAll');
  const bmCustomize = document.getElementById('bmCustomize');
  const bmCustomPanel = document.getElementById('bmCustomPanel');
  const bmCustomColorBtn = document.getElementById('bmCustomColorBtn');
  const bmColorInput = document.getElementById('bmColorInput');
  const bmDelete = document.getElementById('bmDelete');
  let currentBoardMenuTarget = null; // { board, card }

  // Settings Modal Elements
  const settingsOverlay = document.getElementById('settingsOverlay');
  const settingsModal = document.getElementById('settingsModal');
  const settingsCloseBtn = document.getElementById('settingsCloseBtn');
  const settingsNav = document.getElementById('settingsNav');
  const settingsBody = document.getElementById('settingsBody');

  // Wallpaper Modal Elements
  const wpOverlay = document.getElementById('wpOverlay');
  const wpCloseBtn = document.getElementById('wpCloseBtn');
  const wpUploadZone = document.getElementById('wpUploadZone');
  const wpFileInput = document.getElementById('wpFileInput');
  const wpSearchOnlineBtn = document.getElementById('wpSearchOnlineBtn');

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

      if (board.customColor) {
        card.style.setProperty('--card-accent', board.customColor);
        card.dataset.customMode = board.customMode || 'corner';
      }

      // Board Header
      const header = document.createElement('div');
      header.className = 'board-header';

      const title = document.createElement('div');
      title.className = 'board-title';
      title.textContent = board.title;
      title.title = board.title;

      const headerActions = document.createElement('div');
      headerActions.className = 'board-header-actions';

      const addBtn = document.createElement('button');
      addBtn.className = 'add-link-btn';
      addBtn.title = 'Добавить ссылку';
      addBtn.textContent = '+';
      addBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        openAddLinkModal(board.id);
      });

      const menuBtn = document.createElement('button');
      menuBtn.className = 'board-menu-btn';
      menuBtn.title = 'Меню доски';
      menuBtn.textContent = '···';
      menuBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        openBoardMenu(e, board, card);
      });

      headerActions.appendChild(addBtn);
      headerActions.appendChild(menuBtn);

      header.appendChild(title);
      header.appendChild(headerActions);
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

      // Board context menu (rename / delete / customize board)
      card.addEventListener('contextmenu', (e) => {
        if (e.target.closest('.link-item')) return;
        e.preventDefault();
        e.stopPropagation();
        openBoardMenu(e, board, card);
      });

      boardsGrid.insertBefore(card, addBoardPlaceholder);
    });

    applySearchFilter();
  }

  // --- Inline Board Creation ---

  function startInlineBoardCreation() {
    // If an inline creation card is already open, focus it
    const existingInline = boardsGrid.querySelector('.board-card.inline-creating');
    if (existingInline) {
      const existingInput = existingInline.querySelector('.board-title-input');
      existingInput?.focus();
      return;
    }

    const tempCard = document.createElement('div');
    tempCard.className = 'board-card inline-creating';

    const header = document.createElement('div');
    header.className = 'board-header';

    const input = document.createElement('input');
    input.type = 'text';
    input.className = 'board-title-input';
    input.placeholder = 'Новая доска';
    input.maxLength = 50;

    header.appendChild(input);
    tempCard.appendChild(header);

    boardsGrid.insertBefore(tempCard, addBoardPlaceholder);

    let isCommitted = false;

    function commit() {
      if (isCommitted) return;
      isCommitted = true;

      const title = input.value.trim();
      if (title) {
        const currentTab = getActiveTab();
        if (currentTab) {
          if (!currentTab.boards) currentTab.boards = [];
          const newBoard = {
            id: generateId('board'),
            title: title,
            links: []
          };
          currentTab.boards.push(newBoard);
          saveState();
          renderBoards();
          showToast(`Доска "${title}" создана`);
        } else {
          tempCard.remove();
        }
      } else {
        tempCard.remove();
      }
    }

    input.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        e.preventDefault();
        commit();
      } else if (e.key === 'Escape') {
        e.preventDefault();
        isCommitted = true;
        tempCard.remove();
      }
    });

    input.addEventListener('blur', () => {
      commit();
    });

    setTimeout(() => {
      input.focus();
      input.select();
    }, 50);
  }

  // --- Board Context Menu Handlers ---

  function openBoardMenu(e, board, card) {
    hideContextMenu();
    currentBoardMenuTarget = { board, card };

    if (!boardMenu) return;
    boardMenu.style.display = 'block';
    boardMenu.style.visibility = 'hidden';

    // Synchronize mode buttons
    const mode = board.customMode || 'corner';
    if (bmCustomPanel) {
      const segBtns = bmCustomPanel.querySelectorAll('.st-seg-btn');
      segBtns.forEach((btn) => {
        btn.classList.toggle('active', btn.dataset.mode === mode);
      });
    }

    const menuWidth = boardMenu.offsetWidth || 230;
    const menuHeight = boardMenu.offsetHeight || 260;

    let left, top;

    const isBtnTrigger = e.currentTarget && e.currentTarget.classList && e.currentTarget.classList.contains('board-menu-btn');
    if (isBtnTrigger) {
      const btnRect = e.currentTarget.getBoundingClientRect();
      const clientLeft = btnRect.right - menuWidth;
      if (clientLeft < 12) {
        left = 12 + window.scrollX;
      } else if (btnRect.left + menuWidth > window.innerWidth - 12) {
        left = window.innerWidth - menuWidth - 12 + window.scrollX;
      } else {
        left = clientLeft + window.scrollX;
      }

      if (btnRect.bottom + 6 + menuHeight > window.innerHeight - 12) {
        if (btnRect.top - menuHeight - 6 > 12) {
          top = btnRect.top - menuHeight - 6 + window.scrollY;
        } else {
          top = Math.max(12, window.innerHeight - menuHeight - 12) + window.scrollY;
        }
      } else {
        top = btnRect.bottom + 6 + window.scrollY;
      }
    } else {
      let clientX = e.clientX || 0;
      let clientY = e.clientY || 0;

      if (clientX + menuWidth > window.innerWidth - 12) {
        clientX = window.innerWidth - menuWidth - 12;
      }
      if (clientY + menuHeight > window.innerHeight - 12) {
        clientY = window.innerHeight - menuHeight - 12;
      }

      clientX = Math.max(12, clientX);
      clientY = Math.max(12, clientY);

      left = clientX + window.scrollX;
      top = clientY + window.scrollY;
    }

    boardMenu.style.left = `${left}px`;
    boardMenu.style.top = `${top}px`;
    boardMenu.style.visibility = 'visible';
  }

  function hideBoardMenu() {
    if (boardMenu) {
      boardMenu.style.display = 'none';
    }
    currentBoardMenuTarget = null;
  }

  function startInlineBoardRename(board, card) {
    hideBoardMenu();
    if (!card) {
      card = boardsGrid?.querySelector(`.board-card[data-board-id="${board.id}"]`);
    }
    if (!card) return;

    const header = card.querySelector('.board-header');
    const titleEl = card.querySelector('.board-title');
    if (!header || !titleEl) return;

    const currentTitle = board.title;
    const input = document.createElement('input');
    input.type = 'text';
    input.className = 'board-title-input';
    input.value = currentTitle;
    input.placeholder = 'Название доски';
    input.maxLength = 50;

    header.replaceChild(input, titleEl);

    let isCommitted = false;

    function commit() {
      if (isCommitted) return;
      isCommitted = true;

      const newTitle = input.value.trim();
      if (newTitle && newTitle !== currentTitle) {
        board.title = newTitle;
        saveState();
        renderBoards();
        showToast('Доска переименована');
      } else {
        renderBoards();
      }
    }

    input.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        e.preventDefault();
        commit();
      } else if (e.key === 'Escape') {
        e.preventDefault();
        isCommitted = true;
        renderBoards();
      }
    });

    input.addEventListener('blur', () => {
      commit();
    });

    setTimeout(() => {
      input.focus();
      input.select();
    }, 50);
  }

  function handleOpenAllLinks(board) {
    hideBoardMenu();
    if (!board || !board.links || board.links.length === 0) {
      showToast('Нет ссылок для открытия');
      return;
    }
    board.links.forEach((link) => {
      if (link.url) {
        window.open(normalizeUrl(link.url), '_blank');
      }
    });
  }

  function initBoardMenu() {
    if (!boardMenu) return;

    // Rename
    if (bmRename) {
      bmRename.addEventListener('click', (e) => {
        e.stopPropagation();
        if (currentBoardMenuTarget) {
          const { board, card } = currentBoardMenuTarget;
          startInlineBoardRename(board, card);
        }
      });
    }

    // Open All
    if (bmOpenAll) {
      bmOpenAll.addEventListener('click', (e) => {
        e.stopPropagation();
        if (currentBoardMenuTarget) {
          handleOpenAllLinks(currentBoardMenuTarget.board);
        }
      });
    }

    // Customize toggle
    if (bmCustomize && bmCustomPanel) {
      bmCustomize.addEventListener('click', (e) => {
        e.stopPropagation();
        const isHidden = window.getComputedStyle(bmCustomPanel).display === 'none';
        bmCustomPanel.style.display = isHidden ? 'block' : 'none';
      });
    }

    // Segment mode switch
    if (bmCustomPanel) {
      const segBtns = bmCustomPanel.querySelectorAll('.st-seg-btn');
      segBtns.forEach((btn) => {
        btn.addEventListener('click', (e) => {
          e.stopPropagation();
          segBtns.forEach((b) => b.classList.remove('active'));
          btn.classList.add('active');

          const mode = btn.dataset.mode || 'corner';
          if (currentBoardMenuTarget) {
            const { board, card } = currentBoardMenuTarget;
            board.customMode = mode;
            if (board.customColor && card) {
              card.dataset.customMode = mode;
            }
            saveState();
          }
        });
      });
    }

    // Swatches
    const swatches = boardMenu.querySelectorAll('.bm-color-swatch[data-color]');
    swatches.forEach((swatch) => {
      swatch.addEventListener('click', (e) => {
        e.stopPropagation();
        const color = swatch.dataset.color;
        if (currentBoardMenuTarget && color) {
          const { board, card } = currentBoardMenuTarget;
          board.customColor = color;
          board.customMode = board.customMode || 'corner';
          if (card) {
            card.style.setProperty('--card-accent', board.customColor);
            card.dataset.customMode = board.customMode;
          }
          saveState();
        }
      });
    });

    // Custom Color Picker Button
    if (bmCustomColorBtn && bmColorInput) {
      bmCustomColorBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        bmColorInput.click();
      });

      bmColorInput.addEventListener('input', (e) => {
        const color = e.target.value;
        if (currentBoardMenuTarget && color) {
          const { board, card } = currentBoardMenuTarget;
          board.customColor = color;
          board.customMode = board.customMode || 'corner';
          if (card) {
            card.style.setProperty('--card-accent', board.customColor);
            card.dataset.customMode = board.customMode;
          }
          saveState();
        }
      });
    }

    // Delete
    if (bmDelete) {
      bmDelete.addEventListener('click', (e) => {
        e.stopPropagation();
        if (currentBoardMenuTarget) {
          const { board } = currentBoardMenuTarget;
          hideBoardMenu();
          const currentTab = getActiveTab();
          if (currentTab && currentTab.boards) {
            currentTab.boards = currentTab.boards.filter((b) => b.id !== board.id);
            saveState();
            renderBoards();
            showToast('Доска удалена');
          }
        }
      });
    }
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
    const card = boardsGrid?.querySelector(`.board-card[data-board-id="${board.id}"]`);
    openBoardMenu(e, board, card);
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
      startInlineBoardCreation();
    });
  }

  // Add Tab Button Click
  if (addTabBtn) {
    addTabBtn.addEventListener('click', () => {
      openAddTabModal();
    });
  }

  // Close Context Menu & Board Menu & Sidebar on Document Click
  document.addEventListener('click', (e) => {
    if (!e.target.closest('#contextMenu')) {
      hideContextMenu();
    }
    if (!e.target.closest('#boardMenu') && !e.target.closest('.board-menu-btn')) {
      hideBoardMenu();
    }
    if (sidebar && !sidebar.contains(e.target)) {
      sidebar.classList.remove('is-open');
    }
  });

  window.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
      hideContextMenu();
      hideBoardMenu();
      closeModal();
      closeSettingsModal();
      closeWallpaperModal();
      if (sidebar) sidebar.classList.remove('is-open');
    } else if (e.key === '/' && document.activeElement !== searchInput && !['INPUT', 'TEXTAREA'].includes(document.activeElement?.tagName)) {
      if (searchInput) {
        e.preventDefault();
        searchInput.focus();
        searchInput.select();
      }
    }
  });

  window.addEventListener('scroll', () => {
    hideContextMenu();
    hideBoardMenu();
  }, { passive: true });

  window.addEventListener('resize', () => {
    hideContextMenu();
    hideBoardMenu();
  });

  // --- Expandable Sidebar Controls ---
  if (menuSideBtn && sidebar) {
    menuSideBtn.addEventListener('click', (e) => {
      e.stopPropagation();
      sidebar.classList.toggle('is-open');
    });
  }

  if (settingsSideBtn) {
    settingsSideBtn.addEventListener('click', (e) => {
      e.stopPropagation();
      openSettingsModal();
      sidebar?.classList.remove('is-open');
    });
  }

  if (mpWallpaper) {
    mpWallpaper.addEventListener('click', (e) => {
      e.stopPropagation();
      openWallpaperModal();
      sidebar?.classList.remove('is-open');
    });
  }

  if (sideSearch) {
    sideSearch.addEventListener('click', (e) => {
      e.stopPropagation();
      searchInput?.focus();
      searchInput?.select();
      sidebar?.classList.remove('is-open');
    });
  }

  if (sideWidgets) {
    sideWidgets.addEventListener('click', (e) => {
      e.stopPropagation();
      showToast('Виджеты в разработке');
      sidebar?.classList.remove('is-open');
    });
  }

  if (sideImport) {
    sideImport.addEventListener('click', (e) => {
      e.stopPropagation();
      showToast('Импорт закладок');
      sidebar?.classList.remove('is-open');
    });
  }

  if (sideTrash) {
    sideTrash.addEventListener('click', (e) => {
      e.stopPropagation();
      showToast('Корзина пуста');
      sidebar?.classList.remove('is-open');
    });
  }

  // --- Settings Modal Handling ---
  function openSettingsModal() {
    if (!settingsOverlay) return;
    settingsOverlay.style.display = 'flex';
  }

  function closeSettingsModal() {
    if (!settingsOverlay) return;
    settingsOverlay.style.display = 'none';
  }

  function initSettingsModal() {
    // Close Button Click
    if (settingsCloseBtn) {
      settingsCloseBtn.addEventListener('click', () => {
        closeSettingsModal();
      });
    }

    // Close on Backdrop Click
    if (settingsOverlay) {
      settingsOverlay.addEventListener('click', (e) => {
        if (e.target === settingsOverlay) {
          closeSettingsModal();
        }
      });
    }

    // Toggle Buttons Handler
    if (settingsBody) {
      settingsBody.addEventListener('click', (e) => {
        const toggle = e.target.closest('.st-toggle');
        if (toggle) {
          toggle.classList.toggle('on');
        }
      });
    }

    // Nav tab clicks
    const navItems = document.querySelectorAll('.settings-nav-item[data-tab]');
    const tabContents = document.querySelectorAll('.settings-tab-content');

    navItems.forEach(item => {
      item.addEventListener('click', () => {
        navItems.forEach(n => n.classList.remove('active'));
        tabContents.forEach(t => (t.style.display = 'none'));

        item.classList.add('active');
        const target = document.getElementById(item.dataset.tab);
        if (target) target.style.display = 'block';
      });
    });

    // Segmented buttons
    document.querySelectorAll('.st-segment').forEach(segment => {
      const btns = segment.querySelectorAll('.st-seg-btn');
      btns.forEach(btn => {
        btn.addEventListener('click', () => {
          btns.forEach(b => b.classList.remove('active'));
          btn.classList.add('active');
        });
      });
    });

    // Live feedback for all .st-slider-field
    document.querySelectorAll('.st-slider-field').forEach(field => {
      const slider = field.querySelector('.st-slider');
      const valEl = field.querySelector('.st-val');
      if (slider && valEl) {
        const initialText = valEl.textContent.trim();
        const unit = initialText.endsWith('px') ? 'px' : initialText.endsWith('%') ? '%' : '';
        slider.addEventListener('input', () => {
          valEl.textContent = `${slider.value}${unit}`;
        });
      }
    });

    // Support button click handler
    const supportNavBtn = document.getElementById('supportNavBtn');
    if (supportNavBtn) {
      supportNavBtn.addEventListener('click', () => {
        showToast('Служба поддержки: support@novatab.app');
      });
    }
  }

  // --- Wallpaper Modal Handling ---
  function openWallpaperModal() {
    if (!wpOverlay) return;
    wpOverlay.style.display = 'flex';
  }

  function closeWallpaperModal() {
    if (!wpOverlay) return;
    wpOverlay.style.display = 'none';
  }

  function initWallpaperModal() {
    // Close Button Click
    if (wpCloseBtn) {
      wpCloseBtn.addEventListener('click', () => {
        closeWallpaperModal();
      });
    }

    // Close on Backdrop Click
    if (wpOverlay) {
      wpOverlay.addEventListener('click', (e) => {
        if (e.target === wpOverlay) {
          closeWallpaperModal();
        }
      });
    }

    // Upload Zone Click -> Trigger File Input
    if (wpUploadZone && wpFileInput) {
      wpUploadZone.addEventListener('click', () => {
        wpFileInput.click();
      });
    }

    // Handle File Input Change
    if (wpFileInput) {
      wpFileInput.addEventListener('change', (e) => {
        const file = e.target.files && e.target.files[0];
        if (!file) return;

        const reader = new FileReader();
        reader.onload = (event) => {
          const bgLayer = document.getElementById('bg-layer');
          if (bgLayer) {
            bgLayer.style.background = `url(${event.target.result}) center/cover no-repeat`;
          }
          showToast('Обои обновлены');
        };
        reader.readAsDataURL(file);
        wpFileInput.value = '';
      });
    }

    // Preset Items Click
    const presetItems = document.querySelectorAll('.wp-preset-item');
    presetItems.forEach((item) => {
      item.addEventListener('click', () => {
        const bg = item.dataset.bg || item.style.background;
        const bgLayer = document.getElementById('bg-layer');
        if (bgLayer && bg) {
          bgLayer.style.background = bg;
        }
        showToast('Пресет применен');
      });
    });

    // Search Online Button Click
    if (wpSearchOnlineBtn) {
      wpSearchOnlineBtn.addEventListener('click', () => {
        window.open('https://unsplash.com/s/photos/wallpaper', '_blank');
      });
    }
  }

  // --- Initializer ---
  function init() {
    loadState(() => {
      renderTabs();
      renderBoards();
    });
    initBoardMenu();
    initSettingsModal();
    initWallpaperModal();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
