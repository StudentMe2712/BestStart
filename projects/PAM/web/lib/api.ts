/**
 * Backend API client used by the Next.js UI.
 */

const BACKEND_URL =
  process.env.NEXT_PUBLIC_BACKEND_URL || "http://localhost:8000"

export interface ConversationSummary {
  id: string
  source: "chatgpt" | "claude" | "gemini" | "pam"
  external_id: string
  title: string | null
  started_at: string | null
  updated_at: string
  pinned: boolean
  archived: boolean
  message_count: number
}

export interface MessageOut {
  id: string
  role: string
  content: string
  position: number
  sent_at: string | null
}

export interface ConversationDetail extends ConversationSummary {
  messages: MessageOut[]
}

export interface SearchHit {
  conversation_id: string
  message_id: string
  source: string
  title: string | null
  role: string
  snippet: string
  sent_at: string | null
  rank: number | null
}

export async function listConversations(opts?: {
  source?: string
  limit?: number
  offset?: number
  archived?: boolean
}): Promise<ConversationSummary[]> {
  const params = new URLSearchParams()
  if (opts?.source) params.set("source", opts.source)
  if (opts?.limit) params.set("limit", String(opts.limit))
  if (opts?.offset) params.set("offset", String(opts.offset))
  if (opts?.archived) params.set("archived", "true")
  const r = await fetch(`${BACKEND_URL}/conversations?${params}`, {
    cache: "no-store"
  })
  if (!r.ok) throw new Error(`list failed: ${r.status}`)
  return r.json()
}

/** Toggle pinned/archived on a conversation (sidebar actions). */
export async function patchConversation(
  id: string,
  body: { pinned?: boolean; archived?: boolean }
): Promise<ConversationSummary> {
  const r = await fetch(`${BACKEND_URL}/conversations/${id}`, {
    method: "PATCH",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(body)
  })
  if (!r.ok) throw new Error(`patch failed: ${r.status}`)
  return r.json()
}

export async function deleteConversation(id: string): Promise<void> {
  const r = await fetch(`${BACKEND_URL}/conversations/${id}`, { method: "DELETE" })
  if (!r.ok && r.status !== 204) throw new Error(`delete failed: ${r.status}`)
}

export async function getConversation(id: string): Promise<ConversationDetail> {
  const r = await fetch(`${BACKEND_URL}/conversations/${id}`, {
    cache: "no-store"
  })
  if (!r.ok) throw new Error(`detail failed: ${r.status}`)
  return r.json()
}

export type SearchMode = "text" | "semantic" | "hybrid"

export async function search(
  q: string,
  source?: string,
  mode: SearchMode = "text"
): Promise<SearchHit[]> {
  const path =
    mode === "semantic"
      ? "/search/semantic"
      : mode === "hybrid"
        ? "/search/hybrid"
        : "/search"
  const params = new URLSearchParams({ q })
  if (source) params.set("source", source)
  const r = await fetch(`${BACKEND_URL}${path}?${params}`, { cache: "no-store" })
  if (!r.ok) throw new Error(`search failed: ${r.status}`)
  return r.json()
}

// ---- Saved ("Избранное") messages ----

export interface SavedMessage {
  id: string
  conversation_id: string | null
  source: string
  title: string | null
  role: string
  content: string
  position: number | null
  note: string | null
  created_at: string
}

export interface SaveMessageInput {
  conversation_id?: string | null
  source: string
  title?: string | null
  role: string
  content: string
  position?: number | null
  note?: string | null
}

export async function saveMessage(
  input: SaveMessageInput
): Promise<SavedMessage> {
  const r = await fetch(`${BACKEND_URL}/saved`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(input)
  })
  if (!r.ok) throw new Error(`save failed: ${r.status}`)
  return r.json()
}

export async function listSaved(source?: string): Promise<SavedMessage[]> {
  const params = new URLSearchParams()
  if (source) params.set("source", source)
  const r = await fetch(`${BACKEND_URL}/saved?${params}`, { cache: "no-store" })
  if (!r.ok) throw new Error(`list saved failed: ${r.status}`)
  return r.json()
}

export async function deleteSaved(id: string): Promise<void> {
  const r = await fetch(`${BACKEND_URL}/saved/${id}`, { method: "DELETE" })
  if (!r.ok && r.status !== 204) throw new Error(`delete saved failed: ${r.status}`)
}

// ---- Profile facts (Phase 3, "память обо мне") ----

/** Fact Review Queue (P0): факт принимается в память только после проверки. */
export type FactStatus = "pending_review" | "approved" | "rejected" | "edited"

