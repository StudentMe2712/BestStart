# Zenith Commander — Technical Specification & Architecture Manual

> **Level 1 — Zenith Commander Inception (Dual-Panel File Manager)**
> High-performance, cyberpunk dark-themed dual-panel file manager built on .NET 8 & Windows Presentation Foundation (WPF).

---

## 1. Project Overview & Status

Zenith Commander is an ultra-fast, keyboard-driven, dual-panel file management workstation engineered for maximum productivity, responsiveness, and aesthetic precision.

### Stage 1 Checklist
- [x] **1.1 Project Structure & SDK Initialization**: `net8.0-windows`, WPF enabled, nullable reference types, implicit usings, modular MVVM folder architecture (`Models/`, `ViewModels/`, `Controls/`, `Helpers/`, `Converters/`).
- [x] **1.2 Dark Theme Design System**: Sleek color palette (`#1E1E20`, `#252528`, `#2D2D32`, `#3E3E42`, `#0078D4` / `#3B82F6`, `#ECC48D`, `#89DDFF`), custom slim scrollbar styles with rounded thumbs, custom `ListView` / `ListViewItem` templates, and border glow effects.
- [x] **1.3 Asynchronous File System Engine**: Non-blocking `LoadDirectoryAsync` on background threads with `Task.Run()`, directory sorting (`..` parent first, directories A-Z, files A-Z), human-readable sizes (B/KB/MB/GB/TB), resilient error handling (`UnauthorizedAccessException`, `IOException`, `SecurityException`, `DirectoryNotFoundException`), and drive capacity calculations.
- [x] **1.4 Dual-Panel UI & Keyboard Workflows**: Interactive breadcrumb/path bar, drive chip selector, UI virtualization (`VirtualizingStackPanel.IsVirtualizing="True"`, `VirtualizationMode="Recycling"`), grid splitter, custom title bar with window controls, bottom function hotkey bar (`F3`–`F8`, `Alt+F4`), and seamless `Tab`/`Enter`/`Backspace` navigation.

---

## 2. Architectural Blueprint (MVVM)

```
ZenithCommander/
├── ZenithCommander.csproj          # .NET 8.0-windows WPF Project
├── App.xaml                        # Global Dark Theme Resources, Styles & Converters
├── App.xaml.cs                     # Application Entry Point
├── MainWindow.xaml                 # Custom Chrome Window, Dual Panel Splitter, Hotkey Bar
├── MainWindow.xaml.cs              # Window Drag/Resize, Global Key Event Routing
│
├── Models/
│   └── FileSystemItem.cs           # File & Directory entity, Icon resolver, Byte formatter
│
├── ViewModels/
│   ├── FilePanelViewModel.cs       # Independent file panel state, async navigation, drive scan
│   └── MainViewModel.cs            # Orchestrator: Left/Right panels, Active state, File operations
│
├── Controls/
│   ├── FilePanelView.xaml          # Reusable File Panel UserControl with virtualized ListView
│   └── FilePanelView.xaml.cs       # Panel interaction events (Double Click, KeyDown, Focus)
│
├── Helpers/
│   ├── ViewModelBase.cs            # INotifyPropertyChanged implementation with SetField
│   └── RelayCommand.cs             # Generic & parameterless ICommand implementations
│
├── Converters/
│   └── Converters.cs               # Type-to-brush, Boolean-to-visibility, Active-state brushes
│
└── zenith_spec.md                  # Specification, Performance Analysis, & Testing Guide
```

---

## 3. Data Models & ViewModels

### 3.1 `FileSystemItem`
| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | File or folder name (e.g. `[..]`, `Projects`, `notes.txt`) |
| `FullPath` | `string` | Absolute path on disk |
| `IsDirectory` | `bool` | True if folder or drive |
| `IsParentDirectory` | `bool` | True if `[..]` upward navigation link |
| `Size` | `long?` | Byte count for files, null for directories |
| `FormattedSize` | `string` | `<DIR>`, `<UP-DIR>`, or human-readable format (`1.45 MB`, `24.0 KB`) |
| `DateModified` | `DateTime?` | Last modification timestamp (UTC-adjusted local) |
| `FormattedDate` | `string` | Invariant formatted date string `yyyy-MM-dd HH:mm` |
| `Extension` | `string` | Uppercase extension without leading period (e.g. `EXE`, `ZIP`, `CS`) |
| `IconGlyph` | `string` | Semantic glyph icon (`📁`, `⬆️`, `⚡`, `⚙️`, `📦`, `🖼️`, `🎵`, `🎬`, `💻`, `📝`, `📄`) |

### 3.2 `FilePanelViewModel`
- **Asynchronous Execution**: Directory enumeration executes via `Task.Run()` utilizing `DirectoryInfo.EnumerateDirectories()` and `EnumerateFiles()`.
- **Fault-Tolerant I/O**: Intercepts `UnauthorizedAccessException`, `DirectoryNotFoundException`, `IOException`, and `PathTooLongException` gracefully without crashing, restoring the previous valid path and displaying an in-panel warning banner.
- **Drive Telemetry**: Detects system and removable drives on initialization and computes real-time free/total capacity using `DriveInfo`.

