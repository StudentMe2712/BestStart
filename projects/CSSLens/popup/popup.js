/**
 * CSS Lens - Popup Dashboard Controller
 */

document.addEventListener('DOMContentLoaded', async () => {
  const btnToggle = document.getElementById('btn-toggle');
  const statusLabel = document.getElementById('status-label');
  const statusDot = document.getElementById('status-dot');
  const formatRadios = document.querySelectorAll('input[name="copy-format"]');
  const historyList = document.getElementById('history-list');
  const btnClearHistory = document.getElementById('btn-clear-history');

  let currentTabId = null;
  let isLensActive = false;

  // 1. Get current active tab
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  if (tab?.id) {
    currentTabId = tab.id;
  }

  // 2. Fetch current status from background / content script
  async function syncStatus() {
    try {
      const resp = await chrome.runtime.sendMessage({
        type: 'GET_STATUS',
        tabId: currentTabId
      });

      if (resp) {
        updateActiveUI(resp.active);
        if (resp.format) {
          selectFormatRadio(resp.format);
        }
      }
    } catch (err) {
      console.warn('[Popup] Sync error:', err);
    }
  }

  // 3. Update active state UI
  function updateActiveUI(isActive) {
    isLensActive = !!isActive;
    if (isLensActive) {
      btnToggle.classList.add('active');
      statusDot.classList.add('active');
      statusLabel.textContent = 'Инспектор активен';
      statusLabel.style.color = '#10b981';
    } else {
      btnToggle.classList.remove('active');
      statusDot.classList.remove('active');
      statusLabel.textContent = 'Инспектор выключен';
      statusLabel.style.color = '#f8fafc';
    }
  }

  // 4. Set format radio
  function selectFormatRadio(format) {
    const radio = document.querySelector(`input[name="copy-format"][value="${format}"]`);
    if (radio) {
      radio.checked = true;
    }
  }

  // 5. Toggle button click handler
  btnToggle.addEventListener('click', async () => {
    try {
      const resp = await chrome.runtime.sendMessage({
        type: 'TOGGLE_CSS_LENS',
        tabId: currentTabId
      });
      if (resp) {
        updateActiveUI(resp.active);
      }
    } catch (err) {
      console.error('[Popup] Toggle failed:', err);
    }
  });

  // 6. Format radio change handler
  formatRadios.forEach((radio) => {
    radio.addEventListener('change', async (e) => {
      const chosenFormat = e.target.value;
      await chrome.storage.local.set({ cssLensFormat: chosenFormat });

      if (currentTabId) {
        chrome.tabs.sendMessage(currentTabId, {
          type: 'SET_FORMAT',
          format: chosenFormat
        }).catch(() => {});
      }
    });
  });

  // 7. Load saved format from storage
  chrome.storage.local.get(['cssLensFormat'], (res) => {
    if (res.cssLensFormat) {
      selectFormatRadio(res.cssLensFormat);
    }
  });

  // 8. History Management
  async function loadHistory() {
    const res = await chrome.storage.local.get(['cssLensHistory']);
    const history = res.cssLensHistory || [];

    if (history.length === 0) {
      historyList.innerHTML = '<div class="history-empty">История пока пуста. Нажмите на любой элемент на странице!</div>';
      return;
    }

    historyList.innerHTML = '';
    history.forEach((item) => {
      const row = document.createElement('div');
      row.className = 'history-item';
      row.title = 'Нажмите, чтобы скопировать повторно';
      row.innerHTML = `
        <div class="history-meta">
          <span class="history-tag">&lt;${item.tag || 'el'}&gt;</span>
          <span class="history-color-preview" style="background-color: ${item.color || '#fff'}"></span>
          <span class="history-font">${item.font || 'sans-serif'}</span>
        </div>
        <span class="history-badge">${(item.format || 'css').toUpperCase()}</span>
      `;

      row.addEventListener('click', async () => {
        try {
          await navigator.clipboard.writeText(item.snippet);
          row.style.borderColor = '#10b981';
          setTimeout(() => {
            row.style.borderColor = '';
          }, 800);
        } catch (e) {
          console.error('Clipboard copy failed', e);
        }
      });

      historyList.appendChild(row);
    });
  }

  btnClearHistory.addEventListener('click', async () => {
    await chrome.storage.local.set({ cssLensHistory: [] });
    loadHistory();
  });

  // Initial sync & load
  await syncStatus();
  await loadHistory();
});
