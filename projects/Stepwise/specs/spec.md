# Stepwise — Главная архитектурная спецификация и Центр Проекта (Brain Specification)

> **Статус:** В активной разработке (Срез: Фазы 1–6 завершены)  
> **Версия спецификации:** 2.0.0 (Объединена с Master Engineering Prompt / ultraprompt.md)  
> **Платформа:** Windows 10/11 (x64 / ARM64)  
> **Технологический стек:** C# 13, .NET 9+, WinUI 3, Windows App SDK, Microsoft UI Automation (UIA), Win32 API (CsWin32 / PInvoke), Windows.Graphics.Capture, SQLite, CommunityToolkit.Mvvm, System.Text.Json.

---

## 1. Философия, Миссия и Границы Продукта

**Stepwise** — это высокопроизводительное нативное приложение для Windows, предназначенное для автоматического создания интерактивных пошаговых руководств (Walkthrough Guides) по работе с любым Windows-приложением.

### Чем проект НЕ является:
- Очередным простым инструментом для скриншотов (Screenshot tool).
- Очередным рекордером экрана в видеоформате (Screen recorder / OBS).
- AI-оберткой (AI Wrapper): **Core application полностью функционирует без подключения к сети и без AI**.
- Облачным SaaS или веб-приложением на Electron/Tauri/Node.js/Python.

### Главный продукт — это синергия компонентов:
```text
RECORDING ENGINE + WINDOWS OBSERVABILITY + UI AUTOMATION + SCREEN CAPTURE 
+ STEP MODEL + GUIDE EDITOR + INTERACTIVE PLAYER + COMPOSITION OVERLAY
```

### Главный принцип Pipeline:
Windows уже предоставляет колоссальный объём структурированной метаинформации. Мы не пытаемся определять состояние интерфейса через дорогой и ненадежный визуальный AI.
```
User Action → Win32 Input Hook → UI Automation → Active Window Detection 
→ Screen Capture → Event Correlation → Step Detection → Structured Step 
→ Guide Model → Local SQLite Storage → [Опционально: AI Enhancement]
```
> **AI находится строго в конце конвейера** и служит только для синтеза человекочитаемых описаний и заголовков. AI никогда не является источником истины (Source of Truth) и никогда необратимо не перезаписывает исходные телеметрические данные.

---

## 2. Архитектура Solution и Модульность

Проект следует принципам модульности, слабой связности и разделения ответственности:

```text
Stepwise.sln
├── src/
│   ├── Stepwise.Core/                 # Доменные модели, интерфейсы, Event Correlation, чистая логика (без UI-зависимостей)
│   ├── Stepwise.WindowsIntegration/   # Win32 API, Low-Level Hooks, UI Automation, Window Tracker, GDI/WGC Capture
│   ├── Stepwise.Storage/              # SQLite репозиторий, project.json, управление файловой системой и ассетами
│   ├── Stepwise.Guides/               # Guide Builder, модели редактора, логика плеера и экспорт (HTML/MD/JSON)
│   ├── Stepwise.AI/                   # IAIProvider (NullProvider, GroqProvider, OllamaProvider)
│   └── Stepwise.App/                  # WinUI 3 Shell, MVVM (CommunityToolkit), System Tray, Hotkeys
├── tests/
│   ├── Stepwise.Tests/                # Модульные и интеграционные тесты конвейера, UIA и хранилища
│   └── Stepwise.TestTarget/           # Детерминированное тестовое Windows-приложение (кнопки, поля ввода, списки)
└── skills/                            # "Органы чувств" агента (PowerShell диагностика UIA, окон, скриншотов, тестов)
```

---

## 3. Детальная модель данных (Domain Contracts)

### 3.1. Типы действий (`ActionType`)
```csharp
public enum ActionType
{
    LeftClick,
    RightClick,
    DoubleLeftClick,
    MiddleClick,
    MouseDown,
    MouseUp,
    DragAndDrop,
    Scroll,
    KeyPress,
    TextInput,
    WindowActivated,
    WindowClosed,
    ManualStep,
    Unknown
}
```

### 3.2. Координатное пространство (`CoordinateSpace` & `BoundingBox`)
Разрешение проблем DPI (100%, 125%, 150%, 200%) и мультимониторных систем с отрицательными координатами:
```csharp
public readonly record struct BoundingBox(double X, double Y, double Width, double Height)
{
    public static BoundingBox Empty => new(0, 0, 0, 0);
    public bool IsEmpty => Width <= 0 || Height <= 0;
}
```

