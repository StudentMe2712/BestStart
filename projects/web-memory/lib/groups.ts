import type { Group, SmartFolder } from './types';

// Groups (folders for memories) live in browser.storage.local — a small config list, like
// categories — so both the side panel and the content script can read them without going
// through the background/IndexedDB. Memories reference a group by id (Memory.groupId); an
// absent or dangling id means "ungrouped". Groups start empty: the user creates their own.

const STORAGE_KEY = 'wm:groups';
const ACTIVE_KEY = 'wm:activeGroup';
const SMART_KEY = 'wm:smartFolders';
const COLLAPSED_KEY = 'wm:groupsCollapsed';

/** Pseudo-ids used in the tree UI to mean "everything" / "no group". */
export const ALL_GROUPS = '__all__';
export const UNGROUPED = '__none__';

/** Tree selection of a smart folder is encoded as `smart:<id>`. */
export const SMART_PREFIX = 'smart:';

export async function loadGroups(): Promise<Group[]> {
  const stored = (await browser.storage.local.get(STORAGE_KEY))[STORAGE_KEY] as Group[] | undefined;
  return Array.isArray(stored) ? stored : [];
}

export async function saveGroups(groups: Group[]): Promise<void> {
  await browser.storage.local.set({ [STORAGE_KEY]: groups });
}

/** Subscribe to group changes from any context; returns an unsubscribe fn. */
export function subscribeGroups(onChange: (groups: Group[]) => void): () => void {
  const listener = (changes: Record<string, { newValue?: unknown }>, area: string) => {
    if (area === 'local' && changes[STORAGE_KEY]) {
      onChange((changes[STORAGE_KEY].newValue as Group[]) ?? []);
    }
  };
  browser.storage.onChanged.addListener(listener);
  return () => browser.storage.onChanged.removeListener(listener);
}

// --- Active group (the folder new captures go into; set from the in-page toolbar) ----------

export async function loadActiveGroupId(): Promise<string | null> {
  const v = (await browser.storage.local.get(ACTIVE_KEY))[ACTIVE_KEY];
  return typeof v === 'string' ? v : null;
}

export async function saveActiveGroupId(id: string | null): Promise<void> {
  await browser.storage.local.set({ [ACTIVE_KEY]: id });
}

/** Subscribe to active-group changes (keeps multiple tabs/the panel in sync). */
export function subscribeActiveGroup(onChange: (id: string | null) => void): () => void {
  const listener = (changes: Record<string, { newValue?: unknown }>, area: string) => {
    if (area === 'local' && changes[ACTIVE_KEY]) {
      const v = changes[ACTIVE_KEY].newValue;
      onChange(typeof v === 'string' ? v : null);
    }
  };
  browser.storage.onChanged.addListener(listener);
  return () => browser.storage.onChanged.removeListener(listener);
}

// --- Smart folders (saved filters shown as virtual groups) ---------------------------------

export async function loadSmartFolders(): Promise<SmartFolder[]> {
  const stored = (await browser.storage.local.get(SMART_KEY))[SMART_KEY] as
    | SmartFolder[]
    | undefined;
  return Array.isArray(stored) ? stored : [];
}

export async function saveSmartFolders(folders: SmartFolder[]): Promise<void> {
  await browser.storage.local.set({ [SMART_KEY]: folders });
}

export function subscribeSmartFolders(onChange: (folders: SmartFolder[]) => void): () => void {
  const listener = (changes: Record<string, { newValue?: unknown }>, area: string) => {
    if (area === 'local' && changes[SMART_KEY]) {
      onChange((changes[SMART_KEY].newValue as SmartFolder[]) ?? []);
    }
  };
  browser.storage.onChanged.addListener(listener);
  return () => browser.storage.onChanged.removeListener(listener);
}

// --- Collapsed tree state (persisted so expansion survives reopening the panel) ------------

export async function loadCollapsed(): Promise<string[]> {
  const v = (await browser.storage.local.get(COLLAPSED_KEY))[COLLAPSED_KEY];
  return Array.isArray(v) ? (v as string[]) : [];
}

export async function saveCollapsed(ids: string[]): Promise<void> {
  await browser.storage.local.set({ [COLLAPSED_KEY]: ids });
}

// --- Pure tree helpers ---------------------------------------------------------------------

/** Direct children of a group (or of the root when parentId is null), in stored order. */
export function childrenOf(groups: Group[], parentId: string | null): Group[] {
  return groups.filter((g) => (g.parentId ?? null) === parentId);
}

/** A group plus all of its descendants (used to filter "this folder, including nested"). */
export function groupAndDescendants(groups: Group[], id: string): Set<string> {
  const out = new Set<string>([id]);
  let grew = true;
  while (grew) {
    grew = false;
    for (const g of groups) {
      if (g.parentId && out.has(g.parentId) && !out.has(g.id)) {
        out.add(g.id);
        grew = true;
      }
    }
  }
  return out;
}

/** Breadcrumb path root→node for a group id (empty if unknown). Cycle-safe. */
export function pathOf(groups: Group[], id: string | null | undefined): Group[] {
  const byId = new Map(groups.map((g) => [g.id, g]));
  const path: Group[] = [];
  const seen = new Set<string>();
  let cur = id ? byId.get(id) : undefined;
  while (cur && !seen.has(cur.id)) {
    seen.add(cur.id);
    path.unshift(cur);
    cur = cur.parentId ? byId.get(cur.parentId) : undefined;
  }
  return path;
}

/** Can group `id` be re-parented under `newParentId` without creating a cycle? */
export function canReparent(groups: Group[], id: string, newParentId: string | null): boolean {
  if (id === newParentId) return false;
  if (newParentId === null) return true;
  return !groupAndDescendants(groups, id).has(newParentId);
}

/** Remove a group: its child groups move up to its parent. Returns the new list plus the
 *  parent id that the deleted group's MEMORIES should move to (handled in IndexedDB). */
export function removeGroup(
  groups: Group[],
  id: string,
): { groups: Group[]; reparentTo: string | null } {
  const target = groups.find((g) => g.id === id);
  const reparentTo = target ? (target.parentId ?? null) : null;
  const now = Date.now();
  const next = groups
    .filter((g) => g.id !== id)
    .map((g) => (g.parentId === id ? { ...g, parentId: reparentTo, updatedAt: now } : g));
  return { groups: next, reparentTo };
}
