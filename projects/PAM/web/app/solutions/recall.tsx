"use client"

import { useState } from "react"

import { recall, type RecallResult } from "../../lib/api"
import { itemTypeLabel, itemPreview } from "../../lib/knowledge"
import Markdown from "../markdown"

/**
 * «Спросить память» — тонкая обёртка над существующим POST /memory/recall.
 * Один запрос → один ответ (синтез LLM) + найденные элементы. Это НЕ чат: нет
 * истории, нет многошаговости, нет стриминга. Ретрив/синтез целиком на бэкенде.
 */
export default function RecallModal({ onClose }: { onClose: () => void }) {
  const [q, setQ] = useState("")
  const [loading, setLoading] = useState(false)
  const [res, setRes] = useState<RecallResult | null>(null)
  const [error, setError] = useState<string | null>(null)

  async function ask() {
    const query = q.trim()
    if (!query || loading) return
    setLoading(true)
    setError(null)
    try {
      setRes(await recall(query))
    } catch (e) {
      setError(String(e))
      setRes(null)
    } finally {
      setLoading(false)
    }
  }

  return (
    <div
      className="fixed inset-0 z-30 bg-black/60 backdrop-blur-sm flex items-start justify-center overflow-y-auto py-10 px-4"
      onClick={onClose}>
      <div
        className="w-full max-w-2xl bg-neutral-950 border border-neutral-800 rounded-xl shadow-2xl"
        onClick={(e) => e.stopPropagation()}>
        <div className="flex items-center justify-between px-5 h-14 border-b border-neutral-800">
          <h3 className="text-base font-semibold">Спросить память</h3>
          <button
            onClick={onClose}
            className="text-neutral-500 hover:text-neutral-200 transition-colors text-xl leading-none">
            ×
          </button>
        </div>

        <div className="p-5 font-sans">
          <textarea
            value={q}
            onChange={(e) => setQ(e.target.value)}
            onKeyDown={(e) => {
              if ((e.metaKey || e.ctrlKey) && e.key === "Enter") ask()
            }}
            rows={3}
            autoFocus
            placeholder="Что я сохранял по…? Какие решения были по…? (Ctrl/⌘+Enter)"
            className="w-full bg-neutral-900 border border-neutral-800 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-lime-400/50 resize-y"
          />
          <div className="flex items-center justify-between mt-3">
            <span className="text-[11px] text-neutral-600">
              Ответ собирается только из твоей памяти (memory_items).
            </span>
            <button
              onClick={ask}
              disabled={loading || !q.trim()}
              className="text-sm px-4 py-2 rounded-lg bg-lime-400 text-neutral-950 font-medium hover:bg-lime-300 disabled:opacity-50 transition-colors">
              {loading ? "Ищу…" : "Спросить память"}
            </button>
          </div>

          {error && (
            <div className="mt-4 text-red-400 text-sm">Ошибка: {error}</div>
          )}

          {res && (
            <div className="mt-5 space-y-5">
              {res.answer && (
                <div className="rounded-lg border border-neutral-800 bg-neutral-900/40 px-4 py-3">
                  <Markdown>{res.answer}</Markdown>
                </div>
              )}
              <div>
                <div className="text-[11px] uppercase tracking-wider text-neutral-500 mb-2">
                  Найдено в памяти · {res.items.length}
                </div>
                {res.items.length === 0 ? (
                  <div className="text-neutral-500 text-[13px] py-6 px-3 border border-dashed border-neutral-800 rounded-lg text-center">
                    Ничего не нашлось по этому запросу.
                  </div>
                ) : (
                  <div className="space-y-2">
                    {res.items.map((it) => (
                      <div
                        key={it.id}
                        className="rounded-lg border border-neutral-800 bg-neutral-900/30 p-3">
                        <div className="flex items-center gap-2">
                          <span className="text-[10px] font-sans px-1.5 py-0.5 rounded border border-neutral-700 text-neutral-400 shrink-0">
                            {itemTypeLabel(it.item_type)}
                          </span>
                          <span className="text-[13px] text-neutral-100 truncate">
                            {it.title || itemPreview(it.content) || "—"}
                          </span>
                        </div>
                        {(it.summary || it.content) && (
                          <div className="text-[12px] text-neutral-500 mt-1.5 line-clamp-2">
                            {it.summary || itemPreview(it.content, 200)}
                          </div>
                        )}
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
