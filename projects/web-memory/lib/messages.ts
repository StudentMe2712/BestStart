import type { Memory, NewMemory } from './types';

export type CaptureMode = 'highlight' | 'note' | 'important';

/** Messages handled by the background service worker (single owner of IndexedDB). */
export type BgMessage =
  | { type: 'SAVE_MEMORY'; memory: NewMemory }
  | { type: 'GET_PAGE_MEMORIES'; url: string }
  | { type: 'GET_ALL_MEMORIES' }
  | { type: 'UPDATE_MEMORY'; id: string; patch: Partial<Memory> }
  | { type: 'DELETE_MEMORY'; id: string }
  | { type: 'OPEN_AND_SCROLL'; href: string; id: string };

/** Messages handled by the content script (delivered via tabs.sendMessage). */
export type TabMessage =
  | { type: 'CAPTURE_SELECTION'; mode: CaptureMode }
  | { type: 'START_ELEMENT_NOTE' }
  | { type: 'SCROLL_TO_MEMORY'; id: string }
  | { type: 'REMOVE_MEMORY'; id: string }
  | { type: 'REAPPLY' };

export interface BgResponse {
  SAVE_MEMORY: Memory;
  GET_PAGE_MEMORIES: Memory[];
  GET_ALL_MEMORIES: Memory[];
  UPDATE_MEMORY: Memory | null;
  DELETE_MEMORY: { ok: true };
  OPEN_AND_SCROLL: { ok: boolean };
}

/** Typed request to the background worker. */
export function sendBg<T extends BgMessage>(msg: T): Promise<BgResponse[T['type']]> {
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
