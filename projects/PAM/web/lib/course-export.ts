/**
 * Экспорт курса наружу (#7): сборка Markdown из `course.data` и клиентское
 * скачивание. PDF делается печатью страницы (см. `.print-course` в globals.css).
 */
import type { Course } from "./api"

/** Собрать весь курс (содержание → главы/уроки → тест) в один Markdown-документ. */
export function courseToMarkdown(course: Course, sourceTitle?: string | null): string {
  const d = course.data
  const out: string[] = []
  const title = course.title || d.title || sourceTitle || "Курс"
  out.push(`# ${title}`, "")
  const level = course.level || d.level
  if (level) out.push(`**Уровень:** ${level}`, "")
  if (d.summary) out.push(d.summary.trim(), "")

  const modules = d.modules ?? []
  if (modules.length) {
    out.push("## Содержание", "")
    modules.forEach((m, i) => out.push(`${i + 1}. ${m.title}`))
    out.push("")
  }

  modules.forEach((m, i) => {
    out.push(`## ${i + 1}. ${m.title}`, "")
    ;(m.lessons ?? []).forEach((l) => {
      if (l.title) out.push(`### ${l.title}`, "")
      if (l.content) out.push(l.content.trim(), "")
    })
  })

  const quiz = d.quiz ?? []
  if (quiz.length) {
    out.push("## Тест", "")
    quiz.forEach((q, qi) => {
      out.push(`**${qi + 1}. ${q.question}**`, "")
      q.options.forEach((opt, oi) => {
        const mark = oi === q.answer_index ? "  ✓" : ""
        out.push(`- ${String.fromCharCode(65 + oi)}. ${opt}${mark}`)
      })
      out.push("")
      if (q.explanation) out.push(`> ${q.explanation}`, "")
    })
  }

  return out.join("\n")
}

/** Имя файла из названия курса: убираем символы, недопустимые в имени. */
export function safeCourseFilename(name: string | null | undefined, ext: string): string {
  const base =
    (name || "course")
      .replace(/[\\/:*?"<>|]+/g, "_")
      .replace(/\s+/g, " ")
      .trim()
      .slice(0, 80) || "course"
  return `${base}.${ext}`
}

/** Скачать текст как файл (Blob + временная ссылка). */
export function downloadText(
  filename: string,
  text: string,
  mime = "text/markdown"
): void {
  const blob = new Blob([text], { type: `${mime};charset=utf-8` })
  const url = URL.createObjectURL(blob)
  const a = document.createElement("a")
  a.href = url
  a.download = filename
  document.body.appendChild(a)
  a.click()
  a.remove()
  URL.revokeObjectURL(url)
}
