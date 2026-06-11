/**
 * Background service worker.
 *
 * Receives normalized conversations from content scripts and forwards
 * them to the local backend. Implements a simple retry queue.
 */

import { sendConversation, type IncomingConversation } from "~lib/api"

interface QueueItem {
  payload: IncomingConversation
  attempts: number
  addedAt: number
}

const queue: QueueItem[] = []
let processing = false
let stats = { total: 0, success: 0, failed: 0 }

const MAX_ATTEMPTS = 5
const RETRY_DELAY_MS = 4000

// --- Stats persistence ---
chrome.storage.local.get("pam_stats").then((res) => {
  if (res.pam_stats) stats = res.pam_stats
})

function saveStats() {
  chrome.storage.local.set({ pam_stats: stats })
}

// --- Message handler ---
chrome.runtime.onMessage.addListener((msg, _sender, sendResponse) => {
  if (msg?.type === "CAPTURE_CONVERSATION" && msg.payload) {
    queue.push({ payload: msg.payload, attempts: 0, addedAt: Date.now() })
    processQueue()
    sendResponse({ queued: true, queueSize: queue.length })
    return true
  }
  if (msg?.type === "GET_STATS") {
    sendResponse({ ...stats, queueSize: queue.length })
    return true
  }
  if (msg?.type === "RESET_STATS") {
    stats = { total: 0, success: 0, failed: 0 }
    saveStats()
    sendResponse({ ok: true })
    return true
  }
  return false
})

// --- Queue processor ---
async function processQueue() {
  if (processing) return
  processing = true

  while (queue.length > 0) {
    const item = queue[0]
    try {
      const result = await sendConversation(item.payload)
      console.log(
        `[PAM] Saved conversation ${item.payload.source}:${item.payload.external_id}`,
        result
      )
      stats.total += 1
      stats.success += 1
      saveStats()
      queue.shift()
    } catch (err) {
      item.attempts += 1
      console.warn(
        `[PAM] Failed to send (attempt ${item.attempts}/${MAX_ATTEMPTS}):`,
        err
      )
      if (item.attempts >= MAX_ATTEMPTS) {
        console.error("[PAM] Giving up on", item.payload.external_id)
        stats.failed += 1
        saveStats()
        queue.shift()
      } else {
        await sleep(RETRY_DELAY_MS * item.attempts)
      }
    }
  }

  processing = false
}

function sleep(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms))
}

console.log("[PAM] Background worker ready")
