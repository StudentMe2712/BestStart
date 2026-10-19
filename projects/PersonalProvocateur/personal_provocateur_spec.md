# СПЕЦИФИКАЦИЯ ПРОЕКТА: Personal Provocateur (`personal_provocateur_spec.md`)

> **СТАТУС ДОКУМЕНТА:** Спринты 1–4 завершены. Активен **СПРИНТ 5: macOS Premium UI Overhaul, Smooth Transitions, Top-Notch Mascot & Mandatory Video Watch**.
> **ЛОКАЦИЯ ПРОЕКТА:** `projects/PersonalProvocateur/` (внутри репозитория `BestStart`).
> **СТЕК:** C# 13 | .NET 10 LTS (`net10.0-windows`) | WPF + XAML | Win32 P/Invoke | JSON Storage | xUnit.

## 0. Жесткие правила для Оркестратора и Субагентов (Anti-Drift Guardrails)

1. **Ролевая модель Antigravity:**
   * **Оркестратор (Tech Lead / Архитектор):** Декомпозирует задачи, делегирует написание кода субагентам, контролирует соблюдение спецификации, проверяет `dotnet build` / `dotnet test`, отмечает `[ ]` → `[x]` в этом файле и в конце спринта вызывает `./sync.sh`.
   * **Субагенты (Разработчики):** Строго следуют структуре папок, контрактам и ADR, зафиксированным ниже.
2. **Изоляция воркспейса:**
   * Все файлы решения создаются и изменяются **строго внутри `projects/PersonalProvocateur/`**.
3. **Визуальный стандарт (macOS Sonoma / Raycast / Linear Aesthetic):**
   * **Никаких стандартных белых рамок Windows и `MessageBox.Show`:** Категорически запрещено использовать системный белый TitleBar и вызовы `System.Windows.MessageBox.Show(...)` (стиль эпохи Windows XP). Все окна должны быть безрамочными (`WindowStyle="None"`, `AllowsTransparency="True"` у `SettingsWindow` и `TrayPopupMenu`) со скругленными углами, кастомным темным хедером и встроенными плавающими **Toast-уведомлениями**.
   * **Никаких текстовых эмодзи вместо иконок:** Категорически запрещено использовать Unicode-символы и эмодзи (`⚡`, `⏸`, `▶`, `🎬`, `⏱`, `🗑`, `🚪`, `⚙`, `✓`, `⚠️`) в XAML и C#-коде. Использовать исключительно векторные SVG-иконки (`StreamGeometry` / `Path` из `UI/Theme/Icons.xaml` в стиле Lucide / SF Symbols).
   * **Никаких технических меток ТЗ в UI:** Убрать из всех заголовков секций суффиксы `(FR-02)`, `(FR-03, FR-04)`, `(FR-07, FR-08, FR-09)`.
   * **Премиальная типографика и рендеринг:** Использовать шрифтовой стек `"Inter", "Plus Jakarta Sans", "Segoe UI Variable Display", "Segoe UI"` с явным разведением весов (`SemiBold`/`Bold` для заголовков, `Regular`/`Medium` для подписей) и настройками четкости `UseLayoutRounding="True"`, `SnapsToDevicePixels="True"`, `TextOptions.TextFormattingMode="Ideal"`, `TextOptions.TextRenderingMode="ClearType"`.

## 1. Продуктовая концепция и Бизнес-логика

**Personal Provocateur** — нативное настольное приложение для Windows 10/11 в эстетике macOS, которое работает в фоновом режиме, в случайные моменты времени плавно затемняет экран, выдвигает **сверху по центру экрана** (в стиле macOS Notch / Dynamic Island) анимированного 2D-персонажа и после клика по персонажу запускает случайное мотивационное видео, которое пользователь **обязан досмотреть до конца**.

### Машина состояний сессии (`SessionState`)

