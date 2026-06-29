import { useCallback, useEffect, useRef, useState, type KeyboardEvent } from 'react';
import {
  Check,
  ChevronDown,
  Folder,
  Highlighter,
  MapPin,
  Star,
  StickyNote,
  Trash2,
  TriangleAlert,
  X,
  type LucideIcon,
} from 'lucide-react';
import { captureElementAnchor, captureTextAnchor, findElement } from '@/lib/anchor';
import {
  applyHighlight,
  findHighlightEl,
  removeHighlight,
  scrollToElement,
  scrollToText,
} from '@/lib/highlight';
import { sendBg, type CaptureMode } from '@/lib/messages';
import { loadCategories, subscribeCategories } from '@/lib/categories';
import {
  loadActiveGroupId,
  loadGroups,
  saveActiveGroupId,
  saveGroups,
  subscribeActiveGroup,
  subscribeGroups,
} from '@/lib/groups';
import {
  DEFAULT_HIGHLIGHT_COLOR,
  type Category,
  type ElementAnchor,
  type Group,
  type Memory,
  type NewMemory,
} from '@/lib/types';
import {
  DEFAULT_SETTINGS,
  isDisabledOnHost,
  loadSettings,
  subscribeSettings,
  type WmSettings,
} from '@/lib/settings';
import { Button, IconButton } from '@/components/ui';
import { CategoryIcon } from '@/components/category-icon';
import { GroupPickerMenu } from '@/components/groups-ui';
import { on } from './bus';

const Z = 2147483600;

/** Short, human-readable error text for toasts (so failures are diagnosable, not silent). */
function errText(err: unknown): string {
  const m = err instanceof Error ? err.message : String(err);
  return m.length > 90 ? m.slice(0, 90) + '…' : m || 'неизвестная ошибка';
}

type ToastKind = 'ok' | 'err';

/** Memories that get a floating on-page note pin: any note anchored to an element, and any
 *  text highlight that carries a note (so a "Заметка" shows a notepad right on the page). */
