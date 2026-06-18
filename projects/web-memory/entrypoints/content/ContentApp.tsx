import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { captureElementAnchor, captureTextAnchor, findElement } from '@/lib/anchor';
import { applyHighlight, scrollToElement } from '@/lib/highlight';
import { sendBg, type CaptureMode } from '@/lib/messages';
import type { ElementAnchor, Memory, NewMemory } from '@/lib/types';
import { on } from './bus';

const Z = 2147483600;

interface SelectionState {
  range: Range;
  rect: DOMRect;
  text: string;
}

type Mode =
  | { kind: 'idle' }
  | { kind: 'note'; selection: SelectionState }
  | { kind: 'element-pick' }
  | { kind: 'element-note'; anchor: ElementAnchor };

export function ContentApp({ pageUrl }: { pageUrl: string }) {
  const [selection, setSelection] = useState<SelectionState | null>(null);
  const [mode, setMode] = useState<Mode>({ kind: 'idle' });
  const [toast, setToast] = useState<string | null>(null);
  const [pins, setPins] = useState<Memory[]>([]);
  const [hoverRect, setHoverRect] = useState<DOMRect | null>(null);
  const selectionRef = useRef<SelectionState | null>(null);
  const toastTimer = useRef<number | undefined>(undefined);

  const showToast = useCallback((msg: string) => {
    setToast(msg);
    window.clearTimeout(toastTimer.current);
    toastTimer.current = window.setTimeout(() => setToast(null), 2400);
  }, []);

  // --- Load element-note pins for this page -------------------------------------------
  useEffect(() => {
    let active = true;
    sendBg({ type: 'GET_PAGE_MEMORIES', url: pageUrl }).then((memories) => {
      if (active) setPins(memories.filter((m) => m.anchor.kind === 'element'));
    });
    return () => {
      active = false;
    };
  }, [pageUrl]);

  // --- Track the current text selection for the floating toolbar ----------------------
  useEffect(() => {
    const onSelectionChange = () => {
      const sel = window.getSelection();
      if (!sel || sel.isCollapsed || sel.rangeCount === 0) {
        setSelection(null);
        return;
      }
      const text = sel.toString();
      if (!text.trim()) {
        setSelection(null);
        return;
      }
      const node = sel.anchorNode;
      const host = node instanceof Element ? node : (node?.parentElement ?? null);
      if (host?.closest('[data-wm-ui]')) return;
      const range = sel.getRangeAt(0);
      const rect = range.getBoundingClientRect();
      if (rect.width === 0 && rect.height === 0) {
        setSelection(null);
        return;
      }
      const state: SelectionState = { range: range.cloneRange(), rect, text };
      selectionRef.current = state;
      setSelection(state);
    };
    document.addEventListener('selectionchange', onSelectionChange);
    return () => document.removeEventListener('selectionchange', onSelectionChange);
  }, []);

  // --- Persisting -------------------------------------------------------------------
  const saveHighlight = useCallback(
    async (range: Range, captureMode: CaptureMode, note: string) => {
      const anchor = captureTextAnchor(range);
      if (!anchor) {
        showToast('Не удалось сохранить выделение');
        return;
      }
      const memory: NewMemory = {
        url: pageUrl,
        href: location.href,
        title: document.title,
        kind: 'highlight',
        text: anchor.exact,
        note,
        important: captureMode === 'important',
        color: captureMode === 'important' ? '#fde68a' : '#fff3a3',
        anchor,
      };
      const saved = await sendBg({ type: 'SAVE_MEMORY', memory });
      applyHighlight(saved);
      showToast(
        note
          ? 'Заметка сохранена'
          : captureMode === 'important'
            ? 'Отмечено как важное'
            : 'Сохранено в Web Memory',
      );
      window.getSelection()?.removeAllRanges();
      setSelection(null);
      setMode({ kind: 'idle' });
    },
    [pageUrl, showToast],
  );

  const saveElementNote = useCallback(
    async (anchor: ElementAnchor, note: string) => {
      const memory: NewMemory = {
        url: pageUrl,
        href: location.href,
        title: document.title,
        kind: 'note',
        text: anchor.label,
        note,
        important: false,
        color: '#fff3a3',
        anchor,
      };
      const saved = await sendBg({ type: 'SAVE_MEMORY', memory });
      setPins((prev) => [...prev, saved]);
      showToast('Заметка прикреплена к элементу');
      setMode({ kind: 'idle' });
    },
    [pageUrl, showToast],
  );

  const onToolbarAction = useCallback(
    (action: CaptureMode) => {
      const sel = selectionRef.current;
      if (!sel) return;
      if (action === 'note') setMode({ kind: 'note', selection: sel });
      else void saveHighlight(sel.range, action, '');
    },
    [saveHighlight],
  );

  // --- Bus events from the content-script entry (context menu / side panel) ------------
  useEffect(() => {
    const offCapture = on('capture', (captureMode) => {
      const sel = window.getSelection();
      let range: Range | null = null;
      if (sel && !sel.isCollapsed && sel.rangeCount > 0 && sel.toString().trim()) {
        range = sel.getRangeAt(0).cloneRange();
      } else if (selectionRef.current) {
        range = selectionRef.current.range;
      }
      if (!range) {
        showToast('Сначала выделите текст');
        return;
      }
      if (captureMode === 'note') {
        setMode({
          kind: 'note',
          selection: { range, rect: range.getBoundingClientRect(), text: range.toString() },
        });
      } else {
        void saveHighlight(range, captureMode, '');
      }
    });
    const offElement = on('element-note', () => setMode({ kind: 'element-pick' }));
    const offMemories = on('memories', (memories) =>
      setPins(memories.filter((m) => m.anchor.kind === 'element')),
    );
    const offRemoved = on('memory-removed', (id) =>
      setPins((prev) => prev.filter((m) => m.id !== id)),
    );
    return () => {
      offCapture();
      offElement();
      offMemories();
      offRemoved();
    };
  }, [saveHighlight, showToast]);

  // --- Element-pick mode --------------------------------------------------------------
  useEffect(() => {
    if (mode.kind !== 'element-pick') {
      setHoverRect(null);
      return;
    }
    let current: HTMLElement | null = null;
    const onMove = (e: MouseEvent) => {
      const el = document.elementFromPoint(e.clientX, e.clientY) as HTMLElement | null;
      if (!el || el.closest('[data-wm-ui]')) {
        current = null;
        setHoverRect(null);
        return;
      }
      current = el;
      setHoverRect(el.getBoundingClientRect());
    };
    const onClick = (e: MouseEvent) => {
      if (!current) return;
      e.preventDefault();
      e.stopPropagation();
      const anchor = captureElementAnchor(current);
      setHoverRect(null);
      setMode({ kind: 'element-note', anchor });
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setMode({ kind: 'idle' });
    };
    document.addEventListener('mousemove', onMove, true);
    document.addEventListener('click', onClick, true);
    document.addEventListener('keydown', onKey, true);
    return () => {
      document.removeEventListener('mousemove', onMove, true);
      document.removeEventListener('click', onClick, true);
      document.removeEventListener('keydown', onKey, true);
    };
  }, [mode]);

  return (
    <>
      {selection && mode.kind === 'idle' && (
        <div
          style={{
            position: 'fixed',
            top: Math.max(8, selection.rect.top - 46),
            left: Math.min(Math.max(8, selection.rect.left), window.innerWidth - 250),
            zIndex: Z + 2,
          }}
          onMouseDown={(e) => e.preventDefault()}
          className="flex items-center gap-0.5 rounded-full border border-slate-200 bg-white px-1.5 py-1 shadow-2xl"
        >
          <ToolbarButton onClick={() => onToolbarAction('highlight')} label="Запомнить">
            🟡
          </ToolbarButton>
          <ToolbarButton onClick={() => onToolbarAction('note')} label="Заметка">
            📝
          </ToolbarButton>
          <ToolbarButton onClick={() => onToolbarAction('important')} label="Важно">
            ⭐
          </ToolbarButton>
        </div>
      )}

      {(mode.kind === 'note' || mode.kind === 'element-note') && (
        <NoteEditor
          title={mode.kind === 'note' ? 'Заметка к выделению' : 'Заметка к элементу'}
          context={mode.kind === 'note' ? mode.selection.text : mode.anchor.label}
          onCancel={() => setMode({ kind: 'idle' })}
          onSave={(note) => {
            if (mode.kind === 'note') void saveHighlight(mode.selection.range, 'note', note);
            else void saveElementNote(mode.anchor, note);
          }}
        />
      )}

      {mode.kind === 'element-pick' && (
        <>
          <div
            style={{ position: 'fixed', top: 12, left: '50%', transform: 'translateX(-50%)', zIndex: Z + 6 }}
            className="rounded-full bg-slate-900 px-4 py-2 text-sm font-medium text-white shadow-2xl"
          >
            Кликните по элементу, чтобы прикрепить заметку · Esc — отмена
          </div>
          {hoverRect && (
            <div
              style={{
                position: 'fixed',
                top: hoverRect.top,
                left: hoverRect.left,
                width: hoverRect.width,
                height: hoverRect.height,
                zIndex: Z + 4,
                pointerEvents: 'none',
                border: '2px solid #f59e0b',
                background: 'rgba(245,158,11,.12)',
                borderRadius: 4,
              }}
            />
          )}
        </>
      )}

      <PinLayer pins={pins} onScrollTo={(m) => scrollToElement(m)} />

      {toast && (
        <div
          style={{ position: 'fixed', bottom: 24, left: '50%', transform: 'translateX(-50%)', zIndex: Z + 7 }}
          className="rounded-full bg-slate-900 px-4 py-2 text-sm text-white shadow-2xl"
        >
          {toast}
        </div>
      )}
    </>
  );
}

