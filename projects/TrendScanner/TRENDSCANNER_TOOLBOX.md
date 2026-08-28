# Полный каталог MCP-серверов, Skills и инструментов: TrendScanner

Данный документ содержит исчерпывающий список инструментов для автономного агента (Antigravity / Claude) при разработке сервиса TrendScanner (FastAPI + SQLite + Docker + Groq + React).

---

## 1. Топовые MCP-серверы (Model Context Protocol)

### Категория А: Базы данных и Файловая система
1. **`@modelcontextprotocol/server-sqlite`** (Официальный)
   * **Назначение:** Прямой доступ агента к `trendscanner.db`. Позволяет выполнять DDL/DML запросы, инспектировать схему, проверять записи после парсинга.
   * **Команда запуска:** `uvx mcp-server-sqlite --db-path ./backend/data/trendscanner.db`
2. **`@modelcontextprotocol/server-filesystem`** (Официальный)
   * **Назначение:** Изолированное чтение и запись файлов проекта с контролем прав доступа.
   * **Команда запуска:** `npx -y @modelcontextprotocol/server-filesystem <PROJECT_PATH>`

### Категория Б: Веб-скрапинг и Браузер
3. **`@microsoft/playwright-mcp`** (Официальный от Microsoft)
   * **Назначение:** Управление headless-браузером. Агент может открывать динамические SPA-сайты, преодолевать простые защиты, кликать по пагинации и инспектировать DOM-селекторы.
   * **Команда запуска:** `npx -y @microsoft/playwright-mcp`
4. **`@modelcontextprotocol/server-fetch`** (Официальный)
   * **Назначение:** Быстрое скачивание HTML/RSS и конвертация в чистый Markdown для парсинга без накладных расходов браузера.
   * **Команда запуска:** `uvx mcp-server-fetch`
5. **`puppeteer-mcp`**
   * **Назначение:** Альтернатива Playwright для легкого снятия скриншотов фронтенда и проверки рендера компонентов.
   * **Команда запуска:** `npx -y @modelcontextprotocol/server-puppeteer`

### Категория В: DevOps, Терминал и Системный контроль
6. **`mcp-server-commands` / `terminal-mcp`**
   * **Назначение:** Выполнение консольных команд (`docker compose up`, `pytest`, `npm run build`, `curl`).
   * **Команда запуска:** Запуск локального binary/node процесса с правами выполнения.
7. **`docker-mcp`**
   * **Назначение:** Специализированное управление контейнерами: просмотр логов FastAPI-бэкенда, инспекция volumes, рестарт воркеров.
   * **Команда запуска:** `uvx docker-mcp`

### Категория Г: Память и Сложные рассуждения
8. **`@modelcontextprotocol/server-sequential-thinking`** (Официальный)
   * **Назначение:** Активирует пошаговое логическое планирование для архитектурных решений (пайплайны, обработка ошибок 429).
   * **Команда запуска:** `npx -y @modelcontextprotocol/server-sequential-thinking`
9. **`@modelcontextprotocol/server-memory`** (Официальный)
   * **Назначение:** Граф знаний для долговременной памяти агента (сохранение принятых стандартов API, структуры моделей данных).
   * **Команда запуска:** `npx -y @modelcontextprotocol/server-memory`

---

## 2. Специализированные Skills (Кастомные навыки для агента)

Skills создаются в виде отдельных папок внутри `skills/<skill_name>/SKILL.md` в корне `BestStart`:

### 1. `fastapi-clean-architecture`
* **Роль:** Стандарт написания эндпоинтов на FastAPI.
* **Правила:** Асинхронные роутеры (`async def`), валидация через Pydantic v2, строгая типизация, глобальный middleware для перехвата исключений, разделение на слой сервисов (`services/`) и контроллеров (`api/`).

### 2. `sqlite-wal-optimizer`
* **Роль:** Безопасная работа со встроенной БД SQLite в многопоточном асинхронном окружении.
* **Правила:** Обязательное включение режима `PRAGMA journal_mode=WAL;`, настройка `busy_timeout = 5000`, использование пулов подключений или контекстных менеджеров для предотвращения `database is locked`.

### 3. `groq-resilient-parser`
* **Роль:** Отказоустойчивый шлюз к Groq API.
* **Правила:** 
  * Экспоненциальная задержка при ошибках HTTP 429 (Rate Limit).
  * Санитария строкового вывода: удаление блоков markdown (` ```json `), авто-извлечение первого валидного JSON-объекта через регулярные выражения.
  * Фолбэк на дефолтную структуру при сбое парсинга.

### 4. `scraper-anti-block`
* **Роль:** Шаблоны для безопасного сбора данных.
* **Правила:** Ротация заголовков `User-Agent`, рандомизация задержек (jitter), валидация структуры входящего HTML перед парсингом, запрет на частые синхронные вызовы.

### 5. `tailwind-dashboard-ui`
* **Роль:** Проектирование строгого GUI.
* **Правила:** Плотный Data-Grid, отсутствие лишних теней и отвлекающих анимаций, контрастное цветовое кодирование бейджей риска/скора, фиксированный Topbar с индикаторами состояния.

---

## 3. Вспомогательные аддоны и утилиты разработчика (Toolbox)

| Инструмент | Назначение |
| :--- | :--- |
| **`litecli` / `DB Browser for SQLite`** | Локальная GUI/TUI утилита для быстрой проверки содержимого `trendscanner.db`. |
| **`Docker Desktop / Engine`** | Изоляция сервисов, монтирование томов (`volumes`) для персистентности SQLite. |
| **`HTTPie` / `cURL`** | Быстрое тестирование REST API эндпоинтов из консоли. |
| **`FastAPI Swagger UI`** | Доступен из коробки по адресу `http://localhost:8000/docs`. |

---

## 4. Готовый шаблон конфигурации (`mcp_config.json`)

```json
{
  "mcpServers": {
    "sqlite": {
      "command": "uvx",
      "args": ["mcp-server-sqlite", "--db-path", "projects/TrendScanner/backend/data/trendscanner.db"]
    },
    "fetch": {
      "command": "uvx",
      "args": ["mcp-server-fetch"]
    },
    "playwright": {
      "command": "npx",
      "args": ["-y", "@microsoft/playwright-mcp"]
    },
    "sequential-thinking": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"]
    },
    "memory": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-memory"]
    }
  }
}
```
