# Lessons learned — GLOBAL (all projects)

General guardrails learned across projects. Read at session start (it's near the root
CLAUDE.md). Project-specific lessons live in each `projects/<name>/LESSONS.md`.
Add entries with `/lesson` (Scope: all-projects). Newest on top.

## Log

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
