"use client"

import { useEffect, useState } from "react"

import { getStats, type Stats } from "../../lib/api"
import { getCache, setCache } from "../../lib/cache"
import RefreshButton from "../refresh-button"

export default function DiagPage() {
  const [stats, setStats] = useState<Stats | null>(
    () => getCache<Stats>("stats") ?? null
  )
  const [loading, setLoading] = useState(
    () => getCache<Stats>("stats") === undefined
  )
  const [error, setError] = useState<string | null>(null)
  const [tick, setTick] = useState(0)

  useEffect(() => {
    setLoading(true)
    getStats()
      .then((d) => {
        setStats(d)
        setCache("stats", d)
      })
      .catch((e) => setError(String(e)))
      .finally(() => setLoading(false))
  }, [tick])

  return (
    <main className="max-w-[1700px] mx-auto px-6 py-8">
      <header className="border-b border-neutral-800 pb-6 mb-6">
        <div className="text-xs uppercase tracking-widest text-lime-400 mb-2">
          /// диагностика
        </div>
        <div className="flex items-center justify-between gap-3">
          <h1 className="text-3xl font-semibold">Состояние системы</h1>
          <RefreshButton onClick={() => setTick((t) => t + 1)} busy={loading} />
        </div>
        <p className="text-neutral-400 mt-2 text-sm font-sans">
          Качество и наблюдаемость PAM: бэклог индексации памяти и материалов,
          очередь фактов, а также провайдеры, ошибки и тайминги за{" "}
          <span className="text-lime-400">{stats?.days ?? 7} дн.</span>
        </p>
      </header>

      {error && (
        <div className="text-red-400 text-sm font-sans mb-4">Ошибка: {error}</div>
      )}

      {loading && !stats ? (
        <div className="text-neutral-500 text-sm py-12">// загрузка…</div>
      ) : !stats ? (
        <div className="text-neutral-500 text-sm py-12 border border-dashed border-neutral-800 text-center font-sans">
          Нет данных.
        </div>
      ) : (
        <div className="grid gap-6 lg:grid-cols-2">
          <MemorySection memory={stats.memory} />
          <LecturerSection lecturer={stats.lecturer} />
          <FactsSection facts={stats.facts} />
          <EventsSection events={stats.events} />
        </div>
      )}
    </main>
  )
}

// ── Память ─────────────────────────────────────────────────────────────────

function MemorySection({ memory }: { memory: Stats["memory"] }) {
  return (
    <Section title="Память">
      <div className="grid grid-cols-3 gap-3 mb-4">
        <Metric label="разговоры" value={memory.conversations} />
        <Metric label="сообщения" value={memory.messages} />
        <Metric label="чанки" value={memory.chunks_total} />
      </div>
      <IndexBar
        embedded={memory.chunks_embedded}
        total={memory.chunks_total}
        pending={memory.chunks_pending}
      />
    </Section>
  )
}

// ── Лектор ─────────────────────────────────────────────────────────────────

function LecturerSection({ lecturer }: { lecturer: Stats["lecturer"] }) {
  return (
    <Section title="Лектор">
      <div className="grid grid-cols-3 gap-3 mb-4">
        <Metric label="источники" value={lecturer.sources} />
        <Metric label="курсы" value={lecturer.courses} />
        <Metric label="чанки" value={lecturer.content_chunks_total} />
      </div>
      <IndexBar
        embedded={lecturer.content_chunks_embedded}
        total={lecturer.content_chunks_total}
        pending={lecturer.content_chunks_pending}
      />
    </Section>
  )
}

// ── Факты ──────────────────────────────────────────────────────────────────

function FactsSection({ facts }: { facts: Stats["facts"] }) {
  return (
    <Section title="Факты">
      <div className="grid grid-cols-3 gap-3">
        <Metric
          label="на проверке"
          value={facts.pending_review}
          tone={facts.pending_review > 0 ? "amber" : "neutral"}
        />
        <Metric label="принято" value={facts.approved} tone="lime" />
        <Metric label="изменено" value={facts.edited} tone="lime" />
        <Metric label="отклонено" value={facts.rejected} tone="muted" />
        <Metric label="всего" value={facts.total} />
      </div>
    </Section>
  )
}

// ── Провайдеры и события ───────────────────────────────────────────────────

