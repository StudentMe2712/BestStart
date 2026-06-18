# web-memory

Project-specific instructions for Claude Code. This file overrides / extends the
baseline philosophy in the root CLAUDE.md.

## Stack
- **Chrome Extension Manifest V3** built with **WXT** (file-based entrypoints, Vite).
- **React 18 + TypeScript + Tailwind CSS**. Storage: **IndexedDB** (`idb`). Search: **FlexSearch**.
- Entrypoints: `background.ts` (owns IndexedDB), `content/` (in-page shadow-root UI + anchoring),
  `sidepanel/` (main UI). Shared code in `lib/` (`types`, `messages`, `db`, `anchor`, `highlight`,
  `search`, `url`).
- Run: `npm install` → `npm run dev` (HMR) / `npm run build` (→ `.output/chrome-mv3`) /
  `npm run compile` (tsc). Load via `chrome://extensions` → Load unpacked → `.output/chrome-mv3`.
- **Key constraint:** content scripts run in the page origin and CANNOT access the extension's
  IndexedDB — all persistence goes through the background service worker via typed messages
  (`lib/messages.ts`). Don't add a second IndexedDB owner.
- No Docker (the artifact is static files loaded into the browser, no server/DB to containerise).

## Tools enabled (copied from root library into .claude/)
- agents:   ecc/typescript-reviewer, ecc/react-reviewer
- skills:   superpowers, karpathy, ecc/react-patterns, ecc/vite-patterns
- commands: gsd
- rules:    karpathy
- hooks:    
- mcp:      context7 (live docs)

## ⛔ Tool-selection gate (before building anything)
When I give you a new task or paste a prompt, **do not start coding immediately.** Run
`/start-task` first (it reads the root `library/CATALOG.md` + `library/mcp/README.md`,
proposes the best-matching tools as a grouped list of max ~7, and installs my picks via
`scripts/add-tools.ps1`). Only after I choose do you start development.
Skip only if I say "skip tools" or the needed tools are already in `.claude/`.

## 📓 Lessons
Read `LESSONS.md` (this project) and the root `LESSONS.md` at the start of work. After a
non-obvious bug or wrong approach, append an entry with `/lesson`.

## Conventions
- (project-specific rules go here)
