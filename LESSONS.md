# Lessons learned — GLOBAL (all projects)

General guardrails learned across projects. Read at session start (it's near the root
CLAUDE.md). Project-specific lessons live in each `projects/<name>/LESSONS.md`.
Add entries with `/lesson` (Scope: all-projects). Newest on top.

## Log

### 2026-06-09 — Windows MCP servers need `cmd /c`
- **Problem:** MCP servers configured with bare `"command": "npx"` fail to start on Windows.
- **Root cause:** Claude Code on Windows doesn't resolve `npx` directly as a process.
- **Fix:** wrap as `"command": "cmd", "args": ["/c", "npx", "-y", "<pkg>"]`.
- **Rule:** on Windows, always launch npx-based MCP servers via `cmd /c`.
- **Scope:** all-projects
