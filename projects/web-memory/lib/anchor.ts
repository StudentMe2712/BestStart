import type { ElementAnchor, TextAnchor } from './types';

// Anchoring engine (runs in the content script / page context).
//
// Text anchors use the "text-quote" strategy: store the exact passage plus a window of
// surrounding text. On revisit we scan the page's concatenated text for the passage and,
// when it occurs more than once, pick the occurrence whose context best matches.

const CTX = 40;
export const UI_ATTR = 'data-wm-ui';

interface TextMap {
  nodes: Text[];
  starts: number[];
  text: string;
}

/** Concatenate all visible text nodes under a root, keeping a node→offset map. */
function buildTextMap(root: Node | null = document.body): TextMap {
  const nodes: Text[] = [];
  const starts: number[] = [];
  let text = '';
  if (!root) return { nodes, starts, text };

  const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
    acceptNode(node) {
      const parent = node.parentElement;
      if (!parent || !node.nodeValue) return NodeFilter.FILTER_REJECT;
      const tag = parent.tagName;
      if (tag === 'SCRIPT' || tag === 'STYLE' || tag === 'NOSCRIPT' || tag === 'TEXTAREA') {
        return NodeFilter.FILTER_REJECT;
      }
      if (parent.closest(`[${UI_ATTR}]`)) return NodeFilter.FILTER_REJECT;
      return NodeFilter.FILTER_ACCEPT;
    },
  });

  let node = walker.nextNode();
  while (node) {
    starts.push(text.length);
    nodes.push(node as Text);
    text += (node as Text).nodeValue ?? '';
    node = walker.nextNode();
  }
  return { nodes, starts, text };
}

/** Map a global char offset back to a (text node, local offset) point. */
function offsetToPoint(map: TextMap, offset: number): { node: Text; offset: number } | null {
  const { nodes, starts } = map;
  if (nodes.length === 0) return null;
  let lo = 0;
  let hi = nodes.length - 1;
  while (lo < hi) {
    const mid = (lo + hi + 1) >> 1;
    if (starts[mid] <= offset) lo = mid;
    else hi = mid - 1;
  }
  const node = nodes[lo];
  const local = Math.max(0, Math.min(offset - starts[lo], node.length));
  return { node, offset: local };
}

/** Map a DOM point (only meaningful for text-node containers) to a global offset. */
function pointToOffset(map: TextMap, container: Node, offset: number): number {
  if (container.nodeType === Node.TEXT_NODE) {
    const idx = map.nodes.indexOf(container as Text);
    if (idx >= 0) return map.starts[idx] + offset;
  }
  return -1;
}

export function captureTextAnchor(range: Range): TextAnchor | null {
  const exact = range.toString();
  if (!exact.trim()) return null;
  const map = buildTextMap();
  let start = pointToOffset(map, range.startContainer, range.startOffset);
  if (start < 0) start = map.text.indexOf(exact);
  if (start < 0) start = 0;
  const end = start + exact.length;
  return {
    kind: 'text',
    exact,
    prefix: map.text.slice(Math.max(0, start - CTX), start),
    suffix: map.text.slice(end, end + CTX),
    start,
  };
}

