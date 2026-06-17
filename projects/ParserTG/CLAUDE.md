# ParserTG

Project-specific instructions for Claude Code. This file overrides / extends the
baseline philosophy in the root CLAUDE.md.

**ParserTG** — a personal Telegram **channel parser + control bot**. One administrator,
local SQLite, no AI, no web UI, no multi-user. See `README.md` for the full picture.

## Stack
- **Python 3.10+**, **Telethon** (parser, a *user* account), **python-telegram-bot 21.x**
  (control bot), **SQLite** (stdlib `sqlite3`, single-user → no DB server), `python-dotenv`.
- **Two clients, one process, one DB.** A Telegram *bot* can't read channel history, so
  parsing runs on a Telethon *user* session; the bot only takes commands from the admin.
- Layout: `parsertg/{config,db,parser,bot,login,__main__}.py`. Tests: `pytest` under `tests/`.
- Run locally (no Docker): `python -m parsertg`. One-time parser login: `python -m parsertg.login`.

## Tools enabled (copied from root library into .claude/)
- agents:   ecc/{python-reviewer, silent-failure-hunter}
- skills:   superpowers, karpathy, ecc/{python-patterns, python-testing, error-handling}
- commands: gsd, ecc/python-review
- rules:    karpathy, ecc/{python, common}
- hooks:    —
- mcp:      —

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
- **Secrets only in `.env`** (git-ignored). Never hardcode tokens/keys; `.env.example` documents the shape.
- **Single-admin**: every handler is owner-guarded (`ADMIN_USER_ID`). The bot ignores everyone else.
- **The `.session` file is a logged-in user session** — treat it like a secret (git-ignored).
- **Cursor semantics**: each channel tracks `last_message_id`; `/parse N` fetches the next up-to-N
  messages *after* it (oldest first) and advances it. Don't change this to "latest N" without asking.
- **Scope is fixed by the spec**: only `/parse`, `/feed`, `/status`. No AI, no web, no multi-user.
  Channels come from `CHANNELS` in `.env` (synced into the DB at startup) — don't add `/add` commands
  unless asked (YAGNI).
- PEP 8 + type hints + small files. Karpathy rules apply: simplest thing that works, surgical changes.
- Use parameterized SQL only (already the case in `db.py`). Escape user/Telegram text in HTML feed output.