### 3.3 `MainViewModel`
- Maintains `LeftPanel` (defaults to system drive) and `RightPanel` (defaults to user profile folder).
- Tracks `ActivePanel` and `InactivePanel` for cross-panel operations (Copy `F5`, Move `F6`).
- Exposes hotkey commands (`ViewFileCommand`, `EditFileCommand`, `NewFolderCommand`, `DeleteItemCommand`, `ExitCommand`).

---

## 4. UI Design & Virtualization Strategy

### 4.1 Color Palette Reference
```css
--bg-primary:   #1E1E20; /* Deep Obsidian Slate */
--bg-secondary: #252528; /* Panel Headers & Hotkey Bar */
--bg-surface:   #2D2D32; /* Button and Card Surface */
--border-subtle:#3E3E42; /* Divider and Panel Borders */
--accent-blue:  #3B82F6; /* Active Focus Highlight */
--accent-dark:  #0078D4; /* Selection Fill */
--text-primary: #EDEDED; /* Crisp White Text */
--text-muted:   #9E9E9E; /* Metadata / Header Text */
--color-folder: #ECC48D; /* Warm Amber for Directories */
--color-file:   #89DDFF; /* Cyan Accent for Data Files */
--color-error:  #F87171; /* Warning & Error Red */
```

### 4.2 UI Virtualization & Rendering Performance
- **VirtualizingStackPanel.IsVirtualizing="True"**: Only renders items visible in the current viewport (+ clean cache buffer), drastically reducing memory footprint when navigating folders with tens of thousands of items (e.g. `C:\Windows\System32` or `node_modules`).
- **VirtualizationMode="Recycling"**: Reuses existing visual containers (`ListViewItem`) as the user scrolls, avoiding repetitive garbage collection cycles and memory allocation churn.
- **ScrollViewer.IsDeferredScrollingEnabled="False"**: Delivers instant, smooth 60fps scrolling feedback.
- **TextTrimming="CharacterEllipsis"**: Prevents layout reflow and text overflow on long file names.

---

## 5. Keyboard Navigation & Hotkey Matrix

| Key / Hotkey | Scope | Function |
| :--- | :--- | :--- |
| `Tab` | Global | Switch focus between Left and Right file panels |
| `Enter` | Panel | Open selected folder or launch file with associated system handler |
| `Backspace` / `Alt+Left`| Panel | Navigate up to parent directory (`..`) |
| `Ctrl+R` | Panel | Refresh current directory listing and free space telemetry |
| `F3` | Bottom Bar / Key | **View**: Preview or launch selected file |
| `F4` | Bottom Bar / Key | **Edit**: Open selected file directly in Notepad |
| `F5` | Bottom Bar / Key | **Copy**: Copy selected item(s) to the inactive panel's directory |
| `F6` | Bottom Bar / Key | **Move**: Move selected item(s) to the inactive panel's directory |
| `F7` | Bottom Bar / Key | **NewFolder**: Create a new directory in active panel |
| `F8` / `Delete` | Bottom Bar / Key | **Delete**: Delete selected item with confirmation prompt |
| `Alt+F4` | Bottom Bar / Key | **Exit**: Gracefully terminate the application |

---

## 6. Verification & Testing Guide

### 6.1 Build Verification
```powershell
dotnet build projects/ZenithCommander/ZenithCommander.csproj -c Release
```
*Expected Output: `Build succeeded. 0 Warning(s), 0 Error(s)`*

### 6.2 Manual Testing Checklist
1. **Startup & Layout**:
   - Launch application; verify smooth window drop-shadow and rounded custom chrome.
   - Confirm Left Panel loads `C:\` and Right Panel loads the user's home directory.
   - Confirm drive chips (`C:`, `D:`, etc.) are rendered at the top of both panels.
2. **Dual-Panel Navigation**:
   - Press `Tab`: confirm active panel switches smoothly with active blue border indicator.
   - Double-click or press `Enter` on any folder: confirm fast navigation.
   - Double-click or press `Enter` on `[..]`: confirm navigation back up.
   - Press `Backspace`: confirm parent navigation.
3. **Editable Path Bar**:
   - Type `C:\Windows` into the path TextBox and press `Enter`: confirm panel loads the directory immediately.
4. **Large Directory Stress Test**:
   - Navigate into `C:\Windows\System32` (approx. 4,000–5,000 files/folders):
   - Confirm directory loads asynchronously without freezing the UI.
   - Scroll through the list rapidly; verify 60fps smooth scrolling with UI virtualization.
5. **Access Denied / Exception Handling**:
   - Navigate into a protected directory (e.g. `C:\System Volume Information`):
   - Confirm application does not crash; verify red notification banner displays `Access Denied` and restores previous valid directory.
6. **Cross-Panel File Operations**:
   - Select a test file in Left Panel, press `F5`: verify confirmation dialog prompts to copy to Right Panel path.
   - Press `F7`: verify new directory creation.
   - Press `F4`: verify text file opens in Notepad.
   - Press `F8`: verify delete confirmation dialog.