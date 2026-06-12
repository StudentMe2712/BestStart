"use client"

import { useEffect, useMemo, useRef, useState } from "react"

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
  onChanged: () => void
  onDeletedActive: () => void
}

type Menu = { id: string; x: number; y: number } | null

const DATE_ORDER = ["Сегодня", "Вчера", "Эта неделя", "Этот месяц", "Старше"]

function bucketOf(iso: string): string {
  const t = new Date(iso).getTime()
  if (isNaN(t)) return "Старше"
  const now = new Date()
  const startToday = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime()
  const day = 86400000
  if (t >= startToday) return "Сегодня"
  if (t >= startToday - day) return "Вчера"
  if (t >= startToday - 7 * day) return "Эта неделя"
  if (t >= startToday - 30 * day) return "Этот месяц"
  return "Старше"
}

export default function ChatSidebar({
  chats,
  activeId,
  onSelect,
  onNewChat,
  onChanged,
  onDeletedActive
}: Props) {
  const [menu, setMenu] = useState<Menu>(null)
  const [searchOpen, setSearchOpen] = useState(false)

  const pinned = chats.filter((c) => c.pinned)
  const recent = chats.filter((c) => !c.pinned)
  const groups = useMemo(() => groupByDate(recent), [recent])

  function openMenu(id: string, e: React.MouseEvent) {
    e.stopPropagation()
    const r = (e.currentTarget as HTMLElement).getBoundingClientRect()
    const MENU_W = 220
    const MENU_H = 150
    const x = Math.min(r.right + 6, window.innerWidth - MENU_W - 8)
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

  const row = (c: ConversationSummary, showPin?: boolean) => (
    <ChatRow
      key={c.id}
      chat={c}
      active={c.id === activeId}
      showPin={showPin}
      menuOpen={menu?.id === c.id}
      onSelect={() => onSelect(c.id)}
      onTogglePin={() => togglePin(c)}
      onMore={(e) => openMenu(c.id, e)}
    />
  )

  return (
    <aside
      className="hidden md:flex w-[260px] shrink-0 flex-col bg-[#0D0D0D] border-r border-neutral-900 py-3"
      style={{ color: "#ECECEC" }}>
      <div className="px-2 space-y-0.5">
        <NavItem icon={<ComposeIcon />} label="Новый чат" onClick={onNewChat} />
        <NavItem
          icon={<SearchIcon />}
          label="Искать чаты"
          onClick={() => setSearchOpen(true)}
        />
        <NavItem icon={<FolderIcon />} label="Проекты" title="скоро" onClick={() => {}} />
      </div>

      <div className="mt-3 flex-1 overflow-y-auto px-2">
        {pinned.length > 0 && (
          <Section label="Закреплённые">{pinned.map((c) => row(c, true))}</Section>
        )}
        {groups.length === 0 && pinned.length === 0 && <Empty text="пока пусто" />}
        {groups.map(([label, items]) => (
          <Section key={label} label={label}>
            {items.map((c) => row(c))}
          </Section>
        ))}
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

      {searchOpen && (
        <SearchModal
          chats={chats}
          onSelect={(id) => {
            onSelect(id)
            setSearchOpen(false)
          }}
          onNewChat={() => {
            onNewChat()
            setSearchOpen(false)
          }}
          onClose={() => setSearchOpen(false)}
        />
      )}
    </aside>
  )
}

function groupByDate(list: ConversationSummary[]) {
  const m: Record<string, ConversationSummary[]> = {}
  for (const c of list) (m[bucketOf(c.updated_at)] ||= []).push(c)
  return DATE_ORDER.filter((b) => m[b]?.length).map((b) => [b, m[b]] as const)
}

function SearchModal({
  chats,
  onSelect,
  onNewChat,
  onClose
}: {
  chats: ConversationSummary[]
  onSelect: (id: string) => void
  onNewChat: () => void
  onClose: () => void
}) {
  const [q, setQ] = useState("")
  const ql = q.trim().toLowerCase()
  const filtered = ql
    ? chats.filter((c) => (c.title || "").toLowerCase().includes(ql))
    : chats
  const groups = useMemo(() => groupByDate(filtered), [filtered])

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose()
    document.addEventListener("keydown", onKey)
    return () => document.removeEventListener("keydown", onKey)
  }, [onClose])

  return (
    <div
      className="fixed inset-0 z-[60] flex items-start justify-center pt-[12vh] px-4 bg-black/60 backdrop-blur-sm"
      onClick={onClose}>
      <div
        className="w-full max-w-[640px] rounded-2xl border border-neutral-800 bg-neutral-900 shadow-2xl shadow-black/50 overflow-hidden"
        onClick={(e) => e.stopPropagation()}>
        <div className="flex items-center gap-3 px-4 h-14 border-b border-neutral-800 text-neutral-300">
          <SearchIcon />
          <input
            autoFocus
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder="Поиск в чатах"
            className="flex-1 bg-transparent outline-none text-sm font-sans text-neutral-100 placeholder:text-neutral-500"
          />
          <kbd className="text-[10px] text-neutral-500 border border-neutral-700 rounded px-1.5 py-0.5">
            Esc
          </kbd>
        </div>

        <div className="max-h-[60vh] overflow-y-auto p-2">
          <button
            onClick={onNewChat}
            className="w-full flex items-center gap-2.5 rounded-[10px] px-3 py-2.5 text-sm text-neutral-100 hover:bg-neutral-800 transition-colors">
            <span className="text-neutral-400">
              <ComposeIcon />
            </span>
            Новый чат
          </button>

          {filtered.length === 0 ? (
            <div className="px-3 py-8 text-center text-sm text-neutral-500 font-sans">
              Ничего не найдено
            </div>
          ) : (
            groups.map(([label, items]) => (
              <div key={label} className="mt-2">
                <div className="px-3 py-1 text-[11px] uppercase tracking-wider text-neutral-500">
                  {label}
                </div>
                {items.map((c) => (
                  <button
                    key={c.id}
                    onClick={() => onSelect(c.id)}
                    className="w-full flex items-center rounded-[10px] px-3 py-2 text-sm text-left text-neutral-200 hover:bg-neutral-800 transition-colors">
                    <span className="truncate">{c.title || "Новый чат"}</span>
                  </button>
                ))}
              </div>
            ))
          )}
        </div>
      </div>
    </div>
  )
}

