# Project Memory — Design (ADR + Data Schema + UX)

> **Статус: ТОЛЬКО ПРОЕКТ (design-only).** Код, миграции и UI по этому документу
> **не написаны** намеренно — сначала ревью дизайна, потом реализация отдельной
> фазой. Это P2-приоритет из `ORCHESTRATOR.md`. Документ — источник правды по
> *замыслу* Project Memory; при реализации он переедет в `CLAUDE.md`/код.

Дата: 2026-06-14. Автор: оркестратор PAM.

---

## Постановка (из ORCHESTRATOR.md, P2)

> **Project Memory.** Память о проектах. Проект имеет: документы, решения,
> обсуждения, задачи, извлечённые знания. Пользователь может спросить: что было
> решено; почему принято решение; какие задачи остались открытыми.

То есть нужен слой, который **группирует** разрозненную память (чаты, материалы,
факты) вокруг конкретного проекта и добавляет **структуру** поверх неё: решения,
задачи, знания — с провенансом и ответами на вопросы «что/почему/что открыто».

---

# Часть 1 — ADR (Architecture Decision Record)

## ADR-0001: Project Memory как слой scoping поверх существующей памяти

### Контекст

В PAM уже есть полный субстрат памяти:

- **Документы** → `content_sources` (+ `content_chunks` + эмбеддинги): ингест
  статья/PDF/YouTube/файл/текст, чанкование, локальные эмбеддинги, ретрив.
- **Обсуждения** → `conversations` (+ `messages`): чат с памятью, история, SSE.
- **Извлечённые факты** → `profile_facts` (+ Fact Review Queue P0): LLM-извлечение
  с анти-инъекцией и hallucination-guard (каждый факт хранит `source_excerpt` +
  `source_conversation_id`), статусы `pending_review|approved|rejected|edited`,
  гейт ретрива `ACCEPTED_FACT_STATUSES` (в память чата идут только approved+edited).
- **Пайплайн**: `indexing.py` (chunk+embed), RAG-ретрив в `chat.py`, `extraction.py`
  (LLM-извлечение по строгой схеме), observability `events`.

Сильное искушение — построить Project Memory как **параллельную** подсистему
(свои таблицы документов, своих чанков, своего чата). Это противоречит принципам
ORCHESTRATOR: «Не создавать новые сущности без необходимости», «Простота важнее
количества функций», «Минимизация технического долга», local-first.

### Решение

**Project Memory — это тонкий слой группировки и структуры поверх уже
существующей памяти, а не новое хранилище.**

Три добавления, не больше:

1. **`projects`** — лёгкая сущность-группа (id, name, description, status). Нужна
   потому, что концепции «проект» в системе нет; это и есть новая способность.
2. **Нулевой `project_id` (FK, nullable)** на **`conversations`** и
   **`content_sources`** — привязка существующих чатов и материалов к проекту
   **без дублирования хранилища**. `NULL` = «вне проекта» (текущее поведение чата
   и Лектора не меняется). Переиспользуем весь ингест/чанк/эмбеддинг/ретрив.
3. **`project_items`** — структурированное знание проекта: решения, задачи,
   открытые вопросы, заметки/знания. Одна таблица с дискриминатором `kind`,
   **переиспользует паттерн Fact Review Queue** (статусы + гейт ретрива) и
   hallucination-guard (провенанс `source_*`).

Ответы на «что решено / почему / что открыто» строятся **RAG-ом по памяти
проекта**: чанки документов и обсуждений проекта + принятые `project_items`
(блок `<project>` в промпте, по аналогии с блоком `<profile>` фактов, тот же гейт).

### Рассмотренные альтернативы

