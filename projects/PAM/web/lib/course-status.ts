// Статус изучения курса — личная отметка пользователя, хранится ОТДЕЛЬНО от
// контента курса (только в localStorage браузера, не на бэкенде). Никакого
// прогресса/процентов/LMS — просто ярлык: где этот курс в твоей голове.

export type CourseStatus = "new" | "learning" | "done" | "favorite" | "archive"

export const COURSE_STATUSES: {
  key: CourseStatus
  label: string
  dot: string
  text: string
}[] = [
  { key: "new", label: "Не начинал", dot: "bg-sky-400", text: "text-sky-400" },
  { key: "learning", label: "Изучаю", dot: "bg-lime-400", text: "text-lime-400" },
  { key: "done", label: "Изучено", dot: "bg-emerald-400", text: "text-emerald-400" },
  { key: "favorite", label: "Избранное", dot: "bg-amber-400", text: "text-amber-400" },
  { key: "archive", label: "Архив", dot: "bg-neutral-400", text: "text-neutral-400" }
]

const VALID = new Set<CourseStatus>(COURSE_STATUSES.map((s) => s.key))
const KEY = (id: string) => `pam:course-status:${id}`

export function statusMeta(s: CourseStatus) {
  return COURSE_STATUSES.find((x) => x.key === s) ?? COURSE_STATUSES[0]
}

export function getCourseStatus(id: string): CourseStatus {
  if (typeof window === "undefined") return "new"
  const v = window.localStorage.getItem(KEY(id)) as CourseStatus | null
  return v && VALID.has(v) ? v : "new"
}

export function setCourseStatus(id: string, s: CourseStatus): void {
  if (typeof window === "undefined") return
  window.localStorage.setItem(KEY(id), s)
}
