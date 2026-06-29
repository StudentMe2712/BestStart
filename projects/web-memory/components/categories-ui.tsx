import { useState, type ReactNode } from 'react';
import { Check, Plus, Settings2, Star, Tag, Trash2, X } from 'lucide-react';
import { PRESET_COLORS, type Category } from '@/lib/types';
import { CategoryIcon, CUSTOM_ICON_CHOICES } from '@/components/category-icon';
import { ColorPicker } from '@/components/color-picker';
import { Button, IconButton } from '@/components/ui';
import { hexToRgba } from '@/lib/color';

/** Pseudo-id used in filters / assignment to mean "no category". */
export const UNCATEGORIZED = '__none__';

export interface MemoryFilters {
  categoryIds: Set<string>; // empty = any; may contain UNCATEGORIZED
  importantOnly: boolean;
  kind: 'all' | 'highlight' | 'note';
}

export const EMPTY_FILTERS: MemoryFilters = {
  categoryIds: new Set(),
  importantOnly: false,
  kind: 'all',
};

export function filtersActive(f: MemoryFilters): boolean {
  return f.categoryIds.size > 0 || f.importantOnly || f.kind !== 'all';
}

function Chip({
  active,
  onClick,
  title,
  children,
  dotColor,
}: {
  active: boolean;
  onClick: () => void;
  title?: string;
  children: ReactNode;
  dotColor?: string;
}) {
  return (
    <button
      onClick={onClick}
      title={title}
      className={`inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-[11px] font-medium transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-400 ${
        active
          ? 'border-accent-200 bg-accent-50 text-accent-700'
          : 'border-slate-200 bg-white text-slate-500 hover:bg-slate-50'
      }`}
      style={active && dotColor ? { borderColor: dotColor, backgroundColor: hexToRgba(dotColor, 0.12) } : undefined}
    >
      {children}
    </button>
  );
}

export function CategoryFilterBar({
  categories,
  filters,
  onChange,
  onManage,
}: {
  categories: Category[];
  filters: MemoryFilters;
  onChange: (f: MemoryFilters) => void;
  onManage: () => void;
}) {
  const toggleCategory = (id: string) => {
    const next = new Set(filters.categoryIds);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    onChange({ ...filters, categoryIds: next });
  };
  const setKind = (kind: MemoryFilters['kind']) =>
    onChange({ ...filters, kind: filters.kind === kind ? 'all' : kind });

  return (
    <div className="flex flex-wrap items-center gap-1.5">
      <Chip active={filters.kind === 'highlight'} onClick={() => setKind('highlight')} title="Только фрагменты">
        Фрагменты
      </Chip>
      <Chip active={filters.kind === 'note'} onClick={() => setKind('note')} title="Только заметки">
        Заметки
      </Chip>
      <Chip
        active={filters.importantOnly}
        onClick={() => onChange({ ...filters, importantOnly: !filters.importantOnly })}
        title="Только важные"
      >
        <Star size={11} className={filters.importantOnly ? 'fill-current' : ''} />
        Важные
      </Chip>
      <span className="h-4 w-px bg-slate-200" />
      {categories.map((c) => (
        <Chip
          key={c.id}
          active={filters.categoryIds.has(c.id)}
          onClick={() => toggleCategory(c.id)}
          dotColor={c.color}
          title={c.label}
        >
          <CategoryIcon name={c.icon} size={11} className="shrink-0" />
          {c.label}
        </Chip>
      ))}
      <Chip
        active={filters.categoryIds.has(UNCATEGORIZED)}
        onClick={() => toggleCategory(UNCATEGORIZED)}
        title="Без категории"
      >
        Без категории
      </Chip>
      <button
        onClick={onManage}
        title="Управление категориями"
        aria-label="Управление категориями"
        className="ml-auto inline-flex h-6 w-6 items-center justify-center rounded-md text-slate-400 transition-colors hover:bg-slate-100 hover:text-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-300"
      >
        <Settings2 size={14} strokeWidth={1.75} />
      </button>
    </div>
  );
}

/** Small popover to (re)assign a memory's category. Rendered with a transparent backdrop. */
export function CategoryAssignMenu({
  categories,
  value,
  onSelect,
  onClose,
}: {
  categories: Category[];
  value: string | null | undefined;
  onSelect: (categoryId: string | null) => void;
  onClose: () => void;
}) {
  return (
    <>
      <div className="fixed inset-0 z-10" onClick={onClose} />
      <div className="absolute right-0 z-20 mt-1 w-44 animate-scale-in overflow-hidden rounded-xl border border-slate-200 bg-white py-1 shadow-xl ring-1 ring-black/5">
        <MenuItem active={!value} onClick={() => onSelect(null)}>
          <span className="flex h-4 w-4 items-center justify-center text-slate-400">
            <Tag size={12} />
          </span>
          Без категории
        </MenuItem>
        {categories.map((c) => (
          <MenuItem key={c.id} active={value === c.id} onClick={() => onSelect(c.id)}>
            <span
              className="flex h-4 w-4 items-center justify-center rounded"
              style={{ backgroundColor: hexToRgba(c.color, 0.25), color: c.color }}
            >
              <CategoryIcon name={c.icon} size={11} />
            </span>
            {c.label}
          </MenuItem>
        ))}
      </div>
    </>
  );
}

