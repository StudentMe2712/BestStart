import { openDB, type DBSchema, type IDBPDatabase } from 'idb';
import type { Link, Memory } from './types';
import { normalizeUrl } from './url';

// IndexedDB lives in the EXTENSION origin and is owned solely by the background
// service worker. Content scripts run in the page origin and cannot reach it, so all
// reads/writes are funnelled through background messages (see lib/messages.ts).

interface WebMemoryDB extends DBSchema {
  memories: {
    key: string;
    value: Memory;
    indexes: { 'by-url': string; 'by-createdAt': number };
  };
  links: {
    key: string;
    value: Link;
    indexes: { 'by-createdAt': number };
  };
}

let dbPromise: Promise<IDBPDatabase<WebMemoryDB>> | null = null;

function db(): Promise<IDBPDatabase<WebMemoryDB>> {
  if (!dbPromise) {
    dbPromise = openDB<WebMemoryDB>('web-memory', 3, {
      async upgrade(database, oldVersion, _newVersion, tx) {
        // IMPORTANT: create every store/index synchronously BEFORE any `await`. Once the
        // upgrade awaits, the versionchange transaction can deactivate and a later
        // createObjectStore throws — which would break the DB and every read/write after it.
        if (oldVersion < 1) {
          const store = database.createObjectStore('memories', { keyPath: 'id' });
          store.createIndex('by-url', 'url');
          store.createIndex('by-createdAt', 'createdAt');
        }
        // v3: links store (saved pages/resources), kept separate from memories.
        if (oldVersion < 3 && !database.objectStoreNames.contains('links')) {
          const store = database.createObjectStore('links', { keyPath: 'id' });
          store.createIndex('by-createdAt', 'createdAt');
        }
        // v2 data migration (safe to await now that all stores exist): re-normalize stored
        // urls so memories saved before the stricter normalizeUrl() still match their page.
        if (oldVersion >= 1 && oldVersion < 2) {
          const store = tx.objectStore('memories');
          let cursor = await store.openCursor();
          while (cursor) {
            const mem = cursor.value;
            const url = normalizeUrl(mem.href || mem.url);
            if (url !== mem.url) await cursor.update({ ...mem, url });
            cursor = await cursor.continue();
          }
        }
      },
      blocked() {
        console.warn('[web-memory] DB open blocked by another connection');
      },
      terminated() {
        console.warn('[web-memory] DB connection terminated — will reopen on next use');
        dbPromise = null;
      },
    }).catch((err) => {
      // Don't cache a permanently-rejected promise: reset so the next call retries cleanly.
      console.error('[web-memory] failed to open IndexedDB', err);
      dbPromise = null;
      throw err;
    });
  }
  return dbPromise;
}

export async function putMemory(memory: Memory): Promise<Memory> {
  await (await db()).put('memories', memory);
  return memory;
}

export async function getMemory(id: string): Promise<Memory | undefined> {
  return (await db()).get('memories', id);
}

export async function getMemoriesByUrl(url: string): Promise<Memory[]> {
  const items = await (await db()).getAllFromIndex('memories', 'by-url', url);
  return items.sort((a, b) => a.createdAt - b.createdAt);
}

export async function getAllMemories(): Promise<Memory[]> {
  const items = await (await db()).getAll('memories');
  return items.sort((a, b) => b.createdAt - a.createdAt);
}

export async function deleteMemory(id: string): Promise<void> {
  await (await db()).delete('memories', id);
}

/** Re-paint every memory of a category after a recolour (color is denormalized for rendering). */
export async function recolorMemoriesByCategory(
  categoryId: string,
  color: string,
): Promise<number> {
  const tx = (await db()).transaction('memories', 'readwrite');
  let updated = 0;
  let cursor = await tx.store.openCursor();
  while (cursor) {
    if (cursor.value.categoryId === categoryId) {
      await cursor.update({ ...cursor.value, color, updatedAt: Date.now() });
      updated++;
    }
    cursor = await cursor.continue();
  }
  await tx.done;
  return updated;
}

/** Detach a deleted category from its memories: clear categoryId and reset the color. */
export async function clearCategoryFromMemories(
  categoryId: string,
  defaultColor: string,
): Promise<number> {
  const tx = (await db()).transaction('memories', 'readwrite');
  let updated = 0;
  let cursor = await tx.store.openCursor();
  while (cursor) {
    if (cursor.value.categoryId === categoryId) {
      await cursor.update({
        ...cursor.value,
        categoryId: null,
        color: defaultColor,
        updatedAt: Date.now(),
      });
      updated++;
    }
    cursor = await cursor.continue();
  }
  await tx.done;
  return updated;
}

