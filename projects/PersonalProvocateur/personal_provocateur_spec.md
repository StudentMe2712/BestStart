# СПЕЦИФИКАЦИЯ ПРОЕКТА: Personal Provocateur (`personal_provocateur_spec.md`)

> **СТАТУС ДОКУМЕНТА:** Единый источник истины (Single Source of Truth) для Оркестратора и Субагентов Antigravity.
> **ЛОКАЦИЯ ПРОЕКТА:** `projects/PersonalProvocateur/` (внутри репозитория `BestStart`).
> **СТЕК:** C# 13 | .NET 10 LTS (`net10.0-windows`) | WPF + XAML | Win32 P/Invoke | JSON Storage | xUnit.

## 0. Жесткие правила для Оркестратора и Субагентов (Anti-Drift Guardrails)

1. **Ролевая модель Antigravity:**
   * **Оркестратор (Tech Lead / Архитектор):** Не пишет продуктовый код напрямую, а декомпозирует задачи текущего этапа, делегирует их субагентам, проверяет соответствие сигнатур этой спецификации, запускает `dotnet build` / `dotnet test`, отмечает выполненные пункты `[ ]` → `[x]` в этом файле и в конце спринта вызывает `./sync.sh`.
   * **Субагенты (Разработчики):** Строго следуют структуре папок, именам классов, интерфейсам и архитектурным решениям (ADR), зафиксированным ниже.
2. **Изоляция воркспейса:**
   * Все файлы решения создаются и изменяются **строго внутри `projects/PersonalProvocateur/`**.
   * Выход за пределы директории проекта запрещен (единственное исключение — запуск глобального `./sync.sh` из корня `BestStart` в финале спринта).
3. **Запрет на сторонние библиотеки (Zero Third-Party Dependencies в MVP):**
   * В проекте `PersonalProvocateur.App` **запрещено** устанавливать внешние NuGet-пакеты (`Hardcodet.NotifyIcon.Wpf`, `CommunityToolkit.Mvvm`, `Newtonsoft.Json`, `Prism`, `MaterialDesignThemes` и т.д.).
   * Используются только штатные средства платформы `.NET 10` (`System.Text.Json`, `WPF`, `UseWindowsForms` для иконки трея, `P/Invoke user32.dll`).
   * В тестовом проекте `PersonalProvocateur.Tests` разрешены только стандартные пакеты `Microsoft.NET.Test.Sdk` и `xunit` (`xunit.runner.visualstudio`).
4. **Правила WPF-верстки и рендеринга (замена веб-правил DOM/SafeArea):**
   * **Никаких захардкоженных разрешений экрана:** Запрещено фиксировать размеры окон или координат под `1920x1080`. Использовать `SystemParameters.PrimaryScreenWidth` и `SystemParameters.PrimaryScreenHeight`, адаптивные `Grid`, `Viewbox` и относительные отступы с учетом системного DPI.
   * **Разделение прозрачности и видео (Airspace Rule):** Запрещено помещать `MediaElement` внутрь окна с `AllowsTransparency="True"`. Затемнение и персонаж живут в прозрачном `OverlayWindow` (`AllowsTransparency="True"`), а видеоплеер — в отдельном дочернем `VideoWindow` (`AllowsTransparency="False"`, `WindowStyle="None"`).
   * **Потокобезопасность UI:** Любые вызовы из таймеров, событий питания ОС (`SystemEvents`) или Win32-колбэков должны обращаться к элементам WPF только через `Application.Current.Dispatcher.InvokeAsync(...)`.

## 1. Продуктовая концепция и Бизнес-логика

**Personal Provocateur** — нативное настольное приложение для Windows 10/11, которое работает в фоновом режиме (в системном трее), в случайные моменты времени затемняет экран, показывает поверх всех окон анимированного 2D-персонажа (фиолетового провокатора) и после клика по персонажу запускает случайное мотивационное видео из локальной библиотеки пользователя.

### Машина состояний сессии (`SessionState`)

