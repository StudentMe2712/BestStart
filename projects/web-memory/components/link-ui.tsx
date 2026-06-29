import { useEffect, useId, useState, type KeyboardEvent, type ReactNode } from 'react';
import { Check, ExternalLink, Folder, Globe, Pencil, Trash2, X } from 'lucide-react';
import type { Group, Link } from '@/lib/types';
import { pathOf } from '@/lib/groups';
import { Button, IconButton } from '@/components/ui';
import { DND_ITEM, GroupPickerMenu } from '@/components/groups-ui';

export function domainOf(url: string): string {
  try {
    return new URL(url).hostname.replace(/^www\./, '');
  } catch {
    return url;
  }
}

export function normalizeTag(raw: string): string {
  return raw.trim().replace(/^#+/, '').toLowerCase().replace(/\s+/g, '-');
}

const MONO_COLORS = ['#6366f1', '#0ea5e9', '#10b981', '#f59e0b', '#ef4444', '#a855f7', '#ec4899', '#14b8a6'];
function monoColor(s: string): string {
  let h = 0;
  for (let i = 0; i < s.length; i++) h = (h * 31 + s.charCodeAt(i)) >>> 0;
  return MONO_COLORS[h % MONO_COLORS.length];
}

/** Favicon with a local fallback chain: stored favicon → Chrome's _favicon cache → monogram. */
export function Favicon({ url, favicon, size = 18 }: { url: string; favicon?: string; size?: number }) {
  const domain = domainOf(url);
  const sources: string[] = [];
  if (favicon) sources.push(favicon);
  try {
    // _favicon is a Chrome runtime path not in WXT's typed PublicPath union — cast through.
    const getURL = browser.runtime.getURL as unknown as (p: string) => string;
    sources.push(getURL(`/_favicon/?pageUrl=${encodeURIComponent(url)}&size=32`));
  } catch {
    /* getURL unavailable */
  }
  const [idx, setIdx] = useState(0);
  useEffect(() => setIdx(0), [url, favicon]);

  if (idx < sources.length) {
    return (
      <img
        src={sources[idx]}
        alt=""
        onError={() => setIdx((i) => i + 1)}
        className="rounded-sm object-contain"
        style={{ width: size, height: size }}
      />
    );
  }
  return (
    <span
      className="flex items-center justify-center rounded-sm text-[10px] font-bold text-white"
      style={{ width: size, height: size, backgroundColor: monoColor(domain) }}
    >
      {domain.charAt(0).toUpperCase()}
    </span>
  );
}

export function TagInput({
  value,
  onChange,
  suggestions = [],
  autoFocus,
}: {
  value: string[];
  onChange: (tags: string[]) => void;
  suggestions?: string[];
  autoFocus?: boolean;
}) {
  const [draft, setDraft] = useState('');
  const listId = useId();
  const add = (raw: string) => {
    const t = normalizeTag(raw);
    if (t && !value.includes(t)) onChange([...value, t]);
    setDraft('');
  };
  const remove = (t: string) => onChange(value.filter((x) => x !== t));
  const onKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter' || e.key === ',') {
      e.preventDefault();
      add(draft);
    } else if (e.key === 'Backspace' && !draft && value.length) {
      remove(value[value.length - 1]);
    }
  };
  return (
    <div className="flex flex-wrap items-center gap-1 rounded-lg border border-slate-300 bg-white p-1.5 transition-all focus-within:border-accent-400 focus-within:ring-2 focus-within:ring-accent-500/20">
      {value.map((t) => (
        <span
          key={t}
          className="inline-flex items-center gap-1 rounded-full bg-accent-50 px-2 py-0.5 text-[11px] font-medium text-accent-700"
        >
          #{t}
          <button onClick={() => remove(t)} aria-label={`Удалить тег ${t}`} className="hover:text-accent-900">
            <X size={11} />
          </button>
        </span>
      ))}
      <input
        list={listId}
        value={draft}
        autoFocus={autoFocus}
        onChange={(e) => setDraft(e.target.value)}
        onKeyDown={onKeyDown}
        onBlur={() => draft && add(draft)}
        placeholder={value.length ? '' : '#тег'}
        className="min-w-[64px] flex-1 bg-transparent px-1 py-0.5 text-[12px] text-slate-800 outline-none placeholder:text-slate-400"
      />
      <datalist id={listId}>
        {suggestions.filter((s) => !value.includes(s)).map((s) => (
          <option key={s} value={s} />
        ))}
      </datalist>
    </div>
  );
}

export function TypeBadge({ kind }: { kind: 'memory' | 'note' | 'link' }) {
  const map = {
    memory: { label: 'Фрагмент', cls: 'bg-amber-100 text-amber-700' },
    note: { label: 'Заметка', cls: 'bg-slate-100 text-slate-600' },
    link: { label: 'Ссылка', cls: 'bg-accent-100 text-accent-700' },
  } as const;
  const b = map[kind];
  return (
    <span className={`inline-block rounded px-1.5 py-0.5 text-[10px] font-semibold ${b.cls}`}>{b.label}</span>
  );
}

