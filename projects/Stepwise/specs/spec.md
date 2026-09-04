# Stepwise — Архитектурная спецификация и план разработки (Spec-Kit)

> **Статус:** В разработке (MVP 0.1)  
> **Версия:** 0.1.0  
> **Платформа:** Windows 10/11 (x64 / ARM64)  
> **Технологический стек:** C# 13, .NET 9+, WinUI 3 / Windows App SDK, Microsoft UI Automation (UIA), Win32 API (User32 Hook), Windows.Graphics.Capture, SQLite (Microsoft.Data.Sqlite / EF Core).

---

## 1. Введение и Миссия проекта

**Stepwise** — это автономное, высокопроизводительное нативное приложение для Windows, предназначенное для автоматического создания интерактивных пошаговых руководств и обучающих интерактивных инструкций (Walkthrough Guides).

Приложение фиксирует действия пользователя (клики, ввод текста, горячие клавиши), автоматически связывает их с элементами интерфейса целевых приложений через Microsoft UI Automation, делает точечные скриншоты с подсветкой активной области (через Windows.Graphics.Capture / Composition API) и позволяет экспортировать интерактивные руководства в формате JSON, HTML/Web, PDF или проигрывать их через прозрачный интерактивный Overlay поверх рабочего стола.

### Жесткие архитектурные ограничения и принципы:
1. **100% Native Windows Stack:** Исключительно C#, .NET, WinUI 3, Windows App SDK, Win32 API, UIA. Категорически запрещены Electron, Tauri, Python, Node.js, веб-оболочки.
2. **Offline-First Core:** Ядро, захват, сохранение, воспроизведение и экспорт работают на 100% автономно без подключения к сети.
3. **Опциональный AI:** Интеграция с LLM (Groq / Ollama / Local AI) изолирована через `IAIProvider` и используется исключительно для суммаризации, перефразирования шагов и генерации подсказок.
4. **Clean Architecture & Decoupling:**
   - `Stepwise.Core`: Чистая доменная модель (`Step`, `Guide`, `ActionType`), интерфейсы и пайплайны. Никаких прямых зависимостей от конкретных UI-фреймворков.
   - `Stepwise.WindowsIntegration`: Изоляция Win32 P/Invoke, `SetWindowsHookEx` (вынесен в выделенный поток с message pump), Microsoft UI Automation (COM Interop / FlaUI).
   - `Stepwise.Infrastructure`: Доступ к данным (SQLite), локальная файловая система для медиа-ассетов, AI-провайдеры.
   - `Stepwise.UI` / `Stepwise.App`: WinUI 3 Shell с MVVM (CommunityToolkit.Mvvm) и прозрачный оверлей.

---

## 2. Архитектура решения и поток данных (Data Flow)

```mermaid
flowchart TD
    subgraph WindowsOS ["Windows Operating System"]
        MouseHook["Win32 Low-Level Hook (WH_MOUSE_LL)"]
        UIA["Microsoft UI Automation Core / COM"]
        WGC["Windows.Graphics.Capture API"]
    end

    subgraph WindowsIntegration ["Stepwise.WindowsIntegration"]
        HookService["LowLevelMouseHookService (Dedicated Pump Thread)"]
        AutomationService["UIAutomationService (FromPoint / Walker)"]
        CaptureService["WindowCaptureService (DirectX / D3D11)"]
    end

    subgraph CoreEngine ["Stepwise.Core"]
        RecEngine["RecordingPipelineEngine (Channel-based Async Orchestrator)"]
        StepModel["Domain Model: Step & Guide Records"]
        Interfaces["IRecordingEngine, IHookService, IUiaService, ICaptureService"]
    end

    subgraph StorageAI ["Stepwise.Infrastructure"]
        SqliteRepo["GuideRepository (SQLite)"]
        AssetStore["FileSystemAssetStore (PNG/WebP)"]
        AIProvider["IAIProvider (NullProvider / GroqProvider)"]
    end

    subgraph Presentation ["Stepwise.UI (WinUI 3)"]
        ShellView["Shell & Library View"]
        EditorView["Visual Step Editor View"]
        OverlayView["Guide Player Transparent Overlay"]
    end

    MouseHook -->|WM_LBUTTONDOWN (X,Y)| HookService
    HookService -->|Async Event/Channel| RecEngine
    RecEngine -->|Resolve Point (X,Y)| AutomationService
    AutomationService -->|COM Queries| UIA
    RecEngine -->|Capture Target Rect| CaptureService
    CaptureService -->|DirectX Texture / Image| WGC
    RecEngine -->|Construct Immutable Step| StepModel
    StepModel -->|Persist| SqliteRepo
    StepModel -->|Display/Edit| EditorView
    StepModel -->|Replay Overlay| OverlayView
```

