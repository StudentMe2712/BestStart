"use client"

import { useMemo, useState, type ReactNode } from "react"

import {
  createMemoryItem,
  patchMemoryItem,
  type MemoryItem,
  type Project
} from "../../lib/api"
import {
  SOLUTION_STATUSES,
  statusOf,
  withStatus,
  newSolutionTags,
  parseSolution,
  buildSolutionContent,
  type SolutionStatus,
  type SolutionFields
} from "../../lib/solutions"

/**
 * Общий редактор решения (модалка). Используется в двух местах:
 *  • /solutions — ручное создание / правка (`item`);
 *  • чат «Сохранить как решение» — предзаполненный черновик (`initial`).
 *
 * Упрощённая форма (V1.2): Заголовок · Статус · [Проект] · Проблема · Причина ·
 * Решение · Заметки. Категория и навигационные теги НЕ вводятся вручную — их
 * проставит AI-теггер после сохранения (cat:/free tags). Команды — внутри
 * «Решение» как блоки Markdown (отдельного поля больше нет). Существующая секция
 * «Команды» у старых решений сохраняется при правке (parseSolution → f.commands).
 */

const EMPTY: SolutionFields = {
  problem: "",
  cause: "",
  solution: "",
  commands: "",
  notes: ""
}

export interface SolutionDraftInit {
  title?: string
  status?: SolutionStatus
  fields?: Partial<SolutionFields>
  /** источник memory_item при создании (по умолчанию "web"; чат шлёт "chat"). */
  source?: string
  /** ссылка на источник (для чата — id разговора). */
  sourceRef?: string | null
  projectId?: string | null
}

export default function SolutionEditor({
  item,
  initial,
  projects,
  onClose,
  onSaved,
  onError
}: {
  item?: MemoryItem | null
  initial?: SolutionDraftInit
  projects: Project[]
  onClose: () => void
  onSaved: (it: MemoryItem) => void
  onError: (e: string) => void
}) {
  const initFields = useMemo<SolutionFields>(
    () =>
      item
        ? parseSolution(item.content)
        : { ...EMPTY, ...(initial?.fields || {}) },
    [item, initial]
  )
  const [title, setTitle] = useState(item?.title || initial?.title || "")
  const [status, setStatus] = useState<SolutionStatus>(
    item ? statusOf(item.tags) : initial?.status || "unresolved"
  )
  const [projectId, setProjectId] = useState(
    item?.project_id || initial?.projectId || ""
  )
  const [f, setF] = useState<SolutionFields>(initFields)
  const [saving, setSaving] = useState(false)

  function set<K extends keyof SolutionFields>(k: K, v: string) {
    setF((prev) => ({ ...prev, [k]: v }))
  }

  const heading = item
    ? "Редактировать решение"
    : initial?.source === "chat"
      ? "Черновик решения из чата"
      : "Новое решение"

  async function save() {
    const content = buildSolutionContent(f)
    if (!title.trim() && !content) {
      onError("Заполни заголовок и хотя бы одну секцию.")
      return
    }
    setSaving(true)
    onError("")
    try {
      let saved: MemoryItem
      if (item) {
        // Статус меняем через withStatus — сохраняем cat: и навигационные теги.
        saved = await patchMemoryItem(item.id, {
          title: title.trim() || null,
          content: content || title.trim(),
          tags: withStatus(item.tags, status),
          ...(projectId ? { project_id: projectId } : {})
        })
      } else {
        // Новое: только st:<status>; cat:/теги доставит теггер после сохранения.
        saved = await createMemoryItem({
          title: title.trim() || null,
          content: content || title.trim(),
          tags: newSolutionTags(status),
          source: initial?.source || "web",
          source_ref: initial?.sourceRef ?? null,
          item_type: "solution",
          project_id: projectId || null
        })
      }
      onSaved(saved)
    } catch (e) {
      onError(String(e))
    } finally {
      setSaving(false)
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
          <h3 className="text-base font-semibold">{heading}</h3>
          <button
            onClick={onClose}
            className="text-neutral-500 hover:text-neutral-200 transition-colors text-xl leading-none">
            ×
          </button>
        </div>

        <div className="p-5 space-y-4 font-sans">
          <Field label="Заголовок">
            <input
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              placeholder="Коротко: в чём проблема"
              className="w-full bg-neutral-900 border border-neutral-800 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-lime-400/50"
            />
          </Field>

          <div className="grid grid-cols-2 gap-3">
            <Field label="Статус">
              <select
                value={status}
                onChange={(e) => setStatus(e.target.value as SolutionStatus)}
                className="w-full bg-neutral-900 border border-neutral-800 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-lime-400/50">
                {SOLUTION_STATUSES.map((s) => (
                  <option key={s.key} value={s.key}>
                    {s.label}
                  </option>
                ))}
              </select>
            </Field>
            {projects.length > 0 && (
              <Field label="Проект (необязательно)">
                <select
                  value={projectId}
                  onChange={(e) => setProjectId(e.target.value)}
                  className="w-full bg-neutral-900 border border-neutral-800 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-lime-400/50">
                  <option value="">— без проекта —</option>
                  {projects.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.name}
                    </option>
                  ))}
                </select>
              </Field>
            )}
          </div>

          <Area label="Проблема" value={f.problem} onChange={(v) => set("problem", v)} />
          <Area label="Причина" value={f.cause} onChange={(v) => set("cause", v)} />
          <Area
            label="Решение"
            value={f.solution}
            onChange={(v) => set("solution", v)}
            hint="Markdown · блоки кода ``` · команды · логи"
            rows={5}
          />
          <Area label="Заметки" value={f.notes} onChange={(v) => set("notes", v)} />
        </div>

        <div className="flex items-center justify-end gap-2 px-5 h-16 border-t border-neutral-800">
          <button
            onClick={onClose}
            disabled={saving}
            className="text-sm px-4 py-2 rounded-lg border border-neutral-700 text-neutral-300 hover:text-neutral-100 transition-colors">
            Отмена
          </button>
          <button
            onClick={save}
            disabled={saving}
            className="text-sm px-4 py-2 rounded-lg bg-lime-400 text-neutral-950 font-medium hover:bg-lime-300 disabled:opacity-50 transition-colors">
            {saving ? "Сохраняю…" : item ? "Сохранить" : "Создать"}
          </button>
        </div>
      </div>
    </div>
  )
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="block">
      <span className="text-[11px] uppercase tracking-wider text-neutral-500 block mb-1">
        {label}
      </span>
      {children}
    </label>
  )
}

function Area({
  label,
  value,
  onChange,
  hint,
  rows = 3
}: {
  label: string
  value: string
  onChange: (v: string) => void
  hint?: string
  rows?: number
}) {
  return (
    <Field label={label}>
      <textarea
        value={value}
        onChange={(e) => onChange(e.target.value)}
        rows={rows}
        className="w-full bg-neutral-900 border border-neutral-800 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-lime-400/50 resize-y"
      />
      {hint && <span className="text-[10px] text-neutral-600 mt-1 block">{hint}</span>}
    </Field>
  )
}
