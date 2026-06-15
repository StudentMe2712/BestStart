/**
 * Knowledge Hub — общий словарь типов memory_items и мелкие хелперы для
 * generic-представления (линза «Все»). Solution-специфика (статусы, категории,
 * секции Проблема/Причина/Решение) остаётся в web/lib/solutions.ts; здесь — только
 * нейтральные для всех типов вещи. Источник истины по типам — models.py::ITEM_TYPES.
 */

export interface ItemTypeOption {
  type: string
  label: string
}

// Порядок = порядок фильтров в сайдбаре линзы «Все».
export const ITEM_TYPE_OPTIONS: ItemTypeOption[] = [
  { type: "idea", label: "Идея" },
  { type: "note", label: "Заметка" },
  { type: "article", label: "Статья" },
  { type: "tool", label: "Инструмент" },
  { type: "code", label: "Код" },
  { type: "prompt", label: "Промпт" },
  { type: "learning", label: "Обучение" },
  { type: "decision", label: "Решение (общее)" },
  { type: "solution", label: "Решение" }
]

export const ITEM_TYPE_LABEL: Record<string, string> = Object.fromEntries(
  ITEM_TYPE_OPTIONS.map((o) => [o.type, o.label])
)

export function itemTypeLabel(type: string): string {
  return ITEM_TYPE_LABEL[type] || type
}

/** Заголовок-предпросмотр для generic-карточки/детали, когда title не задан. */
export function itemPreview(content: string, max = 80): string {
  return (content || "")
    .replace(/[#>*`_~]/g, " ")
    .replace(/\s+/g, " ")
    .trim()
    .slice(0, max)
}