| Состояние | Описание поведения системы | Переходы |
| :--- | :--- | :--- |
| **`BackgroundIdle`** | Приложение свернуто в системный трей. Активных оверлеев нет. `RandomReminderScheduler` отсчитывает случайный интервал `[MinIntervalMinutes, MaxIntervalMinutes]`. | → `MascotOverlayActive` (сработал таймер или нажата горячая клавиша вызова)<br>→ `Paused` (включена пауза или заблокирован экран Windows) |
| **`Paused`** | Таймер остановлен. Новые сессии по расписанию не запускаются. | → `BackgroundIdle` (снятие паузы / разблокировка Windows со свежим интервалом) |
| **`MascotOverlayActive`** | Полноэкранное `OverlayWindow` плавно проявляется (`Fade-In 350ms`). Персонаж плавно выезжает **сверху по центру экрана (`Top-Center`)** с пружинящей анимацией (`Peek` → `Idle` / `Attention`). Персонаж не исчезает сам, пока пользователь не кликнет по нему. | → `VideoPlaying` (клик мышью по персонажу)<br>→ `BackgroundIdle` (нажат аварийный хоткей `PanicHotkey` или пункт «Скрыть оверлей» в трее) |
| **`VideoPlaying`** | Персонаж с анимацией реакции (`Clicked`) плавно уезжает обратно вверх, а по центру экрана поверх затемнения плавно открывается `VideoWindow`. **Обязательный досмотр (Mandatory Watch Lock):** клик вне рамки видео (по затемненному фону), нажатие `Alt+F4` или `Esc` **НЕ закрывают** видео! Перемотка вперед отключена. | → `BackgroundIdle` (**1)** видео доиграло до конца — оба окна **плавно и автоматически закрываются сами**, запускается новый случайный интервал ожидания `StartNextCycle(...)`; **2)** нажат экстренный `PanicHotkey` или закрыто тестовое превью из настроек). |

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
│       │   │   ├── Icons.xaml
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
```

## 3. Записи архитектурных решений (ADR)

### ADR-001..ADR-005 (Базовая архитектура Спринтов 1–4)
* Нативный `NotifyIcon` + кастомное WPF-меню трея (`TrayPopupMenu.xaml`); двухоконная схема `OverlayWindow` (`AllowsTransparency="True"`) + `VideoWindow` (`AllowsTransparency="False"`) для сохранения аппаратного декодирования MP4; автогенерация прозрачных PNG персонажа (`MascotAssetService`); защита от `Alt+F4`; совместимость `net10.0-windows`.

### ADR-006 (СПРИНТ 5): Кастомный macOS WindowChrome, отказ от `MessageBox.Show`, SVG-иконки (`Icons.xaml`) и Toggle-свитчи
* **Контекст:** Стандартная белая шапка окна Windows, модальное окно `MessageBox.Show("Настройки успешно сохранены!")` в стиле Windows XP, квадратные чекбоксы и сломанные текстовые эмодзи (`⏱`, `🚪`, `🗑`) делают интерфейс дешевым.
* **Решение:**
  1. **Безрамочное окно `SettingsWindow`:** `WindowStyle="None"`, `AllowsTransparency="True"`, `Background="Transparent"`. Внешний `Border` со скруглением `CornerRadius="16"`, фоном `#0B0C10` (глубокий обсидиан), стеклянным бордером `#232634` и тенью `DropShadowEffect`. Кастомный TitleBar сверху с перетаскиванием `DragMove()`, векторным логотипом и минималистичными кнопками управления окном (Свернуть / Закрыть).
  2. **macOS Toast HUD вместо `MessageBox.Show`:** Полный запрет на `MessageBox.Show` во всем проекте. При сохранении настроек сверху/снизу окна плавно выезжает капсульный Toast-баннер (`CornerRadius="18"`, фон `#181B26`, неоново-изумрудный бордер `#10B981`, векторная иконка галочки и текст «Настройки сохранены»), который автоматически плавно растворяется через 2.2 секунды.
  3. **Векторная иконографика `UI/Theme/Icons.xaml`:** Все иконки задаются через векторные `StreamGeometry` (Lucide Icons style) и рендерятся через `<Path Data="{StaticResource ...}" Stretch="Uniform"/>`. Ни одного текстового эмодзи в UI.
  4. **macOS Toggle Switch:** Стандартный `CheckBox` стилизуется в виде плавного анимированного тумблера-переключателя (капсула `40x22px` с белым круглым ползунком, плавно переезжающим по `TranslateTransform.X` при переключении).

