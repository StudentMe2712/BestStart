/**
 * NovaTab — Core Application Engine
 * Pure Vanilla JS, Manifest V3 CSP Compliant
 */

(function () {
  'use strict';

  // --- Constants & Defaults ---
  const STORAGE_KEY = 'novatab_state';
  const SETTINGS_STORAGE_KEY = 'novatab_settings';
  const FALLBACK_FAVICON = "data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' width='16' height='16' viewBox='0 0 24 24' fill='none' stroke='%23666' stroke-width='2'><circle cx='12' cy='12' r='10'/><line x1='2' y1='12' x2='22' y2='12'/><path d='M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10z'/></svg>";

  const WALLPAPER_PRESETS = [
    'wallpapers/01.png',
    'wallpapers/02.jpg',
    'wallpapers/03.jpg',
    'wallpapers/04.jpg',
    'wallpapers/05.jpg',
    'wallpapers/06.png',
    'wallpapers/07.png',
    'wallpapers/08.jpg',
    'wallpapers/09.jpg',
    'wallpapers/10.jpg',
    'wallpapers/11.jpg',
    'wallpapers/12.jpg',
    'wallpapers/13.jpg',
    'wallpapers/14.jpg'
  ];

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
  const widgetGallery = document.getElementById('widgetGallery');

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

  // Trash Modal Elements
  const trashOverlay = document.getElementById('trashOverlay');
  const trashCloseBtn = document.getElementById('trashCloseBtn');
  const trashEmptyBtn = document.getElementById('trashEmptyBtn');
  const trashConfirm = document.getElementById('trashConfirm');
  const trashConfirmCancel = document.getElementById('trashConfirmCancel');
  const trashConfirmYes = document.getElementById('trashConfirmYes');
  const trashList = document.getElementById('trashList');

  // Search Modal Elements
  const searchOverlay = document.getElementById('searchOverlay');
  const modalSearchInput = document.getElementById('modalSearchInput');
  const searchResults = document.getElementById('searchResults');

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

  function insertWidgetToGrid(widgetBoard) {
    const currentTab = getActiveTab();
    if (!currentTab) return;
    if (!currentTab.boards) currentTab.boards = [];
    currentTab.boards.push(widgetBoard);
    saveState();
    renderBoards();
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
    const existingTabs = tabsGroup.querySelectorAll('.page-tab, .tab');
    existingTabs.forEach((t) => t.remove());

    appState.tabs.forEach((tab) => {
      const tabEl = document.createElement('div');
      tabEl.className = `page-tab tab ${tab.id === appState.activeTabId ? 'active' : ''}`;
      tabEl.dataset.id = tab.id;
      tabEl.dataset.tabId = tab.id;
      tabEl.setAttribute('draggable', 'true');

      const nameSpan = document.createElement('span');
      nameSpan.className = 'page-tab-name';
      nameSpan.textContent = tab.title;
      tabEl.appendChild(nameSpan);

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
    const existingCards = boardsGrid.querySelectorAll('.board-card, .board');
    existingCards.forEach((c) => c.remove());

    const currentTab = getActiveTab();
    if (!currentTab || !currentTab.boards) return;

    currentTab.boards.forEach((board) => {
      // 1. NOTES WIDGET
      if (board.type === 'notes') {
        const card = document.createElement('div');
        card.className = 'board board-card';
        card.setAttribute('draggable', 'true');
        card.dataset.id = board.id;
        card.dataset.boardId = board.id;

        if (board.customColor) {
          card.style.setProperty('--card-accent', board.customColor);
          card.dataset.customMode = board.customMode || 'corner';
        }

        // Header
        const header = document.createElement('div');
        header.className = 'board-header';

        const title = document.createElement('div');
        title.className = 'board-title';
        title.textContent = board.title || 'Заметки';
        title.title = board.title || 'Заметки';

        const menuBtn = document.createElement('button');
        menuBtn.className = 'board-menu-btn';
        menuBtn.title = 'Меню доски';
        menuBtn.textContent = '···';

        header.appendChild(title);
        header.appendChild(menuBtn);
        card.appendChild(header);

        // Textarea
        const textarea = document.createElement('textarea');
        textarea.className = 'notes-textarea';
        textarea.placeholder = 'Введите текст...';
        textarea.value = board.content || '';
        textarea.addEventListener('input', () => {
          board.content = textarea.value;
          saveState();
        });
        card.appendChild(textarea);

        // Resize handle
        const resizeHandle = document.createElement('div');
        resizeHandle.className = 'notes-resize-handle';
        resizeHandle.innerHTML = '<svg width="10" height="10" viewBox="0 0 10 10" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M9 1L1 9M9 5L5 9M9 9L9 9"/></svg>';
        card.appendChild(resizeHandle);

        // Context menu
        card.addEventListener('contextmenu', (e) => {
          if (e.target.closest('.notes-textarea')) return;
          e.preventDefault();
          e.stopPropagation();
          openBoardMenu(e, board, card);
        });

        boardsGrid.insertBefore(card, addBoardPlaceholder);
        return;
      }

      // 2. CALENDAR WIDGET
      if (board.type === 'calendar') {
        const card = document.createElement('div');
        card.className = 'board board-card';
        card.setAttribute('draggable', 'true');
        card.dataset.id = board.id;
        card.dataset.boardId = board.id;

        if (board.customColor) {
          card.style.setProperty('--card-accent', board.customColor);
          card.dataset.customMode = board.customMode || 'corner';
        }

        // Header
        const header = document.createElement('div');
        header.className = 'board-header';

        const leftGroup = document.createElement('div');
        leftGroup.style.display = 'flex';
        leftGroup.style.alignItems = 'center';
        leftGroup.style.gap = '4px';

        const prevBtn = document.createElement('button');
        prevBtn.className = 'cal-nav-btn';
        prevBtn.title = 'Предыдущий месяц';
        prevBtn.innerHTML = '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="15 18 9 12 15 6"></polyline></svg>';

        const nextBtn = document.createElement('button');
        nextBtn.className = 'cal-nav-btn';
        nextBtn.title = 'Следующий месяц';
        nextBtn.innerHTML = '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="9 18 15 12 9 6"></polyline></svg>';

        const title = document.createElement('div');
        title.className = 'board-title';
        title.textContent = board.title || 'Август 2026';
        title.title = board.title || 'Август 2026';
        title.style.marginLeft = '4px';

        leftGroup.appendChild(prevBtn);
        leftGroup.appendChild(nextBtn);
        leftGroup.appendChild(title);

        const menuBtn = document.createElement('button');
        menuBtn.className = 'board-menu-btn';
        menuBtn.title = 'Меню доски';
        menuBtn.textContent = '···';

        header.appendChild(leftGroup);
        header.appendChild(menuBtn);
        card.appendChild(header);

        // Days row
        const daysRow = document.createElement('div');
        daysRow.className = 'cal-days-row';
        const dayNames = ['ПН', 'ВТ', 'СР', 'ЧТ', 'ПТ', 'СБ', 'ВС'];
        dayNames.forEach((name) => {
          const dayNameEl = document.createElement('div');
          dayNameEl.className = 'cal-day-name';
          dayNameEl.textContent = name;
          daysRow.appendChild(dayNameEl);
        });
        card.appendChild(daysRow);

        // Calendar Grid
        const calGrid = document.createElement('div');
        calGrid.className = 'cal-grid';

        // August 2026 starts on Saturday (5 blank days: Mon, Tue, Wed, Thu, Fri)
        for (let i = 0; i < 5; i++) {
          const blank = document.createElement('div');
          blank.className = 'cal-day cal-day-blank';
          calGrid.appendChild(blank);
        }

        // 31 days in August 2026
        for (let day = 1; day <= 31; day++) {
          const dayEl = document.createElement('div');
          const dayOfWeek = (5 + day - 1) % 7; // 0=Mon, 1=Tue, 2=Wed, 3=Thu, 4=Fri, 5=Sat, 6=Sun
          const isWeekend = dayOfWeek === 5 || dayOfWeek === 6;
          const isToday = day === 30;

          let classNames = ['cal-day'];
          if (isWeekend) classNames.push('cal-day-weekend');
          if (isToday) classNames.push('cal-day-today');

          dayEl.className = classNames.join(' ');
          dayEl.textContent = day;
          calGrid.appendChild(dayEl);
        }
        card.appendChild(calGrid);

        // Context menu
        card.addEventListener('contextmenu', (e) => {
          e.preventDefault();
          e.stopPropagation();
          openBoardMenu(e, board, card);
        });

        boardsGrid.insertBefore(card, addBoardPlaceholder);
        return;
      }

      // 3. REGULAR BOOKMARK BOARD
      const card = document.createElement('div');
      card.className = 'board board-card';
      card.setAttribute('draggable', 'true');
      card.dataset.id = board.id;
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
      addBtn.className = 'add-link-btn board-add-link-btn';
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
    boardMenu.dataset.targetId = board?.id;
    boardMenu.style.display = 'block';
    boardMenu.style.position = 'fixed';
    boardMenu.style.visibility = 'hidden';

    // Synchronize mode buttons
    const mode = board?.customMode || 'corner';
    if (bmCustomPanel) {
      const segBtns = bmCustomPanel.querySelectorAll('.st-seg-btn');
      segBtns.forEach((btn) => {
        btn.classList.toggle('active', btn.dataset.mode === mode);
      });
    }

    const menuWidth = boardMenu.offsetWidth || 230;
    const menuHeight = boardMenu.offsetHeight || 260;

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

    boardMenu.style.left = `${clientX}px`;
    boardMenu.style.top = `${clientY}px`;
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
      card = boardsGrid?.querySelector(`.board-card[data-board-id="${board.id}"], .board[data-board-id="${board.id}"]`);
    }
    if (!card) return;

    const titleEl = card.querySelector('.board-title');
    if (!titleEl || !titleEl.parentNode) return;

    const currentTitle = board.title || '';
    const input = document.createElement('input');
    input.type = 'text';
    input.className = 'board-title-input';
    input.value = currentTitle;
    input.placeholder = 'Название доски';
    input.maxLength = 50;

    titleEl.parentNode.replaceChild(input, titleEl);

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
    const cards = boardsGrid.querySelectorAll('.board-card, .board');

    cards.forEach((card) => {
      const links = card.querySelectorAll('.link-item');
      if (links.length === 0) {
        const boardTitle = (card.querySelector('.board-title')?.textContent || '').toLowerCase();
        const notesText = (card.querySelector('.notes-textarea')?.value || '').toLowerCase();
        if (!query || boardTitle.includes(query) || notesText.includes(query)) {
          card.style.opacity = '1';
        } else {
          card.style.opacity = '0.35';
        }
        return;
      }

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

  // Global listener for .board-menu-btn & context menus & sidebar
  document.addEventListener('click', (e) => {
    const menuBtn = e.target.closest('.board-menu-btn');
    const boardMenu = document.getElementById('boardMenu');

    if (menuBtn) {
      e.stopPropagation();
      const rect = menuBtn.getBoundingClientRect();
      if (!boardMenu) return;
      boardMenu.style.display = 'block';
      boardMenu.style.position = 'fixed';

      // Сдвигаем меню СПРАВА от кнопки (right) + отступ 8px
      boardMenu.style.left = `${rect.right + 8}px`;
      // Выравниваем по верхнему краю кнопки
      boardMenu.style.top = `${rect.top}px`;

      // Сохраняем контекст доски
      const card = menuBtn.closest('.board-card') || menuBtn.closest('.board');
      const boardId = card ? (card.dataset.boardId || card.dataset.id) : null;
      boardMenu.dataset.targetId = boardId;
      const currentTab = getActiveTab();
      const board = currentTab?.boards?.find(b => b.id === boardId);
      currentBoardMenuTarget = { board, card };

      if (board && bmCustomPanel) {
        const mode = board.customMode || 'corner';
        const segBtns = bmCustomPanel.querySelectorAll('.st-seg-btn');
        segBtns.forEach((btn) => {
          btn.classList.toggle('active', btn.dataset.mode === mode);
        });
      }
    } else if (!e.target.closest('.board-menu')) {
      if (boardMenu) boardMenu.style.display = 'none';
      currentBoardMenuTarget = null;
    }

    if (!e.target.closest('#contextMenu')) {
      hideContextMenu();
    }
    if (sidebar && !sidebar.contains(e.target)) {
      sidebar.classList.remove('is-open');
    }
  });

  window.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
      hideContextMenu();
      hideBoardMenu();
      if (widgetGallery) widgetGallery.style.display = 'none';
      closeModal();
      closeSettingsModal();
      closeWallpaperModal();
      closeTrashModal();
      closeSearchOverlay();
      if (sidebar) sidebar.classList.remove('is-open');
    } else if (e.key === '/' && document.activeElement !== searchInput && document.activeElement !== modalSearchInput && !['INPUT', 'TEXTAREA'].includes(document.activeElement?.tagName)) {
      e.preventDefault();
      openSearchOverlay();
    }
  });

  window.addEventListener('scroll', () => {
    hideContextMenu();
    hideBoardMenu();
    if (widgetGallery) widgetGallery.style.display = 'none';
  }, { passive: true });

  window.addEventListener('resize', () => {
    hideContextMenu();
    hideBoardMenu();
    if (widgetGallery) widgetGallery.style.display = 'none';
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
      openSearchOverlay();
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
      openTrashModal();
      sidebar?.classList.remove('is-open');
    });
  }

  // --- Widget Gallery Management ---
  function initWidgetGallery() {
    const sideWidgetsBtn = document.getElementById('sideWidgets');
    const widgetGallery = document.getElementById('widgetGallery');
    const sidebar = document.getElementById('sidebar');

    if (sideWidgetsBtn && widgetGallery) {
      sideWidgetsBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        const isHidden = widgetGallery.style.display === 'none' || !widgetGallery.style.display;
        widgetGallery.style.display = isHidden ? 'block' : 'none';
        if (sidebar) sidebar.classList.remove('is-open');
      });
    }

    // Закрытие виджетов при клике вне панели
    document.addEventListener('click', (e) => {
      if (widgetGallery && !e.target.closest('#widgetGallery') && !e.target.closest('#sideWidgets')) {
        widgetGallery.style.display = 'none';
      }
    });

    if (!widgetGallery) return;

    // Добавить доску (#wcBoard .widget-add-btn)
    const wcBoardBtn = document.querySelector('#wcBoard .widget-add-btn');
    if (wcBoardBtn) {
      wcBoardBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        widgetGallery.style.display = 'none';
        startInlineBoardCreation();
      });
    }

    // Добавить заметки (#wcNotes .widget-add-btn)
    const wcNotesBtn = document.querySelector('#wcNotes .widget-add-btn');
    if (wcNotesBtn) {
      wcNotesBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        insertWidgetToGrid({
          id: 'note_' + Date.now(),
          type: 'notes',
          title: 'Заметки',
          content: ''
        });
        if (widgetGallery) widgetGallery.style.display = 'none';
        showToast('Виджет "Заметки" добавлен');
      });
    }

    // Добавить календарь (#wcCalendar .widget-add-btn)
    const wcCalendarBtn = document.querySelector('#wcCalendar .widget-add-btn');
    if (wcCalendarBtn) {
      wcCalendarBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        insertWidgetToGrid({
          id: 'cal_' + Date.now(),
          type: 'calendar',
          title: 'Август 2026'
        });
        if (widgetGallery) widgetGallery.style.display = 'none';
        showToast('Виджет "Календарь" добавлен');
      });
    }

    // Добавить помодоро (#wcPomodoro .widget-add-btn)
    const wcPomodoroBtn = document.querySelector('#wcPomodoro .widget-add-btn');
    if (wcPomodoroBtn) {
      wcPomodoroBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        showToast('Виджет "Помодоро" скоро будет доступен');
      });
    }

    // Переключатели внутри галереи виджетов
    widgetGallery.addEventListener('click', (e) => {
      const toggle = e.target.closest('.st-toggle');
      if (toggle) {
        e.stopPropagation();
        toggle.classList.toggle('on');
        const isOn = toggle.classList.contains('on');
        if (toggle.id === 'clockToggle') {
          showToast(isOn ? 'Часы включены' : 'Часы выключены');
        } else if (toggle.id === 'navSearchToggle') {
          const searchBar = document.querySelector('.search-bar');
          if (searchBar) {
            searchBar.style.display = isOn ? 'flex' : 'none';
          }
          showToast(isOn ? 'Поиск включен' : 'Поиск выключен');
        } else if (toggle.id === 'weatherToggle') {
          showToast(isOn ? 'Погода включена' : 'Погода выключена');
        }
      }
    });
  }

  // --- Settings Modal & State Persistence ---
  function saveSetting(key, value) {
    if (typeof chrome !== 'undefined' && chrome.storage && chrome.storage.local) {
      chrome.storage.local.set({ [key]: value });
    } else {
      try {
        localStorage.setItem(key, typeof value === 'boolean' ? String(value) : value);
      } catch (e) {
        console.error('Error saving setting to localStorage', e);
      }
    }
  }

  function loadAllSettings(callback) {
    if (typeof chrome !== 'undefined' && chrome.storage && chrome.storage.local) {
      chrome.storage.local.get(null, (settings) => {
        callback(settings || {});
      });
    } else {
      const settings = {};
      try {
        for (let i = 0; i < localStorage.length; i++) {
          const key = localStorage.key(i);
          const val = localStorage.getItem(key);
          if (val === 'true') {
            settings[key] = true;
          } else if (val === 'false') {
            settings[key] = false;
          } else {
            settings[key] = val;
          }
        }
      } catch (e) {
        console.error('Error loading settings from localStorage', e);
      }
      callback(settings);
    }
  }

  function initSettingsLogic() {
    const modal = document.getElementById('settingsModal') || document.getElementById('settingsBody');
    if (!modal) return;

    const toggles = modal.querySelectorAll('.st-toggle');
    const sliders = modal.querySelectorAll('.st-slider');
    const selects = modal.querySelectorAll('.st-select');
    const groupContainers = modal.querySelectorAll('.st-btn-group, .st-segment');

    // 1. Load all saved settings using chrome.storage.local.get(null, ...) with fallback to localStorage
    loadAllSettings((settings) => {
      if (!settings) return;

      // 1. Toggles: toggle.classList.toggle('on', settings[key]) for key toggle_${idx}
      toggles.forEach((toggle, idx) => {
        const key = `toggle_${idx}`;
        if (settings[key] !== undefined) {
          toggle.classList.toggle('on', Boolean(settings[key]));
        }
      });

      // 2. Sliders: slider.value = settings[key], update value display (.st-val), and dynamically update CSS variable
      sliders.forEach((slider, idx) => {
        const key = `slider_${idx}`;
        if (settings[key] !== undefined) {
          slider.value = settings[key];
          const valDisplay = slider.closest('.st-slider-field')?.querySelector('.st-val');
          if (valDisplay) {
            const initialText = valDisplay.textContent.trim();
            const unit = initialText.endsWith('%') ? '%' : 'px';
            valDisplay.textContent = `${slider.value}${unit}`;
          }
          if (idx === 0 || slider.min === '190' || slider.max === '380') {
            document.documentElement.style.setProperty('--board-w', slider.value + 'px');
          }
        }
      });

      // 3. Selects: select.value = settings[key] for key select_${idx}
      selects.forEach((select, idx) => {
        const key = `select_${idx}`;
        if (settings[key] !== undefined) {
          select.value = settings[key];
        }
      });

      // 4. Button groups & segments: activate button matching settings[key] for key group_${idx}
      groupContainers.forEach((container, idx) => {
        const key = `group_${idx}`;
        if (settings[key] !== undefined) {
          const btns = container.querySelectorAll('.st-group-btn, .st-seg-btn');
          btns.forEach((btn) => {
            btn.classList.toggle('active', btn.textContent.trim() === settings[key]);
          });
        }
      });
    });

    // 2. Implement dynamic event listeners:
    // Click on .st-toggle: toggle .on class, save toggle_${index} in storage
    toggles.forEach((toggle, index) => {
      toggle.addEventListener('click', (e) => {
        e.stopPropagation();
        toggle.classList.toggle('on');
        const isOn = toggle.classList.contains('on');
        saveSetting(`toggle_${index}`, isOn);
      });
    });

    // Input on .st-slider: update value text display, save slider_${index} in storage, and dynamically apply document.documentElement.style.setProperty('--board-w', slider.value + 'px')
    sliders.forEach((slider, index) => {
      slider.addEventListener('input', () => {
        const valDisplay = slider.closest('.st-slider-field')?.querySelector('.st-val');
        if (valDisplay) {
          const initialText = valDisplay.textContent.trim();
          const unit = initialText.endsWith('%') ? '%' : 'px';
          valDisplay.textContent = `${slider.value}${unit}`;
        }
        saveSetting(`slider_${index}`, slider.value);
        if (index === 0 || slider.min === '190' || slider.max === '380') {
          document.documentElement.style.setProperty('--board-w', slider.value + 'px');
        }
      });
    });

    // Change on .st-select: save select_${index} in storage
    selects.forEach((select, index) => {
      select.addEventListener('change', () => {
        saveSetting(`select_${index}`, select.value);
      });
    });

    // Click on .st-group-btn, .settings-modal .st-seg-btn: switch .active, save group_${index} in storage
    groupContainers.forEach((container, index) => {
      const btns = container.querySelectorAll('.st-group-btn, .st-seg-btn');
      btns.forEach((btn) => {
        btn.addEventListener('click', (e) => {
          e.stopPropagation();
          btns.forEach((b) => b.classList.remove('active'));
          btn.classList.add('active');
          saveSetting(`group_${index}`, btn.textContent.trim());
        });
      });
    });
  }

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

    // Support button click handler
    const supportNavBtn = document.getElementById('supportNavBtn');
    if (supportNavBtn) {
      supportNavBtn.addEventListener('click', () => {
        showToast('Служба поддержки: support@novatab.app');
      });
    }

    initSettingsLogic();
  }

  // --- Trash Modal Handling ---
  function openTrashModal() {
    if (!trashOverlay) return;
    trashOverlay.style.display = 'flex';
    if (trashConfirm) trashConfirm.style.display = 'none';
    if (trashEmptyBtn) trashEmptyBtn.style.display = 'inline-block';
  }

  function closeTrashModal() {
    if (!trashOverlay) return;
    trashOverlay.style.display = 'none';
    if (trashConfirm) trashConfirm.style.display = 'none';
    if (trashEmptyBtn) trashEmptyBtn.style.display = 'inline-block';
  }

  function initTrashModal() {
    if (trashCloseBtn) {
      trashCloseBtn.addEventListener('click', () => {
        closeTrashModal();
      });
    }

    if (trashOverlay) {
      trashOverlay.addEventListener('click', (e) => {
        if (e.target === trashOverlay) {
          closeTrashModal();
        }
      });
    }

    if (trashEmptyBtn && trashConfirm) {
      trashEmptyBtn.addEventListener('click', () => {
        trashConfirm.style.display = 'flex';
        trashEmptyBtn.style.display = 'none';
      });
    }

    if (trashConfirmCancel && trashConfirm) {
      trashConfirmCancel.addEventListener('click', () => {
        trashConfirm.style.display = 'none';
        if (trashEmptyBtn) trashEmptyBtn.style.display = 'inline-block';
      });
    }

    if (trashConfirmYes) {
      trashConfirmYes.addEventListener('click', () => {
        if (trashConfirm) trashConfirm.style.display = 'none';
        if (trashEmptyBtn) trashEmptyBtn.style.display = 'inline-block';
        if (trashList) {
          trashList.innerHTML = '<div class="trash-empty-msg" style="color: var(--ui-text-tertiary); font-size: 13px;">Корзина пуста</div>';
        }
        showToast('Корзина очищена');
      });
    }
  }

  // --- Search Overlay Handling ---
  function openSearchOverlay() {
    if (!searchOverlay) return;
    searchOverlay.style.display = 'flex';
    if (modalSearchInput) {
      modalSearchInput.value = '';
      renderSearchResults('');
      setTimeout(() => {
        modalSearchInput.focus();
        modalSearchInput.select();
      }, 50);
    } else if (searchInput) {
      searchInput.focus();
      searchInput.select();
    }
  }

  function closeSearchOverlay() {
    if (!searchOverlay) return;
    searchOverlay.style.display = 'none';
  }

  function renderSearchResults(query) {
    if (!searchResults) return;
    const trimmed = query.trim().toLowerCase();

    if (!trimmed) {
      searchResults.innerHTML = '';
      return;
    }

    const matches = [];
    if (appState && Array.isArray(appState.tabs)) {
      appState.tabs.forEach(tab => {
        if (tab.boards && Array.isArray(tab.boards)) {
          tab.boards.forEach(board => {
            if (board.links && Array.isArray(board.links)) {
              board.links.forEach(link => {
                const title = (link.title || '').toLowerCase();
                const url = (link.url || '').toLowerCase();
                const boardTitle = (board.title || '').toLowerCase();
                if (title.includes(trimmed) || url.includes(trimmed) || boardTitle.includes(trimmed)) {
                  matches.push({
                    link,
                    boardTitle: board.title,
                    tabTitle: tab.title
                  });
                }
              });
            }
          });
        }
      });
    }

    if (matches.length === 0) {
      searchResults.innerHTML = '<div style="padding: 24px 0; text-align: center; color: var(--ui-text-tertiary); font-size: 13px;">Ничего не найдено</div>';
      return;
    }

    searchResults.innerHTML = '';
    matches.forEach(match => {
      const item = document.createElement('a');
      item.className = 'search-results-item';
      item.href = normalizeUrl(match.link.url);
      item.addEventListener('click', (e) => {
        e.preventDefault();
        closeSearchOverlay();
        if (match.link.url) {
          window.location.href = normalizeUrl(match.link.url);
        }
      });

      const img = document.createElement('img');
      img.className = 'favicon';
      img.alt = '';
      img.src = getFaviconUrl(match.link.url);
      img.style.width = '18px';
      img.style.height = '18px';
      img.style.borderRadius = '4px';
      img.style.flexShrink = '0';
      img.addEventListener('error', () => {
        img.src = FALLBACK_FAVICON;
      });

      const titleSpan = document.createElement('span');
      titleSpan.textContent = match.link.title || match.link.url;
      titleSpan.style.flex = '1';
      titleSpan.style.overflow = 'hidden';
      titleSpan.style.textOverflow = 'ellipsis';
      titleSpan.style.whiteSpace = 'nowrap';
      titleSpan.style.fontSize = '14px';

      const pathSpan = document.createElement('span');
      pathSpan.textContent = match.boardTitle ? match.boardTitle : '';
      pathSpan.style.fontSize = '12px';
      pathSpan.style.color = 'var(--ui-text-tertiary)';
      pathSpan.style.flexShrink = '0';

      item.appendChild(img);
      item.appendChild(titleSpan);
      if (match.boardTitle) {
        item.appendChild(pathSpan);
      }

      searchResults.appendChild(item);
    });
  }

  function initSearchOverlay() {
    if (searchOverlay) {
      searchOverlay.addEventListener('click', (e) => {
        if (e.target === searchOverlay) {
          closeSearchOverlay();
        }
      });
    }

    if (modalSearchInput) {
      modalSearchInput.addEventListener('input', () => {
        renderSearchResults(modalSearchInput.value);
      });

      modalSearchInput.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
          const firstResult = searchResults?.querySelector('.search-results-item');
          if (firstResult) {
            firstResult.click();
          } else {
            const q = modalSearchInput.value.trim();
            if (q) {
              window.location.href = `https://www.google.com/search?q=${encodeURIComponent(q)}`;
            }
          }
        }
      });
    }
  }

  // --- Wallpaper Handling ---
  function applyWallpaper(url) {
    const bgLayer = document.getElementById('bg-layer');
    if (!bgLayer || !url) return;

    if (url.startsWith('linear-gradient') || url.startsWith('radial-gradient')) {
      bgLayer.style.background = url;
    } else {
      bgLayer.style.backgroundImage = `url("${url}")`;
      bgLayer.style.backgroundSize = 'cover';
      bgLayer.style.backgroundPosition = 'center';
      bgLayer.style.backgroundRepeat = 'no-repeat';
    }

    if (typeof chrome !== 'undefined' && chrome.storage && chrome.storage.local) {
      chrome.storage.local.set({ savedWallpaper: url });
    } else {
      try {
        localStorage.setItem('savedWallpaper', url);
      } catch (e) {}
    }
  }

  function loadSavedWallpaper() {
    if (typeof chrome !== 'undefined' && chrome.storage && chrome.storage.local) {
      chrome.storage.local.get(['savedWallpaper'], (result) => {
        if (result && result.savedWallpaper) {
          applyWallpaper(result.savedWallpaper);
        } else {
          applyWallpaper(WALLPAPER_PRESETS[0]);
        }
      });
    } else {
      const saved = localStorage.getItem('savedWallpaper');
      if (saved) {
        applyWallpaper(saved);
      } else {
        applyWallpaper(WALLPAPER_PRESETS[0]);
      }
    }
  }

  function initWallpaperPresetsGrid() {
    const grid = document.getElementById('wpPresetsGrid');
    if (!grid) return;
    grid.innerHTML = '';

    WALLPAPER_PRESETS.forEach((wpPath, index) => {
      const btn = document.createElement('button');
      btn.className = 'wp-preset-item';
      btn.style.backgroundImage = `url("${wpPath}")`;
      btn.title = `Preset ${index + 1}`;
      btn.addEventListener('click', () => {
        applyWallpaper(wpPath);
        showToast('Обои обновлены');
      });
      grid.appendChild(btn);
    });
  }

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
          applyWallpaper(event.target.result);
          showToast('Обои обновлены');
        };
        reader.readAsDataURL(file);
        wpFileInput.value = '';
      });
    }

    // Search Online Button Click
    if (wpSearchOnlineBtn) {
      wpSearchOnlineBtn.addEventListener('click', () => {
        window.open('https://unsplash.com/s/photos/wallpaper', '_blank');
      });
    }
  }

  // --- Drag and Drop ---
  function initDragAndDrop() {
    const grid = document.querySelector('.boards-grid');
    if (!grid) return;
    let draggedItem = null;

    grid.addEventListener('dragstart', (e) => {
      const card = e.target.closest('.board, .board-card');
      if (card && !card.classList.contains('board-placeholder')) {
        draggedItem = card;
        setTimeout(() => draggedItem.classList.add('dragging'), 0);
      }
    });

    grid.addEventListener('dragend', (e) => {
      if (draggedItem) {
        draggedItem.classList.remove('dragging');
        draggedItem = null;

        // Update order in state
        const currentTab = getActiveTab();
        if (currentTab && currentTab.boards) {
          const boardElements = [...grid.querySelectorAll('.board:not(.board-placeholder), .board-card:not(.board-placeholder)')];
          const newBoards = [];
          boardElements.forEach((el) => {
            const bId = el.dataset.boardId || el.dataset.id;
            const found = currentTab.boards.find(b => b.id === bId);
            if (found && !newBoards.includes(found)) newBoards.push(found);
          });
          currentTab.boards.forEach(b => {
            if (!newBoards.includes(b)) newBoards.push(b);
          });
          currentTab.boards = newBoards;
          saveState();
        }
      }
    });

    grid.addEventListener('dragover', (e) => {
      e.preventDefault();
      if (!draggedItem) return;

      const afterElement = getDragAfterElement(grid, e.clientX);
      const placeholder = document.getElementById('addBoardPlaceholder');

      if (afterElement == null || afterElement === placeholder) {
        if (placeholder) {
          grid.insertBefore(draggedItem, placeholder);
        } else {
          grid.appendChild(draggedItem);
        }
      } else {
        grid.insertBefore(draggedItem, afterElement);
      }
    });

    function getDragAfterElement(container, x) {
      const draggableElements = [...container.querySelectorAll('.board:not(.dragging):not(.board-placeholder), .board-card:not(.dragging):not(.board-placeholder)')];
      
      return draggableElements.reduce((closest, child) => {
        const box = child.getBoundingClientRect();
        const offset = x - (box.left + box.width / 2);
        if (offset < 0 && offset > closest.offset) {
          return { offset: offset, element: child };
        } else {
          return closest;
        }
      }, { offset: Number.NEGATIVE_INFINITY }).element;
    }
  }

  // --- Initializer ---
  function init() {
    loadSavedWallpaper();
    loadState(() => {
      renderTabs();
      renderBoards();
    });
    initBoardMenu();
    initWidgetGallery();
    initSettingsModal();
    initTrashModal();
    initSearchOverlay();
    initWallpaperModal();
    initWallpaperPresetsGrid();
    initDragAndDrop();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
