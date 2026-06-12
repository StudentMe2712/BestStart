"use client"

import { useEffect, useMemo, useRef, useState } from "react"
import { useRouter } from "next/navigation"

import {
  listSources,
  ingestArticle,
  ingestYoutube,
  ingestText,
  isYoutubeUrl,
  uploadFile,
  deleteSource,
  generateCourse,
  getCourse,
  type ContentSource
} from "../../lib/api"
import { getCache, setCache } from "../../lib/cache"
import {
  KIND_LABEL,
  kindOf,
  youtubeId,
  fmtDate,
  PlayIcon,
  type Kind
} from "../../lib/material-ui"
import RefreshButton from "../refresh-button"

// Ссылка (http/https одним токеном) → статья/видео; всё остальное — простой текст.
const looksLikeUrl = (s: string) => /^https?:\/\/\S+$/i.test(s.trim())

// Kind / KIND_LABEL / kindOf / youtubeId / fmtDate / PlayIcon — общие хелперы
// из lib/material-ui (раньше дублировались здесь).
type ViewMode = "grid" | "list"
type SortMode = "new" | "old" | "title"

const KIND_BADGE: Record<Kind, string> = {
  youtube: "bg-red-600 text-white",
  pdf: "bg-red-600 text-white",
  article: "bg-sky-600 text-white",
  file: "bg-amber-500 text-neutral-950",
  text: "bg-indigo-500 text-white"
}

const FILTERS: { key: Kind | "all"; label: string }[] = [
  { key: "all", label: "Все" },
  { key: "youtube", label: "YouTube" },
  { key: "pdf", label: "PDF" },
  { key: "article", label: "Статьи" },
  { key: "text", label: "Текст" }
]

