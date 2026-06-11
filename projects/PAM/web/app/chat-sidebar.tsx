"use client"

import { useEffect, useRef, useState } from "react"

import {
  patchConversation,
  deleteConversation,
  type ConversationSummary
} from "../lib/api"

interface Props {
  chats: ConversationSummary[]
  activeId: string | null
  onSelect: (id: string) => void
  onNewChat: () => void
  onChanged: () => void // перезагрузить список после pin/archive/delete
  onDeletedActive: () => void // если удалили открытый чат — сбросить
}

type Menu = { id: string; x: number; y: number } | null

export default function ChatSidebar({
  chats,
  activeId,
  onSelect,
  onNewChat,
  onChanged,
  onDeletedActive
}: Props) {
  const pinned = chats.filter((c) => c.pinned)
  const recent = chats.filter((c) => !c.pinned)
  const [recentOpen, setRecentOpen] = useState(true)
  const [menu, setMenu] = useState<Menu>(null)

  function openMenu(id: string, e: React.MouseEvent) {
    e.stopPropagation()
    const r = (e.currentTarget as HTMLElement).getBoundingClientRect()
    const MENU_W = 220
    const MENU_H = 150
    // Открываем СПРАВА от кнопки (в область чата), чтобы не перекрывать список чатов.
    const x = Math.min(r.right + 6, window.innerWidth - MENU_W - 8)
    // У нижних чатов разворачиваем вверх, чтобы не уходило за низ экрана.
    const y =
      r.bottom + 4 + MENU_H > window.innerHeight ? Math.max(8, r.top - MENU_H) : r.bottom + 4
    setMenu({ id, x, y })
  }

  async function togglePin(c: ConversationSummary) {
    setMenu(null)
    try {
      await patchConversation(c.id, { pinned: !c.pinned })
      onChanged()
    } catch {
      /* no-op */
    }
  }

  async function archive(id: string) {
    setMenu(null)
    try {
      await patchConversation(id, { archived: true })
      if (id === activeId) onDeletedActive()
      onChanged()
    } catch {
      /* no-op */
    }
  }

  async function remove(id: string) {
    setMenu(null)
    if (!window.confirm("Удалить чат безвозвратно?")) return
    try {
      await deleteConversation(id)
      if (id === activeId) onDeletedActive()
      onChanged()
    } catch {
      /* no-op */
    }
  }

  const menuChat = menu ? chats.find((c) => c.id === menu.id) : null

  return (
    <aside
      className="hidden md:flex w-[260px] shrink-0 flex-col bg-[#0D0D0D] border-r border-neutral-900 py-3"
      style={{ color: "#ECECEC" }}>
      <div className="px-2">
        <button
          onClick={onNewChat}
          className="w-full flex items-center gap-2 rounded-[10px] px-3 py-2.5 text-sm text-[#ECECEC] hover:bg-[#1F1F1F] transition-colors duration-150">
          <PlusIcon />
          <span>Новый чат</span>
        </button>
      </div>

      <div className="mt-2 flex-1 overflow-y-auto px-2">
        {pinned.length > 0 && (
          <Section label="Закреплённые">
            {pinned.map((c) => (
              <ChatRow
                key={c.id}
                chat={c}
                active={c.id === activeId}
                showPin
                menuOpen={menu?.id === c.id}
                onSelect={() => onSelect(c.id)}
                onTogglePin={() => togglePin(c)}
                onMore={(e) => openMenu(c.id, e)}
              />
            ))}
          </Section>
        )}

        <Section
          label="Недавнее"
          collapsible
          open={recentOpen}
          onToggle={() => setRecentOpen((v) => !v)}>
          {recentOpen &&
            recent.map((c) => (
              <ChatRow
                key={c.id}
                chat={c}
                active={c.id === activeId}
                menuOpen={menu?.id === c.id}
                onSelect={() => onSelect(c.id)}
                onTogglePin={() => togglePin(c)}
                onMore={(e) => openMenu(c.id, e)}
              />
            ))}
          {recentOpen && recent.length === 0 && pinned.length === 0 && (
            <div className="px-2.5 py-2 text-xs" style={{ color: "#A3A3A3" }}>
              пока пусто
            </div>
          )}
        </Section>
      </div>

      {menu && menuChat && (
        <ContextMenu
          x={menu.x}
          y={menu.y}
          pinned={menuChat.pinned}
          onClose={() => setMenu(null)}
          onPin={() => togglePin(menuChat)}
          onArchive={() => archive(menuChat.id)}
          onDelete={() => remove(menuChat.id)}
        />
      )}
    </aside>
  )
}

