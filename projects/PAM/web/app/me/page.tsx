"use client"

import { useEffect, useMemo, useState } from "react"

import {
  listFacts,
  deleteFact,
  extractFacts,
  type ProfileFact
} from "../../lib/api"
import { getCache, setCache } from "../../lib/cache"
import RefreshButton from "../refresh-button"

export default function MePage() {
  const [items, setItems] = useState<ProfileFact[]>(
    () => getCache<ProfileFact[]>("facts") ?? []
  )
  const [loading, setLoading] = useState(
    () => getCache<ProfileFact[]>("facts") === undefined
  )
  const [error, setError] = useState<string | null>(null)
  const [tick, setTick] = useState(0)
  const [extracting, setExtracting] = useState(false)
  const [notice, setNotice] = useState<string | null>(null)

  useEffect(() => {
    setLoading(true)
    listFacts()
      .then((d) => {
        setItems(d)
        setCache("facts", d)
      })
      .catch((e) => setError(String(e)))
      .finally(() => setLoading(false))
  }, [tick])

  // Группировка по категории (бэкенд уже сортирует по category, created_at).
  const groups = useMemo(() => {
    const m = new Map<string, ProfileFact[]>()
    for (const f of items) {
      const arr = m.get(f.category) || []
      arr.push(f)
      m.set(f.category, arr)
    }
    return Array.from(m.entries())
  }, [items])

  async function remove(id: string) {
    try {
      await deleteFact(id)
      setItems((prev) => prev.filter((x) => x.id !== id))
    } catch (e) {
      setError(String(e))
    }
  }

  async function refreshProfile() {
    setExtracting(true)
    setError(null)
    setNotice(null)
    try {
      const res = await extractFacts(10)
      setNotice(
        `Обработано разговоров: ${res.conversations_processed}, новых фактов: ${res.facts_added}.`
      )
      setTick((t) => t + 1) // перезагрузить список
    } catch (e) {
      setError(String(e))
    } finally {
      setExtracting(false)
    }
  }

  return (
    <main className="max-w-5xl mx-auto px-6 py-8">
      <header className="border-b border-neutral-800 pb-6 mb-6">
        <div className="text-xs uppercase tracking-widest text-lime-400 mb-2">
          /// профиль
        </div>
        <div className="flex items-center justify-between gap-3">
          <h1 className="text-3xl font-semibold">Память обо мне</h1>
          <div className="flex items-center gap-2">
            <button
              onClick={refreshProfile}
              disabled={extracting}
              className="text-xs px-3 py-1.5 rounded-md border border-neutral-700 text-neutral-300 hover:text-lime-400 hover:border-lime-400/50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors">
              {extracting ? "обновляю…" : "обновить профиль"}
            </button>
            <RefreshButton onClick={() => setTick((t) => t + 1)} busy={loading} />
          </div>
        </div>
        <p className="text-neutral-400 mt-2 text-sm font-sans">
          Устойчивые факты о тебе, извлечённые из истории разговоров. PAM
          использует их в чате, чтобы отвечать под твоё окружение. Каждый факт
          подкреплён цитатой из переписки — можно удалить лишнее.
        </p>
      </header>

      {error && (
        <div className="text-red-400 text-sm font-sans mb-4">Ошибка: {error}</div>
      )}
      {notice && (
        <div className="text-lime-400 text-sm font-sans mb-4">{notice}</div>
      )}

      {loading && items.length === 0 ? (
        <div className="text-neutral-500 text-sm py-12">// загрузка…</div>
      ) : items.length === 0 ? (
        <div className="text-neutral-500 text-sm py-12 border border-dashed border-neutral-800 text-center font-sans">
          Пока пусто. Нажми «обновить профиль» — PAM просканирует историю и
          выделит устойчивые факты о тебе.
        </div>
      ) : (
        <div className="space-y-8">
          {groups.map(([category, facts]) => (
            <section key={category}>
              <h2 className="text-xs uppercase tracking-widest text-neutral-500 mb-3">
                {category}{" "}
                <span className="text-neutral-700">· {facts.length}</span>
              </h2>
              <div className="space-y-2">
                {facts.map((f) => (
                  <article
                    key={f.id}
                    className="border border-neutral-800 rounded-sm p-3 group">
                    <div className="flex justify-between items-start gap-3">
                      <p className="text-sm font-sans text-neutral-100">
                        {f.content}
                      </p>
                      <div className="flex items-center gap-3 shrink-0">
                        <span
                          title="уверенность"
                          className="text-[10px] tabular-nums text-neutral-600">
                          {Math.round(f.confidence * 100)}%
                        </span>
                        <button
                          onClick={() => remove(f.id)}
                          title="Удалить факт"
                          className="text-[10px] uppercase tracking-wider text-neutral-500 hover:text-red-400 opacity-0 group-hover:opacity-100 transition-opacity">
                          удалить
                        </button>
                      </div>
                    </div>
                    {f.source_excerpt && (
                      <p className="mt-1.5 text-xs font-sans text-neutral-500 border-l-2 border-neutral-800 pl-2 italic">
                        «{f.source_excerpt}»
                      </p>
                    )}
                  </article>
                ))}
              </div>
            </section>
          ))}
        </div>
      )}
    </main>
  )
}
