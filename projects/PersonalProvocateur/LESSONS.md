# Lessons learned — PersonalProvocateur

A running log of mistakes and what fixed them, so Claude, Gemini, Antigravity (and you) don't repeat them.
Add entries with the `/lesson` command, or by hand. Newest on top.

> General lessons that apply to ALL projects also go in the root `LESSONS.md`.

## Log

### 2026-10-29 — Windows PE ApplicationIcon embedding & multi-resolution ICO generation
- **Problem:** Compiled `.exe` binary showed the generic Windows blank executable icon in File Explorer and Windows Shell because `<ApplicationIcon>` was not declared in `.csproj`, and tray icon was only dynamically created in memory.
- **Root cause:** Windows PE executable file icons must be embedded at compile-time as Win32 resources (`RT_GROUP_ICON`) via MSBuild `<ApplicationIcon>Assets\app.ico</ApplicationIcon>`. Windows requires standard ICO packaging (DIB headers for 16..128, PNG for 256) with alpha channel preservation.
- **Fix:** 1) Implemented `TrayIconService.CreateIcoData()` exporting multi-resolution `.ico` binaries (16, 24, 32, 48, 64, 128, 256) matching the procedural crystal icon. 2) Committed `Assets/app.ico` and configured `<ApplicationIcon>Assets\app.ico</ApplicationIcon>` in `PersonalProvocateur.App.csproj`. 3) Added fallback loading from `Assets/app.ico` in `CreateApplicationIcon()` and `CreateTaskbarIconSource()`. 4) Verified via `AppIconTests` extracting the icon directly from the compiled `.exe`.
- **Rule:** For desktop .NET executables, always configure `<ApplicationIcon>` in `.csproj` with a multi-resolution `.ico` (16..256px) so Windows Explorer and Task Manager display the brand icon out-of-the-box.
- **Scope:** this-project

### 2026-10-25 — WPF RenderTargetBitmap cross-thread procedural rendering & WindowsForms namespace collisions
- **Problem:** 1) `CS0104: 'FontFamily'/'FlowDirection' is an ambiguous reference` when using WPF typography inside projects with `<UseWindowsForms>true</UseWindowsForms>`. 2) `RenderTargetBitmap` rendering fails if invoked from non-STA background or unit test worker threads.
- **Root cause:** 1) `System.Drawing.FontFamily` and `System.Windows.Forms.FlowDirection` clash with `System.Windows.Media.FontFamily` and `System.Windows.FlowDirection`. 2) WPF visuals and `RenderTargetBitmap` require an STA apartment thread.
- **Fix:** 1) Explicitly alias `using FontFamily = System.Windows.Media.FontFamily;` and `using FlowDirection = System.Windows.FlowDirection;`. 2) Check `Thread.CurrentThread.GetApartmentState() == ApartmentState.STA`; if not STA, run procedural thumbnail rendering on a dedicated spawned STA thread with `.Join()`.
- **Rule:** When rendering WPF Visuals/RenderTargetBitmap in background services, always verify STA thread apartment or marshal to a dedicated STA worker, and explicitly alias clashing WindowsForms graphics types.
- **Scope:** this-project

### 2026-10-19 — GDI+ Transparent Crystal Icon Generation and WPF Numeric Stepper Two-Way Binding
- **Problem:** 1) Generating Win32 HICON for Taskbar without a background square requires 32-bit ARGB with proper alpha channel preservation. 2) Replacing Sliders with TextBoxes and Steppers requires robust handling of non-numeric input, bounds clamping (1..120, min..480), and debounced saving to prevent race conditions during rapid typing.
- **Root cause:** 1) Default `Graphics.Clear` or non-ARGB bitmaps in GDI+ render black backgrounds on Windows taskbars. 2) WPF `TextBox.TextChanged` fires on every keystroke, including empty or incomplete numeric states (e.g. while backspacing).
- **Fix:** 1) Used `PixelFormat.Format32bppArgb` with `g.Clear(Color.Transparent)` and high-quality smoothing modes when drawing polygon crystal facets with HIcon conversion. 2) Validated numeric keys in `PreviewKeyDown`, clamped values with `Math.Clamp` on `LostFocus` and stepper clicks, and synchronized auto-save via debounced timer.
- **Rule:** For transparent programmatic icons in WPF/Win32, always use 32bpp ARGB bitmaps with explicit transparency clearing and handle numeric text box input with `PreviewKeyDown` filter + `LostFocus` boundary clamping.
- **Scope:** this-project

