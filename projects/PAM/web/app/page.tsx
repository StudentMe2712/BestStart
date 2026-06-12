"use client"

import { useEffect, useRef, useState } from "react"

import {
  getConversation,
  listConversations,
  streamChat,
  type ChatMeta,
  type ConversationSummary,
  type SourceRef
} from "../lib/api"
import { getCache, setCache } from "../lib/cache"
import ChatSidebar from "./chat-sidebar"
import Markdown from "./markdown"

interface Msg {
  role: "user" | "assistant"
  content: string
}

export default function ChatPage() {
  const [chats, setChats] = useState<ConversationSummary[]>(
    () => getCache<ConversationSummary[]>("chats") ?? []
  )
  const [messages, setMessages] = useState<Msg[]>([])
  const [convId, setConvId] = useState<string | null>(null)
  const [input, setInput] = useState("")
  const [busy, setBusy] = useState(false)
  const [sources, setSources] = useState<SourceRef[]>([])
  const [meta, setMeta] = useState<ChatMeta | null>(null)
  const [error, setError] = useState<string | null>(null)
  const endRef = useRef<HTMLDivElement>(null)
  const taRef = useRef<HTMLTextAreaElement>(null)
  // Контекстные переключатели ввода (UI-уровень; на бэкенд пока не уходят).
  const [useMemory, setUseMemory] = useState(true)
  const [ctxMaterials, setCtxMaterials] = useState(false)
  const [ctxCourses, setCtxCourses] = useState(false)
  const [ctxSaved, setCtxSaved] = useState(false)

  const loadChats = () =>
    listConversations({ source: "pam", limit: 50 })
      .then((d) => {
        setChats(d)
        setCache("chats", d)
      })
      .catch(() => {})

  useEffect(() => {
    loadChats()
  }, [])
  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: "smooth" })
  }, [messages])

  function growTextarea() {
    const el = taRef.current
    if (el) {
      el.style.height = "auto"
      el.style.height = Math.min(el.scrollHeight, 160) + "px"
    }
  }

  async function openChat(id: string) {
    setError(null)
    setSources([])
    try {
      const c = await getConversation(id)
      setMessages(
        c.messages.map((m) => ({
          role: m.role === "assistant" ? "assistant" : "user",
          content: m.content
        }))
      )
      setConvId(id)
    } catch (e) {
      setError(String(e))
    }
  }

  function newChat() {
    setMessages([])
    setConvId(null)
    setSources([])
    setError(null)
  }

  async function send() {
    const text = input.trim()
    if (!text || busy) return
    setInput("")
    requestAnimationFrame(growTextarea)
    setError(null)
    setSources([])
    setMeta(null)
    setBusy(true)
    setMessages((prev) => [
      ...prev,
      { role: "user", content: text },
      { role: "assistant", content: "" }
    ])
    try {
      await streamChat(text, convId, {
        onMeta: setMeta,
        onSources: setSources,
        onToken: (t) =>
          setMessages((prev) => {
            const copy = prev.slice()
            const last = copy[copy.length - 1]
            copy[copy.length - 1] = { ...last, content: last.content + t }
            return copy
          }),
        onError: (e) => setError(e),
        onDone: (id) => {
          if (id) setConvId(id)
          loadChats()
        }
      })
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="flex h-[calc(100vh-3.5rem)]">
      <ChatSidebar
        chats={chats}
        activeId={convId}
        onSelect={openChat}
        onNewChat={newChat}
        onChanged={loadChats}
        onDeletedActive={newChat}
      />

      {/* chat column */}
      <main className="flex-1 flex flex-col min-w-0">
        <div className="flex-1 overflow-y-auto">
          <div className="max-w-3xl mx-auto px-4 md:px-6 py-4 space-y-6">
            {messages.length === 0 ? (
              <div className="h-[62vh] flex flex-col items-center justify-center text-center px-6">
                <div className="w-12 h-12 rounded-xl bg-lime-400/10 border border-lime-400/30 text-lime-400 flex items-center justify-center text-lg font-semibold mb-4">
                  P
                </div>
                <div className="text-xl font-semibold mb-1.5">Чат с твоей памятью</div>
                <p className="text-neutral-400 text-sm font-sans max-w-md">
                  Спроси что угодно — я помню твои прошлые разговоры, материалы и
                  знания.
                </p>
              </div>
            ) : (
              messages.map((m, i) =>
                m.role === "user" ? (
                  <div key={i} className="flex justify-end">
                    <div className="bg-neutral-800 text-neutral-100 rounded-2xl rounded-br-md px-4 py-2.5 max-w-[80%] text-sm font-sans whitespace-pre-wrap">
                      {m.content}
                    </div>
                  </div>
                ) : (
                  <div key={i} className="flex gap-3">
                    <div className="shrink-0 w-7 h-7 rounded-md bg-lime-400/10 border border-lime-400/30 text-lime-400 flex items-center justify-center text-[11px] font-semibold mt-0.5">
                      P
                    </div>
                    <div className="min-w-0 flex-1">
                      {m.content ? (
                        <>
                          <Markdown>{m.content}</Markdown>
                          {busy && i === messages.length - 1 && (
                            <span className="inline-block w-2 h-4 -mb-0.5 ml-0.5 bg-lime-400 animate-pulse rounded-[1px]" />
                          )}
                        </>
                      ) : (
                        <TypingDots />
                      )}
                      {i === messages.length - 1 && sources.length > 0 && (
                        <div className="mt-3 flex flex-wrap gap-1.5 items-center">
                          <span className="text-[10px] uppercase tracking-widest text-neutral-600">
                            память:
                          </span>
                          {sources.map((s, j) => (
                            <span
                              key={j}
                              className="text-[10px] px-1.5 py-0.5 border border-neutral-800 rounded text-neutral-500 truncate max-w-[220px]">
                              {s.source}/{s.title || "—"}
                            </span>
                          ))}
                        </div>
                      )}
                      {i === messages.length - 1 && meta && m.content && (
                        <div className="mt-2 text-[10px] text-neutral-600 flex items-center gap-1">
                          <span>
                            {meta.provider === "openrouter" ? "🧠" : meta.provider === "ollama" ? "💻" : "⚡"}
                          </span>
                          <span className="truncate max-w-[280px]">
                            {meta.provider} · {meta.model.split("/").pop()?.replace(":free", "")}
                          </span>
                        </div>
                      )}
                    </div>
                  </div>
                )
              )
            )}
            <div ref={endRef} />
          </div>
        </div>

        {error && (
          <div className="max-w-3xl mx-auto w-full px-4 md:px-6 text-red-400 text-sm font-sans py-2">
            Ошибка: {error}
          </div>
        )}

        {/* input bar */}
        <div className="pt-2 pb-5">
          <div className="max-w-3xl mx-auto px-4 md:px-6">
            <form
              onSubmit={(e) => {
                e.preventDefault()
                send()
              }}
              className="rounded-[26px] border border-neutral-800 bg-neutral-900 px-4 pt-3 pb-2 shadow-lg shadow-black/20 focus-within:border-lime-400/50 transition-colors">
              <textarea
                ref={taRef}
                value={input}
                onChange={(e) => {
                  setInput(e.target.value)
                  growTextarea()
                }}
                onKeyDown={(e) => {
                  if (e.key === "Enter" && !e.shiftKey) {
                    e.preventDefault()
                    send()
                  }
                }}
                rows={1}
                placeholder="Напиши сообщение…"
                className="w-full resize-none bg-transparent outline-none text-sm font-sans leading-relaxed placeholder:text-neutral-600 max-h-40"
              />
              <div className="flex items-center justify-between gap-2 mt-1.5">
                <div className="flex items-center gap-0.5 min-w-0 overflow-x-auto [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
                  <CtxToggle
                    icon={<MemoryIcon />}
                    label="Использовать память"
                    active={useMemory}
                    chevron
                    onClick={() => setUseMemory((v) => !v)}
                  />
                  <span className="text-neutral-700 px-0.5 select-none">|</span>
                  <CtxToggle
                    icon={<DocIcon />}
                    label="Материалы"
                    active={ctxMaterials}
                    onClick={() => setCtxMaterials((v) => !v)}
                  />
                  <CtxToggle
                    icon={<CapIcon />}
                    label="Курсы"
                    active={ctxCourses}
                    onClick={() => setCtxCourses((v) => !v)}
                  />
                  <CtxToggle
                    icon={<StarIcon />}
                    label="Избранное"
                    active={ctxSaved}
                    onClick={() => setCtxSaved((v) => !v)}
                  />
                </div>
                <button
                  type="submit"
                  disabled={busy || !input.trim()}
                  aria-label="Отправить"
                  className="shrink-0 w-9 h-9 rounded-full bg-lime-400 text-neutral-950 flex items-center justify-center transition-colors disabled:bg-neutral-700 disabled:text-neutral-500">
                  <ArrowUpIcon />
                </button>
              </div>
            </form>
          </div>
        </div>
      </main>
    </div>
  )
}

