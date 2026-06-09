# MCP servers catalog

MCP (Model Context Protocol) servers give Claude Code extra capabilities (live docs,
browser control, GitHub API, …). They are configured per-project via a `.mcp.json` file in
the project root.

## Golden rule
**Keep 3–5 servers active.** Past ~5, tool descriptions crowd the context window and Claude
starts picking the wrong tool. Enable per project only what that project needs.

## The starter pack (recommended default)
`starter.mcp.json` → **context7 + playwright + sequential-thinking** (no API keys, instant value):
- **context7** — fetches current, version-specific docs for any library. Kills hallucinated APIs.
- **playwright** — drives a real browser for E2E tests / web automation.
- **sequential-thinking** — structured step-by-step reasoning for hard problems.

`full.mcp.json` adds **github, memory, filesystem, exa, deepwiki** — copy it and delete what
you don't want.

## How to enable in a project
Option A — file (project-scoped, versionable):
```powershell
Copy-Item library/mcp/starter.mcp.json projects/<name>/.mcp.json
```
Or use the bootstrap: `./scripts/new-project.ps1 -Name app -Mcp starter`

Option B — CLI (Windows syntax, note the `cmd /c`):
```powershell
claude mcp add context7 -- cmd /c npx -y @upstash/context7-mcp
claude mcp add playwright -- cmd /c npx -y @playwright/mcp@latest
claude mcp add sequential-thinking -- cmd /c npx -y @modelcontextprotocol/server-sequential-thinking
```
Verify with `claude mcp list` (or `/mcp` inside a session).

## Windows note
All configs use `"command": "cmd", "args": ["/c", "npx", ...]`. Bare `npx` frequently fails to
launch MCP servers on Windows in Claude Code — the `cmd /c` wrapper fixes that.

## Keys
- **github** needs `GITHUB_PERSONAL_ACCESS_TOKEN` (set it in your environment, never commit it).
- **exa** is a hosted endpoint and may require an account on Exa's side.
- Everything else in the starter pack runs key-free.

Package versions are unpinned (`@latest` semantics via `npx -y`) so they stay current; pin a
version (e.g. `@upstash/context7-mcp@2.1.8`) if you need reproducibility.