export interface ProfileFact {
  id: string
  category: string
  content: string
  source_conversation_id: string | null
  source_excerpt: string | null
  confidence: number
  status: FactStatus
  created_at: string
}

export async function listFacts(
  opts: { category?: string; status?: FactStatus } = {}
): Promise<ProfileFact[]> {
  const params = new URLSearchParams()
  if (opts.category) params.set("category", opts.category)
  if (opts.status) params.set("status", opts.status)
  const r = await fetch(`${BACKEND_URL}/facts?${params}`, { cache: "no-store" })
  if (!r.ok) throw new Error(`list facts failed: ${r.status}`)
  return r.json()
}

export type FactCounts = Record<FactStatus, number>

export async function factCounts(): Promise<FactCounts> {
  const r = await fetch(`${BACKEND_URL}/facts/counts`, { cache: "no-store" })
  if (!r.ok) throw new Error(`fact counts failed: ${r.status}`)
  return r.json()
}

export async function approveFact(id: string): Promise<ProfileFact> {
  const r = await fetch(`${BACKEND_URL}/facts/${id}/approve`, { method: "POST" })
  if (!r.ok) throw new Error(`approve fact failed: ${r.status}`)
  return r.json()
}

export async function rejectFact(id: string): Promise<ProfileFact> {
  const r = await fetch(`${BACKEND_URL}/facts/${id}/reject`, { method: "POST" })
  if (!r.ok) throw new Error(`reject fact failed: ${r.status}`)
  return r.json()
}

export async function updateFact(
  id: string,
  body: { content?: string; category?: string }
): Promise<ProfileFact> {
  const r = await fetch(`${BACKEND_URL}/facts/${id}`, {
    method: "PATCH",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(body)
  })
  if (!r.ok) {
    const d = await r.json().catch(() => ({}))
    throw new Error(d.detail || `update fact failed: ${r.status}`)
  }
  return r.json()
}

export async function deleteFact(id: string): Promise<void> {
  const r = await fetch(`${BACKEND_URL}/facts/${id}`, { method: "DELETE" })
  if (!r.ok && r.status !== 204) throw new Error(`delete fact failed: ${r.status}`)
}

export interface ExtractResult {
  conversations_processed: number
  facts_added: number
}

/** POST /facts/extract — scan up to `limit` un-processed conversations for new facts. */
export async function extractFacts(limit = 10): Promise<ExtractResult> {
  const r = await fetch(`${BACKEND_URL}/facts/extract?limit=${limit}`, {
    method: "POST"
  })
  if (!r.ok) throw new Error(`extract failed: ${r.status}`)
  return r.json()
}

// ---- Learning content (Phase 5 — личный лектор) ----

export interface ContentSource {
  id: string
  kind: "article" | "pdf" | "youtube"
  title: string | null
  url: string | null
  status: "pending" | "extracted" | "failed"
  char_count: number
  error: string | null
  created_at: string
}

export interface QuizQuestion {
  question: string
  options: string[]
  answer_index: number
  explanation?: string
}

export interface CourseLesson {
  title: string
  content: string
}

export interface CourseModule {
  title: string
  lessons: CourseLesson[]
}

export interface CourseData {
  title?: string
  level?: string
  summary?: string
  modules: CourseModule[]
  quiz: QuizQuestion[]
}

export interface Course {
  id: string
  source_id: string
  title: string | null
  level: string | null
  data: CourseData
  created_at: string
}

export async function listSources(): Promise<ContentSource[]> {
  const r = await fetch(`${BACKEND_URL}/learn/sources`, { cache: "no-store" })
  if (!r.ok) throw new Error(`list sources failed: ${r.status}`)
  return r.json()
}

export interface ContentSourceDetail extends ContentSource {
  text: string | null
  /** AI-причёсанная версия text (если уже сгенерирована), иначе null. */
  formatted_text: string | null
  /** #6 — статус фонового реформата: null (не запускали) | running | done | failed. */
  reformat_status: "running" | "done" | "failed" | null
  /** #6 — прогресс реформата в процентах (0..100). */
  reformat_progress: number
  /** true, если у источника сохранён оригинал файла для предпросмотра (PDF). */
  has_file: boolean
  /** MIME оригинала, если has_file. */
  mime: string | null
}

/** GET /learn/sources/{id} — one source incl. its extracted text. */
export async function getSource(id: string): Promise<ContentSourceDetail> {
  const r = await fetch(`${BACKEND_URL}/learn/sources/${id}`, { cache: "no-store" })
  if (!r.ok) throw new Error(`get source failed: ${r.status}`)
  return r.json()
}

/** URL для нативного предпросмотра оригинала файла (PDF) в <iframe>. */
export function sourceFileUrl(id: string): string {
  return `${BACKEND_URL}/learn/sources/${id}/file`
}

