"use client"

import { useEffect, useMemo, useRef, useState } from "react"
import Link from "next/link"

import {
  TOOLS,
  CATEGORIES,
  ACCENTS,
  CATEGORY_LABEL,
  featuredTools,
  popularMcp,
  toolsList,
  newTools,
  updatedTools,
  starsOf,
  byStars,
  fmtStars,
  iconUrl,
  type CatalogTool,
  type CatalogCategory
} from "../../lib/catalog"

type View = "all" | "featured" | "popular" | "new" | "updated" | CatalogCategory

const HERO_CHIPS: { key: View; label: string }[] = [
  { key: "all", label: "Все" },
  { key: "mcp", label: "MCP" },
  { key: "agents", label: "Агенты" },
  { key: "tools", label: "Инструменты" },
  { key: "connectors", label: "Коннекторы" },
  { key: "plugins", label: "Плагины" }
]

const OVERVIEW_NAV: { key: View; label: string; icon: React.ReactNode }[] = [
  { key: "all", label: "Обзор", icon: <GridGlyph /> },
  { key: "popular", label: "Популярное", icon: <StarGlyph /> },
  { key: "new", label: "Новинки", icon: <PlusGlyph /> },
  { key: "featured", label: "Рекомендуемое", icon: <SparkGlyph /> },
  { key: "updated", label: "Недавно обновлённые", icon: <ClockGlyph /> }
]

function viewBase(v: View): CatalogTool[] {
  if (v === "featured") return featuredTools()
  if (v === "popular") return popularMcp()
  if (v === "new") return newTools()
  if (v === "updated") return updatedTools()
  if (v === "all") return TOOLS
  return TOOLS.filter((t) => t.category === v)
}

