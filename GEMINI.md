# Root — Gemini CLI & Antigravity Rules (BestStart)

This directory is a **framework / catalog**, not an app. It holds a curated library of
tooling merged from upstream projects, plus a `projects/` workspace.
Projects are self-contained; this root is the source of truth.

This file is loaded automatically by Gemini CLI / Antigravity for the workspace, so the rules
below apply everywhere unless a project's own `GEMINI.md` overrides them.

---

## 📐 Spec-Driven Development (github/spec-kit) — Mandatory Standard

GitHub Spec-Kit (`github/spec-kit` / `specify-cli`) является **обязательным стандартом проектирования, разработки и документирования** для всего репозитория BestStart. Код создаётся исключительно на основе формализованных спецификаций, архитектурных планов и структурированных задач под управлением проектной Конституции.

### Workflow:
1. **Constitution** (`.specify/memory/constitution.md`) — нерушимые правила: TDD, простота, модульность, Docker-изоляция.
2. **Specify** (`specs/<NNN-feature>/spec.md`) — требования (User Stories P1/P2/P3, Functional Requirements `FR-XXX`, Success Criteria `SC-XXX`).
3. **Plan** (`specs/<NNN-feature>/plan.md`) — технический план, архитектурные решения.
4. **Tasks** (`specs/<NNN-feature>/tasks.md`) — декомпозиция на атомарные фазы и задачи.
5. **Implement** — пошаговое выполнение задач с TDD проверками.
6. **Converge** — верификация соответствия кода спецификации (append-only в `tasks.md`).

---

## 🤖 Baseline Working Philosophy (Karpathy Guidelines)

1. **Think before coding** — surface trade-offs and clarify when ambiguous.
2. **Simplicity first** — minimum code, no speculative features or unnecessary abstractions (YAGNI).
3. **Surgical changes** — touch only what's needed; don't "improve" unrelated code or clean formatting randomly.
4. **Goal-driven & Test-verified** — every change must have verifiable tests or concrete success checks.
5. **Secrets & Environment** — secrets belong strictly in `.env` (git-ignored); `.env.example` documents the shape.

---

## 🐳 Docker-First Isolation

Projects must not pollute the host or collide with each other. Every project runs in its own containerized environment or isolated virtualenv where applicable. Shared host ports and persistent DBs must be isolated.

---

## 📓 Learning From Mistakes (LESSONS.md)

- At the start of work, check project `LESSONS.md` and root `LESSONS.md`.
- After resolving non-obvious bugs or wrong approaches, record lessons learned.
- Proactively apply existing lessons to prevent repeat mistakes.

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

---

A project's own `projects/<name>/GEMINI.md` takes precedence over this file.