| Состояние | Описание поведения системы | Переходы |
| :--- | :--- | :--- |
| **`BackgroundIdle`** | Приложение свернуто в системный трей. Активных оверлеев нет. `RandomReminderScheduler` отсчитывает случайный интервал в диапазоне `[MinIntervalMinutes, MaxIntervalMinutes]`. | → `MascotOverlayActive` (сработал таймер или нажата горячая клавиша вызова)<br>→ `Paused` (пользователь включил паузу в трее или экран Windows заблокирован) |
| **`Paused`** | Таймер остановлен. Новые сессии по расписанию не запускаются (ручной тестовый запуск из настроек или трея остается доступным). | → `BackgroundIdle` (снятие паузы / разблокировка сессии Windows с расчетом свежего интервала) |
| **`MascotOverlayActive`** | Открыто полноэкранное окно `OverlayWindow` с полупрозрачным черным фоном (`DimmingOpacity`, по умолчанию `0.60`). Персонаж появляется с анимацией (`Peek` → `Idle` / `Attention`). **Персонаж НЕ исчезает сам по себе**, пока пользователь не кликнет по нему. | → `VideoPlaying` (клик мышью по персонажу)<br>→ `BackgroundIdle` (нажата аварийная горячая клавиша сброса или выбран пункт «Скрыть оверлей» в трее) |
| **`VideoPlaying`** | После короткой анимации реакции (`Clicked`) поверх `OverlayWindow` открывается `VideoWindow` и воспроизводит случайный ролик из `VideoShuffleBag`. Затемнение под видео сохраняется. | → `BackgroundIdle` (ролик доиграл до конца, нажата кнопка закрытия/пропуска, произошла ошибка медиафайла или нажат аварийный хоткей). При переходе в `BackgroundIdle` оба окна закрываются и запускается новый случайный таймер. |

## 2. Архитектура решения и Структура файлов

Вся разработка ведется в следующей структуре каталогов:

```text
projects/PersonalProvocateur/
├── personal_provocateur_spec.md
├── PersonalProvocateur.sln
├── src/
│   └── PersonalProvocateur.App/
│       ├── PersonalProvocateur.App.csproj
│       ├── App.xaml
│       ├── App.xaml.cs
│       ├── Models/
│       │   ├── AppSettings.cs
│       │   ├── VideoItem.cs
│       │   ├── MascotState.cs
│       │   └── SessionState.cs
│       ├── Storage/
│       │   ├── AppPaths.cs
│       │   └── SettingsStore.cs
│       ├── Services/
│       │   ├── SingleInstanceGuard.cs
│       │   ├── VideoShuffleBag.cs
│       │   ├── VideoLibraryService.cs
│       │   ├── RandomReminderScheduler.cs
│       │   ├── OverlayCoordinator.cs
│       │   └── MascotAssetService.cs
│       ├── WindowsInterop/
│       │   ├── NativeMethods.cs
│       │   ├── GlobalHotkeyService.cs
│       │   ├── TrayIconService.cs
│       │   ├── StartupManager.cs
│       │   └── SystemPowerWatcher.cs
│       ├── UI/
│       │   ├── Theme/
│       │   │   └── DarkTheme.xaml
│       │   ├── Controls/
│       │   │   ├── MascotControl.xaml
│       │   │   └── MascotControl.xaml.cs
│       │   ├── OverlayWindow.xaml
│       │   ├── OverlayWindow.xaml.cs
│       │   ├── VideoWindow.xaml
│       │   ├── VideoWindow.xaml.cs
│       │   ├── TrayPopupMenu.xaml
│       │   ├── TrayPopupMenu.xaml.cs
│       │   ├── SettingsWindow.xaml
│       │   └── SettingsWindow.xaml.cs
│       └── Assets/
│           └── Mascot/
│               ├── idle.png
│               ├── peek.png
│               ├── attention.png
│               └── clicked.png
└── tests/
    └── PersonalProvocateur.Tests/
        ├── PersonalProvocateur.Tests.csproj
        ├── VideoShuffleBagTests.cs
        ├── RandomReminderSchedulerTests.cs
        └── SettingsStoreTests.cs
```

