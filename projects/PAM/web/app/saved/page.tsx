"use client"

import { useEffect, useState } from "react"
import Link from "next/link"
import ReactMarkdown from "react-markdown"
import remarkGfm from "remark-gfm"

import { listSaved, deleteSaved, type SavedMessage } from "../../lib/api"
import RefreshButton from "../refresh-button"

export default function SavedPage() {
  const [items, setItems] = useState<SavedMessage[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [tick, setTick] = useState(0)

  useEffect(() => {
    setLoading(true)
    listSaved()
      .then(setItems)
      .catch((e) => setError(String(e)))
      .finally(() => setLoading(false))
  }, [tick])

  async function remove(id: string) {
    try {
      await deleteSaved(id)
      setItems((prev) => prev.filter((x) => x.id !== id))
    } catch (e) {
      setError(String(e))
    }
  }

  return (
    <main className="max-w-5xl mx-auto px-6 py-8">
      <header className="border-b border-neutral-800 pb-6 mb-6">
        <div className="text-xs uppercase tracking-widest text-lime-400 mb-2">
          /// избранное
        </div>
        <div className="flex items-center justify-between gap-3">
          <h1 className="text-3xl font-semibold">Избранное</h1>
          <RefreshButton onClick={() => setTick((t) => t + 1)} busy={loading} />
        </div>
        <p className="text-neutral-400 mt-2 text-sm font-sans">
          Сообщения, которые ты отметил ★ — снимки, которые не теряются при
          повторном захвате разговора.
        </p>
      </header>

      {error && (
        <div className="text-red-400 text-sm font-sans mb-4">Ошибка: {error}</div>
      )}

      {loading ? (
        <div className="text-neutral-500 text-sm py-12">// загрузка…</div>
      ) : items.length === 0 ? (
        <div className="text-neutral-500 text-sm py-12 border border-dashed border-neutral-800 text-center font-sans">
          Пусто. Открой разговор в «Истории» и нажми ☆ на нужном сообщении.
        </div>
      ) : (
        <div className="space-y-3">
          {items.map((m) => (
            <article
              key={m.id}
              className="border border-neutral-800 rounded-sm p-3">
              <div className="flex justify-between items-baseline gap-3 mb-2">
                <div className="flex gap-2 items-baseline min-w-0">
                  <span className="text-[10px] uppercase tracking-wider px-1.5 py-0.5 border border-neutral-700 rounded text-neutral-400">
                    {m.source}
                  </span>
                  <span className="text-[10px] uppercase tracking-widest text-neutral-500">
                    {m.role}
                  </span>
                  <span className="text-sm font-semibold truncate font-sans">
                    {m.title || "(без названия)"}
                  </span>
                </div>
                <div className="flex items-center gap-3 shrink-0">
                  {m.conversation_id && (
                    <Link
                      href={`/c/${m.conversation_id}`}
                      className="text-[10px] uppercase tracking-wider text-neutral-500 hover:text-lime-400">
                      → к разговору
                    </Link>
                  )}
                  <button
                    onClick={() => remove(m.id)}
                    title="Убрать из избранного"
                    className="text-[10px] uppercase tracking-wider text-neutral-500 hover:text-red-400">
                    удалить
                  </button>
                </div>
              </div>
              <div className="prose prose-invert prose-sm max-w-none font-sans">
                <ReactMarkdown remarkPlugins={[remarkGfm]}>
                  {m.content}
                </ReactMarkdown>
              </div>
            </article>
          ))}
        </div>
      )}
    </main>
  )
}
