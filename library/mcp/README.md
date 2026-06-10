# MCP servers catalog

MCP (Model Context Protocol) servers give Claude Code extra capabilities (live docs, browser
control, GitHub/DB access, …). They're configured per-project via a `.mcp.json` file in the
project root. This folder is a **catalog of presets** — copy one in and trim it down.

## Golden rule
**Keep 3–5 servers active per project.** Past ~5, tool descriptions crowd the context window
and Claude starts picking the wrong tool. Enable per project only what it needs — that's the
whole reason this is a catalog and not one big global config.

## Presets
| File | Servers | Keys needed | Use for |
|------|---------|-------------|---------|
| `starter.mcp.json` | context7, playwright, sequential-thinking | none | sensible default for any project |
| `vibe.mcp.json` | context7, playwright, github, supabase | GitHub PAT, Supabase token | full-stack web (the `vibe` template) |
| `full.mcp.json` | all 12 below | various | copy, then DELETE what you don't need |

## The full catalog (`full.mcp.json`)
**No key (run instantly):**
- **context7** — current, version-specific library docs. Kills hallucinated APIs.
- **playwright** — drives a real browser for E2E tests / web automation.
- **sequential-thinking** — structured step-by-step reasoning for hard problems.
- **memory** — knowledge-graph memory across sessions (overlaps claude-mem — pick ONE).
- **filesystem** — extra FS access scoped to a root (edit the path before use).
- **deepwiki** — Q&A over any public GitHub repo's docs.

**Needs a key / config (`[key]`):**
- **github** — GitHub's **official remote** server `https://api.githubcopilot.com/mcp/`. The old
  npm `@modelcontextprotocol/server-github` is **deprecated**; use the remote (PAT) or Docker
  (`ghcr.io/github/github-mcp-server`). → `GITHUB_PERSONAL_ACCESS_TOKEN`
- **supabase** — tables / config / SQL / edge functions, `--read-only` by default (drop it for
  writes). → `SUPABASE_ACCESS_TOKEN`
- **postgres** — read-only schema inspection + SELECTs. → `DATABASE_URL`. For writes / health
  checks / index tuning use *Postgres MCP Pro* (`uvx postgres-mcp`).
- **n8n** — build / inspect n8n workflows; works in docs-only mode without a key. →
  `N8N_API_URL`, `N8N_API_KEY`
- **exa** — hosted web/code search. → Exa account/key
- **brave-search** — privacy-friendly web search. → `BRAVE_API_KEY` (free tier available)

## How to enable in a project
Option A — preset file (project-scoped, versionable):
```powershell
Copy-Item library/mcp/vibe.mcp.json projects/<name>/.mcp.json
# or via the bootstrap script:
./scripts/new-project.ps1 -Name app -Mcp starter   # -Mcp: starter | vibe | full
```
Option B — CLI (Windows syntax, note the `cmd /c`):
```powershell
claude mcp add context7 -- cmd /c npx -y @upstash/context7-mcp
```
Verify with `claude mcp list` (or `/mcp` inside a session).

## Globally active on this machine
The keyless starter trio (**context7 + playwright + sequential-thinking**) is registered at
**user scope** (`claude mcp add --scope user`), so it's live in every project without copying a
file. Remove with `claude mcp remove <name> -s user` for strict per-project control instead.
User-scope config lives in `~/.claude.json`, which **does not sync via git** — re-add it on each
machine (one-time, like the rest of `~/.claude/`).

## Windows notes
- Every stdio config uses `"command": "cmd", "args": ["/c","npx",...]`. Bare `npx` frequently
  fails to launch MCP servers on Windows in Claude Code — the `cmd /c` wrapper fixes that.
- **Smart App Control**: if enforced (Windows 11), it blocks *unsigned* standalone `.exe`s.
  npx/node/python-based MCP servers are fine (they run via signed runtimes); a raw unsigned tool
  like ffmpeg gets blocked. See the root `LESSONS.md` entry.

## Keys & secrets
Set every `[key]` value as an environment variable — never commit it. The `${VAR}` placeholders
in the JSON are expanded by Claude Code from your environment.

Package versions are unpinned (`@latest` via `npx -y`) so they stay current; pin one (e.g.
`@upstash/context7-mcp@3.1.0`) if you need reproducibility.
