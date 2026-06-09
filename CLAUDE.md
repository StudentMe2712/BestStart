# Root — Claude Code skeleton (каркас)

This directory is a **framework / catalog**, not an app. It holds a curated library of
Claude Code tooling merged from several upstream projects, plus a `projects/` workspace.
Each project copies ONLY the tools it needs out of `library/` into its own
`projects/<name>/.claude/`. Projects are self-contained; this root is the source of truth.

This file is loaded automatically for any project nested under this folder, so the rules
below apply everywhere unless a project's own `CLAUDE.md` overrides them.

## Layout
```
start/
├── CLAUDE.md            # this file — philosophy + gate + lessons protocol
├── LESSONS.md           # GLOBAL lessons learned (all projects)
├── README.md            # human overview, catalog, attribution
├── library/             # the tool catalog (copy FROM here)
│   ├── agents/   {ecc, gsd, awesome}
│   ├── skills/   {ecc, superpowers, karpathy, alireza}
│   ├── commands/ {core, ecc, gsd}     # "core" = start-task / add-tools / lesson
│   ├── hooks/    {ecc, gsd, superpowers}
│   ├── rules/    {ecc, karpathy, best-practice}
│   ├── mcp/      {starter.mcp.json, full.mcp.json, README}
│   ├── memory/   {claude-mem}
│   ├── templates/{LESSONS.template.md}
│   └── CATALOG.md
├── projects/
│   ├── _templates/      # starter codebases (e.g. vibe full-stack)
│   └── <your projects>  # each has .claude/ + CLAUDE.md + LESSONS.md
├── scripts/
│   ├── new-project.ps1  # scaffold a project + copy chosen tools
│   └── add-tools.ps1    # add more tools to an existing project
└── _sources/            # raw upstream clones (provenance; gitignored, deletable)
```

## How to start a project
```powershell
./scripts/new-project.ps1 -List                          # browse the catalog
./scripts/new-project.ps1 -Name myapp -Preset lean       # minimal, add per task
./scripts/new-project.ps1 -Name shop -Template vibe -Mcp starter -Preset lean
```

## ⛔ Tool-selection gate (MANDATORY before building anything)
When I give you a new task or paste a prompt (mine or from another AI) inside a project,
**do not start coding immediately.** First run the gate (also available as `/start-task`):

1. Find the skeleton root (nearest ancestor with a `library/` folder) and read
   `library/CATALOG.md` + `library/mcp/README.md`.
2. Understand the task in 1–2 sentences; ask if it's truly ambiguous.
3. Propose the BEST-matching tools — a short grouped numbered list, **max ~7 total**
   (agents / skills / commands / MCP), each as `bucket/name — one-line reason`. Prefer ONE
   tool per function. Flag which are already in `.claude/`.
4. Let me pick (subset / all / none).
5. Install my picks via `scripts/add-tools.ps1` (and copy the chosen MCP entry into `.mcp.json`).
6. **Only then** start development, using the installed tools.

Skip the gate only if I explicitly say "skip tools" or the needed tools are already present.

## 📓 Learning from mistakes (LESSONS.md)
- At the start of work, read this project's `LESSONS.md` and the global `LESSONS.md` (root).
- After resolving a **non-obvious** bug or a wrong approach, append an entry with `/lesson`
  (Problem / Root cause / Fix / Rule / Scope). Put cross-project lessons in the root `LESSONS.md`.
- Don't log trivial/one-off issues. Apply existing lessons proactively.

## Conflict policy (how the merge was done)
Everything is **namespaced by source bucket**, so tools never collide. GSD is `gsd-*` prefixed.
True duplicates removed: agents `code-reviewer` & `seo-specialist` (kept ECC); 5 skills from
`alireza` that duplicated ECC by name. When a project enables overlapping tools, prefer ONE
per function.

## Baseline working philosophy (Karpathy guidelines — see library/rules/karpathy/)
1. **Think before coding** — surface trade-offs and ask when ambiguous.
2. **Simplicity first** — minimum code, no speculative features or abstractions.
3. **Surgical changes** — touch only what's needed; don't "improve" unrelated code.
4. **Goal-driven** — turn instructions into goals with a verification step.

A project's own `projects/<name>/CLAUDE.md` takes precedence over this file.
