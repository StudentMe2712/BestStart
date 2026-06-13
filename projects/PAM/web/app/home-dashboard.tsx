"use client"

import { useEffect, useState } from "react"
import Link from "next/link"

import {
  getStats,
  type Stats,
  type StatsHealth,
  type ConversationSummary
} from "../lib/api"
import { getCache, setCache } from "../lib/cache"
import { fmtDate } from "../lib/material-ui"

/**
 * Главный экран PAM = точка входа в память (см. Home Screen UX Contract).
 * НЕ дашборд: только приветствие, компактная карточка состояния памяти и
 * фиксированные быстрые действия. Истории/материалов/курсов здесь нет — они
 * живут в сайдбаре и своих разделах; дублировать и плодить пустые блоки нельзя.
 */
export default function HomeDashboard({
  chats
}: {
  chats: ConversationSummary[]
}) {
  const [stats, setStats] = useState<Stats | null>(
    () => getCache<Stats>("stats") ?? null
  )

  // Best-effort: карточка памяти — индикатор состояния, а не центр экрана.
  // Любой сбой → карточку просто не показываем, экран остаётся спокойным.
  useEffect(() => {
    let cancelled = false
    getStats()
      .then((d) => {
        if (cancelled) return
        setStats(d)
        setCache("stats", d)
      })
      .catch(() => {})
    return () => {
      cancelled = true
    }
  }, [])

  const lastUpdate = chats[0]?.updated_at

  return (
    <div className="py-8 md:py-12">
      {/* Приветствие + компактная карточка памяти справа */}
      <div className="flex items-start justify-between gap-6 flex-wrap mb-10">
        <header className="min-w-0">
          <h1 className="text-3xl font-semibold leading-tight mb-2">
            С возвращением
          </h1>
          <p className="text-neutral-400 text-sm font-sans max-w-md">
            Продолжи с того места, где остановился — или задай вопрос ниже.
          </p>
        </header>
        {stats && <MemoryCard stats={stats} lastUpdate={lastUpdate} />}
      </div>

      {/* Быстрые действия — главный (и единственный) контент экрана */}
      <section>
        <h2 className="text-[11px] uppercase tracking-widest text-neutral-500 mb-3">
          Быстрые действия
        </h2>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <ActionCard
            href="/learn"
            icon={<DocIcon />}
            title="Добавить материал"
            desc="Статья, PDF, видео или файл в память"
            cta="Создать"
            primary
          />
          <ActionCard
            href="/me"
            icon={<CheckIcon />}
            title="Проверить факты"
            desc="Очередь фактов на проверку"
            cta="Открыть"
          />
          <ActionCard
            href="/diag"
            icon={<PulseIcon />}
            title="Диагностика"
            desc="Состояние и здоровье памяти"
            cta="Открыть"
          />
          <ActionCard
            href="/saved"
            icon={<StarIcon />}
            title="Избранное"
            desc="Сохранённые сообщения и факты"
            cta="Открыть"
          />
        </div>
      </section>
    </div>
  )
}

// ── Карточка памяти (компактный индикатор состояния) ───────────────────────

const HEALTH_BADGE: Record<
  StatsHealth["label"],
  { text: string; cls: string; num: string }
> = {
  good: {
    text: "хорошо",
    cls: "text-lime-400 bg-lime-400/10 border-lime-400/30",
    num: "text-lime-400"
  },
  ok: {
    text: "норм",
    cls: "text-amber-400 bg-amber-400/10 border-amber-400/30",
    num: "text-amber-400"
  },
  poor: {
    text: "внимание",
    cls: "text-red-400 bg-red-400/10 border-red-400/30",
    num: "text-red-400"
  }
}

function MemoryCard({
  stats,
  lastUpdate
}: {
  stats: Stats
  lastUpdate?: string
}) {
  const badge = HEALTH_BADGE[stats.health.label]
  const pending = stats.facts.pending_review
  return (
    <aside className="shrink-0 w-full sm:w-64 rounded-xl border border-neutral-800 bg-neutral-900/40 p-4">
      <div className="flex items-center justify-between mb-3">
        <span className="text-[11px] uppercase tracking-widest text-neutral-500">
          Память
        </span>
        <span
          className={`text-[10px] font-sans px-1.5 py-0.5 rounded border ${badge.cls}`}>
          {badge.text}
        </span>
      </div>
      <div className="flex items-baseline gap-1.5 mb-4">
        <span className={`text-4xl font-semibold tabular-nums ${badge.num}`}>
          {stats.health.score}
        </span>
        <span className="text-xs text-neutral-600 font-sans">/100</span>
      </div>
      <dl className="space-y-1.5 text-sm font-sans">
        <CardRow label="Фактов" value={stats.facts.total.toLocaleString("ru")} />
        <CardRow
          label="На проверке"
          value={pending.toLocaleString("ru")}
          tone={pending > 0 ? "text-amber-400" : undefined}
        />
        {lastUpdate && <CardRow label="Обновлено" value={fmtDate(lastUpdate)} />}
      </dl>
    </aside>
  )
}

function CardRow({
  label,
  value,
  tone
}: {
  label: string
  value: string
  tone?: string
}) {
  return (
    <div className="flex items-center justify-between gap-3">
      <dt className="text-neutral-500">{label}</dt>
      <dd className={`tabular-nums ${tone ?? "text-neutral-200"}`}>{value}</dd>
    </div>
  )
}

// ── Быстрые действия (равные карточки) ─────────────────────────────────────

function ActionCard({
  href,
  icon,
  title,
  desc,
  cta,
  primary = false
}: {
  href: string
  icon: React.ReactNode
  title: string
  desc: string
  cta: string
  primary?: boolean
}) {
  return (
    <Link
      href={href}
      className="group flex flex-col rounded-xl border border-neutral-800 bg-neutral-900/30 hover:border-lime-400/40 hover:bg-lime-400/[0.03] transition-colors p-4 min-h-[150px]">
      <span className="w-9 h-9 rounded-lg bg-neutral-800/60 border border-neutral-700 text-neutral-300 group-hover:text-lime-400 flex items-center justify-center mb-3 transition-colors">
        {icon}
      </span>
      <span className="text-sm font-sans font-medium mb-1">{title}</span>
      <span className="text-[12px] font-sans text-neutral-500 leading-snug mb-4">
        {desc}
      </span>
      <span
        className={`mt-auto inline-flex items-center justify-center text-[12px] font-sans rounded-lg px-3 py-1.5 transition-colors ${
          primary
            ? "bg-lime-400 text-neutral-950"
            : "border border-neutral-700 text-neutral-300 group-hover:border-lime-400/40 group-hover:text-lime-400"
        }`}>
        {cta}
      </span>
    </Link>
  )
}

// ── Иконки ─────────────────────────────────────────────────────────────────

function DocIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
      <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
      <polyline points="14 2 14 8 20 8" />
      <line x1="9" y1="13" x2="15" y2="13" />
      <line x1="9" y1="17" x2="13" y2="17" />
    </svg>
  )
}

function CheckIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
      <path d="M9 11l3 3L22 4" />
      <path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11" />
    </svg>
  )
}

function PulseIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
      <path d="M22 12h-4l-3 9L9 3l-3 9H2" />
    </svg>
  )
}

function StarIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
      <path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z" />
    </svg>
  )
}
