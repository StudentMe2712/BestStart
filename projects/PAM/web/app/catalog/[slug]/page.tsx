"use client"

import { useState } from "react"
import Link from "next/link"
import { useParams } from "next/navigation"

import {
  getTool,
  ACCENTS,
  CATEGORY_LABEL,
  starsOf,
  fmtStars,
  iconUrl,
  type CatalogTool
} from "../../../lib/catalog"

export default function ToolDetailPage() {
  const { slug } = useParams<{ slug: string }>()
  const tool = getTool(slug)

  if (!tool) {
    return (
      <main className="max-w-2xl mx-auto px-6 py-16 text-center">
        <Link
          href="/catalog"
          className="text-sm text-neutral-400 hover:text-lime-400 transition-colors">
          ← Каталог
        </Link>
        <div className="mt-8 border border-dashed border-neutral-800 rounded-xl p-10 text-neutral-400 font-sans">
          Инструмент не найден.
        </div>
      </main>
    )
  }

  const a = ACCENTS[tool.accent]

  return (
    <main className="max-w-[1700px] mx-auto px-6 py-8">
      <Link
        href="/catalog"
        className="inline-flex items-center gap-2 text-sm text-neutral-400 hover:text-lime-400 transition-colors">
        ← Каталог
      </Link>

      {/* Шапка */}
      <header className="mt-5 flex flex-col sm:flex-row sm:items-center gap-5">
        <BigLogo tool={tool} />
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2.5 flex-wrap">
            <h1 className="text-2xl font-semibold">{tool.name}</h1>
            <span className="text-[10px] uppercase tracking-wider px-2 py-0.5 rounded-md border border-neutral-700 text-neutral-400">
              {tool.typeLabel}
            </span>
            <span className="text-[11px] text-neutral-500 font-sans">
              {CATEGORY_LABEL[tool.category]}
            </span>
          </div>
          <p className="text-neutral-400 font-sans text-sm mt-1.5 max-w-2xl">
            {tool.short}
          </p>
          <div className="flex items-center gap-4 text-[13px] font-sans mt-2.5 flex-wrap">
            <span className="inline-flex items-center gap-1 text-amber-400">
              <StarIcon /> {tool.rating}
            </span>
            {starsOf(tool) > 0 && (
              <span className="inline-flex items-center gap-1 text-neutral-400">
                <StarIcon /> {fmtStars(starsOf(tool))} на GitHub
              </span>
            )}
            {tool.users !== "—" && (
              <span className="inline-flex items-center gap-1 text-neutral-400">
                <UsersIcon /> {tool.users} пользователей
              </span>
            )}
          </div>
        </div>
        <a
          href={tool.url}
          target="_blank"
          rel="noopener noreferrer"
          className="shrink-0 px-5 py-2.5 rounded-lg bg-lime-400 text-neutral-950 font-medium text-sm hover:bg-lime-300 transition-colors text-center">
          Открыть сайт ↗
        </a>
      </header>

      {/* Превью / скриншоты */}
      <div className="mt-7 grid grid-cols-1 md:grid-cols-3 gap-4">
        {[0, 1, 2].map((i) => (
          <div
            key={i}
            className={`h-44 rounded-xl border border-neutral-800 bg-gradient-to-br ${a.grad} to-neutral-950 grid place-items-center`}>
            <span className={`text-4xl font-bold opacity-40 ${a.text}`}>
              {tool.logo}
            </span>
          </div>
        ))}
      </div>

      <div className="mt-8 grid grid-cols-1 lg:grid-cols-[1fr_320px] gap-8 items-start">
        {/* Основная колонка */}
        <div className="space-y-8 min-w-0">
          <section>
            <h2 className="text-lg font-semibold mb-2">Описание</h2>
            <p className="text-neutral-300 font-sans text-sm leading-relaxed">
              {tool.description}
            </p>
          </section>

          {tool.install && (
            <section>
              <h2 className="text-lg font-semibold mb-2">Установка</h2>
              <pre className="rounded-lg border border-neutral-800 bg-neutral-900/70 p-4 text-sm overflow-x-auto">
                <code className="text-lime-300 font-mono">{tool.install}</code>
              </pre>
              <p className="text-[12px] text-neutral-600 font-sans mt-1.5">
                Добавь сервер в конфиг MCP своего клиента (Claude Code / Cursor /
                VS Code) и перезапусти его.
              </p>
            </section>
          )}

          {tool.compatibility && tool.compatibility.length > 0 && (
            <section>
              <h2 className="text-lg font-semibold mb-2">Совместимость</h2>
              <div className="flex items-center gap-2 flex-wrap">
                {tool.compatibility.map((c) => (
                  <span
                    key={c}
                    className="text-sm font-sans px-3 py-1.5 rounded-lg border border-neutral-800 bg-neutral-900/40 text-neutral-300">
                    {c}
                  </span>
                ))}
              </div>
            </section>
          )}

          <section>
            <h2 className="text-lg font-semibold mb-3">Отзывы</h2>
            <div className="rounded-xl border border-neutral-800 bg-neutral-950 p-5 flex items-center gap-5">
              <div className="text-center shrink-0">
                <div className="text-3xl font-semibold text-amber-400">
                  {tool.rating}
                </div>
                <div className="flex items-center gap-0.5 mt-1 text-amber-400 justify-center">
                  {Array.from({ length: 5 }).map((_, i) => (
                    <span
                      key={i}
                      className={i < Math.round(tool.rating) ? "opacity-100" : "opacity-25"}>
                      <StarIcon />
                    </span>
                  ))}
                </div>
              </div>
              <p className="text-neutral-400 font-sans text-sm">
                Оценка сообщества PAM. Отзывы и обзоры появятся по мере роста
                каталога — поделись своим опытом через «Предложить инструмент».
              </p>
            </div>
          </section>
        </div>

        {/* Правая колонка */}
        <aside className="space-y-4">
          <div className="rounded-xl border border-neutral-800 bg-neutral-950 p-4">
            <h3 className="text-xs uppercase tracking-widest text-neutral-500 mb-3">
              Об инструменте
            </h3>
            <InfoRow label="Тип" value={tool.typeLabel} />
            <InfoRow label="Категория" value={CATEGORY_LABEL[tool.category]} />
            <InfoRow label="Рейтинг" value={`★ ${tool.rating}`} />
            {starsOf(tool) > 0 && (
              <InfoRow label="GitHub" value={`★ ${fmtStars(starsOf(tool))}`} />
            )}
            {tool.users !== "—" && (
              <InfoRow label="Пользователи" value={tool.users} />
            )}
          </div>

          <div className="rounded-xl border border-neutral-800 bg-neutral-950 p-4">
            <h3 className="text-xs uppercase tracking-widest text-neutral-500 mb-3">
              Теги
            </h3>
            <div className="flex items-center gap-1.5 flex-wrap">
              {tool.tags.map((t) => (
                <span
                  key={t}
                  className="text-[11px] px-2 py-0.5 rounded border border-neutral-800 text-neutral-400">
                  {t}
                </span>
              ))}
            </div>
          </div>

          <div className="rounded-xl border border-neutral-800 bg-neutral-950 p-4">
            <h3 className="text-xs uppercase tracking-widest text-neutral-500 mb-3">
              Ссылки
            </h3>
            <a
              href={tool.url}
              target="_blank"
              rel="noopener noreferrer"
              className="block text-sm font-sans text-neutral-300 hover:text-lime-400 transition-colors py-1">
              Сайт / репозиторий ↗
            </a>
            {tool.docsUrl && (
              <a
                href={tool.docsUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="block text-sm font-sans text-neutral-300 hover:text-lime-400 transition-colors py-1">
                Документация ↗
              </a>
            )}
          </div>
        </aside>
      </div>
    </main>
  )
}

function BigLogo({ tool }: { tool: CatalogTool }) {
  const a = ACCENTS[tool.accent]
  const url = iconUrl(tool)
  const [ok, setOk] = useState(!!url)
  if (url && ok) {
    return (
      // eslint-disable-next-line @next/next/no-img-element
      <img
        src={url}
        alt={tool.name}
        onError={() => setOk(false)}
        className="w-20 h-20 rounded-2xl object-cover border border-neutral-700 bg-neutral-900 shrink-0"
      />
    )
  }
  return (
    <div
      className={`w-20 h-20 rounded-2xl border grid place-items-center text-3xl font-bold shrink-0 ${a.tile}`}>
      {tool.logo}
    </div>
  )
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between gap-3 py-1.5 text-sm font-sans border-b border-neutral-900 last:border-0">
      <span className="text-neutral-500">{label}</span>
      <span className="text-neutral-200 text-right">{value}</span>
    </div>
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
