"use client"

import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode
} from "react"

import {
  listMemoryItems,
  patchMemoryItem,
  deleteMemoryItem,
  listMemoryLinks,
  getSimilarMemoryItems,
  listProjects,
  type MemoryItem,
  type Project
} from "../../lib/api"
import {
  SOLUTION_CATEGORIES,
  SOLUTION_STATUSES,
  STATUS_META,
  CATEGORY_LABEL,
  catOf,
  statusOf,
  freeTags,
  withStatus,
  withCategory,
  parseSolution,
  solutionPreview,
  type SolutionStatus
} from "../../lib/solutions"
import { fmtDate } from "../../lib/material-ui"
import Markdown from "../markdown"
import SolutionEditor from "./editor"

/**
 * /solutions — личная база решённых проблем поверх `memory_items`
 * (item_type="solution"). Три колонки: фильтры (статус+категория) · список
 * карточек · detail выбранного. Категория/статус — теги cat:/st:; секции
 * Проблема/Причина/Решение/Команды/Заметки — markdown в content; связанные
 * решения — из memory_links (без графа). Никакой отдельной таблицы.
 */

const PAGE_SIZE = 20
const SORTS: { key: SortKey; label: string }[] = [
  { key: "new", label: "Сначала новые" },
  { key: "old", label: "Сначала старые" },
  { key: "importance", label: "По важности" }
]
type SortKey = "new" | "old" | "importance"

