---
description: Tool-selection gate — before building, propose matching tools from the root library and let me pick.
argument-hint: <paste your task / prompt here>
---

You are about to start a new task in this project. **Do NOT write any code yet.**
The task (may come from me directly or pasted from another AI):

$ARGUMENTS

If the above is empty, use the most recent task I described in this conversation.

Follow this gate exactly:

1. **Locate the skeleton root** — the nearest ancestor directory that contains a `library/`
   folder (e.g. `C:\Users\Mila\Desktop\start`). Read `library/CATALOG.md` and
   `library/mcp/README.md` from there.

2. **Understand the task** in 1–2 sentences. If it's genuinely ambiguous, ask first.

3. **Propose matching tools.** Scan the catalog and pick the BEST candidates that fit THIS
   task. Present a short, grouped, numbered list — **max ~7 items total** — like:
   ```
   Recommended tools for this task:
   Agents:   1) ecc/architect — system design   2) awesome/<x> — ...
   Skills:   3) superpowers/test-driven-development — ...   4) ecc/<x> — ...
   Commands: 5) gsd/<x> — spec-driven flow
   MCP:      6) context7 — live docs for <lib>   7) playwright — E2E
   ```
   For each: `bucket/name — one-line reason`. Prefer ONE tool per function (don't propose
   ECC + superpowers for the same job). Note anything already installed in this project's
   `.claude/`.

4. **Ask me to choose** which to install (I may pick a subset, all, or none).

5. **Install my picks** by running, from the skeleton root:
   ```powershell
   ./scripts/add-tools.ps1 -Project <thisProjectName> -Agents <...> -Skills <...> -Commands <...> -Rules <...>
   ```
   For MCP picks, copy the relevant entry into this project's `.mcp.json` (see
   `library/mcp/`), or tell me the `claude mcp add` command to run.

6. **Only now begin development.** Use the installed skills/agents. When you finish or hit a
   non-obvious mistake, log it with `/lesson` (see `LESSONS.md`).