### Строгие контракты моделей и сервисов (Data & Service Contracts)

Чтобы субагенты не расходились в именовании, зафиксированы следующие сигнатуры:

#### 1. `Models/AppSettings.cs`
* `int MinIntervalMinutes { get; set; } = 15;` (валидация: `>= 1` и `<= MaxIntervalMinutes`)
* `int MaxIntervalMinutes { get; set; } = 45;` (валидация: `>= MinIntervalMinutes` и `<= 480`)
* `double DimmingOpacity { get; set; } = 0.60;` (валидация: `Math.Clamp(value, 0.10, 0.95)`)
* `string TriggerHotkey { get; set; } = "Ctrl+Alt+P";` (ручной вызов персонажа)
* `string PanicHotkey { get; set; } = "Ctrl+Alt+Shift+P";` (аварийное скрытие активной сессии)
* `bool RunOnStartup { get; set; } = false;` (по умолчанию выключено согласно FR-09)
* `bool IsPaused { get; set; } = false;`
* `double VideoVolume { get; set; } = 0.85;` (диапазон `0.0..1.0`)
* `List<VideoItem> Videos { get; set; } = new();`

#### 2. `Models/VideoItem.cs`
* `Guid Id { get; set; } = Guid.NewGuid();`
* `string Title { get; set; } = string.Empty;`
* `string FilePath { get; set; } = string.Empty;`
* `DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;`
* `bool IsEnabled { get; set; } = true;`

#### 3. `Models/MascotState.cs` и `Models/SessionState.cs`
* `enum MascotState { Hidden, Peek, Idle, Attention, Clicked }`
* `enum SessionState { BackgroundIdle, Paused, MascotOverlayActive, VideoPlaying }`

#### 4. `Services/VideoShuffleBag.cs`
* Универсальный детерминированно тестируемый класс (принимает опциональный `Random? random = null` в конструкторе).
* Методы:
  * `void SyncPool(IEnumerable<VideoItem> availableVideos)` — обновляет пул доступных существующих и включенных роликов (`IsEnabled && File.Exists(FilePath)`).
  * `VideoItem? Next()` — возвращает следующий случайный ролик из перемешанной очереди. Когда очередь пустеет, заново перемешивает актуальный пул по алгоритму Фишера-Йетса. Гарантирует: последний показанный ролик предыдущего цикла не станет первым роликом нового цикла (если в пуле `>= 2` видео).

#### 5. `Services/RandomReminderScheduler.cs`
* Отвечает исключительно за отсчет случайного интервала и реакцию на паузу/сон ОС.
* Свойства и события:
  * `event EventHandler? TimerTriggered;`
  * `DateTime? NextTriggerTimeLocal { get; }`
  * `bool IsSessionActive { get; set; }`
  * `bool IsPaused { get; }`
* Методы:
  * `void StartNextCycle(int minMinutes, int maxMinutes)` — запускает новый случайный таймер только если `!IsSessionActive && !IsPaused`.
  * `void Stop()` — останавливает текущий отсчет.
  * `void SetPaused(bool paused, int minMinutes, int maxMinutes)` — ставит на паузу или возобновляет со свежим интервалом.
  * `void OnSystemSuspendOrLock()` / `void OnSystemResumeOrUnlock(int minMinutes, int maxMinutes)` — предотвращает немедленный залп напоминаний после выхода ПК из сна.

#### 6. `Services/OverlayCoordinator.cs`
* Центральный дирижер пользовательской сессии (`SessionState`).
* Гарантирует, что на экране не может быть двух активных сессий одновременно.
* Методы:
  * `void TriggerProvocation(bool isManualTest = false)` — если сессия уже активна, игнорирует вызов; иначе останавливает планировщик, переводит состояние в `MascotOverlayActive` и открывает `OverlayWindow`.
  * `void OnMascotClicked()` — переводит состояние в `VideoPlaying`, выбирает валидный видеофайл через `VideoLibraryService` / `VideoShuffleBag` и открывает `VideoWindow` поверх `OverlayWindow`. Если видеотека пуста или файлы недоступны — показывает аккуратное всплывающее уведомление на оверлее с кнопкой перехода в настройки и корректно завершает сессию.
  * `void CompleteSession()` — закрывает `VideoWindow` и `OverlayWindow`, возвращает состояние в `BackgroundIdle` и вызывает `scheduler.StartNextCycle(...)`.
  * `void EmergencyDismiss()` — мгновенно закрывает все окна оверлея/видео по аварийному хоткею и перезапускает цикл планировщика.