export default function SolutionsPage() {
  const [items, setItems] = useState<MemoryItem[]>([])
  const [projects, setProjects] = useState<Project[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const [statusFilter, setStatusFilter] = useState<SolutionStatus | "">("")
  const [catFilter, setCatFilter] = useState<string>("")
  const [q, setQ] = useState("")
  const [sort, setSort] = useState<SortKey>("new")
  const [page, setPage] = useState(0)

  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [editing, setEditing] = useState<MemoryItem | null>(null)
  const [creating, setCreating] = useState(false)
  const searchRef = useRef<HTMLInputElement>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const data = await listMemoryItems({
        item_type: "solution",
        status: "active",
        limit: 200 // бэкенд капит limit ≤ 200; для личной базы решений достаточно
      })
      setItems(data)
    } catch (e) {
      setError(String(e))
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    load()
    listProjects()
      .then(setProjects)
      .catch(() => {})
  }, [load])

  // ⌘/Ctrl+K фокусирует поиск (как в каталоге).
  useEffect(() => {
    const h = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") {
        e.preventDefault()
        searchRef.current?.focus()
      }
    }
    window.addEventListener("keydown", h)
    return () => window.removeEventListener("keydown", h)
  }, [])

  const projectName = useCallback(
    (id: string | null) => projects.find((p) => p.id === id)?.name ?? null,
    [projects]
  )

  // Счётчики для сайдбара (по всем активным решениям, без учёта текущих фильтров).
  const statusCounts = useMemo(() => {
    const c: Record<SolutionStatus, number> = {
      resolved: 0,
      in_progress: 0,
      deferred: 0,
      unresolved: 0
    }
    for (const it of items) c[statusOf(it.tags)]++
    return c
  }, [items])

  const catCounts = useMemo(() => {
    const c: Record<string, number> = {}
    for (const it of items) {
      const s = catOf(it.tags)
      c[s] = (c[s] || 0) + 1
    }
    return c
  }, [items])

  const filtered = useMemo(() => {
    const needle = q.trim().toLowerCase()
    let list = items.filter((it) => {
      if (statusFilter && statusOf(it.tags) !== statusFilter) return false
      if (catFilter && catOf(it.tags) !== catFilter) return false
      if (needle) {
        const hay = `${it.title || ""} ${it.content} ${it.tags.join(" ")}`.toLowerCase()
        if (!hay.includes(needle)) return false
      }
      return true
    })
    list = [...list].sort((a, b) => {
      if (sort === "importance") return b.importance - a.importance
      const da = +new Date(a.created_at)
      const db = +new Date(b.created_at)
      return sort === "old" ? da - db : db - da
    })
    return list
  }, [items, statusFilter, catFilter, q, sort])

  // Сброс страницы при смене фильтров/поиска/сортировки.
  useEffect(() => setPage(0), [statusFilter, catFilter, q, sort])

  const pageCount = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE))
  const curPage = Math.min(page, pageCount - 1) // защита от устаревшего page при смене фильтров
  const pageItems = filtered.slice(curPage * PAGE_SIZE, curPage * PAGE_SIZE + PAGE_SIZE)

  // Держим валидный выбор: если выбранного нет в отфильтрованном — берём первый.
  useEffect(() => {
    if (filtered.length === 0) {
      setSelectedId(null)
      return
    }
    if (!selectedId || !filtered.some((it) => it.id === selectedId)) {
      setSelectedId(filtered[0].id)
    }
  }, [filtered, selectedId])

  const recent = useMemo(
    () =>
      [...items]
        .sort((a, b) => +new Date(b.created_at) - +new Date(a.created_at))
        .slice(0, 5),
    [items]
  )

  const selected = items.find((it) => it.id === selectedId) || null

  async function onSaved(saved: MemoryItem) {
    setEditing(null)
    setCreating(false)
    await load()
    setSelectedId(saved.id)
  }

  async function changeStatus(it: MemoryItem, status: SolutionStatus) {
    try {
      const upd = await patchMemoryItem(it.id, { tags: withStatus(it.tags, status) })
      setItems((prev) => prev.map((x) => (x.id === upd.id ? upd : x)))
    } catch (e) {
      setError(String(e))
    }
  }

  async function changeCategory(it: MemoryItem, category: string) {
    try {
      const upd = await patchMemoryItem(it.id, {
        tags: withCategory(it.tags, category)
      })
      setItems((prev) => prev.map((x) => (x.id === upd.id ? upd : x)))
    } catch (e) {
      setError(String(e))
    }
  }

  async function remove(it: MemoryItem) {
    if (!window.confirm(`Удалить решение «${it.title || "без названия"}»?`)) return
    try {
      await deleteMemoryItem(it.id)
      setItems((prev) => prev.filter((x) => x.id !== it.id))
    } catch (e) {
      setError(String(e))
    }
  }

  return (
    <main className="h-[calc(100vh-3.5rem)] flex flex-col">
      {/* Тулбар: заголовок · поиск (⌘K) · новое решение */}
      <div className="shrink-0 flex items-center gap-4 px-6 h-16 border-b border-neutral-800">
        <div className="flex items-baseline gap-2.5">
          <h1 className="text-lg font-semibold">Решения</h1>
          <span className="text-[11px] tabular-nums text-neutral-600 font-sans">
            {items.length}
          </span>
        </div>
        <div className="relative flex-1 max-w-md ml-auto">
          <span className="absolute left-3 top-1/2 -translate-y-1/2 text-neutral-500">
            <SearchIcon />
          </span>
          <input
            ref={searchRef}
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder="Поиск решений…"
            className="w-full bg-neutral-900 border border-neutral-800 rounded-lg pl-10 pr-12 py-2 text-sm font-sans focus:outline-none focus:border-lime-400/50 transition-colors"
          />
          <span className="absolute right-2.5 top-1/2 -translate-y-1/2 text-[11px] text-neutral-600 border border-neutral-800 rounded px-1.5 py-0.5 font-sans">
            ⌘K
          </span>
        </div>
        <button
          onClick={() => {
            setEditing(null)
            setCreating(true)
          }}
          className="shrink-0 text-sm font-sans px-3.5 py-2 rounded-lg bg-lime-400 text-neutral-950 font-medium hover:bg-lime-300 transition-colors">
          + Новое решение
        </button>
      </div>

      <div className="flex-1 flex overflow-hidden">
        {/* ── Сайдбар: статусы + категории + недавние ── */}
        <aside className="hidden lg:flex w-64 shrink-0 flex-col overflow-y-auto border-r border-neutral-800 px-4 py-5 gap-6">
          <FilterGroup title="Статусы">
            <FilterRow
              active={statusFilter === ""}
              onClick={() => setStatusFilter("")}
              label="Все"
              count={items.length}
            />
            {SOLUTION_STATUSES.map((s) => (
              <FilterRow
                key={s.key}
                active={statusFilter === s.key}
                onClick={() =>
                  setStatusFilter(statusFilter === s.key ? "" : s.key)
                }
                label={s.label}
                count={statusCounts[s.key]}
                icon={<StatusIcon status={s.key} />}
              />
            ))}
          </FilterGroup>

          <FilterGroup title="Категории">
            <FilterRow
              active={catFilter === ""}
              onClick={() => setCatFilter("")}
              label="Все"
              count={items.length}
            />
            {SOLUTION_CATEGORIES.map((c) => (
              <FilterRow
                key={c.slug}
                active={catFilter === c.slug}
                onClick={() => setCatFilter(catFilter === c.slug ? "" : c.slug)}
                label={c.label}
                count={catCounts[c.slug] || 0}
                icon={<DotGlyph />}
              />
            ))}
          </FilterGroup>

          {recent.length > 0 && (
            <FilterGroup title="Недавние">
              {recent.map((it) => (
                <button
                  key={it.id}
                  onClick={() => setSelectedId(it.id)}
                  className={`w-full text-left text-[13px] font-sans px-2 py-1.5 rounded-md truncate transition-colors ${
                    selectedId === it.id
                      ? "text-lime-400 bg-neutral-900"
                      : "text-neutral-400 hover:text-neutral-100 hover:bg-neutral-900"
                  }`}>
                  {it.title || solutionPreview(it.content) || "—"}
                </button>
              ))}
            </FilterGroup>
          )}
        </aside>

        {/* ── Список карточек ── */}
        <section className="w-full md:w-[380px] shrink-0 flex flex-col overflow-hidden border-r border-neutral-800">
          <div className="shrink-0 flex items-center justify-between px-4 h-12 border-b border-neutral-800">
            <span className="text-sm font-sans text-neutral-300">
              {statusFilter
                ? STATUS_META[statusFilter].label
                : catFilter
                  ? CATEGORY_LABEL[catFilter]
                  : "Все решения"}
              <span className="text-neutral-600"> · {filtered.length}</span>
            </span>
            <select
              value={sort}
              onChange={(e) => setSort(e.target.value as SortKey)}
              className="bg-transparent text-[12px] font-sans text-neutral-400 hover:text-neutral-200 focus:outline-none cursor-pointer">
              {SORTS.map((s) => (
                <option key={s.key} value={s.key} className="bg-neutral-900">
                  {s.label}
                </option>
              ))}
            </select>
          </div>

          <div className="flex-1 overflow-y-auto p-3 space-y-2">
            {loading ? (
              <div className="text-neutral-500 text-sm py-8 text-center">
                // загрузка…
              </div>
            ) : filtered.length === 0 ? (
              <div className="text-neutral-500 text-[13px] font-sans py-10 px-3 border border-dashed border-neutral-800 rounded-lg text-center">
                {items.length === 0
                  ? "Пока нет решений. Нажми «+ Новое решение» или пришли в Telegram сообщение, начав с #solution."
                  : "Ничего не найдено по фильтрам."}
              </div>
            ) : (
              pageItems.map((it) => (
                <SolutionCard
                  key={it.id}
                  it={it}
                  active={it.id === selectedId}
                  onClick={() => setSelectedId(it.id)}
                  projectName={projectName(it.project_id)}
                />
              ))
            )}
          </div>

          {pageCount > 1 && (
            <Pager
              page={curPage}
              pageCount={pageCount}
              total={filtered.length}
              onPage={setPage}
            />
          )}
        </section>

        {/* ── Detail ── */}
        <section className="flex-1 overflow-y-auto">
          {selected ? (
            <Detail
              key={selected.id}
              it={selected}
              projectName={projectName(selected.project_id)}
              allItems={items}
              onEdit={() => {
                setCreating(false)
                setEditing(selected)
              }}
              onDelete={() => remove(selected)}
              onStatus={(s) => changeStatus(selected, s)}
              onCategory={(c) => changeCategory(selected, c)}
              onSelect={setSelectedId}
            />
          ) : (
            <div className="h-full grid place-items-center text-neutral-600 text-sm font-sans px-6 text-center">
              {items.length === 0
                ? "Здесь появятся твои решения — выбери слева или создай новое."
                : "Выбери решение слева."}
            </div>
          )}
        </section>
      </div>

      {error && (
        <div className="shrink-0 px-6 py-2 text-red-400 text-sm font-sans border-t border-neutral-800">
          Ошибка: {error}
        </div>
      )}

      {(creating || editing) && (
        <SolutionEditor
          item={editing}
          projects={projects}
          onClose={() => {
            setEditing(null)
            setCreating(false)
          }}
          onSaved={onSaved}
          onError={setError}
        />
      )}
    </main>
  )
}