function ToolbarButton({
  onClick,
  label,
  children,
}: {
  onClick: () => void;
  label: string;
  children: ReactNode;
}) {
  return (
    <button
      onClick={onClick}
      title={label}
      className="flex items-center gap-1 rounded-full px-2.5 py-1 text-sm text-slate-700 hover:bg-brand-50"
    >
      <span>{children}</span>
      <span className="text-xs font-medium">{label}</span>
    </button>
  );
}

function NoteEditor({
  title,
  context,
  onSave,
  onCancel,
}: {
  title: string;
  context: string;
  onSave: (note: string) => void;
  onCancel: () => void;
}) {
  const [value, setValue] = useState('');
  const ref = useRef<HTMLTextAreaElement>(null);
  useEffect(() => {
    ref.current?.focus();
  }, []);
  return (
    <div
      style={{ position: 'fixed', inset: 0, zIndex: Z + 5 }}
      className="flex items-start justify-center bg-black/20 pt-24"
      onMouseDown={onCancel}
    >
      <div
        className="w-[380px] max-w-[92vw] rounded-xl border border-slate-200 bg-white p-4 shadow-2xl"
        onMouseDown={(e) => e.stopPropagation()}
      >
        <h3 className="mb-1 text-sm font-semibold text-slate-800">{title}</h3>
        <p className="mb-3 line-clamp-2 rounded bg-slate-50 p-2 text-xs text-slate-500">{context}</p>
        <textarea
          ref={ref}
          value={value}
          onChange={(e) => setValue(e.target.value)}
          rows={4}
          placeholder="Например: использовать в проекте, проверить позже…"
          className="mb-3 w-full resize-none rounded-lg border border-slate-300 p-2 text-sm text-slate-800 outline-none focus:border-brand-500"
        />
        <div className="flex justify-end gap-2">
          <button
            onClick={onCancel}
            className="rounded-lg px-3 py-1.5 text-sm text-slate-600 hover:bg-slate-100"
          >
            Отмена
          </button>
          <button
            onClick={() => onSave(value.trim())}
            className="rounded-lg bg-brand-500 px-3 py-1.5 text-sm font-medium text-white hover:bg-brand-600"
          >
            Сохранить
          </button>
        </div>
      </div>
    </div>
  );
}

