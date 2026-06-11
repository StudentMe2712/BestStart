"use client"

export default function RefreshButton({
  onClick,
  busy = false,
  title = "Обновить"
}: {
  onClick: () => void
  busy?: boolean
  title?: string
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={busy}
      title={title}
      aria-label={title}
      className="shrink-0 inline-flex items-center justify-center w-9 h-9 rounded-sm border border-neutral-800 text-neutral-400 hover:text-lime-400 hover:border-lime-400/40 transition-colors disabled:opacity-50 disabled:hover:text-neutral-400">
      <svg
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth={2}
        strokeLinecap="round"
        strokeLinejoin="round"
        className={`w-4 h-4 ${busy ? "animate-spin" : ""}`}>
        <path d="M23 4v6h-6" />
        <path d="M1 20v-6h6" />
        <path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15" />
      </svg>
    </button>
  )
}
