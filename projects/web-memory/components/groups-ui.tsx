import {
  useState,
  type Dispatch,
  type DragEvent,
  type ReactNode,
  type SetStateAction,
} from 'react';
import {
  ChevronDown,
  ChevronRight,
  ChevronsDownUp,
  ChevronsUpDown,
  Download,
  Folder,
  FolderOpen,
  FolderPlus,
  Inbox,
  Layers,
  Plus,
  Settings2,
  Sparkles,
  Trash2,
  X,
} from 'lucide-react';
import { PRESET_COLORS, type Group, type SmartFolder } from '@/lib/types';
import { ALL_GROUPS, SMART_PREFIX, childrenOf, groupAndDescendants } from '@/lib/groups';
import { ColorPicker } from '@/components/color-picker';
import { Button, IconButton } from '@/components/ui';

// Drag payload MIME types. Rows (memories/links) set DND_ITEM; folder nodes set DND_GROUP.
export const DND_ITEM = 'application/x-wm-item';
const DND_GROUP = 'application/x-wm-group';

/** Flatten the group tree into rows with a depth, in display order (depth-first). */
export function flattenGroups(groups: Group[]): { group: Group; depth: number }[] {
  const out: { group: Group; depth: number }[] = [];
  const walk = (parentId: string | null, depth: number) => {
    for (const g of childrenOf(groups, parentId)) {
      out.push({ group: g, depth });
      walk(g.id, depth + 1);
    }
  };
  walk(null, 0);
  return out;
}

// --- Tree navigation (side panel) ----------------------------------------------------------

/** A folder tree for filtering: "Все" / nested folders / smart folders, plus an inline
 *  "Новая папка" creator. Supports drag-and-drop (reparent folders, drop items into folders)
 *  and persisted collapse. `counts` is keyed by group id (rolled up), ALL_GROUPS, `smart:<id>`. */