function isPinned(m: Memory): boolean {
  return m.anchor.kind === 'element' || (m.anchor.kind === 'text' && !!m.note);
}

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
  const [toast, setToast] = useState<{ text: string; kind: ToastKind } | null>(null);
  const [pins, setPins] = useState<Memory[]>([]);
  const [hoverRect, setHoverRect] = useState<DOMRect | null>(null);
  const [categories, setCategories] = useState<Category[]>([]);
  const [groups, setGroups] = useState<Group[]>([]);
  const [activeGroupId, setActiveGroupId] = useState<string | null>(null);
  const [groupMenuOpen, setGroupMenuOpen] = useState(false);
  const [settings, setSettings] = useState<WmSettings>(DEFAULT_SETTINGS);
  // Whether the in-page UI is suppressed here (master off, or this host is blacklisted).
  const disabled = isDisabledOnHost(location.hostname, settings);
  const selectionRef = useRef<SelectionState | null>(null);
  const toastTimer = useRef<number | undefined>(undefined);
  // Mirror the active group so stable callbacks (toolbar/bus saves) read the latest value.
  const activeGroupRef = useRef<string | null>(null);
  useEffect(() => {
    activeGroupRef.current = activeGroupId;
  }, [activeGroupId]);

  const showToast = useCallback((text: string, kind: ToastKind = 'ok') => {
    setToast({ text, kind });
    window.clearTimeout(toastTimer.current);
    toastTimer.current = window.setTimeout(() => setToast(null), 2400);
  }, []);

  // --- Behavior settings (master toggle + per-domain disable) -------------------------
  useEffect(() => {
    let active = true;
    void loadSettings().then((s) => {
      if (active) setSettings(s);
    });
    const off = subscribeSettings(setSettings);
    return () => {
      active = false;
      off();
    };
  }, []);

  // --- Load element-note pins for this page -------------------------------------------
  useEffect(() => {
    if (disabled) return;
    let active = true;
    sendBg({ type: 'GET_PAGE_MEMORIES', url: pageUrl })
      .then((memories) => {
        if (active) setPins(memories.filter(isPinned));
      })
      .catch(() => {
        /* background not ready (cold start) — reapply will repopulate pins */
      });
    return () => {
      active = false;
    };
  }, [pageUrl, disabled]);

  // --- Category definitions (for the quick-capture dots) ------------------------------
  useEffect(() => {
    let active = true;
    void loadCategories().then((c) => {
      if (active) setCategories(c);
    });
    return subscribeCategories(setCategories);
  }, []);

  // --- Group (folder) definitions + the active group new captures go into -------------
  useEffect(() => {
    let active = true;
    void loadGroups().then((g) => {
      if (active) setGroups(g);
    });
    void loadActiveGroupId().then((id) => {
      if (active) setActiveGroupId(id);
    });
    const offGroups = subscribeGroups(setGroups);
    const offActive = subscribeActiveGroup(setActiveGroupId);
    return () => {
      active = false;
      offGroups();
      offActive();
    };
  }, []);

  const chooseActiveGroup = useCallback((id: string | null) => {
    setActiveGroupId(id);
    void saveActiveGroupId(id);
  }, []);

  // Create a top-level group inline and return its id (used by the "+ Новая группа" rows).
  const createGroupReturningId = useCallback(
    (name: string): string => {
      const id = crypto.randomUUID();
      const now = Date.now();
      const next: Group[] = [
        ...groups,
        { id, name, parentId: null, createdAt: now, updatedAt: now },
      ];
      setGroups(next);
      void saveGroups(next);
      return id;
    },
    [groups],
  );

  // --- Track the current text selection for the floating toolbar ----------------------
  useEffect(() => {
    if (disabled) return; // no toolbar on disabled sites → no per-selection work

    // Read the current usable text selection, or null (collapsed / empty / inside our own UI).
    const readSelection = (): SelectionState | null => {
      const sel = window.getSelection();
      if (!sel || sel.isCollapsed || sel.rangeCount === 0) return null;
      const text = sel.toString();
      if (!text.trim()) return null;
      const node = sel.anchorNode;
      const host = node instanceof Element ? node : (node?.parentElement ?? null);
      if (host?.closest('[data-wm-ui]')) return null;
      const range = sel.getRangeAt(0);
      const rect = range.getBoundingClientRect();
      if (rect.width === 0 && rect.height === 0) return null;
      return { range: range.cloneRange(), rect, text };
    };

    let raf = 0;
    const show = () => {
      const state = readSelection();
      selectionRef.current = state;
      setSelection(state);
    };

    // While dragging, selectionchange fires continuously. Keep the ref fresh (for the context
    // menu / side-panel actions) and hide the toolbar the moment the selection collapses — but
    // never SHOW here: showing mid-drag made the toolbar pop up under the moving cursor (worst
    // selecting right-to-left) and flicker. It's shown on mouseup / keyup, once selection ends.
    const onSelectionChange = () => {
      const state = readSelection();
      selectionRef.current = state;
      if (!state) setSelection(null);
    };

    // A press on the page starts a (possible) new selection: hide any open toolbar so it never
    // jumps from the old position to the new one. Presses on our own UI (data-wm-ui on the shadow
    // host) are ignored so the toolbar's own buttons keep working.
    let dragging = false;
    const onMouseDown = (e: MouseEvent) => {
      const target = e.target as Element | null;
      if (target?.closest?.('[data-wm-ui]')) return;
      dragging = true;
      setSelection(null);
    };

    // Release finalizes the selection → show the toolbar once, one frame later so the browser has
    // settled the final selection geometry (avoids a stale / jumpy position).
    const onMouseUp = () => {
      if (!dragging) return;
      dragging = false;
      cancelAnimationFrame(raf);
      raf = requestAnimationFrame(show);
    };

    // Keyboard selection (Shift+Arrows, Ctrl+A) has no mouseup — show on key release.
    const onKeyUp = () => show();

    document.addEventListener('selectionchange', onSelectionChange);
    document.addEventListener('mousedown', onMouseDown, true);
    document.addEventListener('mouseup', onMouseUp, true);
    document.addEventListener('keyup', onKeyUp, true);
    return () => {
      cancelAnimationFrame(raf);
      document.removeEventListener('selectionchange', onSelectionChange);
      document.removeEventListener('mousedown', onMouseDown, true);
      document.removeEventListener('mouseup', onMouseUp, true);
      document.removeEventListener('keyup', onKeyUp, true);
    };
  }, [disabled]);

  // --- Persisting -------------------------------------------------------------------
  const saveHighlight = useCallback(
    async (
      range: Range,
      captureMode: CaptureMode,
      note: string,
      category?: Category,
      groupId?: string | null,
    ) => {
      const anchor = captureTextAnchor(range);
      if (!anchor) {
        showToast('Не удалось сохранить выделение', 'err');
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
        // Color is denormalized from the category; uncategorized (incl. "important") stays yellow.
        color: category ? category.color : DEFAULT_HIGHLIGHT_COLOR,
        categoryId: category ? category.id : null,
        // Default to the active group; explicit null/id (e.g. from the note editor) overrides.
        groupId: groupId !== undefined ? groupId : activeGroupRef.current,
        anchor,
      };
      try {
        const saved = await sendBg({ type: 'SAVE_MEMORY', memory });
        if (!saved?.id) throw new Error('пустой ответ фонового процесса');
        applyHighlight(saved);
        // A text note also gets a floating on-page pin (notepad over the page), like element notes.
        if (saved.note) setPins((prev) => [...prev.filter((p) => p.id !== saved.id), saved]);
        showToast(
          note
            ? 'Заметка сохранена'
            : category
              ? `Сохранено: ${category.label}`
              : captureMode === 'important'
                ? 'Отмечено как важное'
                : 'Сохранено в Web Memory',
        );
        window.getSelection()?.removeAllRanges();
        setSelection(null);
        setMode({ kind: 'idle' });
      } catch (err) {
        console.error('[web-memory] save failed', err);
        showToast('Не удалось сохранить: ' + errText(err), 'err');
      }
    },
    [pageUrl, showToast],
  );

  const saveElementNote = useCallback(
    async (anchor: ElementAnchor, note: string, groupId?: string | null) => {
      const memory: NewMemory = {
        url: pageUrl,
        href: location.href,
        title: document.title,
        kind: 'note',
        text: anchor.label,
        note,
        important: false,
        color: '#fff3a3',
        groupId: groupId !== undefined ? groupId : activeGroupRef.current,
        anchor,
      };
      try {
        const saved = await sendBg({ type: 'SAVE_MEMORY', memory });
        if (!saved?.id) throw new Error('пустой ответ фонового процесса');
        setPins((prev) => [...prev, saved]);
        showToast('Заметка прикреплена к элементу');
        setMode({ kind: 'idle' });
      } catch (err) {
        console.error('[web-memory] save failed', err);
        showToast('Не удалось сохранить: ' + errText(err), 'err');
      }
    },
    [pageUrl, showToast],
  );

  // Delete a memory straight from its on-page note popup (inline UX, no side panel needed).
  // Background owns IndexedDB + broadcasts DATA_CHANGED, so any open side panel refreshes itself.
  const handleDeletePin = useCallback(
    async (m: Memory) => {
      try {
        await sendBg({ type: 'DELETE_MEMORY', id: m.id });
        removeHighlight(m.id);
        setPins((prev) => prev.filter((p) => p.id !== m.id));
        showToast('Заметка удалена');
      } catch (err) {
        console.error('[web-memory] delete failed', err);
        showToast('Не удалось удалить: ' + errText(err), 'err');
      }
    },
    [showToast],
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

  const onCategoryAction = useCallback(
    (category: Category) => {
      const sel = selectionRef.current;
      if (sel) void saveHighlight(sel.range, 'highlight', '', category);
    },
    [saveHighlight],
  );

  // --- Bus events from the content-script entry (context menu / side panel) ------------
  useEffect(() => {
    if (disabled) return; // in-page UI off here → ignore capture/element triggers
    const offCapture = on('capture', (captureMode) => {
      const sel = window.getSelection();
      let range: Range | null = null;
      if (sel && !sel.isCollapsed && sel.rangeCount > 0 && sel.toString().trim()) {
        range = sel.getRangeAt(0).cloneRange();
      } else if (selectionRef.current) {
        range = selectionRef.current.range;
      }
      if (!range) {
        showToast('Сначала выделите текст', 'err');
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
      setPins(memories.filter(isPinned)),
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
  }, [saveHighlight, showToast, disabled]);

  // --- Element-pick mode --------------------------------------------------------------
  useEffect(() => {
    if (disabled || mode.kind !== 'element-pick') {
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
    const onKey = (e: globalThis.KeyboardEvent) => {
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
  }, [mode, disabled]);

  const activeGroup = activeGroupId ? groups.find((g) => g.id === activeGroupId) : undefined;

  // In-page UI fully off here (master toggle off, or this host is blacklisted, e.g. messengers).
  // Nothing renders → no overlap, text selection/copy on the page is untouched.
  if (disabled) return null;

  return (
    <>
      {selection && mode.kind === 'idle' && (
        <div
          style={{
            position: 'fixed',
            top: Math.max(8, selection.rect.top - 50),
            left: Math.min(Math.max(8, selection.rect.left), Math.max(8, window.innerWidth - 400)),
            zIndex: Z + 2,
          }}
          onMouseDown={(e) => e.preventDefault()}
          className="flex max-w-[95vw] animate-scale-in flex-wrap items-center gap-0.5 rounded-xl border border-slate-200 bg-white p-1 shadow-lg ring-1 ring-black/5"
        >
          <div className="relative">
            <button
              onClick={() => setGroupMenuOpen((v) => !v)}
              title="Группа для новых сохранений"
              className="flex h-7 items-center gap-1 rounded-lg px-2 text-[12px] font-medium text-slate-600 transition-colors hover:bg-slate-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-400"
            >
              <Folder
                size={14}
                strokeWidth={1.75}
                className="text-slate-500"
                style={activeGroup?.color ? { color: activeGroup.color } : undefined}
              />
              <span className="max-w-[90px] truncate">
                {activeGroup ? activeGroup.name : 'Без группы'}
              </span>
              <ChevronDown size={12} strokeWidth={2} className="text-slate-400" />
            </button>
            {groupMenuOpen && (
              <GroupPickerMenu
                groups={groups}
                value={activeGroupId}
                align="left"
                onCreate={createGroupReturningId}
                onSelect={(id) => {
                  setGroupMenuOpen(false);
                  chooseActiveGroup(id);
                }}
                onClose={() => setGroupMenuOpen(false)}
              />
            )}
          </div>
          <span className="mx-0.5 h-5 w-px bg-slate-200" />
          <ToolbarButton onClick={() => onToolbarAction('highlight')} icon={Highlighter} label="Запомнить" />
          <ToolbarButton onClick={() => onToolbarAction('note')} icon={StickyNote} label="Заметка" />
          <ToolbarButton onClick={() => onToolbarAction('important')} icon={Star} label="Важно" />
          {categories.some((c) => c.builtin) && <span className="mx-0.5 h-5 w-px bg-slate-200" />}
          {categories
            .filter((c) => c.builtin)
            .map((cat) => (
              <button
                key={cat.id}
                onClick={() => onCategoryAction(cat)}
                title={`Запомнить · ${cat.label}`}
                aria-label={`Запомнить как «${cat.label}»`}
                className="flex h-7 w-7 items-center justify-center rounded-lg text-white transition-transform duration-150 hover:scale-110 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-400"
                style={{ backgroundColor: cat.color }}
              >
                <CategoryIcon name={cat.icon} size={14} strokeWidth={2} className="text-white" />
              </button>
            ))}
        </div>
      )}

      {(mode.kind === 'note' || mode.kind === 'element-note') && (
        <NoteEditor
          key={mode.kind}
          title={mode.kind === 'note' ? 'Заметка к выделению' : 'Заметка к элементу'}
          context={mode.kind === 'note' ? mode.selection.text : mode.anchor.label}
          groups={groups}
          activeGroupId={activeGroupId}
          onCreateGroup={createGroupReturningId}
          onCancel={() => setMode({ kind: 'idle' })}
          onSave={(note, groupId) => {
            if (mode.kind === 'note')
              void saveHighlight(mode.selection.range, 'note', note, undefined, groupId);
            else void saveElementNote(mode.anchor, note, groupId);
          }}
        />
      )}

      {mode.kind === 'element-pick' && (
        <>
          <div
            style={{ position: 'fixed', top: 14, left: 0, right: 0, zIndex: Z + 6 }}
            className="flex justify-center"
          >
            <div className="flex animate-slide-up items-center gap-2 rounded-full bg-slate-900 px-4 py-2 text-[13px] font-medium text-white shadow-xl">
              <MapPin size={15} className="text-accent-300" />
              Кликните по элементу
              <span className="text-slate-400">·</span>
              <kbd className="rounded bg-white/15 px-1.5 py-0.5 text-[11px] font-semibold">Esc</kbd>
              <span className="text-slate-300">отмена</span>
            </div>
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
                border: '2px solid #6366f1',
                background: 'rgba(99,102,241,.1)',
                borderRadius: 6,
              }}
            />
          )}
        </>
      )}

      <PinLayer
        pins={pins}
        closeOnOutsideClick={settings.closeNoteOnOutsideClick}
        onScrollTo={(m) => (m.anchor.kind === 'element' ? scrollToElement(m) : scrollToText(m))}
        onDelete={handleDeletePin}
      />

      {toast && (
        <div style={{ position: 'fixed', bottom: 24, left: 0, right: 0, zIndex: Z + 7 }} className="flex justify-center">
          <div className="flex animate-slide-up items-center gap-2 rounded-xl bg-slate-900 px-3.5 py-2.5 text-[13px] font-medium text-white shadow-xl">
            {toast.kind === 'err' ? (
              <TriangleAlert size={15} className="text-red-400" />
            ) : (
              <Check size={15} className="text-emerald-400" />
            )}
            {toast.text}
          </div>
        </div>
      )}
    </>
  );
}

function ToolbarButton({
  onClick,
  icon: Icon,
  label,
}: {
  onClick: () => void;
  icon: LucideIcon;
  label: string;
}) {
  return (
    <button
      onClick={onClick}
      title={label}
      className="flex items-center gap-1.5 rounded-lg px-2.5 py-1.5 text-[12px] font-medium text-slate-700 transition-colors duration-150 hover:bg-slate-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-400"
    >
      <Icon size={15} strokeWidth={1.75} className="text-slate-500" />
      {label}
    </button>
  );
}

function NoteEditor({
  title,
  context,
  groups,
  activeGroupId,
  onCreateGroup,
  onSave,
  onCancel,
}: {
  title: string;
  context: string;
  groups: Group[];
  activeGroupId: string | null;
  onCreateGroup: (name: string) => string;
  onSave: (note: string, groupId: string | null) => void;
  onCancel: () => void;
}) {
  const [value, setValue] = useState('');
  const [groupId, setGroupId] = useState<string | null>(activeGroupId);
  const [groupOpen, setGroupOpen] = useState(false);
  const ref = useRef<HTMLTextAreaElement>(null);
  useEffect(() => {
    ref.current?.focus();
  }, []);
  const group = groupId ? groups.find((g) => g.id === groupId) : undefined;
  const onKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Escape') onCancel();
    else if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) onSave(value.trim(), groupId);
  };
  return (
    <div
      style={{ position: 'fixed', inset: 0, zIndex: Z + 5 }}
      className="flex items-start justify-center bg-slate-900/30 pt-24 backdrop-blur-sm"
      onMouseDown={onCancel}
    >
      <div
        className="w-[400px] max-w-[92vw] animate-scale-in overflow-hidden rounded-2xl bg-white shadow-2xl ring-1 ring-black/5"
        onMouseDown={(e) => e.stopPropagation()}
      >
        <div className="flex items-center gap-2 border-b border-slate-100 px-4 py-3">
          <StickyNote size={16} strokeWidth={1.75} className="text-accent-600" />
          <h3 className="flex-1 text-[14px] font-semibold text-slate-900">{title}</h3>
          <button
            onClick={onCancel}
            aria-label="Закрыть"
            className="inline-flex h-7 w-7 items-center justify-center rounded-md text-slate-400 transition-colors hover:bg-slate-100 hover:text-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-300"
          >
            <X size={16} strokeWidth={1.75} />
          </button>
        </div>
        <div className="p-4">
          <p className="mb-3 line-clamp-2 rounded-lg border border-slate-100 bg-slate-50 px-2.5 py-2 text-[12px] leading-relaxed text-slate-500">
            {context}
          </p>
          <textarea
            ref={ref}
            value={value}
            onChange={(e) => setValue(e.target.value)}
            onKeyDown={onKeyDown}
            rows={4}
            placeholder="Например: использовать в проекте, проверить позже…"
            className="w-full resize-none rounded-lg border border-slate-300 bg-white p-2.5 text-[13px] text-slate-800 placeholder:text-slate-400 outline-none transition-all duration-150 focus:border-accent-400 focus:ring-2 focus:ring-accent-500/20"
          />
          <div className="relative mt-3">
            <button
              onClick={() => setGroupOpen((v) => !v)}
              className="flex items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-2.5 py-1.5 text-[12px] font-medium text-slate-600 transition-colors hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-400"
            >
              <Folder
                size={13}
                strokeWidth={1.75}
                className="text-slate-500"
                style={group?.color ? { color: group.color } : undefined}
              />
              <span className="max-w-[200px] truncate">{group ? group.name : 'Без группы'}</span>
              <ChevronDown size={12} strokeWidth={2} className="text-slate-400" />
            </button>
            {groupOpen && (
              <GroupPickerMenu
                groups={groups}
                value={groupId}
                align="left"
                vAlign="bottom"
                onCreate={onCreateGroup}
                onSelect={(id) => {
                  setGroupOpen(false);
                  setGroupId(id);
                }}
                onClose={() => setGroupOpen(false)}
              />
            )}
          </div>
          <div className="mt-3 flex items-center justify-between">
            <span className="text-[11px] text-slate-400">⌘↵ — сохранить</span>
            <div className="flex gap-2">
              <Button variant="ghost" size="md" onClick={onCancel}>
                Отмена
              </Button>
              <Button variant="primary" size="md" onClick={() => onSave(value.trim(), groupId)}>
                Сохранить
              </Button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

function PinLayer({
  pins,
  onScrollTo,
  onDelete,
  closeOnOutsideClick,
}: {
  pins: Memory[];
  onScrollTo: (m: Memory) => void;
  onDelete: (m: Memory) => void;
  closeOnOutsideClick: boolean;
}) {
  const [positions, setPositions] = useState<Record<string, { x: number; y: number }>>({});
  const [open, setOpen] = useState<Memory | null>(null);
  // Id of the pin whose delete awaits a confirming second click (no dialog / extra window).
  const [confirmingId, setConfirmingId] = useState<string | null>(null);

  // Close the popup and clear any pending delete confirmation.
  const close = useCallback(() => {
    setOpen(null);
    setConfirmingId(null);
  }, []);

  useEffect(() => {
    const update = () => {
      const next: Record<string, { x: number; y: number }> = {};
      for (const pin of pins) {
        let rect: DOMRect | null = null;
        let trailing = false; // text pins sit just after the phrase; element pins at the corner
        if (pin.anchor.kind === 'element') {
          const el = findElement(pin.anchor);
          if (el) rect = el.getBoundingClientRect();
        } else {
          const el = findHighlightEl(pin.id);
          if (el) {
            rect = el.getBoundingClientRect();
            trailing = true;
          }
        }
        if (!rect) continue;
        next[pin.id] = {
          x: trailing ? rect.right + window.scrollX + 2 : rect.left + window.scrollX - 11,
          y: trailing ? rect.top + window.scrollY - 3 : rect.top + window.scrollY - 11,
        };
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
            onClick={() => {
              setConfirmingId(null);
              setOpen((cur) => (cur?.id === pin.id ? null : pin));
            }}
            style={{ position: 'absolute', top: pos.y, left: pos.x, zIndex: Z }}
            className="flex h-[22px] w-[22px] items-center justify-center rounded-full bg-accent-600 text-white shadow-md ring-2 ring-white transition-transform duration-150 hover:scale-110"
            title={pin.note || pin.text}
          >
            <StickyNote size={12} strokeWidth={2} />
          </button>
        );
      })}
      {open && positions[open.id] && (
        <>
          {/* Backdrop: a click outside the popup closes it (opt-in). Inner mousedown is stopped
              below, so clicks/selection inside the popup never close it. */}
          {closeOnOutsideClick && (
            // Below pins (Z) and the popup (Z+1), so clicking another pin still opens it directly.
            <div style={{ position: 'fixed', inset: 0, zIndex: Z - 1 }} onMouseDown={close} />
          )}
          <div
            style={{
              position: 'absolute',
              top: positions[open.id].y + 26,
              left: positions[open.id].x,
              zIndex: Z + 1,
            }}
            className="w-64 animate-scale-in rounded-xl border border-slate-200 bg-white p-3 text-[13px] shadow-xl ring-1 ring-black/5"
            onMouseDown={(e) => e.stopPropagation()}
          >
            <p className="mb-2.5 whitespace-pre-wrap leading-relaxed text-slate-700">
              {open.note || open.text}
            </p>
            <div className="flex items-center gap-2">
              <Button
                variant="primary"
                size="sm"
                onClick={() => {
                  onScrollTo(open);
                  close();
                }}
              >
                Показать
              </Button>
              <Button variant="ghost" size="sm" onClick={close}>
                Закрыть
              </Button>
              {confirmingId === open.id ? (
                <Button
                  variant="danger"
                  size="sm"
                  className="ml-auto"
                  onClick={() => {
                    const m = open;
                    close();
                    onDelete(m);
                  }}
                >
                  Удалить?
                </Button>
              ) : (
                <IconButton
                  variant="danger"
                  className="ml-auto"
                  title="Удалить заметку"
                  aria-label="Удалить заметку"
                  onClick={() => setConfirmingId(open.id)}
                >
                  <Trash2 size={15} strokeWidth={1.75} />
                </IconButton>
              )}
            </div>
          </div>
        </>
      )}
    </>
  );
}
