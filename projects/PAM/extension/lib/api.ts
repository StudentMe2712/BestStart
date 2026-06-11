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
