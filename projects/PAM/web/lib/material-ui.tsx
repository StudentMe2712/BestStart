"use client"

// Общие презентационные примитивы для материалов/курсов (Лектор + Курсы).
import { useState } from "react"

export type Kind = "youtube" | "article" | "pdf" | "file" | "text"

export const KIND_LABEL: Record<Kind, string> = {
  youtube: "YouTube",
  article: "Статья",
  pdf: "PDF",
  file: "Файл",
  text: "Текст"
}

export const KIND_BADGE: Record<Kind, string> = {
  youtube: "bg-red-600 text-white",
  pdf: "bg-red-600 text-white",
  article: "bg-sky-600 text-white",
  file: "bg-amber-500 text-neutral-950",
  text: "bg-indigo-500 text-white"
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

export function Preview({
  kind,
  url,
  compact = false
}: {
  kind: Kind
  url: string | null
  compact?: boolean
}) {
  const ytId = kind === "youtube" ? youtubeId(url) : null
  const [imgOk, setImgOk] = useState(true)

  if (ytId && imgOk) {
    return (
      <>
        {/* eslint-disable-next-line @next/next/no-img-element */}
        <img
          src={`https://i.ytimg.com/vi/${ytId}/hqdefault.jpg`}
          alt=""
          className="h-full w-full object-cover"
          onError={() => setImgOk(false)}
        />
        {!compact && (
          <div className="absolute inset-0 flex items-center justify-center">
            <span className="grid place-items-center w-12 h-12 rounded-full bg-black/55 text-white">
              <PlayIcon />
            </span>
          </div>
        )}
      </>
    )
  }

  if (kind === "text") {
    return (
      <div className="h-full w-full bg-gradient-to-br from-neutral-800 to-neutral-950 p-5 flex flex-col justify-center gap-2.5">
        {[92, 78, 88, 64, 84, 56].map((w, i) => (
          <div
            key={i}
            className="h-2.5 rounded bg-neutral-700/60"
            style={{ width: `${w}%` }}
          />
        ))}
      </div>
    )
  }

  if (kind === "pdf" || kind === "file") {
    return (
      <div className="h-full w-full bg-gradient-to-br from-neutral-800 to-neutral-950 grid place-items-center text-neutral-600">
        <DocIcon />
      </div>
    )
  }

  return (
    <div className="h-full w-full bg-gradient-to-br from-neutral-800 via-neutral-900 to-neutral-950 grid place-items-center text-neutral-600">
      <GlobeIcon />
    </div>
  )
}

export function KindBadge({ kind, small = false }: { kind: Kind; small?: boolean }) {
  return (
    <span
      className={`inline-flex items-center gap-1 rounded-md font-semibold uppercase tracking-wide ${
        small ? "text-[8px] px-1.5 py-0.5" : "text-[10px] px-2 py-1"
      } ${KIND_BADGE[kind]}`}>
      {kind === "youtube" && <PlayIcon small />}
      {KIND_LABEL[kind]}
    </span>
  )
}

// ── Иконки (inline SVG) ───────────────────────────────────────────────────

export function PlayIcon({ small = false }: { small?: boolean }) {
  const s = small ? 8 : 18
  return (
    <svg width={s} height={s} viewBox="0 0 24 24" fill="currentColor">
      <path d="M8 5v14l11-7z" />
    </svg>
  )
}

export function DotsIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor">
      <circle cx="12" cy="5" r="1.6" />
      <circle cx="12" cy="12" r="1.6" />
      <circle cx="12" cy="19" r="1.6" />
    </svg>
  )
}

export function GridIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="3" y="3" width="7" height="7" rx="1" />
      <rect x="14" y="3" width="7" height="7" rx="1" />
      <rect x="3" y="14" width="7" height="7" rx="1" />
      <rect x="14" y="14" width="7" height="7" rx="1" />
    </svg>
  )
}

export function ListIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <line x1="8" y1="6" x2="21" y2="6" />
      <line x1="8" y1="12" x2="21" y2="12" />
      <line x1="8" y1="18" x2="21" y2="18" />
      <line x1="3" y1="6" x2="3.01" y2="6" />
      <line x1="3" y1="12" x2="3.01" y2="12" />
      <line x1="3" y1="18" x2="3.01" y2="18" />
    </svg>
  )
}

export function DocIcon() {
  return (
    <svg width="56" height="56" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
      <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
      <polyline points="14 2 14 8 20 8" />
      <line x1="8" y1="13" x2="16" y2="13" />
      <line x1="8" y1="17" x2="13" y2="17" />
    </svg>
  )
}

export function GlobeIcon() {
  return (
    <svg width="56" height="56" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="9" />
      <line x1="3" y1="12" x2="21" y2="12" />
      <path d="M12 3a14 14 0 0 1 0 18 14 14 0 0 1 0-18z" />
    </svg>
  )
}
