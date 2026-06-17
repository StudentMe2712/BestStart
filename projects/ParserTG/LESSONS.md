# Lessons learned — ParserTG

A running log of mistakes and what fixed them, so Claude (and you) don't repeat them.
Add entries with the `/lesson` command, or by hand. Newest on top.

> General lessons that apply to ALL projects also go in the root `LESSONS.md`.

## Log

<!-- entries go here, newest first, in this format:

### YYYY-MM-DD — short title
- **Problem:** what went wrong / the wrong assumption.
- **Root cause:** why it happened.
- **Fix:** what actually worked.
- **Rule:** the one-line guardrail to follow next time.
- **Scope:** this-project | all-projects
-->

### 2026-06-17 — Telethon login silently logged in as the bot
- **Problem:** `python -m parsertg.login` authorized the **bot** (`id=8957872046`) instead of the user account; parsing would then silently return nothing.
- **Root cause:** Telethon's `client.start()` default prompt is "enter your phone (or bot token)" and treats any input containing `:` as a bot token. Pasting the bot token logged in as a bot — and bots can't read channel history (`getHistory` returns empty for bots, even as channel admin).
- **Fix:** `login.py` now forces a phone prompt and, after login, checks `me.bot` — if it's a bot it `log_out()`s (deletes the session) and aborts with a clear message. `bot.py` also warns on startup and rejects `/parse` when the session is a bot.
- **Rule:** for "read channel history" you need a **user** session; never accept a bot token at the Telethon phone prompt — verify `me.bot is False` after login.
- **Scope:** all-projects (any Telethon-based parser)