| # | Альтернатива | Почему отклонена |
|---|--------------|------------------|
| A | **Параллельное хранилище** (`project_documents`, `project_chunks`, `project_chat`) | Дублирует ингест/эмбеддинг/ретрив/чат. Максимальный тех-долг, два пайплайна одной логики. Прямо нарушает принципы 3–4. |
| B | **Отдельные таблицы `decisions` / `tasks` / `knowledge`** | Три таблицы с почти одинаковыми колонками (content, провенанс, статус ревью). `kind`-дискриминатор в одной `project_items` проще, а гейт ревью — единый. Цена: для задач нужен доп. флаг (см. ниже) — приемлемо. |
| C | **Переиспользовать `profile_facts` с `project_id`** | Семантика разная: `profile_facts` — факты *о пользователе*, `project_items` — *о проекте* (+ у задач свой жизненный цикл open/done). Смешивать субъекты в одной таблице — путаница в гейте ретрива профиля и в UI `/me`. Отдельная таблица чище. |
| D | **Только теги-строки на сущностях, без таблицы `projects`** | Нет места под описание/статус проекта, нет FK-целостности, нет каскада при удалении. Группа-сущность нужна. |

### Жизненный цикл статусов `project_items` (важная деталь)

У элементов два независимых измерения:

- **review_status** (всегда): `pending_review | approved | rejected | edited` —
  тот же гейт, что у фактов. **В контекст чата попадают только approved+edited.**
  Извлечённые LLM-ом элементы стартуют как `pending_review`; добавленные руками —
  сразу `approved` (доверенный ввод).
- **task_status** (только для `kind='task'`): `open | done` — отвечает на «какие
  задачи остались открытыми». Для не-задач `NULL`.

Два отдельных поля, а не один смешанный enum — review-гейт остаётся единообразным
с фактами, а лайфцикл задач не ломает его.

### Извлечение (reuse `extraction.py`)

Проектный экстрактор читает обсуждения/документы проекта и предлагает
decisions/tasks/questions/knowledge как `pending_review` `project_items`. Те же
гарды: внешний текст в `<…>` = данные (анти-инъекция); элемент без `source_excerpt`
отбрасывается (hallucination-guard); дедуп по `content`. Это **новый промпт/функция
рядом с `extract_facts_for_conversation`, а не новый пайплайн**.

### Ретрив в чат (reuse `chat.py`)

Когда чат привязан к проекту (`conversations.project_id` задан):

1. ретрив документов/обсуждений **скоупится** на проект (фильтр по `project_id`
   в SQL ретрива — добавочное условие, не новый путь);
2. принятые `project_items` собираются в блок `<project>` (мини-резюме: решения,
   открытые задачи, ключевые знания) — по образцу `_profile_facts()`, тот же гейт
   `review_status in (approved, edited)`.

Вопросы «что решили по X», «почему выбрали Y», «какие задачи открыты» отвечаются
из этого блока + чанков проекта.

### Последствия

**Плюсы**

- Минимум новых сущностей: 2 таблицы + 2 nullable-колонки. Никакого второго
  пайплайна.
- Полное переиспользование ингеста, эмбеддингов, RAG, review-queue, анти-инъекции,
  observability.
- Аддитивно и обратносовместимо: `project_id = NULL` → текущее поведение не
  меняется; обычный чат и Лектор работают как раньше.
- Local-first сохранён (никаких облачных БД/сервисов).
- Единый review-гейт: ничего не попадает в память проекта без проверки (P0-принцип
  распространяется на проектные знания).

**Минусы / риски**

- `project_items` с `kind`-дискриминатором — компромисс: часть колонок (`task_status`)
  значима не для всех `kind`. Принято осознанно (альтернатива B хуже).
- Скоуп-фильтры добавляются в существующие запросы ретрива — нужно аккуратно,
  чтобы не задеть непроектный путь (покрыть тестом «`project_id=NULL` → прежнее
  поведение»).
- Новый раздел продукта `/projects` — оправдан как заявленный P2, но это видимый
  рост поверхности; делать после стабилизации текущих систем.

**Не делаем сейчас (design-only).** Реализация — отдельной фазой после ревью этого
ADR и после стабилизационной фазы.

### Открытые вопросы (на ревью пользователю)

1. **Привязка чата к проекту.** v1: чат, начатый внутри воркспейса проекта,
   получает `project_id`. Нужна ли переаттрибуция *существующих* чатов в проект
   (массовое «перенести в проект»)? Предложение: стретч, не v1.