## 3. Записи архитектурных решений (ADR)

### ADR-001: Гибридный системный трей (Нативный `NotifyIcon` + кастомное WPF-меню в темной теме)
* **Контекст:** В чистом WPF отсутствует контрол системного трея, а требование ТЗ запрещает сторонние библиотеки. При этом стандартное контекстное меню Windows Forms (`ContextMenuStrip`) выглядит устаревшим и визуально конфликтует с современным интерфейсом Windows 11.
* **Решение:** В `PersonalProvocateur.App.csproj` включается `<UseWindowsForms>true</UseWindowsForms>` исключительно ради класса `System.Windows.Forms.NotifyIcon` и генерации иконки в области уведомлений. При правом клике по иконке трея открывается **нативное безрамочное окно/попап WPF (`TrayPopupMenu.xaml`)**, стилизованное под современную темную тему (`DarkTheme.xaml` со скругленными углами, плавной подсветкой и статусом следующего срабатывания).
* **Следствие:** 0 сторонних пакетов, 100% надежность иконки в трее и современный внешний вид меню.

### ADR-002: Двухоконная схема `OverlayWindow` + `VideoWindow` для аппаратного ускорения видео
* **Контекст:** В WPF окно с `AllowsTransparency="True"` использует попиксельное смешивание через DWM, из-за чего встроенный `MediaElement` внутри такого окна может терять аппаратное ускорение или страдать от просадок FPS на высоких разрешениях.
* **Решение:**
  1. `OverlayWindow` (`WindowStyle="None"`, `AllowsTransparency="True"`, `Topmost="True"`, `ShowInTaskbar="False"`) рисует полупрозрачную черную подложку (`SolidColorBrush` с альфа-каналом из `DimmingOpacity`) и размещает `MascotControl`.
  2. При клике по персонажу создается `VideoWindow` (`WindowStyle="None"`, `AllowsTransparency="False"`, `Owner = overlayWindow`, `WindowStartupLocation="CenterOwner"`), которое занимает 75–80% площади экрана поверх затемнения с красивой темной рамкой и тенью.
* **Следствие:** Идеальная плавность прозрачной анимации персонажа и полноценное аппаратное декодирование H.264/MP4 в `MediaElement`.

### ADR-003: Автономная графика персонажа (Векторный WPF-фоллбэк + автогенерация `Assets/Mascot/*.png`)
* **Контекст:** По ТЗ персонаж использует PNG-изображения (`idle.png`, `peek.png`, `attention.png`, `clicked.png`) и `Storyboard`-анимации, но при автоматической сборке субагентами внешние дизайнерские PNG могут еще не быть загружены пользователем.
* **Решение:** Сервис `MascotAssetService` при запуске проверяет наличие файлов `idle.png`, `peek.png`, `attention.png`, `clicked.png` в папке `Assets/Mascot/`. Если файлы отсутствуют, сервис программно рендерит и сохраняет стильные PNG-спрайты с прозрачным фоном (фиолетовое существо с выразительными глазами и ехидной мимикой в 4 состояниях) с помощью `RenderTargetBitmap` / `DrawingVisual`. Если пользователь заменит эти PNG своими арт-файлами с теми же именами, приложение автоматически подхватит пользовательскую графику без перекомпиляции.
* **Следствие:** Приложение на 100% работоспособно и визуально привлекательно сразу после первого билда, и при этом полностью готово к кастомизации художником.

