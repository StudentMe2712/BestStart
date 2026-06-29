import {
  bulkDeleteMemories,
  bulkUpdateMemories,
  clearCategoryFromMemories,
  deleteLink,
  deleteMemory,
  getAllLinks,
  getAllMemories,
  getLink,
  getLinkByUrl,
  getMemoriesByUrl,
  getMemory,
  importLinks,
  importMemories,
  putLink,
  putMemory,
  reassignGroupOnDelete,
  recolorMemoriesByCategory,
} from '@/lib/db';
import { broadcastDataChanged, type BgMessage, type CaptureMode, type TabMessage } from '@/lib/messages';
import { DEFAULT_HIGHLIGHT_COLOR, type Link, type Memory } from '@/lib/types';

// The background service worker is the single owner of IndexedDB. Every other context
// (content script, side panel) reads/writes through the messages handled here.

interface SidePanelApi {
  setPanelBehavior?: (options: { openPanelOnActionClick: boolean }) => Promise<void>;
  open?: (options: { windowId?: number; tabId?: number }) => Promise<void>;
}

// chrome.sidePanel is Chrome-only and isn't in the webextension-polyfill types.
const sidePanel = (browser as unknown as { sidePanel?: SidePanelApi }).sidePanel;

const BG_MESSAGE_TYPES = new Set<string>([
  'SAVE_MEMORY',
  'GET_PAGE_MEMORIES',
  'GET_ALL_MEMORIES',
  'IMPORT_MEMORIES',
  'UPDATE_MEMORY',
  'DELETE_MEMORY',
  'BULK_UPDATE_MEMORIES',
  'BULK_DELETE_MEMORIES',
  'RECOLOR_CATEGORY',
  'CLEAR_CATEGORY',
  'REASSIGN_GROUP',
  'OPEN_AND_SCROLL',
  'SAVE_LINK',
  'GET_ALL_LINKS',
  'UPDATE_LINK',
  'DELETE_LINK',
  'IMPORT_LINKS',
]);

export default defineBackground(() => {
  // CRITICAL: register the message handler first and synchronously, so saving/loading always
  // works even if the optional UI wiring below throws on some Chrome builds.
  browser.runtime.onMessage.addListener((message: unknown) => {
    const type = (message as { type?: string } | null)?.type;
    if (type && BG_MESSAGE_TYPES.has(type)) return handle(message as BgMessage);
    return undefined; // ignore broadcasts (DATA_CHANGED, ANCHOR_STATUS)
  });

  // Open the side panel from the toolbar icon. Guarded so any failure here can NEVER break
  // the message handler above (that bug previously surfaced as "не удалось сохранить").
  try {
    sidePanel?.setPanelBehavior?.({ openPanelOnActionClick: true }).catch(() => {});
    browser.action?.onClicked?.addListener((tab) => {
      if (!sidePanel?.open) return;
      const open = tab.windowId != null ? { windowId: tab.windowId } : { tabId: tab.id };
      sidePanel.open(open).catch(() => {});
    });
  } catch (err) {
    console.warn('[web-memory] side panel wiring failed', err);
  }

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
      const saved = await putMemory(memory);
      broadcastDataChanged();
      return saved;
    }
    case 'GET_PAGE_MEMORIES':
      return getMemoriesByUrl(message.url);
    case 'GET_ALL_MEMORIES':
      return getAllMemories();
    case 'IMPORT_MEMORIES': {
      const res = await importMemories(message.memories);
      broadcastDataChanged();
      return res;
    }
    case 'UPDATE_MEMORY': {
      const existing = await getMemory(message.id);
      if (!existing) return null;
      const updated: Memory = {
        ...existing,
        ...message.patch,
        id: existing.id,
        updatedAt: Date.now(),
      };
      const saved = await putMemory(updated);
      broadcastDataChanged();
      return saved;
    }
    case 'DELETE_MEMORY':
      await deleteMemory(message.id);
      broadcastDataChanged();
      return { ok: true };
    case 'BULK_UPDATE_MEMORIES': {
      const updated = await bulkUpdateMemories(message.ids, message.patch);
      broadcastDataChanged();
      return { updated };
    }
    case 'BULK_DELETE_MEMORIES': {
      const deleted = await bulkDeleteMemories(message.ids);
      broadcastDataChanged();
      return { deleted };
    }
    case 'RECOLOR_CATEGORY': {
      const updated = await recolorMemoriesByCategory(message.categoryId, message.color);
      broadcastDataChanged();
      return { updated };
    }
    case 'CLEAR_CATEGORY': {
      const updated = await clearCategoryFromMemories(message.categoryId, DEFAULT_HIGHLIGHT_COLOR);
      broadcastDataChanged();
      return { updated };
    }
    case 'REASSIGN_GROUP': {
      // Groups are shared by memories and links; reassign both in one transaction.
      const updated = await reassignGroupOnDelete(message.from, message.to);
      broadcastDataChanged();
      return { updated };
    }
    case 'OPEN_AND_SCROLL':
      return openAndScroll(message.href, message.id);
    case 'SAVE_LINK': {
      const existing = await getLinkByUrl(message.link.url);
      if (existing) return { link: existing, duplicate: true };
      const now = Date.now();
      const link: Link = { ...message.link, id: crypto.randomUUID(), createdAt: now, updatedAt: now };
      await putLink(link);
      broadcastDataChanged();
      return { link, duplicate: false };
    }
    case 'GET_ALL_LINKS':
      return getAllLinks();
    case 'UPDATE_LINK': {
      const existing = await getLink(message.id);
      if (!existing) return null;
      const updated: Link = { ...existing, ...message.patch, id: existing.id, updatedAt: Date.now() };
      const saved = await putLink(updated);
      broadcastDataChanged();
      return saved;
    }
    case 'DELETE_LINK':
      await deleteLink(message.id);
      broadcastDataChanged();
      return { ok: true };
    case 'IMPORT_LINKS': {
      const res = await importLinks(message.links);
      broadcastDataChanged();
      return res;
    }
  }
}

async function openAndScroll(href: string, id: string): Promise<{ ok: boolean }> {
  const tab = await browser.tabs.create({ url: href, active: true });
  const tabId = tab.id;
  if (tabId == null) return { ok: false };

  // The content script loads (document_idle) and re-applies highlights asynchronously, and
  // late/lazy content can push the passage in even later. So poll SCROLL_TO_MEMORY until the
  // content script reports success (handleScroll re-applies + scrolls + flashes) or we give up.
  const deadline = Date.now() + 12000;
  const poll = async () => {
    if (Date.now() > deadline) return;
    let ok = false;
    try {
      const msg: TabMessage = { type: 'SCROLL_TO_MEMORY', id };
      const res = (await browser.tabs.sendMessage(tabId, msg)) as { ok?: boolean } | undefined;
      ok = !!res?.ok;
    } catch {
      /* content script not ready yet */
    }
    if (!ok) setTimeout(poll, 700);
  };
  setTimeout(poll, 700);
  return { ok: true };
}
