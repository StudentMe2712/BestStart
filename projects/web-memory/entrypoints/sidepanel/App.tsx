import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type CSSProperties,
  type KeyboardEvent,
  type ReactNode,
} from 'react';
import {
  ArrowUpRight,
  Brain,
  Check,
  CheckSquare,
  Download,
  FileText,
  Folder,
  Highlighter,
  ListChecks,
  Sparkles,
  Square,
  Inbox,
  Library,
  Link2,
  Pencil,
  Pin,
  Plus,
  Search,
  Settings,
  Star,
  StickyNote,
  Tag,
  Trash2,
  TriangleAlert,
  Upload,
  Wallpaper,
  X,
  type LucideIcon,
} from 'lucide-react';
import {
  getAnchorStatus,
  getPageMeta,
  queryTab,
  sendBg,
  sendTab,
  type AnchorStatusBroadcast,
} from '@/lib/messages';
import {
  DEFAULT_HIGHLIGHT_COLOR,
  type Category,
  type Group,
  type Link,
  type Memory,
  type NewLink,
  type SmartFolder,
} from '@/lib/types';
import { normalizeUrl, prettyUrl } from '@/lib/url';
import { buildIndex, buildLinkIndex } from '@/lib/search';
import { ensurePersistentStorage } from '@/lib/persist';
import { hexToRgba } from '@/lib/color';
import { loadCategories, resolveCategory, saveCategories, subscribeCategories } from '@/lib/categories';
import {
  ALL_GROUPS,
  SMART_PREFIX,
  UNGROUPED,
  canReparent,
  groupAndDescendants,
  loadCollapsed,
  loadGroups,
  loadSmartFolders,
  pathOf,
  removeGroup,
  saveCollapsed,
  saveGroups,
  saveSmartFolders,
  subscribeGroups,
  subscribeSmartFolders,
} from '@/lib/groups';
import {
  DEFAULT_BACKGROUND,
  loadBackground,
  presetCss,
  saveBackground,
  type BackgroundConfig,
} from '@/lib/background';
import {
  DEFAULT_SETTINGS,
  loadSettings,
  saveSettings,
  subscribeSettings,
  type WmSettings,
} from '@/lib/settings';
import { Button, IconButton } from '@/components/ui';
import { CategoryIcon } from '@/components/category-icon';
import { LinkCard, TagInput, TypeBadge, domainOf } from '@/components/link-ui';
import { BackgroundPicker } from '@/components/background-picker';
import { SettingsPanel } from '@/components/settings-panel';
import {
  CategoryAssignMenu,
  CategoryFilterBar,
  CategoryManager,
  EMPTY_FILTERS,
  UNCATEGORIZED,
  filtersActive,
  type MemoryFilters,
} from '@/components/categories-ui';
import { DND_ITEM, GroupManager, GroupPickerMenu, GroupTree } from '@/components/groups-ui';

type Notice = { text: string; kind: 'ok' | 'err' };

/** Light validation for imported records (older exports may omit some fields). */
function isMemory(x: unknown): x is Memory {
  if (!x || typeof x !== 'object') return false;
  const m = x as Record<string, unknown>;
  return (
    typeof m.id === 'string' &&
    typeof m.href === 'string' &&
    typeof m.text === 'string' &&
    (m.kind === 'highlight' || m.kind === 'note') &&
    typeof m.anchor === 'object' &&
    m.anchor !== null
  );
}

/** Hostname of a URL (empty string if unparseable), for the "disable on this site" shortcut. */
function hostOf(url: string): string {
  try {
    return new URL(url).hostname;
  } catch {
    return '';
  }
}

/** Light validation for imported links. */
function isLink(x: unknown): x is Link {
  if (!x || typeof x !== 'object') return false;
  const l = x as Record<string, unknown>;
  return typeof l.id === 'string' && typeof l.url === 'string';
}

/** Light validation for imported groups (folders). */
function isGroup(x: unknown): x is Group {
  if (!x || typeof x !== 'object') return false;
  const g = x as Record<string, unknown>;
  return (
    typeof g.id === 'string' &&
    typeof g.name === 'string' &&
    (g.parentId === null || typeof g.parentId === 'string' || g.parentId === undefined)
  );
}

/** Does a memory match a smart folder's saved filter? (kind + important + categories + text). */
function matchesSmart(
  m: Memory,
  sf: SmartFolder,
  categoryById: Map<string, Category>,
): boolean {
  if (sf.kind !== 'all' && m.kind !== sf.kind) return false;
  if (sf.importantOnly && !m.important) return false;
  if (sf.categoryIds.length) {
    const eff = m.categoryId && categoryById.has(m.categoryId) ? m.categoryId : UNCATEGORIZED;
    if (!sf.categoryIds.includes(eff)) return false;
  }
  if (sf.query) {
    const hay = `${m.text} ${m.note} ${m.title} ${m.url}`.toLowerCase();
    if (!hay.includes(sf.query.toLowerCase())) return false;
  }
  return true;
}

/** Short error text for notices (so failures are diagnosable, not silent). */
function errText(err: unknown): string {
  const m = err instanceof Error ? err.message : String(err);
  return m.length > 90 ? m.slice(0, 90) + '…' : m || 'неизвестная ошибка';
}

interface ActiveTab {
  id: number;
  url: string;
  title: string;
}

type Panel = 'page' | 'all' | 'links';

