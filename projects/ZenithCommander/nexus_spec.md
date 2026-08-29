# Nexus Commander — Technical Specification & Architecture Manual

> **Nexus Commander** (Evolution from Zenith Commander)
> High-performance, modern Windows Explorer & WinUI-inspired file manager built on .NET 8 and Windows Presentation Foundation (WPF). Engineered with a sleek dark aesthetic, mouse-first workflows, UI virtualization, and comprehensive navigation capabilities.

---

## 1. Project Overview & Directive Pivot

Nexus Commander is a modern, responsive file management workstation featuring a single unified explorer view, an interactive sidebar (Quick Access + Logical Drives), clickable breadcrumb address bar, instant live search filtering, and an action ribbon.

### Architecture Highlights:
- **Platform**: .NET 8.0-windows (`net8.0-windows`), WPF with XAML styling and hardware-accelerated rendering.
- **Visual Design**: Sleek borderless window (`WindowStyle="None"`, `AllowsTransparency="True"`), WinUI dark palette (`#18181B`, `#202024`, `#27272D`, `#3B82F6` accent), subtle drop-shadows, rounded geometry, and custom scrollbars.
- **Sidebar Navigation Pane**: Instant jump to Quick Access locations (`Desktop`, `Downloads`, `Documents`, `Pictures`, `Music`, `Videos`, `Home`) and Logical Drives (`C:`, `D:`, etc.) with real-time storage telemetry.
- **Interactive Breadcrumbs & Address Bar**: Dynamic path segments allowing one-click navigation to any ancestor directory, with toggleable editable `TextBox` mode (`Ctrl+L` / `Alt+D`).
- **Live Search & In-Memory Filtering**: Instant zero-latency substring filtering across item names, file extensions, and file category types.
- **Action Ribbon Toolbar**: One-click mouse access to `New Folder`, `Copy`, `Cut`, `Paste`, `Rename`, `Delete`, `Properties`, and `Terminal`.
- **Spacious Virtualized Main View**: `ListView` with row height ~36px, semantic file type glyphs, formatted date modified, rich descriptive categories, and human-readable byte sizes.
- **Context Menus**: Complete context menu support on files/folders and on empty background space.
- **Asynchronous I/O Engine**: Non-blocking background enumerations with cancellation token support (`CancellationTokenSource`) and comprehensive exception interception.

---

## 2. Architectural Blueprint (MVVM)

```
projects/ZenithCommander/
├── ZenithCommander.csproj          # .NET 8.0-windows WPF Project (Assembly: NexusCommander)
├── App.xaml                        # Global Dark Theme Resources, Styles & Converters
├── App.xaml.cs                     # Application Entry Point
├── MainWindow.xaml                 # Custom Chrome Window, Sidebar, Breadcrumb, Ribbon, ListView
├── MainWindow.xaml.cs              # Window Drag/Resize, Global Shortcuts, Event Routing
│
├── Models/
│   ├── FileSystemItem.cs           # File & Directory entity, Category resolver, Byte formatter
│   ├── BreadcrumbItem.cs           # Clickable path segment model
│   ├── SidebarItem.cs              # Pinned Quick Access folders and Logical Drive items
│   └── NavigationHistory.cs        # Back and Forward history stack manager
│
├── ViewModels/
│   └── MainViewModel.cs            # Primary orchestrator: Navigation, Search, Clipboard, Dialogs
│
├── Views/
│   ├── InputDialog.xaml            # Modern modal dialog for New Folder and Rename
│   ├── InputDialog.xaml.cs         # Input dialog code-behind
│   ├── PropertiesDialog.xaml       # Modern file/folder metadata inspector
│   └── PropertiesDialog.xaml.cs    # Properties dialog with async directory size calculations
│
├── Helpers/
│   ├── ViewModelBase.cs            # INotifyPropertyChanged implementation with SetField
│   └── RelayCommand.cs             # Generic & parameterless ICommand implementations
│
├── Converters/
│   └── Converters.cs               # Type-to-brush, Boolean-to-visibility, Sidebar active converters
│
├── nexus_spec.md                   # Nexus Commander technical specification
└── zenith_spec.md                  # Evolution archive specification
```

---

## 3. Data Models & ViewModels

### 3.1 `FileSystemItem`
| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | File or folder name (e.g. `Projects`, `notes.txt`) |
| `FullPath` | `string` | Absolute path on disk |
| `IsDirectory` | `bool` | True if folder or drive |
| `Size` | `long?` | Byte count for files, null for directories |
| `FormattedSize` | `string` | Human-readable format (`1.4 MB`, `24.0 KB`, blank for folders) |
| `DateModified` | `DateTime?` | Last modification timestamp |
| `FormattedDate` | `string` | Invariant formatted date string `yyyy-MM-dd HH:mm` |
| `Extension` | `string` | Lowercase extension with dot (e.g. `.cs`, `.png`, `.zip`) |
| `IconGlyph` | `string` | Semantic glyph icon (`📁`, `⚡`, `⚙️`, `📦`, `🖼️`, `🎵`, `🎬`, `💻`, `📝`, `📕`, `📊`, `📽️`, `📄`) |
| `ItemType` | `string` | Descriptive category (e.g. `File folder`, `Application`, `C# Source File`, `PNG Image`, `Text Document`) |
| `Attributes` | `FileAttributes?` | File attributes (Hidden, System, ReadOnly, etc.) |

### 3.2 `BreadcrumbItem`
| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | Name of directory segment (e.g. `C:`, `Users`, `Mila`, `Desktop`) |
| `FullPath` | `string` | Full directory path up to this segment |
| `IsLast` | `bool` | True if current leaf folder (hides separator chevron `›`) |

