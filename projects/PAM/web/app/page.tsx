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
              <div className="h-[60vh] flex flex-col items-center justify-center text-center px-6">
                <div className="w-10 h-10 rounded-xl bg-lime-400/10 border border-lime-400/30 text-lime-400 flex items-center justify-center font-semibold mb-4">
                  P
                </div>
                <div className="text-lg font-semibold mb-1">Чат с твоей памятью</div>
                <p className="text-neutral-400 text-sm font-sans max-w-md">
                  Спроси что угодно — я помню твои прошлые разговоры (ChatGPT,
                  Claude, Gemini) и отвечаю с опорой на них.
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
        <div className="border-t border-neutral-800 pt-3 pb-4">
          <div className="max-w-3xl mx-auto px-4 md:px-6">
            <form
              onSubmit={(e) => {
                e.preventDefault()
                send()
              }}
              className="flex items-end gap-2 bg-neutral-900 border border-neutral-800 rounded-2xl px-3 py-2 focus-within:border-lime-400/50 transition-colors">
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
                className="flex-1 resize-none bg-transparent outline-none text-sm font-sans py-1.5 placeholder:text-neutral-600 max-h-40"
              />
              <button
                type="submit"
                disabled={busy || !input.trim()}
                aria-label="Отправить"
                className="shrink-0 w-8 h-8 rounded-full bg-lime-400 text-neutral-950 font-bold flex items-center justify-center transition-colors disabled:bg-neutral-700 disabled:text-neutral-500">
                ↑
              </button>
            </form>
            <div className="text-[10px] text-neutral-600 text-center mt-1.5">
              PAM помнит твои разговоры · Enter — отправить, Shift+Enter — перенос
            </div>
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