### 3.3. Снимок элемента интерфейса (`ElementInfo`)
```csharp
public sealed record ElementInfo(
    string Name,
    string ControlType,
    string AutomationId,
    string ClassName,
    string ProcessName,
    int ProcessId,
    string WindowTitle,
    long WindowHandle,
    BoundingBox BoundingRectangle,
    string FrameworkId = "Unknown",
    bool IsPassword = false
);
```

### 3.4. Шаг руководства (`Step`)
```csharp
public sealed record Step(
    Guid Id,
    int SequenceIndex,
    DateTime Timestamp,
    ActionType Action,
    double ClickX,
    double ClickY,
    ElementInfo TargetElement,
    string? ScreenshotPath = null,
    string? Title = null,
    string? Description = null,
    Dictionary<string, string>? Metadata = null
);
```

### 3.5. Проект и Руководство (`Project` / `Guide`)
Формат хранения проекта на диске — переносимый гибридный каталог:
```text
[ProjectName]/
├── project.json                       # Метаданные проекта и schemaVersion для миграций
├── project.db                         # SQLite база данных (таблицы Projects, Steps, индексы)
└── assets/
    └── screenshots/
        ├── step_001.png               # Скриншоты с акцентной подсветкой
        └── step_002.png
```

---

## 4. Конвейер обработки: Сырые события vs Логические шаги

Критический принцип: **не путать низкоуровневые прерывания мыши с логическими действиями пользователя**.
```
[Win32 Mouse/Keyboard Hook]
            ↓ (WM_LBUTTONDOWN, WM_LBUTTONUP, WM_KEYDOWN)
[RawInputEvent Stream]
            ↓ (Фильтрация шума, троттлинг перемещений)
[Event Correlator]
            ↓ (Группировка: Click TextBox + Type "Text" + Click Save)
[SemanticAction Classifier]
            ↓ (Сверка с UIA целевого элемента)
[StepDetector]
            ↓ (Вызов ScreenCapture + BoundingBox Highlight)
[Structured Step Generation]
            ↓
[Atomic SQLite & Disk Persistence]
```

### Защита приватности и чувствительных данных (Privacy by Design):
1. **Маскирование паролей:** Если элемент имеет `IsPassword == true` или относится к полям учетных данных, ввод с клавиатуры принудительно заменяется на `••••••••` и не логируется.
2. **Исключение приложений:** Пользователь может задать черный список процессов (например, Keepass, 1Password, Telegram).
3. **Ручной шаг (`Manual Step`):** Глобальный хоткей (по умолчанию `Ctrl+Shift+S`), позволяющий принудительно зафиксировать экран и активный элемент в любой момент.

---

## 5. "Органы чувств" и Инженерная лаборатория агента (`skills/` & `TestTarget`)

Чтобы разработка через AI велась на основе фактов, а не гаданий, агент наделен собственными инструментами исследования Windows:

1. `skills/inspect-ui.ps1`: Выгружает дерево UI Automation любого процесса/окна в формате JSON.
2. `skills/inspect-window.ps1`: Определяет текущее активное окно, HWND, PID, заголовок и границы.
3. `skills/inspect-process.ps1`: Собирает данные о процессе, пути к exe и дескрипторах.
4. `skills/inspect-screenshot.ps1`: Проверяет физический файл скриншота на диске (байтность, размеры, отсутствие повреждений).
5. `skills/build-project.ps1`: Нативная сборка проекта с выводом предупреждений и ошибок.
6. `skills/run-tests.ps1`: Запуск xUnit-тестов с компактным отчетом об упавших тестах.
7. `skills/validate-project.ps1`: Сквозной скрипт валидации (git status + build + tests + assets + DB).
8. `Stepwise.TestTarget`: Контролируемое тестовое Windows-приложение со стабильными `AutomationId` для автоматизированных Golden Tests.

---

## 6. Генеральный план развития проекта (Master Roadmap)

### Фаза 0: Инструментарий и Лаборатория валидации
- [x] Создание Solution `Stepwise.sln` и проектов.
- [x] Инициализация нативных тестов (xUnit).
- [ ] Настройка набора PowerShell-скиллов (`skills/*.ps1`) для исследования Windows UI.
- [ ] Создание детерминированного тестового приложения `Stepwise.TestTarget`.

### Фаза 1: Фундамент и Технический срез (MVP 0.1)
- [x] **Шаг 1: Архитектура Solution, базовые модели (Step, ActionType) и интерфейсы.**
- [x] **Шаг 2: Реализация глобального хука мыши (Win32 API) в выделенном STA-потоке.**
- [x] **Шаг 3: Интеграция Microsoft UI Automation (извлечение Name, ControlType, AutomationId).**

