# Stepwise

> **Нативный Windows-движок для создания и интерактивного воспроизведения пошаговых руководств (Walkthrough Guides)**

[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-blue.svg)](https://microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/)
[![Tests](https://img.shields.io/badge/tests-399%2B%20passing-brightgreen.svg)]()
[![Build](https://img.shields.io/badge/build-0%20warnings%20%2F%200%20errors-brightgreen.svg)]()

---

## Быстрый старт (Quick Start)

### 1. Запуск собранного релиза
Готовый исполняемый файл приложения собран в:
```powershell
.\bin\publish\Stepwise.App.exe
```
Для запуска с указанием конкретной папки руководства:
```powershell
.\bin\publish\Stepwise.App.exe --project "C:\Path\To\MyGuide"
```

### 2. Сборка из исходного кода
```powershell
# Полная сборка решения
dotnet build Stepwise.sln -c Release

# Публикация готового пакета
dotnet publish src/Stepwise.App/Stepwise.App.csproj -c Release -r win-x64 -o bin/publish/
```

### 3. Запуск тестов
```powershell
# Запуск всего набора тестов
pwsh -File .\skills\run-tests.ps1
```

---

## Документация

- 📖 **[Руководство пользователя](file:///docs/user-guide.md)** — подробная иллюстрированная инструкция по записи, редактированию, плееру и оверлею.
- 📐 **[Главная архитектурная спецификация](file:///specs/spec.md)** — доменные контракты, модель безопасности, конвейер записи и требования.
- 📋 **[Реестр бэклога](file:///docs/backlog.md)** — зафиксированные идеи и запланированные будущие модули.

---

## Архитектура решения

```text
Stepwise.sln
├── src/
│   ├── Stepwise.Core/                 # Чистая доменная модель (Step, Action, Player, Policy)
│   ├── Stepwise.WindowsIntegration/   # Win32 Hooks, UI Automation, GDI/WGC Capture, Desktop Overlay
│   ├── Stepwise.Storage/              # SQLite репозиторий (project.db, project.json)
│   └── Stepwise.App/                  # WinUI 3 десктоп-оболочка (Editor, Player, Overlay)
├── tests/
│   ├── Stepwise.Tests/                # 399+ интеграционных и FlaUI GUI E2E тестов
│   └── Stepwise.TestTarget/           # Детерминированное тестовое приложение WPF
└── skills/                            # Инженерные инструменты и диагностические скрипты
```
