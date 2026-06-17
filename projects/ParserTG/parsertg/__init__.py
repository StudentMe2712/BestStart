"""ParserTG — Telegram channel parser + control bot (Telethon + python-telegram-bot).

Two clients, one job:
- Telethon (a *user* session) reads the next N messages from each configured channel,
  because Telegram bots cannot fetch arbitrary channel history.
- python-telegram-bot is the *control* bot: the single admin runs /parse, /feed, /status.
Storage is a single local SQLite file. No AI, no web, no multi-user.
"""

__version__ = "0.1.0"
