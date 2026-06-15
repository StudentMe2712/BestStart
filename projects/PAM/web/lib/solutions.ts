/**
 * Раздел «Решения» — тонкий слой поверх `memory_items` (item_type="solution").
 * Никакой отдельной таблицы: категория и статус хранятся как зарезервированные
 * теги `cat:<slug>` / `st:<status>`, а Проблема/Причина/Решение/Команды/Заметки —
 * как markdown-секции внутри `content`. Здесь — единый источник правды для FE:
 * словарь категорий/статусов и парсер/сборщик content. Совместимо с тем, что
 * пишут Telegram-захват (#solution) и AI-теггер (cat:<slug>).
 */

// ── Категории (фиксированный набор; slug в нижнем регистре — patch лоуэркейзит теги) ──
export interface SolutionCategory {
  slug: string
  label: string
}

export const SOLUTION_CATEGORIES: SolutionCategory[] = [
  { slug: "postgresql", label: "PostgreSQL" },
  { slug: "docker", label: "Docker" },
  { slug: "1c", label: "1C" },
  { slug: "pam", label: "PAM" },
  { slug: "windows", label: "Windows" },
  { slug: "mikrotik", label: "MikroTik" },
  { slug: "python", label: "Python" },
  { slug: "ai", label: "AI" },
  { slug: "other", label: "Другое" }
]

export const CATEGORY_LABEL: Record<string, string> = Object.fromEntries(
  SOLUTION_CATEGORIES.map((c) => [c.slug, c.label])
)

// ── Статусы ──────────────────────────────────────────────────────────────────
export type SolutionStatus = "resolved" | "in_progress" | "unresolved"

export interface SolutionStatusMeta {
  key: SolutionStatus
  label: string
  /** tailwind-классы текста и точки/иконки (один акцент на статус). */
  text: string
  dot: string
}

export const SOLUTION_STATUSES: SolutionStatusMeta[] = [
  { key: "resolved", label: "Решено", text: "text-lime-400", dot: "bg-lime-400" },
  {
    key: "in_progress",
    label: "В процессе",
    text: "text-amber-400",
    dot: "bg-amber-400"
  },
  { key: "unresolved", label: "Не решено", text: "text-red-400", dot: "bg-red-400" }
]

export const STATUS_META: Record<SolutionStatus, SolutionStatusMeta> =
  Object.fromEntries(SOLUTION_STATUSES.map((s) => [s.key, s])) as Record<
    SolutionStatus,
    SolutionStatusMeta
  >

// ── Зарезервированные теги ─────────────────────────────────────────────────────
const CAT_PREFIX = "cat:"
const ST_PREFIX = "st:"

/** Категория-slug элемента (из тега cat:); "other", если нет/неизвестна. */
export function catOf(tags: string[]): string {
  const t = (tags || []).find((x) => x.startsWith(CAT_PREFIX))
  const slug = t ? t.slice(CAT_PREFIX.length) : ""
  return CATEGORY_LABEL[slug] ? slug : "other"
}

/** Статус решения (из тега st:); "unresolved" по умолчанию. */
export function statusOf(tags: string[]): SolutionStatus {
  const t = (tags || []).find((x) => x.startsWith(ST_PREFIX))
  const s = t ? t.slice(ST_PREFIX.length) : ""
  return s === "resolved" || s === "in_progress" || s === "unresolved"
    ? s
    : "unresolved"
}

/** Свободные (навигационные) теги — без cat:/st:. */
export function freeTags(tags: string[]): string[] {
  return (tags || []).filter(
    (t) => !t.startsWith(CAT_PREFIX) && !t.startsWith(ST_PREFIX)
  )
}

/** Собрать полный массив тегов: [st:…, cat:…, ...свободные] (дедуп, кап 8). */
export function buildTags(
  category: string,
  status: SolutionStatus,
  free: string[]
): string[] {
  const reserved = [`${ST_PREFIX}${status}`, `${CAT_PREFIX}${category || "other"}`]
  const clean = Array.from(
    new Set((free || []).map((t) => t.trim().toLowerCase()).filter(Boolean))
  )
  return Array.from(new Set([...reserved, ...clean])).slice(0, 8)
}

