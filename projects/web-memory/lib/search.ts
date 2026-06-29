import type { Link, Memory } from './types';

// FlexSearch's ESM/CJS interop differs across bundlers, so resolve the namespace
// defensively and keep the index loosely typed (its public types are incomplete).
import * as FlexSearchNS from 'flexsearch';

/* eslint-disable @typescript-eslint/no-explicit-any */
const FS: any = (FlexSearchNS as any).default ?? FlexSearchNS;

export interface SearchIndex {
  search(query: string): string[];
}

function collectIds(doc: any, query: string): string[] {
  const q = query.trim();
  if (!q) return [];
  const results = doc.search(q, { limit: 50 });
  const ids: string[] = [];
  const seen = new Set<string>();
  for (const group of results ?? []) {
    for (const id of group.result ?? []) {
      const sid = String(id);
      if (!seen.has(sid)) {
        seen.add(sid);
        ids.push(sid);
      }
    }
  }
  return ids;
}

/** Build an in-memory full-text index over memories (text + note + page title). */
export function buildIndex(items: Memory[]): SearchIndex {
  const doc = new FS.Document({
    tokenize: 'forward',
    document: {
      id: 'id',
      index: [{ field: 'text' }, { field: 'note' }, { field: 'title' }],
    },
  });
  for (const item of items) doc.add(item);
  return { search: (query) => collectIds(doc, query) };
}

/** Build an in-memory full-text index over links (title + url + description + tags). */
export function buildLinkIndex(items: Link[]): SearchIndex {
  const doc = new FS.Document({
    tokenize: 'forward',
    document: {
      id: 'id',
      index: [{ field: 'title' }, { field: 'url' }, { field: 'description' }, { field: 'tagsText' }],
    },
  });
  for (const item of items) doc.add({ ...item, tagsText: item.tags.join(' ') });
  return { search: (query) => collectIds(doc, query) };
}
/* eslint-enable @typescript-eslint/no-explicit-any */