2. **Авто-привязка извлечения.** Извлекать `project_items` только по явной кнопке
   «обновить знания проекта» (как у фактов) или авто-фоном после каждого
   проектного чата (как авто-обучение фактов)? Предложение: начать с явной кнопки
   (дешевле, предсказуемее), авто — позже.
3. **Гранулярность знаний.** Хватает ли `kind ∈ {decision, task, question, note}`?
   Кандидаты на будущее: `risk`, `glossary`. Предложение: 4 вида в v1, расширяемо.
4. **Удаление проекта.** Каскад: `project_items` — `ON DELETE CASCADE`; а
   `conversations`/`content_sources` при удалении проекта — отвязывать
   (`SET NULL`, контент остаётся в общей памяти) или удалять? Предложение:
   `SET NULL` (local-first: не теряем данные пользователя молча).

---

# Часть 2 — Data Schema (проект, НЕ реализовано)

> SQLAlchemy-стиль как в `models.py`. Миграции Alembic будут отдельными
> ревизиями от текущего head `f5a6b7c8d9e0` — **в этом документе НЕ создаются.**

### Новая таблица `projects`

```python
class Project(Base):
    """Группа памяти вокруг проекта (Project Memory, P2).

    Лёгкая сущность-зонтик: к ней привязываются разговоры и материалы
    (nullable project_id), а структурное знание лежит в project_items.
    """
    __tablename__ = "projects"

    id: Mapped[uuid.UUID]   # PK, default uuid4
    name: Mapped[str]                       # String(200), NOT NULL
    description: Mapped[str | None]         # Text, nullable
    status: Mapped[str]                     # String(16), active|archived, default active
    created_at: Mapped[datetime]            # server_default now()
    updated_at: Mapped[datetime]            # server_default now(), onupdate now()

    __table_args__ = (Index("ix_projects_status", "status"),)
```

Константы (рядом, как `COURSE_*` / `FACT_*`):
```python
PROJECT_ACTIVE = "active"
PROJECT_ARCHIVED = "archived"
PROJECT_STATUSES = (PROJECT_ACTIVE, PROJECT_ARCHIVED)
```

### Новая таблица `project_items`

```python
# kind элементов проектной памяти
ITEM_DECISION = "decision"
ITEM_TASK = "task"
ITEM_QUESTION = "question"
ITEM_NOTE = "note"          # извлечённое знание / заметка
ITEM_KINDS = (ITEM_DECISION, ITEM_TASK, ITEM_QUESTION, ITEM_NOTE)

# review-гейт — ТОТ ЖЕ, что у profile_facts (переиспользуем константы из models.py)
#   pending_review | approved | rejected | edited; в чат идут approved+edited.

# task_status — только для kind='task'
TASK_OPEN = "open"
TASK_DONE = "done"

class ProjectItem(Base):
    """Структурный элемент памяти проекта: решение / задача / вопрос / знание.

    Переиспользует паттерн Fact Review Queue: review_status гейтит попадание в
    контекст чата (только approved+edited). Провенанс (source_*) — как у
    profile_facts: извлечённый элемент без source_excerpt отбрасывается.
    """
    __tablename__ = "project_items"

    id: Mapped[uuid.UUID]                   # PK, default uuid4
    project_id: Mapped[uuid.UUID]           # FK projects.id, ON DELETE CASCADE, NOT NULL
    kind: Mapped[str]                       # String(16), ITEM_KINDS, NOT NULL
    content: Mapped[str]                    # Text, NOT NULL — формулировка решения/задачи/...
    # review-гейт (как FACT_*): server_default pending_review
    review_status: Mapped[str]             # String(16), default pending_review
    # лайфцикл задачи (только kind='task'; иначе NULL)
    task_status: Mapped[str | None]        # String(8), open|done, nullable
    # провенанс (hallucination-guard, как profile_facts)
    source_conversation_id: Mapped[uuid.UUID | None]  # FK conversations.id, SET NULL
    source_excerpt: Mapped[str | None]     # Text, nullable — цитата-основание
    confidence: Mapped[float]              # Float, default 0.5
    created_at: Mapped[datetime]           # server_default now()
    updated_at: Mapped[datetime]           # server_default now(), onupdate now()

    __table_args__ = (
        Index("ix_project_items_project_kind", "project_id", "kind"),
        Index("ix_project_items_review", "review_status"),
    )
```