### ADR-004: Защита от случайного закрытия и перехвата ввода (`Focus & Alt+F4 Guard`)
* **Контекст:** Внезапное появление `Topmost`-окна во время набора текста пользователем может привести к тому, что нажатие `Space`/`Enter` активирует кнопку, а рефлекторное `Alt+F4` закроет оверлей без просмотра видео.
* **Решение:**
  1. В `OverlayWindow` все контролы имеют `Focusable="False"` и `IsTabStop="False"`, реакция идет только на явный клик мышью (`MouseLeftButtonUp`) по `MascotControl`.
  2. В `OverlayWindow.OnClosing` событие `e.Cancel = true` блокирует закрытие окна, пока `OverlayCoordinator` явно не установит внутренний флаг `CanClose = true` (после завершения видео или по аварийному хоткею `PanicHotkey`).

### ADR-005: Кросс-окруженческая совместимость сборки в `.csproj`
* **Контекст:** Для стабильной работы инструментов проверки и сборки в любых средах `.NET 10 SDK` проект должен явно декларировать поддержку Windows-таргетинга.
* **Решение:** В оба файла `.csproj` (`PersonalProvocateur.App.csproj` и `PersonalProvocateur.Tests.csproj`) включаются свойства `<TargetFramework>net10.0-windows</TargetFramework>` и `<EnableWindowsTargeting>true</EnableWindowsTargeting>`.

## 4. Чеклист майлстоунов (Разбивка по Спринтам для Antigravity)

### СПРИНТ 1: Инициализация решения, Ядро логики, Хранилище и Unit-тесты
- [x] **1.1. Структура решения и `.csproj`:** Создать `PersonalProvocateur.sln`, проект `src/PersonalProvocateur.App/PersonalProvocateur.App.csproj` (`net10.0-windows`, `UseWPF=true`, `UseWindowsForms=true`, `EnableWindowsTargeting=true`, `OutputType=WinExe`) и тестовый проект `tests/PersonalProvocateur.Tests/PersonalProvocateur.Tests.csproj` (`xUnit`).
- [x] **1.2. Модели и Хранилище (`Models/` & `Storage/`):** Реализовать `AppSettings`, `VideoItem`, `MascotState`, `SessionState`, `AppPaths` (`%LOCALAPPDATA%\PersonalProvocateur\settings.json`) и потокобезопасный `SettingsStore` с автосохранением и дефолтной инициализацией при поврежденном JSON.
- [x] **1.3. Алгоритм `VideoShuffleBag` и `VideoLibraryService`:** Реализовать `VideoShuffleBag` (алгоритм Фишера-Йетса без повтора на стыке циклов) и сервис управления списком локальных MP4-файлов с фильтрацией удаленных/отсутствующих файлов.
- [x] **1.4. Планировщик `RandomReminderScheduler`:** Реализовать генерацию случайных интервалов, блокировку параллельных сессий и методы обработки паузы и сна/блокировки ОС.
- [x] **1.5. Покрытие Unit-тестами и Верификация Спринта 1:** Написать и прогнать тесты `VideoShuffleBagTests`, `RandomReminderSchedulerTests`, `SettingsStoreTests` (`dotnet test`), убедиться в чистой сборке `dotnet build -c Release` и выполнить `./sync.sh`.

