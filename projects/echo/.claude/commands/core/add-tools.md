---
description: Install more tools from the root library into this project (wrapper for add-tools.ps1).
argument-hint: e.g. skills=superpowers,ecc/api-design agents=ecc/architect mcp=starter
---

Install the requested tools into THIS project by running `scripts/add-tools.ps1` from the
skeleton root (the nearest ancestor containing a `library/` folder).

Request: $ARGUMENTS

Steps:
1. Determine this project's name (its folder under `projects/`).
2. Translate the request into the script's parameters (`-Agents -Skills -Commands -Rules -Hooks -Mcp`).
   Items may be whole buckets (`ecc`) or specific paths (`alireza/engineering/code-tour`).
3. Run it, e.g.:
   ```powershell
   ./scripts/add-tools.ps1 -Project <name> -Skills superpowers/test-driven-development -Agents ecc/architect -Mcp starter
   ```
4. Report what landed in `.claude/` and remind me to set any MCP API keys.

If the request is vague, run `/start-task` instead to get recommendations first.