function EventsSection({ events }: { events: Stats["events"] }) {
  return (
    <Section title="Провайдеры и события">
      <div className="grid grid-cols-2 gap-3 mb-5">
        <Metric
          label="фолбэки"
          value={events.fallbacks}
          tone={events.fallbacks > 0 ? "amber" : "neutral"}
        />
        <Metric
          label="ошибки"
          value={events.errors}
          tone={events.errors > 0 ? "red" : "neutral"}
        />
      </div>

      <SubHead>Провайдеры</SubHead>
      {events.providers.length === 0 ? (
        <Empty />
      ) : (
        <div className="space-y-1.5 mb-5">
          {events.providers.map((p, i) => (
            <div
              key={i}
              className="flex items-center justify-between gap-3 text-sm font-sans">
              <span className="flex items-center gap-2">
                <span
                  className={`w-2 h-2 rounded-full ${
                    p.status === "ok" ? "bg-lime-400" : "bg-red-400"
                  }`}
                />
                <span className="text-neutral-200">{p.provider}</span>
                <span className="text-[11px] uppercase tracking-wider text-neutral-500">
                  {p.status}
                </span>
              </span>
              <span className="tabular-nums text-neutral-400">{p.count}</span>
            </div>
          ))}
        </div>
      )}

      <SubHead>Тайминги</SubHead>
      {events.timing.length === 0 ? (
        <Empty />
      ) : (
        <div className="space-y-1.5 mb-5">
          {events.timing.map((t, i) => (
            <div
              key={i}
              className="flex items-center justify-between gap-3 text-sm font-sans">
              <span className="text-neutral-200">{t.kind}</span>
              <span className="tabular-nums text-neutral-500 text-[12px]">
                avg{" "}
                <span className="text-neutral-300">{t.avg_ms}</span>ms / p95{" "}
                <span className="text-neutral-300">{t.p95_ms}</span>ms /{" "}
                <span className="text-neutral-300">{t.count}</span>
              </span>
            </div>
          ))}
        </div>
      )}

      <SubHead>Недавние ошибки</SubHead>
      {events.recent_errors.length === 0 ? (
        <Empty />
      ) : (
        <div className="space-y-2">
          {events.recent_errors.map((e, i) => (
            <div
              key={i}
              className="border border-neutral-800 rounded-sm p-3 text-sm font-sans">
              <div className="flex items-center justify-between gap-3 mb-1">
                <span className="flex items-center gap-2">
                  <span className="text-[11px] uppercase tracking-wider text-red-400">
                    {e.kind}
                  </span>
                  <span className="text-[11px] uppercase tracking-wider text-neutral-500">
                    {e.provider}
                  </span>
                </span>
                <span className="text-[11px] tabular-nums text-neutral-600 shrink-0">
                  {fmtTime(e.created_at)}
                </span>
              </div>
              <p className="text-xs text-neutral-400 break-words">{e.detail}</p>
            </div>
          ))}
        </div>
      )}
    </Section>
  )
}

// ── Переиспользуемые примитивы ─────────────────────────────────────────────

function Section({
  title,
  children
}: {
  title: string
  children: React.ReactNode
}) {
  return (
    <section className="border border-neutral-800 rounded-sm p-4">
      <h2 className="text-xs uppercase tracking-widest text-neutral-500 mb-4">
        {title}
      </h2>
      {children}
    </section>
  )
}

type Tone = "neutral" | "lime" | "amber" | "red" | "muted"

const TONE_TEXT: Record<Tone, string> = {
  neutral: "text-neutral-100",
  lime: "text-lime-400",
  amber: "text-amber-400",
  red: "text-red-400",
  muted: "text-neutral-600"
}

function Metric({
  label,
  value,
  tone = "neutral"
}: {
  label: string
  value: number
  tone?: Tone
}) {
  return (
    <div className="border border-neutral-800 rounded-sm p-3">
      <div className={`text-2xl font-semibold tabular-nums ${TONE_TEXT[tone]}`}>
        {value.toLocaleString("ru")}
      </div>
      <div className="text-[11px] uppercase tracking-wider text-neutral-500 mt-1">
        {label}
      </div>
    </div>
  )
}

/** Бэклог индексации: embedded / total + остаток pending (амбер, если > 0). */
function IndexBar({
  embedded,
  total,
  pending
}: {
  embedded: number
  total: number
  pending: number
}) {
  const pct = total > 0 ? Math.round((embedded / total) * 100) : 0
  return (
    <div>
      <div className="flex items-center justify-between text-xs font-sans mb-1.5">
        <span className="text-neutral-400">
          Индексация{" "}
          <span className="tabular-nums text-neutral-200">
            {embedded.toLocaleString("ru")} / {total.toLocaleString("ru")}
          </span>
        </span>
        {pending > 0 ? (
          <span className="tabular-nums text-amber-400">
            в очереди: {pending.toLocaleString("ru")}
          </span>
        ) : (
          <span className="text-lime-400">всё проиндексировано</span>
        )}
      </div>
      <div className="h-2 rounded-full bg-neutral-900 overflow-hidden">
        <div
          className={`h-full rounded-full transition-all ${
            pending > 0 ? "bg-amber-400" : "bg-lime-400"
          }`}
          style={{ width: `${pct}%` }}
        />
      </div>
    </div>
  )
}

function SubHead({ children }: { children: React.ReactNode }) {
  return (
    <h3 className="text-[11px] uppercase tracking-wider text-neutral-600 mb-2">
      {children}
    </h3>
  )
}

function Empty() {
  return (
    <div className="text-neutral-600 text-sm font-sans mb-5">— нет данных</div>
  )
}

/** Короткое локальное время HH:MM из ISO-таймстампа; иначе «—». */
function fmtTime(iso: string): string {
  const d = new Date(iso)
  if (isNaN(d.getTime())) return "—"
  return d.toLocaleTimeString("ru", { hour: "2-digit", minute: "2-digit" })
}