function TypingDots() {
  return (
    <div className="flex items-center gap-1 py-2">
      {[0, 1, 2].map((i) => (
        <span
          key={i}
          className="w-1.5 h-1.5 rounded-full bg-neutral-500 animate-bounce"
          style={{ animationDelay: `${i * 0.15}s` }}
        />
      ))}
    </div>
  )
}

function CtxToggle({
  icon,
  label,
  active,
  chevron,
  onClick
}: {
  icon: React.ReactNode
  label: string
  active?: boolean
  chevron?: boolean
  onClick: () => void
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={active}
      className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-[12px] font-sans transition-colors whitespace-nowrap ${
        active
          ? "text-lime-400 bg-lime-400/10"
          : "text-neutral-400 hover:text-neutral-200 hover:bg-neutral-800"
      }`}>
      {icon}
      <span>{label}</span>
      {chevron && <ChevronDownIcon />}
    </button>
  )
}

function MemoryIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
      <rect x="4" y="4" width="16" height="16" rx="2" />
      <path d="M9 2v2M15 2v2M9 20v2M15 20v2M2 9h2M2 15h2M20 9h2M20 15h2" />
      <rect x="9" y="9" width="6" height="6" rx="1" />
    </svg>
  )
}
function DocIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
      <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
      <polyline points="14 2 14 8 20 8" />
    </svg>
  )
}
function CapIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
      <path d="M22 10L12 5 2 10l10 5 10-5z" />
      <path d="M6 12v5c3 2 9 2 12 0v-5" />
    </svg>
  )
}
function StarIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
      <path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z" />
    </svg>
  )
}
function ChevronDownIcon() {
  return (
    <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M6 9l6 6 6-6" />
    </svg>
  )
}
function ArrowUpIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M12 19V5M5 12l7-7 7 7" />
    </svg>
  )
}
