import { openDB, type DBSchema, type IDBPDatabase } from 'idb';
import type { Memory } from './types';

// IndexedDB lives in the EXTENSION origin and is owned solely by the background
// service worker. Content scripts run in the page origin and cannot reach it, so all
// reads/writes are funnelled through background messages (see lib/messages.ts).

interface WebMemoryDB extends DBSchema {
  memories: {
    key: string;
    value: Memory;
    indexes: { 'by-url': string; 'by-createdAt': number };
  };
}

let dbPromise: Promise<IDBPDatabase<WebMemoryDB>> | null = null;

function db(): Promise<IDBPDatabase<WebMemoryDB>> {
  if (!dbPromise) {
    dbPromise = openDB<WebMemoryDB>('web-memory', 1, {
      upgrade(database) {
        const store = database.createObjectStore('memories', { keyPath: 'id' });
        store.createIndex('by-url', 'url');
        store.createIndex('by-createdAt', 'createdAt');
      },
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