Производные хелперы (чистые функции, как `course_percent` / `is_fact_accepted`):
```python
def is_item_accepted(review_status: str) -> bool:
    return review_status in ACCEPTED_FACT_STATUSES   # reuse: approved|edited

def is_task_open(item) -> bool:
    return item.kind == ITEM_TASK and item.task_status == TASK_OPEN
```

### Изменения существующих таблиц (аддитивно, nullable)

```python
# conversations: + project_id
project_id: Mapped[uuid.UUID | None]   # FK projects.id, ON DELETE SET NULL, nullable
                                       # + Index("ix_conversations_project", "project_id")

# content_sources: + project_id
project_id: Mapped[uuid.UUID | None]   # FK projects.id, ON DELETE SET NULL, nullable
                                       # + Index("ix_content_sources_project", "project_id")
```

`NULL` = вне проекта (по умолчанию). Существующие данные не мигрируют — остаются
`NULL`, поведение чата/Лектора неизменно.

### Производные показатели проекта (вычисляются, не хранятся)

По образцу «статус/процент курса выводятся, а не хранятся»:

- `documents_count` = `COUNT(content_sources WHERE project_id=?)`
- `discussions_count` = `COUNT(conversations WHERE project_id=?)`
- `decisions_count` = `COUNT(project_items WHERE kind='decision' AND review_status IN (approved,edited))`
- `open_tasks_count` = `COUNT(project_items WHERE kind='task' AND task_status='open' AND review_status IN (approved,edited))`
- `pending_review_count` = `COUNT(project_items WHERE review_status='pending_review')`

Эти счётчики отдаёт `/projects` (для карточек) и `/projects/{id}` (для шапки) — по
аналогии с `/stats` и `/learn/progress`. **Ничего не денормализуем.**

### Эскиз API (для UX-части; реализуется позже)

```
GET    /projects                      список + производные счётчики
POST   /projects                      создать (name, description)
GET    /projects/{id}                 карточка проекта + счётчики
PATCH  /projects/{id}                 переименовать / архивировать
DELETE /projects/{id}                 удалить (items CASCADE, контент SET NULL)

GET    /projects/{id}/documents       content_sources проекта
GET    /projects/{id}/discussions     conversations проекта
GET    /projects/{id}/items?kind=&review_status=   решения/задачи/вопросы/знания
POST   /projects/{id}/items           добавить вручную (review_status=approved)
PATCH  /projects/items/{item_id}      править / approve / edit / toggle task open|done
POST   /projects/items/{item_id}/reject
DELETE /projects/items/{item_id}
POST   /projects/{id}/extract         LLM-извлечение → pending_review items (reuse extraction)

# существующие, расширяются опциональным project_id:
POST   /learn/article|pdf|youtube|remember   + project_id?   (материал в проект)
POST   /chat                                  + project_id?   (чат в проекте → скоуп-ретрив)
```

---

# Часть 3 — UX (проект, НЕ реализовано)

### Принципы

- Раздел `/projects` — вторичный (как `/learn`, `/me`), главным экраном остаётся
  чат. Стиль — текущий «glass / lime-accent / Russian UI».
- Никаких новых жестов навигации: вкладка «Проекты» в nav рядом с «Лектор»/«Профиль».
- Review-гейт **виден**: извлечённые решения/задачи/знания попадают в очередь
  проверки проекта (`pending_review`) — единообразно с Fact Review Queue в `/me`.

### Экран 1 — `/projects` (список)

```
/// проекты                                            [+ Новый проект]

┌───────────────────────────────────────────────┐
│ Миграция биллинга 1С            ● активен       │
│ 12 материалов · 8 обсуждений · 5 решений        │
│ ⚠ 3 задачи открыты · 4 на проверке              │
└───────────────────────────────────────────────┘
┌───────────────────────────────────────────────┐
│ Личный сайт                     ◦ архив         │
│ 3 материала · 2 обсуждения · 1 решение          │
└───────────────────────────────────────────────┘
```

