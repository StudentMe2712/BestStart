# PROJECT_FACTS.md
> АВТОГЕНЕРАЦИЯ из кода (app/generate_project_facts.py). НЕ редактировать вручную —
> код всегда приоритетнее документации. Что нельзя вывести из кода — `Not Derived`.

## Что такое PAM (overview)
Имя приложения (FastAPI title): Personal AI Memory.
Версия: 0.2.0.
Описание (app.description): Phase 2 — collect conversations, full-text + semantic search..
Структурная сводка: таблиц БД — 13; эндпоинтов API — 66; провайдеры LLM — groq, openrouter, ollama; default provider (config) — hybrid.

## Таблицы базы данных (database tables)
Какие таблицы есть в БД: всего 13.
Список таблиц: chunks, content_chunks, content_sources, conversations, course_progress, courses, events, memory_items, memory_links, messages, profile_facts, projects, saved_messages.
Поля и внешние ключи (FK) по таблицам:
- chunks (6 полей; FK: message_id->messages): id, message_id, content, position, embedding, created_at
- content_chunks (6 полей; FK: source_id->content_sources): id, source_id, content, position, embedding, created_at
- content_sources (15 полей; FK: project_id->projects): id, kind, title, url, status, text, formatted_text, reformat_status, reformat_progress, char_count, error, original_data, original_mime, project_id, created_at
- conversations (10 полей; FK: project_id->projects): id, source, external_id, title, started_at, updated_at, pinned, archived, raw_json, project_id
- course_progress (9 полей; FK: course_id->courses): id, course_id, completed_lessons, lessons_total, quiz_score, quiz_total, quiz_completed_at, created_at, updated_at
- courses (6 полей; FK: source_id->content_sources): id, source_id, title, level, data, created_at
- events (7 полей): id, kind, provider, status, duration_ms, detail, created_at
- memory_items (14 полей; FK: project_id->projects): id, project_id, source, source_ref, title, content, summary, item_type, importance, tags, status, content_tsv, created_at, updated_at
- memory_links (8 полей): id, source_kind, source_id, target_kind, target_id, relation, confidence, created_at
- messages (7 полей; FK: conversation_id->conversations): id, conversation_id, role, content, position, sent_at, content_tsv
- profile_facts (8 полей; FK: source_conversation_id->conversations): id, category, content, source_conversation_id, source_excerpt, confidence, status, created_at
- projects (6 полей): id, name, description, status, created_at, updated_at
- saved_messages (9 полей; FK: conversation_id->conversations): id, conversation_id, source, title, role, content, position, note, created_at