export function GroupTree({
  groups,
  counts,
  smartFolders = [],
  selected,
  collapsed,
  onToggleCollapsed,
  onExpandAll,
  onCollapseAll,
  onSelect,
  onManage,
  onReparent,
  onDropItem,
  onDeleteSmart,
  onDeleteGroup,
  onCreateGroup,
}: {
  groups: Group[];
  counts: Map<string, number>;
  smartFolders?: SmartFolder[];
  selected: string;
  collapsed: Set<string>;
  onToggleCollapsed: (id: string) => void;
  onExpandAll: () => void;
  onCollapseAll: () => void;
  onSelect: (id: string) => void;
  onManage: () => void;
  onReparent: (groupId: string, newParentId: string | null) => void;
  onDropItem: (itemId: string, groupId: string | null) => void;
  onDeleteSmart?: (id: string) => void;
  onDeleteGroup: (id: string) => void;
  onCreateGroup: (name: string) => string;
}) {
  const [dropTarget, setDropTarget] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [newName, setNewName] = useState('');
  const roots = childrenOf(groups, null);

  const submitNew = () => {
    const n = newName.trim();
    if (!n) return;
    onCreateGroup(n);
    setNewName('');
    setCreating(false);
  };

  // Read a finished drop's payload (only available on drop, not dragover).
  const readDrop = (e: DragEvent): { kind: 'group' | 'item'; id: string } | null => {
    const g = e.dataTransfer.getData(DND_GROUP);
    if (g) return { kind: 'group', id: g };
    const it = e.dataTransfer.getData(DND_ITEM);
    if (it) return { kind: 'item', id: it };
    return null;
  };
  const has = (e: DragEvent, type: string) => e.dataTransfer.types.includes(type);

  return (
    <div className="rounded-xl border border-slate-200 bg-white/80 p-1.5">
      <div className="mb-0.5 flex items-center gap-0.5 px-1">
        <span className="text-[10.5px] font-semibold uppercase tracking-wide text-slate-400">
          Папки
        </span>
        <div className="ml-auto flex items-center gap-0.5">
          <HeaderButton onClick={onExpandAll} title="Развернуть все">
            <ChevronsUpDown size={13} strokeWidth={1.75} />
          </HeaderButton>
          <HeaderButton onClick={onCollapseAll} title="Свернуть все">
            <ChevronsDownUp size={13} strokeWidth={1.75} />
          </HeaderButton>
          <HeaderButton onClick={onManage} title="Управление группами">
            <Settings2 size={13} strokeWidth={1.75} />
          </HeaderButton>
        </div>
      </div>

      <TreeRow
        icon={<Layers size={14} strokeWidth={1.75} />}
        label="Все"
        count={counts.get(ALL_GROUPS)}
        active={selected === ALL_GROUPS}
        depth={0}
        onClick={() => onSelect(ALL_GROUPS)}
        // Dropping a folder here moves it to the root.
        dropActive={dropTarget === ALL_GROUPS}
        onDragOver={(e) => {
          if (has(e, DND_GROUP)) {
            e.preventDefault();
            setDropTarget(ALL_GROUPS);
          }
        }}
        onDragLeave={() => setDropTarget((t) => (t === ALL_GROUPS ? null : t))}
        onDrop={(e) => {
          const p = readDrop(e);
          setDropTarget(null);
          if (p?.kind === 'group') onReparent(p.id, null);
        }}
      />

      {roots.map((g) => (
        <GroupNode
          key={g.id}
          group={g}
          groups={groups}
          counts={counts}
          selected={selected}
          depth={0}
          collapsed={collapsed}
          dropTarget={dropTarget}
          setDropTarget={setDropTarget}
          onToggle={onToggleCollapsed}
          onSelect={onSelect}
          onReparent={onReparent}
          onDropItem={onDropItem}
          onDeleteGroup={onDeleteGroup}
          readDrop={readDrop}
          has={has}
        />
      ))}

      {smartFolders.length > 0 && (
        <div className="mt-1 border-t border-slate-100 pt-1">
          {smartFolders.map((sf) => {
            const id = SMART_PREFIX + sf.id;
            return (
              <TreeRow
                key={sf.id}
                icon={<Sparkles size={13} strokeWidth={1.75} />}
                iconColor="#a855f7"
                label={sf.name}
                count={counts.get(id)}
                active={selected === id}
                depth={0}
                onClick={() => onSelect(id)}
                onDelete={onDeleteSmart ? () => onDeleteSmart(sf.id) : undefined}
                deleteTitle="Удалить умную папку"
              />
            );
          })}
        </div>
      )}

      {creating ? (
        <div className="mt-1 flex items-center gap-1.5 px-1">
          <FolderPlus size={14} strokeWidth={1.75} className="shrink-0 text-accent-600" />
          <input
            autoFocus
            value={newName}
            onChange={(e) => setNewName(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') submitNew();
              else if (e.key === 'Escape') {
                setCreating(false);
                setNewName('');
              }
            }}
            onBlur={() => (newName.trim() ? submitNew() : setCreating(false))}
            placeholder="Название папки"
            className="w-full rounded-md border border-slate-300 bg-white px-2 py-1 text-[12px] text-slate-800 placeholder:text-slate-400 outline-none focus:border-accent-400 focus:ring-2 focus:ring-accent-500/20"
          />
        </div>
      ) : (
        <button
          onClick={() => setCreating(true)}
          className="mt-1 flex w-full items-center justify-center gap-1.5 rounded-lg border border-dashed border-slate-300 px-2.5 py-1.5 text-[12px] font-medium text-slate-500 transition-colors hover:border-accent-300 hover:bg-accent-50 hover:text-accent-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-400"
        >
          <FolderPlus size={14} strokeWidth={1.75} />
          Новая папка
        </button>
      )}
    </div>
  );
}

function HeaderButton({
  onClick,
  title,
  children,
}: {
  onClick: () => void;
  title: string;
  children: ReactNode;
}) {
  return (
    <button
      onClick={onClick}
      title={title}
      aria-label={title}
      className="inline-flex h-6 w-6 items-center justify-center rounded-md text-slate-400 transition-colors hover:bg-slate-100 hover:text-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-300"
    >
      {children}
    </button>
  );
}

