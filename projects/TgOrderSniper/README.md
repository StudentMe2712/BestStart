# TgOrderSniper 🎯

**Event-Driven MTProto Listener + Real-Time Heuristic Filter + Telegram Bot API Dispatcher**

Высокопроизводительный асинхронный сервис для мгновенного перехвата, фильтрации и интерактивной доставки коммерческих IT-заказов и лидов из сотен Telegram-каналов и чатов в закрытую группу команды отклика с нулевой задержкой (0s latency).

---

## ⚡ Архитектура: Гибридная схема (MTProto + Bot API)

```mermaid
flowchart LR
    Channels["Мониторимые чаты\n(IT, Фриланс, Заказы)"] -->|MTProto Push Stream\n(0s latency)| Telethon["Telethon Listener\n(Userbot Read-Only)"]
    Telethon --> Dedup["MessageDedup\n(LRU + TTL Cache)"]
    Dedup --> Filter["FilterEngine\n(Gatekeeper + Triggers + Budget)"]
    Filter --> Dispatcher["TelegramBotDispatcher\n(Async HTTP via httpx)"]
    Dispatcher -->|Bot API sendMessage\n+ Inline Buttons| TargetChat["Целевой чат команды\n(Мгновенный отклик)"]
```

### Ключевые преимущества:
- **0 секунд задержки**: Никакого периодического опроса (Polling раз в 5-10 минут). Долгоживущее TCP/MTProto соединение Telethon принимает push-события мгновенно в момент публикации на серверах Telegram.
- **100% Антибан защита**: Userbot-аккаунт работает в строго изолированном режиме `read-only listener` без отправки сообщений в каналы, что исключает FloodWait и риск блокировки аккаунта.
- **Интерактивный UX в 1 клик**: Доставка выполняется официальным Telegram-ботом, который формирует карточки с кнопками `[🔍 Открыть пост]` и `[💬 Написать заказчику]` (deep-links).
- **In-Memory Дедупликация**: LRU/TTL кольцевой кэш отсекает повторные срабатывания при пересылке и кросс-постинге одинаковых текстов между каналами.

Подробнее о выборе архитектуры см. в [ADR-001: Гибридная архитектура MTProto + Bot API](specs/ADR-001-hybrid-mtproto-bot-architecture.md) и [Функциональной спецификации](specs/spec.md).

---

## 🚀 Быстрый старт

### 1. Требования
- Python 3.10+
- Аккаунт Telegram для Userbot-сессии
- Telegram-бот от [@BotFather](https://t.me/BotFather)

### 2. Установка зависимостей
```bash
# Клонирование / переход в каталог проекта
cd projects/TgOrderSniper

# Создание и активация виртуального окружения
python -m venv .venv
# Windows:
.venv\Scripts\activate
# Linux/macOS:
source .venv/bin/activate

# Установка зависимостей
pip install -r requirements.txt
```

### 3. Конфигурация окружения (.env)
Скопируйте пример файла конфигурации:
```bash
cp .env.example .env
```

Заполните переменные в `.env`:
```ini
# 1. Telegram MTProto Userbot Client (https://my.telegram.org)
TG_API_ID=1234567
TG_API_HASH=0123456789abcdef0123456789abcdef
TG_SESSION_NAME=order_sniper
# Опционально: StringSession для stateless/Docker развертывания
# TG_STRING_SESSION=

# 2. Telegram Bot API (Dispatcher)
TG_BOT_TOKEN=1234567890:ABCdefGhIJKlmNoPQRsTUVwxyZ
TARGET_CHAT_ID=-1001234567890

# 3. Список отслеживаемых каналов и чатов (через запятую)
CHANNELS_TO_WATCH=@freelance_chat, @python_jobs, -1009876543210

# 4. Настройки кэша дедупликации
DEDUP_CACHE_SIZE=5000
DEDUP_TTL_SECONDS=86400
```

---

## 🔑 Инструкция по получению ключей

### 1. Получение `TG_API_ID` и `TG_API_HASH`:
1. Перейдите на официальный портал разработчиков Telegram: [https://my.telegram.org](https://my.telegram.org).
2. Авторизуйтесь под своим номером телефона с кодом из Telegram.
3. Перейдите в раздел **API development tools**.
4. Создайте новое приложение (любое имя и короткий заголовок, платформа `Desktop` или `Other`).
5. Скопируйте поля `api_id` (целое число) и `api_hash` (32-значная hex-строка) в файл `.env`.

### 2. Получение `TG_STRING_SESSION` (для облачного или Docker развертывания):
Для stateless-развертывания без сохранения SQLite `.session` файлов на диске запустите однострочник:
```bash
python -c "from telethon.sync import TelegramClient; from telethon.sessions import StringSession; api_id = int(input('API ID: ')); api_hash = input('API HASH: '); client = TelegramClient(StringSession(), api_id, api_hash); client.start(); print('\nYOUR TG_STRING_SESSION:\n' + client.session.save())"
```
Скопируйте выведенную строку в переменную `TG_STRING_SESSION` в `.env`.

### 3. Получение `TG_BOT_TOKEN` и `TARGET_CHAT_ID`:
1. Напишите боту [@BotFather](https://t.me/BotFather), отправьте команду `/newbot` и сохраните полученный `token`.
2. Создайте закрытую группу или канал для команды, добавьте туда вашего созданного бота.
3. Чтобы узнать ID чата (`TARGET_CHAT_ID`), перешлите любое сообщение из группы боту [@userinfobot](https://t.me/userinfobot) или [@JsonDumpBot](https://t.me/JsonDumpBot) (для супергрупп ID обычно начинается с `-100...`).

---

## 🛠 Запуск и тестирование

### Запуск сервиса
```bash
python -m backend.main
```

### Запуск модульных тестов
```bash
pytest tests -v
```

### Проверка стиля и линтера (Ruff)
```bash
ruff check backend tests
```

---

## 📂 Структура проекта

```
TgOrderSniper/
├── backend/
│   ├── bot/
│   │   ├── __init__.py
│   │   └── dispatcher.py      # HTTPX асинхронный диспетчер Telegram Bot API
│   ├── core/
│   │   ├── __init__.py
│   │   ├── config.py          # Pydantic Settings конфигурация
│   │   └── dedup.py           # LRU + TTL дедупликация (ID + text hash)
│   ├── filter/
│   │   ├── __init__.py
│   │   └── engine.py          # Strict Gatekeeper, коммерческие триггеры, бюджет
│   ├── listener/
│   │   ├── __init__.py
│   │   └── client.py          # Telethon MTProto event-driven клиент
│   ├── __init__.py
│   └── main.py                # Точка входа, пайплайн и graceful shutdown
├── specs/
│   ├── ADR-001-hybrid-mtproto-bot-architecture.md # Архитектурное решение ADR-001
│   └── spec.md                # Спецификация и требования Spec-Kit
├── tests/
│   ├── test_config.py
│   ├── test_dedup.py
│   ├── test_dispatcher.py     # Тесты форматирования карточек, кнопок и ретраев
│   ├── test_filter.py
│   └── test_listener.py
├── .env.example
├── README.md
├── requirements.txt
├── sync.sh                    # Git синхронизация проекта
└── tg_order_sniper_spec.md    # Трекер выполнения этапов
```

---

## 📜 Лицензия
Внутренний проект BestStart / TgOrderSniper. Все права защищены.
