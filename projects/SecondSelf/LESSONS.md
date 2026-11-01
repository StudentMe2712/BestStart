# Lessons Learned — SecondSelf

## Log

### 2026-10-31 — SQLite Online Backup and WAL Mode in WinUI 3
- **Context:** Implementing backup and WAL PRAGMA settings for `Data/secondself.db`.
- **Finding:** In SQLite WAL mode, copying the `.db` file directly while writers are active can lead to dirty reads or missing uncheckpointed WAL frames (`-wal` file). Using `SqliteConnection.BackupDatabase(destination)` performs an atomic, consistent online backup directly through the SQLite engine without blocking readers.
- **Rule:** Always use the native SQLite Online Backup API (`source.BackupDatabase(dest)`) rather than file-system copy for active SQLite databases in WAL mode.

### 2026-10-31 — ISO-8601 Thursday Rule for Monthly Progress Partitioning
- **Context:** Binding calendar weeks to months for progress calculations ($0.30 \times D + 0.70 \times W$).
- **Finding:** Days of a week can cross month boundaries (e.g., Monday in October, Friday in November). Adhering strictly to the ISO-8601 Thursday rule (a week belongs to the month of its Thursday) guarantees that every week uniquely belongs to exactly one month, resulting in strictly 4 or 5 weeks per month with zero overlap or orphan days.
- **Rule:** In progress engines combining daily and weekly cadence, partition weeks strictly by Thursday date.

### 2026-10-31 — WinUI 3 Window Markup and Resources Scope
- **Context:** Implementing `WidgetWindow.xaml` and custom chome/storyboard transitions in WinUI 3 (Windows App SDK 1.6).
- **Finding:** Unlike WPF or UWP `Page`, `Microsoft.UI.Xaml.Window` inherits directly from `DependencyObject` rather than `FrameworkElement` and lacks a `<Window.Resources>` XAML property. Attempting to place `<Window.Resources>` causes `XamlCompiler.exe` to fail Pass 1 with an opaque exit code 1. Furthermore, `{x:Bind}` methods called from Window XAML must be instance methods rather than static.
- **Rule:** In WinUI 3 Window definitions, declare all styles, brushes, and storyboards within `<Grid.Resources>` (the root content element), and ensure any x:Bind helper methods on the code-behind are public instance methods.

### 2026-10-31 — WinUI 3 Window x:Bind Static Methods vs Converter Lookups
- **Context:** Binding visibility and text decorations in `DataTemplate` within `WidgetWindow.xaml`.
- **Finding:** When using `{x:Bind ... Converter={StaticResource ...}}` inside a `Window` markup, `XamlCompiler.exe` generates `bindings.SetConverterLookupRoot(this)`. Because `Window` inherits directly from `DependencyObject` rather than `FrameworkElement`, the C# compiler rejects this with `CS1503: cannot convert from Window to FrameworkElement`.
- **Rule:** In WinUI 3 `Window` definitions, do not use `{StaticResource Converter}` inside `{x:Bind}`. Instead, call static converter methods directly (e.g., `{x:Bind conv:XamlConverters.ToVisibility(property)}`), which compiles cleanly without requiring a `FrameworkElement` converter lookup root.

### 2026-10-31 — Deferred Archive and 5-Second Undo Toast Architecture
- **Context:** Task completion workflow in `Today's Focus` with a 5000 ms cancellation window.
- **Finding:** If an item is immediately moved to `ArchivedTasks` in SQLite upon completion, clicking "Undo" requires an inverse transaction, risking WAL checkpoint races and timestamp pollution. Keeping the task in a pending cancellation state in memory with a deferred timer guarantees instant, zero-latency restoration and commits to SQLite only upon timer expiration or when the next completion is queued.
- **Rule:** For undoable operational items, decouple UI two-phase animation completion from permanent database archiving using deferred cancellation tokens (`CancellationTokenSource`).

### 2026-10-31 — Fisher-Yates Cycle Queue with Non-Repeating Consecutive Elements and Missing File Tolerant Loop
- **Context:** Implementing `VideoPlaybackQueue` and `FisherYatesShuffleService` for Daily Reset.
- **Finding:** In small libraries (2 videos), standard random shuffling can yield `[A, B]` in cycle 1 and `[B, A]` in cycle 2, leading to `B` being played twice in consecutive days (`A -> B -> B -> A`). Checking `shuffled[0] == lastPlayedId` and swapping with another index ensures strictly alternating playback without duplicate runs. Furthermore, when files on disk disappear, marking `IsMissing = 1` and continuing the queue search within the same transaction ensures zero playback stall and zero crash for the user.
- **Rule:** In cyclic playlist queues, explicitly disallow consecutive duplicate transitions across cycle boundaries by swapping the first element of a new cycle if it matches the tail of the previous cycle.


