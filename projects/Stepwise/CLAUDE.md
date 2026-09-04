# Stepwise

Project-specific instructions for Claude Code. This file overrides / extends the
baseline philosophy in the root CLAUDE.md.

## 📐 Spec-Driven Development (github/spec-kit) — Mandatory Standard
Все новые фичи и изменения должны проектироваться через spec-kit:
1. `/speckit-specify <feature>` — создать спецификацию в `specs/<NNN-feature>/spec.md`
2. `/speckit-plan` — составить архитектурный план и контракты в `specs/<NNN-feature>/plan.md`
3. `/speckit-tasks` — сгенерировать атомарные задачи в `specs/<NNN-feature>/tasks.md`
4. `/speckit-implement` — реализовать задачи пошагово
5. `/speckit-converge` — верифицировать код относительно спеки и дописать оставшиеся задачи

### Баги и идеи:
- Баги: `/speckit-bug-assess` -> `/speckit-bug-fix` (TDD) -> `/speckit-bug-test`
- Идеи: `/speckit-assess-intake` -> `/speckit-assess-shape` -> `/speckit-assess-research` -> `/speckit-assess-define` -> `/speckit-assess-decide`

## Stack
- **Язык & Платформа**: C# 13, .NET 9 (Windows Desktop)
- **UI & Presentation**: WinUI 3, Windows App SDK, CommunityToolkit.Mvvm
- **Системные API**: Win32 User32 (SetWindowsHookEx, Low-Level Mouse Hook), Microsoft UI Automation (UIA), Windows.Graphics.Capture
- **Хранилище**: SQLite (Microsoft.Data.Sqlite) + локальная файловая система
- **AI (Опционально)**: Groq / Ollama через интерфейс IAIProvider (по умолчанию NullProvider)
- **Команды**:
  - Сборка: `dotnet build projects/Stepwise/Stepwise.sln`
  - Тесты: `dotnet test projects/Stepwise/Stepwise.sln`
  - Запуск прототипа: `dotnet run --project projects/Stepwise/src/Stepwise.App/Stepwise.App.csproj`

## ⛔ Архитектурные ограничения
1. **Строго нативный Windows-стек**: Никаких Electron, Tauri, Node.js, Python, веб-оберток.
2. **Offline-first ядро**: Полная работоспособность без интернета и без AI.
3. **Неблокирующие хуки**: Обработчики глобальных хуков выполняются в изолированном STA-потоке с нативным Win32 message loop и асинхронно диспетчеризируют события в ThreadPool, не вызывая лагов курсора.

## Tools enabled (copied from root library into .claude/)
- agents:   
- skills:   spec-kit,superpowers,karpathy
- commands: gsd,speckit
- rules:    karpathy
- hooks:    
- mcp:      

## ⛔ Tool-selection gate (before building anything)
When I give you a new task or paste a prompt, **do not start coding immediately.** Run
`/start-task` first (it reads the root `library/CATALOG.md` + `library/mcp/README.md`,
proposes the best-matching tools as a grouped list of max ~7, and installs my picks via
`scripts/add-tools.ps1`). Only after I choose do you start development.
Skip only if I say "skip tools" or the needed tools are already in `.claude/`.

## 📓 Lessons
Read `LESSONS.md` (this project) and the root `LESSONS.md` at the start of work. After a
non-obvious bug or wrong approach, append an entry with `/lesson`.