// ── Сайдбар ───────────────────────────────────────────────────────────────────

function FilterGroup({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div>
      <div className="text-[10px] uppercase tracking-widest text-neutral-600 mb-2 px-2">
        {title}
      </div>
      <div className="space-y-0.5">{children}</div>
    </div>
  )
}

function FilterRow({
  active,
  onClick,
  label,
  count,
  icon
}: {
  active: boolean
  onClick: () => void
  label: string
  count: number
  icon?: ReactNode
}) {
  return (
    <button
      onClick={onClick}
      className={`w-full flex items-center gap-2.5 text-sm font-sans px-2 py-1.5 rounded-md transition-colors ${
        active
          ? "bg-neutral-900 text-neutral-100"
          : "text-neutral-400 hover:text-neutral-100 hover:bg-neutral-900"
      }`}>
      {icon && <span className="shrink-0 text-neutral-500">{icon}</span>}
      <span className="truncate flex-1 text-left">{label}</span>
      <span className="text-[11px] tabular-nums text-neutral-600 shrink-0">
        {count}
      </span>
    </button>
  )
}

// ── Карточка списка ─────────────────────────────────────────────────────────────

function SolutionCard({
  it,
  active,
  onClick,
  projectName
}: {
  it: MemoryItem
  active: boolean
  onClick: () => void
  projectName: string | null
}) {
  const status = statusOf(it.tags)
  const cat = catOf(it.tags)
  const tags = freeTags(it.tags).slice(0, 3)
  return (
    <button
      onClick={onClick}
      className={`w-full text-left rounded-lg border p-3 transition-colors ${
        active
          ? "border-lime-400/50 bg-lime-400/[0.04]"
          : "border-neutral-800 bg-neutral-900/30 hover:border-neutral-700"
      }`}>
      <div className="flex items-start gap-2">
        <span className="mt-0.5 shrink-0">
          <StatusIcon status={status} />
        </span>
        <div className="min-w-0 flex-1">
          <div className="text-sm font-sans text-neutral-100 break-words line-clamp-2">
            {it.title || solutionPreview(it.content) || "—"}
          </div>
          <div className="flex items-center gap-1.5 flex-wrap mt-1.5">
            <span className="text-[10px] font-sans px-1.5 py-0.5 rounded border border-neutral-700 text-neutral-400">
              {CATEGORY_LABEL[cat]}
            </span>
            {tags.map((t) => (
              <span
                key={t}
                className="text-[10px] font-sans px-1.5 py-0.5 rounded border border-neutral-800 text-neutral-500">
                {t}
              </span>
            ))}
          </div>
          <div className="flex items-center gap-2 mt-1.5 text-[11px] font-sans text-neutral-600">
            <span className="tabular-nums">{fmtDate(it.created_at)}</span>
            {projectName && (
              <>
                <span>·</span>
                <span className="truncate">{projectName}</span>
              </>
            )}
          </div>
        </div>
      </div>
    </button>
  )
}