---

## 3. Доменная модель (Data Contracts)

### `ActionType`
```csharp
public enum ActionType
{
    LeftClick,
    RightClick,
    DoubleLeftClick,
    MiddleClick,
    DragAndDrop,
    KeyPress,
    TextInput
}
```

### `BoundingBox`
```csharp
public readonly record struct BoundingBox(double X, double Y, double Width, double Height);
```

### `ElementInfo`
```csharp
public sealed record ElementInfo(
    string Name,
    string ControlType,
    string AutomationId,
    string ClassName,
    string ProcessName,
    int ProcessId,
    string WindowTitle,
    nint WindowHandle,
    BoundingBox BoundingRectangle
);
```

### `Step`
```csharp
public sealed record Step(
    Guid Id,
    int SequenceIndex,
    DateTime Timestamp,
    ActionType Action,
    double ClickX,
    double ClickY,
    ElementInfo TargetElement,
    string? ScreenshotPath,
    string? Title,
    string? Description,
    Dictionary<string, string>? Metadata
);
```

### `Guide`
```csharp
public sealed record Guide(
    Guid Id,
    string Title,
    string? Description,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<Step> Steps,
    Dictionary<string, string> Tags
);
```

---

## 4. Детальный 11-шаговый план реализации (Roadmap)

Ниже представлен генеральный план разработки системы Stepwise с контрольными чекбоксами:

- [x] **Шаг 1: Архитектура Solution, базовые модели (Step, ActionType) и PoC-консоль.**
  - Создание Solution `Stepwise.sln`.
  - Проекты `Stepwise.Core`, `Stepwise.WindowsIntegration`, `Stepwise.App`.
  - Определение базовых доменных записей (`Step`, `ActionType`, `ElementInfo`, `BoundingBox`).
  - Определение интерфейсов `IRecordingEngine`, `IMouseHookService`, `IUIAutomationService`.

- [x] **Шаг 2: Реализация глобального хука мыши (Win32 API) и сбор координат.**
  - Реализация `SetWindowsHookEx` (`WH_MOUSE_LL`) в `Stepwise.WindowsIntegration`.
  - Запуск хука в изолированном фоновом STA-потоке с нативным Win32 Message Loop (`GetMessage`/`DispatchMessage`).
  - Неблокирующая асинхронная передача событий через `System.Threading.Channels.Channel` или C# `event`.
  - Защита от зависаний системы и хука при задержках в обработчиках.

- [x] **Шаг 3: Интеграция Microsoft UI Automation (получение Name, ControlType, AutomationId по координатам).**
  - Подключение UIA COM Interop / `UIAutomationClient` / `IUIAutomation`.
  - Метод `GetElementFromPoint(int x, int y)` для извлечения `Name`, `ControlType`, `AutomationId`, `ClassName`, `BoundingRectangle`.
  - Извлечение информации о процессе верхнего уровня (`ProcessName`, `ProcessId`, `WindowTitle`, `HWnd`).
  - Безопасная обработка COM-исключений (мертвые окна, повышенные привилегии, UAC границы).

