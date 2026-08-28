---
title: "FastAPI REST API Reference & Endpoints"
tags: [architecture, backend, api, fastapi, endpoints, rest]
created: 2026-08-27
updated: 2026-08-27
status: active
---

# 🔌 REST API Reference

> В данном документе приведена спецификация REST API бэкенда **TrendScanner** на базе FastAPI. Все маршруты используют префикс `/api/v1` и строгую валидацию через Pydantic v2.

Связанные разделы: [[Index]] | [[System_Pipeline]] | [[Database_Schema]] | [[Design_System_and_UI]]

Интерактивная документация Swagger доступна по адресу: `http://localhost:8000/docs`.

---

## 📌 Сводная таблица эндпоинтов

| Метод | Путь | Тег | Назначение |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/trends` | `Trends` | Получение списка трендов с пагинацией, фильтрами и табами Inbox Zero. |
| `GET` | `/api/v1/trends/{trend_id}` | `Trends` | Получение детальной информации по конкретному тренду. |
| `POST` | `/api/v1/trends/{trend_id}/report` | `Trends` | Генерация или получение закэшированного аналитического Deep Report. |
| `PATCH` / `PUT` | `/api/v1/trends/{trend_id}/like` | `Trends` | Переключение или явная установка статуса лайка (Избранное). |
| `PUT` | `/api/v1/trends/{trend_id}/review` | `Trends` | Пометка тренда как просмотренного / перемещение в архив. |
| `DELETE` | `/api/v1/trends/{trend_id}` | `Trends` | Удаление записи тренда из базы данных. |
| `GET` | `/api/v1/sources` | `Sources` | Список всех настроенных источников сбора данных. |
| `POST` | `/api/v1/sources` | `Sources` | Регистрация нового источника в системе. |
| `PUT` | `/api/v1/sources/{source_id}` | `Sources` | Обновление конфигурации или переключение активности источника. |
| `DELETE` | `/api/v1/sources/{source_id}` | `Sources` | Удаление источника с каскадным удалением его трендов. |
| `POST` | `/api/v1/scan/manual` | `Scan` | Принудительный ручной запуск цикла сбора и ИИ-анализа. |
| `GET` | `/api/v1/system/status` | `System` | Состояние системы, счетчики, время последнего и следующего скана. |

---

## 📈 1. Тренды (Trends Endpoints)

### `GET /api/v1/trends`
Возвращает массив объектов трендов для грид-таблицы с поддержкой аналитической фильтрации.

#### Query Параметры:
- `skip` (`int`, default: `0`): Смещение для пагинации.
- `limit` (`int`, default: `50`, min: `1`, max: `200`): Размер страницы.
- `min_score` (`int`, optional, `1-10`): Минимальный скор жизнеспособности ИИ.
- `max_scam` (`int`, optional, `0-100`): Максимально допустимый риск скама (%).
- `status` (`str`, optional, `new` или `reviewed`): Фильтр по статусу прочтения.
- `source_id` (`int`, optional): Фильтр по конкретному источнику.
- `only_trends` (`bool`, optional): Выводить только подтвержденные тренды (`is_trend = 1`).
- `tab` (`str`, optional, `inbox` | `liked` | `all`): Режим Inbox Zero:
  - `inbox` (по умолчанию) — только нелайкнутые тренды (`is_liked = 0`).
  - `liked` — только избранные тренды (`is_liked = 1`).
  - `all` — все записи без фильтрации по лайкам.
- `is_liked` (`bool`, optional): Явный фильтр по статусу избранного.

#### Пример ответа (`200 OK`):
```json
[
  {
    "id": 42,
    "source_id": 1,
    "original_text": "Micro-SaaS tool for automated invoice reconciliation...",
    "parsed_date": "2026-08-27T10:00:00Z",
    "is_trend": true,
    "trend_name": "Автоматизация сверки инвойсов для клиник",
    "ai_score": 9,
    "scam_probability": 8,
    "ai_summary": "B2B сервис автоматической сверки счетов со страховыми компаниями. Высокий LTV, готовность платить.",
    "source_url": "https://www.producthunt.com/posts/med-reconcile",
    "is_reviewed": false,
    "ai_status": "processed",
    "mention_count": 3,
    "detailed_report": null,
    "is_liked": 0,
    "source_name": "Product Hunt Trending (SPA)",
    "source_type": "playwright_spa"
  }
]
```

---

### `POST /api/v1/trends/{trend_id}/report`
Генерирует глубокий венчурный аналитический отчет (Deep Report) на русском языке через Groq LLM или возвращает закэшированный отчет из SQLite.

#### Пример ответа (`200 OK`):
```json
{
  "trend_id": 42,
  "trend_name": "Автоматизация сверки инвойсов для клиник",
  "detailed_report": "### 🎯 1. Суть и ценность продукта\n- Автоматизация сверки медицинских счетов...\n\n### 🚀 4. План запуска MVP за 2 недели\n- FastAPI бэкенд + интеграция со Stripe..."
}
```

---

### `PATCH /api/v1/trends/{trend_id}/like` (и `PUT`)
Переключает статус лайка (toggle) или устанавливает его явно при передаче JSON тела `{"is_liked": true}`.

#### Request Body (опционально):
```json
{
  "is_liked": true
}
```

#### Пример ответа (`200 OK`):
```json
{
  "trend_id": 42,
  "is_liked": 1,
  "updated": true
}
```

---

### `PUT /api/v1/trends/{trend_id}/review`
Помечает тренд как просмотренный (`is_reviewed = true`) для перемещения в архив.

#### Request Body:
```json
{
  "is_reviewed": true
}
```

---

## 📡 2. Источники (Sources Endpoints)

### `GET /api/v1/sources`
Возвращает список всех 21 настроенных источников с информацией о последнем сканировании.

#### Query Параметры:
- `active_only` (`bool`, default: `false`): Фильтровать только активные источники.

#### Пример ответа (`200 OK`):
```json
[
  {
    "id": 1,
    "name": "Product Hunt Trending (SPA)",
    "url": "https://www.producthunt.com/",
    "source_type": "playwright_spa",
    "is_active": 1,
    "last_scanned": "2026-08-27T09:45:12Z",
    "trends_count": 18
  }
]
```

---

### `POST /api/v1/sources`
Добавляет новый источник для регулярного сканирования.

#### Request Body (`201 Created`):
```json
{
  "name": "Reddit /r/SoloBiz",
  "url": "https://www.reddit.com/r/SoloBiz/hot.json?limit=25",
  "source_type": "reddit",
  "is_active": 1
}
```

---

## ⚡ 3. Сканирование и Системный статус

### `POST /api/v1/scan/manual`
Запускает немедленный полный цикл сбора данных по всем активным источникам и обрабатывает первую пачку через Groq.

#### Пример ответа (`200 OK`):
```json
{
  "status": "completed",
  "scanned_sources": 21,
  "new_trends_found": 8,
  "processed_ai": 3,
  "pending_ai_count": 5,
  "errors": []
}
```

---

### `GET /api/v1/system/status`
Предоставляет полные метаданные для Topbar: статус системы, планировщика, очереди ИИ, время последнего и следующего запуска.

#### Пример ответа (`200 OK`):
```json
{
  "status": "operational",
  "scheduler": {
    "available": true,
    "running": true,
    "job_id": "radar_periodic_ingest",
    "interval_minutes": 60,
    "next_run_time": "2026-08-27T11:00:00Z",
    "pipeline_running": false
  },
  "active_sources_count": 21,
  "pending_ai_count": 0,
  "stats": {
    "total_count": 142,
    "inbox_count": 128,
    "liked_count": 14,
    "reviewed_count": 35,
    "pending_ai_count": 0
  },
  "groq_model": "llama-3.1-8b-instant",
  "last_scan_time": "2026-08-27T10:00:00Z",
  "next_scan_time": "2026-08-27T11:00:00Z"
}
```

---

## 🛡️ Коды состояния HTTP и обработка ошибок

| Код | Описание | Причина |
| :--- | :--- | :--- |
| `200 OK` | Успешный запрос | Запрос выполнен штатно, данные возвращены в теле ответа. |
| `201 Created` | Создан ресурс | Новый источник успешно зарегистрирован в базе данных. |
| `400 Bad Request` | Неверный запрос | Ошибка валидации параметров или некорректный формат входных данных. |
| `404 Not Found` | Не найдено | Запрошенный тренд (`trend_id`) или источник (`source_id`) не существует. |
| `422 Unprocessable Entity`| Ошибка Pydantic | Несоответствие структуры JSON переданной Pydantic схеме. |
| `429 Too Many Requests` | Превышен лимит Groq | Воркер автоматически включает бэкофф на 60 секунд. |
| `502 Bad Gateway` | Сбой Groq API | Не удалось связаться с Groq Cloud API или получить корректный ответ. |