### 2026-10-19 — WPF static Freezable brushes and cross-STA-thread unit test execution
- **Problem:** `InvalidOperationException: Cannot use a DependencyObject that belongs to a different thread than its parent Freezable` when assigning `static readonly Brush` fields to UI elements in tests running across multiple STA threads.
- **Root cause:** WPF `Brush` (and other `Freezable` types) instantiated statically on one STA thread are bound to that thread by default. If another test runs in a new STA thread and accesses or assigns these static brushes to controls, WPF throws thread ownership exceptions.
- **Fix:** Always call `.Freeze()` on static brushes (e.g., via helper `CreateFrozenBrush(...)`), or use frozen brushes from `Brushes.*`. Once frozen, a Freezable is thread-safe and can be accessed across any thread.
- **Rule:** All static `Brush`, `Pen`, or `Freezable` definitions in WPF components MUST be frozen (`brush.Freeze()`) to permit safe cross-STA-thread usage in test runners and background threads.
- **Scope:** this-project

### 2026-10-19 — WPF BAML Package stream concurrency in xUnit test suites
- **Problem:** Intermittent `NullReferenceException: Object reference not set to an instance of an object at System.IO.Packaging.PackagePart.IsStreamClosed(Stream s)` during `Application.LoadComponent` in unit tests loading UserControls or Windows.
- **Root cause:** WPF's internal `System.IO.Packaging.PackagePart` stream collection is not thread-safe. Concurrent instantiation of XAML components across parallel xUnit test threads creates race conditions when accessing BAML package streams.
- **Fix:** Added `[assembly: CollectionBehavior(DisableTestParallelization = true)]` to `tests/PersonalProvocateur.Tests/AssemblyInfo.cs` to ensure single-threaded sequential execution of WPF window/control test fixtures.
- **Rule:** For test suites containing WPF UI components or BAML loading, always disable parallelization via `CollectionBehavior` to prevent `PackagePart` internal collection corruption.
- **Scope:** this-project

### 2026-10-19 — WPF Window Loaded reflection ambiguity and Mandatory Watch Lock close guard
- **Problem:** 1) `AmbiguousMatchException: Ambiguous match found for 'OverlayWindow Void OnLoaded(Object, RoutedEventArgs)'` when querying `GetMethod("OnLoaded", ...)` via reflection in tests. 2) Closing windows in `OverlayCoordinator.CompleteSession()` being cancelled when `VideoWindow` has closing protection.
- **Root cause:** 1) Base `FrameworkElement` defines non-public loaded lifecycle methods that clash with window event handlers when reflected without explicit type signatures. 2) Closing guard in `OnClosing` blocks `Window.Close()` unless `AllowClose = true` is explicitly granted beforehand.
- **Fix:** 1) Trigger loaded event handlers via `window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent))` instead of fragile private reflection. 2) Always set `AllowClose = true` on `VideoWindow` and `CanClose = true` on `OverlayWindow` in coordinator before calling `Close()`.
- **Rule:** Use `RaiseEvent(LoadedEvent)` to simulate WPF window loading in tests, and always grant close authorization flags on windows before programmatically closing them from coordinators.
- **Scope:** this-project

### 2026-10-19 — WPF StaticResource Resolution on Root Window and SystemEvents DisplaySettingsChanged
- **Problem:** 1) `XamlParseException: Cannot find resource named 'BackgroundBrush'` when root `<Window Background="{StaticResource BackgroundBrush}">` is parsed in unit tests. 2) Memory leak or invalid UI thread access on screen resolution changes (`SystemEvents.DisplaySettingsChanged`).
- **Root cause:** 1) Properties declared on root `<Window>` element are evaluated by XAML before `<Window.Resources>` is populated. 2) Static event `SystemEvents.DisplaySettingsChanged` holds references to window instances indefinitely if not unsubscribed, and fires on a non-UI thread.
- **Fix:** 1) Use literal hex background (`Background="#12131A"`) on root `<Window>` elements. 2) Marshal `DisplaySettingsChanged` handlers via `Dispatcher.InvokeAsync` and strictly unsubscribe in `OnClosed(EventArgs e)`.
- **Rule:** Never use StaticResource on the root element of a standalone window, and always unsubscribe from Win32 static events in `OnClosed`.
- **Scope:** this-project
- **Problem:** 1) `error MC3072: property 'IsTabStop' does not exist in XML namespace` on `Grid` or `Border`. 2) `error CS0104: 'Brush'/'Pen'/'UserControl' is an ambiguous reference between System.Drawing/System.Windows.Forms and System.Windows.Media/System.Windows.Controls`.
- **Root cause:** `IsTabStop` is defined only on `Control` subclasses, not on `Panel` (`Grid`) or `Border`. Also, `<UseWindowsForms>true</UseWindowsForms>` imports `System.Drawing` and `System.Windows.Forms` which clash with `System.Windows.Media.Brush/Pen` and `System.Windows.Controls.UserControl`.
- **Fix:** 1) Use attached property `KeyboardNavigation.IsTabStop="False"` on non-`Control` elements (`Grid`, `Border`). 2) Explicitly alias `using Brush = System.Windows.Media.Brush;`, `using Pen = System.Windows.Media.Pen;`, `using UserControl = System.Windows.Controls.UserControl;`.
- **Rule:** When mixing WPF and WinForms, always alias graphics brushes/pens and use `KeyboardNavigation.IsTabStop` on layout containers.
- **Scope:** this-project