export function findTextRange(anchor: TextAnchor): Range | null {
  const map = buildTextMap();
  const { exact, prefix, suffix, start } = anchor;
  if (!exact) return null;

  const occurrences: number[] = [];
  let i = map.text.indexOf(exact);
  while (i >= 0) {
    occurrences.push(i);
    if (occurrences.length > 1000) break;
    i = map.text.indexOf(exact, i + 1);
  }
  if (occurrences.length === 0) return null;

  let best = occurrences[0];
  let bestScore = -Infinity;
  for (const o of occurrences) {
    let score = 0;
    const pre = map.text.slice(Math.max(0, o - prefix.length), o);
    const suf = map.text.slice(o + exact.length, o + exact.length + suffix.length);
    if (prefix) {
      score += pre.endsWith(prefix) ? 3 : commonSuffixLen(pre, prefix) / Math.max(1, prefix.length);
    }
    if (suffix) {
      score += suf.startsWith(suffix) ? 3 : commonPrefixLen(suf, suffix) / Math.max(1, suffix.length);
    }
    score += 1 - Math.min(1, Math.abs(o - start) / 8000);
    if (score > bestScore) {
      bestScore = score;
      best = o;
    }
  }

  const startPt = offsetToPoint(map, best);
  const endPt = offsetToPoint(map, best + exact.length);
  if (!startPt || !endPt) return null;
  try {
    const range = document.createRange();
    range.setStart(startPt.node, startPt.offset);
    range.setEnd(endPt.node, endPt.offset);
    return range;
  } catch {
    return null;
  }
}

function commonPrefixLen(a: string, b: string): number {
  const n = Math.min(a.length, b.length);
  let i = 0;
  while (i < n && a[i] === b[i]) i++;
  return i;
}

function commonSuffixLen(a: string, b: string): number {
  const n = Math.min(a.length, b.length);
  let i = 0;
  while (i < n && a[a.length - 1 - i] === b[b.length - 1 - i]) i++;
  return i;
}

// --- Element anchors -------------------------------------------------------------------

export function captureElementAnchor(el: Element): ElementAnchor {
  return {
    kind: 'element',
    selector: cssPath(el),
    xpath: xPath(el),
    label: elementLabel(el),
  };
}

export function elementLabel(el: Element): string {
  const text = (el.textContent ?? '').replace(/\s+/g, ' ').trim();
  if (text) return text.slice(0, 80);
  if (el instanceof HTMLImageElement && el.alt) return `🖼 ${el.alt.slice(0, 60)}`;
  return `<${el.tagName.toLowerCase()}>`;
}

export function cssPath(el: Element): string {
  if (el.id) return `#${cssEscape(el.id)}`;
  const parts: string[] = [];
  let node: Element | null = el;
  while (node && node.nodeType === Node.ELEMENT_NODE && parts.length < 8) {
    if (node.id) {
      parts.unshift(`#${cssEscape(node.id)}`);
      break;
    }
    let selector = node.tagName.toLowerCase();
    const parent: Element | null = node.parentElement;
    if (parent) {
      const current = node;
      const sameTag = Array.from(parent.children).filter((c) => c.tagName === current.tagName);
      if (sameTag.length > 1) selector += `:nth-of-type(${sameTag.indexOf(current) + 1})`;
    }
    parts.unshift(selector);
    node = parent;
  }
  return parts.join(' > ');
}

export function xPath(el: Element): string {
  const parts: string[] = [];
  let node: Element | null = el;
  while (node && node.nodeType === Node.ELEMENT_NODE) {
    let index = 1;
    let sibling = node.previousElementSibling;
    while (sibling) {
      if (sibling.tagName === node.tagName) index++;
      sibling = sibling.previousElementSibling;
    }
    parts.unshift(`${node.tagName.toLowerCase()}[${index}]`);
    node = node.parentElement;
  }
  return '/' + parts.join('/');
}

export function findElement(anchor: ElementAnchor): HTMLElement | null {
  try {
    const el = document.querySelector(anchor.selector);
    if (el instanceof HTMLElement) return el;
  } catch {
    /* invalid selector */
  }
  try {
    const res = document.evaluate(
      anchor.xpath,
      document,
      null,
      XPathResult.FIRST_ORDERED_NODE_TYPE,
      null,
    );
    if (res.singleNodeValue instanceof HTMLElement) return res.singleNodeValue;
  } catch {
    /* invalid xpath */
  }
  return null;
}

export function cssEscape(value: string): string {
  if (typeof CSS !== 'undefined' && typeof CSS.escape === 'function') return CSS.escape(value);
  return value.replace(/[^a-zA-Z0-9_-]/g, (c) => `\\${c}`);
}
