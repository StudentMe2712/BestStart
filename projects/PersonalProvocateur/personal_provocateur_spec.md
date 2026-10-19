# СПЕЦИФИКАЦИЯ ПРОЕКТА: Personal Provocateur (`personal_provocateur_spec.md`)

> **СТАТУС ДОКУМЕНТА:** Спринты 1–4 завершены. Активен **СПРИНТ 5: macOS Premium UI Overhaul, Top-Notch Mascot Peek & Video UX Fixes**.
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
4. **Правила WPF-верстки, рендеринга и визуального качества (macOS Premium Standard):**
   * **Никаких стандартных белых рамок Windows и `MessageBox.Show`:** Категорически запрещено использовать системный белый TitleBar и вызовы `System.Windows.MessageBox.Show(...)`. Все окна (`SettingsWindow`, `TrayPopupMenu`, `VideoWindow`) должны быть безрамочными (`WindowStyle="None"`) с кастомным темным заголовком и встроенными Toast-уведомлениями.
   * **Никаких текстовых эмодзи вместо иконок:** Категорически запрещено использовать Unicode-эмодзи (`⚡`, `⏸`, `🎬`, `⏱`, `🗑`, `🚪`, `⚙`) в кнопках и заголовках — они рендерятся криво в WPF. Использовать исключительно векторные SVG-пути (`StreamGeometry` / `Path` в стиле Lucide / Apple SF Symbols), определенные в `DarkTheme.xaml`.
   * **Никаких технических кодов ТЗ в интерфейсе:** Запрещено выводить в пользовательский UI метки вроде `(FR-02)`, `(FR-03, FR-04)`.
   * **Четкость рендеринга:** На всех корневых окнах обязательно задавать `UseLayoutRounding="True"`, `SnapsToDevicePixels="True"`, `TextOptions.TextFormattingMode="Ideal"` и `TextOptions.TextRenderingMode="ClearType"`.

## 1. Продуктовая концепция и Бизнес-логика

**Personal Provocateur** — нативное настольное приложение для Windows 10/11 в премиальной эстетике macOS, которое работает в фоновом режиме (в системном трее), в случайные моменты времени затемняет экран, плавно выдвигает **сверху по центру экрана** (в стиле macOS Notch / Dynamic Island) анимированного 2D-персонажа и после клика по персонажу запускает случайное мотивационное видео из локальной библиотеки пользователя.

### Машина состояний сессии (`SessionState`)

| Состояние | Описание поведения системы | Переходы |
| :--- | :--- | :--- |
| **`BackgroundIdle`** | Приложение свернуто в системный трей. Активных оверлеев нет. `RandomReminderScheduler` отсчитывает случайный интервал в диапазоне `[MinIntervalMinutes, MaxIntervalMinutes]`. | → `MascotOverlayActive` (сработал таймер или нажата горячая клавиша вызова)<br>→ `Paused` (пользователь включил паузу в трее или экран Windows заблокирован) |
| **`Paused`** | Таймер остановлен. Новые сессии по расписанию не запускаются (ручной тестовый запуск из настроек или трея остается доступным). | → `BackgroundIdle` (снятие паузы / разблокировка сессии Windows с расчетом свежего интервала) |
| **`MascotOverlayActive`** | Открыто полноэкранное окно `OverlayWindow` с полупрозрачным черным фоном (`DimmingOpacity`). Персонаж плавно выезжает **сверху по центру экрана (`Top-Center`)** с анимацией (`Peek` → `Idle` / `Attention`). **Персонаж НЕ исчезает сам по себе**, пока пользователь не кликнет по нему. | → `VideoPlaying` (клик мышью по персонажу)<br>→ `BackgroundIdle` (нажата аварийная горячая клавиша сброса или выбран пункт «Скрыть оверлей» в трее) |
| **`VideoPlaying`** | После короткой анимации реакции (`Clicked`) персонаж плавно прячется вверх, а поверх `OverlayWindow` по центру открывается `VideoWindow` и воспроизводит случайный ролик из `VideoShuffleBag`. Затемнение вокруг видео сохраняется. | → `BackgroundIdle` (**1)** видео доиграло до конца — автоматическое закрытие; **2)** пользователь кликнул мышью в любое место затемненного фона вне области видео — закрытие по Backdrop Click; **3)** нажата кнопка закрытия `✕` на плеере или аварийный хоткей). При переходе в `BackgroundIdle` оба окна закрываются и автоматически запускается новый случайный таймер `StartNextCycle(...)`. |