function Section({
  label,
  collapsible,
  open,
  onToggle,
  children
}: {
  label: string
  collapsible?: boolean
  open?: boolean
  onToggle?: () => void
  children: React.ReactNode
}) {
  return (
    <div className="mb-3">
      <button
        onClick={onToggle}
        disabled={!collapsible}
        className="w-full flex items-center gap-1 px-2.5 py-1 text-[11px] uppercase tracking-wider disabled:cursor-default"
        style={{ color: "#A3A3A3" }}>
        {collapsible && (
          <ChevronIcon
            className={`transition-transform duration-150 ${open ? "" : "-rotate-90"}`}
          />
        )}
        {label}
      </button>
      <div className="mt-0.5 flex flex-col gap-[2px]">{children}</div>
    </div>
  )
}

function ChatRow({
  chat,
  active,
  showPin,
  menuOpen,
  onSelect,
  onTogglePin,
  onMore
}: {
  chat: ConversationSummary
  active: boolean
  showPin?: boolean
  menuOpen?: boolean
  onSelect: () => void
  onTogglePin: () => void
  onMore: (e: React.MouseEvent) => void
}) {
  return (
    <div
      role="button"
      tabIndex={0}
      onClick={onSelect}
      onKeyDown={(e) => e.key === "Enter" && onSelect()}
      className={`group relative flex items-center h-9 px-2.5 rounded-[10px] cursor-pointer transition-colors duration-150 outline-none focus-visible:ring-1 focus-visible:ring-neutral-600 ${
        active ? "bg-[#2A2A2A]" : "hover:bg-[#1F1F1F]"
      }`}>
      <span className="flex-1 truncate text-sm pr-1">
        {chat.title || "Новый чат"}
      </span>
      <div className="flex items-center gap-0.5 shrink-0">
        <IconButton
          title={chat.pinned ? "Открепить" : "Закрепить"}
          onClick={(e) => {
            e.stopPropagation()
            onTogglePin()
          }}
          className={
            showPin || chat.pinned
              ? "opacity-100"
              : "opacity-0 group-hover:opacity-100"
          }>
          <PinIcon filled={chat.pinned} />
        </IconButton>
        <IconButton
          title="Ещё"
          onClick={onMore}
          className={menuOpen ? "opacity-100" : "opacity-0 group-hover:opacity-100"}>
          <DotsIcon />
        </IconButton>
      </div>
    </div>
  )
}

function IconButton({
  children,
  onClick,
  className = "",
  title
}: {
  children: React.ReactNode
  onClick: (e: React.MouseEvent) => void
  className?: string
  title?: string
}) {
  return (
    <button
      type="button"
      title={title}
      onClick={onClick}
      className={`w-6 h-6 flex items-center justify-center rounded-md hover:bg-[#3A3A3A] transition-all duration-150 ${className}`}
      style={{ color: "#A3A3A3" }}>
      {children}
    </button>
  )
}

