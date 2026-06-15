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
  listProjects,
  type MemoryItem,
  type Project
} from "../../lib/api"
import { ITEM_TYPE_OPTIONS, itemTypeLabel, itemPreview } from "../../lib/knowledge"
import { freeTags } from "../../lib/solutions"
import { fmtDate } from "../../lib/material-ui"
import Markdown from "../markdown"
import GenericEditor from "./generic-editor"

/**
 * Линза «Все» Knowledge Hub — generic-браузер по ВСЕМ memory_items (любой
 * item_type). Раскрывает уже существующий слой знаний (в т.ч. Telegram Capture) и
 * даёт лёгкое редактирование. Реюз бэкенда целиком: GET /memory/items (фильтр по
 * типу/полнотекст), PATCH/DELETE /memory/items, GET /memory/links (только просмотр).
 * Solution здесь — обычная карточка; его специализированная карточка живёт в линзе
 * «Решения» (клик уводит туда через onOpenSolution). Структурно зеркалит solutions.
 */

const PAGE_SIZE = 20
const SORTS: { key: SortKey; label: string }[] = [
  { key: "new", label: "Сначала новые" },
  { key: "old", label: "Сначала старые" },
  { key: "importance", label: "По важности" }
]
type SortKey = "new" | "old" | "importance"

export default function AllLens({
  onOpenSolution
}: {
  onOpenSolution: (id: string) => void
}) {
  const [items, setItems] = useState<MemoryItem[]>([])
  const [projects, setProjects] = useState<Project[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const [typeFilter, setTypeFilter] = useState<string>("")
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
      // Все активные элементы всех типов (бэкенд капит limit ≤ 200; для личной
      // базы знаний этого достаточно в V1 — серверная пагинация в V1.1).
      const data = await listMemoryItems({ status: "active", limit: 200 })
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

  // ⌘/Ctrl+K фокусирует поиск.
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

  // Счётчики по типам (по всем активным, без учёта текущих фильтров).
  const typeCounts = useMemo(() => {
    const c: Record<string, number> = {}
    for (const it of items) c[it.item_type] = (c[it.item_type] || 0) + 1
    return c
  }, [items])

  const filtered = useMemo(() => {
    const needle = q.trim().toLowerCase()
    let list = items.filter((it) => {
      if (typeFilter && it.item_type !== typeFilter) return false
      if (needle) {
        const hay = `${it.title || ""} ${it.summary || ""} ${it.content} ${it.tags.join(
          " "
        )}`.toLowerCase()
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
  }, [items, typeFilter, q, sort])

  useEffect(() => setPage(0), [typeFilter, q, sort])

  const pageCount = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE))
  const curPage = Math.min(page, pageCount - 1)
  const pageItems = filtered.slice(curPage * PAGE_SIZE, curPage * PAGE_SIZE + PAGE_SIZE)

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

  function editItem(it: MemoryItem) {
    // Solution редактируется своим специализированным редактором в линзе «Решения».
    if (it.item_type === "solution") {
      onOpenSolution(it.id)
      return
    }
    setCreating(false)
    setEditing(it)
  }

  async function archive(it: MemoryItem) {
    try {
      await patchMemoryItem(it.id, { status: "archived" })
      // Архивный больше не в active-списке — убираем локально.
      setItems((prev) => prev.filter((x) => x.id !== it.id))
    } catch (e) {
      setError(String(e))
    }
  }

  async function remove(it: MemoryItem) {
    if (!window.confirm(`Удалить «${it.title || itemPreview(it.content) || "без названия"}»?`))
      return
    try {
      await deleteMemoryItem(it.id)
      setItems((prev) => prev.filter((x) => x.id !== it.id))
    } catch (e) {
      setError(String(e))
    }
  }

  return (
    <div className="h-full flex flex-col">
      {/* Тулбар: заголовок · поиск (⌘K) · новый элемент */}
      <div className="shrink-0 flex items-center gap-4 px-6 h-16 border-b border-neutral-800">
        <div className="flex items-baseline gap-2.5">
          <h1 className="text-lg font-semibold">Все знания</h1>
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
            placeholder="Поиск по знаниям…"
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
          + Заметка
        </button>
      </div>

      <div className="flex-1 flex overflow-hidden">
        {/* ── Сайдбар: типы + недавние ── */}
        <aside className="hidden lg:flex w-64 shrink-0 flex-col overflow-y-auto border-r border-neutral-800 px-4 py-5 gap-6">
          <FilterGroup title="Типы">
            <FilterRow
              active={typeFilter === ""}
              onClick={() => setTypeFilter("")}
              label="Все"
              count={items.length}
            />
            {ITEM_TYPE_OPTIONS.map((o) => (
              <FilterRow
                key={o.type}
                active={typeFilter === o.type}
                onClick={() => setTypeFilter(typeFilter === o.type ? "" : o.type)}
                label={o.label}
                count={typeCounts[o.type] || 0}
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
                  {it.title || itemPreview(it.content) || "—"}
                </button>
              ))}
            </FilterGroup>
          )}
        </aside>

        {/* ── Список карточек ── */}
        <section className="w-full md:w-[380px] shrink-0 flex flex-col overflow-hidden border-r border-neutral-800">
          <div className="shrink-0 flex items-center justify-between px-4 h-12 border-b border-neutral-800">
            <span className="text-sm font-sans text-neutral-300">
              {typeFilter ? itemTypeLabel(typeFilter) : "Все знания"}
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
              <div className="text-neutral-500 text-sm py-8 text-center">// загрузка…</div>
            ) : filtered.length === 0 ? (
              <div className="text-neutral-500 text-[13px] font-sans py-10 px-3 border border-dashed border-neutral-800 rounded-lg text-center">
                {items.length === 0
                  ? "Пока нет знаний. Пришли сообщение в Telegram-бот, сохрани решение из чата или нажми «+ Заметка»."
                  : "Ничего не найдено по фильтрам."}
              </div>
            ) : (
              pageItems.map((it) => (
                <ItemCard
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
              onEdit={() => editItem(selected)}
              onArchive={() => archive(selected)}
              onDelete={() => remove(selected)}
              onOpenSolution={onOpenSolution}
              onSelect={setSelectedId}
            />
          ) : (
            <div className="h-full grid place-items-center text-neutral-600 text-sm font-sans px-6 text-center">
              {items.length === 0
                ? "Здесь появятся твои знания — из Telegram, чата или созданные вручную."
                : "Выбери элемент слева."}
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
        <GenericEditor
          item={editing}
          onClose={() => {
            setEditing(null)
            setCreating(false)
          }}
          onSaved={onSaved}
          onError={setError}
        />
      )}
    </div>
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
  count
}: {
  active: boolean
  onClick: () => void
  label: string
  count: number
}) {
  return (
    <button
      onClick={onClick}
      className={`w-full flex items-center gap-2.5 text-sm font-sans px-2 py-1.5 rounded-md transition-colors ${
        active
          ? "bg-neutral-900 text-neutral-100"
          : "text-neutral-400 hover:text-neutral-100 hover:bg-neutral-900"
      }`}>
      <span className="truncate flex-1 text-left">{label}</span>
      <span className="text-[11px] tabular-nums text-neutral-600 shrink-0">{count}</span>
    </button>
  )
}