## 2. Архитектура решения и Структура файлов

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
│       │   │   ├── DarkTheme.xaml
│       │   │   └── Icons.xaml
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
```

## 3. Записи архитектурных решений (ADR)

### ADR-001: Гибридный системный трей (Нативный `NotifyIcon` + кастомное WPF-меню в темной теме)
* В `PersonalProvocateur.App.csproj` используется `<UseWindowsForms>true</UseWindowsForms>` для `NotifyIcon`. Контекстное меню реализовано нативным безрамочным окном `TrayPopupMenu.xaml` с полной прозрачностью углов (`AllowsTransparency="True"`, `Background="Transparent"`).

### ADR-002: Двухоконная схема `OverlayWindow` + `VideoWindow` для аппаратного ускорения видео
* `OverlayWindow` (`AllowsTransparency="True"`) отвечает за полупрозрачный фон и анимацию персонажа, а `VideoWindow` (`AllowsTransparency="False"`, `WindowStyle="None"`) открывается поверх него для сохранения аппаратного декодирования H.264/MP4.

### ADR-003: Автономная графика персонажа (`MascotAssetService`)
* Автогенерация и кэширование прозрачных PNG-спрайтов в `Assets/Mascot/` с `BitmapCacheOption.OnLoad`.

### ADR-004: Защита от случайного закрытия (`Focus & Alt+F4 Guard`)
* Блокировка `Alt+F4` в `OverlayWindow.OnClosing` до явного разрешения от `OverlayCoordinator`.

### ADR-005: Кросс-окруженческая совместимость сборки в `.csproj`
* Использование `<TargetFramework>net10.0-windows</TargetFramework>` и `<EnableWindowsTargeting>true</EnableWindowsTargeting>`.

### ADR-006 (СПРИНТ 5): Кастомный macOS WindowChrome, отказ от `MessageBox` и векторная иконографика (`Icons.xaml`)
* **Контекст:** Стандартный белый заголовок окна Windows (`TitleBar`), системные диалоги `MessageBox.Show` из эпохи Windows XP и разнокалиберные текстовые эмодзи (`🎬`, `⏱`, `🗑`, `🚪`) разрушают визуальную целостность темного интерфейса.
* **Решение:**
  1. `SettingsWindow` переводится на безрамочный режим (`WindowStyle="None"`, `AllowsTransparency="True"`, `Background="Transparent"`) с внешним `Border` (`CornerRadius="16"`, фон `#0F1117`, бордер `#262938`, мягкая `DropShadowEffect`) и кастомным хедером с поддержкой перетаскивания `DragMove()` и кнопками управления окном.
  2. Все вызовы `MessageBox.Show` удаляются. В `SettingsWindow` внедряется плавающий капсульный **Toast-индикатор** в стиле macOS (появляется сверху/снизу при сохранении настроек и плавно гаснет через 2.2 секунды).
  3. Создается словарь векторных иконок `UI/Theme/Icons.xaml` (или секция в `DarkTheme.xaml`) с чистыми SVG `PathGeometry` в стиле Lucide / SF Symbols. Все эмодзи в `SettingsWindow`, `TrayPopupMenu` и `VideoWindow` заменяются на аккуратные векторные `<Path Data="{StaticResource ...}" />`.

### ADR-007 (СПРИНТ 5): Появление персонажа сверху по центру в стиле macOS Notch / Claude Companion
* **Контекст:** Случайное появление персонажа в разных углах экрана выглядит хаотично. Целевой референс — инструменты-компаньоны для Claude на macOS, которые плавно выезжают сверху из области «челки» (Top-Center Notch / Dynamic Island).
* **Решение:**
  1. В `OverlayWindow` персонаж (`MascotControl`) закрепляется строго по центру верхнего края экрана (`HorizontalAlignment="Center"`, `VerticalAlignment="Top"`, `Margin="0,0,0,0"`).
  2. Анимация `PlayPeekAnimation()` плавно выдвигает персонажа и его темную капсулу-баббл **сверху вниз** (`TranslateTransform.Y` от `-240` до `0`) с упругой кривой `BackEase` / `CubicEase` (`EaseOut`, ~550 мс).
  3. Во время ожидания (`Idle` / `Attention`) персонаж мягко покачивается у верхней кромки экрана, выглядывая сверху. При клике (`Clicked`) он дает короткий импульс и плавно уезжает обратно вверх перед открытием `VideoWindow`.

