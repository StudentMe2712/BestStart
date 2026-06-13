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
  const onDiag = pathname === "/diag"
  const onCatalog = pathname === "/catalog"

  return (
    <header className="sticky top-0 z-20 border-b border-neutral-800 bg-neutral-950/80 backdrop-blur print:hidden">
      {/* Единый нав-бар для всех страниц (как на чате): лого по левому краю на
          уровне «Новый чат» в сайдбаре (pl-5), вкладки прижаты вправо (pr-6). */}
      <div className="h-14 flex items-center justify-between w-full pl-5 pr-6">
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
          <Tab href="/diag" active={onDiag}>
            Диагностика
          </Tab>
          {/* «Каталог» — второстепенная витрина: отделяем дивайдером и приглушаем. */}
          <span className="w-px h-4 bg-neutral-800 mx-1" />
          <Tab href="/catalog" active={onCatalog} muted>
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
  muted = false,
  children
}: {
  href: string
  active: boolean
  muted?: boolean
  children: ReactNode
}) {
  return (
    <Link
      href={href}
      aria-current={active ? "page" : undefined}
      className={`relative px-3 py-1.5 transition-colors duration-[180ms] ${
        active
          ? "text-lime-400"
          : muted
            ? "text-neutral-600 hover:text-neutral-300"
            : "text-neutral-400 hover:text-neutral-100"
      }`}>
      {children}
      {active && (
        <span className="absolute left-3 right-3 -bottom-px h-px bg-lime-400 rounded-full" />
      )}
    </Link>
  )
}