### 3.3 `SidebarItem`
| Property | Type | Description |
| :--- | :--- | :--- |
| `Title` | `string` | Display label (e.g. `Desktop`, `Local Disk (C:)`) |
| `Path` | `string` | Absolute target directory path |
| `IconGlyph` | `string` | Sidebar icon (`🖥️`, `📥`, `📄`, `🖼️`, `🎵`, `🎬`, `🏠`, `💾`) |
| `Subtitle` | `string` | Capacity telemetry for drives (e.g. `142.5 GB free / 476.2 GB`) |
| `IsDrive` | `bool` | True if logical drive |
| `IsActive` | `bool` | True if current path matches item path (triggers blue accent indicator) |
| `UsagePercent`| `double` | Percentage of disk space utilized |

### 3.4 `NavigationHistory`
Maintains internal `_backStack` and `_forwardStack` collections of directory paths. Provides `CanGoBack`, `CanGoForward`, `GoBack()`, `GoForward()`, and `NavigateTo(string newPath)` with forward-stack truncation on new navigation branches.

### 3.5 `MainViewModel`
- **Asynchronous Execution & Cancellation**: Directory enumeration executes via `Task.Run()` utilizing `DirectoryInfo.EnumerateDirectories()` and `EnumerateFiles()`. Rapid navigation automatically cancels the preceding task using `CancellationTokenSource`.
- **Search & Live Filtering**: Updates visible `Items` in real-time as user types into `SearchQuery`.
- **Clipboard Management**: Supports both internal and Windows standard `Clipboard.SetFileDropList(...)` / `Clipboard.GetFileDropList()` for Cut/Copy/Paste operations.
- **Status Telemetry**: Dynamically calculates total item count, selected item count with cumulative byte size, and root drive free space.

---

## 4. UI Design & Virtualization Strategy

### 4.1 Color Palette Reference (WinUI Dark Slate)
```css
--bg-primary:   #18181B; /* Deep Charcoal / Slate Background */
--bg-secondary: #202024; /* Title Bar, Ribbon, Status Bar */
--bg-surface:   #27272D; /* Inputs, Cards, Address Bar */
--bg-hover:     #2A2A30; /* ListView Item Hover */
--border-subtle:#36363D; /* Panel Dividers & Borders */
--border-light: #4E4E58; /* Active Input Border */
--accent-blue:  #3B82F6; /* Primary Accent & Highlights */
--accent-dark:  #2563EB; /* Selected Item Fill */
--text-primary: #F1F5F9; /* High-contrast Crisp Text */
--text-muted:   #94A3B8; /* Subtitles, Headers & Secondary Info */
--color-folder: #ECC48D; /* Warm Amber for Directory Names */
--color-error:  #F87171; /* Warning & Error Red */
--color-success:#34D399; /* Operation Success Green */
```

### 4.2 UI Virtualization & Rendering Performance
- **VirtualizingStackPanel.IsVirtualizing="True"**: Only renders items visible in the current viewport (+ clean cache buffer), drastically reducing memory footprint when navigating folders with tens of thousands of items (e.g. `C:\Windows\System32` or `node_modules`).
- **VirtualizationMode="Recycling"**: Reuses existing visual containers (`ListViewItem`) as the user scrolls, avoiding repetitive garbage collection cycles and memory allocation churn.
- **ScrollViewer.IsDeferredScrollingEnabled="False"**: Delivers instant, smooth 60fps scrolling feedback.
- **TextTrimming="CharacterEllipsis"**: Prevents layout reflow and text overflow on long file names.

---

## 5. Mouse & Keyboard Matrix

| Action / Shortcut | Scope | Function |
| :--- | :--- | :--- |
| **Single Click** | Main List | Select file or folder |
| **Double Click** | Main List | Open folder or launch file with associated default Windows app |
| **Right Click (Item)** | Main List | Context Menu: Open, Copy, Cut, Paste, Rename, Delete, Copy Path, Properties |
| **Right Click (Empty)**| Main List | Context Menu: New Folder, Refresh, Paste, Open in Terminal |
| **Sidebar Click** | Sidebar | Immediately navigate to selected pinned folder or drive |
| **Breadcrumb Click** | Top Bar | Jump directly to clicked ancestor directory |
| `Alt+Left` / `BrowserBack` | Global | Navigate Back in history |
| `Alt+Right` / `BrowserForward` | Global | Navigate Forward in history |
| `Alt+Up` / `Backspace` | Global / List | Navigate Up to parent directory |
| `Ctrl+L` / `Alt+D` | Global | Focus Address Bar in editable text mode |
| `Ctrl+F` / `F3` | Global | Focus Live Search / Filter Box |
| `F5` / `Ctrl+R` | Global | Refresh current directory and storage telemetry |
| `Ctrl+Shift+N` | Global / List | Create New Folder (prompts with modal input dialog) |
| `F2` | Main List | Rename selected item |
| `Delete` | Main List | Delete selected item(s) with confirmation prompt |
| `Ctrl+C` | Main List | Copy selected item(s) |
| `Ctrl+X` | Main List | Cut selected item(s) |
| `Ctrl+V` | Global / List | Paste items into current directory |
| `Alt+Enter` | Main List | Open Properties inspector dialog |

---

## 6. Build Verification

```powershell
dotnet build projects/ZenithCommander/ZenithCommander.csproj -c Release
```
*Expected Output: `Build succeeded. 0 Warning(s), 0 Error(s)`*
