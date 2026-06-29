import type { Link, Memory, NewLink, NewMemory } from './types';

export type CaptureMode = 'highlight' | 'note' | 'important';

/** Messages handled by the background service worker (single owner of IndexedDB). */
export type BgMessage =
  | { type: 'SAVE_MEMORY'; memory: NewMemory }
  | { type: 'GET_PAGE_MEMORIES'; url: string }
  | { type: 'GET_ALL_MEMORIES' }
  | { type: 'IMPORT_MEMORIES'; memories: Memory[] }
  | { type: 'UPDATE_MEMORY'; id: string; patch: Partial<Memory> }
  | { type: 'DELETE_MEMORY'; id: string }
  // Bulk operations (multi-select): one DB transaction + one broadcast instead of N.
  | { type: 'BULK_UPDATE_MEMORIES'; ids: string[]; patch: Partial<Memory> }
  | { type: 'BULK_DELETE_MEMORIES'; ids: string[] }
  // Re-paint every memory of a category after the user recolours it.
  | { type: 'RECOLOR_CATEGORY'; categoryId: string; color: string }
  // Detach a deleted category from its memories (they become uncategorized, never deleted).
  | { type: 'CLEAR_CATEGORY'; categoryId: string }
  // Move every memory of a deleted group up to another group (or null), never deleting them.
  | { type: 'REASSIGN_GROUP'; from: string; to: string | null }
  | { type: 'OPEN_AND_SCROLL'; href: string; id: string }
  // Links (saved pages) — a separate entity from memories.
  | { type: 'SAVE_LINK'; link: NewLink }
  | { type: 'GET_ALL_LINKS' }
  | { type: 'UPDATE_LINK'; id: string; patch: Partial<Link> }
  | { type: 'DELETE_LINK'; id: string }
  | { type: 'IMPORT_LINKS'; links: Link[] };

/** Messages handled by the content script (delivered via tabs.sendMessage). */
export type TabMessage =
  | { type: 'CAPTURE_SELECTION'; mode: CaptureMode }
  | { type: 'START_ELEMENT_NOTE' }
  | { type: 'SCROLL_TO_MEMORY'; id: string }
  | { type: 'REMOVE_MEMORY'; id: string }
  | { type: 'REAPPLY' }
  | { type: 'GET_ANCHOR_STATUS' }
  | { type: 'GET_PAGE_META' };

/** Ids of this page's text memories that the content script could not re-locate. */
export interface AnchorStatus {
  unlocated: string[];
}

/** Page metadata read locally by the content script (for "Save Page"). */
export interface PageMeta {
  title: string;
  description: string;
}

export interface BgResponse {
  SAVE_MEMORY: Memory;
  GET_PAGE_MEMORIES: Memory[];
  GET_ALL_MEMORIES: Memory[];
  IMPORT_MEMORIES: { added: number; skipped: number };
  UPDATE_MEMORY: Memory | null;
  DELETE_MEMORY: { ok: true };
  BULK_UPDATE_MEMORIES: { updated: number };
  BULK_DELETE_MEMORIES: { deleted: number };
  RECOLOR_CATEGORY: { updated: number };
  CLEAR_CATEGORY: { updated: number };
  REASSIGN_GROUP: { updated: number };
  OPEN_AND_SCROLL: { ok: boolean };
  SAVE_LINK: { link: Link; duplicate: boolean };
  GET_ALL_LINKS: Link[];
  UPDATE_LINK: Link | null;
  DELETE_LINK: { ok: true };
  IMPORT_LINKS: { added: number; skipped: number };
}

/** Typed request to the background worker. When the extension is reloaded/updated, an
 *  already-injected content script loses its connection: `browser.runtime` (and `.id`) go
 *  undefined. Detect that and fail with a clear, actionable message instead of a cryptic
 *  "Cannot read properties of undefined (reading 'sendMessage')". */
export function sendBg<T extends BgMessage>(msg: T): Promise<BgResponse[T['type']]> {
  if (!browser?.runtime?.id) {
    return Promise.reject(new Error('Расширение было обновлено — обновите страницу (F5)'));
  }
  return browser.runtime.sendMessage(msg) as Promise<BgResponse[T['type']]>;
}

/** Fire-and-forget message to a tab's content script (swallows "no receiver" errors). */
export async function sendTab(tabId: number, msg: TabMessage): Promise<void> {
  try {
    await browser.tabs.sendMessage(tabId, msg);
  } catch {
    /* content script not present on this tab — ignore */
  }
}

/** Request/response to a tab's content script; resolves null if there's no receiver. */
export async function queryTab<T>(tabId: number, msg: TabMessage): Promise<T | null> {
  try {
    return ((await browser.tabs.sendMessage(tabId, msg)) as T | undefined) ?? null;
  } catch {
    return null;
  }
}

/** Ask a tab's content script which of its memories failed to anchor. */
export async function getAnchorStatus(tabId: number): Promise<AnchorStatus | null> {
  return queryTab<AnchorStatus>(tabId, { type: 'GET_ANCHOR_STATUS' });
}

/** Ask a tab's content script for the page's title + meta description (for "Save Page"). */
export async function getPageMeta(tabId: number): Promise<PageMeta | null> {
  return queryTab<PageMeta>(tabId, { type: 'GET_PAGE_META' });
}

/** Broadcast from a content script to any open side panel when anchor status changes. */
export interface AnchorStatusBroadcast {
  type: 'ANCHOR_STATUS';
  url: string;
  unlocated: string[];
}

/** Broadcast from the background after any data write (memories or links), so an open side
 *  panel re-reads. */
export interface DataChangedBroadcast {
  type: 'DATA_CHANGED';
}

export function broadcastDataChanged(): void {
  browser.runtime
    .sendMessage({ type: 'DATA_CHANGED' } satisfies DataChangedBroadcast)
    .catch(() => {
      /* no side panel open — nobody to notify */
    });
}