/** Заменить статус, сохранив категорию и свободные теги. */
export function withStatus(tags: string[], status: SolutionStatus): string[] {
  return buildTags(catOf(tags), status, freeTags(tags))
}

/** Заменить категорию, сохранив статус и свободные теги. */
export function withCategory(tags: string[], category: string): string[] {
  return buildTags(category, statusOf(tags), freeTags(tags))
}

// ── Секции content (markdown) ──────────────────────────────────────────────────
export interface SolutionFields {
  problem: string
  cause: string
  solution: string
  commands: string
  notes: string
}

const SECTION_DEFS: { key: keyof SolutionFields; label: string }[] = [
  { key: "problem", label: "Проблема" },
  { key: "cause", label: "Причина" },
  { key: "solution", label: "Решение" },
  { key: "commands", label: "Команды" },
  { key: "notes", label: "Заметки" }
]

const LABEL_TO_KEY = new Map<string, keyof SolutionFields | "skip">(
  SECTION_DEFS.map((s) => [s.label.toLowerCase(), s.key])
)
// «Связанные решения» — навигационная секция: связи берём из memory_links,
// поэтому при разборе её просто пропускаем (а не тащим в поля формы).
LABEL_TO_KEY.set("связанные решения", "skip")

const EMPTY: SolutionFields = {
  problem: "",
  cause: "",
  solution: "",
  commands: "",
  notes: ""
}

/** Распознать строку-заголовок секции. Поддержка «## Проблема», «Проблема:»,
    «Проблема: текст» (inline). Возвращает ключ + остаток строки. */
function matchHeader(
  line: string
): { key: keyof SolutionFields | "skip"; rest: string } | null {
  const s = line.replace(/^#{1,3}\s*/, "").trim()
  // «Label» или «Label:»
  const standalone = s.match(/^([^:]+?)\s*:?$/)
  if (standalone) {
    const key = LABEL_TO_KEY.get(standalone[1].trim().toLowerCase())
    if (key) return { key, rest: "" }
  }
  // «Label: текст на той же строке»
  const inline = s.match(/^([^:]+?)\s*:\s*(.+)$/)
  if (inline) {
    const key = LABEL_TO_KEY.get(inline[1].trim().toLowerCase())
    if (key) return { key, rest: inline[2] }
  }
  return null
}

/** Разобрать content на поля решения. Текст без распознанных заголовков (или до
    первого) попадает в «Проблема» — чтобы ничего не потерялось. */
export function parseSolution(content: string): SolutionFields {
  const fields: SolutionFields = { ...EMPTY }
  const buf: Record<keyof SolutionFields, string[]> = {
    problem: [],
    cause: [],
    solution: [],
    commands: [],
    notes: []
  }
  const lead: string[] = []
  let current: keyof SolutionFields | null = null

  for (const raw of (content || "").replace(/\r\n?/g, "\n").split("\n")) {
    const h = matchHeader(raw)
    if (h) {
      current = h.key === "skip" ? null : h.key
      if (current && h.rest) buf[current].push(h.rest)
      continue
    }
    if (current) buf[current].push(raw)
    else lead.push(raw)
  }

  for (const def of SECTION_DEFS) fields[def.key] = buf[def.key].join("\n").trim()
  const leadText = lead.join("\n").trim()
  if (leadText) {
    fields.problem = fields.problem
      ? `${leadText}\n\n${fields.problem}`
      : leadText
  }
  return fields
}

/** Собрать content из полей. Пустые секции опускаем; гарантируем непустой
    результат (бэкенд требует content). */
export function buildSolutionContent(f: SolutionFields): string {
  const parts: string[] = []
  for (const def of SECTION_DEFS) {
    const v = (f[def.key] || "").trim()
    if (v) parts.push(`## ${def.label}\n${v}`)
  }
  return parts.join("\n\n")
}

/** Заголовок-предпросмотр для списка, когда title не задан. */
export function solutionPreview(content: string): string {
  const f = parseSolution(content)
  const text = f.problem || f.solution || content || ""
  return text.replace(/\s+/g, " ").trim().slice(0, 80)
}