- [x] **Шаг 4: Интеграция Windows.Graphics.Capture (скриншоты).**
  - Интеграция Windows Graphics Capture API (Direct3D11 / WinRT interop).
  - Снятие скриншота конкретного окна или экрана целиком при клике.
  - Кадрирование области вокруг целевого элемента (`BoundingRectangle` + padding).
  - Сохранение изображений в фоновом потоке в PNG / WebP без блокировки основного UI.

- [x] **Шаг 5: Сборка конвейера Hook -> UIA -> Capture -> Step JSON.**
  - Реализация `RecordingPipelineEngine` в `Stepwise.Core`.
  - Координация: `MouseClick` -> `UIA Element Inspection` -> `Frame Capture` -> `Construct Step`.
  - Потоковая сериализация в JSON и валидация целостности данных шагов.

- [x] **Шаг 6: Проектирование локального хранилища (SQLite + File System для assets).**
  - Инициализация локальной базы данных SQLite (`Microsoft.Data.Sqlite`).
  - Таблицы `Guides`, `Steps`, `Metadata`.
  - Файловый сторидж для скриншотов в `%LocalAppData%/Stepwise/assets/`.
  - Репозиторий `IGuideRepository` с CRUD операциями.

- [ ] **Шаг 7: WinUI 3 Shell & MVVM (CommunityToolkit.Mvvm) - базовый UI без логики.**
  - Создание проекта `Stepwise.UI` на базе WinUI 3 / Windows App SDK.
  - Настройка Fluent Design, NavigationView, Mvvm Messenger, ViewModels (`MainViewModel`, `RecordingViewModel`).
  - Статусная панель и индикация состояния записи (Idle, Recording, Paused).

- [ ] **Шаг 8: Визуальный редактор шагов (Editor View).**
  - Экран редактирования руководства: список шагов с превью скриншотов.
  - Редактирование текстовых описаний шагов, названий и подсказок.
  - Изменение порядка шагов (Drag & Drop), удаление лишних кликов, объединение шагов.
  - Редактор аннотаций скриншота (стрелочки, рамки подсветки, размытие конфиденциальных данных).

- [ ] **Шаг 9: Guide Player (режим воспроизведения).**
  - Интерактивный плеер руководства внутри приложения.
  - Пошаговое руководство с кнопками "Далее", "Назад", горячими клавишами.
  - Режим "Инструкция в окне" и режим "Плавающий виджет поверх всех окон".

- [ ] **Шаг 10: Реализация прозрачного Overlay (Composition API) для подсветки элементов.**
  - Создание прозрачного клик-сквозного (Click-Through) окна без рамок (`WS_EX_TRANSPARENT | WS_EX_LAYERED`).
  - Использование Windows Composition API / DirectComposition для плавной подсветки реального элемента на экране пользователя во время выполнения шага.
  - Визуальный маркер / пульсирующая рамка вокруг `BoundingRectangle` активного контрола.

- [ ] **Шаг 11: Интеграция IAIProvider (NullProvider по умолчанию, GroqProvider для суммаризации).**
  - Определение контракта `IAIProvider` (`SummarizeGuideAsync`, `GenerateStepDescriptionAsync`, `RefineInstructionsAsync`).
  - Реализация `NullAIProvider` (работает полностью оффлайн по шаблонам).
  - Реализация `GroqAIProvider` / `OllamaAIProvider` с поддержкой локальных и облачных ключей.
  - Автоматическая генерация человекочитаемых инструкций вида: *"Нажмите кнопку 'Сохранить' в верхнем меню Notepad"*.

---

## 5. Критерии успеха технического среза (MVP 0.1)

1. Консольное приложение `Stepwise.App` перехватывает клики мыши вне своего окна через глобальный Win32 Hook.
2. Обработчик хука не вызывает лагов или зависаний курсора Windows (выделенный message pump thread).
3. По координатам клика корректно определяются: `ProcessName`, `WindowTitle`, `ControlType`, `AutomationId`, `Name`, `BoundingRectangle`.
4. Сформированный `Step` сериализуется в чистый JSON и выводится в консоль в режиме реального времени.
