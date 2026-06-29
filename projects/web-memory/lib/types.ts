// Shared data contract used across background, content script and side panel.

export type MemoryKind = 'highlight' | 'note';

/** A robust text-quote anchor (à la W3C Web Annotation): re-locate a passage by its
 *  exact text plus surrounding context, with a stored offset as a tie-breaker. */
export interface TextAnchor {
  kind: 'text';
  exact: string;
  prefix: string;
  suffix: string;
  /** Approximate char offset within the page text at capture time (disambiguation only). */
  start: number;
}

/** Anchor to an arbitrary DOM element (for notes pinned to non-text elements). */
export interface ElementAnchor {
  kind: 'element';
  selector: string;
  xpath: string;
  /** Short human-readable label of the element, for lists. */
  label: string;
}

export type Anchor = TextAnchor | ElementAnchor;

export interface Memory {
  id: string;
  /** Normalized URL (no hash) used to group "this page". */
  url: string;
  /** Full original href, used to re-open the exact location. */
  href: string;
  /** Page <title> at capture time. */
  title: string;
  kind: MemoryKind;
  /** Selected passage (highlight) or element label (note). */
  text: string;
  /** Free-form user note (may be empty). */
  note: string;
  /** Orthogonal "starred" emphasis, independent of category. */
  important: boolean;
  /** Highlight background color — denormalized from the category (or the default). */
  color: string;
  /** Optional category reference; absent or dangling ⇒ uncategorized. */
  categoryId?: string | null;
  /** Optional group (folder) reference; absent or dangling ⇒ ungrouped. Independent of category. */
  groupId?: string | null;
  anchor: Anchor;
  createdAt: number;
  updatedAt: number;
}

export type NewMemory = Omit<Memory, 'id' | 'createdAt' | 'updatedAt'>;

/** A saved page/resource — distinct from a Memory (a fragment inside a page). Links live in
 *  their own store and never appear in "My Memory". */
export interface Link {
  id: string;
  /** Original URL, used to open the page. */
  url: string;
  title: string;
  /** Favicon URL captured from the tab (optional; derived at render if absent). */
  favicon?: string;
  /** Optional description (auto-filled only for the current page). */
  description?: string;
  /** Free-form labels, normalized (lowercase, no '#'). */
  tags: string[];
  /** Optional group (folder) reference; absent or dangling ⇒ ungrouped. Shared with memories. */
  groupId?: string | null;
  createdAt: number;
  updatedAt: number;
}

export type NewLink = Omit<Link, 'id' | 'createdAt' | 'updatedAt'>;

/** A highlight category: a typed label with its own color + icon, so meaning never rests
 *  on color alone. System categories are builtin; users can add their own. */
export interface Category {
  id: string;
  label: string;
  color: string;
  /** Lucide icon key, resolved via components/category-icon. */
  icon: string;
  builtin: boolean;
}

/** A folder for memories: an organizational dimension orthogonal to Category (which types a
 *  highlight) and to color. Groups nest into a tree via parentId; a memory lives in at most one. */
export interface Group {
  id: string;
  name: string;
  /** Parent folder, or null for a top-level group. */
  parentId: string | null;
  /** Optional accent color for the folder (purely cosmetic in the tree). */
  color?: string;
  createdAt: number;
  updatedAt: number;
}

/** A "smart folder": a saved memory filter that behaves like a virtual group in the tree.
 *  Stored alongside groups; it owns no memories — it just re-applies a filter on selection. */
export interface SmartFolder {
  id: string;
  name: string;
  /** Category ids to match (may include the "__none__" uncategorized pseudo-id). */
  categoryIds: string[];
  importantOnly: boolean;
  kind: 'all' | 'highlight' | 'note';
  /** Optional case-insensitive substring matched against text + note. */
  query?: string;
  createdAt: number;
  updatedAt: number;
}

/** Soft yellow used for uncategorized highlights (the historical default). */
export const DEFAULT_HIGHLIGHT_COLOR = '#fff3a3';

/** Preset swatches offered by the color picker (label kept for a11y / tooltip). */
export const PRESET_COLORS: { label: string; value: string }[] = [
  { label: 'Жёлтый', value: '#fde047' },
  { label: 'Синий', value: '#3b82f6' },
  { label: 'Зелёный', value: '#22c55e' },
  { label: 'Красный', value: '#ef4444' },
  { label: 'Фиолетовый', value: '#a855f7' },
  { label: 'Оранжевый', value: '#f97316' },
  { label: 'Розовый', value: '#ec4899' },
  { label: 'Серый', value: '#94a3b8' },
];
