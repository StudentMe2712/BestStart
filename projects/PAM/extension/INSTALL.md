# Extension setup (Plasmo)

This folder contains the **source files** for the browser extension.
Plasmo handles bundling and the manifest for us.

## One-time setup

```bash
cd extension
npm init -y
npm install plasmo@latest react react-dom
npm install -D typescript @types/react @types/react-dom @types/chrome
```

Then add to `package.json`:

```json
{
  "scripts": {
    "dev": "plasmo dev",
    "build": "plasmo build",
    "package": "plasmo package"
  },
  "manifest": {
    "host_permissions": [
      "https://chatgpt.com/*",
      "https://claude.ai/*",
      "https://gemini.google.com/*",
      "http://localhost:8000/*"
    ],
    "permissions": ["storage", "webRequest"]
  }
}
```

Plasmo also needs a `tsconfig.json` (it does NOT auto-generate one in v0.90+) and an
icon at `assets/icon.png` (it generates the sized variants from it). Both are committed
in this folder — don't delete them.

## Run

```bash
npm run dev      # writes to build/chrome-mv3-dev
npm run build    # writes to build/chrome-mv3-prod
```

Then in Chrome → `chrome://extensions` → enable Developer mode →
"Load unpacked" → select `extension/build/chrome-mv3-dev` (dev) or `-prod` (build).

## Files included

- `background.ts` — service worker, queue, sends to backend
- `popup.tsx` — small UI with stats counter
- `contents/chatgpt.ts` — MAIN-world fetch patch for chatgpt.com
- `contents/claude.ts` — MAIN-world fetch patch for claude.ai
- `contents/gemini.ts` — STUB for gemini.google.com (implement after DevTools)
- `contents/relay.ts` — isolated-world bridge: `postMessage` → `chrome.runtime.sendMessage`
- `lib/api.ts` — backend API client
- `tsconfig.json`, `assets/icon.png` — required by Plasmo

> ⚠️ The per-site scripts run in `world: "MAIN"` (to patch `window.fetch`), where
> `chrome.runtime` is unavailable. They `window.postMessage` to `contents/relay.ts`
> (isolated world) which forwards to the background. Don't put `chrome.*` calls in
> the MAIN-world scripts. See CLAUDE.md → page vs isolated world.

## How perchatchikov work

Each content script **patches `window.fetch`** to intercept API responses
from the AI site, then normalizes them into a unified format and sends
to the backend via `chrome.runtime.sendMessage`.

The background worker batches sends and handles retries.

## When something breaks

AI sites change their API frequently. Logs are in:

- Background: `chrome://extensions` → your extension → service worker → "Inspect"
- Content: DevTools on the AI site itself

Most common issue: site changed the endpoint URL or the JSON shape.
You'll see `parse error` in the content script logs and zero conversations
arriving at the backend.