### 2026-10-31 — WinUI 3 Typography Numeral Alignment and Non-Seekable Progress Bar
- **Context:** Implementing `Daily Reset Player` tabular timecode and locked progress bar in `WidgetWindow.xaml`.
- **Finding:** In WinUI 3 `TextBlock`, there is no `FontNumeralAlignment` property; OpenType tabular digits are specified using `Typography.NumeralAlignment="Tabular"`. Additionally, to guarantee non-seekable playback (spec AC-07), setting `IsHitTestVisible="False"` on `ProgressBar` / track container prevents all mouse clicks and drag events from dispatching, while allowing visual progress updates via binding.
- **Rule:** For fixed-width timecodes and numerical displays in WinUI 3, always use attached property `Typography.NumeralAlignment="Tabular"`. For read-only progress indicators, use `IsHitTestVisible="False"`.

### 2026-10-31 — Commitment Lock Sliding Window and Zero-Inflation Big Goal Milestone Linkage
- **Context:** Implementing `WeeklyGoalModalViewModel`, `GoalsTabViewModel`, and BDD tests AC-09..AC-13.
- **Finding:** Weekly goal formulation locking has a dual trigger: explicit commit by user OR expiration of a 2-hour sliding window from creation. Implementing a testable `CurrentTimeForTesting` property allows deterministic BDD testing without real-time delays. Additionally, linking a weekly goal to a Big Goal's milestone (`MilestoneId`) must never prematurely inflate the Big Goal's milestone completion counter or percentage ($P_G$); progress must advance strictly upon explicit completion of the milestone itself (`CompleteMilestoneAsync`).
- **Rule:** Decouple time-based commitment locks with injected or overrideable clock timestamps for testability, and enforce strict progress invariance so that subsidiary linkage never alters parent progress counters without explicit milestone completion.

### 2026-10-31 — WinUI 3 XamlCompiler Crash on ItemsPanelTemplate Containing ItemsControl and String DataTemplates
- **Context:** Implementing `DashboardWindow.xaml` with complex grid layouts, cards, and collections.
- **Finding:** In WinUI 3 (Windows App SDK), placing an `ItemsControl` (such as `GridView` or `ListView`) inside an `<ItemsControl.ItemsPanelTemplate>` causes `XamlCompiler.exe` to crash internally with exit code 1 without a descriptive error message because `ItemsPanelTemplate` can only host a `Panel` subclass (e.g. `ItemsWrapGrid`, `VariableSizedWrapGrid`, `StackPanel`, `Grid`). If a grid is needed, use `<GridView ItemsSource="..." SelectionMode="None">` directly. Additionally, using `{x:Bind}` with `x:DataType="x:String"` in data templates can fail resolution; standard `{Binding}` should be used for simple primitive collections (e.g. `ObservableCollection<string>`).
- **Rule:** Never nest an `ItemsControl` inside `ItemsPanelTemplate`; use the appropriate control (`GridView` / `ListView`) directly or specify a true `Panel`. For primitive string collections, use standard `{Binding}` in `DataTemplate`.

### 2026-10-31 — SQLite Connection Pool Eviction Scope in Multi-Threaded / Test Environments
- **Context:** Implementing `DatabaseRecoveryService.CheckAndRecoverAsync` and running concurrent BDD tests.
- **Finding:** Invoking `SqliteConnection.ClearPool(connection)` unconditionally after healthy read checks invalidates the underlying native handle (`SQLitePCL.sqlite3`) for all active connections sharing the same connection string. In concurrent environments or parallel xUnit test runs, this causes `System.ObjectDisposedException: Cannot access a disposed object. Object name: 'SQLitePCL.sqlite3'`.
- **Rule:** Only clear the connection pool when corruption is positively detected and file-system replacement is required. Routine health checks must simply dispose the connection/command without purging the global connection pool.

