"use client"

import { useEffect, useState } from "react"
import Link from "next/link"

import {
  getTimeline,
  listProjects,
  type TimelineItem,
  type TimelineEntity,
  type Project
} from "../../lib/api"
import { fmtDate } from "../../lib/material-ui"

/**
 * Хронология (P2.3) — read-only производная лента: как знание появлялось во
 * времени. Линейная история + связанные элементы (idea → became → decision),
 * без графа. Источник — GET /timeline поверх существующих сущностей.
 */

const ENTITY_META: Record<
  TimelineEntity,
  { label: string; cls: string }
> = {
  memory_item: { label: "заметка", cls: "text-lime-400 border-lime-400/30 bg-lime-400/10" },
  conversation: { label: "чат", cls: "text-sky-400 border-sky-400/30 bg-sky-400/10" },
  material: { label: "материал", cls: "text-amber-400 border-amber-400/30 bg-amber-400/10" },
  course: { label: "курс", cls: "text-violet-400 border-violet-400/30 bg-violet-400/10" },
  fact: { label: "факт", cls: "text-neutral-300 border-neutral-600 bg-neutral-800/40" },
  link: { label: "связь", cls: "text-pink-400 border-pink-400/30 bg-pink-400/10" }
}

const ENTITY_ORDER: TimelineEntity[] = [
  "memory_item",
  "conversation",
  "material",
  "course",
  "fact",
  "link"
]

const PERIODS: Array<[string, number | null]> = [
  ["7 дней", 7],
  ["30 дней", 30],
  ["90 дней", 90],
  ["всё время", null]
]

function detailHref(it: TimelineItem): string | null {
  if (it.entity_type === "conversation") return `/c/${it.ref_id}`
  if (it.entity_type === "course") return `/courses/${it.ref_id}`
  return null // memory_item / material / fact / link — отдельной страницы нет
}

