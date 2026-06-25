# Lessons learned — GLOBAL (all projects)

General guardrails learned across projects. Read at session start (it's near the root
CLAUDE.md). Project-specific lessons live in each `projects/<name>/LESSONS.md`.
Add entries with `/lesson` (Scope: all-projects). Newest on top.

## Log

### 2026-06-25 — System.Text.Json drops a Dictionary's custom comparer on round-trip
- **Problem:** SelectCast's currency converter worked on the first launch but silently returned "нет данных для USD" on every launch afterward and offline. Unit tests (with a hand-built dictionary) were all green and missed it.
- **Root cause:** the live rates fetch built `Rates` as `Dictionary<string,decimal>(StringComparer.OrdinalIgnoreCase)` with lower-case keys, and the converter looks codes up upper-cased (`USD`, `KZT`). On the cache path, `JsonSerializer.Deserialize<RateTable>` rebuilds `Rates` as a **default, case-sensitive** dictionary — STJ does not (and cannot) preserve a custom `IEqualityComparer`. So upper-case lookups missed the lower-case keys. The bug only ever appears after the value crosses the JSON boundary, never on first fetch — which is exactly why mocked tests didn't catch it but an end-to-end run did.
- **Fix:** normalize at the deserialization boundary — `table with { Rates = new Dictionary<…>(table.Rates, StringComparer.OrdinalIgnoreCase) }` right after `Deserialize`. Added a regression test that drives the real cache path (write JSON → load via a fresh service with no network → convert).
- **Rule:** a `Dictionary` with a non-default comparer loses that comparer through any serializer round-trip. If lookup correctness depends on the comparer (case-insensitive keys, culture), re-impose it after deserialization — and test the *deserialized* path, not just an in-memory instance.
- **Scope:** all-projects

### 2026-06-10 — Smart App Control silently blocks unsigned `.exe`s (ffmpeg) even after a clean install
- **Problem:** `winget install Gyan.FFmpeg` succeeded and `ffmpeg.exe` was on PATH, but running it failed with *"An Application Control policy has blocked this file."* `bun` and `python` from the same winget batch ran fine.
- **Root cause:** Windows 11 **Smart App Control** is ENFORCED on this machine (`HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy\VerifiedAndReputablePolicyState = 1`). SAC blocks *unsigned / un-reputable* standalone executables; the GyanD ffmpeg build is `NotSigned`. Signed runtimes (node, python, git, bun) pass. Confirmed via `Microsoft-Windows-CodeIntegrity/Operational` event **3118** "Smart App Control Block" firing at the exact run time.
- **Fix / options:** no clean per-app allow-list exists for SAC. Either (a) turn SAC off in Windows Security → App & browser control → Smart App Control — **IRREVERSIBLE** (can't re-enable without reinstalling Windows); (b) use a signed build (rare for ffmpeg); or (c) run ffmpeg-dependent skills (e.g. `transcribe`) on a machine without SAC. **npx/node/python-based MCP servers are unaffected** — the signed runtime executes the code.
- **Rule:** on Windows 11, before relying on any unsigned native `.exe` (ffmpeg, gcomp, custom tools), check SAC state; if enforced, expect blocks. Never disable SAC silently — it's irreversible, so surface it as the user's call.
- **Scope:** all-projects

### 2026-06-10 — Git for Windows ships a broken bundled ssh; point git at system OpenSSH with forward slashes
- **Problem:** on a fresh machine `git clone git@github.com:…` failed with *"Could not read from remote repository … make sure you have the correct access rights and the repository exists"* — yet `ssh -T git@github.com` (system OpenSSH) authenticated fine (*"Hi StudentMe2712!"*). The wording points at a missing/no-access repo; it was neither. The repo existed and the key was valid.
- **Root cause:** two layers. (1) Git's default ssh is the bundled MSYS `/usr/bin/ssh`, which on this box fails to even launch (exit 127), so git never reached GitHub. (2) Overriding with `GIT_SSH_COMMAND="C:\Windows\…\ssh.exe"` *also* failed: git runs that command through its bundled `sh`, which eats the backslashes (`C:\WINDOWS` → `C:WINDOWS` → "command not found").
- **Fix:** `git config --global core.sshCommand "C:/Windows/System32/OpenSSH/ssh.exe"` — full path with **forward** slashes (they survive `sh` parsing). Then clone/pull/push all work.
- **Diagnosis tip:** PowerShell 5.1 wraps a native command's stderr as `NativeCommandError`, hiding GitHub's real reply. Capture the raw stream with `Start-Process git -RedirectStandardError <file>` — seeing *"Repository not found"* means access/repo; seeing *nothing* means the local ssh died before connecting.
- **Rule:** on Windows, if git SSH ops fail but `ssh -T git@github.com` works, set `core.sshCommand` to the system OpenSSH path with forward slashes. Don't trust the "repository doesn't exist" wording — verify with `git ls-remote` via the system ssh first.
- **Scope:** all-projects

### 2026-06-09 — Multi-line `git commit -m` with embedded quotes breaks in PowerShell 5.1
- **Problem:** `git commit -q -m @'...'@` silently failed; git reported `pathspec 'across' did not match` and the commit didn't happen (changes left staged). The message contained `"Working across machines"`.
- **Root cause:** Windows PowerShell 5.1 mangles native-command arguments that contain embedded double-quotes — the `"..."` inside the message split the argument, so git parsed words as extra pathspecs.
- **Fix:** write the message to a temp file and use `git commit -F msg.txt` (then delete it). Avoids all PowerShell quoting/escaping of the message entirely.
- **Rule:** on Windows, commit multi-line or quote-containing messages with `git commit -F <file>`, not `-m "..."`.
- **Scope:** all-projects

### 2026-06-09 — `.ps1` scripts with non-ASCII need a UTF-8 BOM
- **Problem:** `new-project.ps1` failed to parse ("Missing closing '}'") after adding an em-dash to a `Write-Host "..."` string. The em-dash, not a brace, was the cause.
- **Root cause:** Windows PowerShell 5.1 reads BOM-less files as Windows-1252. A UTF-8 em-dash (`E2 80 94`) decodes to `â€"` — the embedded `"` prematurely closes the string, cascading into a brace error. (Inside `@"..."@` here-strings and `#` comments it's harmless, so symptoms can be confusing.)
- **Fix:** save scripts as **UTF-8 with BOM**. The editor tools write BOM-less UTF-8 and strip the BOM on every edit, so re-apply after each change: `[IO.File]::WriteAllText($p, [IO.File]::ReadAllText($p,[Text.Encoding]::UTF8), (New-Object Text.UTF8Encoding $true))`. Also read template files with explicit UTF-8, not `Get-Content` (defaults to ANSI in 5.1).
- **Rule:** any `.ps1` (or file `Get-Content`-ed by 5.1) containing non-ASCII must be UTF-8 **with BOM**; re-add the BOM after editing and syntax-check with `[Parser]::ParseFile`.
- **Scope:** all-projects

### 2026-06-09 — Windows MCP servers need `cmd /c`
- **Problem:** MCP servers configured with bare `"command": "npx"` fail to start on Windows.
- **Root cause:** Claude Code on Windows doesn't resolve `npx` directly as a process.
- **Fix:** wrap as `"command": "cmd", "args": ["/c", "npx", "-y", "<pkg>"]`.
- **Rule:** on Windows, always launch npx-based MCP servers via `cmd /c`.
- **Scope:** all-projects