### СПРИНТ 2: Фоновый режим, Win32 Интероп, Системный трей и Технический прототип окон
- [x] **2.1. Защита от повторного запуска (`SingleInstanceGuard`):** Реализовать глобальный `Mutex` + `EventWaitHandle` для передачи сигнала уже запущенному экземпляру (при повторном запуске открывается `SettingsWindow` активного процесса).
- [x] **2.2. Win32 Interop (`NativeMethods`, `GlobalHotkeyService`, `StartupManager`, `SystemPowerWatcher`):** Реализовать P/Invoke `RegisterHotKey` / `UnregisterHotKey` с проверкой занятости сочетания клавиш, управление автозапуском в `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` и подписку на `SystemEvents.SessionSwitch` / `PowerModeChanged`.
- [x] **2.3. Системный трей (`TrayIconService` + `TrayPopupMenu`):** Создать программную иконку трея и стильное WPF-меню трея с пунктами: «Вызвать провокатора сейчас», «Пауза / Возобновить», «Настройки...», «Аварийный сброс оверлея», «Выход».
- [x] **2.4. Прототип `OverlayWindow`, `VideoWindow` и `OverlayCoordinator`:** Реализовать полноэкранное полупрозрачное `OverlayWindow` (с защитой от `Alt+F4`), дочернее `VideoWindow` с `MediaElement` (обработка `MediaOpened`, `MediaEnded`, `MediaFailed`) и связку через `OverlayCoordinator`.
- [x] **2.5. Верификация Спринта 2:** Проверить компиляцию `dotnet build -c Release --warnaserror`, прогнать `dotnet test`, актуализировать чекбоксы в `personal_provocateur_spec.md` и выполнить `./sync.sh`.

### СПРИНТ 3: 2D-Персонаж, Генератор спрайтов и WPF Storyboard-анимации
- [x] **3.1. Генератор и загрузчик спрайтов (`MascotAssetService`):** Реализовать проверку и автогенерацию 4 высококачественных прозрачных PNG-спрайтов фиолетового персонажа (`idle.png`, `peek.png`, `attention.png`, `clicked.png`) в `Assets/Mascot/` при их отсутствии, а также загрузку с `CacheOption = BitmapCacheOption.OnLoad`.
- [x] **3.2. Компонент персонажа (`MascotControl.xaml`):** Сверстать интерактивный контрол персонажа с поддержкой `RenderTransform` (`ScaleTransform`, `RotateTransform`, `TranslateTransform`), бабблом провокационной подсказки («Ну давай, нажми меня!») и привязкой к состояниям `MascotState`.
- [x] **3.3. Программные и XAML-анимации (`Storyboard`):** Реализовать 4 плавные анимации:
  * `Peek` — плавное выглядывание из-за края экрана с легким наклоном;
  * `Idle` — мягкое «дыхание» и покачивание;
  * `Attention` — периодическое подпрыгивание и легкая пульсация каждые несколько секунд ожидания;
  * `Clicked` — резкий скейл-импульс и радостно-ехидная реакция перед открытием `VideoWindow`.
- [x] **3.4. Рандомизация позиции появления:** Настроить в `OverlayWindow` выбор случайной точки/угла появления персонажа в пределах безопасной зоны основного монитора, чтобы пользователь не кликал мышечной памятью в одну точку.
- [x] **3.5. Верификация Спринта 3:** Проверить сборку и тесты, убедиться в отсутствии утечек памяти анимаций при закрытии `OverlayWindow`, обновить `personal_provocateur_spec.md` и выполнить `./sync.sh`.

### СПРИНТ 4: Окно настроек, Полиш UI, Обработка крайних случаев и Финализация MVP
- [x] **4.1. Единая дизайн-система (`DarkTheme.xaml`):** Оформить современную темную тему (глубокий графитовый фон `#12131A`, акцентный фиолетовый `#8B5CF6`, аккуратные карточки, кастомные кнопки, слайдеры и списки).
- [x] **4.2. Окно настроек (`SettingsWindow.xaml`):** Реализовать полноценный дашборд управления:
  * Блок **Статус и Тест:** индикатор состояния планировщика, время следующего появления, кнопки «Тест оверлея» и «Пауза».
  * Блок **Библиотека видео (FR-02):** добавление нескольких `.mp4` через `OpenFileDialog`, отображение статуса файла (доступен / не найден на диске), включение/отключение ролика, тестовый предпросмотр выбранного видео и удаление из списка.
  * Блок **Расписание и Затемнение (FR-03, FR-04):** настройка `MinIntervalMinutes` и `MaxIntervalMinutes` с валидацией, ползунок прозрачности затемнения (`10%–95%`) и громкости видео.
  * Блок **Система и Горячие клавиши (FR-07, FR-08, FR-09):** настройка глобального хоткея вызова и аварийного хоткея (с индикацией, если хоткей занят другой программой), тумблер «Запускать вместе с Windows».
