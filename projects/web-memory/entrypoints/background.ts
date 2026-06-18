import {
  deleteMemory,
  getAllMemories,
  getMemoriesByUrl,
  getMemory,
  putMemory,
} from '@/lib/db';
import type { BgMessage, CaptureMode, TabMessage } from '@/lib/messages';
import type { Memory } from '@/lib/types';

// The background service worker is the single owner of IndexedDB. Every other context
// (content script, side panel) reads/writes through the messages handled here.

interface SidePanelApi {
  setPanelBehavior?: (options: { openPanelOnActionClick: boolean }) => Promise<void>;
}

export default defineBackground(() => {
  // Clicking the toolbar icon opens the side panel directly (no popup in the MVP).
  // chrome.sidePanel is Chrome-only and isn't in the webextension-polyfill types.
  const sidePanel = (browser as unknown as { sidePanel?: SidePanelApi }).sidePanel;
  sidePanel?.setPanelBehavior?.({ openPanelOnActionClick: true }).catch(() => {
    /* setPanelBehavior unavailable — ignore */
  });

  browser.runtime.onInstalled.addListener(async () => {
    await browser.contextMenus.removeAll();
    browser.contextMenus.create({
      id: 'wm-highlight',
      title: 'Запомнить выделение',
      contexts: ['selection'],
    });
    browser.contextMenus.create({
      id: 'wm-note',
      title: 'Добавить заметку к выделению',
      contexts: ['selection'],
    });
    browser.contextMenus.create({
      id: 'wm-important',
      title: 'Отметить важным',
      contexts: ['selection'],
    });
    browser.contextMenus.create({
      id: 'wm-element-note',
      title: 'Web Memory: прикрепить заметку к элементу',
      contexts: ['page', 'image', 'link'],
    });
  });

  browser.contextMenus.onClicked.addListener((info, tab) => {
    if (!tab?.id) return;
    const id = String(info.menuItemId);
    let message: TabMessage;
    if (id === 'wm-element-note') {
      message = { type: 'START_ELEMENT_NOTE' };
    } else {
      const modes: Record<string, CaptureMode> = {
        'wm-highlight': 'highlight',
        'wm-note': 'note',
        'wm-important': 'important',
      };
      const mode = modes[id];
      if (!mode) return;
      message = { type: 'CAPTURE_SELECTION', mode };
    }
    browser.tabs.sendMessage(tab.id, message).catch(() => {
      /* no content script on this page */
    });
  });

  browser.runtime.onMessage.addListener((message: unknown) => handle(message as BgMessage));
});

async function handle(message: BgMessage): Promise<unknown> {
  switch (message.type) {
    case 'SAVE_MEMORY': {
      const now = Date.now();
      const memory: Memory = {
        ...message.memory,
        id: crypto.randomUUID(),
        createdAt: now,
        updatedAt: now,
      };
      return putMemory(memory);
    }
    case 'GET_PAGE_MEMORIES':
      return getMemoriesByUrl(message.url);
    case 'GET_ALL_MEMORIES':
      return getAllMemories();
    case 'UPDATE_MEMORY': {
      const existing = await getMemory(message.id);
      if (!existing) return null;
      const updated: Memory = {
        ...existing,
        ...message.patch,
        id: existing.id,
        updatedAt: Date.now(),
      };
      return putMemory(updated);
    }
    case 'DELETE_MEMORY':
      await deleteMemory(message.id);
      return { ok: true };
    case 'OPEN_AND_SCROLL':
      return openAndScroll(message.href, message.id);
  }
}

async function openAndScroll(href: string, id: string): Promise<{ ok: boolean }> {
  const tab = await browser.tabs.create({ url: href });
  const tabId = tab.id;
  if (tabId == null) return { ok: false };

  const onUpdated = (updatedTabId: number, info: { status?: string }) => {
    if (updatedTabId === tabId && info.status === 'complete') {
      browser.tabs.onUpdated.removeListener(onUpdated);
      // Give the content script a moment to re-apply highlights before scrolling.
      setTimeout(() => {
        const msg: TabMessage = { type: 'SCROLL_TO_MEMORY', id };
        browser.tabs.sendMessage(tabId, msg).catch(() => {});
      }, 800);
    }
  };
  browser.tabs.onUpdated.addListener(onUpdated);
  return { ok: true };
}