function MenuItem({
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
      className="flex w-full items-center gap-2 px-2.5 py-1.5 text-left text-[12px] text-slate-700 hover:bg-slate-50"
    >
      {children}
      {active && <Check size={13} className="ml-auto text-accent-600" />}
    </button>
  );
}

export function CategoryManager({
  categories,
  onClose,
  onCreate,
  onRename,
  onRecolor,
  onDelete,
}: {
  categories: Category[];
  onClose: () => void;
  onCreate: (label: string, color: string, icon: string) => void;
  onRename: (id: string, label: string) => void;
  onRecolor: (id: string, color: string) => void;
  onDelete: (id: string) => void;
}) {
  const [label, setLabel] = useState('');
  const [color, setColor] = useState(PRESET_COLORS[1].value);
  const [icon, setIcon] = useState(CUSTOM_ICON_CHOICES[0]);
  const [editingColor, setEditingColor] = useState<string | null>(null);

  const create = () => {
    const trimmed = label.trim();
    if (!trimmed) return;
    onCreate(trimmed, color, icon);
    setLabel('');
  };

  return (
    <div
      style={{ position: 'fixed', inset: 0, zIndex: 60 }}
      className="flex items-start justify-center bg-slate-900/30 p-4 pt-10 backdrop-blur-sm"
      onMouseDown={onClose}
    >
      <div
        className="flex max-h-[85vh] w-[360px] max-w-full animate-scale-in flex-col overflow-hidden rounded-2xl bg-white shadow-2xl ring-1 ring-black/5"
        onMouseDown={(e) => e.stopPropagation()}
      >
        <div className="flex items-center gap-2 border-b border-slate-100 px-4 py-3">
          <Tag size={16} strokeWidth={1.75} className="text-accent-600" />
          <h3 className="flex-1 text-[14px] font-semibold text-slate-900">Категории</h3>
          <IconButton onClick={onClose} aria-label="Закрыть">
            <X size={16} strokeWidth={1.75} />
          </IconButton>
        </div>

        <div className="flex-1 overflow-y-auto p-4">
          <ul className="space-y-1.5">
            {categories.map((c) => (
              <li key={c.id} className="rounded-lg border border-slate-200 px-2.5 py-2">
                <div className="flex items-center gap-2">
                  <button
                    onClick={() => setEditingColor(editingColor === c.id ? null : c.id)}
                    title="Изменить цвет"
                    className="flex h-6 w-6 shrink-0 items-center justify-center rounded-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-400"
                    style={{ backgroundColor: hexToRgba(c.color, 0.25), color: c.color }}
                  >
                    <CategoryIcon name={c.icon} size={13} />
                  </button>
                  {c.builtin ? (
                    <span className="flex-1 text-[13px] font-medium text-slate-700">{c.label}</span>
                  ) : (
                    <input
                      value={c.label}
                      onChange={(e) => onRename(c.id, e.target.value)}
                      className="flex-1 rounded border border-transparent bg-transparent px-1 py-0.5 text-[13px] font-medium text-slate-800 outline-none hover:border-slate-200 focus:border-accent-400 focus:ring-2 focus:ring-accent-500/20"
                    />
                  )}
                  {c.builtin ? (
                    <span className="text-[10px] uppercase tracking-wide text-slate-300">сист.</span>
                  ) : (
                    <IconButton variant="danger" onClick={() => onDelete(c.id)} aria-label="Удалить категорию">
                      <Trash2 size={14} strokeWidth={1.75} />
                    </IconButton>
                  )}
                </div>
                {editingColor === c.id && (
                  <div className="mt-2 border-t border-slate-100 pt-2">
                    <ColorPicker value={c.color} onChange={(col) => onRecolor(c.id, col)} />
                  </div>
                )}
              </li>
            ))}
          </ul>
        </div>

        <div className="border-t border-slate-100 p-4">
          <p className="mb-2 text-[11px] font-semibold uppercase tracking-wide text-slate-400">
            Новая категория
          </p>
          <input
            value={label}
            onChange={(e) => setLabel(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && create()}
            placeholder="Название (напр. DevOps)"
            className="mb-2 w-full rounded-lg border border-slate-300 bg-white px-2.5 py-1.5 text-[13px] text-slate-800 placeholder:text-slate-400 outline-none focus:border-accent-400 focus:ring-2 focus:ring-accent-500/20"
          />
          <div className="mb-2 flex flex-wrap gap-1">
            {CUSTOM_ICON_CHOICES.map((name) => (
              <button
                key={name}
                onClick={() => setIcon(name)}
                aria-label={name}
                className={`flex h-7 w-7 items-center justify-center rounded-md border transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-400 ${
                  icon === name
                    ? 'border-accent-300 bg-accent-50 text-accent-700'
                    : 'border-slate-200 text-slate-500 hover:bg-slate-50'
                }`}
              >
                <CategoryIcon name={name} size={14} />
              </button>
            ))}
          </div>
          <div className="mb-3">
            <ColorPicker value={color} onChange={setColor} />
          </div>
          <Button variant="primary" size="md" onClick={create} className="w-full" disabled={!label.trim()}>
            <Plus size={15} strokeWidth={2} />
            Добавить категорию
          </Button>
        </div>
      </div>
    </div>
  );
}