## Миграции (Alembic migrations)
Сколько миграций: всего 15.
Migration head: c8d9e0f1a2b3.
Цепочка миграций (base -> head):
- 0001: initial schema
- efc12b5654c3: saved_messages
- 2ec708645017: chunks_embeddings
- 4b747af609d8: profile_facts
- 96d1d6982add: content_sources + content_chunks (phase5 lecturer)
- a1a3e3d1fb0d: courses table (phase5 lecturer)
- 68113919ba65: conversations pinned + archived (sidebar)
- b1f2c3d4e5a6: content_sources: formatted_text + original file bytes (preview)
- c2d3e4f5a6b7: content_sources: reformat_status + reformat_progress (#6 background re
- d3e4f5a6b7c8: profile_facts: status (Fact Review Queue — P0)
- e4f5a6b7c8d9: events: lightweight observability log (Observability panel — P0)
- f5a6b7c8d9e0: course_progress: learning progress per course (P1)
- a6b7c8d9e0f1: projects + project scope (Project Memory P2, stage 1)
- b7c8d9e0f1a2: memory_items: universal knowledge container (Project Memory P2, stage 
- c8d9e0f1a2b3: memory_links: links between knowledge objects (Project Memory P2, stag

## API / роуты (endpoints)
Какие эндпоинты существуют: всего 66 эндпоинтов API.
Группы (router prefix): /chat=2, /conversations=5, /facts=7, /health=1, /index=1, /learn=19, /memory=11, /projects=10, /saved=3, /search=3, /stats=2, /timeline=1.
Маршруты (метод путь):
- GET /
- POST /chat
- POST /chat/attachment
- GET /conversations
- POST /conversations
- DELETE /conversations/{conv_id}
- GET /conversations/{conv_id}
- PATCH /conversations/{conv_id}
- GET /facts
- GET /facts/counts
- POST /facts/extract
- DELETE /facts/{fact_id}
- PATCH /facts/{fact_id}
- POST /facts/{fact_id}/approve
- POST /facts/{fact_id}/reject
- GET /health
- POST /index/run
- POST /learn/article
- POST /learn/file
- POST /learn/pdf
- GET /learn/progress
- POST /learn/project-docs/reindex
- POST /learn/project-facts/regenerate
- POST /learn/remember
- GET /learn/sources
- DELETE /learn/sources/{source_id}
- GET /learn/sources/{source_id}
- GET /learn/sources/{source_id}/course
- POST /learn/sources/{source_id}/course
- GET /learn/sources/{source_id}/file
- POST /learn/sources/{source_id}/lesson
- GET /learn/sources/{source_id}/progress
- POST /learn/sources/{source_id}/quiz
- POST /learn/sources/{source_id}/reformat
- POST /learn/text
- POST /learn/youtube
- GET /memory/items
- POST /memory/items
- POST /memory/items/file
- DELETE /memory/items/{item_id}
- GET /memory/items/{item_id}
- PATCH /memory/items/{item_id}
- POST /memory/items/{item_id}/tag
- GET /memory/links
- POST /memory/links
- DELETE /memory/links/{link_id}
- POST /memory/recall
- GET /projects
- POST /projects
- DELETE /projects/{project_id}
- GET /projects/{project_id}
- PATCH /projects/{project_id}
- POST /projects/{project_id}/attach
- GET /projects/{project_id}/conversations
- POST /projects/{project_id}/detach
- GET /projects/{project_id}/items
- GET /projects/{project_id}/materials
- GET /saved
- POST /saved
- DELETE /saved/{saved_id}
- GET /search
- GET /search/hybrid
- GET /search/semantic
- GET /stats
- POST /stats/capture-failed
- GET /timeline

## Компоненты памяти и Unified Retrieval
Что входит в Unified Retrieval (компонент: участие и механизм):
- conversations (разговоры): ✓ участвует (вектор + строгий текстовый fallback)
- lecturer materials + project knowledge (content_sources): ✓ участвует (вектор + строгий текстовый fallback)
- memory_items: ✓ участвует (полнотекст + теги)
- memory_links: ✓ участвует (1-hop расширение по связям)
- profile_facts: ✓ участвует (только accepted (approved/edited))
- saved / избранное: ✓ участвует (полнотекст (по чипу use_saved))
- courses / курсы: ✓ участвует (полнотекст (по чипу use_courses))

## LLM инфраструктура
Доступные провайдеры (код): groq, openrouter, ollama.
Default provider (config LLM_PROVIDER): hybrid.
Модели: groq=openai/gpt-oss-120b; groq_json=llama-3.3-70b-versatile; groq_vision=meta-llama/llama-4-scout-17b-16e-instruct; openrouter=nvidia/nemotron-3-super-120b-a12b:free; ollama_chat=llama3.2:3b; embeddings=nomic-embed-text.
Hybrid-роутинг (llm.route_provider): длинные/«тяжёлые» запросы → openrouter, иначе groq (если задан ключ OpenRouter).
Fallback цепочка: provider-to-provider переключения НЕТ (закрытый набор). Деградации: hybrid-роутинг; без Ollama разговоры/материалы → текстовый fallback; vision → text pre-pass.

## Telegram Bot
Как работает Telegram Bot (бот PAM):
- Команды: /ask, /new.
- Режимы: захват (текст/ссылка/код/файл/фото → memory item) и чат (/ask → POST /chat, RAG по всей памяти PAM).
- Configured (token + allowed user заданы): True.
- Polling на этой машине (config TELEGRAM_BOT_OWNER): True.
- Запуск: отдельный процесс (python -m app.telegram_bot), long polling без webhook.
- Голосовые/аудио: не поддерживаются (V1).

## Implemented Features (факты, без маркетинга)
- Чат с памятью (SSE RAG) + вложения (/chat)
- Импорт/захват разговоров (идемпотентный UPSERT) (/conversations)
- Поиск: полнотекст + семантика (/search)
- Лектор: материалы + генерация курсов + project knowledge ingest (/learn)
- Profile facts + Fact Review Queue (P0) (/facts)
- Memory items + links + recall (Project Memory P2) (/memory)
- Project Memory: проекты и scope (/projects)
- Избранное (saved messages) (/saved)
- Observability: events + Memory Health (P0) (/stats)
- Timeline памяти (/timeline)
- Индексация/эмбеддинги (ручной триггер) (/index)
