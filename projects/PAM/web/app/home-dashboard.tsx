"use client"

import { useEffect, useState } from "react"
import Link from "next/link"

import {
  listSources,
  listProgress,
  factCounts,
  type ConversationSummary,
  type ContentSource,
  type ProgressSummary
} from "../lib/api"
import { KIND_LABEL, kindOf, fmtDate } from "../lib/material-ui"

/** Курс в процессе: прогресс + название материала (из listSources по source_id). */
interface ActiveCourse {
  source_id: string
  title: string
  percent: number
}

export default function HomeDashboard({
  chats,
  onOpenChat
}: {
  chats: ConversationSummary[]
  onOpenChat: (id: string) => void
}) {
  const [sources, setSources] = useState<ContentSource[]>([])
  const [active, setActive] = useState<ActiveCourse[]>([])
  const [pendingFacts, setPendingFacts] = useState(0)

  // Best-effort: любой сбой → пустое состояние, дашборд остаётся полезным.
  useEffect(() => {
    let cancelled = false
    Promise.allSettled([listSources(), listProgress(), factCounts()]).then(
      ([srcRes, progRes, factRes]) => {
        if (cancelled) return
        const srcs =
          srcRes.status === "fulfilled" ? srcRes.value : []
        setSources(srcs)

        if (progRes.status === "fulfilled") {
          const byId = new Map(srcs.map((s) => [s.id, s]))
          const titleOf = (sid: string) =>
            byId.get(sid)?.title || byId.get(sid)?.url || "Без названия"
          const rows = progRes.value
          // Курсы в процессе; fallback — любой курс с ненулевым прогрессом.
          let pick = rows.filter((r) => r.status === "in_progress")
          if (pick.length === 0) pick = rows.filter((r) => r.percent > 0)
          setActive(
            pick
              .slice(0, 3)
              .map((r): ActiveCourse => ({
                source_id: r.source_id,
                title: titleOf(r.source_id),
                percent: r.percent
              }))
          )
        }

        if (factRes.status === "fulfilled") {
          setPendingFacts(factRes.value.pending_review || 0)
        }
      }
    )
    return () => {
      cancelled = true
    }
  }, [])

  const recentSources = sources.slice(0, 4)
  const top = chats[0]
  const more = chats.slice(1, 3)

  return (
    <div className="py-6 space-y-7">
      {/* 1. Заголовок */}
      <header>
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-lime-400/10 border border-lime-400/30 text-lime-400 flex items-center justify-center text-base font-semibold">
            P
          </div>
          <div>
            <h1 className="text-2xl font-semibold leading-tight">
              С возвращением
            </h1>
            <p className="text-neutral-400 text-sm font-sans">
              Продолжи с того места, где остановился — или спроси что угодно ниже.
            </p>
          </div>
        </div>
      </header>

      {/* 2. Продолжить */}
      <section>
        <SectionTitle>Продолжить</SectionTitle>
        {top ? (
          <div className="space-y-1.5">
            <button
              onClick={() => onOpenChat(top.id)}
              className="w-full text-left flex items-center gap-3 rounded-lg border border-neutral-800 bg-neutral-900/40 hover:border-lime-400/40 hover:bg-lime-400/[0.04] transition-colors px-4 py-3 group">
              <span className="shrink-0 w-8 h-8 rounded-md bg-lime-400/10 border border-lime-400/30 text-lime-400 flex items-center justify-center">
                <ChatIcon />
              </span>
              <span className="min-w-0 flex-1">
                <span className="block text-sm font-sans font-medium truncate">
                  {top.title || "Без названия"}
                </span>
                <span className="block text-[11px] text-neutral-500 font-sans tabular-nums">
                  {fmtDate(top.updated_at)} · {top.message_count} сообщ.
                </span>
              </span>
              <span className="shrink-0 text-neutral-600 group-hover:text-lime-400 transition-colors">
                <ArrowIcon />
              </span>
            </button>
            {more.map((c) => (
              <button
                key={c.id}
                onClick={() => onOpenChat(c.id)}
                className="w-full text-left flex items-center justify-between gap-3 rounded-md px-4 py-2 hover:bg-neutral-900/60 transition-colors group">
                <span className="min-w-0 text-sm font-sans text-neutral-300 truncate group-hover:text-neutral-100">
                  {c.title || "Без названия"}
                </span>
                <span className="shrink-0 text-[11px] text-neutral-600 font-sans tabular-nums">
                  {fmtDate(c.updated_at)}
                </span>
              </button>
            ))}
          </div>
        ) : (
          <Muted>Начни новый разговор ниже.</Muted>
        )}
      </section>

      {/* 3. Последние материалы */}
      <section>
        <SectionTitle>
          Последние материалы
          <Link
            href="/learn"
            className="ml-auto text-[11px] normal-case tracking-normal text-neutral-500 hover:text-lime-400 transition-colors">
            все →
          </Link>
        </SectionTitle>
        {recentSources.length > 0 ? (
          <div className="grid gap-1.5">
            {recentSources.map((s) => (
              <Link
                key={s.id}
                href={"/courses/" + s.id}
                className="flex items-center gap-3 rounded-md border border-neutral-800 bg-neutral-900/30 hover:border-neutral-700 transition-colors px-3 py-2">
                <span className="shrink-0 text-[9px] uppercase tracking-wide font-semibold text-neutral-400 border border-neutral-700 rounded px-1.5 py-0.5">
                  {KIND_LABEL[kindOf(s)]}
                </span>
                <span className="min-w-0 flex-1 text-sm font-sans text-neutral-200 truncate">
                  {s.title || s.url || "Без названия"}
                </span>
                <span className="shrink-0 text-[11px] text-neutral-600 font-sans tabular-nums">
                  {fmtDate(s.created_at)}
                </span>
              </Link>
            ))}
          </div>
        ) : (
          <Muted>
            Пока нет материалов.{" "}
            <Link href="/learn" className="text-lime-400 hover:underline">
              Добавить →
            </Link>
          </Muted>
        )}
      </section>

      {/* 4. Курсы в процессе */}
      <section>
        <SectionTitle>Курсы в процессе</SectionTitle>
        {active.length > 0 ? (
          <div className="grid gap-1.5">
            {active.map((c) => (
              <Link
                key={c.source_id}
                href={"/courses/" + c.source_id}
                className="block rounded-md border border-neutral-800 bg-neutral-900/30 hover:border-neutral-700 transition-colors px-3 py-2.5">
                <div className="flex items-center justify-between gap-3 mb-1.5">
                  <span className="min-w-0 text-sm font-sans text-neutral-200 truncate">
                    {c.title}
                  </span>
                  <span className="shrink-0 text-[11px] text-neutral-500 font-sans tabular-nums">
                    {c.percent}%
                  </span>
                </div>
                <div className="h-1 rounded-full bg-neutral-800 overflow-hidden">
                  <div
                    className="h-full bg-lime-400 transition-all"
                    style={{ width: `${c.percent}%` }}
                  />
                </div>
              </Link>
            ))}
          </div>
        ) : (
          <Muted>Нет активных курсов.</Muted>
        )}
      </section>

      {/* 5. Новые факты */}
      <section>
        <SectionTitle>Новые факты</SectionTitle>
        {pendingFacts > 0 ? (
          <Link
            href="/me"
            className="inline-flex items-center gap-2 rounded-md border border-amber-400/30 bg-amber-400/[0.04] hover:bg-amber-400/[0.08] transition-colors px-3 py-2 text-sm font-sans text-amber-300">
            <span className="w-1.5 h-1.5 rounded-full bg-amber-400" />
            {pendingFacts}{" "}
            {factWord(pendingFacts)} на проверке →
          </Link>
        ) : (
          <Muted>Очередь фактов пуста.</Muted>
        )}
      </section>

      {/* 6. Быстрые действия */}
      <section>
        <SectionTitle>Быстрые действия</SectionTitle>
        <div className="flex flex-wrap gap-2">
          <QuickAction href="/learn">Добавить материал</QuickAction>
          <QuickAction href="/me">Проверить факты</QuickAction>
          <QuickAction href="/diag">Диагностика</QuickAction>
          <QuickAction href="/saved">Избранное</QuickAction>
        </div>
      </section>
    </div>
  )
}