export function App() {
  const [memories, setMemories] = useState<Memory[]>([]);
  const [links, setLinks] = useState<Link[]>([]);
  const [activeTab, setActiveTab] = useState<ActiveTab | null>(null);
  const [panel, setPanel] = useState<Panel>('page');
  const [query, setQuery] = useState('');
  const [notice, setNotice] = useState<Notice | null>(null);
  const [unlocated, setUnlocated] = useState<Set<string>>(new Set());
  const [categories, setCategories] = useState<Category[]>([]);
  const [filters, setFilters] = useState<MemoryFilters>(EMPTY_FILTERS);
  const [manageOpen, setManageOpen] = useState(false);
  const [groups, setGroups] = useState<Group[]>([]);
  const [selectedGroup, setSelectedGroup] = useState<string>(ALL_GROUPS);
  const [groupManageOpen, setGroupManageOpen] = useState(false);
  const [smartFolders, setSmartFolders] = useState<SmartFolder[]>([]);
  const [collapsed, setCollapsed] = useState<Set<string>>(new Set());
  const [selectMode, setSelectMode] = useState(false);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [selectedLinkGroup, setSelectedLinkGroup] = useState<string>(ALL_GROUPS);
  const [linkTagFilter, setLinkTagFilter] = useState<string | null>(null);
  const [background, setBackground] = useState<BackgroundConfig>(DEFAULT_BACKGROUND);
  const [bgOpen, setBgOpen] = useState(false);
  const [settings, setSettings] = useState<WmSettings>(DEFAULT_SETTINGS);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const fileInput = useRef<HTMLInputElement>(null);
  const noticeTimer = useRef<number | undefined>(undefined);

  const notify = useCallback((text: string, kind: Notice['kind'] = 'ok') => {
    setNotice({ text, kind });
    window.clearTimeout(noticeTimer.current);
    noticeTimer.current = window.setTimeout(() => setNotice(null), 3500);
  }, []);

  const refresh = useCallback(async () => {
    const [mem, lnk] = await Promise.all([
      sendBg({ type: 'GET_ALL_MEMORIES' }),
      sendBg({ type: 'GET_ALL_LINKS' }),
    ]);
    setMemories(mem);
    setLinks(lnk);
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  // Ask for persistent storage once (no-op if already granted / already requested).
  useEffect(() => {
    void ensurePersistentStorage();
  }, []);

  // Load the saved background once.
  useEffect(() => {
    void loadBackground().then(setBackground);
  }, []);

  const changeBackground = useCallback((cfg: BackgroundConfig) => {
    setBackground(cfg);
    void saveBackground(cfg);
  }, []);

  // Load behavior settings once + live-update if changed from another context.
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

  const changeSettings = useCallback((next: WmSettings) => {
    setSettings(next);
    void saveSettings(next);
  }, []);

  // Live-refresh when data is saved/edited/deleted anywhere (e.g. from the page toolbar),
  // so "Моя память" stays current without reopening the panel.
  useEffect(() => {
    const onChanged = (raw: unknown) => {
      if ((raw as { type?: string })?.type === 'DATA_CHANGED') void refresh();
    };
    browser.runtime.onMessage.addListener(onChanged);
    return () => browser.runtime.onMessage.removeListener(onChanged);
  }, [refresh]);

  // Category definitions (stored in storage.local; shared with the content script).
  useEffect(() => {
    let active = true;
    void loadCategories().then((c) => {
      if (active) setCategories(c);
    });
    return subscribeCategories(setCategories);
  }, []);

  // Group (folder) definitions — also in storage.local, shared with the content script.
  useEffect(() => {
    let active = true;
    void loadGroups().then((g) => {
      if (active) setGroups(g);
    });
    return subscribeGroups(setGroups);
  }, []);

  // Smart folders (saved filters) + persisted tree expansion.
  useEffect(() => {
    let active = true;
    void loadSmartFolders().then((s) => {
      if (active) setSmartFolders(s);
    });
    void loadCollapsed().then((ids) => {
      if (active) setCollapsed(new Set(ids));
    });
    return subscribeSmartFolders(setSmartFolders);
  }, []);

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

  // Track which of the active page's memories the content script couldn't re-locate.
  // Pull once on tab change; the content script also pushes updates as it retries.
  useEffect(() => {
    if (!activeTab) {
      setUnlocated(new Set());
      return;
    }
    let active = true;
    void getAnchorStatus(activeTab.id).then((status) => {
      if (active && status) setUnlocated(new Set(status.unlocated));
    });
    const onPush = (raw: unknown) => {
      const msg = raw as Partial<AnchorStatusBroadcast>;
      if (msg?.type === 'ANCHOR_STATUS' && msg.url === activeTab.url) {
        setUnlocated(new Set(msg.unlocated ?? []));
      }
    };
    browser.runtime.onMessage.addListener(onPush);
    return () => {
      active = false;
      browser.runtime.onMessage.removeListener(onPush);
    };
  }, [activeTab]);

  const index = useMemo(() => buildIndex(memories), [memories]);
  const byId = useMemo(() => new Map(memories.map((m) => [m.id, m])), [memories]);
  const linkIndex = useMemo(() => buildLinkIndex(links), [links]);
  const linkById = useMemo(() => new Map(links.map((l) => [l.id, l])), [links]);
  const categoryById = useMemo(() => new Map(categories.map((c) => [c.id, c])), [categories]);
  const groupById = useMemo(() => new Map(groups.map((g) => [g.id, g])), [groups]);

  // group id → {self + all descendants}, computed once (memoized DFS) and reused by counts and
  // filters so we never call the O(N²) groupAndDescendants per group on every render.
  const groupDescendants = useMemo(() => {
    const children = new Map<string | null, string[]>();
    for (const g of groups) {
      const p = g.parentId ?? null;
      const arr = children.get(p);
      if (arr) arr.push(g.id);
      else children.set(p, [g.id]);
    }
    const map = new Map<string, Set<string>>();
    const collect = (id: string): Set<string> => {
      const cached = map.get(id);
      if (cached) return cached;
      const set = new Set<string>([id]);
      for (const c of children.get(id) ?? []) for (const d of collect(c)) set.add(d);
      map.set(id, set);
      return set;
    };
    for (const g of groups) collect(g.id);
    return map;
  }, [groups]);
  const descendantsOf = useCallback(
    (id: string): Set<string> => groupDescendants.get(id) ?? new Set([id]),
    [groupDescendants],
  );

  // Counts for the folder tree: per-group totals rolled up to include descendants, plus the
  // "Без группы" and "Все" pseudo-nodes.
  const groupCounts = useMemo(() => {
    const direct = new Map<string, number>();
    let ungrouped = 0;
    for (const m of memories) {
      const gid = m.groupId && groupById.has(m.groupId) ? m.groupId : null;
      if (gid) direct.set(gid, (direct.get(gid) ?? 0) + 1);
      else ungrouped++;
    }
    const rolled = new Map<string, number>();
    for (const g of groups) {
      let sum = 0;
      for (const id of descendantsOf(g.id)) sum += direct.get(id) ?? 0;
      rolled.set(g.id, sum);
    }
    rolled.set(UNGROUPED, ungrouped);
    rolled.set(ALL_GROUPS, memories.length);
    for (const sf of smartFolders) {
      let n = 0;
      for (const m of memories) if (matchesSmart(m, sf, categoryById)) n++;
      rolled.set(SMART_PREFIX + sf.id, n);
    }
    return rolled;
  }, [memories, groups, groupById, smartFolders, categoryById, descendantsOf]);

  // Counts for the links folder tree (links carry the same groupId, no descendants roll-down
  // needed beyond the standard descendant inclusion).
  const linkGroupCounts = useMemo(() => {
    const direct = new Map<string, number>();
    let ungrouped = 0;
    for (const l of links) {
      const gid = l.groupId && groupById.has(l.groupId) ? l.groupId : null;
      if (gid) direct.set(gid, (direct.get(gid) ?? 0) + 1);
      else ungrouped++;
    }
    const rolled = new Map<string, number>();
    for (const g of groups) {
      let sum = 0;
      for (const id of descendantsOf(g.id)) sum += direct.get(id) ?? 0;
      rolled.set(g.id, sum);
    }
    rolled.set(UNGROUPED, ungrouped);
    rolled.set(ALL_GROUPS, links.length);
    return rolled;
  }, [links, groups, groupById, descendantsOf]);

  const pageMemories = useMemo(
    () => (activeTab ? memories.filter((m) => m.url === activeTab.url) : []),
    [memories, activeTab],
  );

  const searching = query.trim().length > 0;

  // Global search spans memories + links; results are badged and shown on their own surface,
  // so links never appear inside "My Memory".
  const searchHits = useMemo(() => {
    if (!searching) return { memHits: [] as Memory[], linkHits: [] as Link[] };
    const memHits = index.search(query).map((id) => byId.get(id)).filter((m): m is Memory => !!m);
    const linkHits = linkIndex.search(query).map((id) => linkById.get(id)).filter((l): l is Link => !!l);
    return { memHits, linkHits };
  }, [searching, query, index, byId, linkIndex, linkById]);

  // The smart folder selected in the tree, if any (overrides the group scope below).
  const activeSmart = useMemo(
    () =>
      selectedGroup.startsWith(SMART_PREFIX)
        ? smartFolders.find((s) => SMART_PREFIX + s.id === selectedGroup)
        : undefined,
    [selectedGroup, smartFolders],
  );

  // "Моя память" list = memories narrowed by the active filters + selected folder/smart (AND).
  const memoryFiltered = useMemo(() => {
    const groupScope: Set<string> | 'ungrouped' | null =
      activeSmart || selectedGroup === ALL_GROUPS
        ? null
        : selectedGroup === UNGROUPED
          ? 'ungrouped'
          : descendantsOf(selectedGroup);
    return memories.filter((m) => {
      if (filters.kind !== 'all' && m.kind !== filters.kind) return false;
      if (filters.importantOnly && !m.important) return false;
      if (filters.categoryIds.size > 0) {
        const effective = m.categoryId && categoryById.has(m.categoryId) ? m.categoryId : UNCATEGORIZED;
        if (!filters.categoryIds.has(effective)) return false;
      }
      if (activeSmart) {
        if (!matchesSmart(m, activeSmart, categoryById)) return false;
      } else if (groupScope) {
        const gid = m.groupId && groupById.has(m.groupId) ? m.groupId : null;
        if (groupScope === 'ungrouped') {
          if (gid) return false;
        } else if (!gid || !groupScope.has(gid)) {
          return false;
        }
      }
      return true;
    });
  }, [memories, filters, categoryById, selectedGroup, groups, groupById, activeSmart, descendantsOf]);

  // Links narrowed by the selected links folder (+ the existing tag filter, applied in panel).
  const linksByGroup = useMemo(() => {
    const scope: Set<string> | 'ungrouped' | null =
      selectedLinkGroup === ALL_GROUPS
        ? null
        : selectedLinkGroup === UNGROUPED
          ? 'ungrouped'
          : descendantsOf(selectedLinkGroup);
    if (!scope) return links;
    return links.filter((l) => {
      const gid = l.groupId && groupById.has(l.groupId) ? l.groupId : null;
      if (scope === 'ungrouped') return !gid;
      return !!gid && scope.has(gid);
    });
  }, [links, selectedLinkGroup, groups, groupById, descendantsOf]);

  const tagSuggestions = useMemo(
    () => [...new Set(links.flatMap((l) => l.tags))].sort(),
    [links],
  );

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
    async (mem: Memory) => {
      // Already on the right page → scroll + flash in place. If the passage can't be located
      // on the live page, fall back to reopening it fresh (a new load may surface lazy content).
      if (activeTab && mem.url === activeTab.url) {
        const res = await queryTab<{ ok: boolean }>(activeTab.id, {
          type: 'SCROLL_TO_MEMORY',
          id: mem.id,
        });
        if (res?.ok) return;
      }
      await sendBg({ type: 'OPEN_AND_SCROLL', href: mem.href, id: mem.id });
    },
    [activeTab],
  );

  const remove = useCallback(
    async (mem: Memory) => {
      try {
        await sendBg({ type: 'DELETE_MEMORY', id: mem.id });
        if (activeTab && mem.url === activeTab.url) {
          void sendTab(activeTab.id, { type: 'REMOVE_MEMORY', id: mem.id });
        }
        await refresh();
      } catch (err) {
        console.error('[web-memory] delete failed', err);
        notify('Не удалось удалить', 'err');
      }
    },
    [activeTab, refresh, notify],
  );

  const saveNote = useCallback(
    async (mem: Memory, note: string) => {
      try {
        const updated = await sendBg({ type: 'UPDATE_MEMORY', id: mem.id, patch: { note } });
        await refresh();
        if (updated) relayReapply(updated);
        notify('Заметка сохранена', 'ok');
      } catch (err) {
        console.error('[web-memory] save failed', err);
        notify('Не удалось сохранить: ' + errText(err), 'err');
      }
    },
    [refresh, relayReapply, notify],
  );

  const toggleImportant = useCallback(
    async (mem: Memory) => {
      try {
        const updated = await sendBg({
          type: 'UPDATE_MEMORY',
          id: mem.id,
          patch: { important: !mem.important },
        });
        await refresh();
        if (updated) relayReapply(updated);
      } catch (err) {
        console.error('[web-memory] save failed', err);
        notify('Не удалось сохранить', 'err');
      }
    },
    [refresh, relayReapply, notify],
  );

  const startElementNote = useCallback(() => {
    if (activeTab) void sendTab(activeTab.id, { type: 'START_ELEMENT_NOTE' });
  }, [activeTab]);

  // --- Categories ---------------------------------------------------------------------
  const setMemoryCategory = useCallback(
    async (mem: Memory, categoryId: string | null) => {
      const color = categoryId
        ? (categoryById.get(categoryId)?.color ?? DEFAULT_HIGHLIGHT_COLOR)
        : DEFAULT_HIGHLIGHT_COLOR;
      try {
        const updated = await sendBg({
          type: 'UPDATE_MEMORY',
          id: mem.id,
          patch: { categoryId, color },
        });
        await refresh();
        if (updated) relayReapply(updated);
      } catch (err) {
        console.error('[web-memory] set category failed', err);
        notify('Не удалось изменить категорию', 'err');
      }
    },
    [categoryById, refresh, relayReapply, notify],
  );

  const createCategory = useCallback(
    (label: string, color: string, icon: string) => {
      void saveCategories([
        ...categories,
        { id: crypto.randomUUID(), label, color, icon, builtin: false },
      ]);
    },
    [categories],
  );

  const renameCategory = useCallback(
    (id: string, label: string) => {
      void saveCategories(categories.map((c) => (c.id === id ? { ...c, label } : c)));
    },
    [categories],
  );

  const recolorCategory = useCallback(
    async (id: string, color: string) => {
      await saveCategories(categories.map((c) => (c.id === id ? { ...c, color } : c)));
      await sendBg({ type: 'RECOLOR_CATEGORY', categoryId: id, color });
      // Refresh the on-page highlights of the active tab to the new color.
      if (activeTab) void sendTab(activeTab.id, { type: 'REAPPLY' });
    },
    [categories, activeTab],
  );

  const deleteCategory = useCallback(
    async (id: string) => {
      await saveCategories(categories.filter((c) => c.id !== id));
      await sendBg({ type: 'CLEAR_CATEGORY', categoryId: id });
      setFilters((f) => {
        if (!f.categoryIds.has(id)) return f;
        const next = new Set(f.categoryIds);
        next.delete(id);
        return { ...f, categoryIds: next };
      });
      if (activeTab) void sendTab(activeTab.id, { type: 'REAPPLY' });
    },
    [categories, activeTab],
  );

  // --- Groups (folders) ---------------------------------------------------------------
  const setMemoryGroup = useCallback(
    async (mem: Memory, groupId: string | null) => {
      try {
        await sendBg({ type: 'UPDATE_MEMORY', id: mem.id, patch: { groupId } });
        await refresh();
      } catch (err) {
        console.error('[web-memory] set group failed', err);
        notify('Не удалось изменить группу', 'err');
      }
    },
    [refresh, notify],
  );

  const createGroup = useCallback(
    (name: string, parentId: string | null, color: string) => {
      const now = Date.now();
      void saveGroups([
        ...groups,
        { id: crypto.randomUUID(), name, parentId, color, createdAt: now, updatedAt: now },
      ]);
    },
    [groups],
  );

  // Inline "+ Новая группа" from a picker: create a top-level folder and return its id so the
  // caller can immediately select it.
  const createGroupReturningId = useCallback(
    (name: string): string => {
      const id = crypto.randomUUID();
      const now = Date.now();
      void saveGroups([...groups, { id, name, parentId: null, createdAt: now, updatedAt: now }]);
      return id;
    },
    [groups],
  );

  const renameGroup = useCallback(
    (id: string, name: string) => {
      void saveGroups(groups.map((g) => (g.id === id ? { ...g, name, updatedAt: Date.now() } : g)));
    },
    [groups],
  );

  const recolorGroup = useCallback(
    (id: string, color: string) => {
      void saveGroups(groups.map((g) => (g.id === id ? { ...g, color, updatedAt: Date.now() } : g)));
    },
    [groups],
  );

  const reparentGroup = useCallback(
    (id: string, parentId: string | null) => {
      if (!canReparent(groups, id, parentId)) return;
      void saveGroups(
        groups.map((g) => (g.id === id ? { ...g, parentId, updatedAt: Date.now() } : g)),
      );
    },
    [groups],
  );

  const deleteGroup = useCallback(
    async (id: string) => {
      const { groups: next, reparentTo } = removeGroup(groups, id);
      try {
        // Reassign content FIRST, then drop the folder from storage — so a failure between the
        // two can never leave memories/links pointing at a folder that no longer exists.
        await sendBg({ type: 'REASSIGN_GROUP', from: id, to: reparentTo });
        await saveGroups(next);
        setSelectedGroup((cur) => (cur === id ? (reparentTo ?? ALL_GROUPS) : cur));
        await refresh();
      } catch (err) {
        console.error('[web-memory] delete group failed', err);
        notify('Не удалось удалить группу', 'err');
      }
    },
    [groups, refresh, notify],
  );

  // --- Tree expansion (persisted) -----------------------------------------------------
  const toggleCollapsed = useCallback((id: string) => {
    setCollapsed((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      void saveCollapsed([...next]);
      return next;
    });
  }, []);

  const expandAll = useCallback(() => {
    setCollapsed(new Set());
    void saveCollapsed([]);
  }, []);

  const collapseAll = useCallback(() => {
    const all = new Set(groups.map((g) => g.id));
    setCollapsed(all);
    void saveCollapsed([...all]);
  }, [groups]);

  // --- Smart folders ------------------------------------------------------------------
  const createSmartFolder = useCallback(
    (name: string) => {
      const now = Date.now();
      const sf: SmartFolder = {
        id: crypto.randomUUID(),
        name,
        categoryIds: [...filters.categoryIds],
        importantOnly: filters.importantOnly,
        kind: filters.kind,
        query: query.trim() || undefined,
        createdAt: now,
        updatedAt: now,
      };
      void saveSmartFolders([...smartFolders, sf]);
    },
    [filters, query, smartFolders],
  );

  const deleteSmartFolder = useCallback(
    (id: string) => {
      void saveSmartFolders(smartFolders.filter((s) => s.id !== id));
      setSelectedGroup((cur) => (cur === SMART_PREFIX + id ? ALL_GROUPS : cur));
    },
    [smartFolders],
  );

  // --- Multi-select (bulk operations) -------------------------------------------------
  const toggleSelect = useCallback((id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }, []);

  const exitSelect = useCallback(() => {
    setSelectMode(false);
    setSelectedIds(new Set());
  }, []);

  const bulkSetGroup = useCallback(
    async (groupId: string | null) => {
      const ids = [...selectedIds];
      if (!ids.length) return;
      await sendBg({ type: 'BULK_UPDATE_MEMORIES', ids, patch: { groupId } });
      setSelectedIds(new Set());
      await refresh();
      notify(`Перемещено: ${ids.length}`);
    },
    [selectedIds, refresh, notify],
  );

  const bulkSetImportant = useCallback(
    async (important: boolean) => {
      const ids = [...selectedIds];
      if (!ids.length) return;
      await sendBg({ type: 'BULK_UPDATE_MEMORIES', ids, patch: { important } });
      setSelectedIds(new Set());
      await refresh();
      if (activeTab) void sendTab(activeTab.id, { type: 'REAPPLY' });
    },
    [selectedIds, refresh, activeTab],
  );

  const bulkDelete = useCallback(async () => {
    const ids = [...selectedIds];
    if (!ids.length) return;
    await sendBg({ type: 'BULK_DELETE_MEMORIES', ids });
    if (activeTab) for (const id of ids) void sendTab(activeTab.id, { type: 'REMOVE_MEMORY', id });
    exitSelect();
    await refresh();
    notify(`Удалено: ${ids.length}`);
  }, [selectedIds, activeTab, exitSelect, refresh, notify]);

  // --- Export a single group (its subtree of folders + memories + links) --------------
  const exportGroup = useCallback(
    async (id: string) => {
      const ids = groupAndDescendants(groups, id);
      const [allMem, allLinks] = await Promise.all([
        sendBg({ type: 'GET_ALL_MEMORIES' }),
        sendBg({ type: 'GET_ALL_LINKS' }),
      ]);
      const mem = allMem.filter((m) => m.groupId && ids.has(m.groupId));
      const lnk = allLinks.filter((l) => l.groupId && ids.has(l.groupId));
      const root = groups.find((g) => g.id === id);
      const payload = {
        format: 'web-memory',
        version: 3,
        exportedAt: new Date().toISOString(),
        groups: groups.filter((g) => ids.has(g.id)),
        memories: mem,
        links: lnk,
      };
      const blob = new Blob([JSON.stringify(payload, null, 2)], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      const slug = (root?.name || 'group').replace(/[^\p{L}\p{N}-]+/gu, '_').slice(0, 40);
      a.href = url;
      a.download = `web-memory-${slug}-${new Date().toISOString().slice(0, 10)}.json`;
      a.click();
      URL.revokeObjectURL(url);
      notify(`Экспортирована «${root?.name ?? '—'}»: ${mem.length} памяти, ${lnk.length} ссылок`);
    },
    [groups, notify],
  );

  // --- Links --------------------------------------------------------------------------
  const setLinkGroup = useCallback(
    async (l: Link, groupId: string | null) => {
      try {
        await sendBg({ type: 'UPDATE_LINK', id: l.id, patch: { groupId } });
        await refresh();
      } catch (err) {
        console.error('[web-memory] set link group failed', err);
        notify('Не удалось изменить группу ссылки', 'err');
      }
    },
    [refresh, notify],
  );

  const openLink = useCallback(
    (l: Link) => {
      const url = /^[a-z][a-z0-9+.-]*:\/\//i.test(l.url) ? l.url : `https://${l.url}`;
      browser.tabs.create({ url, active: true }).catch((err) => {
        console.error('[web-memory] open link failed', err);
        notify('Не удалось открыть ссылку', 'err');
      });
    },
    [notify],
  );

  const savePage = useCallback(async () => {
    try {
      const [tab] = await browser.tabs.query({ active: true, currentWindow: true });
      if (!tab?.url) {
        notify('Нет активной страницы', 'err');
        return;
      }
      const meta = tab.id != null ? await getPageMeta(tab.id) : null;
      // When viewing a real folder, "Сохранить страницу" lands it there.
      const groupId =
        selectedLinkGroup !== ALL_GROUPS && selectedLinkGroup !== UNGROUPED ? selectedLinkGroup : null;
      const link: NewLink = {
        url: tab.url,
        title: (meta?.title || tab.title || tab.url).trim(),
        favicon: tab.favIconUrl,
        description: meta?.description ?? '',
        tags: [],
        groupId,
      };
      const res = await sendBg({ type: 'SAVE_LINK', link });
      await refresh();
      notify(res.duplicate ? 'Эта страница уже сохранена' : 'Страница сохранена', res.duplicate ? 'err' : 'ok');
    } catch (err) {
      console.error('[web-memory] save page failed', err);
      notify('Не удалось сохранить ссылку', 'err');
    }
  }, [notify, refresh, selectedLinkGroup]);

  const saveManualLink = useCallback(
    async (rawUrl: string, title: string, tags: string[], groupId: string | null): Promise<boolean> => {
      let url: string;
      try {
        const withScheme = /^https?:\/\//i.test(rawUrl.trim()) ? rawUrl.trim() : `https://${rawUrl.trim()}`;
        url = new URL(withScheme).toString();
      } catch {
        notify('Неверный URL', 'err');
        return false;
      }
      try {
        const link: NewLink = { url, title: title.trim() || domainOf(url), description: '', tags, groupId };
        const res = await sendBg({ type: 'SAVE_LINK', link });
        await refresh();
        notify(res.duplicate ? 'Эта ссылка уже сохранена' : 'Ссылка сохранена', res.duplicate ? 'err' : 'ok');
        return !res.duplicate;
      } catch (err) {
        console.error('[web-memory] save link failed', err);
        notify('Не удалось сохранить ссылку', 'err');
        return false;
      }
    },
    [notify, refresh],
  );

  const updateLink = useCallback(
    async (l: Link, patch: { title: string; tags: string[] }) => {
      try {
        await sendBg({ type: 'UPDATE_LINK', id: l.id, patch });
        await refresh();
      } catch (err) {
        console.error('[web-memory] update link failed', err);
        notify('Не удалось обновить ссылку', 'err');
      }
    },
    [refresh, notify],
  );

  const removeLink = useCallback(
    async (l: Link) => {
      try {
        await sendBg({ type: 'DELETE_LINK', id: l.id });
        await refresh();
      } catch (err) {
        console.error('[web-memory] delete link failed', err);
        notify('Не удалось удалить ссылку', 'err');
      }
    },
    [refresh, notify],
  );

  // --- Export / Import ----------------------------------------------------------------
  const exportJson = useCallback(async () => {
    try {
      const [all, allLinks, allGroups] = await Promise.all([
        sendBg({ type: 'GET_ALL_MEMORIES' }),
        sendBg({ type: 'GET_ALL_LINKS' }),
        loadGroups(),
      ]);
      const payload = {
        format: 'web-memory',
        version: 3,
        exportedAt: new Date().toISOString(),
        memories: all,
        links: allLinks,
        groups: allGroups,
      };
      const blob = new Blob([JSON.stringify(payload, null, 2)], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `web-memory-${new Date().toISOString().slice(0, 10)}.json`;
      a.click();
      URL.revokeObjectURL(url);
      notify(`Экспортировано: ${all.length} памяти, ${allLinks.length} ссылок`, 'ok');
    } catch (err) {
      console.error('[web-memory] export failed', err);
      notify('Не удалось экспортировать', 'err');
    }
  }, [notify]);

  const importJson = useCallback(
    async (file: File) => {
      try {
        const data: unknown = JSON.parse(await file.text());
        // Back-compatible: v0 = bare memory array, v1 = { memories }, v2 = { memories, links }.
        const rawMem = Array.isArray(data) ? data : (data as { memories?: unknown }).memories;
        const rawLinks = Array.isArray(data) ? null : (data as { links?: unknown }).links;
        const rawGroups = Array.isArray(data) ? null : (data as { groups?: unknown }).groups;
        const validMem = Array.isArray(rawMem) ? rawMem.filter(isMemory) : [];
        const validLinks = Array.isArray(rawLinks) ? rawLinks.filter(isLink) : [];
        const validGroups = Array.isArray(rawGroups) ? rawGroups.filter(isGroup) : [];
        if (validMem.length === 0 && validLinks.length === 0 && validGroups.length === 0) {
          notify('В файле нет записей для импорта', 'err');
          return;
        }
        // Merge groups (config in storage.local), keeping existing ones; skip ids we already have.
        if (validGroups.length) {
          const have = new Set(groups.map((g) => g.id));
          const fresh = validGroups.filter((g) => !have.has(g.id));
          if (fresh.length) {
            const merged = [...groups, ...fresh];
            const ids = new Set(merged.map((g) => g.id));
            // Normalize parentId to null when undefined or dangling, so a folder never becomes
            // invisible in the tree (childrenOf only walks down from the root).
            const safe = merged.map((g) => {
              const parentId = g.parentId && ids.has(g.parentId) ? g.parentId : null;
              return parentId === (g.parentId ?? null) ? g : { ...g, parentId };
            });
            await saveGroups(safe);
          }
        }
        const mem = validMem.length
          ? await sendBg({ type: 'IMPORT_MEMORIES', memories: validMem })
          : { added: 0, skipped: 0 };
        const lnk = validLinks.length
          ? await sendBg({ type: 'IMPORT_LINKS', links: validLinks })
          : { added: 0, skipped: 0 };
        await refresh();
        notify(
          `Импортировано: ${mem.added + lnk.added}, пропущено: ${mem.skipped + lnk.skipped}`,
          'ok',
        );
      } catch (err) {
        console.error('[web-memory] import failed', err);
        notify('Не удалось импортировать (повреждённый JSON?)', 'err');
      }
    },
    [notify, refresh, groups],
  );

  const bgStyle: CSSProperties =
    background.kind === 'image'
      ? {
          backgroundImage: `url(${background.value})`,
          backgroundSize: 'cover',
          backgroundPosition: 'center',
          backgroundRepeat: 'no-repeat',
        }
      : background.kind === 'preset'
        ? { backgroundImage: presetCss(background.value) }
        : {};

  return (
    <div
      className={`flex h-full flex-col font-sans text-slate-800 ${
        background.kind === 'none' ? 'bg-slate-50' : ''
      }`}
      style={bgStyle}
    >
      <header
        className={`border-b px-4 pb-3 pt-3.5 ${
          background.kind === 'none'
            ? 'border-slate-200 bg-white'
            : 'border-white/30 bg-white/35 backdrop-blur-md'
        }`}
      >
        <div className="flex items-center gap-2.5">
          <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-accent-600 text-white shadow-sm">
            <Brain size={17} strokeWidth={2} />
          </span>
          <div className="min-w-0 leading-tight">
            <h1 className="text-[15px] font-semibold tracking-tight text-slate-900">Web Memory</h1>
            <p className="text-[11px] tabular-nums text-slate-400">
              {memories.length} {plural(memories.length, 'запись', 'записи', 'записей')}
            </p>
          </div>
          <div className="ml-auto flex items-center gap-0.5">
            <IconButton
              onClick={() => setSettingsOpen(true)}
              title="Настройки"
              aria-label="Настройки"
              className="text-slate-500"
            >
              <Settings size={16} strokeWidth={1.75} />
            </IconButton>
            <IconButton
              onClick={() => setBgOpen(true)}
              title="Фон панели"
              aria-label="Фон"
              className="text-slate-500"
            >
              <Wallpaper size={16} strokeWidth={1.75} />
            </IconButton>
            <IconButton
              onClick={exportJson}
              title="Экспортировать всё в JSON"
              aria-label="Экспорт"
              className="text-slate-500"
            >
              <Download size={16} strokeWidth={1.75} />
            </IconButton>
            <IconButton
              onClick={() => fileInput.current?.click()}
              title="Импортировать из JSON"
              aria-label="Импорт"
              className="text-slate-500"
            >
              <Upload size={16} strokeWidth={1.75} />
            </IconButton>
          </div>
          <input
            ref={fileInput}
            type="file"
            accept="application/json,.json"
            className="hidden"
            onChange={(e) => {
              const file = e.target.files?.[0];
              if (file) void importJson(file);
              e.target.value = '';
            }}
          />
        </div>
        <div className="relative mt-3">
          <Search
            size={15}
            strokeWidth={1.75}
            className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-400"
          />
          <input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Поиск по фрагментам, заметкам, ссылкам…"
            className="w-full rounded-xl border border-slate-200 bg-white py-2 pl-9 pr-9 text-[13px] text-slate-800 placeholder:text-slate-400 outline-none transition-all duration-150 focus:border-accent-400 focus:ring-2 focus:ring-accent-500/20"
          />
          {query && (
            <IconButton
              onClick={() => setQuery('')}
              title="Очистить"
              aria-label="Очистить поиск"
              className="absolute right-1.5 top-1/2 -translate-y-1/2"
            >
              <X size={15} strokeWidth={1.75} />
            </IconButton>
          )}
        </div>

        <nav
          className={`mt-3 grid grid-cols-3 gap-1 rounded-xl p-1 ${
            background.kind === 'none' ? 'bg-slate-100' : 'bg-white/40 backdrop-blur-sm'
          }`}
        >
          <TabButton
            active={!searching && panel === 'page'}
            onClick={() => {
              setQuery('');
              setPanel('page');
            }}
            icon={FileText}
            count={pageMemories.length}
          >
            Страница
          </TabButton>
          <TabButton
            active={!searching && panel === 'all'}
            onClick={() => {
              setQuery('');
              setPanel('all');
            }}
            icon={Library}
            count={memories.length}
          >
            Память
          </TabButton>
          <TabButton
            active={!searching && panel === 'links'}
            onClick={() => {
              setQuery('');
              setPanel('links');
            }}
            icon={Link2}
            count={links.length}
          >
            Ссылки
          </TabButton>
        </nav>
      </header>

      <main className="flex-1 overflow-y-auto px-3 py-3">
        {searching ? (
          <SearchResults
            memHits={searchHits.memHits}
            linkHits={searchHits.linkHits}
            categories={categories}
            groups={groups}
            tagSuggestions={tagSuggestions}
            onNavigate={navigate}
            onRemove={remove}
            onSaveNote={saveNote}
            onToggleImportant={toggleImportant}
            onSetCategory={setMemoryCategory}
            onSetGroup={setMemoryGroup}
            onCreateGroup={createGroupReturningId}
            onOpenLink={openLink}
            onUpdateLink={updateLink}
            onDeleteLink={removeLink}
            onSetLinkGroup={setLinkGroup}
          />
        ) : panel === 'page' ? (
          <PagePanel
            activeTab={activeTab}
            items={pageMemories}
            unlocatedIds={unlocated}
            categories={categories}
            groups={groups}
            onNavigate={navigate}
            onRemove={remove}
            onSaveNote={saveNote}
            onToggleImportant={toggleImportant}
            onSetCategory={setMemoryCategory}
            onSetGroup={setMemoryGroup}
            onCreateGroup={createGroupReturningId}
            onStartElementNote={startElementNote}
          />
        ) : panel === 'all' ? (
          <AllPanel
            items={memoryFiltered}
            categories={categories}
            groups={groups}
            groupCounts={groupCounts}
            smartFolders={smartFolders}
            collapsed={collapsed}
            onToggleCollapsed={toggleCollapsed}
            onExpandAll={expandAll}
            onCollapseAll={collapseAll}
            selectedGroup={selectedGroup}
            onSelectGroup={setSelectedGroup}
            onManageGroups={() => setGroupManageOpen(true)}
            onReparentGroup={reparentGroup}
            onDropItem={(itemId, gid) => {
              const m = byId.get(itemId);
              if (m) void setMemoryGroup(m, gid);
            }}
            onCreateSmart={createSmartFolder}
            onDeleteSmart={deleteSmartFolder}
            onDeleteGroup={deleteGroup}
            filters={filters}
            onFilters={setFilters}
            onManage={() => setManageOpen(true)}
            selectMode={selectMode}
            selectedIds={selectedIds}
            onToggleSelectMode={() => (selectMode ? exitSelect() : setSelectMode(true))}
            onToggleSelect={toggleSelect}
            onBulkSetGroup={bulkSetGroup}
            onBulkImportant={bulkSetImportant}
            onBulkDelete={bulkDelete}
            onNavigate={navigate}
            onRemove={remove}
            onSaveNote={saveNote}
            onToggleImportant={toggleImportant}
            onSetCategory={setMemoryCategory}
            onSetGroup={setMemoryGroup}
            onCreateGroup={createGroupReturningId}
          />
        ) : (
          <LinksPanel
            links={linksByGroup}
            groups={groups}
            groupCounts={linkGroupCounts}
            collapsed={collapsed}
            onToggleCollapsed={toggleCollapsed}
            onExpandAll={expandAll}
            onCollapseAll={collapseAll}
            selectedGroup={selectedLinkGroup}
            onSelectGroup={setSelectedLinkGroup}
            onManageGroups={() => setGroupManageOpen(true)}
            onReparentGroup={reparentGroup}
            onDropLink={(itemId, gid) => {
              const l = linkById.get(itemId);
              if (l) void setLinkGroup(l, gid);
            }}
            onDeleteGroup={deleteGroup}
            tagSuggestions={tagSuggestions}
            tagFilter={linkTagFilter}
            onTagFilter={setLinkTagFilter}
            onSavePage={savePage}
            onSaveManual={saveManualLink}
            onSetGroup={setLinkGroup}
            onCreateGroup={createGroupReturningId}
            onOpen={openLink}
            onUpdate={updateLink}
            onDelete={removeLink}
          />
        )}
      </main>

      {manageOpen && (
        <CategoryManager
          categories={categories}
          onClose={() => setManageOpen(false)}
          onCreate={createCategory}
          onRename={renameCategory}
          onRecolor={recolorCategory}
          onDelete={deleteCategory}
        />
      )}

      {groupManageOpen && (
        <GroupManager
          groups={groups}
          onClose={() => setGroupManageOpen(false)}
          onCreate={createGroup}
          onRename={renameGroup}
          onRecolor={recolorGroup}
          onReparent={reparentGroup}
          onDelete={deleteGroup}
          onExport={exportGroup}
        />
      )}

      {bgOpen && (
        <BackgroundPicker
          value={background}
          onChange={changeBackground}
          onClose={() => setBgOpen(false)}
          onError={(m) => notify(m, 'err')}
        />
      )}

      {settingsOpen && (
        <SettingsPanel
          settings={settings}
          onChange={changeSettings}
          onClose={() => setSettingsOpen(false)}
          currentHost={activeTab ? hostOf(activeTab.url) : undefined}
        />
      )}

      {notice && (
        <div className="pointer-events-none fixed inset-x-0 bottom-4 z-50 flex justify-center px-4">
          <div className="flex animate-slide-up items-center gap-2 rounded-xl bg-slate-900 px-3.5 py-2.5 text-[13px] font-medium text-white shadow-xl">
            {notice.kind === 'err' ? (
              <TriangleAlert size={15} className="text-red-400" />
            ) : (
              <Check size={15} className="text-emerald-400" />
            )}
            {notice.text}
          </div>
        </div>
      )}
    </div>
  );
}

function TabButton({
  active,
  onClick,
  icon: Icon,
  count,
  children,
}: {
  active: boolean;
  onClick: () => void;
  icon: LucideIcon;
  count?: number;
  children: string;
}) {
  return (
    <button
      onClick={onClick}
      className={`flex items-center justify-center gap-1.5 rounded-lg px-2.5 py-1.5 text-[13px] font-medium transition-all duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-400 ${
        active
          ? 'bg-white text-slate-900 shadow-sm'
          : 'text-slate-500 hover:text-slate-700'
      }`}
    >
      <Icon size={15} strokeWidth={1.75} className={active ? 'text-accent-600' : ''} />
      {children}
      {count != null && count > 0 && (
        <span
          className={`rounded-full px-1.5 text-[11px] font-semibold tabular-nums ${
            active ? 'bg-accent-50 text-accent-700' : 'bg-slate-200 text-slate-500'
          }`}
        >
          {count}
        </span>
      )}
    </button>
  );
}

interface RowHandlers {
  onNavigate: (m: Memory) => void;
  onRemove: (m: Memory) => void;
  onSaveNote: (m: Memory, note: string) => void;
  onToggleImportant: (m: Memory) => void;
  onSetCategory: (m: Memory, categoryId: string | null) => void;
  onSetGroup: (m: Memory, groupId: string | null) => void;
}

/** Group props every memory list needs to render + edit a memory's folder. */
interface GroupRowProps {
  groups: Group[];
  onCreateGroup: (name: string) => string;
}

function PagePanel({
  activeTab,
  items,
  unlocatedIds,
  categories,
  groups,
  onCreateGroup,
  onStartElementNote,
  ...handlers
}: RowHandlers &
  GroupRowProps & {
    activeTab: ActiveTab | null;
    items: Memory[];
    unlocatedIds: Set<string>;
    categories: Category[];
    onStartElementNote: () => void;
  }) {
  const unlocatedCount = items.filter((m) => unlocatedIds.has(m.id)).length;
  return (
    <div className="space-y-2.5">
      {activeTab && (
        <div className="rounded-xl border border-slate-200 bg-white px-3 py-2.5">
          <p className="truncate text-[13px] font-medium text-slate-800">
            {activeTab.title || 'Текущая страница'}
          </p>
          <p className="mt-0.5 flex items-center gap-1 truncate text-[11px] text-slate-400">
            <FileText size={11} strokeWidth={1.75} className="shrink-0" />
            {prettyUrl(activeTab.url)}
          </p>
        </div>
      )}

      {unlocatedCount > 0 && (
        <div className="flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2.5 text-[12px] leading-relaxed text-amber-800">
          <TriangleAlert size={15} className="mt-px shrink-0 text-amber-500" />
          <span>
            {unlocatedCount} {plural(unlocatedCount, 'фрагмент', 'фрагмента', 'фрагментов')} не
            удалось найти на странице — текст мог измениться. Откройте {unlocatedCount === 1 ? 'его' : 'их'} из
            списка ниже.
          </span>
        </div>
      )}

      <button
        onClick={onStartElementNote}
        className="flex w-full items-center justify-center gap-2 rounded-xl border border-dashed border-slate-300 bg-white px-3 py-2.5 text-[13px] font-medium text-slate-600 transition-colors duration-150 hover:border-accent-300 hover:bg-accent-50 hover:text-accent-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-400"
      >
        <Pin size={15} strokeWidth={1.75} />
        Прикрепить заметку к элементу
      </button>

      {items.length === 0 ? (
        <EmptyState
          icon={Highlighter}
          title="На этой странице пока пусто"
          hint="Выделите текст на странице — появится панель «Запомнить»."
        />
      ) : (
        <ul className="space-y-2">
          {items.map((m) => (
            <MemoryRow
              key={m.id}
              memory={m}
              unlocated={unlocatedIds.has(m.id)}
              categories={categories}
              groups={groups}
              onCreateGroup={onCreateGroup}
              {...handlers}
            />
          ))}
        </ul>
      )}
    </div>
  );
}

function AllPanel({
  items,
  categories,
  groups,
  groupCounts,
  smartFolders,
  collapsed,
  onToggleCollapsed,
  onExpandAll,
  onCollapseAll,
  selectedGroup,
  onSelectGroup,
  onManageGroups,
  onReparentGroup,
  onDropItem,
  onCreateSmart,
  onDeleteSmart,
  onDeleteGroup,
  onCreateGroup,
  filters,
  onFilters,
  onManage,
  selectMode,
  selectedIds,
  onToggleSelectMode,
  onToggleSelect,
  onBulkSetGroup,
  onBulkImportant,
  onBulkDelete,
  ...handlers
}: RowHandlers &
  GroupRowProps & {
    items: Memory[];
    categories: Category[];
    groupCounts: Map<string, number>;
    smartFolders: SmartFolder[];
    collapsed: Set<string>;
    onToggleCollapsed: (id: string) => void;
    onExpandAll: () => void;
    onCollapseAll: () => void;
    selectedGroup: string;
    onSelectGroup: (id: string) => void;
    onManageGroups: () => void;
    onReparentGroup: (id: string, parentId: string | null) => void;
    onDropItem: (itemId: string, groupId: string | null) => void;
    onCreateSmart: (name: string) => void;
    onDeleteSmart: (id: string) => void;
    onDeleteGroup: (id: string) => void;
    filters: MemoryFilters;
    onFilters: (f: MemoryFilters) => void;
    onManage: () => void;
    selectMode: boolean;
    selectedIds: Set<string>;
    onToggleSelectMode: () => void;
    onToggleSelect: (id: string) => void;
    onBulkSetGroup: (groupId: string | null) => void;
    onBulkImportant: (important: boolean) => void;
    onBulkDelete: () => void;
  }) {
  const narrowed = filtersActive(filters) || selectedGroup !== ALL_GROUPS;
  const [smartName, setSmartName] = useState('');
  const [smartOpen, setSmartOpen] = useState(false);
  const [bulkGroupOpen, setBulkGroupOpen] = useState(false);
  const saveSmart = () => {
    const n = smartName.trim();
    if (!n) return;
    onCreateSmart(n);
    setSmartName('');
    setSmartOpen(false);
  };

  return (
    <div className="space-y-3">
      <GroupTree
        groups={groups}
        counts={groupCounts}
        smartFolders={smartFolders}
        selected={selectedGroup}
        collapsed={collapsed}
        onToggleCollapsed={onToggleCollapsed}
        onExpandAll={onExpandAll}
        onCollapseAll={onCollapseAll}
        onSelect={onSelectGroup}
        onManage={onManageGroups}
        onReparent={onReparentGroup}
        onDropItem={onDropItem}
        onDeleteSmart={onDeleteSmart}
        onDeleteGroup={onDeleteGroup}
        onCreateGroup={onCreateGroup}
      />

      <div className="space-y-2">
        <CategoryFilterBar
          categories={categories}
          filters={filters}
          onChange={onFilters}
          onManage={onManage}
        />
        {/* Unified actions: reset everything, save current filter as a smart folder, select mode. */}
        <div className="flex flex-wrap items-center gap-1.5">
          {narrowed && (
            <button
              onClick={() => {
                onFilters(EMPTY_FILTERS);
                onSelectGroup(ALL_GROUPS);
              }}
              className="rounded-full border border-slate-200 bg-white px-2 py-0.5 text-[11px] font-medium text-slate-500 hover:bg-slate-50"
            >
              Сбросить всё
            </button>
          )}
          {filtersActive(filters) &&
            (smartOpen ? (
              <span className="inline-flex items-center gap-1">
                <input
                  autoFocus
                  value={smartName}
                  onChange={(e) => setSmartName(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') saveSmart();
                    else if (e.key === 'Escape') setSmartOpen(false);
                  }}
                  placeholder="Имя умной папки"
                  className="w-36 rounded-full border border-slate-300 px-2 py-0.5 text-[11px] outline-none focus:border-accent-400"
                />
                <button onClick={saveSmart} className="text-[11px] font-semibold text-accent-700">
                  ОК
                </button>
              </span>
            ) : (
              <button
                onClick={() => setSmartOpen(true)}
                className="inline-flex items-center gap-1 rounded-full border border-purple-200 bg-purple-50 px-2 py-0.5 text-[11px] font-medium text-purple-700 hover:bg-purple-100"
              >
                <Sparkles size={11} /> В умную папку
              </button>
            ))}
          <button
            onClick={onToggleSelectMode}
            className={`ml-auto inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-[11px] font-medium ${
              selectMode
                ? 'border-accent-300 bg-accent-50 text-accent-700'
                : 'border-slate-200 bg-white text-slate-500 hover:bg-slate-50'
            }`}
          >
            <ListChecks size={12} /> {selectMode ? 'Готово' : 'Выбрать'}
          </button>
        </div>

        {selectMode && selectedIds.size > 0 && (
          <div className="flex flex-wrap items-center gap-1.5 rounded-xl border border-accent-200 bg-accent-50/60 px-2.5 py-1.5">
            <span className="text-[12px] font-medium text-accent-800">Выбрано: {selectedIds.size}</span>
            <div className="ml-auto flex items-center gap-1">
              <div className="relative">
                <button
                  onClick={() => setBulkGroupOpen((v) => !v)}
                  className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-2 py-1 text-[11px] font-medium text-slate-600 hover:bg-slate-50"
                >
                  <Folder size={12} /> В группу
                </button>
                {bulkGroupOpen && (
                  <GroupPickerMenu
                    groups={groups}
                    value={null}
                    onCreate={onCreateGroup}
                    onSelect={(id) => {
                      setBulkGroupOpen(false);
                      onBulkSetGroup(id);
                    }}
                    onClose={() => setBulkGroupOpen(false)}
                  />
                )}
              </div>
              <button
                onClick={() => onBulkImportant(true)}
                title="Отметить важными"
                className="inline-flex items-center justify-center rounded-lg border border-slate-200 bg-white px-2 py-1 text-amber-500 hover:bg-slate-50"
              >
                <Star size={13} />
              </button>
              <button
                onClick={onBulkDelete}
                title="Удалить выбранные"
                className="inline-flex items-center justify-center rounded-lg border border-slate-200 bg-white px-2 py-1 text-red-500 hover:bg-red-50"
              >
                <Trash2 size={13} />
              </button>
            </div>
          </div>
        )}
      </div>

      {items.length === 0 ? (
        <EmptyState
          icon={narrowed ? Search : Inbox}
          title={narrowed ? 'Ничего не найдено' : 'Память пуста'}
          hint={
            narrowed
              ? 'Ослабьте фильтры или выберите другую группу.'
              : 'Выделяйте важное на любой странице — сохранённое появится здесь.'
          }
        />
      ) : (
        <ul className="space-y-2">
          {items.map((m) => (
            <MemoryRow
              key={m.id}
              memory={m}
              showPage
              categories={categories}
              groups={groups}
              onCreateGroup={onCreateGroup}
              selectMode={selectMode}
              selected={selectedIds.has(m.id)}
              onToggleSelect={onToggleSelect}
              {...handlers}
            />
          ))}
        </ul>
      )}
    </div>
  );
}

function SearchResults({
  memHits,
  linkHits,
  categories,
  groups,
  onCreateGroup,
  tagSuggestions,
  onOpenLink,
  onUpdateLink,
  onDeleteLink,
  onSetLinkGroup,
  ...handlers
}: RowHandlers &
  GroupRowProps & {
    memHits: Memory[];
    linkHits: Link[];
    categories: Category[];
    tagSuggestions: string[];
    onOpenLink: (l: Link) => void;
    onUpdateLink: (l: Link, patch: { title: string; tags: string[] }) => void;
    onDeleteLink: (l: Link) => void;
    onSetLinkGroup: (l: Link, groupId: string | null) => void;
  }) {
  const total = memHits.length + linkHits.length;
  if (total === 0) {
    return (
      <EmptyState
        icon={Search}
        title="Ничего не найдено"
        hint="Поиск охватывает фрагменты, заметки и ссылки. Попробуйте другой запрос."
      />
    );
  }
  return (
    <div className="space-y-3">
      <p className="text-[11px] font-medium text-slate-400">Найдено: {total}</p>
      <ul className="space-y-2">
        {memHits.map((m) => (
          <MemoryRow
            key={m.id}
            memory={m}
            showPage
            categories={categories}
            groups={groups}
            onCreateGroup={onCreateGroup}
            badge={<TypeBadge kind={m.kind === 'note' ? 'note' : 'memory'} />}
            {...handlers}
          />
        ))}
        {linkHits.map((l) => (
          <LinkCard
            key={l.id}
            link={l}
            suggestions={tagSuggestions}
            badge={<TypeBadge kind="link" />}
            groups={groups}
            onSetGroup={onSetLinkGroup}
            onCreateGroup={onCreateGroup}
            onOpen={onOpenLink}
            onUpdate={onUpdateLink}
            onDelete={onDeleteLink}
          />
        ))}
      </ul>
    </div>
  );
}

function LinksPanel({
  links,
  groups,
  groupCounts,
  collapsed,
  onToggleCollapsed,
  onExpandAll,
  onCollapseAll,
  selectedGroup,
  onSelectGroup,
  onManageGroups,
  onReparentGroup,
  onDropLink,
  onDeleteGroup,
  tagSuggestions,
  tagFilter,
  onTagFilter,
  onSavePage,
  onSaveManual,
  onSetGroup,
  onCreateGroup,
  onOpen,
  onUpdate,
  onDelete,
}: {
  links: Link[];
  groups: Group[];
  groupCounts: Map<string, number>;
  collapsed: Set<string>;
  onToggleCollapsed: (id: string) => void;
  onExpandAll: () => void;
  onCollapseAll: () => void;
  selectedGroup: string;
  onSelectGroup: (id: string) => void;
  onManageGroups: () => void;
  onReparentGroup: (id: string, parentId: string | null) => void;
  onDropLink: (itemId: string, groupId: string | null) => void;
  onDeleteGroup: (id: string) => void;
  tagSuggestions: string[];
  tagFilter: string | null;
  onTagFilter: (t: string | null) => void;
  onSavePage: () => void;
  onSaveManual: (url: string, title: string, tags: string[], groupId: string | null) => Promise<boolean>;
  onSetGroup: (l: Link, groupId: string | null) => void;
  onCreateGroup: (name: string) => string;
  onOpen: (l: Link) => void;
  onUpdate: (l: Link, patch: { title: string; tags: string[] }) => void;
  onDelete: (l: Link) => void;
}) {
  const realGroup =
    selectedGroup !== ALL_GROUPS && selectedGroup !== UNGROUPED ? selectedGroup : null;
  const [manual, setManual] = useState(false);
  const [url, setUrl] = useState('');
  const [title, setTitle] = useState('');
  const [tags, setTags] = useState<string[]>([]);
  const [manualGroup, setManualGroup] = useState<string | null>(realGroup);
  const [manualGroupOpen, setManualGroupOpen] = useState(false);

  const openManual = () => {
    setManualGroup(realGroup);
    setManual((v) => !v);
  };
  const submit = async () => {
    if (!url.trim()) return;
    const ok = await onSaveManual(url, title, tags, manualGroup);
    if (ok) {
      setUrl('');
      setTitle('');
      setTags([]);
      setManual(false);
    }
  };
  const manualGroupName = manualGroup ? groups.find((g) => g.id === manualGroup)?.name : undefined;

  const shown = tagFilter ? links.filter((l) => l.tags.includes(tagFilter)) : links;

  return (
    <div className="space-y-2.5">
      <div className="flex gap-1.5">
        <Button variant="primary" size="md" onClick={onSavePage} className="flex-1">
          <Plus size={15} strokeWidth={2} />
          Сохранить страницу
        </Button>
        <Button variant="secondary" size="md" onClick={openManual}>
          По ссылке…
        </Button>
      </div>

      {manual && (
        <div className="space-y-2 rounded-xl border border-slate-200 bg-white p-3">
          <input
            value={url}
            autoFocus
            onChange={(e) => setUrl(e.target.value)}
            placeholder="https://…"
            className="w-full rounded-lg border border-slate-300 bg-white px-2.5 py-1.5 text-[13px] text-slate-800 placeholder:text-slate-400 outline-none transition-all focus:border-accent-400 focus:ring-2 focus:ring-accent-500/20"
          />
          <input
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="Название (необязательно)"
            className="w-full rounded-lg border border-slate-300 bg-white px-2.5 py-1.5 text-[13px] text-slate-800 placeholder:text-slate-400 outline-none transition-all focus:border-accent-400 focus:ring-2 focus:ring-accent-500/20"
          />
          <TagInput value={tags} onChange={setTags} suggestions={tagSuggestions} />
          <div className="flex items-center justify-between">
            <div className="relative">
              <button
                onClick={() => setManualGroupOpen((v) => !v)}
                className="inline-flex items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-2 py-1 text-[12px] font-medium text-slate-600 hover:bg-slate-50"
              >
                <Folder size={13} strokeWidth={1.75} className="text-slate-500" />
                <span className="max-w-[140px] truncate">{manualGroupName ?? 'Без группы'}</span>
              </button>
              {manualGroupOpen && (
                <GroupPickerMenu
                  groups={groups}
                  value={manualGroup}
                  align="left"
                  onCreate={onCreateGroup}
                  onSelect={(id) => {
                    setManualGroupOpen(false);
                    setManualGroup(id);
                  }}
                  onClose={() => setManualGroupOpen(false)}
                />
              )}
            </div>
            <div className="flex gap-1.5">
              <Button variant="ghost" size="sm" onClick={() => setManual(false)}>
                Отмена
              </Button>
              <Button variant="primary" size="sm" onClick={submit} disabled={!url.trim()}>
                Сохранить
              </Button>
            </div>
          </div>
        </div>
      )}

      <GroupTree
        groups={groups}
        counts={groupCounts}
        selected={selectedGroup}
        collapsed={collapsed}
        onToggleCollapsed={onToggleCollapsed}
        onExpandAll={onExpandAll}
        onCollapseAll={onCollapseAll}
        onSelect={onSelectGroup}
        onManage={onManageGroups}
        onReparent={onReparentGroup}
        onDropItem={onDropLink}
        onDeleteGroup={onDeleteGroup}
        onCreateGroup={onCreateGroup}
      />

      {tagFilter && (
        <div className="flex items-center gap-2 text-[12px] text-slate-500">
          <span>
            Фильтр: <span className="font-medium text-accent-700">#{tagFilter}</span>
          </span>
          <button onClick={() => onTagFilter(null)} className="text-slate-400 hover:text-slate-700">
            сбросить
          </button>
        </div>
      )}

      {shown.length === 0 ? (
        <EmptyState
          icon={Link2}
          title={tagFilter ? 'Нет ссылок с этим тегом' : 'Пока нет ссылок'}
          hint={
            tagFilter
              ? 'Сбросьте фильтр, чтобы увидеть все ссылки.'
              : 'Сохраняйте полезные страницы целиком — они появятся здесь, отдельно от памяти.'
          }
        />
      ) : (
        <ul className="space-y-2">
          {shown.map((l) => (
            <LinkCard
              key={l.id}
              link={l}
              suggestions={tagSuggestions}
              groups={groups}
              onSetGroup={onSetGroup}
              onCreateGroup={onCreateGroup}
              onOpen={onOpen}
              onUpdate={onUpdate}
              onDelete={onDelete}
              onTagClick={onTagFilter}
            />
          ))}
        </ul>
      )}
    </div>
  );
}

function MemoryRow({
  memory,
  showPage,
  unlocated,
  categories,
  groups,
  onCreateGroup,
  badge,
  selectMode,
  selected,
  onToggleSelect,
  onNavigate,
  onRemove,
  onSaveNote,
  onToggleImportant,
  onSetCategory,
  onSetGroup,
}: RowHandlers &
  GroupRowProps & {
    memory: Memory;
    showPage?: boolean;
    unlocated?: boolean;
    categories: Category[];
    badge?: ReactNode;
    selectMode?: boolean;
    selected?: boolean;
    onToggleSelect?: (id: string) => void;
  }) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(memory.note);
  const [menuOpen, setMenuOpen] = useState(false);
  const [groupMenuOpen, setGroupMenuOpen] = useState(false);
  const isHighlight = memory.kind === 'highlight';
  const category = resolveCategory(categories, memory.categoryId);
  const groupPath = pathOf(groups, memory.groupId);
  const group = groupPath.length ? groupPath[groupPath.length - 1] : undefined;
  const groupCrumbs = groupPath.map((g) => g.name).join(' / ');

  const onKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Escape') {
      setDraft(memory.note);
      setEditing(false);
    } else if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) {
      onSaveNote(memory, draft.trim());
      setEditing(false);
    }
  };

  return (
    <li
      draggable={!editing}
      onDragStart={(e) => {
        e.dataTransfer.setData(DND_ITEM, memory.id);
        e.dataTransfer.effectAllowed = 'move';
      }}
      className={`group rounded-xl border bg-white transition-all duration-150 hover:shadow-sm ${
        selected
          ? 'border-accent-300 ring-1 ring-accent-300'
          : unlocated
            ? 'border-amber-200'
            : 'border-slate-200 hover:border-slate-300'
      } ${memory.important ? 'border-l-2 border-l-amber-400' : ''}`}
    >
      <div className="flex items-start gap-2.5 p-3">
        {selectMode && (
          <button
            onClick={() => onToggleSelect?.(memory.id)}
            aria-label={selected ? 'Снять выделение' : 'Выделить'}
            className="mt-px shrink-0 text-slate-400 hover:text-accent-600"
          >
            {selected ? (
              <CheckSquare size={18} strokeWidth={1.75} className="text-accent-600" />
            ) : (
              <Square size={18} strokeWidth={1.75} />
            )}
          </button>
        )}
        <span
          className={`mt-px flex h-6 w-6 shrink-0 items-center justify-center rounded-md ${
            category
              ? ''
              : isHighlight
                ? 'text-amber-700'
                : 'bg-slate-100 text-slate-500'
          }`}
          style={
            category
              ? { backgroundColor: hexToRgba(category.color, 0.28), color: category.color }
              : isHighlight
                ? { backgroundColor: hexToRgba(memory.color, 0.28) }
                : undefined
          }
          title={category ? category.label : isHighlight ? 'Без категории' : 'Заметка к элементу'}
        >
          {category ? (
            <CategoryIcon name={category.icon} size={13} />
          ) : isHighlight ? (
            <Highlighter size={13} strokeWidth={2} />
          ) : (
            <Pin size={13} strokeWidth={2} />
          )}
        </span>

        <button
          onClick={() => onNavigate(memory)}
          className="min-w-0 flex-1 text-left"
          title="Открыть и перейти к фрагменту"
        >
          {badge && <span className="mb-1 block">{badge}</span>}
          <span className="line-clamp-3 text-[13px] leading-relaxed text-slate-700 transition-colors group-hover:text-slate-900">
            {memory.text || 'Без текста'}
          </span>
        </button>

        {unlocated && (
          <span className="mt-0.5 shrink-0 text-amber-500" title="Не найдено на странице">
            <TriangleAlert size={14} strokeWidth={1.75} />
          </span>
        )}
      </div>

      {memory.note && !editing && (
        <div className="mx-3 mb-3 flex gap-1.5 rounded-lg bg-slate-50 px-2.5 py-1.5 text-[12px] leading-relaxed text-slate-600">
          <StickyNote size={13} strokeWidth={1.75} className="mt-0.5 shrink-0 text-slate-400" />
          <p className="whitespace-pre-wrap">{memory.note}</p>
        </div>
      )}

      {editing && (
        <div className="mx-3 mb-3">
          <textarea
            value={draft}
            autoFocus
            onChange={(e) => setDraft(e.target.value)}
            onKeyDown={onKeyDown}
            rows={3}
            placeholder="Добавьте заметку…"
            className="w-full resize-none rounded-lg border border-slate-300 bg-white p-2.5 text-[13px] text-slate-800 placeholder:text-slate-400 outline-none transition-all duration-150 focus:border-accent-400 focus:ring-2 focus:ring-accent-500/20"
          />
          <div className="mt-1.5 flex items-center justify-between">
            <span className="text-[11px] text-slate-400">Esc — отмена · ⌘↵ — сохранить</span>
            <div className="flex gap-1.5">
              <Button
                variant="ghost"
                size="sm"
                onClick={() => {
                  setDraft(memory.note);
                  setEditing(false);
                }}
              >
                Отмена
              </Button>
              <Button
                variant="primary"
                size="sm"
                onClick={() => {
                  onSaveNote(memory, draft.trim());
                  setEditing(false);
                }}
              >
                Сохранить
              </Button>
            </div>
          </div>
        </div>
      )}

      {showPage && (
        <p
          className="mx-3 mb-2 flex items-center gap-1 truncate text-[11px] text-slate-400"
          title={memory.href}
        >
          <FileText size={11} strokeWidth={1.75} className="shrink-0" />
          <span className="truncate">{memory.title || prettyUrl(memory.url)}</span>
        </p>
      )}

      <div className="flex items-center gap-1 border-t border-slate-100 px-2.5 py-1.5">
        <span className="text-[11px] tabular-nums text-slate-400">{formatDate(memory.createdAt)}</span>
        {group && (
          <button
            onClick={() => setGroupMenuOpen((v) => !v)}
            title={groupCrumbs}
            className="inline-flex min-w-0 max-w-[55%] items-center gap-1 rounded-full bg-slate-100 px-2 py-0.5 text-[10.5px] font-medium text-slate-500 transition-colors hover:bg-slate-200"
          >
            <Folder
              size={11}
              strokeWidth={1.75}
              className="shrink-0"
              style={group.color ? { color: group.color } : undefined}
            />
            <span className="truncate">{groupCrumbs}</span>
          </button>
        )}
        <div className="ml-auto flex items-center gap-0.5">
          <IconButton onClick={() => onNavigate(memory)} title="Открыть" aria-label="Открыть">
            <ArrowUpRight size={16} strokeWidth={1.75} />
          </IconButton>
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
                value={memory.groupId ?? null}
                onCreate={onCreateGroup}
                onSelect={(id) => {
                  setGroupMenuOpen(false);
                  onSetGroup(memory, id);
                }}
                onClose={() => setGroupMenuOpen(false)}
              />
            )}
          </div>
          {isHighlight && (
            <div className="relative">
              <IconButton
                onClick={() => setMenuOpen((v) => !v)}
                title="Категория"
                aria-label="Категория"
              >
                <Tag size={15} strokeWidth={1.75} />
              </IconButton>
              {menuOpen && (
                <CategoryAssignMenu
                  categories={categories}
                  value={category?.id}
                  onSelect={(id) => {
                    setMenuOpen(false);
                    onSetCategory(memory, id);
                  }}
                  onClose={() => setMenuOpen(false)}
                />
              )}
            </div>
          )}
          <IconButton
            variant={memory.important ? 'active' : 'default'}
            onClick={() => onToggleImportant(memory)}
            title={memory.important ? 'Снять важность' : 'Отметить важным'}
            aria-label="Важное"
          >
            <Star size={15} strokeWidth={1.75} className={memory.important ? 'fill-current' : ''} />
          </IconButton>
          <IconButton
            onClick={() => setEditing((v) => !v)}
            title="Заметка"
            aria-label="Редактировать заметку"
          >
            <Pencil size={15} strokeWidth={1.75} />
          </IconButton>
          <IconButton
            variant="danger"
            onClick={() => onRemove(memory)}
            title="Удалить"
            aria-label="Удалить"
          >
            <Trash2 size={15} strokeWidth={1.75} />
          </IconButton>
        </div>
      </div>
    </li>
  );
}

function EmptyState({
  icon: Icon,
  title,
  hint,
}: {
  icon: LucideIcon;
  title: string;
  hint: string;
}) {
  return (
    <div className="flex animate-fade-in flex-col items-center rounded-2xl border border-dashed border-slate-200 bg-white px-6 py-12 text-center">
      <span className="mb-3 flex h-11 w-11 items-center justify-center rounded-full bg-slate-100 text-slate-400">
        <Icon size={20} strokeWidth={1.75} />
      </span>
      <p className="text-[13px] font-medium text-slate-700">{title}</p>
      <p className="mt-1 max-w-[230px] text-[12px] leading-relaxed text-slate-400">{hint}</p>
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

/** Russian plural: pick the form for `n` (one / few / many). */
function plural(n: number, one: string, few: string, many: string): string {
  const mod10 = n % 10;
  const mod100 = n % 100;
  if (mod10 === 1 && mod100 !== 11) return one;
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 10 || mod100 >= 20)) return few;
  return many;
}
