// Небольшие общие хелперы для материалов/курсов (используются страницей курса).

export type Kind = "youtube" | "article" | "pdf" | "file" | "text"

export const KIND_LABEL: Record<Kind, string> = {
  youtube: "YouTube",
  article: "Статья",
  pdf: "PDF",
  file: "Файл",
  text: "Текст"
}

// kind в рантайме шире TS-типа из api.ts (бэкенд отдаёт ещё file/text).
export function kindOf(s: { kind: string }): Kind {
  return s.kind as Kind
}

export function youtubeId(url: string | null): string | null {
  if (!url) return null
  try {
    const u = new URL(url)
    const h = u.hostname.toLowerCase().replace(/^www\./, "")
    if (h === "youtu.be") return u.pathname.slice(1).split("/")[0] || null
    if (h.endsWith("youtube.com")) {
      if (u.pathname === "/watch") return u.searchParams.get("v")
      const m = u.pathname.match(/^\/(?:shorts|embed|v|live)\/([^/]+)/)
      if (m) return m[1]
    }
  } catch {
    /* ignore */
  }
  return null
}

export const fmtDate = (iso: string) => {
  const d = new Date(iso)
  return isNaN(d.getTime()) ? "" : d.toLocaleDateString("ru-RU")
}

/**
 * Детерминированная чистка сырого текста markitdown (Word/PDF) для показа:
 * нормализуем переносы, убираем пустые ссылки на картинки и висящие пробелы,
 * схлопываем простыни пустых строк. Контент не меняем — только причёсываем шум.
 * (AI-разбивка по смыслу — отдельная кнопка «Улучшить читаемость».)
 */
export function cleanSourceMarkdown(text: string | null | undefined): string {
  if (!text) return ""
  return text
    .replace(/\r\n?/g, "\n") // CRLF → LF
    .replace(/!\[[^\]]*\]\(\s*\)/g, "") // пустые ссылки на картинки ![]()
    .replace(/[ \t]+$/gm, "") // висящие пробелы в конце строк
    .replace(/\n{3,}/g, "\n\n") // 3+ пустых строк → одна
    .trim()
}

export function PlayIcon({ small = false }: { small?: boolean }) {
  const s = small ? 8 : 18
  return (
    <svg width={s} height={s} viewBox="0 0 24 24" fill="currentColor">
      <path d="M8 5v14l11-7z" />
    </svg>
  )
}
