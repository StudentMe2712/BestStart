# start — a Claude Code skeleton for all your projects

A single root that bundles the best community Claude Code tooling into one **catalog**
(`library/`), plus a `projects/` workspace. Spin up a new project and pull in only the
agents / skills / commands / rules you actually need — copied into that project's local
`.claude/`, kept isolated, each project with its own `CLAUDE.md`.

## Quick start
```powershell
# from the start/ folder:
./scripts/new-project.ps1 -List                                 # browse the catalog
./scripts/new-project.ps1 -Name myapp -Preset lean -Mcp starter # light setup + 3 MCP servers
./scripts/new-project.ps1 -Name shop  -Template vibe -Preset full -Memory   # full-stack + everything
```
Then open `projects/<name>/`, edit its `CLAUDE.md`, and delete any tool you don't want
under `.claude/`.

Every new project also gets, automatically: the **core gate commands** (`/start-task`,
`/add-tools`, `/lesson`), a `LESSONS.md`, and a `CLAUDE.md` that wires both in.

## What's inside the library
| Type | Buckets | Count |
|------|---------|-------|
| agents   | `ecc` (64), `gsd` (33), `awesome` (152) | **249** |
| skills   | `ecc` (261), `alireza` (342), `superpowers` (14), `karpathy` (1) | **618** |
| commands | `core` (3), `ecc` (84), `gsd` (67) | **154** |
| hooks    | `ecc`, `gsd`, `superpowers` | per-source sets |
| rules    | `ecc` (20 languages), `karpathy`, `best-practice` (docs) | — |
| mcp      | `starter` (3 servers), `full` (8) | 2 packs |
| memory   | `claude-mem` (plugin) | 1 |

`projects/_templates/` ships **vibe** — a full-stack starter (Bun/Hono/Prisma/React/Astro/Expo).

## The workflow: gate + lessons
Two habits are baked into every project so Claude builds the right thing and learns from misses:

- **Tool-selection gate (`/start-task`).** Before writing code for a new task, Claude reads the
  catalog, proposes the ~7 best-matching tools (agents/skills/commands/MCP), and waits for you to
  pick. Your picks are installed with `scripts/add-tools.ps1` (or `/add-tools`). This keeps each
  project lean instead of dragging in all 249 agents.
- **Lessons (`/lesson`).** After a non-obvious bug or wrong turn, Claude appends a terse entry to
  the project's `LESSONS.md` (or the root one for cross-project lessons). Both are read at the
  start of every session, so the same mistake isn't repeated.

## Design decisions
- **Hybrid dedup.** Tools are namespaced by source bucket, so nothing collides at the file
  level. GSD is already `gsd-*` prefixed. The only true duplicates removed: agents
  `code-reviewer` and `seo-specialist` (ECC versions kept).
- **Copy, not symlink.** Projects own their tools — portable, no admin/dev-mode needed on
  Windows, and you can freely edit a project's copy without touching the catalog.
- **vibe is a template, not tooling.** It's an app scaffold, so it lives in `projects/_templates/`.
- **claude-mem stays a plugin.** Memory is a self-contained app; the `-Memory` flag copies it
  in, then follow its upstream README to finish install.

## Sources & attribution
Each upstream repo keeps its own license under `_sources/<repo>/`. Raw clones live in
`_sources/` (provenance + future updates; safe to delete to reclaim space).

| Bucket | Upstream |
|--------|----------|
| ecc        | https://github.com/affaan-m/everything-claude-code |
| gsd        | https://github.com/gsd-build/get-shit-done |
| superpowers| https://github.com/obra/superpowers |
| awesome    | https://github.com/VoltAgent/awesome-claude-code-subagents |
| karpathy   | https://github.com/multica-ai/andrej-karpathy-skills |
| claude-mem | https://github.com/thedotmack/claude-mem |
| best-practice | https://github.com/shanraisshan/claude-code-best-practice |
| vibe (template) | https://github.com/di-sukharev/vibe |

## Updating a source
```powershell
cd _sources/<repo>; git pull
# then re-copy the bucket(s) you changed into library/  (see scripts or do it manually)
```

See `CLAUDE.md` for the working philosophy and `library/CATALOG.md` for the full tool list.
