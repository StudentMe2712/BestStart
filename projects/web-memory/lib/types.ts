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
  important: boolean;
  /** Highlight background color. */
  color: string;
  anchor: Anchor;
  createdAt: number;
  updatedAt: number;
}

export type NewMemory = Omit<Memory, 'id' | 'createdAt' | 'updatedAt'>;

export const HIGHLIGHT_COLORS = ['#fff3a3', '#a7f3d0', '#bfdbfe', '#fbcfe8', '#fed7aa'] as const;