function ContextMenu({
  x,
  y,
  pinned,
  onClose,
  onPin,
  onArchive,
  onDelete
}: {
  x: number
  y: number
  pinned: boolean
  onClose: () => void
  onPin: () => void
  onArchive: () => void
  onDelete: () => void
}) {
  const ref = useRef<HTMLDivElement>(null)
  const [shown, setShown] = useState(false)

  useEffect(() => {
    const raf = requestAnimationFrame(() => setShown(true))
    const onDoc = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) onClose()
    }
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose()
      if (e.key === "ArrowDown" || e.key === "ArrowUp") {
        e.preventDefault()
        const items = Array.from(
          ref.current?.querySelectorAll<HTMLButtonElement>("button") || []
        )
        const i = items.indexOf(document.activeElement as HTMLButtonElement)
        const next =
          e.key === "ArrowDown"
            ? items[(i + 1) % items.length]
            : items[(i - 1 + items.length) % items.length]
        next?.focus()
      }
    }
    document.addEventListener("mousedown", onDoc)
    document.addEventListener("keydown", onKey)
    ref.current?.querySelector("button")?.focus()
    return () => {
      cancelAnimationFrame(raf)
      document.removeEventListener("mousedown", onDoc)
      document.removeEventListener("keydown", onKey)
    }
  }, [onClose])

  return (
    <div
      ref={ref}
      role="menu"
      className="fixed z-50 w-[220px] p-1.5 origin-top-left transition-all duration-150"
      style={{
        left: x,
        top: y,
        background: "#2B2B2B",
        border: "1px solid #3A3A3A",
        borderRadius: 14,
        boxShadow: "0 8px 30px rgba(0,0,0,.45)",
        opacity: shown ? 1 : 0,
        transform: shown ? "scale(1)" : "scale(.96)"
      }}>
      <MenuItem icon="📌" label={pinned ? "Открепить чат" : "Закрепить чат"} onClick={onPin} />
      <MenuItem icon="📦" label="Архивировать" onClick={onArchive} />
      <MenuItem icon="🗑" label="Удалить" danger onClick={onDelete} />
    </div>
  )
}

function MenuItem({
  icon,
  label,
  danger,
  onClick
}: {
  icon: string
  label: string
  danger?: boolean
  onClick: () => void
}) {
  return (
    <button
      role="menuitem"
      onClick={onClick}
      className="w-full h-10 flex items-center gap-2.5 px-3 rounded-[10px] text-sm text-left transition-colors duration-150 outline-none"
      style={{ color: danger ? "#FF5F57" : "#F5F5F5" }}
      onMouseEnter={(e) =>
        (e.currentTarget.style.background = danger
          ? "rgba(255,95,87,.12)"
          : "#3A3A3A")
      }
      onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
      onFocus={(e) =>
        (e.currentTarget.style.background = danger
          ? "rgba(255,95,87,.12)"
          : "#3A3A3A")
      }
      onBlur={(e) => (e.currentTarget.style.background = "transparent")}>
      <span className="text-[15px] leading-none">{icon}</span>
      {label}
    </button>
  )
}

/* ---- icons ---- */
function PlusIcon() {
  return (
    <svg viewBox="0 0 24 24" className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round">
      <path d="M12 5v14M5 12h14" />
    </svg>
  )
}
function ChevronIcon({ className = "" }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={`w-3 h-3 ${className}`} fill="none" stroke="currentColor" strokeWidth={2.5} strokeLinecap="round" strokeLinejoin="round">
      <path d="M6 9l6 6 6-6" />
    </svg>
  )
}
function DotsIcon() {
  return (
    <svg viewBox="0 0 24 24" className="w-4 h-4" fill="currentColor">
      <circle cx="5" cy="12" r="1.6" />
      <circle cx="12" cy="12" r="1.6" />
      <circle cx="19" cy="12" r="1.6" />
    </svg>
  )
}
function PinIcon({ filled }: { filled?: boolean }) {
  return (
    <svg viewBox="0 0 24 24" className="w-4 h-4" fill={filled ? "currentColor" : "none"} stroke="currentColor" strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round" style={filled ? { color: "#D4D4D4" } : undefined}>
      <path d="M9 4h6l-1 6 3 3v2H7v-2l3-3-1-6z" />
      <path d="M12 15v5" />
    </svg>
  )
}