/** Move every memory AND link of a group to another group (or null) in ONE transaction, so a
 *  deleted folder can never leave memories/links half-reassigned if the worker is killed. */
export async function reassignGroupOnDelete(from: string, to: string | null): Promise<number> {
  const tx = (await db()).transaction(['memories', 'links'], 'readwrite');
  const now = Date.now();
  let updated = 0;
  const memStore = tx.objectStore('memories');
  let mc = await memStore.openCursor();
  while (mc) {
    if (mc.value.groupId === from) {
      await mc.update({ ...mc.value, groupId: to, updatedAt: now });
      updated++;
    }
    mc = await mc.continue();
  }
  const linkStore = tx.objectStore('links');
  let lc = await linkStore.openCursor();
  while (lc) {
    if (lc.value.groupId === from) {
      await lc.update({ ...lc.value, groupId: to, updatedAt: now });
      updated++;
    }
    lc = await lc.continue();
  }
  await tx.done;
  return updated;
}

/** Update many memories at once with the same patch (bulk move-to-group, set important, …). */
export async function bulkUpdateMemories(
  ids: string[],
  patch: Partial<Memory>,
): Promise<number> {
  const idSet = new Set(ids);
  const tx = (await db()).transaction('memories', 'readwrite');
  let updated = 0;
  let cursor = await tx.store.openCursor();
  while (cursor) {
    if (idSet.has(cursor.value.id)) {
      await cursor.update({ ...cursor.value, ...patch, id: cursor.value.id, updatedAt: Date.now() });
      updated++;
    }
    cursor = await cursor.continue();
  }
  await tx.done;
  return updated;
}

/** Delete many memories at once (ids come from a selection Set, so they're unique). */
export async function bulkDeleteMemories(ids: string[]): Promise<number> {
  const tx = (await db()).transaction('memories', 'readwrite');
  for (const id of ids) await tx.store.delete(id);
  await tx.done;
  return ids.length;
}

/** Insert imported memories, preserving their ids and skipping ones that already exist.
 *  Urls are re-normalized and missing fields defaulted so older exports import cleanly. */
export async function importMemories(items: Memory[]): Promise<{ added: number; skipped: number }> {
  const tx = (await db()).transaction('memories', 'readwrite');
  let added = 0;
  let skipped = 0;
  for (const item of items) {
    if (await tx.store.get(item.id)) {
      skipped++;
      continue;
    }
    const now = Date.now();
    const memory: Memory = {
      ...item,
      url: normalizeUrl(item.href || item.url),
      note: item.note ?? '',
      important: !!item.important,
      color: item.color || '#fff3a3',
      title: item.title ?? '',
      createdAt: typeof item.createdAt === 'number' ? item.createdAt : now,
      updatedAt: typeof item.updatedAt === 'number' ? item.updatedAt : now,
    };
    await tx.store.put(memory);
    added++;
  }
  await tx.done;
  return { added, skipped };
}

// --- Links -----------------------------------------------------------------------------

export async function putLink(link: Link): Promise<Link> {
  await (await db()).put('links', link);
  return link;
}

export async function getLink(id: string): Promise<Link | undefined> {
  return (await db()).get('links', id);
}

export async function getAllLinks(): Promise<Link[]> {
  const items = await (await db()).getAll('links');
  return items.sort((a, b) => b.createdAt - a.createdAt);
}

/** Find an existing link whose URL normalizes the same (used to avoid duplicate saves). */
export async function getLinkByUrl(url: string): Promise<Link | undefined> {
  const key = normalizeUrl(url);
  const all = await (await db()).getAll('links');
  return all.find((l) => normalizeUrl(l.url) === key);
}

export async function deleteLink(id: string): Promise<void> {
  await (await db()).delete('links', id);
}

/** Insert imported links, preserving ids and skipping ones that already exist. */
export async function importLinks(items: Link[]): Promise<{ added: number; skipped: number }> {
  const tx = (await db()).transaction('links', 'readwrite');
  let added = 0;
  let skipped = 0;
  for (const item of items) {
    if (await tx.store.get(item.id)) {
      skipped++;
      continue;
    }
    const now = Date.now();
    const link: Link = {
      ...item,
      title: item.title ?? '',
      tags: Array.isArray(item.tags) ? item.tags : [],
      createdAt: typeof item.createdAt === 'number' ? item.createdAt : now,
      updatedAt: typeof item.updatedAt === 'number' ? item.updatedAt : now,
    };
    await tx.store.put(link);
    added++;
  }
  await tx.done;
  return { added, skipped };
}
