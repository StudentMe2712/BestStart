"use client"

import { useEffect, useMemo, useRef, useState } from "react"
import Link from "next/link"
import { useRouter } from "next/navigation"

import {
  listSources,
  getCourse,
  deleteSource,
  type ContentSource,
  type Course
} from "../../lib/api"
import {
  COURSE_STATUSES,
  getCourseStatus,
  statusMeta,
  type CourseStatus
} from "../../lib/course-status"
import {
  Preview,
  KindBadge,
  kindOf,
  fmtDate,
  KIND_LABEL,
  GridIcon,
  ListIcon,
  DotsIcon
} from "../../lib/material-ui"
import RefreshButton from "../refresh-button"

interface CourseItem {
  source: ContentSource
  course: Course
}

type SortMode = "new" | "old" | "title"
type ViewMode = "grid" | "list"

export default function CoursesPage() {
  const router = useRouter()

  const [items, setItems] = useState<CourseItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [tick, setTick] = useState(0)
  const [statuses, setStatuses] = useState<Record<string, CourseStatus>>({})

  const [filter, setFilter] = useState<CourseStatus | "all">("all")
  const [sort, setSort] = useState<SortMode>("new")
  const [view, setView] = useState<ViewMode>("grid")

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    listSources()
      .then(async (sources) => {
        const extracted = sources.filter((s) => s.status === "extracted")
        const res = await Promise.allSettled(extracted.map((s) => getCourse(s.id)))
        if (cancelled) return
        const list: CourseItem[] = []
        res.forEach((r, i) => {
          if (r.status === "fulfilled" && r.value)
            list.push({ source: extracted[i], course: r.value })
        })
        setItems(list)
      })
      .catch((e) => setError(String(e)))
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [tick])

  // Статусы — из localStorage, после того как известен список курсов.
  useEffect(() => {
    const m: Record<string, CourseStatus> = {}
    items.forEach((it) => {
      m[it.source.id] = getCourseStatus(it.source.id)
    })
    setStatuses(m)
  }, [items])

  async function handleDelete(id: string) {
    if (!window.confirm("Удалить курс и его материал? Действие необратимо.")) return
    try {
      await deleteSource(id)
      setItems((prev) => prev.filter((it) => it.source.id !== id))
    } catch (e) {
      setError(String(e))
    }
  }

  const counts = useMemo(() => {
    const c: Record<string, number> = { all: items.length }
    for (const it of items) {
      const st = statuses[it.source.id] || "new"
      c[st] = (c[st] || 0) + 1
    }
    return c
  }, [items, statuses])

  const visible = useMemo(() => {
    const list =
      filter === "all"
        ? items
        : items.filter((it) => (statuses[it.source.id] || "new") === filter)
    return [...list].sort((a, b) => {
      if (sort === "title")
        return (a.course.title || a.source.title || "").localeCompare(
          b.course.title || b.source.title || "",
          "ru"
        )
      const ta = new Date(a.course.created_at).getTime()
      const tb = new Date(b.course.created_at).getTime()
      return sort === "old" ? ta - tb : tb - ta
    })
  }, [items, statuses, filter, sort])

  const filters: { key: CourseStatus | "all"; label: string }[] = [
    { key: "all", label: "Все" },
    ...COURSE_STATUSES.map((s) => ({ key: s.key, label: s.label }))
  ]

  return (
    <main className="max-w-[1700px] mx-auto px-6 py-8">
      <header className="mb-6">
        <div className="text-xs uppercase tracking-widest text-lime-400 mb-2">
          /// лектор
        </div>
        <div className="flex items-start justify-between gap-4 flex-wrap">
          <div>
            <h1 className="text-3xl font-semibold">Курсы</h1>
            <p className="text-neutral-400 mt-2 text-sm font-sans max-w-2xl">
              Персональные мини-курсы, созданные на основе ваших материалов.
              Изучайте, повторяйте и закрепляйте знания в удобном темпе.
            </p>
          </div>
          <div className="flex items-center gap-3">
            <RefreshButton onClick={() => setTick((t) => t + 1)} busy={loading} />
            <Link
              href="/learn"
              className="inline-flex items-center gap-2 px-5 py-2.5 rounded-lg bg-lime-400 text-neutral-950 font-medium text-sm hover:bg-lime-300 transition-colors">
              Создать курс из материала
              <span className="text-lg leading-none">+</span>
            </Link>
          </div>
        </div>
      </header>

      {/* Легенда статусов */}
      <div className="flex items-center gap-4 flex-wrap rounded-xl border border-neutral-800 bg-neutral-950 px-4 py-3 mb-6">
        <span className="text-xs uppercase tracking-widest text-neutral-500">
          Статусы курсов
        </span>
        {COURSE_STATUSES.map((s) => (
          <span
            key={s.key}
            className="inline-flex items-center gap-2 text-[12px] font-sans text-neutral-400">
            <span className={`w-2 h-2 rounded-full ${s.dot}`} />
            {s.label}
          </span>
        ))}
      </div>

      {/* Фильтры + сортировка + вид */}
      <div className="flex items-center justify-between gap-3 flex-wrap mb-6">
        <div className="flex items-center gap-2 flex-wrap">
          {filters.map((f) => {
            const active = filter === f.key
            const dot =
              f.key === "all" ? null : statusMeta(f.key as CourseStatus).dot
            return (
              <button
                key={f.key}
                onClick={() => setFilter(f.key)}
                className={`flex items-center gap-2 text-sm font-sans px-3 py-1.5 rounded-lg border transition-colors ${
                  active
                    ? "border-lime-400/50 text-lime-400 bg-lime-400/5"
                    : "border-neutral-800 text-neutral-400 hover:border-neutral-700 hover:text-neutral-200"
                }`}>
                {dot && <span className={`w-2 h-2 rounded-full ${dot}`} />}
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
            <option value="new">По дате создания</option>
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

      {loading ? (
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
          {items.length === 0 ? (
            <>
              Курсов пока нет. Создай первый из материала на странице{" "}
              <Link href="/learn" className="text-lime-400 hover:underline">
                Лектор
              </Link>
              .
            </>
          ) : (
            "Нет курсов с этим статусом."
          )}
        </div>
      ) : view === "grid" ? (
        <div
          className="grid gap-5"
          style={{ gridTemplateColumns: "repeat(auto-fill, minmax(440px, 1fr))" }}>
          {visible.map((it) => (
            <CourseCard
              key={it.source.id}
              item={it}
              status={statuses[it.source.id] || "new"}
              onOpen={() => router.push(`/courses/${it.source.id}`)}
              onDelete={() => handleDelete(it.source.id)}
            />
          ))}
        </div>
      ) : (
        <div className="flex flex-col gap-2">
          {visible.map((it) => (
            <CourseRow
              key={it.source.id}
              item={it}
              status={statuses[it.source.id] || "new"}
              onOpen={() => router.push(`/courses/${it.source.id}`)}
              onDelete={() => handleDelete(it.source.id)}
            />
          ))}
        </div>
      )}
    </main>
  )
}

// ── Карточка курса ────────────────────────────────────────────────────────

interface CardProps {
  item: CourseItem
  status: CourseStatus
  onOpen: () => void
  onDelete: () => void
}

function counts(item: CourseItem) {
  const chapters = item.course.data.modules?.length || 0
  const questions = item.course.data.quiz?.length || 0
  return { chapters, questions }
}

function CourseCard({ item, status, onOpen, onDelete }: CardProps) {
  const { source, course } = item
  const kind = kindOf(source)
  const { chapters, questions } = counts(item)
  const sm = statusMeta(status)

  return (
    <div className="group relative flex flex-col rounded-xl border border-neutral-800 bg-neutral-900/40 hover:border-neutral-700 transition-colors">
      <button
        onClick={onOpen}
        className="relative h-[230px] overflow-hidden rounded-t-xl text-left">
        <Preview kind={kind} url={source.url} />
        <div className="absolute top-3 left-3">
          <KindBadge kind={kind} />
        </div>
      </button>

      <div className="flex flex-col gap-3 p-4 flex-1">
        <h3 className="text-sm font-sans font-semibold leading-snug line-clamp-2">
          {course.title || source.title || source.url || "Курс"}
        </h3>

        <div className="text-[11px] text-neutral-500 font-sans flex items-center gap-1.5 flex-wrap">
          <span>{KIND_LABEL[kind]}</span>
          <span>· {fmtDate(course.created_at)}</span>
        </div>

        <div className="text-[12px] text-neutral-400 font-sans flex items-center gap-4">
          <span className="inline-flex items-center gap-1.5">
            <ChaptersIcon />
            {chapters} {plural(chapters, "глава", "главы", "глав")}
          </span>
          <span className="inline-flex items-center gap-1.5">
            <QuizIcon />
            {questions} {plural(questions, "вопрос", "вопроса", "вопросов")}
          </span>
        </div>

        <div className="mt-auto flex items-center justify-between gap-2 pt-1">
          <span
            className={`inline-flex items-center gap-2 text-[12px] font-sans ${sm.text}`}>
            <span className={`w-2 h-2 rounded-full ${sm.dot}`} />
            {sm.label}
          </span>
          <div className="flex items-center gap-1.5">
            <button
              onClick={onOpen}
              className="text-sm px-4 py-2 rounded-lg border border-neutral-700 text-neutral-200 hover:text-lime-400 hover:border-lime-400/50 transition-colors">
              Открыть курс
            </button>
            <CardMenu onOpen={onOpen} onDelete={onDelete} />
          </div>
        </div>
      </div>
    </div>
  )
}

function CourseRow({ item, status, onOpen, onDelete }: CardProps) {
  const { source, course } = item
  const kind = kindOf(source)
  const { chapters, questions } = counts(item)
  const sm = statusMeta(status)

  return (
    <div className="flex items-center gap-4 rounded-xl border border-neutral-800 bg-neutral-900/40 hover:border-neutral-700 transition-colors p-3">
      <button
        onClick={onOpen}
        className="relative w-40 h-24 shrink-0 overflow-hidden rounded-lg">
        <Preview kind={kind} url={source.url} compact />
        <div className="absolute top-1.5 left-1.5">
          <KindBadge kind={kind} small />
        </div>
      </button>

      <div className="min-w-0 flex-1">
        <h3 className="text-sm font-sans font-semibold leading-snug line-clamp-1">
          {course.title || source.title || source.url || "Курс"}
        </h3>
        <div className="mt-1 text-[11px] text-neutral-500 font-sans flex items-center gap-1.5 flex-wrap">
          <span>{KIND_LABEL[kind]}</span>
          <span>· {fmtDate(course.created_at)}</span>
          <span>· {chapters} {plural(chapters, "глава", "главы", "глав")}</span>
          <span>· {questions} {plural(questions, "вопрос", "вопроса", "вопросов")}</span>
        </div>
        <div className="mt-2">
          <span
            className={`inline-flex items-center gap-2 text-[12px] font-sans ${sm.text}`}>
            <span className={`w-2 h-2 rounded-full ${sm.dot}`} />
            {sm.label}
          </span>
        </div>
      </div>

      <div className="flex items-center gap-1.5 shrink-0">
        <button
          onClick={onOpen}
          className="text-sm px-4 py-2 rounded-lg border border-neutral-700 text-neutral-200 hover:text-lime-400 hover:border-lime-400/50 transition-colors">
          Открыть курс
        </button>
        <CardMenu onOpen={onOpen} onDelete={onDelete} />
      </div>
    </div>
  )
}

function CardMenu({ onOpen, onDelete }: { onOpen: () => void; onDelete: () => void }) {
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
          <button
            onClick={() => {
              setOpen(false)
              onOpen()
            }}
            className="w-full text-left px-3 py-2 text-neutral-200 hover:bg-neutral-800 transition-colors">
            Открыть курс
          </button>
          <button
            onClick={() => {
              setOpen(false)
              onDelete()
            }}
            className="w-full text-left px-3 py-2 text-red-400 hover:bg-red-400/10 transition-colors">
            Удалить
          </button>
        </div>
      )}
    </div>
  )
}

function plural(n: number, one: string, few: string, many: string) {
  const m10 = n % 10
  const m100 = n % 100
  if (m10 === 1 && m100 !== 11) return one
  if (m10 >= 2 && m10 <= 4 && (m100 < 10 || m100 >= 20)) return few
  return many
}

function ChaptersIcon() {
  return (
    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20" />
      <path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z" />
    </svg>
  )
}

function QuizIcon() {
  return (
    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3" />
      <line x1="12" y1="17" x2="12.01" y2="17" />
    </svg>
  )
}
