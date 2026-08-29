/**
 * QuickTranslate Content Script
 * Listens for user text selections, displays a floating button, and renders a translation popup.
 */

(() => {
  if (window.__QUICK_TRANSLATE_INITIALIZED__) return;
  window.__QUICK_TRANSLATE_INITIALIZED__ = true;

  const ICONS = {
    translate: `
      <svg class="qt-btn-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <path d="m5 8 6 6" />
        <path d="m4 14 6-6 2-3" />
        <path d="M2 5h12" />
        <path d="M7 2h1" />
        <path d="m22 22-5-10-5 10" />
        <path d="M14 18h6" />
      </svg>
    `,
    copy: `
      <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <rect width="14" height="14" x="8" y="8" rx="2" ry="2"/>
        <path d="M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2"/>
      </svg>
    `,
    check: `
      <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
        <polyline points="20 6 9 17 4 12"/>
      </svg>
    `
  };

  let rootContainer = null;
  let floatingBtn = null;
  let popupContainer = null;
  let currentSelectionText = '';
  let lastRect = null;

  /**
   * Initializes root container and persistent UI widgets
   */
  function initUI() {
    if (document.getElementById('quicktranslate-root')) {
      rootContainer = document.getElementById('quicktranslate-root');
      floatingBtn = rootContainer.querySelector('.qt-floating-btn');
      popupContainer = rootContainer.querySelector('.qt-popup');
      return;
    }

    rootContainer = document.createElement('div');
    rootContainer.id = 'quicktranslate-root';

    // 1. Floating Translate Button
    floatingBtn = document.createElement('button');
    floatingBtn.className = 'qt-floating-btn qt-hidden';
    floatingBtn.setAttribute('type', 'button');
    floatingBtn.setAttribute('title', 'Перевести выделенный текст');
    floatingBtn.innerHTML = `
      ${ICONS.translate}
      <span class="qt-btn-label">Перевести</span>
    `;

    // Prevent text deselect when clicking on button
    floatingBtn.addEventListener('mousedown', (e) => {
      e.preventDefault();
      e.stopPropagation();
    });

    floatingBtn.addEventListener('click', (e) => {
      e.stopPropagation();
      if (!currentSelectionText) return;
      openTranslationPopup(currentSelectionText, lastRect);
    });

    // 2. Translation Popup Window
    popupContainer = document.createElement('div');
    popupContainer.className = 'qt-popup qt-hidden';
    popupContainer.addEventListener('mousedown', (e) => {
      e.stopPropagation();
    });

    rootContainer.appendChild(floatingBtn);
    rootContainer.appendChild(popupContainer);
    document.documentElement.appendChild(rootContainer);
  }

  /**
   * Position calculator with viewport bounds clamping
   */
  function calculateCoordinates(rect, elementWidth, elementHeight) {
    const scrollX = window.scrollX || window.pageXOffset || 0;
    const scrollY = window.scrollY || window.pageYOffset || 0;
    const offset = 8;

    let top = rect.bottom + scrollY + offset;
    let left = rect.left + scrollX;

    // Flip to top if overflowing bottom
    if (rect.bottom + elementHeight + offset > window.innerHeight) {
      top = rect.top + scrollY - elementHeight - offset;
    }

    // Keep within horizontal screen bounds
    const maxLeft = scrollX + window.innerWidth - elementWidth - 16;
    const minLeft = scrollX + 16;
    left = Math.max(minLeft, Math.min(left, maxLeft));
    top = Math.max(scrollY + 8, top);

    return { top: Math.round(top), left: Math.round(left) };
  }

  /**
   * Shows floating action button near selection
   */
  function showFloatingButton(rect, text) {
    if (!floatingBtn) initUI();

    // Do not show button if popup is already open for this selection
    if (popupContainer && !popupContainer.classList.contains('qt-hidden')) {
      return;
    }

    currentSelectionText = text;
    lastRect = rect;

    const coords = calculateCoordinates(rect, 110, 32);
    floatingBtn.style.top = `${coords.top}px`;
    floatingBtn.style.left = `${coords.left}px`;

    floatingBtn.classList.remove('qt-hidden');
    floatingBtn.classList.add('qt-visible');
  }

  /**
   * Hides floating button
   */
  function hideFloatingButton() {
    if (floatingBtn && !floatingBtn.classList.contains('qt-hidden')) {
      floatingBtn.classList.add('qt-hidden');
      floatingBtn.classList.remove('qt-visible');
    }
  }

  /**
   * Opens the translation popup and dispatches translate request to background service worker
   */
  function openTranslationPopup(text, rect) {
    hideFloatingButton();

    const popupWidth = 320;
    const popupHeight = 150; // estimate for initial render
    const coords = calculateCoordinates(rect || { bottom: 100, left: 100, top: 80 }, popupWidth, popupHeight);

    popupContainer.style.top = `${coords.top}px`;
    popupContainer.style.left = `${coords.left}px`;
    popupContainer.classList.remove('qt-hidden');
    popupContainer.classList.add('qt-visible');

    // Render Loading State
    popupContainer.innerHTML = `
      <div class="qt-popup-header">
        <div class="qt-header-brand">
          ${ICONS.translate}
          <span>QuickTranslate</span>
        </div>
        <button class="qt-close-btn" type="button" title="Закрыть">✕</button>
      </div>
      <div class="qt-popup-body">
        <div class="qt-loading">
          <div class="qt-spinner"></div>
          <span>Переводим...</span>
        </div>
      </div>
    `;

    popupContainer.querySelector('.qt-close-btn').addEventListener('click', closeTranslationPopup);

    // Send translation request to background service worker
    chrome.runtime.sendMessage(
      { action: 'translate', text: text, targetLang: 'ru' },
      (response) => {
        if (chrome.runtime.lastError) {
          renderErrorState(text, rect, chrome.runtime.lastError.message || 'Ошибка соединения с расширением');
          return;
        }

        if (!response || !response.success) {
          renderErrorState(text, rect, (response && response.error) ? response.error : 'Не удалось получить перевод');
          return;
        }

        renderResultState(response);
      }
    );
  }

  /**
   * Renders successful translation result
   */
  function renderResultState(result) {
    const langDisplay = result.detectedLang ? `${result.detectedLang} → RU` : 'RU';
    const translatedText = result.translation;

    popupContainer.innerHTML = `
      <div class="qt-popup-header">
        <div class="qt-header-brand">
          ${ICONS.translate}
          <span>QuickTranslate</span>
        </div>
        <span class="qt-lang-badge">${langDisplay}</span>
        <button class="qt-close-btn" type="button" title="Закрыть">✕</button>
      </div>
      <div class="qt-popup-body">
        <div class="qt-translated-text">${escapeHtml(translatedText)}</div>
      </div>
      <div class="qt-popup-footer">
        <button class="qt-copy-btn" type="button" title="Скопировать перевод">
          ${ICONS.copy}
          <span>Копировать</span>
        </button>
        <span class="qt-meta-info">Нажмите Esc для закрытия</span>
      </div>
    `;

    // Event listeners for popup controls
    popupContainer.querySelector('.qt-close-btn').addEventListener('click', closeTranslationPopup);

    const copyBtn = popupContainer.querySelector('.qt-copy-btn');
    copyBtn.addEventListener('click', () => {
      navigator.clipboard.writeText(translatedText).then(() => {
        copyBtn.classList.add('qt-copied');
        copyBtn.innerHTML = `${ICONS.check} <span>Скопировано!</span>`;
        setTimeout(() => {
          if (copyBtn) {
            copyBtn.classList.remove('qt-copied');
            copyBtn.innerHTML = `${ICONS.copy} <span>Копировать</span>`;
          }
        }, 1800);
      });
    });
  }

  /**
   * Renders error state with retry option
   */
  function renderErrorState(originalText, rect, errorMsg) {
    popupContainer.innerHTML = `
      <div class="qt-popup-header">
        <div class="qt-header-brand">
          ${ICONS.translate}
          <span>QuickTranslate</span>
        </div>
        <button class="qt-close-btn" type="button" title="Закрыть">✕</button>
      </div>
      <div class="qt-popup-body">
        <div class="qt-error">
          <span>${escapeHtml(errorMsg)}</span>
          <button class="qt-error-retry" type="button">Повторить попытку</button>
        </div>
      </div>
    `;

    popupContainer.querySelector('.qt-close-btn').addEventListener('click', closeTranslationPopup);
    popupContainer.querySelector('.qt-error-retry').addEventListener('click', () => {
      openTranslationPopup(originalText, rect);
    });
  }

  /**
   * Closes popup and clears selection cache
   */
  function closeTranslationPopup() {
    if (popupContainer && !popupContainer.classList.contains('qt-hidden')) {
      popupContainer.classList.add('qt-hidden');
      popupContainer.classList.remove('qt-visible');
    }
  }

  /**
   * Helper to escape HTML characters
   */
  function escapeHtml(str) {
    const div = document.createElement('div');
    div.textContent = str;
    return div.innerHTML;
  }

  /**
   * Text selection change listener
   */
  function handleSelectionChange() {
    const selection = window.getSelection();
    if (!selection || selection.isCollapsed || selection.rangeCount === 0) {
      hideFloatingButton();
      return;
    }

    const text = selection.toString().trim();
    if (!text || text.length < 2) {
      hideFloatingButton();
      return;
    }

    const range = selection.getRangeAt(0);
    const rect = range.getBoundingClientRect();

    if (rect.width === 0 && rect.height === 0) {
      hideFloatingButton();
      return;
    }

    showFloatingButton(rect, text);
  }

  // Document Events
  document.addEventListener('mouseup', (e) => {
    if (rootContainer && rootContainer.contains(e.target)) {
      return;
    }
    setTimeout(handleSelectionChange, 10);
  });

  document.addEventListener('keyup', (e) => {
    if (e.key === 'Escape') {
      closeTranslationPopup();
      hideFloatingButton();
      return;
    }

    if (e.shiftKey || e.key === 'Shift') {
      setTimeout(handleSelectionChange, 10);
    }
  });

  document.addEventListener('mousedown', (e) => {
    if (rootContainer && !rootContainer.contains(e.target)) {
      hideFloatingButton();
      closeTranslationPopup();
    }
  });

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initUI);
  } else {
    initUI();
  }
})();
