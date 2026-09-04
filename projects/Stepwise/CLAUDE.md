# Stepwise

Project-specific instructions for Claude Code and Antigravity Agents. This file overrides / extends the
baseline philosophy in the root CLAUDE.md.

## 📐 Spec-Driven Development (github/spec-kit) — Mandatory Standard
Все новые фичи и изменения должны проектироваться через spec-kit:
1. `/speckit-specify <feature>` — создать спецификацию в `specs/<NNN-feature>/spec.md`
2. `/speckit-plan` — составить архитектурный план и контракты в `specs/<NNN-feature>/plan.md`
3. `/speckit-tasks` — сгенерировать атомарные задачи в `specs/<NNN-feature>/tasks.md`
4. `/speckit-implement` — реализовать задачи пошагово
5. `/speckit-converge` — верифицировать код относительно спеки и дописать оставшиеся задачи

## 👑 ORCHESTRATOR / SUBAGENT PROTOCOL (Обязательный регламент)
Я являюсь **ОРКЕСТРАТОРОМ**.
1. Не выполнять крупные изменения проекта напрямую, если задача может быть делегирована субагенту.
2. Перед каждой задачей:
   - Определи, требуется ли специализированный субагент (`Architect`, `WinUI Engineer`, `Windows API Engineer`, `Storage Engineer`, `QA/Test Engineer`, `UI/UX Reviewer`, `Code Reviewer`).
   - Сформируй узкую атомарную задачу (один субагент = одна четкая задача).
   - Передай только необходимые файлы и контракты.
   - Лично проверь результат (сборка, тесты, код). Не доверяй "done" на слово без валидации.
3. После завершения серии изменений и прохождения тестов самостоятельно выполнить `./sync.sh`. Не считать работу завершенной до успешного выполнения `./sync.sh`.

## 🛡️ SCOPE CONTROL (Запрет самовольного расширения скоупа)
1. *"Could be useful" ≠ "Must implement now"*.
2. Не реализовывать функции, которых нет в текущем этапе.
3. При обнаружении полезной идеи вне текущего скоупа: занести в `docs/backlog.md` (Title, Reason, Potential value, Complexity, Dependencies) и продолжить текущую задачу.
4. Категорически запрещены настройки, плагины, темы, анимации, сторонние экспорты, облака без согласования.

## Stack
- **Язык & Платформа**: C# 13, .NET 9 (Windows Desktop)
- **UI & Presentation**: WinUI 3, Windows App SDK, CommunityToolkit.Mvvm
- **Системные API**: Win32 User32 (SetWindowsHookEx, Low-Level Mouse Hook), Microsoft UI Automation (UIA), Windows.Graphics.Capture, Windows Composition
- **Хранилище**: SQLite (Microsoft.Data.Sqlite) + локальная файловая система
- **AI (Опционально)**: Groq / Ollama через интерфейс IAIProvider (по умолчанию NullProvider)
- **Инженерные скиллы (skills/)**:
  - `skills/run-tests.ps1`: Запуск xUnit с компактной сводкой
  - `skills/build-project.ps1`: Сборка солюшена и отчет об ошибках
  - `skills/inspect-screenshot.ps1`: Валидация файла скриншота на диске
  - `skills/inspect-ui.ps1`: Дамп UIA-дерева в JSON
  - `skills/inspect-window.ps1`: Получение параметров активного окна
  - `skills/validate-project.ps1`: Сквозная валидация проекта

## ⛔ Архитектурные ограничения
1. **Строго нативный Windows-стек**: Никаких Electron, Tauri, Node.js, Python, веб-оберток.
2. **Offline-first ядро**: Полная работоспособность без интернета и без AI.
3. **Неблокирующие хуки**: Обработчики глобальных хуков выполняются в изолированном STA-потоке с нативным Win32 message loop и асинхронно диспетчеризируют события в ThreadPool, не вызывая лагов курсора.
4. **Безопасность БД**: Phase 3 является READ-FIRST. Запрещено произвольно изменять схему или удалять `project.db`.
5. **Жизненный цикл изображений**: Освобождать файловые стримы сразу после `SetSourceAsync`, защищать UI от race conditions при быстром переключении шагов.

## 📓 Lessons
Read `LESSONS.md` (this project) and the root `LESSONS.md` at the start of work. After a
non-obvious bug or wrong approach, append an entry with `/lesson`.