- [x] **4.3. Обработка ошибок и граничных сценариев:** Проверить сценарии отсутствующих/битых видеофайлов (автоматический переход к следующему валидному ролику из `VideoShuffleBag` без падения приложения), изменение разрешения экрана во время активного оверлея (`SystemEvents.DisplaySettingsChanged`) и корректное освобождение ресурсов (`Dispose`) при выходе.
- [x] **4.4. Финальная валидация и Синхронизация MVP:** Запустить полную сборку `dotnet build -c Release --warnaserror` и все тесты `dotnet test`, заполнить итоговую сводку в `personal_provocateur_spec.md` и выполнить финальный `./sync.sh`.

## 5. Сводка изменений по спринтам (Заполняется Оркестратором)

| Спринт | Статус | Ключевые созданные / измененные файлы | Результат `dotnet build` & `dotnet test` |
| :--- | :--- | :--- | :--- |
| **Спринт 1** | `[x]` Завершен | `PersonalProvocateur.sln`, `PersonalProvocateur.App.csproj`, `PersonalProvocateur.Tests.csproj`, `App.xaml`, `App.xaml.cs`, `AppSettings.cs`, `VideoItem.cs`, `MascotState.cs`, `SessionState.cs`, `AppPaths.cs`, `SettingsStore.cs`, `VideoShuffleBag.cs`, `VideoLibraryService.cs`, `RandomReminderScheduler.cs`, `SettingsStoreTests.cs`, `VideoShuffleBagTests.cs`, `VideoLibraryServiceTests.cs`, `RandomReminderSchedulerTests.cs`, `AppSettingsTests.cs` | `dotnet build -c Release --warnaserror`: 0 errors, 0 warnings.<br>`dotnet test -c Release`: 28 passed, 0 failed (100% pass rate). |
| **Спринт 2** | `[x]` Завершен | `SingleInstanceGuard.cs`, `NativeMethods.cs`, `GlobalHotkeyService.cs`, `StartupManager.cs`, `SystemPowerWatcher.cs`, `DarkTheme.xaml`, `OverlayCoordinator.cs`, `OverlayWindow.xaml`, `OverlayWindow.xaml.cs`, `VideoWindow.xaml`, `VideoWindow.xaml.cs`, `TrayPopupMenu.xaml`, `TrayPopupMenu.xaml.cs`, `TrayIconService.cs`, `SettingsWindow.xaml`, `SettingsWindow.xaml.cs`, `App.xaml`, `App.xaml.cs`, `OverlayCoordinatorTests.cs`, `GlobalHotkeyParserTests.cs`, `SingleInstanceGuardTests.cs`, `StartupManagerTests.cs`, `SystemPowerWatcherTests.cs` | `dotnet build -c Release --warnaserror`: 0 errors, 0 warnings.<br>`dotnet test -c Release`: 92 passed, 0 failed (100% pass rate). |
| **Спринт 3** | `[x]` Завершен | `MascotAssetService.cs`, `MascotControl.xaml`, `MascotControl.xaml.cs`, `OverlayWindow.xaml`, `OverlayWindow.xaml.cs`, `MascotAssetServiceTests.cs`, `MascotControlTests.cs`, `OverlayWindowTests.cs` | `dotnet build -c Release --warnaserror`: 0 errors, 0 warnings.<br>`dotnet test -c Release`: 124 passed, 0 failed (100% pass rate). |
| **Спринт 4** | `[x]` Завершен | `DarkTheme.xaml`, `SettingsWindow.xaml`, `SettingsWindow.xaml.cs`, `GlobalHotkeyService.cs`, `OverlayCoordinator.cs`, `OverlayWindow.xaml.cs`, `VideoWindow.xaml.cs`, `App.xaml.cs`, `OverlayCoordinatorTests.cs`, `SettingsWindowTests.cs` | `dotnet build -c Release --warnaserror`: 0 errors, 0 warnings.<br>`dotnet test -c Release`: 143 passed, 0 failed (100% pass rate). |
