# Root — Multi-Agent System Rules (BestStart)

This repository is a structured framework and catalog containing autonomous tooling and self-contained projects under `projects/`.

All AI agents operating in this repository (Claude, Gemini, Antigravity, Subagents, Orchestrators) must adhere to the rules below.

---

## 🎯 Core Operating Principles

1. **Spec-Driven Development (SDD)**: All architectural and functional changes must follow github/spec-kit specifications (`specs/`) and respect the project constitution.
2. **Karpathy Guidelines**:
   - **Think before coding**: Analyze trade-offs, clarify ambiguous requirements.
   - **Simplicity first**: Minimal code footprint, avoid premature abstractions or over-engineering.
   - **Surgical changes**: Touch only lines necessary for the task; preserve comments and unrelated formatting.
   - **Goal-driven verification**: Every implementation must pass automated tests (`pytest`) before completion.
3. **Environment & Secrets Safety**: Credentials and API keys must never be committed to git. Use `.env` with documentation in `.env.example`.
4. **Isolated Environments**: Individual projects run in isolated Docker containers or dedicated virtualenvs (`.venv`). Do not install project dependencies into the global host environment.

---

## 🤝 Multi-Agent Protocol

- Subagents must clearly report results and status back to the parent agent via message passing (`send_message`).
- Avoid running multiple concurrent conflicting file modifications.
- Check project and root `LESSONS.md` before starting tasks; document non-trivial solutions after completion.

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

A project's own `projects/<name>/AGENTS.md` takes precedence over this file.
