/**
 * Backend API client used by the background worker.
 */

const BACKEND_URL = "http://localhost:8000"

export type Role = "user" | "assistant" | "system" | "tool"
export type Source = "chatgpt" | "claude" | "gemini"

export interface IncomingMessage {
  role: Role
  content: string
  position?: number
  sent_at?: string | null
}

export interface IncomingConversation {
  source: Source
  external_id: string
  title?: string | null
  started_at?: string | null
  messages: IncomingMessage[]
  raw?: Record<string, unknown> | null
}

export interface IngestResult {
  conversation_id: string
  created: boolean
  message_count: number
}

export async function sendConversation(
  payload: IncomingConversation
): Promise<IngestResult> {
  const resp = await fetch(`${BACKEND_URL}/conversations`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(payload)
  })
  if (!resp.ok) {
    const text = await resp.text()
    throw new Error(`Backend error ${resp.status}: ${text}`)
  }
  return resp.json()
}

/**
 * Сообщить бэкенду о перманентном сбросе захвата (после исчерпания ретраев) —
 * для observability «сколько не удалось захватить». Best-effort: если бэкенд
 * недоступен, фиксировать всё равно негде, так что ошибку глотаем.
 */
export async function reportCaptureFailure(
  source: string,
  reason: string
): Promise<void> {
  try {
    await fetch(`${BACKEND_URL}/stats/capture-failed`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ source, reason: reason.slice(0, 255) })
    })
  } catch {
    /* backend недоступен — сброс не зафиксировать, это ожидаемо */
  }
}