export default function CatalogPage() {
  const [query, setQuery] = useState("")
  const [view, setView] = useState<View>("all")
  const searchRef = useRef<HTMLInputElement>(null)

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

  const q = query.trim().toLowerCase()
  const results = useMemo(() => {
    let base = viewBase(view)
    if (q) {
      base = (q && view === "all" ? TOOLS : base).filter((t) =>
        (t.name + " " + t.short + " " + t.tags.join(" ") + " " + CATEGORY_LABEL[t.category])
          .toLowerCase()
          .includes(q)
      )
    }
    return [...base].sort(byStars)
  }, [view, q])

  return (
    <div className="max-w-[1700px] mx-auto px-6 py-8">
      <div className="flex gap-8 items-start">
        {/* ── Левая колонка ──────────────────────────────────────────────── */}
        <aside className="hidden lg:block w-[300px] shrink-0 sticky top-20 space-y-6">
          <div>
            <h1 className="text-2xl font-semibold">Каталог</h1>
            <p className="text-neutral-400 text-sm font-sans mt-1.5">
              Лучшие AI-инструменты, MCP-серверы, агенты и плагины для ваших
              задач.
            </p>
          </div>

          <nav className="space-y-1">
            {OVERVIEW_NAV.map((n) => {
              const active = view === n.key
              return (
                <button
                  key={n.key}
                  onClick={() => {
                    setView(n.key)
                    setQuery("")
                  }}
                  className={`w-full flex items-center gap-2.5 text-sm font-sans px-3 py-2 rounded-lg transition-colors ${
                    active
                      ? "bg-neutral-900 text-lime-400 border border-lime-400/30"
                      : "text-neutral-400 hover:text-neutral-100 hover:bg-neutral-900 border border-transparent"
                  }`}>
                  <span className={active ? "text-lime-400" : "text-neutral-500"}>
                    {n.icon}
                  </span>
                  {n.label}
                </button>
              )
            })}
          </nav>

          <div>
            <div className="text-[10px] uppercase tracking-widest text-neutral-600 px-3 mb-2">
              Категории
            </div>
            <div className="space-y-0.5">
              {CATEGORIES.map((c) => {
                const active = view === c.key
                return (
                  <button
                    key={c.key}
                    onClick={() => {
                      setView(c.key)
                      setQuery("")
                    }}
                    className={`w-full flex items-center justify-between gap-2 text-sm font-sans px-3 py-1.5 rounded-lg transition-colors ${
                      active
                        ? "bg-neutral-900 text-neutral-100"
                        : "text-neutral-400 hover:text-neutral-100 hover:bg-neutral-900"
                    }`}>
                    <span className="flex items-center gap-2.5 min-w-0">
                      <span className="text-neutral-600">
                        <DotGlyph />
                      </span>
                      <span className="truncate">{c.label}</span>
                    </span>
                    <span className="text-[11px] tabular-nums text-neutral-600 shrink-0">
                      {c.count}
                    </span>
                  </button>
                )
              })}
            </div>
          </div>

          <p className="text-[11px] text-neutral-600 font-sans px-1">
            Каталог обновляется сообществом. Подробнее в{" "}
            <code className="text-neutral-500">docs/ai-ecosystem-catalog.md</code>.
          </p>
        </aside>

        {/* ── Главная область ────────────────────────────────────────────── */}
        <div className="flex-1 min-w-0">
          {/* Hero */}
          <div className="mb-7">
            <h2 className="text-3xl font-semibold leading-tight">
              Откройте лучшие{" "}
              <span className="text-lime-400">AI-инструменты</span> для себя
            </h2>
            <p className="sr-only">
              Кураторская подборка MCP, агентов, инструментов и коннекторов для
              PAM.
            </p>

            <div className="relative mt-5">
              <span className="absolute left-4 top-1/2 -translate-y-1/2 text-neutral-500">
                <SearchIcon />
              </span>
              <input
                ref={searchRef}
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder="Поиск инструментов, MCP, агентов, плагинов..."
                className="w-full bg-neutral-900 border border-neutral-800 rounded-xl pl-11 pr-16 py-3.5 text-sm font-sans focus:outline-none focus:border-lime-400/50 transition-colors"
              />
              <span className="absolute right-3 top-1/2 -translate-y-1/2 text-[11px] text-neutral-600 border border-neutral-800 rounded px-1.5 py-0.5 font-sans">
                ⌘ K
              </span>
            </div>

            <div className="flex items-center gap-2 flex-wrap mt-4">
              {HERO_CHIPS.map((c) => {
                const active = view === c.key
                return (
                  <button
                    key={c.key}
                    onClick={() => setView(c.key)}
                    className={`text-sm font-sans px-3.5 py-1.5 rounded-lg border transition-colors ${
                      active
                        ? "border-lime-400/50 text-lime-400 bg-lime-400/5"
                        : "border-neutral-800 text-neutral-400 hover:border-neutral-700 hover:text-neutral-200"
                    }`}>
                    {c.label}
                  </button>
                )
              })}
            </div>
          </div>

          {view === "all" && !q && (
            <section className="mb-10">
              <div className="mb-4">
                <h3 className="text-lg font-semibold flex items-center gap-2">
                  <span className="text-lime-400">
                    <SparkGlyph />
                  </span>
                  Топ · Рекомендуемое
                </h3>
                <p className="text-neutral-500 text-sm font-sans mt-0.5">
                  Подборка инструментов, которые стоит попробовать
                </p>
              </div>
              <Carousel>
                {[...featuredTools()].sort(byStars).map((t) => (
                  <FeaturedCard key={t.slug} tool={t} />
                ))}
              </Carousel>
            </section>
          )}

          <div>
            <div className="flex items-baseline justify-between gap-3 mb-4 flex-wrap">
              <h3 className="text-lg font-semibold">
                {q
                  ? `Результаты: «${query.trim()}»`
                  : view === "all"
                    ? "Все инструменты"
                    : viewTitle(view)}
              </h3>
              <span className="text-sm text-neutral-500 font-sans">
                {results.length}{" "}
                {plural(results.length, "инструмент", "инструмента", "инструментов")} · по
                звёздам GitHub ★
              </span>
            </div>
            {results.length === 0 ? (
              <div className="text-neutral-500 text-sm py-16 border border-dashed border-neutral-800 rounded-xl text-center font-sans">
                Ничего не найдено. Попробуй другой запрос или фильтр.
              </div>
            ) : (
              <div
                className="grid gap-4"
                style={{ gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))" }}>
                {results.map((t) => (
                  <ToolCard key={t.slug} tool={t} />
                ))}
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}

function viewTitle(v: View): string {
  if (v === "featured") return "Рекомендуемое"
  if (v === "popular") return "Популярные MCP-серверы"
  if (v === "new") return "Новые поступления"
  if (v === "updated") return "Недавно обновлённые"
  if (v === "all") return "Все инструменты"
  return CATEGORY_LABEL[v]
}

// ── Секция ──────────────────────────────────────────────────────────────

function Section({
  icon,
  title,
  subtitle,
  onSeeAll,
  children
}: {
  icon: React.ReactNode
  title: string
  subtitle: string
  onSeeAll: () => void
  children: React.ReactNode
}) {
  return (
    <section>
      <div className="flex items-end justify-between gap-3 mb-4">
        <div>
          <h3 className="text-lg font-semibold flex items-center gap-2">
            {icon}
            {title}
          </h3>
          <p className="text-neutral-500 text-sm font-sans mt-0.5">{subtitle}</p>
        </div>
        <button
          onClick={onSeeAll}
          className="shrink-0 text-sm font-sans text-neutral-400 hover:text-lime-400 transition-colors">
          Смотреть всё →
        </button>
      </div>
      {children}
    </section>
  )
}

function Carousel({ children }: { children: React.ReactNode }) {
  const ref = useRef<HTMLDivElement>(null)
  const scroll = (dir: number) =>
    ref.current?.scrollBy({ left: dir * 640, behavior: "smooth" })
  return (
    <div className="relative group">
      <div
        ref={ref}
        className="flex gap-4 overflow-x-auto scroll-smooth snap-x pb-2 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
        {children}
      </div>
      <button
        onClick={() => scroll(-1)}
        aria-label="Назад"
        className="absolute left-0 top-1/2 -translate-y-1/2 -translate-x-1/2 w-9 h-9 rounded-full bg-neutral-900 border border-neutral-700 text-neutral-200 grid place-items-center opacity-0 group-hover:opacity-100 hover:border-lime-400/50 transition-all shadow-lg">
        <ChevronLeft />
      </button>
      <button
        onClick={() => scroll(1)}
        aria-label="Вперёд"
        className="absolute right-0 top-1/2 -translate-y-1/2 translate-x-1/2 w-9 h-9 rounded-full bg-neutral-900 border border-neutral-700 text-neutral-200 grid place-items-center opacity-0 group-hover:opacity-100 hover:border-lime-400/50 transition-all shadow-lg">
        <ChevronRight />
      </button>
    </div>
  )
}

// ── Карточки ────────────────────────────────────────────────────────────

function Logo({ tool, big = false }: { tool: CatalogTool; big?: boolean }) {
  const a = ACCENTS[tool.accent]
  const url = iconUrl(tool)
  const [ok, setOk] = useState(!!url)
  const box = big ? "w-16 h-16 rounded-2xl text-2xl" : "w-11 h-11 rounded-xl text-base"
  if (url && ok) {
    return (
      // eslint-disable-next-line @next/next/no-img-element
      <img
        src={url}
        alt={tool.name}
        onError={() => setOk(false)}
        className={`${box} object-cover border border-neutral-700 bg-neutral-900 shrink-0`}
      />
    )
  }
  return (
    <div className={`${box} border grid place-items-center font-bold shrink-0 ${a.tile}`}>
      {tool.logo}
    </div>
  )
}

function FeaturedCard({ tool }: { tool: CatalogTool }) {
  const a = ACCENTS[tool.accent]
  return (
    <Link
      href={`/catalog/${tool.slug}`}
      className="snap-start shrink-0 w-[360px] rounded-xl border border-neutral-800 hover:border-lime-400/40 transition-all overflow-hidden group/card">
      <div
        className={`relative h-[150px] bg-gradient-to-br ${a.grad} to-neutral-950 p-4 flex items-start justify-between`}>
        <Logo tool={tool} big />
        <span className="text-[10px] uppercase tracking-wider px-2 py-0.5 rounded-md bg-black/40 border border-white/10 text-neutral-200">
          {tool.typeLabel}
        </span>
      </div>
      <div className="p-4 bg-neutral-950 group-hover/card:bg-neutral-900 transition-colors">
        <div className="text-base font-semibold">{tool.name}</div>
        <p className="text-sm text-neutral-400 font-sans mt-1 line-clamp-2 min-h-[2.5rem]">
          {tool.short}
        </p>
        <div className="flex items-center gap-1.5 flex-wrap mt-2">
          {tool.tags.map((tg) => (
            <span
              key={tg}
              className="text-[10px] px-1.5 py-0.5 rounded border border-neutral-800 text-neutral-400">
              {tg}
            </span>
          ))}
        </div>
        <div className="flex items-center justify-between mt-3">
          <div className="flex items-center gap-3 text-[12px] text-neutral-400 font-sans">
            <span className="inline-flex items-center gap-1 text-amber-400">
              <StarIcon /> {starsOf(tool) > 0 ? fmtStars(starsOf(tool)) : tool.rating}
            </span>
            {tool.users !== "—" && (
              <span className="inline-flex items-center gap-1">
                <UsersIcon /> {tool.users}
              </span>
            )}
          </div>
          <span className="text-sm px-3 py-1.5 rounded-lg border border-neutral-700 text-neutral-200 group-hover/card:text-lime-400 group-hover/card:border-lime-400/50 transition-colors">
            Подробнее
          </span>
        </div>
      </div>
    </Link>
  )
}

function ToolCard({ tool }: { tool: CatalogTool }) {
  return (
    <Link
      href={`/catalog/${tool.slug}`}
      className="snap-start shrink-0 w-[300px] max-w-full rounded-xl border border-neutral-800 bg-neutral-900/40 hover:border-lime-400/40 hover:bg-neutral-900 transition-all p-4 flex flex-col">
      <div className="flex items-start gap-3">
        <Logo tool={tool} />
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <span className="text-sm font-semibold truncate">{tool.name}</span>
            <span className="text-[9px] uppercase tracking-wider px-1.5 py-px rounded border border-neutral-700 text-neutral-500 shrink-0">
              {tool.typeLabel}
            </span>
          </div>
          <p className="text-[13px] text-neutral-400 font-sans mt-0.5 line-clamp-2">
            {tool.short}
          </p>
        </div>
      </div>
      <div className="flex items-center gap-3 text-[12px] text-neutral-500 font-sans mt-3 pt-3 border-t border-neutral-800/70">
        <span className="inline-flex items-center gap-1 text-amber-400">
          <StarIcon /> {starsOf(tool) > 0 ? fmtStars(starsOf(tool)) : tool.rating}
        </span>
        {tool.users !== "—" && (
          <span className="inline-flex items-center gap-1">
            <UsersIcon /> {tool.users}
          </span>
        )}
        <span className="ml-auto text-neutral-500 group-hover:text-lime-400">›</span>
      </div>
    </Link>
  )
}

function plural(n: number, one: string, few: string, many: string) {
  const m10 = n % 10
  const m100 = n % 100
  if (m10 === 1 && m100 !== 11) return one
  if (m10 >= 2 && m10 <= 4 && (m100 < 10 || m100 >= 20)) return few
  return many
}

// ── Иконки ──────────────────────────────────────────────────────────────

function SearchIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="11" cy="11" r="7" />
      <line x1="21" y1="21" x2="16.65" y2="16.65" />
    </svg>
  )
}
function StarIcon() {
  return (
    <svg width="13" height="13" viewBox="0 0 24 24" fill="currentColor">
      <path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z" />
    </svg>
  )
}
function UsersIcon() {
  return (
    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
      <circle cx="9" cy="7" r="4" />
      <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
    </svg>
  )
}
function ChevronRight() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polyline points="9 18 15 12 9 6" /></svg>
  )
}
function ChevronLeft() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polyline points="15 18 9 12 15 6" /></svg>
  )
}
function SparkGlyph() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor"><path d="M12 2l1.6 5.4L19 9l-5.4 1.6L12 16l-1.6-5.4L5 9l5.4-1.6L12 2zM19 14l.8 2.7L22 17l-2.2.3L19 20l-.8-2.7L16 17l2.2-.3L19 14z" /></svg>
  )
}
function RocketGlyph() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2c.71-.84.7-2.13-.09-2.91a2.18 2.18 0 0 0-2.91-.09z" /><path d="M12 15l-3-3a22 22 0 0 1 2-3.95A12.88 12.88 0 0 1 22 2c0 2.72-.78 7.5-6 11a22.35 22.35 0 0 1-4 2z" /></svg>
  )
}
function WrenchGlyph() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M14.7 6.3a4 4 0 0 0-5.4 5.4L3 18l3 3 6.3-6.3a4 4 0 0 0 5.4-5.4l-2.5 2.5-2.7-.4-.4-2.7 2.5-2.5z" /></svg>
  )
}
function GiftGlyph() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polyline points="20 12 20 22 4 22 4 12" /><rect x="2" y="7" width="20" height="5" /><line x1="12" y1="22" x2="12" y2="7" /><path d="M12 7H7.5a2.5 2.5 0 0 1 0-5C11 2 12 7 12 7z" /><path d="M12 7h4.5a2.5 2.5 0 0 0 0-5C13 2 12 7 12 7z" /></svg>
  )
}
function ClockGlyph() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="9" /><polyline points="12 7 12 12 15 14" /></svg>
  )
}
function StarGlyph() {
  return <StarIcon />
}
function PlusGlyph() {
  return (
    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><line x1="12" y1="5" x2="12" y2="19" /><line x1="5" y1="12" x2="19" y2="12" /></svg>
  )
}
function GridGlyph() {
  return (
    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="3" y="3" width="7" height="7" rx="1" /><rect x="14" y="3" width="7" height="7" rx="1" /><rect x="3" y="14" width="7" height="7" rx="1" /><rect x="14" y="14" width="7" height="7" rx="1" /></svg>
  )
}
function DotGlyph() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="4" y="4" width="16" height="16" rx="4" /></svg>
  )
}
function CubeGlyph() {
  return (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round"><path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z" /><polyline points="3.27 6.96 12 12.01 20.73 6.96" /><line x1="12" y1="22.08" x2="12" y2="12" /></svg>
  )
}
