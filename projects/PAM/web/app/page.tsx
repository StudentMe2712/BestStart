"use client"

import { useEffect, useRef, useState } from "react"

import {
  getConversation,
  listConversations,
  streamChat,
  uploadChatAttachment,
  type ChatMeta,
  type ConversationSummary,
  type SourceRef
} from "../lib/api"
import { getCache, setCache } from "../lib/cache"
import ChatSidebar from "./chat-sidebar"
import Markdown from "./markdown"

interface Attach {
  id: string
  name: string
  kind: string
  text: string
  status: "loading" | "done" | "error"
  error?: string
}

interface Msg {
  role: "user" | "assistant"
  content: string
  attachments?: { name: string }[]
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
  const fileRef = useRef<HTMLInputElement>(null)
  // Вложения текущего сообщения (распознаются на бэке: изображения → vision,
  // документы → markitdown).
  const [attachments, setAttachments] = useState<Attach[]>([])
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
    setAttachments([])
    setError(null)
  }

  async function onAttach(e: React.ChangeEvent<HTMLInputElement>) {
    const files = Array.from(e.target.files || [])
    e.target.value = "" // позволяем повторно выбрать тот же файл
    for (const f of files) {
      const id =
        (globalThis.crypto?.randomUUID?.() as string) || `${Date.now()}-${f.name}`
      setAttachments((prev) => [
        ...prev,
        { id, name: f.name, kind: "", text: "", status: "loading" }
      ])
      try {
        const res = await uploadChatAttachment(f)
        setAttachments((prev) =>
          prev.map((a) =>
            a.id === id
              ? { ...a, kind: res.kind, text: res.text, status: "done" }
              : a
          )
        )
      } catch (err) {
        setAttachments((prev) =>
          prev.map((a) =>
            a.id === id ? { ...a, status: "error", error: String(err) } : a
          )
        )
      }
    }
  }

  function removeAttach(id: string) {
    setAttachments((prev) => prev.filter((a) => a.id !== id))
  }

  async function send() {
    const text = input.trim()
    const ready = attachments.filter((a) => a.status === "done")
    const loading = attachments.some((a) => a.status === "loading")
    if (busy || loading || (!text && ready.length === 0)) return
    setInput("")
    requestAnimationFrame(growTextarea)
    setError(null)
    setSources([])
    setMeta(null)
    setBusy(true)
    const atts = ready.map((a) => ({ name: a.name, text: a.text }))
    setMessages((prev) => [
      ...prev,
      {
        role: "user",
        content: text,
        attachments: ready.length ? ready.map((a) => ({ name: a.name })) : undefined
      },
      { role: "assistant", content: "" }
    ])
    setAttachments([])
    try {
      await streamChat(
        text,
        convId,
        {
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
        },
        undefined,
        atts
      )
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
      <main className="relative flex-1 flex flex-col min-w-0 overflow-hidden">
        {/* Мягкий lime-glow по центру секции чата (как на референсе). */}
        <div
          aria-hidden
          className="pointer-events-none absolute inset-0 z-0"
          style={{
            background:
              "radial-gradient(58% 52% at 50% 42%, rgba(163,230,53,0.09) 0%, rgba(163,230,53,0.04) 30%, transparent 65%)"
          }}
        />
        <div className="relative z-10 flex-1 overflow-y-auto">
          <div className="max-w-3xl mx-auto px-4 md:px-6 py-4 space-y-6">
            {messages.length === 0 ? (
              <div className="h-[62vh] flex flex-col items-center justify-center text-center px-6">
                <div className="w-12 h-12 rounded-xl bg-lime-400/10 border border-lime-400/30 text-lime-400 flex items-center justify-center text-lg font-semibold mb-4">
                  P
                </div>
                <div className="text-xl font-semibold mb-1.5">Чат с твоей памятью</div>
                <p className="text-neutral-400 text-sm font-sans max-w-md">
                  Спроси что угодно — я помню прошлые разговоры, материалы и
                  знания.
                </p>
              </div>
            ) : (
              messages.map((m, i) =>
                m.role === "user" ? (
                  <div key={i} className="flex justify-end">
                    <div className="flex flex-col items-end gap-1.5 max-w-[80%]">
                      {m.attachments?.length ? (
                        <div className="flex flex-wrap justify-end gap-1.5">
                          {m.attachments.map((a, j) => (
                            <span
                              key={j}
                              className="inline-flex items-center gap-1.5 text-[11px] font-sans px-2 py-1 rounded-lg bg-neutral-800/70 border border-neutral-700 text-neutral-300">
                              <PaperclipIcon small />
                              <span className="truncate max-w-[160px]">{a.name}</span>
                            </span>
                          ))}
                        </div>
                      ) : null}
                      {m.content && (
                        <div className="bg-neutral-800 text-neutral-100 rounded-2xl rounded-br-md px-4 py-2.5 text-sm font-sans whitespace-pre-wrap">
                          {m.content}
                        </div>
                      )}
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
          <div className="relative z-10 max-w-3xl mx-auto w-full px-4 md:px-6 text-red-400 text-sm font-sans py-2">
            Ошибка: {error}
          </div>
        )}

        {/* input bar — Glass Panel (главный визуальный акцент) */}
        <div className="relative z-10 pt-2 pb-6">
          <div className="max-w-[1100px] mx-auto px-4 md:px-6">
            <form
              onSubmit={(e) => {
                e.preventDefault()
                send()
              }}
              className="rounded-[28px] border border-white/[0.12] bg-white/[0.06] backdrop-blur-[24px] px-4 pt-3 pb-2.5 shadow-2xl shadow-black/40 focus-within:border-lime-400/40 transition-all duration-[180ms]">
              {attachments.length > 0 && (
                <div className="flex flex-wrap gap-2 mb-2.5">
                  {attachments.map((a) => (
                    <AttachChip
                      key={a.id}
                      a={a}
                      onRemove={() => removeAttach(a.id)}
                    />
                  ))}
                </div>
              )}
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
                placeholder="Напиши сообщение..."
                className="w-full resize-none bg-transparent outline-none text-sm font-sans leading-relaxed placeholder:text-neutral-500 max-h-40"
              />
              <div className="flex items-center justify-between gap-2 mt-2">
                <div className="flex items-center gap-1 min-w-0 overflow-x-auto [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
                  <button
                    type="button"
                    onClick={() => fileRef.current?.click()}
                    title="Прикрепить файл или изображение"
                    aria-label="Прикрепить"
                    className="shrink-0 w-8 h-8 grid place-items-center rounded-full text-neutral-400 hover:text-lime-400 hover:bg-white/[0.05] transition-colors">
                    <PaperclipIcon />
                  </button>
                  <input
                    ref={fileRef}
                    type="file"
                    multiple
                    accept="image/*,.pdf,.docx,.pptx,.xlsx,.xls,.txt,.md,.markdown,.html,.htm,.csv,.json,.log,.rst"
                    onChange={onAttach}
                    className="hidden"
                  />
                  <span className="text-white/10 px-0.5 select-none">|</span>
                  <CtxToggle
                    icon={<MemoryIcon />}
                    label="Использовать память"
                    active={useMemory}
                    chevron
                    onClick={() => setUseMemory((v) => !v)}
                  />
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
                  disabled={
                    busy ||
                    (!input.trim() &&
                      attachments.filter((a) => a.status === "done").length === 0)
                  }
                  aria-label="Отправить"
                  className="shrink-0 w-12 h-12 rounded-full bg-lime-400 text-neutral-950 flex items-center justify-center transition-all duration-[180ms] hover:shadow-[0_0_20px_rgba(163,230,53,0.45)] disabled:bg-neutral-700 disabled:text-neutral-500 disabled:shadow-none">
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
      className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-[12px] font-sans transition-all duration-[180ms] whitespace-nowrap border backdrop-blur-[12px] ${
        active
          ? "text-lime-400 bg-lime-400/10 border-lime-400/30"
          : "text-neutral-300 bg-white/[0.05] border-white/[0.08] hover:text-neutral-100 hover:border-lime-400/30"
      }`}>
      {icon}
      <span>{label}</span>
      {chevron && <ChevronDownIcon />}
    </button>
  )
}

function AttachChip({ a, onRemove }: { a: Attach; onRemove: () => void }) {
  const err = a.status === "error"
  return (
    <span
      title={err ? a.error : a.name}
      className={`inline-flex items-center gap-1.5 text-[12px] font-sans px-2.5 py-1.5 rounded-xl border backdrop-blur-[12px] max-w-[260px] ${
        err
          ? "border-red-500/40 bg-red-500/10 text-red-300"
          : "border-white/[0.08] bg-white/[0.05] text-neutral-200"
      }`}>
      <span className="shrink-0 text-neutral-400">
        {a.status === "loading" ? (
          <Spinner />
        ) : a.kind === "image" ? (
          <ImageIcon />
        ) : (
          <FileIcon />
        )}
      </span>
      <span className="truncate">{a.name}</span>
      {a.status === "loading" && (
        <span className="shrink-0 text-neutral-500">распознаю…</span>
      )}
      {err && <span className="shrink-0 text-red-400">ошибка</span>}
      <button
        type="button"
        onClick={onRemove}
        aria-label="Убрать вложение"
        className="shrink-0 ml-0.5 text-neutral-500 hover:text-neutral-100 transition-colors">
        <CloseTinyIcon />
      </button>
    </span>
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
function PaperclipIcon({ small = false }: { small?: boolean }) {
  const s = small ? 12 : 17
  return (
    <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.9" strokeLinecap="round" strokeLinejoin="round">
      <path d="M21.44 11.05l-9.19 9.19a5 5 0 0 1-7.07-7.07l8.49-8.49a3 3 0 1 1 4.24 4.24l-8.49 8.49a1 1 0 0 1-1.41-1.41l7.78-7.78" />
    </svg>
  )
}
function ImageIcon() {
  return (
    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.9" strokeLinecap="round" strokeLinejoin="round">
      <rect x="3" y="3" width="18" height="18" rx="2" />
      <circle cx="8.5" cy="8.5" r="1.5" />
      <path d="M21 15l-5-5L5 21" />
    </svg>
  )
}
function FileIcon() {
  return (
    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.9" strokeLinecap="round" strokeLinejoin="round">
      <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
      <polyline points="14 2 14 8 20 8" />
    </svg>
  )
}
function Spinner() {
  return (
    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" className="animate-spin">
      <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="2.5" opacity="0.25" />
      <path d="M21 12a9 9 0 0 0-9-9" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
    </svg>
  )
}
function CloseTinyIcon() {
  return (
    <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round">
      <path d="M18 6L6 18M6 6l12 12" />
    </svg>
  )
}