### ADR-007 (СПРИНТ 5): Плавность всех переходов и выезд персонажа сверху по центру (macOS Notch / Claude Style)
* **Контекст:** Резкое появление окон и случайная позиция персонажа на экране портят впечатление. Персонаж должен плавно вылезать сверху по центру, как в утилитах для Claude на macOS (Dynamic Island / Notch).
* **Решение:**
  1. **Top-Center Notch Peek:** В `OverlayWindow` контрол `MascotControl` жестко центрируется у верхней кромки экрана (`HorizontalAlignment="Center"`, `VerticalAlignment="Top"`, `Margin="0"`).
  2. **Каскад плавных переходов сессии:**
     * **Открытие оверлея:** `OverlayWindow` плавно анимирует `Opacity` от `0` до `1` (`350ms`, `CubicEase EaseOut`), одновременно `MascotControl` плавно выезжает сверху вниз (`TranslateTransform.Y` от `-260` до `0` за `520ms` с пружинящим `BackEase Amplitude=0.35`).
     * **Переход к видео по клику:** При клике по персонажу он отыгрывает радостный импульс и плавно убирается вверх (`TranslateTransform.Y` от `0` до `-260` за `240ms`), после чего по центру экрана открывается `VideoWindow` с плавной анимацией проявления (`Opacity` `0 -> 1`).
     * **Плавное закрытие после досмотра:** По окончании ролика окна не исчезают рывком — запускается плавный `FadeOut` (`Opacity` `1 -> 0` за `280ms`), и только по его завершении вызывается физический `Close()`.

### ADR-008 (СПРИНТ 5): Обязательный досмотр видео (Блокировка клика вне видео) и 100% автозакрытие по окончании
* **Контекст:** Пользователь должен обязательно досмотреть мотивационное видео до конца: клик вне рамки видео (по затемненному фону) НЕ должен закрывать плеер. При этом сразу после окончания ролика плеер и оверлей обязаны гарантированно автоматически закрыться и запустить отсчет до следующего появления.
* **Решение:**
  1. **Запрет закрытия по клику вне видео (`Mandatory Watch Lock`):**
     * В состоянии `SessionState.VideoPlaying` клик мышью по затемненному фону `OverlayWindow` **НЕ закрывает** сессию, а наоборот — возвращает фокус на `VideoWindow` (`ActiveVideoWindow?.Activate()`) и показывает легкую пульсацию рамки плеера («Досмотри до конца!»).
     * В боевом режиме провокации (`!isManualTest`) в `VideoWindow` скрыта кнопка закрытия `✕`, а закрытие через `Alt+F4` заблокировано в `VideoWindow.OnClosing` (`e.Cancel = true`, пока `AllowClose == false`). В режиме ручного теста из окна настроек (`isManualTest == true`) аккуратная кнопка закрытия доступна для проверки.
  2. **100% надежное автозакрытие после окончания видео:**
     * КРИТИЧЕСКИЙ ФИКС: В `OverlayCoordinator.CompleteSession()` перед вызовом `Close()` обязательно выставлять `AllowClose = true` как на `VideoWindow`, так и на `OverlayWindow`, чтобы защита `OnClosing` не блокировала автоматическое закрытие после конца видео!
     * Двойной триггер окончания в `VideoWindow`: подписка на `Player.MediaEnded` + страховочный `DispatcherTimer` (`Interval = 100ms`), который проверяет `Player.NaturalDuration.HasTimeSpan && Player.NaturalDuration.TimeSpan.TotalSeconds > 0 && Player.Position >= Player.NaturalDuration.TimeSpan - TimeSpan.FromMilliseconds(120)`. Как только видео дошло до конца (по любому из двух триггеров, с защитой от двойного вызова `_isCompleting`), запускается плавный `FadeOut` и вызывается `OverlayCoordinator.CompleteSession()`, который закрывает оба окна и запускает `RandomReminderScheduler.StartNextCycle(min, max)`.