### 2026-10-19 — WPF Standalone Window Resources and WindowsForms Namespace Collisions
- **Problem:** When instantiating WPF Windows in unit tests or when `<UseWindowsForms>true</UseWindowsForms>` is enabled in `.csproj`, errors occur: 1) `XamlParseException: Cannot find resource named '...'`, 2) `CS0104: 'Application'/'Color'/'Size'/'KeyEventArgs' is an ambiguous reference between System.Windows.Forms and System.Windows`.
- **Root cause:** Unit tests do not execute `App.xaml` by default so `Application.Current.Resources` is unpopulated. Also, `UseWindowsForms` imports WinForms namespaces that collide with WPF standard types.
- **Fix:** 1) Merge `Theme/DarkTheme.xaml` directly into `<Window.Resources>` of each window so they are 100% self-contained. 2) Explicitly qualify or alias WPF types (`System.Windows.Application`, `using Color = System.Windows.Media.Color;`, `using Size = System.Windows.Size;`). 3) Run WPF window instantiation tests on STA thread (`ApartmentState.STA`).
- **Rule:** Keep WPF windows self-contained with merged theme dictionaries, always disambiguate types when mixing WinForms NotifyIcon and WPF, and run window unit tests in STA threads.
- **Scope:** this-project

### 2026-10-19 — Win32 Named Mutex thread re-entrancy and SingleInstance guard tests
- **Problem:** When testing `SingleInstanceGuard` within the same thread in unit tests, calling `WaitOne` on a named Mutex acquired by another instance returns `true` instead of `false`.
- **Root cause:** Win32 Mutex has thread affinity and is inherently recursive/re-entrant for the thread that currently owns it.
- **Fix:** In `TryAcquire()`, determine single-instance exclusivity based on `new Mutex(true, name, out bool createdNew)`. If `createdNew == false`, immediately return `false` without calling re-entrant `WaitOne`.
- **Rule:** For single-instance mutex guards in .NET, rely on `createdNew` from `new Mutex(true, ...)` to ensure process exclusivity and avoid false positives from thread re-entrancy.
- **Scope:** this-project

### 2026-10-19 — Dynamic SDK targeting for .NET 10 / .NET 9 cross-environment builds
- **Problem:** When building a project targeting `net10.0-windows` on an environment with only .NET 9 SDK (`9.0.317`), `dotnet build` fails immediately with `NETSDK1045: current SDK does not support .NET 10.0`.
- **Root cause:** Hardcoded `<TargetFramework>net10.0-windows</TargetFramework>` without conditional SDK inference fails on machines where .NET 10 SDK is not yet globally installed.
- **Fix:** In `.csproj`, configure conditional TargetFramework based on `$(NETCoreSdkVersion)`:
  `<TargetFramework Condition="$(NETCoreSdkVersion.StartsWith('10.'))">net10.0-windows</TargetFramework>`
  `<TargetFramework Condition="!$(NETCoreSdkVersion.StartsWith('10.'))">net9.0-windows</TargetFramework>`.
- **Rule:** For forward-compatible .NET LTS projects, use conditional TargetFramework probing `$(NETCoreSdkVersion)` to build cleanly across preview and LTS environments.
- **Scope:** this-project

### 2026-10-19 — Decoupling file system checks for ShuffleBag and VideoLibrary unit tests
- **Problem:** `VideoShuffleBag` and `VideoLibraryService` rely on `File.Exists(FilePath)` to filter valid videos, causing unit tests to fail unless real `.mp4` files are written to physical temp disk.
- **Root cause:** Direct static call to `File.Exists` couples business logic with physical disk state.
- **Fix:** Added optional delegate `Func<string, bool>? fileExistsChecker = null` (defaulting to `File.Exists`) in `VideoShuffleBag` and `VideoLibraryService` constructors.
- **Rule:** Always allow injecting a file existence predicate into library services to keep tests fast, in-memory, and deterministic.
- **Scope:** this-project
