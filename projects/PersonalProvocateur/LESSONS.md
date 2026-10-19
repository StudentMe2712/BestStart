# Lessons learned — PersonalProvocateur

A running log of mistakes and what fixed them, so Claude, Gemini, Antigravity (and you) don't repeat them.
Add entries with the `/lesson` command, or by hand. Newest on top.

> General lessons that apply to ALL projects also go in the root `LESSONS.md`.

## Log

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
