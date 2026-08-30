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

  const SETTINGS_MAP = {
    'navSearchToggle': 'setting-show-search',
    'descToggle': 'setting-show-descriptions',
    'sidebarAlwaysOpenToggle': 'setting-sidebar-always-open',
    'clockToggle': 'setting-show-clock',
    'weatherToggle': 'setting-show-weather',
    'openNewTabToggle': 'setting-open-new-tab',
    'hideExcessBookmarksToggle': 'setting-hide-excess-bookmarks'
  };

  const TOAST_MESSAGES = {
    'clockToggle': { on: 'Часы включены', off: 'Часы выключены' },
    'navSearchToggle': { on: 'Поиск включен', off: 'Поиск выключен' },
    'weatherToggle': { on: 'Погода включена', off: 'Погода выключена' },
    'openNewTabToggle': { on: 'Открывать ссылки в новой вкладке: Включено', off: 'Открывать ссылки в новой вкладке: Выключено' },
    'descToggle': { on: 'Описания включены', off: 'Описания выключены' },
    'sidebarAlwaysOpenToggle': { on: 'Боковая панель всегда открыта', off: 'Боковая панель скрыта' },
    'hideExcessBookmarksToggle': { on: 'Лишние закладки скрыты', off: 'Все закладки показаны' }
  };

  const I18N_STRINGS = {
    ru: {
      'settings.title': 'Настройки',
      'settings.tabGeneral': 'Общие',
      'settings.tabAppearance': 'Внешний вид',
      'settings.tabRegion': 'Язык и регион',
      'settings.tabSupport': 'Поддержка',
      'settings.behavior': 'ПОВЕДЕНИЕ',
      'settings.openNewTab': 'Открывать ссылки в новой вкладке',
      'settings.hideExcess': 'Скрывать лишние закладки',
      'settings.showDesc': 'Показывать описания',
      'settings.layout': 'РАСПОЛОЖЕНИЕ',
      'settings.columns': 'Количество колонок',
      'settings.columnsAuto': 'Авто',
      'settings.boardWidth': 'Ширина досок',
      'settings.sidebar': 'БОКОВАЯ ПАНЕЛЬ',
      'settings.sidebarAlwaysOpen': 'Всегда показывать все кнопки',
      'settings.quickSave': 'БЫСТРОЕ СОХРАНЕНИЕ',
      'settings.saveToBoard': 'Сохранять на доску',
      'settings.hotkey': 'Горячая клавиша',
      'settings.notSet': 'Не задано',
      'settings.pressKeys': 'Нажмите клавиши...',
      'settings.changeHotkey': 'Изменить',
      'settings.searchColor': 'Цвет плашки поиска',
      'settings.searchAlpha': 'Прозрачность',
      'settings.searchBlur': 'Размытие',
      'settings.searchWidth': 'Ширина',
      'settings.boardText': 'ТЕКСТ ДОСКИ',
      'settings.textSize': 'Размер',
      'settings.textWeight': 'Толщина',
      'settings.normalWeight': 'Обычный',
      'settings.boldWeight': 'Жирный',
      'settings.language': 'ЯЗЫК',
      'settings.formatting': 'Форматирование',
      'settings.autoDetect': 'Автоопределение',
      'settings.timeFormat': 'Формат времени',
      'settings.dateFormat': 'Формат даты',
      'settings.weekStart': 'Начало недели',
      'settings.monday': 'Понедельник',
      'settings.sunday': 'Воскресенье',
      'settings.temperature': 'Температура',
      'search.placeholder': 'Поиск...',
      'search.modalPlaceholder': 'Поиск по закладкам…',
      'search.notFound': 'Ничего не найдено',
      'sidebar.search': 'Поиск',
      'sidebar.wallpaper': 'Обои',
      'sidebar.widgets': 'Виджеты',
      'sidebar.import': 'Импорт',
      'sidebar.trash': 'Корзина',
      'sidebar.settings': 'Настройки',
      'sidebar.menu': 'Меню',
      'trash.title': 'Корзина',
      'trash.empty': 'Очистить корзину',
      'trash.emptyMsg': 'Корзина пуста',
      'trash.emptyConfirm': 'Очистить корзину?',
      'trash.deleteAll': 'Удалить всё',
      'wallpaper.title': 'Обои',
      'wallpaper.uploadTitle': 'Загрузите изображение или видео',
      'wallpaper.presets': 'ПРЕСЕТЫ',
      'wallpaper.searchTitle': 'НАЙТИ ОБОИ',
      'wallpaper.searchBtn': 'Искать обои в интернете',
      'widgets.label': 'ВИДЖЕТЫ',
      'widget.board': 'Доска',
      'widget.notes': 'Заметки',
      'widget.calendar': 'Календарь',
      'widget.pomodoro': 'Помодоро',
      'widget.clock': 'Часы',
      'widget.search': 'Поиск',
      'widget.weather': 'Погода',
      'widget.add': 'Добавить',
      'boardMenu.rename': 'Переименовать',
      'boardMenu.openAll': 'Открыть все ссылки',
      'boardMenu.customize': 'Кастомизация',
      'boardMenu.corner': 'Уголок',
      'boardMenu.border': 'Обводка',
      'boardMenu.delete': 'Удалить доску',
      'modal.addBookmark': 'Добавить закладку',
      'modal.editBookmark': 'Изменить закладку',
      'modal.addBoard': 'Новая доска',
      'modal.editBoard': 'Переименовать доску',
      'modal.addTab': 'Новая страница',
      'modal.editTab': 'Переименовать вкладку',
      'modal.titleLabel': 'Название',
      'modal.urlLabel': 'URL адрес',
      'modal.cancel': 'Отмена',
      'modal.save': 'Сохранить',
      'context.openNewTab': 'Открыть в новой вкладке',
      'context.openIncognito': 'Открыть в режиме инкогнито',
      'context.edit': 'Изменить',
      'context.copyUrl': 'Копировать адрес ссылки',
      'context.delete': 'Удалить',
      'toast.regionDetected': 'Настройки региона определены автоматически',
      'toast.hotkeySaved': 'Горячая клавиша сохранена: ',
      'toast.quickSaveTriggered': 'Быстрое сохранение: '
    },
    en: {
      'settings.title': 'Settings',
      'settings.tabGeneral': 'General',
      'settings.tabAppearance': 'Appearance',
      'settings.tabRegion': 'Language & Region',
      'settings.tabSupport': 'Support',
      'settings.behavior': 'BEHAVIOR',
      'settings.openNewTab': 'Open links in new tab',
      'settings.hideExcess': 'Hide excess bookmarks',
      'settings.showDesc': 'Show descriptions',
      'settings.layout': 'LAYOUT',
      'settings.columns': 'Columns count',
      'settings.columnsAuto': 'Auto',
      'settings.boardWidth': 'Board width',
      'settings.sidebar': 'SIDEBAR',
      'settings.sidebarAlwaysOpen': 'Always show all buttons',
      'settings.quickSave': 'QUICK SAVE',
      'settings.saveToBoard': 'Save to board',
      'settings.hotkey': 'Shortcut',
      'settings.notSet': 'Not set',
      'settings.pressKeys': 'Press keys...',
      'settings.changeHotkey': 'Change',
      'settings.searchColor': 'Search bar color',
      'settings.searchAlpha': 'Opacity',
      'settings.searchBlur': 'Blur',
      'settings.searchWidth': 'Width',
      'settings.boardText': 'BOARD TEXT',
      'settings.textSize': 'Size',
      'settings.textWeight': 'Weight',
      'settings.normalWeight': 'Normal',
      'settings.boldWeight': 'Bold',
      'settings.language': 'LANGUAGE',
      'settings.formatting': 'Formatting',
      'settings.autoDetect': 'Auto detect',
      'settings.timeFormat': 'Time format',
      'settings.dateFormat': 'Date format',
      'settings.weekStart': 'First day of week',
      'settings.monday': 'Monday',
      'settings.sunday': 'Sunday',
      'settings.temperature': 'Temperature',
      'search.placeholder': 'Search...',
      'search.modalPlaceholder': 'Search bookmarks…',
      'search.notFound': 'Nothing found',
      'sidebar.search': 'Search',
      'sidebar.wallpaper': 'Wallpaper',
      'sidebar.widgets': 'Widgets',
      'sidebar.import': 'Import',
      'sidebar.trash': 'Trash',
      'sidebar.settings': 'Settings',
      'sidebar.menu': 'Menu',
      'trash.title': 'Trash',
      'trash.empty': 'Empty trash',
      'trash.emptyMsg': 'Trash is empty',
      'trash.emptyConfirm': 'Empty trash?',
      'trash.deleteAll': 'Delete all',
      'wallpaper.title': 'Wallpaper',
      'wallpaper.uploadTitle': 'Upload image or video',
      'wallpaper.presets': 'PRESETS',
      'wallpaper.searchTitle': 'FIND WALLPAPER',
      'wallpaper.searchBtn': 'Search wallpapers online',
      'widgets.label': 'WIDGETS',
      'widget.board': 'Board',
      'widget.notes': 'Notes',
      'widget.calendar': 'Calendar',
      'widget.pomodoro': 'Pomodoro',
      'widget.clock': 'Clock',
      'widget.search': 'Search',
      'widget.weather': 'Weather',
      'widget.add': 'Add',
      'boardMenu.rename': 'Rename',
      'boardMenu.openAll': 'Open all links',
      'boardMenu.customize': 'Customize',
      'boardMenu.corner': 'Corner',
      'boardMenu.border': 'Border',
      'boardMenu.delete': 'Delete board',
      'modal.addBookmark': 'Add Bookmark',
      'modal.editBookmark': 'Edit Bookmark',
      'modal.addBoard': 'New Board',
      'modal.editBoard': 'Rename Board',
      'modal.addTab': 'New Page',
      'modal.editTab': 'Rename Tab',
      'modal.titleLabel': 'Title',
      'modal.urlLabel': 'URL address',
      'modal.cancel': 'Cancel',
      'modal.save': 'Save',
      'context.openNewTab': 'Open in new tab',
      'context.openIncognito': 'Open in incognito mode',
      'context.edit': 'Edit',
      'context.copyUrl': 'Copy link address',
      'context.delete': 'Delete',
      'toast.regionDetected': 'Region settings detected automatically',
      'toast.hotkeySaved': 'Shortcut saved: ',
      'toast.quickSaveTriggered': 'Quick save: '
    },
    de: {
      'settings.title': 'Einstellungen',
      'settings.tabGeneral': 'Allgemein',
      'settings.tabAppearance': 'Erscheinungsbild',
      'settings.tabRegion': 'Sprache & Region',
      'settings.tabSupport': 'Support',
      'settings.behavior': 'VERHALTEN',
      'settings.openNewTab': 'Links in neuem Tab öffnen',
      'settings.hideExcess': 'Überschüssige Lesezeichen ausblenden',
      'settings.showDesc': 'Beschreibungen anzeigen',
      'settings.layout': 'LAYOUT',
      'settings.columns': 'Spaltenanzahl',
      'settings.columnsAuto': 'Auto',
      'settings.boardWidth': 'Board-Breite',
      'settings.sidebar': 'SEITENLEISTE',
      'settings.sidebarAlwaysOpen': 'Alle Schaltflächen immer anzeigen',
      'settings.quickSave': 'SCHNELLSPEICHERN',
      'settings.saveToBoard': 'Auf Board speichern',
      'settings.hotkey': 'Tastenkürzel',
      'settings.notSet': 'Nicht festgelegt',
      'settings.pressKeys': 'Tasten drücken...',
      'settings.changeHotkey': 'Ändern',
      'settings.searchColor': 'Suchleistenfarbe',
      'settings.searchAlpha': 'Deckkraft',
      'settings.searchBlur': 'Weichzeichner',
      'settings.searchWidth': 'Breite',
      'settings.boardText': 'BOARD-TEXT',
      'settings.textSize': 'Größe',
      'settings.textWeight': 'Stärke',
      'settings.normalWeight': 'Normal',
      'settings.boldWeight': 'Fett',
      'settings.language': 'SPRACHE',
      'settings.formatting': 'Formatierung',
      'settings.autoDetect': 'Automatisch erkennen',
      'settings.timeFormat': 'Zeitformat',
      'settings.dateFormat': 'Datumsformat',
      'settings.weekStart': 'Wochenbeginn',
      'settings.monday': 'Montag',
      'settings.sunday': 'Sonntag',
      'settings.temperature': 'Temperatur',
      'search.placeholder': 'Suchen...',
      'search.modalPlaceholder': 'Lesezeichen suchen…',
      'search.notFound': 'Nichts gefunden',
      'sidebar.search': 'Suchen',
      'sidebar.wallpaper': 'Hintergrund',
      'sidebar.widgets': 'Widgets',
      'sidebar.import': 'Importieren',
      'sidebar.trash': 'Papierkorb',
      'sidebar.settings': 'Einstellungen',
      'sidebar.menu': 'Menü',
      'trash.title': 'Papierkorb',
      'trash.empty': 'Papierkorb leeren',
      'trash.emptyMsg': 'Papierkorb ist leer',
      'trash.emptyConfirm': 'Papierkorb leeren?',
      'trash.deleteAll': 'Alles löschen',
      'wallpaper.title': 'Hintergrund',
      'wallpaper.uploadTitle': 'Bild oder Video hochladen',
      'wallpaper.presets': 'VOREINSTELLUNGEN',
      'wallpaper.searchTitle': 'HINTERGRUND FINDEN',
      'wallpaper.searchBtn': 'Hintergründe online suchen',
      'widgets.label': 'WIDGETS',
      'widget.board': 'Board',
      'widget.notes': 'Notizen',
      'widget.calendar': 'Kalender',
      'widget.pomodoro': 'Pomodoro',
      'widget.clock': 'Uhr',
      'widget.search': 'Suchen',
      'widget.weather': 'Wetter',
      'widget.add': 'Hinzufügen',
      'boardMenu.rename': 'Umbenennen',
      'boardMenu.openAll': 'Alle Links öffnen',
      'boardMenu.customize': 'Anpassen',
      'boardMenu.corner': 'Ecke',
      'boardMenu.border': 'Rahmen',
      'boardMenu.delete': 'Board löschen',
      'modal.addBookmark': 'Lesezeichen hinzufügen',
      'modal.editBookmark': 'Lesezeichen bearbeiten',
      'modal.addBoard': 'Neues Board',
      'modal.editBoard': 'Board umbenennen',
      'modal.addTab': 'Neue Seite',
      'modal.editTab': 'Tab umbenennen',
      'modal.titleLabel': 'Titel',
      'modal.urlLabel': 'URL-Adresse',
      'modal.cancel': 'Abbrechen',
      'modal.save': 'Speichern',
      'context.openNewTab': 'In neuem Tab öffnen',
      'context.openIncognito': 'Im Inkognito-Modus öffnen',
      'context.edit': 'Bearbeiten',
      'context.copyUrl': 'Link-Adresse kopieren',
      'context.delete': 'Löschen',
      'toast.regionDetected': 'Regionseinstellungen automatisch erkannt',
      'toast.hotkeySaved': 'Tastenkürzel gespeichert: ',
      'toast.quickSaveTriggered': 'Schnellspeichern: '
    }
  };

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
  let currentLanguage = 'ru';
  let currentWeekStart = 'mon';
  let currentQuickBoardId = null;
  let currentHotkey = 'Alt+B';
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

  function setLanguage(lang) {
    if (!I18N_STRINGS[lang]) lang = 'ru';
    currentLanguage = lang;
    document.documentElement.lang = lang;
    saveSetting('stSegLanguage', lang);
    saveSetting('language', lang);

    const dict = I18N_STRINGS[lang];

    // Update all elements with data-i18n
    document.querySelectorAll('[data-i18n]').forEach((el) => {
      const key = el.dataset.i18n;
      if (dict[key]) {
        if (el.tagName === 'INPUT' && el.getAttribute('placeholder')) {
          el.placeholder = dict[key];
        } else {
          el.textContent = dict[key];
        }
      }
    });

    // Update settings nav item labels
    const navGeneral = document.querySelector('.settings-nav-item[data-tab="tab-general"] span');
    if (navGeneral) navGeneral.textContent = dict['settings.tabGeneral'];
    const navApp = document.querySelector('.settings-nav-item[data-tab="tab-appearance"] span');
    if (navApp) navApp.textContent = dict['settings.tabAppearance'];
    const navRegion = document.querySelector('.settings-nav-item[data-tab="tab-region"] span');
    if (navRegion) navRegion.textContent = dict['settings.tabRegion'];
    const navSupport = document.querySelector('#supportNavBtn span');
    if (navSupport) navSupport.textContent = dict['settings.tabSupport'];

    // Update search placeholders
    if (searchInput) searchInput.placeholder = dict['search.placeholder'];
    if (modalSearchInput) modalSearchInput.placeholder = dict['search.modalPlaceholder'];

    // Update active state in language segment
    const langSegment = document.getElementById('stSegLanguage');
    if (langSegment) {
      langSegment.querySelectorAll('.st-seg-btn').forEach((btn) => {
        const bLang = btn.dataset.lang || (btn.textContent.trim() === 'English' ? 'en' : btn.textContent.trim() === 'Deutsch' ? 'de' : 'ru');
        btn.classList.toggle('active', bLang === lang);
      });
    }

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
          populateQuickBoardSelect();
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

    // Remove all existing board cards and placeholders
    const existingCards = boardsGrid.querySelectorAll('.board-card, .board, .board-placeholder');
    existingCards.forEach((c) => c.remove());

    const currentTab = getActiveTab();
    if (!currentTab || !currentTab.boards) {
      renderGridPlaceholders();
      return;
    }

    const dict = I18N_STRINGS[currentLanguage] || I18N_STRINGS.ru;

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
        title.textContent = board.title || dict['widget.notes'] || 'Заметки';
        title.title = board.title || dict['widget.notes'] || 'Заметки';

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
        textarea.placeholder = currentLanguage === 'en' ? 'Type notes here...' : currentLanguage === 'de' ? 'Notizen hier eingeben...' : 'Введите текст...';
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

        boardsGrid.appendChild(card);
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
        const monthYearStr = currentLanguage === 'en' ? 'August 2026' : currentLanguage === 'de' ? 'August 2026' : 'Август 2026';
        title.textContent = board.title || monthYearStr;
        title.title = board.title || monthYearStr;
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

        // Days row headers based on currentLanguage and currentWeekStart ('mon' vs 'sun')
        const daysRow = document.createElement('div');
        daysRow.className = 'cal-days-row';

        let dayNames;
        if (currentWeekStart === 'sun') {
          if (currentLanguage === 'en') {
            dayNames = ['SU', 'MO', 'TU', 'WE', 'TH', 'FR', 'SA'];
          } else if (currentLanguage === 'de') {
            dayNames = ['SO', 'MO', 'DI', 'MI', 'DO', 'FR', 'SA'];
          } else {
            dayNames = ['ВС', 'ПН', 'ВТ', 'СР', 'ЧТ', 'ПТ', 'СБ'];
          }
        } else {
          if (currentLanguage === 'en') {
            dayNames = ['MO', 'TU', 'WE', 'TH', 'FR', 'SA', 'SU'];
          } else if (currentLanguage === 'de') {
            dayNames = ['MO', 'DI', 'MI', 'DO', 'FR', 'SA', 'SO'];
          } else {
            dayNames = ['ПН', 'ВТ', 'СР', 'ЧТ', 'ПТ', 'СБ', 'ВС'];
          }
        }

        dayNames.forEach((name) => {
          const dayNameEl = document.createElement('div');
          dayNameEl.className = 'cal-day-name';
          dayNameEl.textContent = name;
          daysRow.appendChild(dayNameEl);
        });
        card.appendChild(daysRow);

        // Calendar Grid: August 2026 (Aug 1 is Saturday)
        const calGrid = document.createElement('div');
        calGrid.className = 'cal-grid';

        // Saturday index:
        // When week starts on Monday: Mon(0), Tue(1), Wed(2), Thu(3), Fri(4), Sat(5), Sun(6) -> 5 blank days before Aug 1
        // When week starts on Sunday: Sun(0), Mon(1), Tue(2), Wed(3), Thu(4), Fri(5), Sat(6) -> 6 blank days before Aug 1
        const blankDaysCount = currentWeekStart === 'sun' ? 6 : 5;

        for (let i = 0; i < blankDaysCount; i++) {
          const blank = document.createElement('div');
          blank.className = 'cal-day cal-day-blank';
          calGrid.appendChild(blank);
        }

        // 31 days in August 2026
        for (let day = 1; day <= 31; day++) {
          const dayEl = document.createElement('div');
          let dayOfWeek;
          let isWeekend;

          if (currentWeekStart === 'sun') {
            dayOfWeek = (6 + day - 1) % 7; // 0=Sun, 1=Mon, ..., 6=Sat
            isWeekend = (dayOfWeek === 0 || dayOfWeek === 6);
          } else {
            dayOfWeek = (5 + day - 1) % 7; // 0=Mon, 1=Tue, ..., 5=Sat, 6=Sun
            isWeekend = (dayOfWeek === 5 || dayOfWeek === 6);
          }

          const isToday = (day === 30);

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

        boardsGrid.appendChild(card);
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

          if (link.desc) {
            const descSpan = document.createElement('span');
            descSpan.className = 'link-desc';
            descSpan.textContent = link.desc;
            item.appendChild(descSpan);
          }

          // Left Click: Open Link
          item.addEventListener('click', (e) => {
            e.preventDefault();
            if (link.url) {
              const openInNewTab = document.body.classList.contains('setting-open-new-tab');
              if (openInNewTab) {
                window.open(normalizeUrl(link.url), '_blank');
              } else {
                window.location.href = normalizeUrl(link.url);
              }
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

      boardsGrid.appendChild(card);
    });

    renderGridPlaceholders();
    applySearchFilter();
  }

  // --- Dynamic Grid Placeholders & Inline Board Creation ---

  function renderGridPlaceholders() {
    const grid = document.querySelector('.boards-grid') || boardsGrid;
    if (!grid) return;
    // Remove old placeholders
    grid.querySelectorAll('.board-placeholder').forEach(el => el.remove());
    
    // Count current boards
    const boardsCount = grid.querySelectorAll('.board:not(.board-placeholder):not(.inline-creating), .board-card:not(.board-placeholder):not(.inline-creating)').length;
    // Fill up empty slots (at least 1, up to 10 - boardsCount)
    const placeholdersNeeded = Math.max(1, 10 - boardsCount);

    for (let i = 0; i < placeholdersNeeded; i++) {
      const slot = document.createElement('div');
      slot.className = 'board-placeholder';
      slot.innerHTML = '<span class="plus-icon">+</span>';
      
      // Clicking any slot starts inline board creation at that position
      slot.addEventListener('click', () => initiateInlineBoardCreation(slot));
      grid.appendChild(slot);
    }
  }

  function initiateInlineBoardCreation(targetSlot) {
    const grid = document.querySelector('.boards-grid') || boardsGrid;
    if (!grid) return;

    // If an inline creation card is already open, focus it
    const existingInline = grid.querySelector('.board-card.inline-creating');
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
    input.placeholder = currentLanguage === 'en' ? 'New Board' : currentLanguage === 'de' ? 'Neues Board' : 'Новая доска';
    input.maxLength = 50;

    header.appendChild(input);
    tempCard.appendChild(header);

    if (targetSlot && targetSlot.parentNode === grid) {
      grid.insertBefore(tempCard, targetSlot);
      targetSlot.remove();
    } else {
      const firstPlaceholder = grid.querySelector('.board-placeholder');
      if (firstPlaceholder) {
        grid.insertBefore(tempCard, firstPlaceholder);
        firstPlaceholder.remove();
      } else {
        grid.appendChild(tempCard);
      }
    }

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
          const allBoardElements = [...grid.querySelectorAll('.board:not(.board-placeholder), .board-card:not(.board-placeholder)')];
          const insertIdx = allBoardElements.indexOf(tempCard);
          if (insertIdx >= 0 && insertIdx <= currentTab.boards.length) {
            currentTab.boards.splice(insertIdx, 0, newBoard);
          } else {
            currentTab.boards.push(newBoard);
          }
          saveState();
          renderBoards();
          populateQuickBoardSelect();
          showToast(`Доска "${title}" создана`);
        } else {
          tempCard.remove();
          renderGridPlaceholders();
        }
      } else {
        tempCard.remove();
        renderGridPlaceholders();
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
        renderGridPlaceholders();
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
    input.placeholder = currentLanguage === 'en' ? 'Board Title' : currentLanguage === 'de' ? 'Board-Titel' : 'Название доски';
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
        populateQuickBoardSelect();
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
            renderGridPlaceholders();
            populateQuickBoardSelect();
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
  contextMenu?.addEventListener('click', (e) => {
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
    const card = boardsGrid?.querySelector(`.board-card[data-board-id="${board.id}"], .board[data-board-id="${board.id}"]`);
    openBoardMenu(e, board, card);
  }

  function handleBoardAction(action, target) {
    if (action === 'delete') {
      const tab = appState.tabs.find((t) => t.id === target.tabId);
      if (!tab) return;
      tab.boards = tab.boards.filter((b) => b.id !== target.boardId);
      saveState();
      renderBoards();
      renderGridPlaceholders();
      populateQuickBoardSelect();
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
      populateQuickBoardSelect();
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

  modalCancelBtn?.addEventListener('click', () => {
    closeModal();
  });

  // Close modal when clicking backdrop
  itemModal?.addEventListener('click', (e) => {
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

  modalForm?.addEventListener('submit', (e) => {
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
    const dict = I18N_STRINGS[currentLanguage] || I18N_STRINGS.ru;
    openModal({
      title: dict['modal.addBookmark'] || 'Добавить закладку',
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
    const dict = I18N_STRINGS[currentLanguage] || I18N_STRINGS.ru;
    openModal({
      title: dict['modal.editBookmark'] || 'Изменить закладку',
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
    const dict = I18N_STRINGS[currentLanguage] || I18N_STRINGS.ru;
    openModal({
      title: dict['modal.addBoard'] || 'Новая доска',
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
        populateQuickBoardSelect();
        showToast(`Доска "${title}" создана`);
      }
    });
  }

  function openEditBoardModal(target) {
    const dict = I18N_STRINGS[currentLanguage] || I18N_STRINGS.ru;
    openModal({
      title: dict['modal.editBoard'] || 'Переименовать доску',
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
        populateQuickBoardSelect();
        showToast('Доска переименована');
      }
    });
  }

  function openAddTabModal() {
    const dict = I18N_STRINGS[currentLanguage] || I18N_STRINGS.ru;
    openModal({
      title: dict['modal.addTab'] || 'Новая страница',
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
        populateQuickBoardSelect();
        showToast(`Вкладка "${title}" создана`);
      }
    });
  }

  function openEditTabModal(target) {
    const dict = I18N_STRINGS[currentLanguage] || I18N_STRINGS.ru;
    openModal({
      title: dict['modal.editTab'] || 'Переименовать вкладку',
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

  if (searchInput) {
    searchInput.addEventListener('input', applySearchFilter);

    searchInput.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        const query = searchInput.value.trim();
        if (query) {
          window.location.href = `https://www.google.com/search?q=${encodeURIComponent(query)}`;
        }
      }
    });
  }

  const engineIcon = document.querySelector('.engine-icon');
  if (engineIcon) {
    engineIcon.addEventListener('click', () => {
      const query = searchInput?.value.trim();
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
      initiateInlineBoardCreation();
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
    const boardMenuEl = document.getElementById('boardMenu');

    if (menuBtn) {
      e.stopPropagation();
      const rect = menuBtn.getBoundingClientRect();
      if (!boardMenuEl) return;
      boardMenuEl.style.display = 'block';
      boardMenuEl.style.position = 'fixed';

      boardMenuEl.style.left = `${rect.right + 8}px`;
      boardMenuEl.style.top = `${rect.top}px`;

      const card = menuBtn.closest('.board-card') || menuBtn.closest('.board');
      const boardId = card ? (card.dataset.boardId || card.dataset.id) : null;
      boardMenuEl.dataset.targetId = boardId;
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
      if (boardMenuEl) boardMenuEl.style.display = 'none';
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
    const gallery = document.getElementById('widgetGallery');
    const sideEl = document.getElementById('sidebar');

    if (sideWidgetsBtn && gallery) {
      sideWidgetsBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        const isHidden = gallery.style.display === 'none' || !gallery.style.display;
        gallery.style.display = isHidden ? 'block' : 'none';
        if (sideEl) sideEl.classList.remove('is-open');
      });
    }

    // Close widgets on click outside
    document.addEventListener('click', (e) => {
      if (gallery && !e.target.closest('#widgetGallery') && !e.target.closest('#sideWidgets')) {
        gallery.style.display = 'none';
      }
    });

    if (!gallery) return;

    // Add board (#wcBoard .widget-add-btn)
    const wcBoardBtn = document.querySelector('#wcBoard .widget-add-btn');
    if (wcBoardBtn) {
      wcBoardBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        gallery.style.display = 'none';
        initiateInlineBoardCreation();
      });
    }

    // Add notes (#wcNotes .widget-add-btn)
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
        if (gallery) gallery.style.display = 'none';
        showToast('Виджет "Заметки" добавлен');
      });
    }

    // Add calendar (#wcCalendar .widget-add-btn)
    const wcCalendarBtn = document.querySelector('#wcCalendar .widget-add-btn');
    if (wcCalendarBtn) {
      wcCalendarBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        insertWidgetToGrid({
          id: 'cal_' + Date.now(),
          type: 'calendar',
          title: 'Август 2026'
        });
        if (gallery) gallery.style.display = 'none';
        showToast('Виджет "Календарь" добавлен');
      });
    }

    // Add pomodoro (#wcPomodoro .widget-add-btn)
    const wcPomodoroBtn = document.querySelector('#wcPomodoro .widget-add-btn');
    if (wcPomodoroBtn) {
      wcPomodoroBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        showToast('Виджет "Помодоро" скоро будет доступен');
      });
    }
  }

  // --- Reactive Settings Engine & State Persistence ---

  function applySettingToDOM(settingId, isEnabled) {
    const className = SETTINGS_MAP[settingId];
    if (className) {
      document.body.classList.toggle(className, isEnabled);
    }
    if (settingId === 'navSearchToggle') {
      const searchBar = document.querySelector('.search-bar');
      if (searchBar) {
        searchBar.style.display = isEnabled ? 'flex' : 'none';
      }
    }
  }

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

  function handleToggleClick(toggle) {
    toggle.classList.toggle('on');
    const isOn = toggle.classList.contains('on');
    const toggleId = toggle.id;

    if (toggleId) {
      // Sync other toggle instances with same ID if present
      document.querySelectorAll(`.st-toggle#${toggleId}`).forEach((t) => {
        if (t !== toggle) t.classList.toggle('on', isOn);
      });

      saveSetting(toggleId, isOn);
      applySettingToDOM(toggleId, isOn);

      if (TOAST_MESSAGES[toggleId]) {
        showToast(isOn ? TOAST_MESSAGES[toggleId].on : TOAST_MESSAGES[toggleId].off);
      }
    }
  }

  function populateQuickBoardSelect() {
    const select = document.getElementById('stSelectQuickBoard');
    if (!select) return;

    const currentTab = getActiveTab();
    select.innerHTML = '';

    if (currentTab && Array.isArray(currentTab.boards) && currentTab.boards.length > 0) {
      currentTab.boards.forEach((b) => {
        const opt = document.createElement('option');
        opt.value = b.id;
        opt.textContent = b.title || (b.type === 'notes' ? 'Заметки' : b.type === 'calendar' ? 'Календарь' : 'Доска');
        select.appendChild(opt);
      });

      if (currentQuickBoardId) {
        select.value = currentQuickBoardId;
      }
      if (!select.value && select.options.length > 0) {
        select.selectedIndex = 0;
        currentQuickBoardId = select.value;
      }
    } else {
      const opt = document.createElement('option');
      opt.value = '';
      opt.textContent = 'Нет доступных досок';
      select.appendChild(opt);
    }
  }

  function applyColumnsSetting(colsVal) {
    const grid = document.querySelector('.boards-grid') || boardsGrid;
    if (!grid) return;
    grid.classList.remove('cols-3', 'cols-4', 'cols-5', 'cols-6');
    if (colsVal && colsVal !== 'auto' && colsVal !== 'Авто') {
      grid.classList.add(`cols-${colsVal}`);
    }
  }

  function applyTextSizeSetting(sizeVal) {
    let px = '13.5px';
    if (sizeVal === 'S' || sizeVal === '12px' || sizeVal === '12') px = '12px';
    else if (sizeVal === 'M' || sizeVal === '13.5px' || sizeVal === '13.5') px = '13.5px';
    else if (sizeVal === 'L' || sizeVal === '15px' || sizeVal === '15') px = '15px';

    document.documentElement.style.setProperty('--board-text-size', px);
    document.querySelectorAll('.link-item').forEach(el => {
      el.style.fontSize = px;
    });
  }

  function applyTextWeightSetting(weightVal) {
    let fw = '400';
    if (weightVal === 'bold' || weightVal === 'Жирный' || weightVal === '600' || weightVal === 'Bold') {
      fw = '600';
    } else {
      fw = '400';
    }
    document.documentElement.style.setProperty('--board-font-weight', fw);
    document.documentElement.style.setProperty('--link-weight', fw);
    document.querySelectorAll('.link-item').forEach(el => {
      el.style.fontWeight = fw;
    });
  }

  function hexToRgba(hex, alpha) {
    if (!hex || typeof hex !== 'string') return `rgba(255, 255, 255, ${alpha})`;
    let c = hex.replace('#', '');
    if (c.length === 3) c = c.split('').map(x => x + x).join('');
    const num = parseInt(c, 16);
    if (isNaN(num)) return `rgba(255, 255, 255, ${alpha})`;
    const r = (num >> 16) & 255;
    const g = (num >> 8) & 255;
    const b = num & 255;
    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
  }

  function applySearchAppearance(color, alphaVal, blurVal, widthVal) {
    const searchBar = document.querySelector('.search-bar');
    if (alphaVal !== undefined && alphaVal !== null) {
      document.documentElement.style.setProperty('--search-alpha', alphaVal / 100);
    }
    if (blurVal !== undefined && blurVal !== null) {
      document.documentElement.style.setProperty('--search-blur', `${blurVal}px`);
      if (searchBar) {
        searchBar.style.backdropFilter = `blur(${blurVal}px)`;
        searchBar.style.webkitBackdropFilter = `blur(${blurVal}px)`;
      }
    }
    if (widthVal !== undefined && widthVal !== null) {
      document.documentElement.style.setProperty('--search-width', `${widthVal}px`);
      if (searchBar) {
        searchBar.style.width = `${widthVal}px`;
      }
    }
    if (color) {
      document.documentElement.style.setProperty('--search-bg-color', color);
      const effectiveAlpha = (alphaVal !== undefined && alphaVal !== null) ? alphaVal / 100 : 0.2;
      const rgba = hexToRgba(color, effectiveAlpha);
      if (searchBar) {
        searchBar.style.background = rgba;
      }
      const preview = document.getElementById('stColorPickerSearchPreview');
      if (preview) {
        preview.style.background = color;
      }
    }
  }

  function initSettingsLogic() {
    const allToggles = document.querySelectorAll('.st-toggle');

    // 1. Load all saved settings
    loadAllSettings((settings) => {
      if (!settings) settings = {};

      // Open new tab default true
      const openNewTabVal = settings['openNewTabToggle'] !== undefined ? Boolean(settings['openNewTabToggle']) : true;
      const openNewTabEl = document.getElementById('openNewTabToggle');
      if (openNewTabEl) openNewTabEl.classList.toggle('on', openNewTabVal);
      applySettingToDOM('openNewTabToggle', openNewTabVal);

      // Search default false
      const isSearchEnabled = settings['navSearchToggle'] === true;
      applySettingToDOM('navSearchToggle', isSearchEnabled);

      // Descriptions default true
      const descVal = settings['descToggle'] !== undefined ? Boolean(settings['descToggle']) : true;
      const descEl = document.getElementById('descToggle');
      if (descEl) descEl.classList.toggle('on', descVal);
      applySettingToDOM('descToggle', descVal);

      // Hide excess bookmarks default false
      const hideExcessVal = Boolean(settings['hideExcessBookmarksToggle']);
      const hideExcessEl = document.getElementById('hideExcessBookmarksToggle');
      if (hideExcessEl) hideExcessEl.classList.toggle('on', hideExcessVal);
      applySettingToDOM('hideExcessBookmarksToggle', hideExcessVal);

      // Sidebar always open default false
      const sidebarOpenVal = Boolean(settings['sidebarAlwaysOpenToggle']);
      const sidebarOpenEl = document.getElementById('sidebarAlwaysOpenToggle');
      if (sidebarOpenEl) sidebarOpenEl.classList.toggle('on', sidebarOpenVal);
      applySettingToDOM('sidebarAlwaysOpenToggle', sidebarOpenVal);

      // Clock default true
      const clockVal = settings['clockToggle'] !== undefined ? Boolean(settings['clockToggle']) : true;
      const clockEl = document.getElementById('clockToggle');
      if (clockEl) clockEl.classList.toggle('on', clockVal);
      applySettingToDOM('clockToggle', clockVal);

      // Weather default true
      const weatherVal = settings['weatherToggle'] !== undefined ? Boolean(settings['weatherToggle']) : true;
      const weatherEl = document.getElementById('weatherToggle');
      if (weatherEl) weatherEl.classList.toggle('on', weatherVal);
      applySettingToDOM('weatherToggle', weatherVal);

      // Columns setting
      const colsVal = settings['stSelectColumns'] || settings['grid_columns'] || 'auto';
      const colSelect = document.getElementById('stSelectColumns');
      if (colSelect) {
        colSelect.value = colsVal;
      }
      applyColumnsSetting(colsVal);

      // Board width slider
      const boardWidthVal = settings['stSliderBoardWidth'] || settings['board_width'] || '260';
      const boardWidthSlider = document.getElementById('stSliderBoardWidth');
      if (boardWidthSlider) {
        boardWidthSlider.value = boardWidthVal;
        const valSpan = boardWidthSlider.closest('.st-slider-field')?.querySelector('.st-val');
        if (valSpan) valSpan.textContent = `${boardWidthVal}px`;
      }
      document.documentElement.style.setProperty('--board-w', `${boardWidthVal}px`);

      // Quick board selection
      if (settings['stSelectQuickBoard']) {
        currentQuickBoardId = settings['stSelectQuickBoard'];
      }
      populateQuickBoardSelect();

      // Hotkey
      if (settings['stKbdShortcut']) {
        currentHotkey = settings['stKbdShortcut'];
      }
      const kbdEl = document.getElementById('stKbdShortcut');
      if (kbdEl) {
        kbdEl.textContent = currentHotkey || 'Alt+B';
      }

      // Search Appearance settings
      const searchColor = settings['stColorPickerSearch'] || '#ffffff';
      const searchAlpha = settings['stSliderSearchAlpha'] !== undefined ? parseInt(settings['stSliderSearchAlpha'], 10) : 20;
      const searchBlur = settings['stSliderSearchBlur'] !== undefined ? parseInt(settings['stSliderSearchBlur'], 10) : 12;
      const searchWidth = settings['stSliderSearchWidth'] !== undefined ? parseInt(settings['stSliderSearchWidth'], 10) : 340;

      const alphaSlider = document.getElementById('stSliderSearchAlpha');
      if (alphaSlider) {
        alphaSlider.value = searchAlpha;
        const valSpan = alphaSlider.closest('.st-slider-field')?.querySelector('.st-val');
        if (valSpan) valSpan.textContent = `${searchAlpha}%`;
      }

      const blurSlider = document.getElementById('stSliderSearchBlur');
      if (blurSlider) {
        blurSlider.value = searchBlur;
        const valSpan = blurSlider.closest('.st-slider-field')?.querySelector('.st-val');
        if (valSpan) valSpan.textContent = `${searchBlur}px`;
      }

      const widthSlider = document.getElementById('stSliderSearchWidth');
      if (widthSlider) {
        widthSlider.value = searchWidth;
        const valSpan = widthSlider.closest('.st-slider-field')?.querySelector('.st-val');
        if (valSpan) valSpan.textContent = `${searchWidth}px`;
      }

      const colorInput = document.getElementById('stColorPickerSearch');
      if (colorInput) {
        colorInput.value = searchColor.startsWith('#') && searchColor.length === 7 ? searchColor : '#ffffff';
      }
      applySearchAppearance(searchColor, searchAlpha, searchBlur, searchWidth);

      // Text Size
      const textSizeVal = settings['stSegTextSize'] || 'M';
      const textSizeSeg = document.getElementById('stSegTextSize');
      if (textSizeSeg) {
        textSizeSeg.querySelectorAll('.st-seg-btn').forEach((btn) => {
          const bVal = btn.dataset.val || btn.textContent.trim();
          btn.classList.toggle('active', bVal === textSizeVal);
        });
      }
      applyTextSizeSetting(textSizeVal);

      // Text Weight
      const textWeightVal = settings['stSegTextWeight'] || 'normal';
      const textWeightSeg = document.getElementById('stSegTextWeight');
      if (textWeightSeg) {
        textWeightSeg.querySelectorAll('.st-seg-btn').forEach((btn) => {
          const bVal = btn.dataset.val || (btn.textContent.trim() === 'Жирный' || btn.textContent.trim() === 'Bold' ? 'bold' : 'normal');
          btn.classList.toggle('active', bVal === textWeightVal || btn.textContent.trim() === textWeightVal);
        });
      }
      applyTextWeightSetting(textWeightVal);

      // Time Format
      const timeFormatVal = settings['stGroupTimeFormat'] || '24h';
      const timeFormatGroup = document.getElementById('stGroupTimeFormat');
      if (timeFormatGroup) {
        timeFormatGroup.querySelectorAll('.st-group-btn').forEach((btn) => {
          const bVal = btn.dataset.val || btn.textContent.trim();
          btn.classList.toggle('active', bVal.startsWith(timeFormatVal));
        });
      }

      // Date Format
      const dateFormatVal = settings['stGroupDateFormat'] || 'DD/MM/YY';
      const dateFormatGroup = document.getElementById('stGroupDateFormat');
      if (dateFormatGroup) {
        dateFormatGroup.querySelectorAll('.st-group-btn').forEach((btn) => {
          const bVal = btn.dataset.val || btn.textContent.trim();
          btn.classList.toggle('active', bVal === dateFormatVal);
        });
      }

      // Week Start
      const weekStartVal = settings['stGroupWeekStart'] || 'mon';
      currentWeekStart = weekStartVal;
      const weekStartGroup = document.getElementById('stGroupWeekStart');
      if (weekStartGroup) {
        weekStartGroup.querySelectorAll('.st-group-btn').forEach((btn) => {
          const bVal = btn.dataset.val || (btn.textContent.trim() === 'Воскресенье' || btn.textContent.trim() === 'Sunday' ? 'sun' : 'mon');
          btn.classList.toggle('active', bVal === weekStartVal);
        });
      }

      // Temperature
      const tempVal = settings['stGroupTemperature'] || 'C';
      const tempGroup = document.getElementById('stGroupTemperature');
      if (tempGroup) {
        tempGroup.querySelectorAll('.st-group-btn').forEach((btn) => {
          const bVal = btn.dataset.val || (btn.textContent.trim().includes('F') ? 'F' : 'C');
          btn.classList.toggle('active', bVal === tempVal);
        });
      }

      // Language
      const langVal = settings['stSegLanguage'] || settings['language'] || 'ru';
      setLanguage(langVal);
    });

    // 2. Attach Event Listeners to Settings Controls

    // Toggles
    allToggles.forEach((toggle) => {
      toggle.addEventListener('click', (e) => {
        e.stopPropagation();
        handleToggleClick(toggle);
      });
    });

    // #stSelectColumns
    const stSelectColumns = document.getElementById('stSelectColumns');
    if (stSelectColumns) {
      stSelectColumns.addEventListener('change', () => {
        const val = stSelectColumns.value;
        applyColumnsSetting(val);
        saveSetting('stSelectColumns', val);
        saveSetting('grid_columns', val);
      });
    }

    // #stSliderBoardWidth
    const stSliderBoardWidth = document.getElementById('stSliderBoardWidth');
    if (stSliderBoardWidth) {
      stSliderBoardWidth.addEventListener('input', () => {
        const val = stSliderBoardWidth.value;
        document.documentElement.style.setProperty('--board-w', `${val}px`);
        const valSpan = stSliderBoardWidth.closest('.st-slider-field')?.querySelector('.st-val');
        if (valSpan) valSpan.textContent = `${val}px`;
        saveSetting('stSliderBoardWidth', val);
        saveSetting('board_width', val);
      });
    }

    // #stSelectQuickBoard
    const stSelectQuickBoard = document.getElementById('stSelectQuickBoard');
    if (stSelectQuickBoard) {
      stSelectQuickBoard.addEventListener('change', () => {
        currentQuickBoardId = stSelectQuickBoard.value;
        saveSetting('stSelectQuickBoard', currentQuickBoardId);
      });
    }

    // #stBtnChangeHotkey & #stKbdShortcut
    const stBtnChangeHotkey = document.getElementById('stBtnChangeHotkey');
    const stKbdShortcut = document.getElementById('stKbdShortcut');
    let isRecordingHotkey = false;

    if (stBtnChangeHotkey && stKbdShortcut) {
      stBtnChangeHotkey.addEventListener('click', (e) => {
        e.stopPropagation();
        if (isRecordingHotkey) return;

        isRecordingHotkey = true;
        const dict = I18N_STRINGS[currentLanguage] || I18N_STRINGS.ru;
        stBtnChangeHotkey.textContent = dict['settings.pressKeys'] || 'Нажмите клавиши...';
        stBtnChangeHotkey.classList.add('active');

        function onKeyDownCapture(evt) {
          evt.preventDefault();
          evt.stopPropagation();

          if (evt.key === 'Escape') {
            window.removeEventListener('keydown', onKeyDownCapture, true);
            isRecordingHotkey = false;
            stBtnChangeHotkey.textContent = dict['settings.changeHotkey'] || 'Изменить';
            stBtnChangeHotkey.classList.remove('active');
            return;
          }

          if (['Control', 'Shift', 'Alt', 'Meta'].includes(evt.key)) {
            return; // Wait for the non-modifier key
          }

          const parts = [];
          if (evt.ctrlKey) parts.push('Ctrl');
          if (evt.altKey) parts.push('Alt');
          if (evt.shiftKey) parts.push('Shift');
          if (evt.metaKey) parts.push('Meta');

          let keyName = evt.key;
          if (keyName.length === 1) {
            keyName = keyName.toUpperCase();
          }
          parts.push(keyName);

          const fullShortcut = parts.join('+');
          currentHotkey = fullShortcut;
          stKbdShortcut.textContent = fullShortcut;
          saveSetting('stKbdShortcut', fullShortcut);

          window.removeEventListener('keydown', onKeyDownCapture, true);
          isRecordingHotkey = false;
          stBtnChangeHotkey.textContent = dict['settings.changeHotkey'] || 'Изменить';
          stBtnChangeHotkey.classList.remove('active');
          showToast((dict['toast.hotkeySaved'] || 'Горячая клавиша сохранена: ') + fullShortcut);
        }

        window.addEventListener('keydown', onKeyDownCapture, true);
      });
    }

    // Global shortcut trigger
    window.addEventListener('keydown', (evt) => {
      if (isRecordingHotkey) return;
      if (['INPUT', 'TEXTAREA', 'SELECT'].includes(document.activeElement?.tagName)) {
        return;
      }
      if (!currentHotkey || currentHotkey === 'Не задано' || currentHotkey === 'Not set') {
        return;
      }

      const parts = currentHotkey.split('+');
      const mainKey = parts[parts.length - 1].toUpperCase();
      const needsCtrl = parts.includes('Ctrl');
      const needsAlt = parts.includes('Alt');
      const needsShift = parts.includes('Shift');
      const needsMeta = parts.includes('Meta');

      const currentKey = evt.key.toUpperCase();
      if (
        currentKey === mainKey &&
        evt.ctrlKey === needsCtrl &&
        evt.altKey === needsAlt &&
        evt.shiftKey === needsShift &&
        evt.metaKey === needsMeta
      ) {
        evt.preventDefault();
        const currentTab = getActiveTab();
        if (currentTab && currentTab.boards && currentTab.boards.length > 0) {
          let targetBoard = currentTab.boards.find(b => b.id === currentQuickBoardId);
          if (!targetBoard) targetBoard = currentTab.boards[0];
          if (targetBoard) {
            openAddLinkModal(targetBoard.id);
            const dict = I18N_STRINGS[currentLanguage] || I18N_STRINGS.ru;
            showToast((dict['toast.quickSaveTriggered'] || 'Быстрое сохранение: ') + (targetBoard.title || 'Доска'));
          }
        }
      }
    });

    // #stColorPickerSearch & #stColorPickerSearchPreview
    const stColorPickerSearch = document.getElementById('stColorPickerSearch');
    const stColorPickerSearchPreview = document.getElementById('stColorPickerSearchPreview');

    if (stColorPickerSearchPreview && stColorPickerSearch) {
      stColorPickerSearchPreview.addEventListener('click', () => {
        stColorPickerSearch.click();
      });

      stColorPickerSearch.addEventListener('input', (e) => {
        const color = e.target.value;
        const alpha = parseInt(document.getElementById('stSliderSearchAlpha')?.value || '20', 10);
        applySearchAppearance(color, alpha);
        saveSetting('stColorPickerSearch', color);
      });
    }

    // #stSliderSearchAlpha
    const stSliderSearchAlpha = document.getElementById('stSliderSearchAlpha');
    if (stSliderSearchAlpha) {
      stSliderSearchAlpha.addEventListener('input', () => {
        const val = parseInt(stSliderSearchAlpha.value, 10);
        const valSpan = stSliderSearchAlpha.closest('.st-slider-field')?.querySelector('.st-val');
        if (valSpan) valSpan.textContent = `${val}%`;
        const color = document.getElementById('stColorPickerSearch')?.value || '#ffffff';
        applySearchAppearance(color, val);
        saveSetting('stSliderSearchAlpha', val);
      });
    }

    // #stSliderSearchBlur
    const stSliderSearchBlur = document.getElementById('stSliderSearchBlur');
    if (stSliderSearchBlur) {
      stSliderSearchBlur.addEventListener('input', () => {
        const val = parseInt(stSliderSearchBlur.value, 10);
        const valSpan = stSliderSearchBlur.closest('.st-slider-field')?.querySelector('.st-val');
        if (valSpan) valSpan.textContent = `${val}px`;
        applySearchAppearance(null, null, val);
        saveSetting('stSliderSearchBlur', val);
      });
    }

    // #stSliderSearchWidth
    const stSliderSearchWidth = document.getElementById('stSliderSearchWidth');
    if (stSliderSearchWidth) {
      stSliderSearchWidth.addEventListener('input', () => {
        const val = parseInt(stSliderSearchWidth.value, 10);
        const valSpan = stSliderSearchWidth.closest('.st-slider-field')?.querySelector('.st-val');
        if (valSpan) valSpan.textContent = `${val}px`;
        applySearchAppearance(null, null, null, val);
        saveSetting('stSliderSearchWidth', val);
      });
    }

    // #stSegTextSize
    const stSegTextSize = document.getElementById('stSegTextSize');
    if (stSegTextSize) {
      const btns = stSegTextSize.querySelectorAll('.st-seg-btn');
      btns.forEach((btn) => {
        btn.addEventListener('click', (e) => {
          e.stopPropagation();
          btns.forEach(b => b.classList.remove('active'));
          btn.classList.add('active');
          const val = btn.dataset.val || btn.textContent.trim();
          applyTextSizeSetting(val);
          saveSetting('stSegTextSize', val);
        });
      });
    }

    // #stSegTextWeight
    const stSegTextWeight = document.getElementById('stSegTextWeight');
    if (stSegTextWeight) {
      const btns = stSegTextWeight.querySelectorAll('.st-seg-btn');
      btns.forEach((btn) => {
        btn.addEventListener('click', (e) => {
          e.stopPropagation();
          btns.forEach(b => b.classList.remove('active'));
          btn.classList.add('active');
          const val = btn.dataset.val || (btn.textContent.trim() === 'Жирный' || btn.textContent.trim() === 'Bold' ? 'bold' : 'normal');
          applyTextWeightSetting(val);
          saveSetting('stSegTextWeight', val);
        });
      });
    }

    // #stSegLanguage
    const stSegLanguage = document.getElementById('stSegLanguage');
    if (stSegLanguage) {
      const btns = stSegLanguage.querySelectorAll('.st-seg-btn');
      btns.forEach((btn) => {
        btn.addEventListener('click', (e) => {
          e.stopPropagation();
          btns.forEach(b => b.classList.remove('active'));
          btn.classList.add('active');
          const lang = btn.dataset.lang || (btn.textContent.trim() === 'English' ? 'en' : btn.textContent.trim() === 'Deutsch' ? 'de' : 'ru');
          setLanguage(lang);
        });
      });
    }

    // #stBtnAutoDetect
    const stBtnAutoDetect = document.getElementById('stBtnAutoDetect');
    if (stBtnAutoDetect) {
      stBtnAutoDetect.addEventListener('click', (e) => {
        e.stopPropagation();
        const userLocale = navigator.language || 'ru-RU';
        let detectedLang = 'en';
        if (/^ru/i.test(userLocale)) detectedLang = 'ru';
        else if (/^de/i.test(userLocale)) detectedLang = 'de';

        // Detect 24h vs 12h
        let detectedTimeFormat = '24h';
        try {
          const hourCycle = new Intl.DateTimeFormat(userLocale, { hour: 'numeric' }).resolvedOptions().hourCycle;
          if (hourCycle === 'h11' || hourCycle === 'h12') {
            detectedTimeFormat = '12h';
          }
        } catch {
          if (/^(en-US|en-CA|en-PH)/i.test(userLocale)) detectedTimeFormat = '12h';
        }

        // Detect Date Format
        let detectedDateFormat = 'DD/MM/YY';
        if (/^(en-US)/i.test(userLocale)) detectedDateFormat = 'MM/DD/YY';
        else if (/^(ja|zh|ko|hu|lt|se)/i.test(userLocale)) detectedDateFormat = 'YY-MM-DD';

        // Detect Week Start
        let detectedWeekStart = 'mon';
        if (/^(en-US|en-CA|ja-JP|zh-TW|he-IL|ar-SA)/i.test(userLocale)) {
          detectedWeekStart = 'sun';
        }

        // Detect Temperature
        let detectedTemp = 'C';
        if (/^(en-US|es-US|en-BS|en-BZ|en-KY)/i.test(userLocale)) {
          detectedTemp = 'F';
        }

        // Apply detected values
        setLanguage(detectedLang);

        const timeGroup = document.getElementById('stGroupTimeFormat');
        if (timeGroup) {
          timeGroup.querySelectorAll('.st-group-btn').forEach((b) => {
            b.classList.toggle('active', (b.dataset.val || b.textContent.trim()).startsWith(detectedTimeFormat));
          });
          saveSetting('stGroupTimeFormat', detectedTimeFormat);
        }

        const dateGroup = document.getElementById('stGroupDateFormat');
        if (dateGroup) {
          dateGroup.querySelectorAll('.st-group-btn').forEach((b) => {
            b.classList.toggle('active', (b.dataset.val || b.textContent.trim()) === detectedDateFormat);
          });
          saveSetting('stGroupDateFormat', detectedDateFormat);
        }

        const weekGroup = document.getElementById('stGroupWeekStart');
        if (weekGroup) {
          currentWeekStart = detectedWeekStart;
          weekGroup.querySelectorAll('.st-group-btn').forEach((b) => {
            const bVal = b.dataset.val || (b.textContent.trim() === 'Воскресенье' || b.textContent.trim() === 'Sunday' ? 'sun' : 'mon');
            b.classList.toggle('active', bVal === detectedWeekStart);
          });
          saveSetting('stGroupWeekStart', detectedWeekStart);
        }

        const tempGroup = document.getElementById('stGroupTemperature');
        if (tempGroup) {
          tempGroup.querySelectorAll('.st-group-btn').forEach((b) => {
            const bVal = b.dataset.val || (b.textContent.trim().includes('F') ? 'F' : 'C');
            b.classList.toggle('active', bVal === detectedTemp);
          });
          saveSetting('stGroupTemperature', detectedTemp);
        }

        renderBoards();
        const dict = I18N_STRINGS[detectedLang] || I18N_STRINGS.ru;
        showToast(dict['toast.regionDetected'] || 'Настройки региона определены автоматически');
      });
    }

    // #stGroupTimeFormat
    const stGroupTimeFormat = document.getElementById('stGroupTimeFormat');
    if (stGroupTimeFormat) {
      const btns = stGroupTimeFormat.querySelectorAll('.st-group-btn');
      btns.forEach((btn) => {
        btn.addEventListener('click', (e) => {
          e.stopPropagation();
          btns.forEach(b => b.classList.remove('active'));
          btn.classList.add('active');
          const val = btn.dataset.val || (btn.textContent.trim().includes('12') ? '12h' : '24h');
          saveSetting('stGroupTimeFormat', val);
        });
      });
    }

    // #stGroupDateFormat
    const stGroupDateFormat = document.getElementById('stGroupDateFormat');
    if (stGroupDateFormat) {
      const btns = stGroupDateFormat.querySelectorAll('.st-group-btn');
      btns.forEach((btn) => {
        btn.addEventListener('click', (e) => {
          e.stopPropagation();
          btns.forEach(b => b.classList.remove('active'));
          btn.classList.add('active');
          const val = btn.dataset.val || btn.textContent.trim();
          saveSetting('stGroupDateFormat', val);
        });
      });
    }

    // #stGroupWeekStart
    const stGroupWeekStart = document.getElementById('stGroupWeekStart');
    if (stGroupWeekStart) {
      const btns = stGroupWeekStart.querySelectorAll('.st-group-btn');
      btns.forEach((btn) => {
        btn.addEventListener('click', (e) => {
          e.stopPropagation();
          btns.forEach(b => b.classList.remove('active'));
          btn.classList.add('active');
          const val = btn.dataset.val || (btn.textContent.trim() === 'Воскресенье' || btn.textContent.trim() === 'Sunday' ? 'sun' : 'mon');
          currentWeekStart = val;
          saveSetting('stGroupWeekStart', val);
          renderBoards();
        });
      });
    }

    // #stGroupTemperature
    const stGroupTemperature = document.getElementById('stGroupTemperature');
    if (stGroupTemperature) {
      const btns = stGroupTemperature.querySelectorAll('.st-group-btn');
      btns.forEach((btn) => {
        btn.addEventListener('click', (e) => {
          e.stopPropagation();
          btns.forEach(b => b.classList.remove('active'));
          btn.classList.add('active');
          const val = btn.dataset.val || (btn.textContent.trim().includes('F') ? 'F' : 'C');
          saveSetting('stGroupTemperature', val);
        });
      });
    }
  }

  function openSettingsModal() {
    if (!settingsOverlay) return;
    populateQuickBoardSelect();
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

  // --- Drag and Drop Engine ---
  function initDragAndDrop() {
    const grid = document.querySelector('.boards-grid') || boardsGrid;
    if (!grid) return;
    let draggedItem = null;

    grid.addEventListener('dragstart', (e) => {
      if (e.target.closest('input, textarea, button, select, .st-toggle, .notes-resize-handle')) {
        return;
      }

      const card = e.target.closest('.board, .board-card');
      if (card && !card.classList.contains('board-placeholder')) {
        draggedItem = card;
        if (e.dataTransfer) {
          e.dataTransfer.effectAllowed = 'move';
          e.dataTransfer.setData('text/plain', card.dataset.boardId || card.dataset.id || 'board');
        }
        grid.classList.add('is-dragging');
        setTimeout(() => {
          if (draggedItem) draggedItem.classList.add('dragging');
        }, 0);
      }
    });

    grid.addEventListener('dragover', (e) => {
      e.preventDefault();
      if (!draggedItem) return;
      if (e.dataTransfer) e.dataTransfer.dropEffect = 'move';

      const target = e.target.closest('.board, .board-card, .board-placeholder');
      if (target && target !== draggedItem && target.parentNode === grid) {
        const rect = target.getBoundingClientRect();
        const isAfter = (e.clientX > rect.left + rect.width / 2) || (e.clientY > rect.bottom - rect.height / 3);
        if (isAfter) {
          if (target.nextSibling !== draggedItem) {
            target.after(draggedItem);
          }
        } else {
          if (target.previousSibling !== draggedItem) {
            target.before(draggedItem);
          }
        }
      }
    });

    function commitOrderAndCleanup() {
      if (!draggedItem && !grid.classList.contains('is-dragging')) return;

      if (draggedItem) {
        draggedItem.classList.remove('dragging');
        draggedItem = null;
      }
      grid.classList.remove('is-dragging');

      // Commit DOM order to appState
      const currentTab = getActiveTab();
      if (currentTab && Array.isArray(currentTab.boards)) {
        const boardElements = [...grid.querySelectorAll('.board:not(.board-placeholder):not(.inline-creating), .board-card:not(.board-placeholder):not(.inline-creating)')];
        const newBoards = [];
        boardElements.forEach((el) => {
          const bId = el.dataset.boardId || el.dataset.id;
          if (bId) {
            const found = currentTab.boards.find(b => String(b.id) === String(bId));
            if (found && !newBoards.includes(found)) {
              newBoards.push(found);
            }
          }
        });
        currentTab.boards.forEach(b => {
          if (!newBoards.includes(b)) newBoards.push(b);
        });
        currentTab.boards = newBoards;
        saveState();
      }

      renderGridPlaceholders();
    }

    grid.addEventListener('drop', (e) => {
      e.preventDefault();
      commitOrderAndCleanup();
    });

    grid.addEventListener('dragend', commitOrderAndCleanup);

    document.addEventListener('dragend', () => {
      if (draggedItem) commitOrderAndCleanup();
    });
  }

  // --- Initializer ---
  function init() {
    loadSavedWallpaper();
    loadState(() => {
      renderTabs();
      renderBoards();
      populateQuickBoardSelect();
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