function PinLayer({ pins, onScrollTo }: { pins: Memory[]; onScrollTo: (m: Memory) => void }) {
  const [positions, setPositions] = useState<Record<string, { x: number; y: number }>>({});
  const [open, setOpen] = useState<Memory | null>(null);

  useEffect(() => {
    const update = () => {
      const next: Record<string, { x: number; y: number }> = {};
      for (const pin of pins) {
        if (pin.anchor.kind !== 'element') continue;
        const el = findElement(pin.anchor);
        if (el) {
          const r = el.getBoundingClientRect();
          next[pin.id] = { x: r.left + window.scrollX, y: r.top + window.scrollY };
        }
      }
      setPositions(next);
    };
    update();
    let raf = 0;
    const onScroll = () => {
      cancelAnimationFrame(raf);
      raf = requestAnimationFrame(update);
    };
    window.addEventListener('scroll', onScroll, true);
    window.addEventListener('resize', onScroll);
    const interval = window.setInterval(update, 2000);
    return () => {
      cancelAnimationFrame(raf);
      window.removeEventListener('scroll', onScroll, true);
      window.removeEventListener('resize', onScroll);
      window.clearInterval(interval);
    };
  }, [pins]);

  return (
    <>
      {pins.map((pin) => {
        const pos = positions[pin.id];
        if (!pos) return null;
        return (
          <button
            key={pin.id}
            onClick={() => setOpen((cur) => (cur?.id === pin.id ? null : pin))}
            style={{ position: 'absolute', top: pos.y - 10, left: pos.x - 10, zIndex: Z }}
            className="flex h-6 w-6 items-center justify-center rounded-full bg-brand-500 text-xs shadow-lg ring-2 ring-white"
            title={pin.note}
          >
            📝
          </button>
        );
      })}
      {open && positions[open.id] && (
        <div
          style={{
            position: 'absolute',
            top: positions[open.id].y + 20,
            left: positions[open.id].x,
            zIndex: Z + 1,
          }}
          className="w-64 rounded-lg border border-slate-200 bg-white p-3 text-sm shadow-2xl"
        >
          <p className="mb-2 whitespace-pre-wrap text-slate-700">{open.note || open.text}</p>
          <div className="flex gap-2">
            <button
              onClick={() => {
                onScrollTo(open);
                setOpen(null);
              }}
              className="rounded bg-brand-500 px-2 py-1 text-xs font-medium text-white"
            >
              Показать
            </button>
            <button
              onClick={() => setOpen(null)}
              className="rounded bg-slate-100 px-2 py-1 text-xs text-slate-600"
            >
              Закрыть
            </button>
          </div>
        </div>
      )}
    </>
  );
}
