import type { Memory } from './types';

// FlexSearch's ESM/CJS interop differs across bundlers, so resolve the namespace
// defensively and keep the index loosely typed (its public types are incomplete).
import * as FlexSearchNS from 'flexsearch';

/* eslint-disable @typescript-eslint/no-explicit-any */
const FS: any = (FlexSearchNS as any).default ?? FlexSearchNS;

export interface SearchIndex {
  search(query: string): string[];
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

  return {
    search(query: string): string[] {
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
    },
  };
}
/* eslint-enable @typescript-eslint/no-explicit-any */