Карточка: имя, статус-точка, производные счётчики, амбер-бейдж «N на проверке»
(как «новые факты» на Home Dashboard). Пусто → «Создайте первый проект».

### Экран 2 — `/projects/[id]` (воркспейс проекта)

Шапка: имя (инлайн-rename), статус, кнопки «Спросить о проекте», «Обновить знания»
(= `/extract`), «Архивировать». Под шапкой — компактная сводка
(decisions / open tasks / docs / discussions).

Секции (вкладки или якоря на одной странице):

- **Обзор** — авто-сводка: последние решения, открытые задачи, ключевые знания
  (только approved+edited). Каждый пункт кликабелен → к источнику (цитата +
  обсуждение-первоисточник, как traceable-факт в `/me`).
- **Документы** — материалы проекта (переиспользуем карточки `/learn`);
  «+ Добавить материал» вызывает существующий ингест с `project_id`. Открытие →
  существующий ридер `/courses/[id]`.
- **Обсуждения** — проектные чаты (карточки как в сайдбаре чата); «+ Новый чат о
  проекте» открывает чат с `project_id` (ретрив скоупится на проект).
- **Решения / Задачи / Знания** — списки `project_items` по `kind`. Задачи с
  чекбоксом open→done. Ручное «+ Добавить решение/задачу» (сразу approved).
- **На проверке** (бейдж с числом) — очередь `pending_review` элементов:
  принять / отклонить / поправить-и-принять — **тот же UI-паттерн, что Fact Review
  Queue в `/me`** (карточка: тип, формулировка, цитата-основание, действия).

### Экран 3 — Проектный чат (расширение существующего)

- Чат, начатый из проекта, помечен плашкой «Проект: <имя>» над полем ввода.
- Ретрив автоматически скоупится: память/материалы/решения проекта; чипы контекста
  (`Память/Материалы/Курсы/Избранное`) работают как сейчас, но в границах проекта.
- Под ответом — те же источники-чипы; решения/знания проекта показываются как
  использованный контекст (по аналогии с чипами памяти).
- Вопросы-сценарии, которые должны «просто работать»:
  «что мы решили по авторизации?» · «почему выбрали OpenRouter?» ·
  «какие задачи ещё открыты?» — ответ из `<project>`-блока + чанков проекта.

### Ответы на ключевые пользовательские вопросы (трассировка к данным)

| Вопрос | Откуда ответ |
|--------|--------------|
| Что было решено? | `project_items` kind=decision (approved/edited) + чанки обсуждений |
| Почему приняли решение? | `source_excerpt` + первоисточник-обсуждение элемента-decision |
| Какие задачи открыты? | `project_items` kind=task, task_status=open (approved/edited) |
| Что мы знаем по теме? | RAG по чанкам документов/обсуждений проекта + items kind=note |

### Что НЕ входит в v1 (по UX)

- Перенос существующих чатов/материалов в проект задним числом (стретч).
- Авто-фоновое извлечение знаний после каждого чата (v1 — кнопка «Обновить знания»).
- Доски/канбан задач, дедлайны, назначение исполнителей (PAM — личная память, не
  трекер задач).

---

## Связь с остальной системой

- **Fact Review Queue (P0)** — паттерн review-гейта переиспользован 1:1
  (`review_status`, `ACCEPTED_FACT_STATUSES`, провенанс).
- **Observability (P0)** — извлечение/чат проекта инструментируются теми же
  `record_event(...)` (kinds `extraction`/`chat`), провайдер атрибутируется через
  `completion_provider()` / `prov_used` (см. closed provider attribution в `CLAUDE.md`).
- **Лектор / RAG** — ингест и ретрив переиспользуются со скоуп-фильтром `project_id`.

## Критерий готовности дизайна (этот документ)

- [x] ADR: контекст, решение, ≥4 альтернативы, последствия, открытые вопросы.
- [x] Схема данных: 2 новые таблицы + 2 nullable-FK, константы, производные
      показатели (не денормализованы), эскиз API.
- [x] UX: 3 экрана, review-очередь, проектный чат, трассировка вопрос→данные.
- [x] Явно зафиксировано: **не реализовано**, реализация — отдельной фазой.
