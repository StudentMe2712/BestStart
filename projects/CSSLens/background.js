/**
 * CSS Lens - Background Service Worker (Manifest V3)
 * Manages extension state, commands, badge indicators, and message relay.
 */

// Tab state cache: Map<number, { active: boolean, format: string }>
const tabStates = new Map();

// Default settings
const DEFAULT_FORMAT = 'css'; // 'css' | 'compact' | 'tailwind' | 'json'

/**
 * Update the extension icon badge for a tab
 */
function updateBadge(tabId, isActive) {
  if (!tabId) return;

  if (isActive) {
    chrome.action.setBadgeText({ tabId, text: 'ON' });
    chrome.action.setBadgeBackgroundColor({ tabId, color: '#10B981' }); // Emerald Green
    if (chrome.action.setBadgeTextColor) {
      chrome.action.setBadgeTextColor({ tabId, color: '#FFFFFF' });
    }
    chrome.action.setTitle({
      tabId,
      title: 'CSS Lens: ACTIVE (Click or press Alt+C to deactivate)'
    });
  } else {
    chrome.action.setBadgeText({ tabId, text: '' });
    chrome.action.setTitle({
      tabId,
      title: 'CSS Lens - Style Inspector & Color Picker (Alt+C)'
    });
  }
}

/**
 * Ensure content script is ready in the tab, then send message
 */
async function sendTabMessage(tabId, message) {
  try {
    const response = await chrome.tabs.sendMessage(tabId, message);
    return response;
  } catch (err) {
    // If content script is not yet injected or tab is restricted
    try {
      await chrome.scripting.executeScript({
        target: { tabId },
        files: ['content.js']
      });
      // Retry message after injection
      return await chrome.tabs.sendMessage(tabId, message);
    } catch (injectErr) {
      console.warn(`[CSS Lens] Cannot inject script in tab ${tabId}:`, injectErr.message);
      return null;
    }
  }
}

/**
 * Toggle CSS Lens state on a specific tab
 */
async function toggleTabLens(tabId, forceState) {
  if (!tabId) return;

  const current = tabStates.get(tabId) || { active: false, format: DEFAULT_FORMAT };
  const nextActive = forceState !== undefined ? forceState : !current.active;

  const resp = await sendTabMessage(tabId, {
    type: 'TOGGLE_CSS_LENS',
    state: nextActive
  });

  const finalActive = resp?.active !== undefined ? resp.active : nextActive;
  tabStates.set(tabId, { ...current, active: finalActive });
  updateBadge(tabId, finalActive);

  return finalActive;
}

// Listen to keyboard shortcut commands (Alt+C)
chrome.commands.onCommand.addListener(async (command) => {
  if (command === 'toggle-css-lens') {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    if (tab?.id) {
      await toggleTabLens(tab.id);
    }
  }
});

// Listen to messages from Popup or Content scripts
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  const tabId = sender?.tab?.id || message.tabId;

  if (message.type === 'GET_STATUS') {
    (async () => {
      let targetTabId = tabId;
      if (!targetTabId) {
        const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
        targetTabId = tab?.id;
      }

      if (!targetTabId) {
        sendResponse({ active: false, format: DEFAULT_FORMAT });
        return;
      }

      // Query content script directly for ground truth
      try {
        const status = await chrome.tabs.sendMessage(targetTabId, { type: 'GET_STATUS' });
        if (status) {
          tabStates.set(targetTabId, {
            active: status.active,
            format: status.format || DEFAULT_FORMAT,
            isFrozen: status.isFrozen || false
          });
          updateBadge(targetTabId, status.active);
          sendResponse(status);
          return;
        }
      } catch (e) {
        // Fallback to cache
      }

      const cached = tabStates.get(targetTabId) || { active: false, format: DEFAULT_FORMAT };
      sendResponse(cached);
    })();
    return true; // Async response
  }

  if (message.type === 'TOGGLE_CSS_LENS') {
    (async () => {
      let targetTabId = tabId;
      if (!targetTabId) {
        const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
        targetTabId = tab?.id;
      }
      if (targetTabId) {
        const active = await toggleTabLens(targetTabId, message.state);
        sendResponse({ active });
      } else {
        sendResponse({ active: false });
      }
    })();
    return true;
  }

  if (message.type === 'STATE_CHANGED') {
    if (tabId) {
      const state = tabStates.get(tabId) || {};
      tabStates.set(tabId, { ...state, active: message.active });
      updateBadge(tabId, message.active);
    }
    sendResponse({ success: true });
    return true;
  }

  if (message.type === 'SAVE_HISTORY_ITEM') {
    (async () => {
      try {
        const data = await chrome.storage.local.get(['cssLensHistory']);
        const history = data.cssLensHistory || [];
        const newItem = {
          id: Date.now(),
          tag: message.item.tag,
          classes: message.item.classes,
          color: message.item.color,
          bg: message.item.bg,
          font: message.item.font,
          snippet: message.item.snippet,
          format: message.item.format,
          timestamp: new Date().toISOString()
        };
        // Keep last 20 items
        const updated = [newItem, ...history.filter(h => h.snippet !== newItem.snippet)].slice(0, 20);
        await chrome.storage.local.set({ cssLensHistory: updated });
        sendResponse({ success: true, history: updated });
      } catch (err) {
        sendResponse({ success: false, error: err.message });
      }
    })();
    return true;
  }
});

// Clean up state when tab is closed
chrome.tabs.onRemoved.addListener((tabId) => {
  tabStates.delete(tabId);
});

// Sync badge on tab activation
chrome.tabs.onActivated.addListener(async (activeInfo) => {
  const state = tabStates.get(activeInfo.tabId);
  if (state) {
    updateBadge(activeInfo.tabId, state.active);
  } else {
    // Check with content script
    try {
      const status = await chrome.tabs.sendMessage(activeInfo.tabId, { type: 'GET_STATUS' });
      if (status) {
        tabStates.set(activeInfo.tabId, { active: status.active, format: status.format });
        updateBadge(activeInfo.tabId, status.active);
      }
    } catch {
      updateBadge(activeInfo.tabId, false);
    }
  }
});