function Pager({
  page,
  pageCount,
  total,
  onPage
}: {
  page: number
  pageCount: number
  total: number
  onPage: (p: number) => void
}) {
  const from = page * PAGE_SIZE + 1
  const to = Math.min(total, (page + 1) * PAGE_SIZE)
  return (
    <div className="shrink-0 flex items-center justify-between px-4 h-11 border-t border-neutral-800 text-[12px] font-sans text-neutral-500">
      <span className="tabular-nums">
        {from}–{to} из {total}
      </span>
      <div className="flex items-center gap-1">
        <button
          disabled={page === 0}
          onClick={() => onPage(page - 1)}
          className="px-2 py-0.5 rounded hover:text-neutral-100 disabled:opacity-30 disabled:cursor-not-allowed">
          ‹
        </button>
        <span className="tabular-nums text-neutral-400">
          {page + 1} / {pageCount}
        </span>
        <button
          disabled={page >= pageCount - 1}
          onClick={() => onPage(page + 1)}
          className="px-2 py-0.5 rounded hover:text-neutral-100 disabled:opacity-30 disabled:cursor-not-allowed">
          ›
        </button>
      </div>
    </div>
  )
}

// ── Detail ─────────────────────────────────────────────────────────────────────

function Detail({
  it,
  projectName,
  allItems,
  onEdit,
  onDelete,
  onStatus,
  onCategory,
  onSelect
}: {
  it: MemoryItem
  projectName: string | null
  allItems: MemoryItem[]
  onEdit: () => void
  onDelete: () => void
  onStatus: (s: SolutionStatus) => void
  onCategory: (c: string) => void
  onSelect: (id: string) => void
}) {
  const f = useMemo(() => parseSolution(it.content), [it.content])
  const status = statusOf(it.tags)
  const cat = catOf(it.tags)
  const tags = freeTags(it.tags)

  // Связанные решения — из memory_links (1 hop), резолвим к загруженным решениям.
  const [related, setRelated] = useState<{ item: MemoryItem; relation: string }[]>(
    []
  )
  useEffect(() => {
    let alive = true
    listMemoryLinks(it.id)
      .then((links) => {
        if (!alive) return
        const byId = new Map(allItems.map((x) => [x.id, x]))
        const out: { item: MemoryItem; relation: string }[] = []
        const seen = new Set<string>()
        for (const ln of links) {
          const otherId = ln.source_id === it.id ? ln.target_id : ln.source_id
          const other = byId.get(otherId)
          if (other && other.id !== it.id && !seen.has(other.id)) {
            seen.add(other.id)
            out.push({ item: other, relation: ln.relation })
          }
        }
        setRelated(out)
      })
      .catch(() => alive && setRelated([]))
    return () => {
      alive = false
    }
  }, [it.id, allItems])

  // Похожие решения — серверный реюз search_memory_items (полнотекст ∪ теги).
  // Бэкенд исключает сам элемент и уже связанные, поэтому блок не дублирует
  // «Связанные решения». Без LLM/эмбеддингов/нового движка рекомендаций.
  const [similar, setSimilar] = useState<MemoryItem[]>([])
  useEffect(() => {
    let alive = true
    getSimilarMemoryItems(it.id)
      .then((rows) => alive && setSimilar(rows))
      .catch(() => alive && setSimilar([]))
    return () => {
      alive = false
    }
  }, [it.id])

  return (
    <div className="max-w-3xl mx-auto px-8 py-7">
      {/* Заголовок + действия */}
      <div className="flex items-start justify-between gap-4">
        <h2 className="text-2xl font-semibold break-words">
          {it.title || solutionPreview(it.content) || "Без названия"}
        </h2>
        <div className="flex items-center gap-2 shrink-0">
          <button
            onClick={onEdit}
            className="text-xs font-sans px-3 py-1.5 rounded-md border border-neutral-700 text-neutral-300 hover:text-lime-400 hover:border-lime-400/50 transition-colors">
            ✎ Редактировать
          </button>
          <button
            onClick={onDelete}
            title="Удалить"
            className="text-xs font-sans px-2.5 py-1.5 rounded-md border border-neutral-800 text-neutral-500 hover:text-red-400 hover:border-red-400/40 transition-colors">
            Удалить
          </button>
        </div>
      </div>

      {/* Мета: статус (select) · категория (select) · дата · проект */}
      <div className="flex items-center gap-3 flex-wrap mt-3 text-[13px] font-sans">
        <span className="inline-flex items-center gap-1.5">
          <StatusIcon status={status} />
          <select
            value={status}
            onChange={(e) => onStatus(e.target.value as SolutionStatus)}
            className={`bg-transparent focus:outline-none cursor-pointer ${STATUS_META[status].text}`}>
            {SOLUTION_STATUSES.map((s) => (
              <option key={s.key} value={s.key} className="bg-neutral-900 text-neutral-100">
                {s.label}
              </option>
            ))}
          </select>
        </span>
        <span className="text-neutral-700">·</span>
        <span className="inline-flex items-center gap-1.5 text-neutral-400">
          <FolderGlyph />
          <select
            value={cat}
            onChange={(e) => onCategory(e.target.value)}
            className="bg-transparent text-neutral-300 focus:outline-none cursor-pointer">
            {SOLUTION_CATEGORIES.map((c) => (
              <option key={c.slug} value={c.slug} className="bg-neutral-900">
                {c.label}
              </option>
            ))}
          </select>
        </span>
        <span className="text-neutral-700">·</span>
        <span className="text-neutral-500 tabular-nums">{fmtDate(it.created_at)}</span>
        {projectName && (
          <>
            <span className="text-neutral-700">·</span>
            <span className="text-neutral-500">{projectName}</span>
          </>
        )}
      </div>

      {/* Свободные теги */}
      {tags.length > 0 && (
        <div className="flex items-center gap-1.5 flex-wrap mt-3">
          {tags.map((t) => (
            <span
              key={t}
              className="text-[12px] font-sans px-2 py-0.5 rounded-md border border-neutral-800 text-neutral-400">
              {t}
            </span>
          ))}
        </div>
      )}

      {/* Секции */}
      <div className="mt-7 space-y-6">
        <Section title="Проблема" body={f.problem} boxed />
        <Section title="Причина" body={f.cause} />
        <Section title="Решение" body={f.solution} />
        {f.commands.trim() && <Commands title="Команды" code={f.commands} />}
        <Section title="Заметки" body={f.notes} />
      </div>

      {/* Связанные решения (из memory_links) */}
      {related.length > 0 && (
        <div className="mt-8 pt-6 border-t border-neutral-800">
          <div className="text-sm font-semibold mb-3">Связанные решения</div>
          <div className="grid sm:grid-cols-2 gap-2">
            {related.map(({ item, relation }) => (
              <button
                key={item.id}
                onClick={() => onSelect(item.id)}
                className="text-left rounded-lg border border-neutral-800 bg-neutral-900/30 hover:border-lime-400/40 p-2.5 transition-colors">
                <div className="flex items-center gap-1.5">
                  <StatusIcon status={statusOf(item.tags)} />
                  <span className="text-[13px] font-sans text-neutral-100 truncate">
                    {item.title || solutionPreview(item.content) || "—"}
                  </span>
                </div>
                <div className="text-[11px] font-sans text-neutral-600 mt-1 flex items-center gap-1.5">
                  <span className="text-pink-400">{relation}</span>
                  <span>·</span>
                  <span>{CATEGORY_LABEL[catOf(item.tags)]}</span>
                </div>
              </button>
            ))}
          </div>
        </div>
      )}

      {/* Похожие решения (реюз search_memory_items — полнотекст ∪ теги) */}
      {similar.length > 0 && (
        <div className="mt-6">
          <div className="text-sm font-semibold mb-3">Похожие решения</div>
          <div className="grid sm:grid-cols-2 gap-2">
            {similar.map((item) => (
              <button
                key={item.id}
                onClick={() => onSelect(item.id)}
                className="text-left rounded-lg border border-neutral-800 bg-neutral-900/30 hover:border-lime-400/40 p-2.5 transition-colors">
                <div className="flex items-center gap-1.5">
                  <StatusIcon status={statusOf(item.tags)} />
                  <span className="text-[13px] font-sans text-neutral-100 truncate">
                    {item.title || solutionPreview(item.content) || "—"}
                  </span>
                </div>
                <div className="text-[11px] font-sans text-neutral-600 mt-1">
                  {CATEGORY_LABEL[catOf(item.tags)]}
                </div>
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}

function Section({
  title,
  body,
  boxed = false
}: {
  title: string
  body: string
  boxed?: boolean
}) {
  if (!body.trim()) return null
  return (
    <div>
      <div className="text-sm font-semibold mb-2">{title}</div>
      <div
        className={
          boxed
            ? "rounded-lg border border-neutral-800 bg-neutral-900/40 px-4 py-3"
            : ""
        }>
        <Markdown>{body}</Markdown>
      </div>
    </div>
  )
}

function Commands({ title, code }: { title: string; code: string }) {
  const [copied, setCopied] = useState(false)
  async function copy() {
    try {
      await navigator.clipboard.writeText(code.trim())
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    } catch {
      /* clipboard may be unavailable */
    }
  }
  return (
    <div>
      <div className="text-sm font-semibold mb-2">{title}</div>
      <div className="rounded-lg overflow-hidden border border-neutral-800 bg-[#0b0b0f]">
        <div className="flex items-center justify-end px-3 py-1.5 bg-neutral-900/70 border-b border-neutral-800">
          <button
            onClick={copy}
            className="text-[10px] uppercase tracking-wider text-neutral-400 hover:text-lime-400 transition-colors">
            {copied ? "скопировано ✓" : "копировать"}
          </button>
        </div>
        <pre className="px-4 py-3 text-[0.8rem] leading-relaxed text-neutral-200 overflow-x-auto whitespace-pre-wrap break-words">
          {code.trim()}
        </pre>
      </div>
    </div>
  )
}

// ── Иконки ─────────────────────────────────────────────────────────────────────

function StatusIcon({ status }: { status: SolutionStatus }) {
  if (status === "resolved")
    return (
      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" className="text-lime-400">
        <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="2" />
        <path
          d="M8 12l2.5 2.5L16 9"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
        />
      </svg>
    )
  if (status === "in_progress")
    return (
      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" className="text-amber-400">
        <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="2" />
        <path
          d="M12 7v5l3 2"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
        />
      </svg>
    )
  if (status === "deferred")
    return (
      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" className="text-sky-400">
        <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="2" />
        <path
          d="M10 9v6M14 9v6"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
        />
      </svg>
    )
  return (
    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" className="text-red-400">
      <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="2" />
      <path
        d="M9 9l6 6M15 9l-6 6"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
      />
    </svg>
  )
}

function SearchIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="11" cy="11" r="7" />
      <line x1="21" y1="21" x2="16.65" y2="16.65" />
    </svg>
  )
}

function DotGlyph() {
  return (
    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="4" y="4" width="16" height="16" rx="4" />
    </svg>
  )
}

function FolderGlyph() {
  return (
    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
    </svg>
  )
}
