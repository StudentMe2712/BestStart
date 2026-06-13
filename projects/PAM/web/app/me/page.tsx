"use client"

import { useEffect, useMemo, useState } from "react"

import {
  listFacts,
  deleteFact,
  extractFacts,
  approveFact,
  rejectFact,
  updateFact,
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
  const [editing, setEditing] = useState<string | null>(null)
  const [draft, setDraft] = useState("")
  const [busy, setBusy] = useState<string | null>(null)

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

  // Очередь проверки: новые факты ждут решения, в память чата идут approved+edited.
  const pending = useMemo(
    () => items.filter((f) => f.status === "pending_review"),
    [items]
  )
  const accepted = useMemo(
    () => items.filter((f) => f.status === "approved" || f.status === "edited"),
    [items]
  )

  // Принятые факты группируем по категории (как раньше).
  const groups = useMemo(() => {
    const m = new Map<string, ProfileFact[]>()
    for (const f of accepted) {
      const arr = m.get(f.category) || []
      arr.push(f)
      m.set(f.category, arr)
    }
    return Array.from(m.entries())
  }, [accepted])

  function patchLocal(updated: ProfileFact) {
    setItems((prev) => prev.map((x) => (x.id === updated.id ? updated : x)))
  }

  async function onApprove(id: string) {
    setBusy(id)
    setError(null)
    try {
      patchLocal(await approveFact(id))
    } catch (e) {
      setError(String(e))
    } finally {
      setBusy(null)
    }
  }

  async function onReject(id: string) {
    setBusy(id)
    setError(null)
    try {
      // status → rejected: факт выпадает и из очереди, и из принятых (скрыт).
      patchLocal(await rejectFact(id))
    } catch (e) {
      setError(String(e))
    } finally {
      setBusy(null)
    }
  }

  function startEdit(f: ProfileFact) {
    setEditing(f.id)
    setDraft(f.content)
  }

  function cancelEdit() {
    setEditing(null)
    setDraft("")
  }

  async function saveEdit(id: string) {
    const content = draft.trim()
    if (!content) return
    setBusy(id)
    setError(null)
    try {
      patchLocal(await updateFact(id, { content })) // status → edited (принят)
      cancelEdit()
    } catch (e) {
      setError(String(e))
    } finally {
      setBusy(null)
    }
  }

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
        `Обработано разговоров: ${res.conversations_processed}, новых фактов на проверку: ${res.facts_added}.`
      )
      setTick((t) => t + 1) // перезагрузить список
    } catch (e) {
      setError(String(e))
    } finally {
      setExtracting(false)
    }
  }

  return (
    <main className="max-w-[1700px] mx-auto px-6 py-8">
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
          Устойчивые факты о тебе, извлечённые из истории разговоров. Новые факты
          сначала попадают в очередь проверки — в память чата идут только те, что
          ты <span className="text-lime-400">принял</span>. Каждый факт подкреплён
          цитатой из переписки.
        </p>
      </header>

      {error && (
        <div className="text-red-400 text-sm font-sans mb-4">Ошибка: {error}</div>
      )}
      {notice && (
        <div className="text-lime-400 text-sm font-sans mb-4">{notice}</div>
      )}

      {/* --- Очередь проверки --- */}
      {pending.length > 0 && (
        <section className="mb-10">
          <div className="flex items-center gap-2 mb-3">
            <h2 className="text-xs uppercase tracking-widest text-amber-400">
              На проверку
            </h2>
            <span className="text-[10px] px-1.5 py-0.5 rounded-full bg-amber-400/15 text-amber-300 tabular-nums">
              {pending.length}
            </span>
          </div>
          <div className="space-y-2">
            {pending.map((f) => (
              <article
                key={f.id}
                className="border border-amber-400/30 bg-amber-400/[0.03] rounded-sm p-3">
                <div className="flex items-center gap-2 mb-1.5">
                  <span className="text-[10px] uppercase tracking-wider text-neutral-500">
                    {f.category}
                  </span>
                  <span
                    title="уверенность"
                    className="text-[10px] tabular-nums text-neutral-600">
                    {Math.round(f.confidence * 100)}%
                  </span>
                </div>

                {editing === f.id ? (
                  <textarea
                    value={draft}
                    onChange={(e) => setDraft(e.target.value)}
                    rows={2}
                    className="w-full text-sm font-sans bg-neutral-950 border border-neutral-700 rounded-sm p-2 text-neutral-100 focus:outline-none focus:border-lime-400/50"
                  />
                ) : (
                  <p className="text-sm font-sans text-neutral-100">{f.content}</p>
                )}

                {f.source_excerpt && (
                  <p className="mt-1.5 text-xs font-sans text-neutral-500 border-l-2 border-neutral-800 pl-2 italic">
                    «{f.source_excerpt}»
                  </p>
                )}

                <div className="flex items-center gap-3 mt-2.5 text-[11px] uppercase tracking-wider">
                  {editing === f.id ? (
                    <>
                      <button
                        onClick={() => saveEdit(f.id)}
                        disabled={busy === f.id || !draft.trim()}
                        className="text-lime-400 hover:text-lime-300 disabled:opacity-40">
                        сохранить и принять
                      </button>
                      <button
                        onClick={cancelEdit}
                        disabled={busy === f.id}
                        className="text-neutral-500 hover:text-neutral-300">
                        отмена
                      </button>
                    </>
                  ) : (
                    <>
                      <button
                        onClick={() => onApprove(f.id)}
                        disabled={busy === f.id}
                        className="text-lime-400 hover:text-lime-300 disabled:opacity-40">
                        ✓ принять
                      </button>
                      <button
                        onClick={() => startEdit(f)}
                        disabled={busy === f.id}
                        className="text-neutral-400 hover:text-lime-400 disabled:opacity-40">
                        ✎ изменить
                      </button>
                      <button
                        onClick={() => onReject(f.id)}
                        disabled={busy === f.id}
                        className="text-neutral-500 hover:text-red-400 disabled:opacity-40">
                        ✕ отклонить
                      </button>
                    </>
                  )}
                </div>
              </article>
            ))}
          </div>
        </section>
      )}

      {/* --- Принятые факты --- */}
      {loading && items.length === 0 ? (
        <div className="text-neutral-500 text-sm py-12">// загрузка…</div>
      ) : items.length === 0 ? (
        <div className="text-neutral-500 text-sm py-12 border border-dashed border-neutral-800 text-center font-sans">
          Пока пусто. Нажми «обновить профиль» — PAM просканирует историю и
          выделит устойчивые факты о тебе на проверку.
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
                        {f.status === "edited" && (
                          <span
                            title="отредактировано вручную"
                            className="text-[10px] uppercase tracking-wider text-neutral-600">
                            ред.
                          </span>
                        )}
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