function NavItem({
  icon,
  label,
  onClick,
  active,
  title
}: {
  icon: React.ReactNode
  label: string
  onClick: () => void
  active?: boolean
  title?: string
}) {
  return (
    <button
      onClick={onClick}
      title={title}
      className={`w-full flex items-center gap-2.5 rounded-[10px] px-3 py-2 text-sm transition-colors duration-150 ${
        active ? "bg-[#1F1F1F]" : "hover:bg-[#1F1F1F]"
      }`}>
      <span className="text-[#B4B4B4]">{icon}</span>
      <span>{label}</span>
    </button>
  )
}

function Section({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="mb-3">
      <div
        className="px-2.5 py-1 text-[11px] uppercase tracking-wider"
        style={{ color: "#8A8A8A" }}>
        {label}
      </div>
      <div className="mt-0.5 flex flex-col gap-[2px]">{children}</div>
    </div>
  )
}

function Empty({ text }: { text: string }) {
  return (
    <div className="px-2.5 py-2 text-xs" style={{ color: "#A3A3A3" }}>
      {text}
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
      <span className="flex-1 truncate text-sm pr-1">{chat.title || "Новый чат"}</span>
      <div className="flex items-center gap-0.5 shrink-0">
        <IconButton
          title={chat.pinned ? "Открепить" : "Закрепить"}
          onClick={(e) => {
            e.stopPropagation()
            onTogglePin()
          }}
          className={showPin || chat.pinned ? "opacity-100" : "opacity-0 group-hover:opacity-100"}>
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
    }
    document.addEventListener("mousedown", onDoc)
    document.addEventListener("keydown", onKey)
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
        (e.currentTarget.style.background = danger ? "rgba(255,95,87,.12)" : "#3A3A3A")
      }
      onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}>
      <span className="text-[15px] leading-none">{icon}</span>
      {label}
    </button>
  )
}

/* ---- icons ---- */
function ComposeIcon() {
  return (
    <svg viewBox="0 0 24 24" className="w-[18px] h-[18px]" fill="none" stroke="currentColor" strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round">
      <path d="M12 20h9" />
      <path d="M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4 12.5-12.5z" />
    </svg>
  )
}
function SearchIcon() {
  return (
    <svg viewBox="0 0 24 24" className="w-[18px] h-[18px]" fill="none" stroke="currentColor" strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round">
      <circle cx="11" cy="11" r="7" />
      <path d="M21 21l-4.3-4.3" />
    </svg>
  )
}
function FolderIcon() {
  return (
    <svg viewBox="0 0 24 24" className="w-[18px] h-[18px]" fill="none" stroke="currentColor" strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round">
      <path d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V7z" />
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