export interface ReformatStatus {
  status: "running" | "done" | "failed" | null
  progress: number
  formatted_text?: string | null
}

/** POST /learn/sources/{id}/reformat — запустить ФОНОВЫЙ реформат (#6).
    Возвращает текущий статус; готовый текст забираем поллингом getSource. */
export async function startReformat(id: string): Promise<ReformatStatus> {
  const r = await fetch(`${BACKEND_URL}/learn/sources/${id}/reformat`, {
    method: "POST"
  })
  if (!r.ok) {
    const d = await r.json().catch(() => ({}))
    throw new Error(d.detail || `reformat failed: ${r.status}`)
  }
  return r.json()
}

export async function ingestArticle(url: string): Promise<ContentSource> {
  const r = await fetch(`${BACKEND_URL}/learn/article`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ url })
  })
  if (!r.ok) {
    const d = await r.json().catch(() => ({}))
    throw new Error(d.detail || `article ingest failed: ${r.status}`)
  }
  return r.json()
}

export async function ingestYoutube(url: string): Promise<ContentSource> {
  const r = await fetch(`${BACKEND_URL}/learn/youtube`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ url })
  })
  if (!r.ok) {
    const d = await r.json().catch(() => ({}))
    throw new Error(d.detail || `youtube ingest failed: ${r.status}`)
  }
  return r.json()
}

/** True for youtube.com / youtu.be links (so the UI can route to /learn/youtube). */
export function isYoutubeUrl(url: string): boolean {
  try {
    const h = new URL(url).hostname.toLowerCase().replace(/^www\./, "")
    return (
      h === "youtu.be" ||
      h === "youtube.com" ||
      h === "m.youtube.com" ||
      h === "music.youtube.com"
    )
  } catch {
    return false
  }
}

export async function uploadPdf(file: File): Promise<ContentSource> {
  const fd = new FormData()
  fd.append("file", file)
  const r = await fetch(`${BACKEND_URL}/learn/pdf`, { method: "POST", body: fd })
  if (!r.ok) {
    const d = await r.json().catch(() => ({}))
    throw new Error(d.detail || `pdf upload failed: ${r.status}`)
  }
  return r.json()
}

// Универсальный инбокс: txt/md/html/docx/pdf → в память.
export async function uploadFile(file: File): Promise<ContentSource> {
  const fd = new FormData()
  fd.append("file", file)
  const r = await fetch(`${BACKEND_URL}/learn/file`, { method: "POST", body: fd })
  if (!r.ok) {
    const d = await r.json().catch(() => ({}))
    throw new Error(d.detail || `file upload failed: ${r.status}`)
  }
  return r.json()
}

// Вставленный вручную текст → в память.
export async function ingestText(
  text: string,
  title?: string
): Promise<ContentSource> {
  const r = await fetch(`${BACKEND_URL}/learn/text`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ text, title: title || null })
  })
  if (!r.ok) {
    const d = await r.json().catch(() => ({}))
    throw new Error(d.detail || `text ingest failed: ${r.status}`)
  }
  return r.json()
}

export async function deleteSource(id: string): Promise<void> {
  const r = await fetch(`${BACKEND_URL}/learn/sources/${id}`, { method: "DELETE" })
  if (!r.ok && r.status !== 204) throw new Error(`delete source failed: ${r.status}`)
}

export async function generateCourse(sourceId: string): Promise<Course> {
  const r = await fetch(`${BACKEND_URL}/learn/sources/${sourceId}/course`, {
    method: "POST"
  })
  if (!r.ok) {
    const d = await r.json().catch(() => ({}))
    throw new Error(d.detail || `course generation failed: ${r.status}`)
  }
  return r.json()
}

export async function getCourse(sourceId: string): Promise<Course | null> {
  const r = await fetch(`${BACKEND_URL}/learn/sources/${sourceId}/course`, {
    cache: "no-store"
  })
  if (!r.ok) throw new Error(`get course failed: ${r.status}`)
  return r.json()
}

// ---- Chat (Phase 4) ----

export interface SourceRef {
  source: string
  title: string | null
}

export interface ChatMeta {
  provider: string
  model: string
}

export interface ChatHandlers {
  onMeta?: (m: ChatMeta) => void
  onSources?: (s: SourceRef[]) => void
  onToken?: (t: string) => void
  onDone?: (conversationId: string | null) => void
  onError?: (e: string) => void
}

/** Вложение чата с уже распознанным текстом (передаётся в /chat как контекст). */
export interface ChatAttachment {
  name: string
  text: string
  /** data:-URL картинки для multimodal-режима (уходит прямо в vision-модель). */
  image_url?: string
}

