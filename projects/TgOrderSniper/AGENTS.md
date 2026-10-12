# OmniLeadSniper (TgOrderSniper) — Multi-Agent System Rules

Guidelines for AI agents (Coder, Reviewer, Tester, Architect) working within `TgOrderSniper`. This file extends the root `AGENTS.md`.

---

## 🎯 Project Mission & Scope
OmniLeadSniper перехватывает лиды из Telegram и веб-бирж, пропускает через многоуровневый фильтр (Privacy Gate -> Regex Pre-Filter -> Groq AI Classifier), персистентно сохраняет в SQLite и транслирует в Telegram-чат команды через Bot API.

---

## ⚙️ Agent Guidelines & Quality Gates

1. **TDD & Verification Gate**:
   - Перед завершением любой задачи агент обязан выполнить `pytest` из корня проекта.
   - Все 100+ существующих тестов должны оставаться зелеными.
2. **Surgical Modifications**:
   - При внесении правок затрагивать строго целевые модули.
   - Сохранять существующие докстринги, комментарии и сигнатуры типов.
3. **Data Integrity & Storage**:
   - Персистентное состояние хранится в SQLite (`data/sniper.db`).
   - Использовать `ProcessedLead` модель и проверять дедупликацию по `(source, external_id)` и `content_hash`.

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
