/**
 * Content script for claude.ai
 *
 * Intercepts fetch() responses to capture conversation data.
 * The relevant endpoint is roughly:
 *   GET /api/organizations/{org_id}/chat_conversations/{conv_id}?...
 *
 * Strategy:
 *   1. Inject a patched fetch into the page (page context, not isolated content world)
 *      so we can read response bodies.
 *   2. Listen for our custom event and forward to the background worker.
 *
 * NOTE FOR VIBE-CODING:
 *   This is the MOST FRAGILE part of Phase 1. The endpoint URL and JSON shape
 *   change every few months. When you see "parse error" in logs, open the
 *   network tab on claude.ai, find the request that returns a full conversation,
 *   and update the URL_RE and the parser.
 */

import type { PlasmoCSConfig } from "plasmo"

export const config: PlasmoCSConfig = {
  matches: ["https://claude.ai/*"],
  run_at: "document_start",
  world: "MAIN"
}

const URL_RE = /\/api\/organizations\/[^/]+\/chat_conversations\/[a-f0-9-]+/i

// 1. Patch fetch in the page context
const origFetch = window.fetch
window.fetch = async function (...args: Parameters<typeof fetch>) {
  const resp = await origFetch.apply(this, args)
  try {
    const url = typeof args[0] === "string" ? args[0] : (args[0] as Request).url
    if (URL_RE.test(url) && resp.ok) {
      const cloned = resp.clone()
      cloned
        .json()
        .then((data) => handleConversation(data))
        .catch(() => {})
    }
  } catch {
    // ignore
  }
  return resp
}

function handleConversation(data: any) {
  if (!data || !data.uuid) return
  try {
    const normalized = {
      source: "claude" as const,
      external_id: String(data.uuid),
      title: data.name || data.title || null,
      started_at: data.created_at || null,
      messages: (data.chat_messages || [])
        .map((m: any, idx: number) => {
          const sender = (m.sender || "").toLowerCase()
          const role =
            sender === "human"
              ? "user"
              : sender === "assistant"
                ? "assistant"
                : "user"
          const text = m.text || extractBlocks(m.content)
          if (!text) return null
          return {
            role,
            content: String(text).trim(),
            position: idx,
            sent_at: m.created_at || null
          }
        })
        .filter(Boolean),
      raw: data
    }
    console.log(
      `[PAM/claude] extracted ${normalized.messages.length} of ${(data.chat_messages || []).length} chat_messages`
    )
    if (normalized.messages.length === 0) return

    // Bridge: MAIN world -> isolated-world relay (contents/relay.ts) via postMessage.
    // We can't call chrome.runtime.sendMessage here: this script runs in world "MAIN".
    window.postMessage(
      { __PAM__: true, payload: normalized },
      window.location.origin
    )
  } catch (err) {
    console.warn("[PAM/claude] parse error:", err)
  }
}

function extractBlocks(blocks: any): string {
  if (!blocks || !Array.isArray(blocks)) return ""
  return blocks
    .filter((b: any) => b && b.type === "text")
    .map((b: any) => b.text || "")
    .join("\n")
}

// The isolated-world side of the bridge lives in contents/relay.ts.

console.log("[PAM/claude] patched fetch")
