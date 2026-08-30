/**
 * NovaTab Background Service Worker (Manifest V3)
 * Handles global keyboard shortcuts, storage initialization, and quick bookmark captures.
 */

// Default settings
const DEFAULT_SETTINGS = {
  theme: 'dark',
  viewMode: 'grid', // 'grid' or 'list'
  sortBy: 'dateAdded-desc', // 'dateAdded-desc', 'dateAdded-asc', 'title-asc', 'domain-asc'
  defaultFolderName: 'NovaTab',
  installedAt: Date.now()
};

// 1. Extension Installation & Storage Initialization
chrome.runtime.onInstalled.addListener(async (details) => {
  console.log('[NovaTab] Extension installed/updated:', details.reason);

  try {
    const existing = await chrome.storage.local.get(Object.keys(DEFAULT_SETTINGS));
    const toSet = {};
    for (const [key, value] of Object.entries(DEFAULT_SETTINGS)) {
      if (existing[key] === undefined) {
        toSet[key] = value;
      }
    }
    if (Object.keys(toSet).length > 0) {
      await chrome.storage.local.set(toSet);
      console.log('[NovaTab] Initialized default settings:', toSet);
    }

    // Set default badge appearance
    if (chrome.action?.setBadgeBackgroundColor) {
      chrome.action.setBadgeBackgroundColor({ color: '#6366F1' });
    }
  } catch (err) {
    console.error('[NovaTab] Failed initializing storage:', err);
  }
});

// 2. Helper: Find or create the default "NovaTab" bookmark folder
async function getOrCreateNovaTabFolder() {
  try {
    const tree = await chrome.bookmarks.getTree();
    const rootNodes = tree[0]?.children || [];

    // Search for existing "NovaTab" folder in the entire tree
    function searchFolder(nodes) {
      for (const node of nodes) {
        if (!node.url && node.title && node.title.toLowerCase() === 'novatab') {
          return node;
        }
        if (node.children) {
          const found = searchFolder(node.children);
          if (found) return found;
        }
      }
      return null;
    }

    const existingFolder = searchFolder(rootNodes);
    if (existingFolder) {
      return existingFolder.id;
    }

    // If not found, place it in "Bookmarks bar" (usually id '1') or first available root folder
    const targetParentId = rootNodes[0]?.id || '1';
    const newFolder = await chrome.bookmarks.create({
      parentId: targetParentId,
      title: 'NovaTab'
    });
    console.log('[NovaTab] Created new NovaTab bookmark folder with ID:', newFolder.id);
    return newFolder.id;
  } catch (err) {
    console.error('[NovaTab] Error finding or creating NovaTab folder:', err);
    return '1'; // Fallback to main bookmarks bar
  }
}

// 3. Helper: Check if URL is valid to bookmark
function isRestrictedUrl(url) {
  if (!url || typeof url !== 'string') return true;
  const restrictedProtocols = ['chrome:', 'chrome-extension:', 'edge:', 'about:', 'view-source:', 'data:', 'javascript:'];
  return restrictedProtocols.some(proto => url.startsWith(proto));
}

// 4. Helper: Show badge confirmation feedback
function showBadgeFeedback(text = '✓', durationMs = 2000) {
  if (!chrome.action?.setBadgeText) return;

  chrome.action.setBadgeText({ text });
  chrome.action.setBadgeBackgroundColor({ color: '#6366F1' });

  setTimeout(() => {
    chrome.action.setBadgeText({ text: '' });
  }, durationMs);
}

// 5. Global Command Handler (`Ctrl+Shift+Y` / `Cmd+Shift+Y`)
chrome.commands.onCommand.addListener(async (command) => {
  console.log('[NovaTab] Received command:', command);

  if (command === 'save-current-tab') {
    try {
      const [activeTab] = await chrome.tabs.query({ active: true, currentWindow: true });

      if (!activeTab || !activeTab.url) {
        console.warn('[NovaTab] No active tab found.');
        showBadgeFeedback('✕', 1500);
        return;
      }

      if (isRestrictedUrl(activeTab.url)) {
        console.warn('[NovaTab] Cannot bookmark restricted URL:', activeTab.url);
        showBadgeFeedback('✕', 1500);
        return;
      }

      const folderId = await getOrCreateNovaTabFolder();
      const title = activeTab.title || activeTab.url;

      await chrome.bookmarks.create({
        parentId: folderId,
        title: title,
        url: activeTab.url
      });

      console.log(`[NovaTab] Successfully saved bookmark: "${title}" -> Folder: ${folderId}`);
      showBadgeFeedback('✓', 2000);
    } catch (err) {
      console.error('[NovaTab] Error saving current tab bookmark:', err);
      showBadgeFeedback('!', 2000);
    }
  }
});

// Optional: Clicking action icon opens New Tab
if (chrome.action?.onClicked) {
  chrome.action.onClicked.addListener(() => {
    chrome.tabs.create({ url: 'chrome://newtab' });
  });
}
