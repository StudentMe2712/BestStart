# Architecture — Техническая архитектура и системный дизайн Second Self

**Документ:** `02_Technical/Architecture.md`
**Этап:** 3. Технический анализ
**Статус:** Версия 1.0 (На согласовании)

## 1. Технологический стек и обоснование выбора

Second Self проектируется как высокопроизводительное, легковесное десктопное приложение для Windows с нулевой зависимостью от сторонних веб-серверов или облачных подписок (Local-First).

| Компонент системы | Выбранная технология | Обоснование выбора и преимущества |
| :--- | :--- | :--- |
| **Язык и среда выполнения** | **C# 14 / .NET 10** | Современный компилятор с поддержкой Native AOT / ReadyToRun, минимальное время холодного старта (<250 мс), низкое потребление RAM (<60 МБ в простое) и поддержка новейших языковых возможностей. |
| **UI-фреймворк** | **WinUI 3 (Windows App SDK 1.7+)** | Официальный нативный UI-фреймворк Microsoft для Windows 10/11. Обеспечивает нативный рендеринг через DirectX 12, аппаратное ускорение и доступ к Windows App SDK APIs. |
| **Графика и анимации** | **WinUI Composition (Visual Layer)** | Аппаратные плавные 60–120 FPS анимации сжатия/раскрытия окна, эффекты размытия фона (`BackdropBrush` / Acrylic) и управление прозрачностью без просадки UI-потока. |
| **Медиаплеер** | **Windows Media Playback (`MediaPlayerElement`)** | Встроенный нативный мультимедийный движок Windows (Media Foundation) с аппаратным декодированием H.264/HEVC/VP9 через GPU, нулевой нагрузкой на CPU и точным позиционированием кадра. |
| **Хранилище данных** | **SQLite 3 + Microsoft.Data.Sqlite** | Локальная реляционная СУБД в режиме WAL (Write-Ahead Logging). Обеспечивает транзакционную надёжность (ACID), мгновенный отклик (<1 мс на запрос) и простоту резервного копирования в один файл. |
| **Системная интеграция** | **Win32 P/Invoke / CsWin32** | Прямой вызов Win32 API для низкоуровневого управления окнами (`SetWindowLong`, `DwmExtendFrameIntoClientArea`, `IVirtualDesktopManager`, `SHQueryUserNotificationState`). |
| **Архитектурный паттерн** | **Clean Architecture + MVVM** | Библиотека `CommunityToolkit.Mvvm` (Source Generators), слабая связанность компонентов, поддержка DI (`Microsoft.Extensions.DependencyInjection`). |

---

## 2. Архитектура приложения и управление окнами

Приложение функционирует в рамках **единого процесса** (`SecondSelf.exe`), исключая накладные расходы на межпроцессное взаимодействие (IPC), но разделено на два независимых слоя оконного интерфейса:

```text
                               ┌────────────────────────────────────────────────┐
                               │           SecondSelf.exe (.NET 10)             │
                               │                                                │
                               │  ┌──────────────────────────────────────────┐  │
                               │  │ AppHost / Dependency Injection Container │  │
                               │  └────────────────────┬─────────────────────┘  │
                               └───────────────────────┼────────────────────────┘
                                                       │
            ┌──────────────────────────────────────────┴──────────────────────────────────────────┐
            ▼                                                                                     ▼
┌───────────────────────────────┐                                             ┌───────────────────────────────────────┐
│       Desktop Widget          │                                             │          Personal Dashboard           │
│  (Window: Borderless/Popup)   │                                             │        (Window: Standard App)         │
├───────────────────────────────┤                                             ├───────────────────────────────────────┤
│ • WS_POPUP, WS_EX_TOOLWINDOW  │                                             │ • Стандартный хром Windows 11 Snap    │
│ • IVirtualDesktopManager      │                                             │ • Размеры 1120×780 (Resizable)        │
│ • Always On Top (Опционально) │                                             │ • 5 вкладок (Today, Goals, Progress,  │
│ • Детектор Fullscreen-приложений│                                           │   Archive, Settings)                  │
│ • Плеер Daily Reset           │                                             │ • Скрытие в трей при клике на крестик │
└───────────────┬───────────────┘                                             └───────────────────┬───────────────────┘
                │                                                                                 │
                └──────────────────────────────┬──────────────────────────────────────────────────┘
                                               │
                                               ▼
                              ┌───────────────────────────────────┐
                              │     Domain & Application Core     │
                              │ (Services, ViewModels, Mediators) │
                              ├───────────────────────────────────┤
                              │ • TaskService                     │
                              │ • VideoPlaybackService            │
                              │ • ProgressCalculationEngine       │
                              │ • GoalMilestoneService            │
                              │ • SystemMonitorService            │
                              └─────────────────┬─────────────────┘
                                                │
                                                ▼
                              ┌───────────────────────────────────┐
                              │        Data & Storage Layer       │
                              ├───────────────────────────────────┤
                              │ • SQLite Database (secondself.db) │
                              │ • WAL Mode & Backup Service       │
                              │ • Local Video File System         │
                              └───────────────────────────────────┘
```

### 2.1. Низкоуровневая спецификация окна Desktop Widget (Win32 Interop)
Чтобы плавающий виджет вел себя как органичный нативный элемент рабочего стола Windows (а не обычное диалоговое окно), применяются специальные оконные стили:

```csharp
// Инициализация окна виджета в WinUI 3
var hWnd = WindowNative.GetWindowHandle(widgetWindow);
var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
var appWindow = AppWindow.GetFromWindowId(windowId);

// 1. Убираем стандартную рамку и заголовок
int style = PInvoke.GetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
style &= ~(int)(WINDOW_STYLE.WS_CAPTION | WINDOW_STYLE.WS_THICKFRAME | WINDOW_STYLE.WS_MINIMIZEBOX | WINDOW_STYLE.WS_MAXIMIZEBOX);
PInvoke.SetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, style);

// 2. Исключаем из Alt+Tab переключателя задач
int exStyle = PInvoke.GetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
exStyle |= (int)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW;
PInvoke.SetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, exStyle);

// 3. Закрепляем на всех виртуальных рабочих столах Windows (Win+Tab)
var vdm = (IVirtualDesktopManager)new VirtualDesktopManager();
vdm.PinWindow(hWnd);
```

### 2.2. Позиционирование и мультимониторность
* Координаты виджета сохраняются в файле `Settings/window_state.json` в нормализованном виде относительно конкретного монитора: `DisplayId`, `DpiScale`, `NormalizedX = (ScreenX - MonitorLeft) / MonitorWidth`, `NormalizedY = (ScreenY - MonitorTop) / MonitorHeight`.
* При перезапуске приложения или изменении конфигурации дисплеев (`WM_DISPLAYCHANGE`) координаты пересчитываются с учётом актуального рабочего пространства (`WorkArea`), исключая улетание виджета за пределы экрана при отключении второго монитора.

### 2.3. Детекция полноэкранных приложений (Fullscreen Detection)
Для того чтобы виджет не мешал пользователю во время игр, просмотра фильмов или презентаций:
* Фоновая служба `SystemMonitorService` с интервалом `1000 мс` проверяет статус Windows через системный API `SHQueryUserNotificationState`:
  * Если возвращается `QUNS_RUNNING_D3D_FULL_SCREEN` или активное окно в фокусе (`GetForegroundWindow`) имеет размеры, равные или превышающие габариты текущего монитора, виджет временно снимает флаг `HWND_TOPMOST` или переходит в скрытое состояние (`Opacity = 0`).
  * При выходе из полноэкранного режима виджет плавно восстанавливает отображение поверх окон.

---

## 3. Системный трей и управление жизненным циклом

* **Интеграция с областью уведомлений Windows (System Tray):**
  * Используется Win32 `Shell_NotifyIconW` с поддержкой адаптивных темной/светлой иконок.
  * Контекстное меню трея:
    * *Открыть Personal Dashboard* (жирный пункт по умолчанию / двойной клик)
    * *Показать / Скрыть виджет*
    * *Быстрое добавление задачи (`Alt+Shift+N`)*
    * *Настройки*
    * *Выход*
* **Автозапуск с Windows:**
  * Реализуется через ветку реестра `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` с ключом `SecondSelf` и параметром `--startup-background` (при старте запускается только виджет и трей, а Dashboard остаётся закрытым до явного вызова пользователем).
* **Жизненный цикл фоновой работы:**
  * Приложение перехватывает системные сообщения `WM_QUERYENDSESSION` / `WM_ENDSESSION` для гарантированного сброса кэша SQLite на диск перед выключением ПК.

---

## 4. Архитектура сервисов и компонентов (Dependency Injection)

Архитектура строго декомпозирована на слабосвязанные сервисы, зарегистрированные как `Singleton` в DI-контейнере:

```text
SecondSelf.Core/
├── Services/
│   ├── ITaskService.cs                # CRUD задач, логика выполнения, таймер Undo
│   ├── IVideoLibraryService.cs        # Сканирование Videos/, учет IsMissing
│   ├── IFisherYatesShuffleService.cs  # Shuffle без повторений CycleQueue
│   ├── IDayTransitionService.cs       # Смена суток в 04:00, привязка видео на дату
│   ├── IVideoPlaybackService.cs       # Очередь Shuffle, плеер, таймкоды, валидация файлов
│   ├── IProgressCalculationEngine.cs   # Расчет формулы месяца (30/70%), ISO-8601 четверги
│   ├── IGoalMilestoneService.cs       # Большие цели (до 4), этапы, связка с Weekly Goal
│   ├── IBackupService.cs              # Ежедневные снапшоты SQLite, ротация 14 дней
│   ├── ISystemTrayService.cs          # Системный трей Shell_NotifyIconW, контекстное меню
│   ├── IAutostartService.cs           # Автозапуск через HKCU\...\Run (--startup-background)
│   ├── IWindowStateService.cs         # Координаты виджета, DpiScale, сохранение в window_state.json
│   ├── ISystemMonitorService.cs       # Отслеживание мониторов, DPI, Fullscreen приложений
│   └── INotificationBus.cs            # Внутрипроцессная шина событий (Messenger)
├── ViewModels/
│   ├── WidgetViewModel.cs             # Компактный и раскрытый режим виджета
│   ├── VideoPlayerViewModel.cs        # Плеер Daily Reset, таймкод, кнопка "Засчитать"
│   ├── DashboardViewModel.cs          # Навигация и контейнер Personal Dashboard
│   ├── GoalsViewModel.cs              # Сетка Больших целей и дорожная карта этапов
│   └── ProgressViewModel.cs           # Аналитика месяца и экран Monthly Recap
```

### Внутрипроцессная шина событий (`INotificationBus`)
Виджет и Dashboard работают с одними и теми же данными. Чтобы не делать тяжёлых опросов базы, используется реактивная шина `WeakReferenceMessenger` (CommunityToolkit):
* Событие `TaskCompletedEvent`: когда задача закрывается в виджете, счётчик задач дня в открытом окне Dashboard мгновенно инкрементируется без перезагрузки экрана.
* Событие `DailyRitualCountedEvent`: когда в плеере нажимается кнопка «Засчитать выполнение», полоса прогресса месяца одновременно анимируется и в виджете, и в Dashboard.
* Событие `DayChangedEvent`: при наступлении `04:00` утра таймер генерирует событие смены суток, инициируя выбор нового видео и пересчёт активного дня.