// ── Карточка списка ─────────────────────────────────────────────────────────────

function ItemCard({
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
  const tags = freeTags(it.tags).slice(0, 3)
  return (
    <button
      onClick={onClick}
      className={`w-full text-left rounded-lg border p-3 transition-colors ${
        active
          ? "border-lime-400/50 bg-lime-400/[0.04]"
          : "border-neutral-800 bg-neutral-900/30 hover:border-neutral-700"
      }`}>
      <div className="flex items-center gap-1.5">
        <span className="text-[10px] font-sans px-1.5 py-0.5 rounded border border-neutral-700 text-neutral-400 shrink-0">
          {itemTypeLabel(it.item_type)}
        </span>
        {it.importance >= 4 && (
          <span className="text-[10px] font-sans text-amber-400/80 shrink-0">
            ★{it.importance}
          </span>
        )}
      </div>
      <div className="text-sm font-sans text-neutral-100 break-words line-clamp-2 mt-1.5">
        {it.title || itemPreview(it.content) || "—"}
      </div>
      {tags.length > 0 && (
        <div className="flex items-center gap-1.5 flex-wrap mt-1.5">
          {tags.map((t) => (
            <span
              key={t}
              className="text-[10px] font-sans px-1.5 py-0.5 rounded border border-neutral-800 text-neutral-500">
              {t}
            </span>
          ))}
        </div>
      )}
      <div className="flex items-center gap-2 mt-1.5 text-[11px] font-sans text-neutral-600">
        <span className="tabular-nums">{fmtDate(it.created_at)}</span>
        {projectName && (
          <>
            <span>·</span>
            <span className="truncate">{projectName}</span>
          </>
        )}
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
  onArchive,
  onDelete,
  onOpenSolution,
  onSelect
}: {
  it: MemoryItem
  projectName: string | null
  allItems: MemoryItem[]
  onEdit: () => void
  onArchive: () => void
  onDelete: () => void
  onOpenSolution: (id: string) => void
  onSelect: (id: string) => void
}) {
  const isSolution = it.item_type === "solution"
  const tags = freeTags(it.tags)

  // Связанные элементы — из memory_links (1 hop, только просмотр), резолвим к
  // загруженным элементам. Создание/удаление связей в V1 нет.
  const [related, setRelated] = useState<{ item: MemoryItem; relation: string }[]>([])
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

  return (
    <div className="max-w-3xl mx-auto px-8 py-7">
      {/* Заголовок + действия */}
      <div className="flex items-start justify-between gap-4">
        <h2 className="text-2xl font-semibold break-words">
          {it.title || itemPreview(it.content) || "Без названия"}
        </h2>
        <div className="flex items-center gap-2 shrink-0">
          {isSolution ? (
            <button
              onClick={() => onOpenSolution(it.id)}
              className="text-xs font-sans px-3 py-1.5 rounded-md border border-neutral-700 text-neutral-300 hover:text-lime-400 hover:border-lime-400/50 transition-colors">
              Открыть карточку решения →
            </button>
          ) : (
            <>
              <button
                onClick={onEdit}
                className="text-xs font-sans px-3 py-1.5 rounded-md border border-neutral-700 text-neutral-300 hover:text-lime-400 hover:border-lime-400/50 transition-colors">
                ✎ Редактировать
              </button>
              <button
                onClick={onArchive}
                title="В архив"
                className="text-xs font-sans px-2.5 py-1.5 rounded-md border border-neutral-800 text-neutral-500 hover:text-neutral-200 hover:border-neutral-600 transition-colors">
                В архив
              </button>
              <button
                onClick={onDelete}
                title="Удалить"
                className="text-xs font-sans px-2.5 py-1.5 rounded-md border border-neutral-800 text-neutral-500 hover:text-red-400 hover:border-red-400/40 transition-colors">
                Удалить
              </button>
            </>
          )}
        </div>
      </div>

      {/* Мета: тип · источник · дата · проект · важность */}
      <div className="flex items-center gap-3 flex-wrap mt-3 text-[13px] font-sans text-neutral-500">
        <span className="text-neutral-300">{itemTypeLabel(it.item_type)}</span>
        <span className="text-neutral-700">·</span>
        <span>{it.source}</span>
        <span className="text-neutral-700">·</span>
        <span className="tabular-nums">{fmtDate(it.created_at)}</span>
        {projectName && (
          <>
            <span className="text-neutral-700">·</span>
            <span>{projectName}</span>
          </>
        )}
        {it.importance >= 4 && (
          <>
            <span className="text-neutral-700">·</span>
            <span className="text-amber-400/80">важность {it.importance}</span>
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

      {isSolution && (
        <div className="mt-4 text-[12px] font-sans text-neutral-500 rounded-lg border border-neutral-800 bg-neutral-900/40 px-3 py-2">
          Это решение. Статус, секции и похожие решения — в специализированной
          карточке (раздел «Решения»).
        </div>
      )}

      {/* Содержимое (markdown) */}
      <div className="mt-6">
        <Markdown>{it.content || ""}</Markdown>
      </div>

      {/* Связанные элементы (из memory_links, только просмотр) */}
      {related.length > 0 && (
        <div className="mt-8 pt-6 border-t border-neutral-800">
          <div className="text-sm font-semibold mb-3">Связанные элементы</div>
          <div className="grid sm:grid-cols-2 gap-2">
            {related.map(({ item, relation }) => (
              <button
                key={item.id}
                onClick={() => onSelect(item.id)}
                className="text-left rounded-lg border border-neutral-800 bg-neutral-900/30 hover:border-lime-400/40 p-2.5 transition-colors">
                <div className="flex items-center gap-1.5">
                  <span className="text-[10px] font-sans px-1.5 py-0.5 rounded border border-neutral-700 text-neutral-400 shrink-0">
                    {itemTypeLabel(item.item_type)}
                  </span>
                  <span className="text-[13px] font-sans text-neutral-100 truncate">
                    {item.title || itemPreview(item.content) || "—"}
                  </span>
                </div>
                <div className="text-[11px] font-sans text-pink-400 mt-1">{relation}</div>
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}

// ── Иконки ─────────────────────────────────────────────────────────────────────

function SearchIcon() {
  return (
    <svg
      width="16"
      height="16"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round">
      <circle cx="11" cy="11" r="7" />
      <line x1="21" y1="21" x2="16.65" y2="16.65" />
    </svg>
  )
}
