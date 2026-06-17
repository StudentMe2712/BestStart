# ParserTG

A personal Telegram **channel parser + control bot**. One administrator, local SQLite,
no AI, no web UI, no multi-user. You drive it from Telegram with three commands.

## Why two clients

A Telegram **bot cannot read arbitrary channel history** (a platform restriction). So
ParserTG runs two clients that share one SQLite database:

| Piece | Library | Job |
|-------|---------|-----|
| **Control bot** | `python-telegram-bot` | Receives `/parse`, `/feed`, `/status` from you (the admin) |
| **Parser** | `Telethon` (a **user** account) | Actually reads the next N messages from each channel |

The control bot uses your **bot token**. The parser uses **your own account**
(`api_id` / `api_hash` + a one-time phone login). They run in the same process.

## Commands

| Command | What it does |
|---------|--------------|
| `/parse N` | For **each** configured channel, fetch the next up-to-`N` messages after its cursor, save them, and advance the cursor. `N` defaults to `PARSE_DEFAULT_N` if omitted. |
| `/feed` | Show the last `FEED_LIMIT` saved records (channel · date · text · link). |
| `/status` | Channel count, message count, and the last processed message. |

### How `/parse N` advances

Each channel has a cursor `last_message_id`. `/parse N` returns the **next** N messages
*after* that cursor (oldest first) and moves the cursor forward. A brand-new channel
starts at `0`, so the first `/parse` begins at the channel's **oldest** messages and each
call walks forward. (Want to start near the present instead? Set that channel's
`last_message_id` once, or change `iter_messages` in `parsertg/parser.py`.)

## What's stored

- **channels**: `username`, `title`, `last_message_id` (the per-channel cursor)
- **messages**: `channel`, `date`, `text`, `link` (`https://t.me/<channel>/<id>`)

## Setup (local, no Docker)

```powershell
cd projects\ParserTG
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
```

1. **Config** — `.env` already has your bot token and admin id. Fill in the parser account:
   - Go to <https://my.telegram.org> → *API development tools* → copy `api_id` + `api_hash`.
   - Put them in `.env` as `TELETHON_API_ID` / `TELETHON_API_HASH`.
   - Set `CHANNELS=` to the channels you want (e.g. `@durov,@telegram`).
2. **One-time login** (creates the Telethon session file):
   ```powershell
   python -m parsertg.login
   ```
   Enter your phone, the code Telegram sends, and your 2FA password if you have one.
3. **Run the bot:**
   ```powershell
   python -m parsertg
   ```
4. In Telegram, message your bot: `/status`, then `/parse 50`, then `/feed`.

## Tests

```powershell
pip install -r requirements-dev.txt
python -m pytest
```

The DB and formatting layers are covered without any network access.

## Notes

- The `.session` file is a logged-in session — treat it like a password. It's git-ignored.
- For **private** channels, your account must be a member; use the numeric id in `CHANNELS`.
- Secrets live only in `.env` (git-ignored). `.env.example` documents the shape.