### Фаза 2: Визуализация и Хранилище (MVP 0.2)
- [x] **Шаг 4: Интеграция захвата экрана (Win32 GDI / WGC) с акцентной подсветкой элементов.**
- [x] **Шаг 5: Сборка конвейера Hook -> UIA -> Capture -> Step.**
- [x] **Шаг 6: Проектирование локального хранилища (SQLite + File System для ассетов).**

### Фаза 3: Интерактивный интерфейс и Редактор руководств
- [ ] **Шаг 7: WinUI 3 Shell & MVVM (CommunityToolkit.Mvvm).**
  - Главное окно: список проектов, кнопка "Новая запись", индикатор статуса.
  - Состояния: Готов к записи (Idle), Запись активна (Recording ●), Пауза (Paused).
- [ ] **Шаг 8: Визуальный 3-панельный редактор шагов (Editor View).**
  - Слева: Вертикальный список карточек шагов с номерами, типами действий и миниатюрами.
  - По центру: Большой просмотрщик скриншота с интерактивной рамкой подсветки активного элемента.
  - Справа: Панель свойств — редактирование заголовка, описания, подсказок, ручная корректировка региона.
  - Операции: Переупорядочивание шагов (Drag & Drop), удаление лишних кликов, добавление шага вручную.

### Фаза 4: Воспроизведение и Прозрачный оверлей (Player & Overlay)
- [ ] **Шаг 9: Guide Player (Автономный плеер инструкций).**
  - Режим последовательного прохождения: "Шаг N из M", кнопки "Далее", "Назад", горячие клавиши.
  - Компактный плавающий виджет поверх всех окон.
- [ ] **Шаг 10: Реализация прозрачного Desktop Overlay (Composition API).**
  - Клик-сквозное полноэкранное окно без рамок (`WS_EX_TRANSPARENT | WS_EX_LAYERED`).
  - Мягкое затемнение фона экрана вокруг целевого элемента.
  - Пульсирующая анимированная рамка вокруг реального `BoundingRectangle` элемента на рабочем столе.
  - Информационная плашка с инструкцией рядом с подсвечиваемым контролом.

### Фаза 5: Системный трей, Хоткеи и Обработка ввода
- [ ] **Шаг 11: Системный трей и глобальные горячие клавиши.**
  - Иконка в системном трее Windows с контекстным меню управления.
  - Хоткеи: `Start/Stop Recording`, `Pause/Resume`, `Manual Step`.
- [ ] **Шаг 12: Event Correlation & Текстовый ввод.**
  - Захват `TextInput` с фильтрацией паролей и сжатием серии нажатий клавиш в один осмысленный шаг.

### Фаза 6: Экспорт и Опциональный AI
- [ ] **Шаг 13: Модуль экспорта руководств (HTML, Markdown, JSON, PDF).**
  - Автономный HTML-файл руководства со встроенными скриншотами (Base64) и стилями.
  - Экспорт в GitHub-flavored Markdown.
- [ ] **Шаг 14: Интеграция IAIProvider (NullProvider по умолчанию, GroqProvider для суммаризации).**
  - Реализация `GroqAIProvider` / `OllamaAIProvider`.
  - Генерация кратких и понятных заголовков (например: *"Нажмите 'Сохранить' в верхнем меню Блокнота"*).
  - Защита данных: отправка минимального контекста (`targetName`, `controlType`, `windowTitle`), полный запрет отправки скриншотов без согласия.
  - Возможность мгновенного отката сгенерированных AI описаний к исходным телеметрическим данным.

---

## 7. Критерии приемки готовности фич (Definition of Done)

Фича считается завершенной **только при выполнении следующих условий**:
1. Код успешно компилируется без предупреждений (0 warnings, 0 errors).
2. Написаны модульные и интеграционные тесты (xUnit), и все они зеленые (`dotnet test`).
3. Реальное поведение проверено в среде Windows (с валидацией скриншотов или UIA деревьев).
4. Предусмотрена и протестирована обработка отказов (мертвые окна, неинтерактивные десктопы, отсутствие UIA).
5. Не допущено утечек ресурсов (дескрипторы GDI, DC, Bitmaps, соединения SQLite освобождаются через `using` / `Dispose`).
6. Документация и чеклист `specs/spec.md` актуализированы.
7. Изменения зафиксированы и отправлены в репозиторий через `./sync.sh`.
