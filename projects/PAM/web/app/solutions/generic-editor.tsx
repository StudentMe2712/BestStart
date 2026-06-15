"use client"

import { useState, type ReactNode } from "react"

import {
  createMemoryItem,
  patchMemoryItem,
  type MemoryItem
} from "../../lib/api"
import { ITEM_TYPE_OPTIONS } from "../../lib/knowledge"
import { freeTags } from "../../lib/solutions"

/**
 * Generic-редактор memory item (модалка) — для всех типов, КРОМЕ solution.
 * Лёгкое редактирование: заголовок · тип · важность · теги · содержимое (markdown).
 * Реюз существующих endpoint'ов (createMemoryItem / patchMemoryItem) — новых
 * сущностей/полей нет. Solution правится своим специализированным редактором
 * (editor.tsx); сюда он не попадает (линза «Все» уводит solution в раздел «Решения»).
 */

// В выпадашке типа решения нет — это не его редактор.
const GENERIC_TYPES = ITEM_TYPE_OPTIONS.filter((o) => o.type !== "solution")

export default function GenericEditor({
  item,
  onClose,
  onSaved,
  onError
}: {
  item?: MemoryItem | null
  onClose: () => void
  onSaved: (it: MemoryItem) => void
  onError: (e: string) => void
}) {
  const [title, setTitle] = useState(item?.title || "")
  const [type, setType] = useState(
    item && item.item_type !== "solution" ? item.item_type : "note"
  )
  const [importance, setImportance] = useState(item?.importance || 3)
  const [tags, setTags] = useState((item ? freeTags(item.tags) : []).join(", "))
  const [content, setContent] = useState(item?.content || "")
  const [saving, setSaving] = useState(false)

  async function save() {
    const body = content.trim()
    if (!body) {
      onError("Содержимое не может быть пустым.")
      return
    }
    const tagList = tags
      .split(",")
      .map((t) => t.trim().toLowerCase())
      .filter(Boolean)
      .slice(0, 8)
    setSaving(true)
    onError("")
    try {
      let saved: MemoryItem
      if (item) {
        saved = await patchMemoryItem(item.id, {
          title: title.trim() || null,
          content: body,
          item_type: type,
          importance,
          tags: tagList
        })
      } else {
        // На создании importance не передаётся (его проставит AI-теггер); тип/теги —
        // явные, чтобы теггер их не перетирал.
        saved = await createMemoryItem({
          title: title.trim() || null,
          content: body,
          item_type: type,
          tags: tagList,
          source: "web"
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
          <h3 className="text-base font-semibold">
            {item ? "Редактировать элемент" : "Новый элемент"}
          </h3>
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
              placeholder="Коротко, о чём это"
              className="w-full bg-neutral-900 border border-neutral-800 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-lime-400/50"
            />
          </Field>

          <div className="grid grid-cols-2 gap-3">
            <Field label="Тип">
              <select
                value={type}
                onChange={(e) => setType(e.target.value)}
                className="w-full bg-neutral-900 border border-neutral-800 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-lime-400/50">
                {GENERIC_TYPES.map((o) => (
                  <option key={o.type} value={o.type}>
                    {o.label}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Важность">
              <select
                value={importance}
                onChange={(e) => setImportance(Number(e.target.value))}
                className="w-full bg-neutral-900 border border-neutral-800 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-lime-400/50">
                {[1, 2, 3, 4, 5].map((n) => (
                  <option key={n} value={n}>
                    {n}
                  </option>
                ))}
              </select>
            </Field>
          </div>

          <Field label="Теги (через запятую)">
            <input
              value={tags}
              onChange={(e) => setTags(e.target.value)}
              placeholder="docker, postgres, бэкап"
              className="w-full bg-neutral-900 border border-neutral-800 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-lime-400/50"
            />
          </Field>

          <Field label="Содержимое">
            <textarea
              value={content}
              onChange={(e) => setContent(e.target.value)}
              rows={10}
              placeholder="Текст · Markdown · блоки кода ```"
              className="w-full bg-neutral-900 border border-neutral-800 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-lime-400/50 resize-y"
            />
          </Field>
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
