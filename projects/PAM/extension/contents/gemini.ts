/**
 * Content script for gemini.google.com
 *
 * STATUS: Stub. Gemini is the trickiest of the three — responses come as
 * streaming chunks of a non-standard format ("XHR with chunked text/plain"
 * containing JSON-like payloads in a custom encoding).
 *
 * VIBE-CODING TASK: Open gemini.google.com → start a chat → DevTools Network
 * tab → find the request that returns the assistant's reply → study its
 * format → write a parser here.
 *
 * Until you implement this, claude.ai and chatgpt.com capture will work fine
 * — Gemini just won't.
 */

import type { PlasmoCSConfig } from "plasmo"

export const config: PlasmoCSConfig = {
  matches: ["https://gemini.google.com/*"],
  run_at: "document_start",
  world: "MAIN"
}

console.log("[PAM/gemini] stub loaded — implement me!")

// TODO:
// 1. Identify the streaming endpoint (currently something like
//    /_/BardChatUi/data/assistant.lamda.BardFrontendService/StreamGenerate)
// 2. Buffer chunks per conversation_id (which you'll need to extract from URL or DOM)
// 3. When stream completes (or after debouncing), assemble full message and emit
//
// You can also fall back to a DOM-based approach if streams are unparseable:
//    use MutationObserver on the messages container, extract text from rendered
//    DOM nodes. Less reliable but works in a pinch.
