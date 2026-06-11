"use client"

import Link from "next/link"
import { usePathname } from "next/navigation"
import type { ReactNode } from "react"

export default function Nav() {
  const pathname = usePathname() || "/"
  const onChat = pathname === "/"
  const onHistory = pathname === "/history" || pathname.startsWith("/c/")
  const onSaved = pathname === "/saved"
  const onMe = pathname === "/me"
  const onLearn = pathname === "/learn"
  const onCatalog = pathname === "/catalog"

  return (
    <header className="sticky top-0 z-20 border-b border-neutral-800 bg-neutral-950/80 backdrop-blur">
      <div className="max-w-5xl mx-auto px-6 h-14 flex items-center justify-between">
        <Link href="/" className="flex items-center gap-2">
          <Logo />
          <span className="text-sm font-semibold tracking-wide">PAM</span>
          <span className="hidden sm:inline text-[10px] uppercase tracking-widest text-neutral-600">
            personal_ai_memory
          </span>
        </Link>

        <nav className="flex items-center gap-1 text-xs">
          <Tab href="/" active={onChat}>
            Чат
          </Tab>
          <Tab href="/history" active={onHistory}>
            История
          </Tab>
          <Tab href="/saved" active={onSaved}>
            Избранное
          </Tab>
          <Tab href="/me" active={onMe}>
            Профиль
          </Tab>
          <Tab href="/learn" active={onLearn}>
            Лектор
          </Tab>
          <Tab href="/catalog" active={onCatalog}>
            Каталог
          </Tab>
        </nav>
      </div>
    </header>
  )
}

function Tab({
  href,
  active,
  children
}: {
  href: string
  active: boolean
  children: ReactNode
}) {
  return (
    <Link
      href={href}
      aria-current={active ? "page" : undefined}
      className={`px-3 py-1.5 rounded-md transition-colors ${
        active
          ? "bg-neutral-800 text-lime-400"
          : "text-neutral-400 hover:text-neutral-100 hover:bg-neutral-900"
      }`}>
      {children}
    </Link>
  )
}

// Бренд-глиф PAM — узлы «памяти» (тот же мотив, что и favicon app/icon.svg).
function Logo() {
  return (
    <svg
      viewBox="0 0 32 32"
      className="w-5 h-5"
      fill="none"
      aria-hidden="true">
      <rect width="32" height="32" rx="7" fill="#0d0d0d" />
      <g
        stroke="#a3e635"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round">
        <path d="M10 11 L16 20 L22 11" />
        <path d="M10 11 L22 11" />
        <path d="M16 20 L16 25" />
      </g>
      <g fill="#a3e635">
        <circle cx="10" cy="11" r="3" />
        <circle cx="22" cy="11" r="3" />
        <circle cx="16" cy="20" r="3" />
        <circle cx="16" cy="25" r="1.7" />
      </g>
    </svg>
  )
}