export function LinkCard({
  link,
  suggestions,
  badge,
  groups = [],
  onSetGroup,
  onCreateGroup,
  onOpen,
  onUpdate,
  onDelete,
  onTagClick,
}: {
  link: Link;
  suggestions: string[];
  badge?: ReactNode;
  groups?: Group[];
  onSetGroup?: (l: Link, groupId: string | null) => void;
  onCreateGroup?: (name: string) => string;
  onOpen: (l: Link) => void;
  onUpdate: (l: Link, patch: { title: string; tags: string[] }) => void;
  onDelete: (l: Link) => void;
  onTagClick?: (tag: string) => void;
}) {
  const [editing, setEditing] = useState(false);
  const [title, setTitle] = useState(link.title);
  const [tags, setTags] = useState<string[]>(link.tags);
  const [groupMenuOpen, setGroupMenuOpen] = useState(false);
  const domain = domainOf(link.url);
  const groupPath = pathOf(groups, link.groupId);
  const group = groupPath.length ? groupPath[groupPath.length - 1] : undefined;

  const startEdit = () => {
    setTitle(link.title);
    setTags(link.tags);
    setEditing(true);
  };
  const save = () => {
    onUpdate(link, { title: title.trim() || link.url, tags });
    setEditing(false);
  };

  return (
    <li
      draggable={!!onSetGroup && !editing}
      onDragStart={(e) => {
        e.dataTransfer.setData(DND_ITEM, link.id);
        e.dataTransfer.effectAllowed = 'move';
      }}
      className="group rounded-xl border border-slate-200 bg-white transition-all duration-150 hover:border-slate-300 hover:shadow-sm"
    >
      <div className="flex items-start gap-2.5 p-3">
        <span className="mt-0.5 shrink-0">
          <Favicon url={link.url} favicon={link.favicon} size={18} />
        </span>
        <button onClick={() => onOpen(link)} className="min-w-0 flex-1 text-left" title={link.url}>
          {badge && <span className="mb-1 block">{badge}</span>}
          <span className="line-clamp-2 text-[13px] font-medium text-slate-800 transition-colors group-hover:text-accent-700">
            {link.title || link.url}
          </span>
          <span className="mt-0.5 flex items-center gap-1 truncate text-[11px] text-slate-400">
            <Globe size={11} strokeWidth={1.75} className="shrink-0" />
            {domain}
          </span>
        </button>
        <ExternalLink size={14} strokeWidth={1.75} className="mt-0.5 shrink-0 text-slate-300" />
      </div>

      {link.tags.length > 0 && !editing && (
        <div className="flex flex-wrap gap-1 px-3 pb-2.5">
          {link.tags.map((t) => (
            <button
              key={t}
              onClick={() => onTagClick?.(t)}
              className="rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-500 transition-colors hover:bg-slate-200 hover:text-slate-700"
            >
              #{t}
            </button>
          ))}
        </div>
      )}

      {editing && (
        <div className="space-y-2 px-3 pb-3">
          <input
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="Название"
            className="w-full rounded-lg border border-slate-300 bg-white px-2.5 py-1.5 text-[13px] text-slate-800 outline-none transition-all focus:border-accent-400 focus:ring-2 focus:ring-accent-500/20"
          />
          <TagInput value={tags} onChange={setTags} suggestions={suggestions} />
          <div className="flex justify-end gap-1.5">
            <Button variant="ghost" size="sm" onClick={() => setEditing(false)}>
              Отмена
            </Button>
            <Button variant="primary" size="sm" onClick={save}>
              <Check size={14} strokeWidth={2} />
              Сохранить
            </Button>
          </div>
        </div>
      )}

      <div className="flex items-center gap-1 border-t border-slate-100 px-2.5 py-1.5">
        <span className="text-[11px] tabular-nums text-slate-400">{formatDate(link.createdAt)}</span>
        {group && (
          <button
            onClick={() => setGroupMenuOpen((v) => !v)}
            title={groupPath.map((g) => g.name).join(' / ')}
            className="inline-flex min-w-0 max-w-[45%] items-center gap-1 rounded-full bg-slate-100 px-2 py-0.5 text-[10.5px] font-medium text-slate-500 transition-colors hover:bg-slate-200"
          >
            <Folder
              size={11}
              strokeWidth={1.75}
              className="shrink-0"
              style={group.color ? { color: group.color } : undefined}
            />
            <span className="truncate">{group.name}</span>
          </button>
        )}
        <div className="ml-auto flex items-center gap-0.5">
          <IconButton onClick={() => onOpen(link)} title="Открыть" aria-label="Открыть">
            <ExternalLink size={15} strokeWidth={1.75} />
          </IconButton>
          {onSetGroup && (
            <div className="relative">
              <IconButton
                variant={group ? 'active' : 'default'}
                onClick={() => setGroupMenuOpen((v) => !v)}
                title={group ? `Группа: ${group.name}` : 'Добавить в группу'}
                aria-label="Группа"
              >
                <Folder size={15} strokeWidth={1.75} />
              </IconButton>
              {groupMenuOpen && (
                <GroupPickerMenu
                  groups={groups}
                  value={link.groupId ?? null}
                  onCreate={onCreateGroup}
                  onSelect={(id) => {
                    setGroupMenuOpen(false);
                    onSetGroup(link, id);
                  }}
                  onClose={() => setGroupMenuOpen(false)}
                />
              )}
            </div>
          )}
          <IconButton onClick={startEdit} title="Редактировать" aria-label="Редактировать">
            <Pencil size={15} strokeWidth={1.75} />
          </IconButton>
          <IconButton variant="danger" onClick={() => onDelete(link)} title="Удалить" aria-label="Удалить">
            <Trash2 size={15} strokeWidth={1.75} />
          </IconButton>
        </div>
      </div>
    </li>
  );
}

function formatDate(ts: number): string {
  return new Date(ts).toLocaleDateString('ru-RU', { day: 'numeric', month: 'short', year: 'numeric' });
}
