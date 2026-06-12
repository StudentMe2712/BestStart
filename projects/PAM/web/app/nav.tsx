"use client"

import Link from "next/link"
import { usePathname } from "next/navigation"
import type { ReactNode } from "react"

export default function Nav() {
  const pathname = usePathname() || "/"
  const onChat = pathname === "/"
  const onHistory = pathname === "/history" || pathname.startsWith("/c/")
  const onSaved = pathname === "/saved"
  const onLearn = pathname === "/learn"
  const onCatalog = pathname === "/catalog"

  return (
    <header className="sticky top-0 z-20 border-b border-neutral-800 bg-neutral-950/80 backdrop-blur">
      {/* На странице чата лого выровнено по левому краю — на уровне «Новый чат»
          в сайдбаре (pl-5 = отступ пунктов сайдбара). Остальные страницы —
          центрированный контейнер 1700px. */}
      <div
        className={`h-14 flex items-center justify-between ${
          onChat ? "w-full pl-5 pr-6" : "max-w-[1700px] mx-auto px-6"
        }`}>
        <Link href="/" className="flex items-center gap-2.5 shrink-0">
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img
            src="/pam-logo.png"
            alt=""
            className="h-8 w-auto object-contain select-none"
            draggable={false}
          />
          <span className="hidden md:inline text-[10px] uppercase tracking-widest text-neutral-600">
            
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