/** Контекст-чипы: какие источники подмешивать в ретрив чата. */
export interface ChatCtx {
  memory: boolean
  materials: boolean
  courses: boolean
  saved: boolean
}

/** POST /learn/remember — сохранить распознанный текст вложения как материал. */
export async function rememberAttachment(input: {
  title: string
  text: string
  kind?: string
}): Promise<ContentSource> {
  const r = await fetch(`${BACKEND_URL}/learn/remember`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({
      title: input.title,
      text: input.text,
      kind: input.kind || "file"
    })
  })
  if (!r.ok) {
    const d = await r.json().catch(() => ({}))
    throw new Error(d.detail || `remember failed: ${r.status}`)
  }
  return r.json()
}

export interface ChatAttachmentResult {
  name: string
  kind: "image" | "document"
  text: string
  char_count: number
}

/**
 * POST /chat/attachment — распознать загруженный файл:
 * изображение → vision-модель, документ → markitdown. Возвращает извлечённый
 * текст, который потом уходит в /chat как контекст сообщения.
 */
export async function uploadChatAttachment(
  file: File
): Promise<ChatAttachmentResult> {
  const fd = new FormData()
  fd.append("file", file)
  const r = await fetch(`${BACKEND_URL}/chat/attachment`, {
    method: "POST",
    body: fd
  })
  if (!r.ok) {
    const d = await r.json().catch(() => ({}))
    throw new Error(d.detail || `attachment failed: ${r.status}`)
  }
  return r.json()
}

// ---- Diagnostics / quality panel (GET /stats) ----

export interface StatsMemory {
  conversations: number
  messages: number
  chunks_total: number
  chunks_embedded: number
  chunks_pending: number
}

export interface StatsLecturer {
  sources: number
  content_chunks_total: number
  content_chunks_embedded: number
  content_chunks_pending: number
  courses: number
}

export interface StatsFacts {
  pending_review: number
  approved: number
  rejected: number
  edited: number
  total: number
}

export interface StatsProvider {
  provider: string
  status: string
  count: number
}

export interface StatsTiming {
  kind: string
  count: number
  avg_ms: number
  p95_ms: number
}

export interface StatsRecentError {
  kind: string
  provider: string
  detail: string
  created_at: string
}

export interface StatsEvents {
  providers: StatsProvider[]
  fallbacks: number
  errors: number
  timing: StatsTiming[]
  recent_errors: StatsRecentError[]
}

export interface Stats {
  days: number
  memory: StatsMemory
  lecturer: StatsLecturer
  facts: StatsFacts
  events: StatsEvents
}

export async function getStats(days = 7): Promise<Stats> {
  const r = await fetch(`${BACKEND_URL}/stats?days=${days}`, {
    cache: "no-store"
  })
  if (!r.ok) throw new Error(`stats failed: ${r.status}`)
  return r.json()
}

/** POST /chat and consume the SSE stream (data: {...}\n\n). */
export async function streamChat(
  message: string,
  conversationId: string | null,
  h: ChatHandlers,
  signal?: AbortSignal,
  attachments?: ChatAttachment[],
  ctx?: ChatCtx,
  multimodal?: boolean
): Promise<void> {
  const r = await fetch(`${BACKEND_URL}/chat`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({
      message,
      conversation_id: conversationId,
      attachments: attachments && attachments.length ? attachments : undefined,
      use_memory: ctx ? ctx.memory : true,
      use_materials: ctx ? ctx.materials : false,
      use_courses: ctx ? ctx.courses : false,
      use_saved: ctx ? ctx.saved : false,
      multimodal: multimodal || undefined
    }),
    signal
  })
  if (!r.ok || !r.body) {
    h.onError?.(`chat failed: ${r.status}`)
    return
  }
  const reader = r.body.getReader()
  const dec = new TextDecoder()
  let buf = ""
  while (true) {
    const { value, done } = await reader.read()
    if (done) break
    buf += dec.decode(value, { stream: true })
    const parts = buf.split("\n\n")
    buf = parts.pop() || ""
    for (const part of parts) {
      const line = part.trim()
      if (!line.startsWith("data:")) continue
      try {
        const obj = JSON.parse(line.slice(5).trim())
        if (obj.meta) h.onMeta?.(obj.meta)
        if (obj.sources) h.onSources?.(obj.sources)
        if (obj.token) h.onToken?.(obj.token)
        if (obj.error) h.onError?.(obj.error)
        if (obj.done) h.onDone?.(obj.conversation_id ?? null)
      } catch {
        /* ignore partial/non-JSON */
      }
    }
  }
}