function GroupNode({
  group,
  groups,
  counts,
  selected,
  depth,
  collapsed,
  dropTarget,
  setDropTarget,
  onToggle,
  onSelect,
  onReparent,
  onDropItem,
  onDeleteGroup,
  readDrop,
  has,
}: {
  group: Group;
  groups: Group[];
  counts: Map<string, number>;
  selected: string;
  depth: number;
  collapsed: Set<string>;
  dropTarget: string | null;
  setDropTarget: Dispatch<SetStateAction<string | null>>;
  onToggle: (id: string) => void;
  onSelect: (id: string) => void;
  onReparent: (groupId: string, newParentId: string | null) => void;
  onDropItem: (itemId: string, groupId: string | null) => void;
  onDeleteGroup: (id: string) => void;
  readDrop: (e: DragEvent) => { kind: 'group' | 'item'; id: string } | null;
  has: (e: DragEvent, type: string) => boolean;
}) {
  const kids = childrenOf(groups, group.id);
  const hasKids = kids.length > 0;
  const open = hasKids && !collapsed.has(group.id);

  return (
    <>
      <TreeRow
        icon={open ? <FolderOpen size={14} strokeWidth={1.75} /> : <Folder size={14} strokeWidth={1.75} />}
        iconColor={group.color}
        chevron={
          hasKids ? (
            open ? (
              <ChevronDown size={13} strokeWidth={2} />
            ) : (
              <ChevronRight size={13} strokeWidth={2} />
            )
          ) : undefined
        }
        onChevron={() => onToggle(group.id)}
        label={group.name}
        count={counts.get(group.id)}
        active={selected === group.id}
        depth={depth}
        onClick={() => onSelect(group.id)}
        onDelete={() => onDeleteGroup(group.id)}
        deleteTitle="Удалить папку (содержимое перейдёт в родителя)"
        draggable
        onDragStart={(e) => {
          e.dataTransfer.setData(DND_GROUP, group.id);
          e.dataTransfer.effectAllowed = 'move';
        }}
        dropActive={dropTarget === group.id}
        onDragOver={(e) => {
          if (has(e, DND_GROUP) || has(e, DND_ITEM)) {
            e.preventDefault();
            setDropTarget(group.id);
          }
        }}
        onDragLeave={() => setDropTarget((t) => (t === group.id ? null : t))}
        onDrop={(e) => {
          const p = readDrop(e);
          setDropTarget(null);
          if (!p) return;
          if (p.kind === 'group') onReparent(p.id, group.id);
          else onDropItem(p.id, group.id);
        }}
      />
      {open &&
        kids.map((k) => (
          <GroupNode
            key={k.id}
            group={k}
            groups={groups}
            counts={counts}
            selected={selected}
            depth={depth + 1}
            collapsed={collapsed}
            dropTarget={dropTarget}
            setDropTarget={setDropTarget}
            onToggle={onToggle}
            onSelect={onSelect}
            onReparent={onReparent}
            onDropItem={onDropItem}
            onDeleteGroup={onDeleteGroup}
            readDrop={readDrop}
            has={has}
          />
        ))}
    </>
  );
}

