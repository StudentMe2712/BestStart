# PAM — Deployment (Docker) + Telegram Bot Single-Instance

Запуск стека и правила для **Telegram-бота**, который должен работать **только на одной
машине** одновременно (у владельца несколько ПК с одним репозиторием и одним токеном).

## Стек

`docker compose up -d` поднимает **только** инфраструктуру приложения:

| Сервис | Что | Порт |
|---|---|---|
| `db` | Postgres + pgvector | 5432 |
| `backend` | FastAPI (+ авто-миграции) | 8000 |
| `web` | Next.js UI | 3000 |
| `extension` | сборка расширения (Plasmo) | — |

Telegram-бот (`bot`) **НЕ** поднимается этой командой — он вынесен в compose-профиль `bot`.

```bash
docker compose up -d                  # db + backend + web + extension (БЕЗ бота)
docker compose --profile bot up -d    # то же + Telegram-бот (только на основной машине)
docker compose ps                     # что реально запущено
docker compose down                   # стоп
```

---

## Telegram Bot Deployment (важно: один инстанс)

### Почему нельзя запускать бота на двух машинах
Бот использует **Telegram long polling** (`aiogram start_polling`, без webhook). Telegram
разрешает **только один активный `getUpdates` на токен**.

### Что произойдёт при конфликте
Два инстанса с одним токеном:
- Telegram отдаёт второму **HTTP 409 Conflict** (*"terminated by other getUpdates request"*)
  → постоянный 409-шум в логах обеих машин;
- апдейты «перетягиваются»: каждое сообщение приходит **ровно один раз**, но **на случайную**
  машину → часть твоих ответов теряется/уходит не туда;
- состояние треда чата (`conversation_id`) хранится **в памяти процесса бота** → у двух
  инстансов треды разные, непрерывность `/ask`/`/new` ломается.

### Две независимые защиты
1. **Compose-профиль** (в git): `docker compose up -d` бота не поднимает. Нужен явный
   `--profile bot up -d`. → после `git pull` на второй машине обычный запуск **не может**
   случайно поднять бота.
2. **Owner-флаг** (per-machine, gitignored): даже если на второй машине явно дать
   `--profile bot up -d`, бот **не начнёт polling**, пока в её `backend/.env` не стоит
   `TELEGRAM_BOT_OWNER=true`. Контейнер просто простаивает (idle), без `getUpdates`.
   Флаг живёт в `.env` (в `.gitignore`), поэтому `git pull` его не переносит между машинами.

> Машина уникально опознаётся своим `backend/.env`. На хостнейм/IP не завязываемся: внутри
> контейнера виден не хост, а сам контейнер, а IP меняется (DHCP/VPN). Явный per-machine
> флаг надёжнее.

### Как запускать

**Основной ПК (этот) — с ботом:**
```bash
# backend/.env: TELEGRAM_BOT_OWNER=true  (плюс токен и TELEGRAM_ALLOWED_USER_ID)
docker compose --profile bot up -d
```

**Второй ПК — без бота:**
```bash
# backend/.env: TELEGRAM_BOT_OWNER=false (или не задан)
docker compose up -d
```
Остальной стек (backend/web/db) на второй машине работает независимо и ни с чем не
конфликтует — единственный «один на токен» компонент это поллер бота.

**Передать бота на другую машину:** на старой — `docker compose stop bot` (и/или
`TELEGRAM_BOT_OWNER=false`), на новой — выставить `TELEGRAM_BOT_OWNER=true` и
`docker compose --profile bot up -d`.

---

## Bootstrap новой машины

```bash
# 1) код
git pull                # или git clone … && cd PAM

# 2) секреты (per-machine, не в git)
cp .env.example backend/.env
#   впиши: DATABASE_URL не трогай (локальный pg в compose), GROQ_API_KEY,
#   TELEGRAM_BOT_TOKEN, TELEGRAM_ALLOWED_USER_ID.
#   TELEGRAM_BOT_OWNER оставь false — кроме ОСНОВНОЙ машины.

# 3) стек без бота
docker compose up -d
#   web → http://localhost:3000, backend → http://localhost:8000/docs

# 4) Telegram-бот — ТОЛЬКО на основной машине
#   backend/.env: TELEGRAM_BOT_OWNER=true
docker compose --profile bot up -d
```

---

## Проверка single-instance

```bash
docker compose config --services         # перечислит ВСЕ сервисы (включая bot из профиля)
docker compose up -d && docker compose ps # должно подняться db/backend/web/extension, БЕЗ pam-bot
docker compose --profile bot up -d && docker compose ps   # добавится pam-bot
docker compose logs bot | tail            # owner=true → "long polling started"; иначе → "idle"
```

---

## Заметка на будущее (не реализовано): conversation_id на стороне backend

Сейчас «текущий тред» Telegram-чата (`_threads: dict[uid -> conversation_id]`) живёт в
памяти процесса бота (`telegram_bot.py`). Последствия: при рестарте контейнера `bot` тред
сбрасывается (следующий `/ask` начинает новый), и тред не переносится между машинами.

Предложение (отдельной задачей): хранить «активный тред на пользователя» на backend, без
новой большой подсистемы:
- лёгкая таблица `telegram_threads(user_id PK, conversation_id FK→conversations, updated_at)`
  **или** переиспользовать `conversations` (например, последняя `source='pam'` беседа с
  меткой `telegram` в `raw_json`/external_id);
- бот при `/ask` спрашивает у backend активный `conversation_id` (а не держит в памяти),
  `/new` — сбрасывает запись.
Эффект: тред переживает рестарт бота и единообразен с веб-сайдбаром. Это не отменяет
single-instance (Telegram всё равно один поллер), но убирает потерю контекста при рестарте.