### 2026-11-01 — Glassmorphism Shadow Hosts Behind Cards in WinUI 3 Composition
- **Context:** Implementing hardware DropShadow (Offset 0, 16; BlurRadius 40; Color rgba(0,0,0,0.5)) on translucent glass cards.
- **Finding:** Placing a `SpriteVisual` with `DropShadow` directly onto a translucent XAML element via `ElementCompositionPreview.SetElementChildVisual` causes the child visual to render on top of the element's background in the visual tree. Placing a dedicated `<Border x:Name="*ShadowHost" CornerRadius="24" />` directly behind the glass card in the same Grid cell allows the shadow to render strictly behind the card, keeping the warm specular rim and card interior pristine.
- **Rule:** In WinUI 3 Composition, always host drop shadows on a dedicated transparent peer element positioned behind translucent cards.

### 2026-11-01 — First-Touch Instant Window Drag and Clickable Control Isolation
- **Context:** Eliminating first-click drag lag and double-touch requirements in desktop widgets.
- **Finding:** Intercepting `PointerPressed` on cards to trigger state transitions (such as expanding on click) suppresses WM_NCLBUTTONDOWN and forces a second click for dragging. Calling Win32 `ReleaseCapture()` and `SendMessage(hWnd, WM_NCLBUTTONDOWN, HTCAPTION, 0)` immediately on the card's `PointerPressed`, while setting `e.Handled = true` on all interactive child controls (+ buttons, Play, checkboxes, text), ensures instant dragging on the first touch while preventing accidental drags during clicks.
- **Rule:** Always isolate interactive controls with `e.Handled = true` and dispatch Win32 `WM_NCLBUTTONDOWN` on the parent container's first left-button pointer press.

### 2026-11-01 — Zero-Crash Media Player Lifecycle and Automatic Broken File Replacement
- **Context:** Stabilizing Daily Reset focus video player and preventing app crashes on missing or corrupted media.
- **Finding:** WinUI 3 `MediaPlayer` instances can lock Direct3D decoding buffers if not explicitly detached (`MediaPlayer.Source = null`) and paused before window collapsing or tab switching. Furthermore, when decoding fails (`MediaFailed`) or files are missing on disk, catching the error and calling `DayTransitionService.ReplaceCorruptedVideoAsync` to draw the next video from `CycleQueue` with status "Файл заменен" recovers the player cleanly with zero application crashes.
- **Rule:** Always set `MediaPlayer.Source = null` on view dismissal, wrap `MediaSource` creation in try-catch, and route decoding failures to cyclic auto-replacement.

### 2026-11-01 — True Window Transparency in WinUI 3 with DWM and ICompositionSupportsSystemBackdrop
- **Context:** Eliminating opaque black rectangular backdrops in frameless desktop widgets with Glassmorphism.
- **Finding:** In WinUI 3 (Windows App SDK), setting `Window.SystemBackdrop = null` and `Grid.Background = "Transparent"` alone leaves an opaque black backing canvas rendered by `DesktopWindowXamlSource`. To achieve 100% true alpha transparency where the Windows desktop wallpaper is visible through card margins, one must combine Win32 `DwmExtendFrameIntoClientArea(margins = -1)` and `DwmEnableBlurBehindWindow` with WinRT `window.As<ICompositionSupportsSystemBackdrop>()`, assigning an alpha-zero `CompositionColorBrush` (`Color.FromArgb(0, 0, 0, 0)`).
- **Rule:** Always assign an explicit alpha-zero `CompositionColorBrush` to `ICompositionSupportsSystemBackdrop.SystemBackdrop` and invoke `DwmEnableBlurBehindWindow` for true glassmorphic transparent windows in WinUI 3.

### 2026-11-01 — Constant Window Size with TranslateTransform Slide Animation (Eliminating Black Screen Resizing Artifacts)
- **Context:** Eliminating black screen flashes, DWM surface re-allocation lags, and mouse dragging jitter in top-anchored Rainmeter-style desktop widgets.
- **Finding:** Resizing a WinUI 3 `AppWindow` dynamically between compact (e.g. 180×36) and expanded (420×280) via `MoveAndResize` forces Windows DWM to destroy and re-create the backing DirectX swapchain and composition visual surfaces, producing visible black frames. Keeping the window size strictly constant (420×280) anchored at the top-center edge ($Y=0$, $X=(\text{WorkArea.Width} - \text{Width})/2$) and animating the widget canvas via `TranslateTransform.Y` ($0 \leftrightarrow -244$) moves the cards off-screen with zero window reallocation and instantaneous 60fps GPU smoothness, while positioning a tactile bottom tab at $Y=0..36$ when collapsed.
- **Rule:** Never dynamically resize a transparent frameless window for expand/collapse transitions; use a fixed top-anchored window bounds and slide content vertically via `RenderTransform` KeySpline animations.