function TreeRow({
  icon,
  iconColor,
  chevron,
  onChevron,
  label,
  count,
  active,
  depth,
  onClick,
  onDelete,
  deleteTitle = 'Удалить',
  draggable,
  onDragStart,
  dropActive,
  onDragOver,
  onDragLeave,
  onDrop,
}: {
  icon: ReactNode;
  iconColor?: string;
  chevron?: ReactNode;
  onChevron?: () => void;
  label: string;
  count?: number;
  active: boolean;
  depth: number;
  onClick: () => void;
  onDelete?: () => void;
  deleteTitle?: string;
  draggable?: boolean;
  onDragStart?: (e: DragEvent) => void;
  dropActive?: boolean;
  onDragOver?: (e: DragEvent) => void;
  onDragLeave?: (e: DragEvent) => void;
  onDrop?: (e: DragEvent) => void;
}) {
  return (
    <div
      draggable={draggable}
      onDragStart={onDragStart}
      onDragOver={onDragOver}
      onDragLeave={onDragLeave}
      onDrop={onDrop}
      className={`group/row flex items-center gap-0.5 rounded-lg pr-1.5 transition-colors ${
        dropActive
          ? 'bg-accent-100 ring-1 ring-accent-400'
          : active
            ? 'bg-accent-50 text-accent-700'
            : 'text-slate-600 hover:bg-slate-50'
      }`}
      style={{ paddingLeft: 4 + depth * 14 }}
    >
      {chevron ? (
        <button
          onClick={onChevron}
          aria-label="Свернуть/развернуть"
          className="flex h-5 w-5 shrink-0 items-center justify-center rounded text-slate-400 hover:text-slate-700"
        >
          {chevron}
        </button>
      ) : (
        <span className="h-5 w-5 shrink-0" />
      )}
      <button onClick={onClick} className="flex min-w-0 flex-1 items-center gap-1.5 py-1.5 text-left">
        <span className="shrink-0" style={iconColor ? { color: iconColor } : undefined}>
          {icon}
        </span>
        <span className="truncate text-[12.5px] font-medium">{label}</span>
      </button>
      {count != null && count > 0 && (
        <span
          className={`shrink-0 rounded-full px-1.5 text-[10.5px] font-semibold tabular-nums ${
            active ? 'bg-accent-100 text-accent-700' : 'bg-slate-100 text-slate-500'
          }`}
        >
          {count}
        </span>
      )}
      {onDelete && (
        <button
          onClick={onDelete}
          title={deleteTitle}
          aria-label={deleteTitle}
          className="hidden h-5 w-5 shrink-0 items-center justify-center rounded text-slate-400 hover:text-red-600 group-hover/row:flex"
        >
          <X size={12} strokeWidth={2} />
        </button>
      )}
    </div>
  );
}

// --- Picker popover (assign a memory/link / choose the active group) -----------------------

/** Small popover tree to (re)assign a memory's group or pick the active capture group.
 *  When `onCreate` is given, an inline "new group" row creates a top-level folder. */
export function GroupPickerMenu({
  groups,
  value,
  onSelect,
  onCreate,
  onClose,
  align = 'right',
  vAlign = 'top',
}: {
  groups: Group[];
  value: string | null | undefined;
  onSelect: (groupId: string | null) => void;
  onCreate?: (name: string) => string;
  onClose: () => void;
  align?: 'left' | 'right';
  /** 'top' drops the menu below the trigger (default); 'bottom' drops it up (avoids clipping
   *  inside an overflow-hidden modal). */
  vAlign?: 'top' | 'bottom';
}) {
  const [creating, setCreating] = useState(false);
  const [name, setName] = useState('');
  const flat = flattenGroups(groups);

  const submit = () => {
    const n = name.trim();
    if (!n || !onCreate) return;
    const id = onCreate(n);
    setCreating(false);
    setName('');
    onSelect(id);
  };

  return (
    <>
      <div className="fixed inset-0 z-10" onClick={onClose} />
      <div
        className={`absolute ${align === 'right' ? 'right-0' : 'left-0'} ${
          vAlign === 'bottom' ? 'bottom-full mb-1' : 'mt-1'
        } z-20 max-h-72 w-56 animate-scale-in overflow-y-auto rounded-xl border border-slate-200 bg-white py-1 shadow-xl ring-1 ring-black/5`}
      >
        <MenuItem active={!value} depth={0} onClick={() => onSelect(null)}>
          <Inbox size={12} className="text-slate-400" />
          Без группы
        </MenuItem>
        {flat.map(({ group, depth }) => (
          <MenuItem key={group.id} active={value === group.id} depth={depth} onClick={() => onSelect(group.id)}>
            <Folder size={12} style={group.color ? { color: group.color } : undefined} className="text-slate-400" />
            <span className="truncate">{group.name}</span>
          </MenuItem>
        ))}
        {onCreate &&
          (creating ? (
            <div className="px-2 py-1.5">
              <input
                autoFocus
                value={name}
                onChange={(e) => setName(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') submit();
                  else if (e.key === 'Escape') {
                    setCreating(false);
                    setName('');
                  }
                }}
                placeholder="Название группы"
                className="w-full rounded-lg border border-slate-300 bg-white px-2 py-1 text-[12px] text-slate-800 placeholder:text-slate-400 outline-none focus:border-accent-400 focus:ring-2 focus:ring-accent-500/20"
              />
            </div>
          ) : (
            <button
              onClick={() => setCreating(true)}
              className="mt-0.5 flex w-full items-center gap-1.5 border-t border-slate-100 px-2.5 py-1.5 text-left text-[12px] font-medium text-accent-700 hover:bg-slate-50"
            >
              <FolderPlus size={12} />
              Новая группа
            </button>
          ))}
      </div>
    </>
  );
}

