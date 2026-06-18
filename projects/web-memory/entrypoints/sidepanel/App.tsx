import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { sendBg, sendTab } from '@/lib/messages';
import type { Memory } from '@/lib/types';
import { normalizeUrl, prettyUrl } from '@/lib/url';
import { buildIndex } from '@/lib/search';

interface ActiveTab {
  id: number;
  url: string;
  title: string;
}

type Panel = 'page' | 'all';

export function App() {
  const [memories, setMemories] = useState<Memory[]>([]);
  const [activeTab, setActiveTab] = useState<ActiveTab | null>(null);
  const [panel, setPanel] = useState<Panel>('page');
  const [query, setQuery] = useState('');

  const refresh = useCallback(async () => {
    setMemories(await sendBg({ type: 'GET_ALL_MEMORIES' }));
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  // Track the active tab so "Эта страница" stays in sync.
  useEffect(() => {
    const load = async () => {
      const [tab] = await browser.tabs.query({ active: true, currentWindow: true });
      if (tab?.id != null) {
        setActiveTab({ id: tab.id, url: normalizeUrl(tab.url ?? ''), title: tab.title ?? '' });
      }
    };
    void load();
    const onActivated = () => void load();
    const onUpdated = (_tabId: number, info: { status?: string; url?: string }) => {
      if (info.status === 'complete' || info.url) void load();
    };
    browser.tabs.onActivated.addListener(onActivated);
    browser.tabs.onUpdated.addListener(onUpdated);
    return () => {
      browser.tabs.onActivated.removeListener(onActivated);
      browser.tabs.onUpdated.removeListener(onUpdated);
    };
  }, []);

  const index = useMemo(() => buildIndex(memories), [memories]);
  const byId = useMemo(() => new Map(memories.map((m) => [m.id, m])), [memories]);

  const pageMemories = useMemo(
    () => (activeTab ? memories.filter((m) => m.url === activeTab.url) : []),
    [memories, activeTab],
  );

  const searchResults = useMemo(() => {
    if (!query.trim()) return memories;
    return index
      .search(query)
      .map((id) => byId.get(id))
      .filter((m): m is Memory => !!m);
  }, [query, index, byId, memories]);

  // Re-render an edited highlight on the live page (remove + reapply with new style/note).
  const relayReapply = useCallback(
    (mem: Memory) => {
      if (activeTab && mem.url === activeTab.url) {
        void sendTab(activeTab.id, { type: 'REMOVE_MEMORY', id: mem.id });
        void sendTab(activeTab.id, { type: 'REAPPLY' });
      }
    },
    [activeTab],
  );

  const navigate = useCallback(
    (mem: Memory) => {
      if (activeTab && mem.url === activeTab.url) {
        void sendTab(activeTab.id, { type: 'SCROLL_TO_MEMORY', id: mem.id });
      } else {
        void sendBg({ type: 'OPEN_AND_SCROLL', href: mem.href, id: mem.id });
      }
    },
    [activeTab],
  );

  const remove = useCallback(
    async (mem: Memory) => {
      await sendBg({ type: 'DELETE_MEMORY', id: mem.id });
      if (activeTab && mem.url === activeTab.url) {
        void sendTab(activeTab.id, { type: 'REMOVE_MEMORY', id: mem.id });
      }
      await refresh();
    },
    [activeTab, refresh],
  );

  const saveNote = useCallback(
    async (mem: Memory, note: string) => {
      const updated = await sendBg({ type: 'UPDATE_MEMORY', id: mem.id, patch: { note } });
      await refresh();
      if (updated) relayReapply(updated);
    },
    [refresh, relayReapply],
  );

  const toggleImportant = useCallback(
    async (mem: Memory) => {
      const updated = await sendBg({
        type: 'UPDATE_MEMORY',
        id: mem.id,
        patch: { important: !mem.important },
      });
      await refresh();
      if (updated) relayReapply(updated);
    },
    [refresh, relayReapply],
  );

  const startElementNote = useCallback(() => {
    if (activeTab) void sendTab(activeTab.id, { type: 'START_ELEMENT_NOTE' });
  }, [activeTab]);

  return (
    <div className="flex h-full flex-col bg-slate-50 text-slate-800">
      <header className="border-b border-slate-200 bg-white px-4 py-3">
        <div className="flex items-center gap-2">
          <span className="text-lg">🧠</span>
          <h1 className="text-base font-semibold">Web Memory</h1>
          <span className="ml-auto rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-500">
            {memories.length} сохранено
          </span>
        </div>
        <nav className="mt-3 flex gap-1 rounded-lg bg-slate-100 p-1 text-sm">
          <TabButton active={panel === 'page'} onClick={() => setPanel('page')}>
            Эта страница{pageMemories.length ? ` · ${pageMemories.length}` : ''}
          </TabButton>
          <TabButton active={panel === 'all'} onClick={() => setPanel('all')}>
            Моя память
          </TabButton>
        </nav>
      </header>

      <main className="flex-1 overflow-y-auto px-3 py-3">
        {panel === 'page' ? (
          <PagePanel
            activeTab={activeTab}
            items={pageMemories}
            onNavigate={navigate}
            onRemove={remove}
            onSaveNote={saveNote}
            onToggleImportant={toggleImportant}
            onStartElementNote={startElementNote}
          />
        ) : (
          <AllPanel
            query={query}
            onQuery={setQuery}
            items={searchResults}
            onNavigate={navigate}
            onRemove={remove}
            onSaveNote={saveNote}
            onToggleImportant={toggleImportant}
          />
        )}
      </main>
    </div>
  );
}

function TabButton({
  active,
  onClick,
  children,
}: {
  active: boolean;
  onClick: () => void;
  children: ReactNode;
}) {
  return (
    <button
      onClick={onClick}
      className={`flex-1 rounded-md px-3 py-1.5 font-medium transition ${
        active ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-500 hover:text-slate-700'
      }`}
    >
      {children}
    </button>
  );
}

interface RowHandlers {
  onNavigate: (m: Memory) => void;
  onRemove: (m: Memory) => void;
  onSaveNote: (m: Memory, note: string) => void;
  onToggleImportant: (m: Memory) => void;
}

function PagePanel({
  activeTab,
  items,
  onStartElementNote,
  ...handlers
}: RowHandlers & {
  activeTab: ActiveTab | null;
  items: Memory[];
  onStartElementNote: () => void;
}) {
  return (
    <div className="space-y-3">
      {activeTab && (
        <div className="rounded-lg border border-slate-200 bg-white px-3 py-2">
          <p className="truncate text-sm font-medium text-slate-700">
            {activeTab.title || 'Текущая страница'}
          </p>
          <p className="truncate text-xs text-slate-400">{prettyUrl(activeTab.url)}</p>
        </div>
      )}
      <button
        onClick={onStartElementNote}
        className="w-full rounded-lg border border-dashed border-brand-400 bg-brand-50 px-3 py-2 text-sm font-medium text-brand-600 hover:bg-brand-100"
      >
        📌 Прикрепить заметку к элементу
      </button>
      {items.length === 0 ? (
        <EmptyState
          title="На этой странице пока пусто"
          hint="Выделите текст на странице — появится панель «Запомнить»."
        />
      ) : (
        <ul className="space-y-2">
          {items.map((m) => (
            <MemoryRow key={m.id} memory={m} {...handlers} />
          ))}
        </ul>
      )}
    </div>
  );
}

function AllPanel({
  query,
  onQuery,
  items,
  ...handlers
}: RowHandlers & {
  query: string;
  onQuery: (q: string) => void;
  items: Memory[];
}) {
  return (
    <div className="space-y-3">
      <input
        value={query}
        onChange={(e) => onQuery(e.target.value)}
        placeholder="Поиск по фрагментам, заметкам, заголовкам…"
        className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500"
      />
      {items.length === 0 ? (
        <EmptyState
          title={query ? 'Ничего не найдено' : 'Память пуста'}
          hint={query ? 'Попробуйте другой запрос.' : 'Сохранённые фрагменты появятся здесь.'}
        />
      ) : (
        <ul className="space-y-2">
          {items.map((m) => (
            <MemoryRow key={m.id} memory={m} showPage {...handlers} />
          ))}
        </ul>
      )}
    </div>
  );
}

function MemoryRow({
  memory,
  showPage,
  onNavigate,
  onRemove,
  onSaveNote,
  onToggleImportant,
}: RowHandlers & { memory: Memory; showPage?: boolean }) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(memory.note);

  return (
    <li className="rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
      <div className="flex items-start gap-2">
        <span
          className="mt-0.5 inline-block h-3 w-3 shrink-0 rounded-sm"
          style={{ background: memory.kind === 'highlight' ? memory.color : '#e2e8f0' }}
          title={memory.kind === 'highlight' ? 'Фрагмент' : 'Заметка к элементу'}
        />
        <button
          onClick={() => onNavigate(memory)}
          className="flex-1 text-left text-sm leading-snug text-slate-700 hover:text-brand-600"
        >
          <span className="line-clamp-3">
            {memory.important && '⭐ '}
            {memory.kind === 'note' && '📌 '}
            {memory.text || '(без текста)'}
          </span>
        </button>
      </div>

      {memory.note && !editing && (
        <p className="mt-2 whitespace-pre-wrap rounded bg-amber-50 px-2 py-1 text-xs text-amber-800">
          📝 {memory.note}
        </p>
      )}

      {editing && (
        <div className="mt-2">
          <textarea
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            rows={3}
            className="w-full resize-none rounded border border-slate-300 p-2 text-xs outline-none focus:border-brand-500"
          />
          <div className="mt-1 flex justify-end gap-2">
            <button
              onClick={() => {
                setDraft(memory.note);
                setEditing(false);
              }}
              className="rounded px-2 py-1 text-xs text-slate-500 hover:bg-slate-100"
            >
              Отмена
            </button>
            <button
              onClick={() => {
                onSaveNote(memory, draft.trim());
                setEditing(false);
              }}
              className="rounded bg-brand-500 px-2 py-1 text-xs font-medium text-white hover:bg-brand-600"
            >
              Сохранить
            </button>
          </div>
        </div>
      )}

      {showPage && (
        <p className="mt-2 truncate text-xs text-slate-400" title={memory.href}>
          {memory.title || prettyUrl(memory.url)}
        </p>
      )}

      <div className="mt-2 flex items-center gap-3 text-xs text-slate-400">
        <span>{formatDate(memory.createdAt)}</span>
        <button onClick={() => onNavigate(memory)} className="ml-auto hover:text-brand-600">
          Перейти
        </button>
        <button
          onClick={() => onToggleImportant(memory)}
          className={memory.important ? 'text-amber-500' : 'hover:text-amber-500'}
          title="Важное"
        >
          ⭐
        </button>
        <button onClick={() => setEditing((v) => !v)} className="hover:text-brand-600" title="Заметка">
          ✎
        </button>
        <button onClick={() => onRemove(memory)} className="hover:text-red-500" title="Удалить">
          🗑
        </button>
      </div>
    </li>
  );
}

function EmptyState({ title, hint }: { title: string; hint: string }) {
  return (
    <div className="rounded-lg border border-dashed border-slate-300 bg-white px-4 py-8 text-center">
      <p className="text-sm font-medium text-slate-600">{title}</p>
      <p className="mt-1 text-xs text-slate-400">{hint}</p>
    </div>
  );
}

function formatDate(ts: number): string {
  return new Date(ts).toLocaleDateString('ru-RU', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  });
}
