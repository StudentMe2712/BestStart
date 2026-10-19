# Lessons learned — PersonalProvocateur

A running log of mistakes and what fixed them, so Claude, Gemini, Antigravity (and you) don't repeat them.
Add entries with the `/lesson` command, or by hand. Newest on top.

> General lessons that apply to ALL projects also go in the root `LESSONS.md`.

## Log

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
