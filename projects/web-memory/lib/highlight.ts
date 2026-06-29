import type { Memory } from './types';
import { cssEscape, findElement, findTextRange } from './anchor';
import { hexToRgba } from './color';

// In-page rendering of saved memories: wrap text passages in <mark> and provide
// scroll-to / flash helpers. Marks live in the page DOM (unavoidable to wrap text), so
// they use inline styles + a unique class to minimise clashes with page CSS.

export const HL_CLASS = 'wm-hl';
const STYLE_ID = 'wm-hl-style';

function markSelector(id: string): string {
  return `mark.${HL_CLASS}[data-wm-id="${cssEscape(id)}"]`;
}

/** The first <mark> of a text memory on the page (used to anchor its on-page note pin). */
export function findHighlightEl(id: string): HTMLElement | null {
  return document.querySelector<HTMLElement>(markSelector(id));
}

// :hover can't be expressed inline, so inject one tiny rule (once) that deepens the tint.
// `!important` lets it win over the inline base background on hover only.
function ensureHoverStyle(): void {
  if (document.getElementById(STYLE_ID)) return;
  const style = document.createElement('style');
  style.id = STYLE_ID;
  style.textContent = `mark.${HL_CLASS}:hover{background-color:var(--wm-tint-strong) !important;}`;
  (document.head ?? document.documentElement).appendChild(style);
}

export function isApplied(id: string): boolean {
  return !!document.querySelector(markSelector(id));
}

/** Re-locate a text memory and wrap it. Returns false if it can't be found on the page. */
export function applyHighlight(mem: Memory): boolean {
  if (mem.anchor.kind !== 'text') return false;
  if (isApplied(mem.id)) return true;
  const range = findTextRange(mem.anchor);
  if (!range || range.collapsed) return false;
  try {
    ensureHoverStyle();
    wrapRange(range, mem);
    return true;
  } catch {
    return false;
  }
}

function styleMark(mark: HTMLElement, mem: Memory): void {
  mark.className = HL_CLASS;
  mark.dataset.wmId = mem.id;
  // Soft, low-opacity tint (reader-friendly) that deepens on hover; not a solid marker.
  const color = mem.color || '#fff3a3';
  mark.style.setProperty('--wm-tint-strong', hexToRgba(color, 0.55));
  mark.style.backgroundColor = hexToRgba(color, 0.3);
  mark.style.color = 'inherit';
  mark.style.borderRadius = '2px';
  mark.style.padding = '0';
  mark.style.cursor = 'pointer';
  mark.style.transition = 'background-color .15s ease, box-shadow .15s ease';
  // Thin underline accent (instead of a heavy bar) to flag important / noted fragments.
  if (mem.important) mark.style.boxShadow = 'inset 0 -2px 0 rgba(245,158,11,.6)';
  else if (mem.note) mark.style.boxShadow = 'inset 0 -2px 0 rgba(100,116,139,.55)';
  else mark.style.boxShadow = 'none';
  mark.title = mem.note ? `📝 ${mem.note}` : 'Web Memory';
}

function wrapRange(range: Range, mem: Memory): void {
  const ancestor = range.commonAncestorContainer;
  const rootEl = ancestor.nodeType === Node.TEXT_NODE ? ancestor.parentNode : ancestor;
  if (!rootEl) return;

  const walker = document.createTreeWalker(rootEl, NodeFilter.SHOW_TEXT, {
    acceptNode: (node) =>
      node.nodeValue && range.intersectsNode(node)
        ? NodeFilter.FILTER_ACCEPT
        : NodeFilter.FILTER_REJECT,
  });

  const targets: Text[] = [];
  let node = walker.nextNode();
  while (node) {
    targets.push(node as Text);
    node = walker.nextNode();
  }

  // Process in reverse so splitting a node never invalidates earlier offsets.
  for (let i = targets.length - 1; i >= 0; i--) {
    const textNode = targets[i];
    let s = 0;
    let e = textNode.length;
    if (textNode === range.startContainer) s = range.startOffset;
    if (textNode === range.endContainer) e = range.endOffset;
    if (s >= e) continue;

    let target = textNode;
    if (e < target.length) target.splitText(e);
    if (s > 0) target = target.splitText(s);

    const mark = document.createElement('mark');
    styleMark(mark, mem);
    target.parentNode?.insertBefore(mark, target);
    mark.appendChild(target);
  }
}

/** Update the visual style of an already-applied highlight (e.g. after editing note/important). */
export function restyleHighlight(mem: Memory): void {
  document.querySelectorAll<HTMLElement>(markSelector(mem.id)).forEach((mark) => styleMark(mark, mem));
}

export function removeHighlight(id: string): void {
  document.querySelectorAll(markSelector(id)).forEach((mark) => {
    const parent = mark.parentNode;
    if (!parent) return;
    while (mark.firstChild) parent.insertBefore(mark.firstChild, mark);
    parent.removeChild(mark);
    parent.normalize();
  });
}

export function scrollToText(mem: Memory): boolean {
  let el = document.querySelector<HTMLElement>(markSelector(mem.id));
  if (!el) {
    applyHighlight(mem);
    el = document.querySelector<HTMLElement>(markSelector(mem.id));
  }
  if (!el) return false;
  el.scrollIntoView({ behavior: 'smooth', block: 'center' });
  flash(el);
  return true;
}

export function scrollToElement(mem: Memory): boolean {
  if (mem.anchor.kind !== 'element') return false;
  const el = findElement(mem.anchor);
  if (!el) return false;
  el.scrollIntoView({ behavior: 'smooth', block: 'center' });
  flash(el);
  return true;
}

export function flash(el: HTMLElement): void {
  const prev = {
    outline: el.style.outline,
    offset: el.style.outlineOffset,
    transition: el.style.transition,
  };
  el.style.transition = 'outline-color .25s ease';
  el.style.outline = '3px solid #f59e0b';
  el.style.outlineOffset = '2px';
  window.setTimeout(() => {
    el.style.outline = prev.outline;
    el.style.outlineOffset = prev.offset;
    el.style.transition = prev.transition;
  }, 1600);
}