export default function TimelinePage() {
  const [items, setItems] = useState<TimelineItem[]>([])
  const [projects, setProjects] = useState<Project[]>([])
  const [projectId, setProjectId] = useState<string>("")
  const [entity, setEntity] = useState<TimelineEntity | "">("")
  const [days, setDays] = useState<number | null>(30)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [open, setOpen] = useState<Set<string>>(new Set())

  useEffect(() => {
    listProjects()
      .then(setProjects)
      .catch(() => {})
  }, [])

  useEffect(() => {
    setLoading(true)
    setError(null)
    const from =
      days === null
        ? undefined
        : new Date(Date.now() - days * 86_400_000).toISOString()
    getTimeline({
      projectId: projectId || undefined,
      entityType: entity || undefined,
      from,
      limit: 100
    })
      .then(setItems)
      .catch((e) => setError(String(e)))
      .finally(() => setLoading(false))
  }, [projectId, entity, days])

  function toggle(id: string) {
    setOpen((prev) => {
      const next = new Set(prev)
      next.has(id) ? next.delete(id) : next.add(id)
      return next
    })
  }

  return (
    <main className="max-w-[900px] mx-auto px-6 py-8">
      <header className="border-b border-neutral-800 pb-6 mb-6">
        <div className="text-xs uppercase tracking-widest text-lime-400 mb-2">
          /// хронология
        </div>
        <h1 className="text-3xl font-semibold">Лента памяти</h1>
        <p className="text-neutral-400 mt-2 text-sm font-sans">
          Как знание появлялось и связывалось во времени — заметки, чаты,
          материалы, курсы, факты и связи между ними.
        </p>
      </header>

      {/* Фильтры */}
      <div className="flex flex-wrap items-center gap-3 mb-6">
        <select
          value={projectId}
          onChange={(e) => setProjectId(e.target.value)}
          className="bg-neutral-900 border border-neutral-800 rounded-lg px-3 py-1.5 text-sm font-sans text-neutral-200">
          <option value="">Все проекты</option>
          {projects.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </select>

        <select
          value={days === null ? "" : String(days)}
          onChange={(e) =>
            setDays(e.target.value === "" ? null : Number(e.target.value))
          }
          className="bg-neutral-900 border border-neutral-800 rounded-lg px-3 py-1.5 text-sm font-sans text-neutral-200">
          {PERIODS.map(([label, v]) => (
            <option key={label} value={v === null ? "" : String(v)}>
              {label}
            </option>
          ))}
        </select>

        <div className="flex flex-wrap gap-1.5">
          <Chip active={entity === ""} onClick={() => setEntity("")}>
            все
          </Chip>
          {ENTITY_ORDER.map((e) => (
            <Chip key={e} active={entity === e} onClick={() => setEntity(e)}>
              {ENTITY_META[e].label}
            </Chip>
          ))}
        </div>
      </div>

      {error && (
        <div className="text-red-400 text-sm font-sans mb-4">Ошибка: {error}</div>
      )}

      {loading && items.length === 0 ? (
        <div className="text-neutral-500 text-sm py-12">// загрузка…</div>
      ) : items.length === 0 ? (
        <div className="text-neutral-500 text-sm py-12 border border-dashed border-neutral-800 text-center font-sans">
          Пока нет событий за выбранный период.
        </div>
      ) : (
        <ol className="relative border-l border-neutral-800 ml-2">
          {items.map((it) => (
            <Row
              key={`${it.entity_type}-${it.ref_id}`}
              it={it}
              expanded={open.has(it.ref_id)}
              onToggle={() => toggle(it.ref_id)}
            />
          ))}
        </ol>
      )}
    </main>
  )
}

function Row({
  it,
  expanded,
  onToggle
}: {
  it: TimelineItem
  expanded: boolean
  onToggle: () => void
}) {
  const meta = ENTITY_META[it.entity_type]
  const href = detailHref(it)
  const hasRelated = it.related.length > 0
  return (
    <li className="ml-5 mb-5">
      <span className="absolute -left-[5px] w-2.5 h-2.5 rounded-full bg-neutral-700 border border-neutral-950" />
      <div className="rounded-lg border border-neutral-800 bg-neutral-900/30 p-3">
        <div className="flex items-center gap-2 mb-1.5">
          <span
            className={`text-[10px] font-sans px-1.5 py-0.5 rounded border ${meta.cls}`}>
            {meta.label}
          </span>
          {it.subtype && (
            <span className="text-[11px] text-neutral-500 font-sans">
              {it.subtype}
            </span>
          )}
          <span className="ml-auto text-[11px] tabular-nums text-neutral-600 font-sans">
            {fmtDate(it.timestamp)}
          </span>
        </div>
        <div className="text-sm font-sans text-neutral-100 break-words">
          {href ? (
            <Link href={href} className="hover:text-lime-400 transition-colors">
              {it.title || "—"}
            </Link>
          ) : (
            it.title || "—"
          )}
        </div>
        {it.summary && (
          <p className="text-[13px] font-sans text-neutral-400 mt-1 break-words">
            {it.summary}
          </p>
        )}
        {hasRelated && (
          <button
            onClick={onToggle}
            className="mt-2 text-[11px] font-sans text-neutral-500 hover:text-lime-400 transition-colors">
            {expanded ? "− скрыть связи" : `→ связи (${it.related.length})`}
          </button>
        )}
        {expanded && hasRelated && (
          <ul className="mt-2 space-y-1 border-t border-neutral-800 pt-2">
            {it.related.map((r, i) => (
              <li
                key={i}
                className="text-[12px] font-sans text-neutral-400 flex items-center gap-2">
                <span className="text-pink-400">{r.relation}</span>
                <span className="text-neutral-300 break-words">
                  {r.title || r.kind}
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </li>
  )
}

function Chip({
  active,
  onClick,
  children
}: {
  active: boolean
  onClick: () => void
  children: React.ReactNode
}) {
  return (
    <button
      onClick={onClick}
      className={`text-[12px] font-sans px-2.5 py-1 rounded-full border transition-colors ${
        active
          ? "border-lime-400/50 bg-lime-400/10 text-lime-400"
          : "border-neutral-800 text-neutral-400 hover:text-neutral-100"
      }`}>
      {children}
    </button>
  )
}
