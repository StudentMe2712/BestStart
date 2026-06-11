/**
 * Isolated-world relay (bridge).
 *
 * The per-site content scripts (claude.ts / chatgpt.ts / gemini.ts) run in the
 * page world (`world: "MAIN"`) so they can patch `window.fetch`. But in the MAIN
 * world `chrome.runtime` is NOT available, so they cannot talk to the background
 * worker directly — they `window.postMessage(...)` instead.
 *
 * This script runs in the DEFAULT isolated content-script world (no `world: "MAIN"`),
 * where `chrome.runtime.sendMessage` IS available. It shares the same page `window`
 * (same postMessage event target) as the MAIN-world scripts, so it can receive their
 * messages and forward the normalized conversation to the background worker.
 *
 * See CLAUDE.md → "The split between page world and isolated content-script world matters".
 */

import type { PlasmoCSConfig } from "plasmo"

export const config: PlasmoCSConfig = {
  matches: [
    "https://chatgpt.com/*",
    "https://chat.openai.com/*",
    "https://claude.ai/*",
    "https://gemini.google.com/*"
  ],
  run_at: "document_start"
  // no `world` → runs in the isolated world, where chrome.runtime exists
}

window.addEventListener("message", (event) => {
  if (event.source !== window) return
  if (!event.data?.__PAM__) return
  chrome.runtime.sendMessage({
    type: "CAPTURE_CONVERSATION",
    payload: event.data.payload
  })
})

console.log("[PAM/relay] isolated-world bridge ready")