function MenuItem({
  active,
  depth,
  onClick,
  children,
}: {
  active: boolean;
  depth: number;
  onClick: () => void;
  children: ReactNode;
}) {
  return (
    <button
      onClick={onClick}
      style={{ paddingLeft: 10 + depth * 14 }}
      className="flex w-full items-center gap-1.5 py-1.5 pr-2.5 text-left text-[12px] text-slate-700 hover:bg-slate-50"
    >
      {children}
      {active && <span className="ml-auto h-1.5 w-1.5 shrink-0 rounded-full bg-accent-500" />}
    </button>
  );
}

// --- Manager modal (create / rename / recolor / move / delete / export) --------------------

export function GroupManager({
  groups,
  onClose,
  onCreate,
  onRename,
  onRecolor,
  onReparent,
  onDelete,
  onExport,
}: {
  groups: Group[];
  onClose: () => void;
  onCreate: (name: string, parentId: string | null, color: string) => void;
  onRename: (id: string, name: string) => void;
  onRecolor: (id: string, color: string) => void;
  onReparent: (id: string, parentId: string | null) => void;
  onDelete: (id: string) => void;
  onExport: (id: string) => void;
}) {
  const [name, setName] = useState('');
  const [parentId, setParentId] = useState<string>('');
  const [color, setColor] = useState(PRESET_COLORS[1].value);
  const [editingColor, setEditingColor] = useState<string | null>(null);
  const flat = flattenGroups(groups);

  const create = () => {
    const trimmed = name.trim();
    if (!trimmed) return;
    onCreate(trimmed, parentId || null, color);
    setName('');
    setParentId('');
  };

  return (
    <div
      style={{ position: 'fixed', inset: 0, zIndex: 60 }}
      className="flex items-start justify-center bg-slate-900/30 p-4 pt-10 backdrop-blur-sm"
      onMouseDown={onClose}
    >
      <div
        className="flex max-h-[85vh] w-[380px] max-w-full animate-scale-in flex-col overflow-hidden rounded-2xl bg-white shadow-2xl ring-1 ring-black/5"
        onMouseDown={(e) => e.stopPropagation()}
      >
        <div className="flex items-center gap-2 border-b border-slate-100 px-4 py-3">
          <Folder size={16} strokeWidth={1.75} className="text-accent-600" />
          <h3 className="flex-1 text-[14px] font-semibold text-slate-900">Группы</h3>
          <IconButton onClick={onClose} aria-label="Закрыть">
            <X size={16} strokeWidth={1.75} />
          </IconButton>
        </div>

        <div className="flex-1 overflow-y-auto p-4">
          {flat.length === 0 ? (
            <p className="px-1 py-6 text-center text-[12px] text-slate-400">
              Групп пока нет. Создайте первую ниже.
            </p>
          ) : (
            <ul className="space-y-1.5">
              {flat.map(({ group, depth }) => {
                // Parents a group may move under: anything but itself and its own descendants.
                const banned = groupAndDescendants(groups, group.id);
                return (
                  <li
                    key={group.id}
                    className="rounded-lg border border-slate-200 px-2.5 py-2"
                    style={{ marginLeft: depth * 14 }}
                  >
                    <div className="flex items-center gap-2">
                      <button
                        onClick={() => setEditingColor(editingColor === group.id ? null : group.id)}
                        title="Изменить цвет"
                        className="flex h-6 w-6 shrink-0 items-center justify-center rounded-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-400"
                        style={{ color: group.color ?? '#94a3b8' }}
                      >
                        <Folder size={14} strokeWidth={1.75} />
                      </button>
                      <input
                        value={group.name}
                        onChange={(e) => onRename(group.id, e.target.value)}
                        className="min-w-0 flex-1 rounded border border-transparent bg-transparent px-1 py-0.5 text-[13px] font-medium text-slate-800 outline-none hover:border-slate-200 focus:border-accent-400 focus:ring-2 focus:ring-accent-500/20"
                      />
                      <IconButton
                        onClick={() => onExport(group.id)}
                        aria-label="Экспортировать группу"
                        title="Экспортировать группу в JSON"
                      >
                        <Download size={14} strokeWidth={1.75} />
                      </IconButton>
                      <IconButton
                        variant="danger"
                        onClick={() => onDelete(group.id)}
                        aria-label="Удалить группу"
                        title="Удалить (содержимое перейдёт в родителя)"
                      >
                        <Trash2 size={14} strokeWidth={1.75} />
                      </IconButton>
                    </div>
                    <div className="mt-1.5 flex items-center gap-1.5">
                      <span className="text-[11px] text-slate-400">В:</span>
                      <select
                        value={group.parentId ?? ''}
                        onChange={(e) => onReparent(group.id, e.target.value || null)}
                        className="min-w-0 flex-1 rounded-md border border-slate-200 bg-white px-1.5 py-1 text-[12px] text-slate-700 outline-none focus:border-accent-400"
                      >
                        <option value="">— корень —</option>
                        {flat
                          .filter(({ group: g }) => !banned.has(g.id))
                          .map(({ group: g, depth: d }) => (
                            <option key={g.id} value={g.id}>
                              {' '.repeat(d * 2) + g.name}
                            </option>
                          ))}
                      </select>
                    </div>
                    {editingColor === group.id && (
                      <div className="mt-2 border-t border-slate-100 pt-2">
                        <ColorPicker
                          value={group.color ?? PRESET_COLORS[7].value}
                          onChange={(col) => onRecolor(group.id, col)}
                        />
                      </div>
                    )}
                  </li>
                );
              })}
            </ul>
          )}
        </div>

        <div className="border-t border-slate-100 p-4">
          <p className="mb-2 text-[11px] font-semibold uppercase tracking-wide text-slate-400">
            Новая группа
          </p>
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && create()}
            placeholder="Название (напр. Проект X)"
            className="mb-2 w-full rounded-lg border border-slate-300 bg-white px-2.5 py-1.5 text-[13px] text-slate-800 placeholder:text-slate-400 outline-none focus:border-accent-400 focus:ring-2 focus:ring-accent-500/20"
          />
          <div className="mb-2 flex items-center gap-1.5">
            <span className="text-[11px] text-slate-400">В:</span>
            <select
              value={parentId}
              onChange={(e) => setParentId(e.target.value)}
              className="min-w-0 flex-1 rounded-md border border-slate-200 bg-white px-1.5 py-1 text-[12px] text-slate-700 outline-none focus:border-accent-400"
            >
              <option value="">— корень —</option>
              {flat.map(({ group: g, depth: d }) => (
                <option key={g.id} value={g.id}>
                  {' '.repeat(d * 2) + g.name}
                </option>
              ))}
            </select>
          </div>
          <div className="mb-3">
            <ColorPicker value={color} onChange={setColor} />
          </div>
          <Button variant="primary" size="md" onClick={create} className="w-full" disabled={!name.trim()}>
            <Plus size={15} strokeWidth={2} />
            Добавить группу
          </Button>
        </div>
      </div>
    </div>
  );
}
