# echo

Project-specific instructions for Claude Code. This file overrides / extends the
baseline philosophy in the root CLAUDE.md.

**Echo** — a personal Telegram companion that writes to its owner first: short,
role-flavored messages (questions, brainstorms, nudges, friendly check-ins) at
meaningful moments, without spam, adapting to how the owner reacts. Telegram-only,
single-user, local. See `README.md` for the full picture.

## Stack
- **Python 3.12**, **aiogram 3.x** (Telegram), **APScheduler** (probabilistic timing),
  **SQLite** (stdlib `sqlite3`, single-user → no DB server), **httpx** (LLM calls).
- Message text: hybrid chain **Groq → OpenRouter → curated templates** (always falls back).
- Layout: `app/{config,roles,templates,quality,llm,generator,db,scheduler,preferences,bot,main}.py`.
- Tests: `pytest` under `tests/`.

## Run in Docker (isolated — do this, don't install on the host)
Single long-polling service, no inbound port, no Postgres (SQLite in a mounted volume).
- Fill `.env` first (copy from `.env.example`) — it holds the bot token & API keys and is git-ignored.
- Bring it up: `docker compose up --build`   (stop: `docker compose down`).
- SQLite persists in `./data/echo.db` (host bind mount).
- Tests (no host install): `docker compose run --rm app pip install -r requirements-dev.txt && \
  docker compose run --rm app python -m pytest`  — or a throwaway venv (see README).

## Tools enabled (copied from root library into .claude/)
- agents:   ecc/python-reviewer
- skills:   superpowers, karpathy, ecc/{python-patterns, python-testing, prompt-optimizer, cost-aware-llm-pipeline, docker-patterns}
- commands: gsd
- rules:    karpathy, ecc/{python, common}
- mcp:      starter (context7 + sequential-thinking + playwright)

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
- **Single-user**: every handler is owner-guarded (`OWNER_USER_ID`). The bot ignores everyone else.
- **No spam is a hard requirement**: quiet hours + daily quota + min-gap + per-tick probability + ignore-penalty. Don't bypass these except in `/now` (manual).
- **Quality gate before every send**: length, banality, toxicity, repetition. New message types must pass it.
- Keep roles as the unit of voice; add a new archetype in `roles.py` + a template bank in `templates.py`.
- PEP 8 + type hints + small files (see `.claude/rules/ecc/python`). Karpathy rules apply: simplest thing that works, surgical changes.