// ── Подкомпоненты ─────────────────────────────────────────────────────────

function SectionTitle({ children }: { children: React.ReactNode }) {
  return (
    <h2 className="flex items-center gap-2 text-[11px] uppercase tracking-widest text-neutral-500 mb-2.5">
      {children}
    </h2>
  )
}

function Muted({ children }: { children: React.ReactNode }) {
  return <p className="text-sm font-sans text-neutral-600">{children}</p>
}

function QuickAction({
  href,
  children
}: {
  href: string
  children: React.ReactNode
}) {
  return (
    <Link
      href={href}
      className="text-sm font-sans px-3.5 py-1.5 rounded-lg border border-neutral-800 text-neutral-300 hover:text-lime-400 hover:border-lime-400/40 transition-colors">
      {children}
    </Link>
  )
}

/** «факт / факта / фактов» по числу. */
function factWord(n: number): string {
  const d = n % 10
  const dd = n % 100
  if (d === 1 && dd !== 11) return "факт"
  if (d >= 2 && d <= 4 && (dd < 10 || dd >= 20)) return "факта"
  return "фактов"
}

function ChatIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.9" strokeLinecap="round" strokeLinejoin="round">
      <path d="M21 11.5a8.38 8.38 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.38 8.38 0 0 1-3.8-.9L3 21l1.9-5.7a8.38 8.38 0 0 1-.9-3.8 8.5 8.5 0 0 1 4.7-7.6 8.38 8.38 0 0 1 3.8-.9h.5a8.48 8.48 0 0 1 8 8v.5z" />
    </svg>
  )
}

function ArrowIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M5 12h14M13 6l6 6-6 6" />
    </svg>
  )
}
