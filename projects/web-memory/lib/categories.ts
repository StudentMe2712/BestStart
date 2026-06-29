import type { Category } from './types';

// Category definitions live in browser.storage.local (a tiny config list), so both the side
// panel and the content script can read them without going through the background/IndexedDB.
// Memories reference a category by id; color is also denormalized onto the memory for rendering.

const STORAGE_KEY = 'wm:categories';

/** The four builtin semantic categories. "Important" stays an orthogonal star, not a category. */
export const SYSTEM_CATEGORIES: Category[] = [
  { id: 'idea', label: 'Идея', color: '#3b82f6', icon: 'Lightbulb', builtin: true },
  { id: 'action', label: 'Действие', color: '#22c55e', icon: 'CircleCheck', builtin: true },
  { id: 'problem', label: 'Проблема', color: '#ef4444', icon: 'TriangleAlert', builtin: true },
  { id: 'quote', label: 'Цитата', color: '#a855f7', icon: 'Quote', builtin: true },
];

/** Read all categories, seeding/merging the builtin set on first use. */
export async function loadCategories(): Promise<Category[]> {
  const stored = (await browser.storage.local.get(STORAGE_KEY))[STORAGE_KEY] as
    | Category[]
    | undefined;
  if (!stored || stored.length === 0) {
    await browser.storage.local.set({ [STORAGE_KEY]: SYSTEM_CATEGORIES });
    return [...SYSTEM_CATEGORIES];
  }
  // Make sure any builtin that isn't present (e.g. added in a later version) is appended,
  // preserving the user's recolours of the ones that already exist.
  const ids = new Set(stored.map((c) => c.id));
  const missing = SYSTEM_CATEGORIES.filter((c) => !ids.has(c.id));
  return missing.length ? [...stored, ...missing] : stored;
}

export async function saveCategories(categories: Category[]): Promise<void> {
  await browser.storage.local.set({ [STORAGE_KEY]: categories });
}

/** Look up a category by id within a known list (pure). */
export function resolveCategory(
  categories: Category[],
  id: string | null | undefined,
): Category | undefined {
  if (!id) return undefined;
  return categories.find((c) => c.id === id);
}

/** Subscribe to category changes from any context; returns an unsubscribe fn. */
export function subscribeCategories(onChange: (categories: Category[]) => void): () => void {
  const listener = (changes: Record<string, { newValue?: unknown }>, area: string) => {
    if (area === 'local' && changes[STORAGE_KEY]) {
      onChange((changes[STORAGE_KEY].newValue as Category[]) ?? []);
    }
  };
  browser.storage.onChanged.addListener(listener);
  return () => browser.storage.onChanged.removeListener(listener);
}
