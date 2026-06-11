/**
 * Content script for chatgpt.com
 *
 * Intercepts /backend-api/conversation/{id} responses.
 *
 * ChatGPT API returns conversations as a tree (`mapping`). We linearize it
 * to a chronological message list.
 */

import type { PlasmoCSConfig } from "plasmo"

export const config: PlasmoCSConfig = {
  matches: ["https://chatgpt.com/*", "https://chat.openai.com/*"],
  run_at: "document_start",
  world: "MAIN"
}

const URL_RE = /\/backend-api\/conversation\/[a-f0-9-]+(?:\?|$)/i

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
    /* ignore */
  }
  return resp
}

function handleConversation(data: any) {
  if (!data || !data.mapping) return
  try {
    const messages = linearize(data.mapping)
    const byRole = messages.reduce<Record<string, number>>((a, m) => {
      a[m.role] = (a[m.role] || 0) + 1
      return a
    }, {})
    console.log(
      `[PAM/chatgpt] extracted ${messages.length} from ${Object.keys(data.mapping).length} nodes`,
      byRole
    )
    if (messages.length === 0) return

    const normalized = {
      source: "chatgpt" as const,
      external_id: String(data.conversation_id || data.id || ""),
      title: data.title || null,
      started_at: data.create_time
        ? new Date(data.create_time * 1000).toISOString()
        : null,
      messages,
      raw: data
    }
    if (!normalized.external_id) return

    // Bridge: MAIN world -> isolated-world relay (contents/relay.ts) via postMessage.
    // We can't call chrome.runtime.sendMessage here: this script runs in world "MAIN".
    window.postMessage(
      { __PAM__: true, payload: normalized },
      window.location.origin
    )
  } catch (err) {
    console.warn("[PAM/chatgpt] parse error:", err)
  }
}

function linearize(mapping: Record<string, any>): any[] {
  // find root
  const rootId = Object.keys(mapping).find((k) => mapping[k]?.parent === null)
  if (!rootId) return []

  const result: any[] = []
  const stack: string[] = [rootId]
  const seen = new Set<string>()
  let pos = 0
  while (stack.length) {
    const nid = stack.shift()!
    if (seen.has(nid)) continue
    seen.add(nid)
    const node = mapping[nid]
    if (!node) continue
    const msg = node.message
    if (msg) {
      const role = msg.author?.role || "user"
      // ChatGPT parts can be plain strings OR objects (multimodal / structured
      // content). The old code dropped object-parts, losing whole messages.
      const parts: string[] = (msg.content?.parts || [])
        .map((p: any) => {
          if (typeof p === "string") return p
          if (p && typeof p === "object") return p.text || p.content || ""
          return ""
        })
        .filter((s: string) => typeof s === "string" && s.trim().length > 0)
      const content = parts.join("\n").trim()
      if (content) {
        result.push({
          role: ["user", "assistant", "system", "tool"].includes(role)
            ? role
            : "user",
          content,
          position: pos++,
          sent_at: msg.create_time
            ? new Date(msg.create_time * 1000).toISOString()
            : null
        })
      }
    }
    for (const cid of node.children || []) stack.push(cid)
  }
  return result
}

// The isolated-world side of the bridge lives in contents/relay.ts.

console.log("[PAM/chatgpt] patched fetch")