### ADR-009 (СПРИНТ 5): Полиш мелочей UX (Интерактивный захват хоткеев, чистый футер, прогресс-бар без перемотки)
* **Решение:**
  1. **Интерактивный захват комбинаций клавиш (`Hotkey Recorder`):** Поля `TriggerHotkey` и `PanicHotkey` в `SettingsWindow` перехватывают `PreviewKeyDown` — пользователю достаточно кликнуть в поле и нажать сочетание клавиш на клавиатуре (например `Ctrl+Alt+P`), и оно автоматически отформатируется и проверится.
  2. **Устранение дублей кнопок:** Из нижнего бара `SettingsWindow` убирается дублирующая кнопка «Проверить оверлей» (она уже есть в верхней карточке статуса).
  3. **Неперематываемый прогресс-бар в `VideoWindow`:** В нижней части видеоплеера отображается тонкая стильная полоска прогресса (`#8B5CF6`) и оставшееся время (`0:14`), без возможности перемотать ролик вперед мышью.

## 4. Чеклист майлстоунов

### СПРИНТ 1 – СПРИНТ 4 (Базовый MVP)
- [x] **Спринты 1–4:** Ядро, Win32, трей, 2D-персонаж, базовые настройки и тесты (143/143 тестов пройдено).

### СПРИНТ 5: macOS Premium UI Overhaul, Smooth Transitions, Top-Notch Mascot & Mandatory Video Watch
- [x] **5.1. Векторные иконки (`UI/Theme/Icons.xaml`), шрифты и удаление всех эмодзи:**
  * Создать словарь `UI/Theme/Icons.xaml` с точными векторными `StreamGeometry` (Lucide style): `IconBolt`, `IconPlay`, `IconPause`, `IconTrash`, `IconPlus`, `IconVideo`, `IconClock`, `IconSettings`, `IconEyeOff`, `IconLogOut`, `IconCheck`, `IconWarning`, `IconClose`, `IconMinimize`.
  * Подключить `Icons.xaml` в `App.xaml` / `DarkTheme.xaml` и заменить все текстовые эмодзи (`⚡`, `⏸`, `🎬`, `⏱`, `🗑`, `🚪`, `⚙`) во всех окнах на векторные `<Path>`.
  * Убрать технические коды `(FR-02)`, `(FR-03, FR-04)`, `(FR-07, FR-08, FR-09)` из всех заголовков.
- [x] **5.2. Редизайн `SettingsWindow` и `TrayPopupMenu` под macOS (Без белой шапки и без `MessageBox.Show`):**
  * Перевести `SettingsWindow` на безрамочный режим (`WindowStyle="None"`, `AllowsTransparency="True"`, `Background="Transparent"`) с кастомным темным TitleBar, перетаскиванием мышью и кастомными кнопками закрытия/свертывания.
  * Заменить квадратные чекбоксы на анимированные тумблеры **macOS Toggle Switch**.
  * Полностью удалить все вызовы `MessageBox.Show` из проекта; при сохранении настроек показывать плавающий **macOS Toast HUD** с плавной анимацией появления и автоскрытия.
  * Добавить интерактивный захват клавиш (`PreviewKeyDown` Hotkey Recorder) в поля настройки горячих клавиш и убрать дублирующую кнопку «Проверить оверлей» из нижнего футера.
  * Полирнуть `TrayPopupMenu.xaml` (чистые векторные иконки, включая `IconLogOut` у кнопки «Выход», скругленный стеклянный корпус без артефактов по углам, плавный `FadeIn` при открытии).
