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