### ADR-008 (СПРИНТ 5): Закрытие видео по клику вне плеера (Backdrop Dismiss) и двойной контроль автозавершения (`MediaEnded` + Watchdog)
* **Контекст:** Пользователь ожидает, что: 1) при клике на затемненное пространство вокруг видеоролика видео и оверлей сразу закроются; 2) когда видео доигрывает до конца, оно гарантированно закрывается само и запускает ожидание следующего случайного интервала.
* **Решение:**
  1. **Backdrop Click to Dismiss:** В `OverlayWindow` при клике по фону (`MouseLeftButtonDown` на корневом `RootGrid` / `DimmingBorder`), если `OverlayCoordinator.CurrentState == SessionState.VideoPlaying`, немедленно вызывается `_coordinator.CompleteSession()`. Кроме того, если `VideoWindow` теряет активацию из-за клика по `OverlayWindow`, сессия также корректно завершается.
  2. **100% надежное автозакрытие по окончании видео:** В `VideoWindow` помимо обработчика `Player_MediaEnded` запускается `DispatcherTimer` (`Interval = 200ms`), отслеживающий прогресс `Player.Position`: как только `Player.NaturalDuration.HasTimeSpan && Player.Position >= Player.NaturalDuration.TimeSpan - TimeSpan.FromMilliseconds(150)`, таймер принудительно вызывает завершение воспроизведения (`CompleteSession()`). Это исключает зависание на последнем кадре из-за особенностей WMP/WPF кодеков, закрывает оба окна и запускает `RandomReminderScheduler.StartNextCycle(...)`.

## 4. Чеклист майлстоунов

### СПРИНТ 1 – СПРИНТ 4 (Базовый MVP)
- [x] **Спринт 1:** Инициализация решения, Модели, Хранилище, Планировщик и Unit-тесты (28 тестов).
- [x] **Спринт 2:** Фоновый режим, Win32 Интероп, Системный трей и Технический прототип окон (92 теста).
- [x] **Спринт 3:** 2D-Персонаж, Генератор спрайтов и WPF Storyboard-анимации (124 теста).
- [x] **Спринт 4:** Окно настроек, Полиш UI, Обработка крайних случаев и Финализация MVP (143 теста).

### СПРИНТ 5: macOS Premium UI Overhaul, Top-Center Notch Peek & Video UX Fixes
- [x] **5.1. Векторная иконографика (`Icons.xaml` / `DarkTheme.xaml`) и отказ от эмодзи:**
  * Создать набор чистых векторных `Geometry`-иконок (Lucide / SF Symbols style): `IconBolt`, `IconPlay`, `IconPause`, `IconTrash`, `IconPlus`, `IconVideo`, `IconClock`, `IconSettings`, `IconEyeOff`, `IconLogOut`, `IconCheck`, `IconWarning`, `IconClose`, `IconMinimize`.
  * Полностью удалить все текстовые эмодзи (`⚡`, `⏸`, `🎬`, `⏱`, `🗑`, `🚪`, `⚙`) из `SettingsWindow.xaml`, `TrayPopupMenu.xaml`, `VideoWindow.xaml` и кода `.cs`, заменив их на векторные `<Path>` с правильным выравниванием.
  * Убрать из текста UI технические пометки `(FR-02)`, `(FR-03, FR-04)` и т.д.
- [x] **5.2. Безрамочный macOS-дизайн `SettingsWindow` и `TrayPopupMenu` (Прощай, белая шапка и Internet Explorer):**
  * Перевести `SettingsWindow.xaml` на кастомный безрамочный дизайн (`WindowStyle="None"`, `AllowsTransparency="True"`, `Background="Transparent"`), внешний контейнер со скруглением `CornerRadius="16"`, глубоким темным фоном `#0F1117`, тонкой стеклянной обводкой `#262938` и кастомным темным заголовком окна (с перетаскиванием мышью `DragMove()`, кнопками свернуть и закрыть без единого белого пикселя).
  * Полностью удалить `MessageBox.Show` во всем проекте. Реализовать внутри `SettingsWindow` плавающий **macOS Toast-баннер** («Настройки успешно сохранены») с векторной зеленой галочкой и плавной анимацией появления/исчезновения.
  * Исправить прозрачность и скругление углов в `TrayPopupMenu.xaml` (`AllowsTransparency="True"`, `Background="Transparent"`, `CornerRadius="14"`), чтобы вокруг меню трея не было квадратных артефактов, и обновить пункт «Выход» чистой векторной иконкой `IconLogOut`.