- [x] **5.3. Выезд персонажа сверху по центру (macOS Notch / Claude Style) и плавные переходы:**
  * Зафиксировать `MascotControl` в `OverlayWindow` строго сверху по центру (`HorizontalAlignment="Center"`, `VerticalAlignment="Top"`), удалив случайное позиционирование по углам экрана.
  * Реализовать плавный выезд персонажа сверху из-за края экрана (`TranslateTransform.Y` от `-260` до `0` с `BackEase`) одновременно с плавным `FadeIn` затемнения `OverlayWindow`.
  * Добавить плавный уход персонажа вверх после клика и плавное проявление/затухание (`FadeIn` / `FadeOut`) окон при смене состояний сессии.
- [x] **5.4. Запрет закрытия по клику вне видео (Mandatory Watch) и 100% автозакрытие после окончания ролика:**
  * Заблокировать закрытие `VideoWindow` и `OverlayWindow` при клике мышью по затемненному полю вне рамки видео (при клике вне плеера возвращать фокус на `VideoWindow` и показывать легкую подсветку рамки «Досмотри видео до конца»).
  * Заблокировать `Alt+F4` на `VideoWindow` до окончания ролика и добавить тонкий прогресс-бар внизу видео.
  * Обеспечить 100% автоматическое закрытие `VideoWindow` и `OverlayWindow` сразу после окончания проигрывания видео (через `MediaEnded` + страховочный таймер по `NaturalDuration`, с обязательной установкой флага разрешения закрытия `AllowClose = true` перед `Close()`) и автоматический запуск `RandomReminderScheduler.StartNextCycle(...)`.
- [x] **5.5. Верификация Спринта 5 и Синхронизация:**
  * Обновить и прогнать модульные тесты (`dotnet test`), проверить сборку `dotnet build -c Release --warnaserror`.
  * Отметить все пункты Спринта 5 `[x]` в `personal_provocateur_spec.md` и выполнить `./sync.sh`.

## 5. Сводка изменений по спринтам (Заполняется Оркестратором)

| Спринт | Статус | Ключевые созданные / измененные файлы | Результат `dotnet build` & `dotnet test` |
| :--- | :--- | :--- | :--- |
| **Спринт 1** | `[x]` Завершен | `PersonalProvocateur.sln`, `.csproj`, `Models/*`, `Storage/*`, `VideoShuffleBag.cs`, `VideoLibraryService.cs`, `RandomReminderScheduler.cs` + тесты | Release: 0 ошибок, 0 предупреждений. 28 тестов пройдено |
| **Спринт 2** | `[x]` Завершен | `SingleInstanceGuard.cs`, `WindowsInterop/*`, `OverlayCoordinator.cs`, `OverlayWindow`, `VideoWindow`, `TrayPopupMenu`, `SettingsWindow` + тесты | Release: 0 ошибок, 0 предупреждений. 92 теста пройдено |
| **Спринт 3** | `[x]` Завершен | `MascotAssetService.cs`, `MascotControl.xaml/.cs`, `OverlayWindow.xaml/.cs` + тесты | Release: 0 ошибок, 0 предупреждений. 124 теста пройдено |
| **Спринт 4** | `[x]` Завершен | `DarkTheme.xaml`, `SettingsWindow.xaml/.cs`, `GlobalHotkeyService.cs`, `OverlayCoordinator.cs`, `OverlayWindow.xaml.cs`, `VideoWindow.xaml.cs` + тесты | Release: 0 ошибок, 0 предупреждений. 143 теста пройдено |
| **Спринт 5** | `[x]` Завершен | `Icons.xaml`, `SettingsWindow.xaml/.cs`, `TrayPopupMenu.xaml`, `OverlayWindow.xaml/.cs`, `VideoWindow.xaml/.cs`, `OverlayCoordinator.cs` + тесты | Release: 0 ошибок, 0 предупреждений. 160 тестов пройдено |