export default function LearnPage() {
  const router = useRouter()

  const [sources, setSources] = useState<ContentSource[]>(
    () => getCache<ContentSource[]>("sources") ?? []
  )
  const [loading, setLoading] = useState(
    () => getCache<ContentSource[]>("sources") === undefined
  )
  const [error, setError] = useState<string | null>(null)
  const [tick, setTick] = useState(0)

  const [material, setMaterial] = useState("")
  const [adding, setAdding] = useState(false)
  const fileRef = useRef<HTMLInputElement>(null)

  // sourceId → курс уже сгенерирован (определяется фоном через getCourse).
  const [coursed, setCoursed] = useState<Set<string>>(new Set())
  const [busyId, setBusyId] = useState<string | null>(null)

  const [filter, setFilter] = useState<Kind | "all">("all")
  const [sort, setSort] = useState<SortMode>("new")
  const [view, setView] = useState<ViewMode>("grid")

  useEffect(() => {
    setLoading(true)
    listSources()
      .then((d) => {
        setSources(d)
        setCache("sources", d)
      })
      .catch((e) => setError(String(e)))
      .finally(() => setLoading(false))
  }, [tick])

  // Фоновая проверка: у каких материалов уже есть курс (только у извлечённых).
  useEffect(() => {
    const extracted = sources.filter((s) => s.status === "extracted")
    if (extracted.length === 0) return
    let cancelled = false
    Promise.allSettled(extracted.map((s) => getCourse(s.id))).then((res) => {
      if (cancelled) return
      const set = new Set<string>()
      res.forEach((r, i) => {
        if (r.status === "fulfilled" && r.value) set.add(extracted[i].id)
      })
      setCoursed(set)
    })
    return () => {
      cancelled = true
    }
  }, [sources])

  async function addMaterial() {
    const v = material.trim()
    if (!v) return
    setAdding(true)
    setError(null)
    try {
      if (isYoutubeUrl(v)) await ingestYoutube(v)
      else if (looksLikeUrl(v)) await ingestArticle(v)
      else await ingestText(v)
      setMaterial("")
      setTick((t) => t + 1)
    } catch (e) {
      setError(String(e))
    } finally {
      setAdding(false)
    }
  }

  async function onFile(e: React.ChangeEvent<HTMLInputElement>) {
    const f = e.target.files?.[0]
    if (!f) return
    setAdding(true)
    setError(null)
    try {
      await uploadFile(f)
      setTick((t) => t + 1)
    } catch (err) {
      setError(String(err))
    } finally {
      setAdding(false)
      if (fileRef.current) fileRef.current.value = ""
    }
  }

  async function handleDelete(id: string) {
    try {
      await deleteSource(id)
      setSources((prev) => prev.filter((s) => s.id !== id))
      setCoursed((prev) => {
        const n = new Set(prev)
        n.delete(id)
        return n
      })
    } catch (e) {
      setError(String(e))
    }
  }

  async function handleCreate(id: string) {
    setBusyId(id)
    setError(null)
    try {
      await generateCourse(id)
      setCoursed((prev) => new Set(prev).add(id))
    } catch (e) {
      setError(String(e))
    } finally {
      setBusyId(null)
    }
  }

  function handleOpen(id: string) {
    router.push(`/courses/${id}`)
  }

  const counts = useMemo(() => {
    const c: Record<string, number> = { all: sources.length }
    for (const s of sources) c[kindOf(s)] = (c[kindOf(s)] || 0) + 1
    return c
  }, [sources])

  const filters = useMemo(() => {
    const base = [...FILTERS]
    if ((counts["file"] || 0) > 0) base.push({ key: "file", label: "Файлы" })
    return base
  }, [counts])

  const visible = useMemo(() => {
    const list =
      filter === "all" ? sources : sources.filter((s) => kindOf(s) === filter)
    return [...list].sort((a, b) => {
      if (sort === "title") return (a.title || "").localeCompare(b.title || "", "ru")
      const ta = new Date(a.created_at).getTime()
      const tb = new Date(b.created_at).getTime()
      return sort === "old" ? ta - tb : tb - ta
    })
  }, [sources, filter, sort])

  return (
    <main className="max-w-[1700px] mx-auto px-6 py-8">
      {/* ── Блок добавления (карточка) ─────────────────────────────────────── */}
      <section className="border border-neutral-800 rounded-xl p-6 bg-neutral-950 mb-8">
        <div className="text-xs uppercase tracking-widest text-lime-400 mb-2">
          /// лектор
        </div>
        <div className="flex items-center justify-between gap-3">
          <h1 className="text-3xl font-semibold">Личный лектор</h1>
          <RefreshButton onClick={() => setTick((t) => t + 1)} busy={loading} />
        </div>
        <p className="text-neutral-400 mt-2 text-sm font-sans max-w-3xl">
          Добавляй материалы — ссылки на статьи или YouTube, файлы
          (PDF/DOCX/PPTX/XLSX/TXT/MD/HTML) или вставляй текст. PAM извлечёт его,
          добавит в память и сможет собрать по нему персональный мини-курс с
          тестом.
        </p>

        <div className="mt-5 flex flex-col sm:flex-row gap-3">
          <div className="relative flex-1">
            <span className="absolute left-3 top-1/2 -translate-y-1/2 text-neutral-600">
              <LinkIcon />
            </span>
            <input
              value={material}
              onChange={(e) => setMaterial(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && addMaterial()}
              placeholder="Вставь ссылку на YouTube или статью..."
              className="w-full bg-neutral-900 border border-neutral-800 rounded-lg pl-10 pr-3 py-3 text-sm font-sans focus:outline-none focus:border-lime-400/50"
            />
          </div>
          <button
            onClick={addMaterial}
            disabled={adding || !material.trim()}
            className="shrink-0 px-6 py-3 rounded-lg bg-lime-400 text-neutral-950 font-medium text-sm hover:bg-lime-300 disabled:opacity-50 disabled:cursor-not-allowed transition-colors">
            {adding ? "Добавляю…" : "Добавить материал"}
          </button>
        </div>

        <div className="mt-3 flex items-center gap-3 text-sm font-sans text-neutral-400 flex-wrap">
          <span>или загрузите файл:</span>
          <label className="inline-flex items-center gap-2 cursor-pointer text-sm px-4 py-2 rounded-lg border border-neutral-700 text-neutral-200 hover:border-lime-400/50 hover:text-lime-400 transition-colors">
            <UploadIcon />
            Выбрать файл
            <input
              ref={fileRef}
              type="file"
              accept=".pdf,.docx,.pptx,.xlsx,.xls,.txt,.md,.markdown,.html,.htm,.csv,.json,.log,.rst"
              onChange={onFile}
              disabled={adding}
              className="hidden"
            />
          </label>
          <span className="text-[10px] text-neutral-600">
            PDF · DOCX · PPTX · XLSX · TXT · MD · HTML
          </span>
        </div>
      </section>

      {/* ── Материалы ──────────────────────────────────────────────────────── */}
      <section>
        <h2 className="text-2xl font-semibold mb-4">Материалы</h2>

        <div className="flex items-center justify-between gap-3 flex-wrap mb-6">
          <div className="flex items-center gap-2 flex-wrap">
            {filters.map((f) => {
              const active = filter === f.key
              return (
                <button
                  key={f.key}
                  onClick={() => setFilter(f.key)}
                  className={`flex items-center gap-2 text-sm font-sans px-3 py-1.5 rounded-lg border transition-colors ${
                    active
                      ? "border-lime-400/50 text-lime-400 bg-lime-400/5"
                      : "border-neutral-800 text-neutral-400 hover:border-neutral-700 hover:text-neutral-200"
                  }`}>
                  {f.label}
                  <span
                    className={`text-[11px] tabular-nums ${
                      active ? "text-lime-400/80" : "text-neutral-600"
                    }`}>
                    {counts[f.key] || 0}
                  </span>
                </button>
              )
            })}
          </div>

          <div className="flex items-center gap-2">
            <select
              value={sort}
              onChange={(e) => setSort(e.target.value as SortMode)}
              className="text-sm font-sans bg-neutral-900 border border-neutral-800 rounded-lg px-3 py-1.5 text-neutral-300 focus:outline-none focus:border-lime-400/50 cursor-pointer">
              <option value="new">По дате добавления</option>
              <option value="old">Сначала старые</option>
              <option value="title">По названию</option>
            </select>
            <div className="flex border border-neutral-800 rounded-lg overflow-hidden">
              <button
                onClick={() => setView("grid")}
                title="Сетка"
                className={`p-2 transition-colors ${
                  view === "grid"
                    ? "bg-lime-400/10 text-lime-400"
                    : "text-neutral-500 hover:text-neutral-200"
                }`}>
                <GridIcon />
              </button>
              <button
                onClick={() => setView("list")}
                title="Список"
                className={`p-2 transition-colors border-l border-neutral-800 ${
                  view === "list"
                    ? "bg-lime-400/10 text-lime-400"
                    : "text-neutral-500 hover:text-neutral-200"
                }`}>
                <ListIcon />
              </button>
            </div>
          </div>
        </div>

        {error && (
          <div className="text-red-400 text-sm font-sans mb-4">Ошибка: {error}</div>
        )}

        {loading && sources.length === 0 ? (
          <div
            className="grid gap-5"
            style={{ gridTemplateColumns: "repeat(auto-fill, minmax(440px, 1fr))" }}>
            {Array.from({ length: 6 }).map((_, i) => (
              <div
                key={i}
                className="h-[360px] rounded-xl border border-neutral-800 bg-neutral-900/40 animate-pulse"
              />
            ))}
          </div>
        ) : visible.length === 0 ? (
          <div className="text-neutral-500 text-sm py-16 border border-dashed border-neutral-800 rounded-xl text-center font-sans">
            {sources.length === 0
              ? "Пусто. Добавь первую ссылку, файл или текст выше."
              : "Нет материалов в этом фильтре."}
          </div>
        ) : view === "grid" ? (
          <div
            className="grid gap-5"
            style={{ gridTemplateColumns: "repeat(auto-fill, minmax(440px, 1fr))" }}>
            {visible.map((s) => (
              <MaterialCard
                key={s.id}
                source={s}
                hasCourse={coursed.has(s.id)}
                busy={busyId === s.id}
                onOpen={() => handleOpen(s.id)}
                onCreate={() => handleCreate(s.id)}
                onDelete={() => handleDelete(s.id)}
              />
            ))}
          </div>
        ) : (
          <div className="flex flex-col gap-2">
            {visible.map((s) => (
              <MaterialRow
                key={s.id}
                source={s}
                hasCourse={coursed.has(s.id)}
                busy={busyId === s.id}
                onOpen={() => handleOpen(s.id)}
                onCreate={() => handleCreate(s.id)}
                onDelete={() => handleDelete(s.id)}
              />
            ))}
          </div>
        )}
      </section>
    </main>
  )
}

// ── Карточка материала (grid) ─────────────────────────────────────────────

interface CardProps {
  source: ContentSource
  hasCourse: boolean
  busy: boolean
  onOpen: () => void
  onCreate: () => void
  onDelete: () => void
}

function MaterialCard({
  source,
  hasCourse,
  busy,
  onOpen,
  onCreate,
  onDelete
}: CardProps) {
  const kind = kindOf(source)
  const canCreate = source.status === "extracted"
  const size =
    kind !== "youtube" && source.char_count > 0
      ? `${source.char_count.toLocaleString("ru")} симв.`
      : null

  return (
    <div className="group relative flex flex-col rounded-xl border border-neutral-800 bg-neutral-900/40 hover:border-neutral-700 transition-colors">
      <div className="relative h-[230px] overflow-hidden rounded-t-xl">
        <Preview source={source} />
        <div className="absolute top-3 left-3">
          <KindBadge kind={kind} />
        </div>
      </div>

      <div className="flex flex-col gap-3 p-4 flex-1">
        <h3 className="text-sm font-sans font-semibold leading-snug line-clamp-2">
          {source.title || source.url || "(без названия)"}
        </h3>

        <div className="text-[11px] text-neutral-500 font-sans flex items-center gap-1.5 flex-wrap">
          <span>{KIND_LABEL[kind]}</span>
          {size && <span>· {size}</span>}
          <span>· {fmtDate(source.created_at)}</span>
        </div>

        <div className="mt-auto flex items-center justify-between gap-2 pt-1">
          <StatusPill source={source} hasCourse={hasCourse} busy={busy} />
          <div className="flex items-center gap-1.5">
            <ActionButton
              hasCourse={hasCourse}
              busy={busy}
              canCreate={canCreate}
              onOpen={onOpen}
              onCreate={onCreate}
            />
            <CardMenu
              hasCourse={hasCourse}
              canCreate={canCreate}
              onOpen={onOpen}
              onCreate={onCreate}
              onDelete={onDelete}
            />
          </div>
        </div>
      </div>
    </div>
  )
}

// ── Строка материала (list) ───────────────────────────────────────────────

function MaterialRow({
  source,
  hasCourse,
  busy,
  onOpen,
  onCreate,
  onDelete
}: CardProps) {
  const kind = kindOf(source)
  const canCreate = source.status === "extracted"
  const size =
    kind !== "youtube" && source.char_count > 0
      ? `${source.char_count.toLocaleString("ru")} симв.`
      : null

  return (
    <div className="flex items-center gap-4 rounded-xl border border-neutral-800 bg-neutral-900/40 hover:border-neutral-700 transition-colors p-3">
      <div className="relative w-40 h-24 shrink-0 overflow-hidden rounded-lg">
        <Preview source={source} compact />
        <div className="absolute top-1.5 left-1.5">
          <KindBadge kind={kind} small />
        </div>
      </div>

      <div className="min-w-0 flex-1">
        <h3 className="text-sm font-sans font-semibold leading-snug line-clamp-1">
          {source.title || source.url || "(без названия)"}
        </h3>
        <div className="mt-1 text-[11px] text-neutral-500 font-sans flex items-center gap-1.5 flex-wrap">
          <span>{KIND_LABEL[kind]}</span>
          {size && <span>· {size}</span>}
          <span>· {fmtDate(source.created_at)}</span>
        </div>
        <div className="mt-2">
          <StatusPill source={source} hasCourse={hasCourse} busy={busy} />
        </div>
      </div>

      <div className="flex items-center gap-1.5 shrink-0">
        <ActionButton
          hasCourse={hasCourse}
          busy={busy}
          canCreate={canCreate}
          onOpen={onOpen}
          onCreate={onCreate}
        />
        <CardMenu
          hasCourse={hasCourse}
          canCreate={canCreate}
          onOpen={onOpen}
          onCreate={onCreate}
          onDelete={onDelete}
        />
      </div>
    </div>
  )
}

// ── Превью по типам ───────────────────────────────────────────────────────

function Preview({
  source,
  compact = false
}: {
  source: ContentSource
  compact?: boolean
}) {
  const kind = kindOf(source)
  const ytId = kind === "youtube" ? youtubeId(source.url) : null
  const [imgOk, setImgOk] = useState(true)

  if (ytId && imgOk) {
    return (
      <>
        {/* eslint-disable-next-line @next/next/no-img-element */}
        <img
          src={`https://i.ytimg.com/vi/${ytId}/hqdefault.jpg`}
          alt=""
          className="h-full w-full object-cover"
          onError={() => setImgOk(false)}
        />
        {!compact && (
          <div className="absolute inset-0 flex items-center justify-center">
            <span className="grid place-items-center w-12 h-12 rounded-full bg-black/55 text-white">
              <PlayIcon />
            </span>
          </div>
        )}
      </>
    )
  }

  if (kind === "text") {
    return (
      <div className="h-full w-full bg-gradient-to-br from-neutral-800 to-neutral-950 p-5 flex flex-col justify-center gap-2.5">
        {[92, 78, 88, 64, 84, 56].map((w, i) => (
          <div
            key={i}
            className="h-2.5 rounded bg-neutral-700/60"
            style={{ width: `${w}%` }}
          />
        ))}
      </div>
    )
  }

  if (kind === "pdf" || kind === "file") {
    return (
      <div className="h-full w-full bg-gradient-to-br from-neutral-800 to-neutral-950 grid place-items-center text-neutral-600">
        <DocIcon />
      </div>
    )
  }

  // article + youtube-fallback
  return (
    <div className="h-full w-full bg-gradient-to-br from-neutral-800 via-neutral-900 to-neutral-950 grid place-items-center text-neutral-600">
      <GlobeIcon />
    </div>
  )
}

function KindBadge({ kind, small = false }: { kind: Kind; small?: boolean }) {
  return (
    <span
      className={`inline-flex items-center gap-1 rounded-md font-semibold uppercase tracking-wide ${
        small ? "text-[8px] px-1.5 py-0.5" : "text-[10px] px-2 py-1"
      } ${KIND_BADGE[kind]}`}>
      {kind === "youtube" && <PlayIcon small />}
      {KIND_LABEL[kind]}
    </span>
  )
}

function StatusPill({
  source,
  hasCourse,
  busy
}: {
  source: ContentSource
  hasCourse: boolean
  busy: boolean
}) {
  let label = "Ошибка"
  let dot = "bg-red-400"
  let text = "text-red-400"
  if (busy || source.status === "pending") {
    label = "Обрабатывается"
    dot = "bg-sky-400"
    text = "text-sky-400"
  } else if (hasCourse) {
    label = "Курс создан"
    dot = "bg-lime-400"
    text = "text-lime-400"
  } else if (source.status === "extracted") {
    label = "Готов к обработке"
    dot = "bg-yellow-400"
    text = "text-yellow-400"
  }
  return (
    <span className={`inline-flex items-center gap-2 text-[12px] font-sans ${text}`}>
      <span className={`w-2 h-2 rounded-full ${dot}`} />
      {label}
    </span>
  )
}

function ActionButton({
  hasCourse,
  busy,
  canCreate,
  onOpen,
  onCreate
}: {
  hasCourse: boolean
  busy: boolean
  canCreate: boolean
  onOpen: () => void
  onCreate: () => void
}) {
  if (hasCourse) {
    return (
      <button
        onClick={onOpen}
        className="text-sm px-4 py-2 rounded-lg border border-neutral-700 text-neutral-200 hover:text-lime-400 hover:border-lime-400/50 transition-colors">
        Открыть курс
      </button>
    )
  }
  return (
    <button
      onClick={onCreate}
      disabled={busy || !canCreate}
      title={canCreate ? "" : "у материала нет извлечённого текста"}
      className="text-sm px-4 py-2 rounded-lg border border-neutral-700 text-neutral-200 hover:text-lime-400 hover:border-lime-400/50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors">
      {busy ? "Создаю…" : "Создать курс"}
    </button>
  )
}

function CardMenu({
  hasCourse,
  canCreate,
  onOpen,
  onCreate,
  onDelete
}: {
  hasCourse: boolean
  canCreate: boolean
  onOpen: () => void
  onCreate: () => void
  onDelete: () => void
}) {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  useEffect(() => {
    if (!open) return
    const h = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener("mousedown", h)
    return () => document.removeEventListener("mousedown", h)
  }, [open])

  return (
    <div className="relative" ref={ref}>
      <button
        onClick={() => setOpen((o) => !o)}
        title="Меню"
        className="grid place-items-center w-9 h-9 rounded-lg border border-neutral-800 text-neutral-400 hover:text-neutral-100 hover:border-neutral-700 transition-colors">
        <DotsIcon />
      </button>
      {open && (
        <div className="absolute right-0 bottom-full mb-1 w-44 bg-neutral-900 border border-neutral-800 rounded-lg shadow-xl py-1 z-30 text-sm font-sans">
          {hasCourse ? (
            <MenuItem
              onClick={() => {
                setOpen(false)
                onOpen()
              }}>
              Открыть курс
            </MenuItem>
          ) : (
            <MenuItem
              disabled={!canCreate}
              onClick={() => {
                setOpen(false)
                onCreate()
              }}>
              Создать курс
            </MenuItem>
          )}
          <MenuItem
            danger
            onClick={() => {
              setOpen(false)
              onDelete()
            }}>
            Удалить
          </MenuItem>
        </div>
      )}
    </div>
  )
}

function MenuItem({
  children,
  onClick,
  danger = false,
  disabled = false
}: {
  children: React.ReactNode
  onClick: () => void
  danger?: boolean
  disabled?: boolean
}) {
  return (
    <button
      onClick={onClick}
      disabled={disabled}
      className={`w-full text-left px-3 py-2 transition-colors disabled:opacity-40 disabled:cursor-not-allowed ${
        danger
          ? "text-red-400 hover:bg-red-400/10"
          : "text-neutral-200 hover:bg-neutral-800"
      }`}>
      {children}
    </button>
  )
}

// ── Иконки (inline SVG) ───────────────────────────────────────────────────

function LinkIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71" />
      <path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71" />
    </svg>
  )
}

function UploadIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
      <polyline points="17 8 12 3 7 8" />
      <line x1="12" y1="3" x2="12" y2="15" />
    </svg>
  )
}

function DotsIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor">
      <circle cx="12" cy="5" r="1.6" />
      <circle cx="12" cy="12" r="1.6" />
      <circle cx="12" cy="19" r="1.6" />
    </svg>
  )
}

function GridIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="3" y="3" width="7" height="7" rx="1" />
      <rect x="14" y="3" width="7" height="7" rx="1" />
      <rect x="3" y="14" width="7" height="7" rx="1" />
      <rect x="14" y="14" width="7" height="7" rx="1" />
    </svg>
  )
}

function ListIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <line x1="8" y1="6" x2="21" y2="6" />
      <line x1="8" y1="12" x2="21" y2="12" />
      <line x1="8" y1="18" x2="21" y2="18" />
      <line x1="3" y1="6" x2="3.01" y2="6" />
      <line x1="3" y1="12" x2="3.01" y2="12" />
      <line x1="3" y1="18" x2="3.01" y2="18" />
    </svg>
  )
}

function DocIcon() {
  return (
    <svg width="56" height="56" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
      <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
      <polyline points="14 2 14 8 20 8" />
      <line x1="8" y1="13" x2="16" y2="13" />
      <line x1="8" y1="17" x2="13" y2="17" />
    </svg>
  )
}

function GlobeIcon() {
  return (
    <svg width="56" height="56" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="9" />
      <line x1="3" y1="12" x2="21" y2="12" />
      <path d="M12 3a14 14 0 0 1 0 18 14 14 0 0 1 0-18z" />
    </svg>
  )
}