- [x] **5.3. Выезд персонажа сверху по центру (macOS Notch / Claude Top-Peek):**
  * В `OverlayWindow` зафиксировать позицию `MascotControl` строго **сверху по центру экрана** (`HorizontalAlignment="Center"`, `VerticalAlignment="Top"`), убрав рандомизацию по углам.
  * Переписать анимацию появления (`PlayPeekAnimation`) в `MascotControl`: плавный спуск персонажа сверху из-за верхней границы экрана (`TranslateTransform.Y` от `-250` до `0`) с пружинящим `BackEase`/`CubicEase`, как у шторки/компаньона Claude на macOS.
  * Оформить подложку/баббл реплики персонажа в виде стильной темной стеклянной капсулы (`#181A24`, бордер `#34384E`, акцентное свечение `#8B5CF6`).
- [x] **5.4. Закрытие видео по клику вне плеера (Backdrop Click) и надежное автозакрытие по окончании:**
  * В `OverlayWindow` добавить обработку клика по затемненному фону: если открыто видео (`SessionState.VideoPlaying`), клик мимо `VideoWindow` по затемненному оверлею немедленно закрывает видео и оверлей (`CompleteSession()`) и запускает таймер до следующего появления.
  * В `VideoWindow` добавить нижний прогресс-бар воспроизведения, стильную плавающую кнопку закрытия `✕` и страховочный таймер контроля окончания ролика (`Position >= NaturalDuration - 150ms`), гарантирующий 100% автоматическое закрытие окон сразу после досмотра видео и постановку `RandomReminderScheduler` на новый случайный интервал.
- [x] **5.5. Верификация Спринта 5 и Синхронизация:**
  * Обновить и прогнать модульные тесты (`dotnet test`), убедившись, что новые тесты покрывают Top-Center позиционирование и закрытие сессии по Backdrop-клику.
  * Проверить чистую сборку `dotnet build -c Release --warnaserror`.
  * Отметить все пункты Спринта 5 `[x]` в `personal_provocateur_spec.md` и выполнить `./sync.sh`.

## 5. Сводка изменений по спринтам (Заполняется Оркестратором)

| Спринт | Статус | Ключевые созданные / измененные файлы | Результат `dotnet build` & `dotnet test` |
| :--- | :--- | :--- | :--- |
| **Спринт 1** | `[x]` Завершен | `PersonalProvocateur.sln`, `.csproj`, `Models/*`, `Storage/*`, `VideoShuffleBag.cs`, `VideoLibraryService.cs`, `RandomReminderScheduler.cs` + тесты | Release: 0 ошибок, 0 предупреждений. 28 тестов пройдено |
| **Спринт 2** | `[x]` Завершен | `SingleInstanceGuard.cs`, `WindowsInterop/*`, `OverlayCoordinator.cs`, `OverlayWindow`, `VideoWindow`, `TrayPopupMenu`, `SettingsWindow` + тесты | Release: 0 ошибок, 0 предупреждений. 92 теста пройдено |
| **Спринт 3** | `[x]` Завершен | `MascotAssetService.cs`, `MascotControl.xaml/.cs`, `OverlayWindow.xaml/.cs` + тесты | Release: 0 ошибок, 0 предупреждений. 124 теста пройдено |
| **Спринт 4** | `[x]` Завершен | `DarkTheme.xaml`, `SettingsWindow.xaml/.cs`, `GlobalHotkeyService.cs`, `OverlayCoordinator.cs`, `OverlayWindow.xaml.cs`, `VideoWindow.xaml.cs` + тесты | Release: 0 ошибок, 0 предупреждений. 143 теста пройдено |
| **Спринт 5** | `[x]` Завершен | `Icons.xaml`, `DarkTheme.xaml`, `SettingsWindow.xaml/.cs`, `TrayPopupMenu.xaml/.cs`, `MascotControl.xaml/.cs`, `MascotAssetService.cs`, `OverlayWindow.xaml/.cs`, `VideoWindow.xaml/.cs`, `OverlayWindowTests.cs`, `MascotControlTests.cs`, `VideoWindowTests.cs` | Release: 0 ошибок, 0 предупреждений. 148 тестов пройдено (100% pass rate). |
