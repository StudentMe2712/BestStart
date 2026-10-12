# OmniLeadSniper (TgOrderSniper) — Gemini CLI & Antigravity Rules

Project-specific guidelines for Gemini CLI & Antigravity. This file extends the
baseline standards in the root `GEMINI.md`.

**OmniLeadSniper** — асинхронный сервис реального времени для перехвата, скоринга и доставки коммерческих заказов из Telegram и веб-бирж (Хабр Фриланс, Kwork).

---

## 🏗 Архитектурные правила проекта

1. **Компоненты ядра (`backend/`)**:
   - `listener/`: Telethon MTProto клиент + фоновый глобальный поиск Telegram.
   - `scrapers/`: Фоновый планировщик и парсеры веб-бирж (Habr Freelance, Kwork via Playwright).
   - `filter/`: Privacy Gatekeeper, коммерческие регулярные выражения, Groq AI классификатор.
   - `db/`: Персистентная модель `ProcessedLead` в SQLite (`data/sniper.db`).
   - `core/`: Pydantic Settings конфигурация (`config.py`) и LRU/TTL дедупликация (`dedup.py`).
   - `bot/`: Асинхронный Telegram Bot API диспетчер сообщений (`dispatcher.py`).
2. **Безопасность и приватность**:
   - Никаких перехватов личных сообщений (`is_private=True`) или собственных исходящих (`out=True`).
   - Не логировать токены и сессионные ключи.
3. **Строгая верификация**:
   - Любое изменение сопровождается запуском `pytest`.
   - Проверка линтером: `ruff check .`.

---

## 📊 Session Context Tracking (MANDATORY IN EVERY RESPONSE)

В конце каждого своего ответа ОБЯЗАТЕЛЬНО выводи визуальный прогресс-бар заполнения контекста сессии в формате:
`[Контекст: ████░░░░░░░░░░░░░░░░] 20% (~40k / 200k токенов)`

Правила расчёта и отображения:
- Номинальный лимит контекста сессии: 200k токенов (200,000 токенов).
- Длина прогресс-бара: 20 блоков (`█` — 5% на блок, `░` — пусто).
- Формат: `[Контекст: ████░░░░░░░░░░░░░░░░] XX% (~YYk / 200k токенов)`.
- Если процент превышает 75%, выводи предупреждение:
  `⚠️ Рекомендуется начать новый чат.`
