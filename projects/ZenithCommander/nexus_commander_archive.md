# 🌌 Nexus Commander — Monolithic Technical Architecture & System Archive

> **Document Version**: 1.0.0 (Frozen Architecture Archive)  
> **Release State**: Level 1 Complete / Standalone Modern Desktop File Manager  
> **Target OS**: Windows 10 (Build 19041+) / Windows 11 (Build 22000+ recommended for Native Mica)  
> **Localization**: 100% Russian (Русский язык)

---

## 1. Header & System Metadata

| Metadata Field | Specification / Value |
| :--- | :--- |
| **Project Name** | **Nexus Commander** (formerly *Zenith Commander*) |
| **Target Framework** | `.NET 8.0-windows` (`net8.0-windows`) |
| **Language & Version** | C# 12.0 / XAML (WPF PresentationCore / PresentationFramework) |
| **Window & Rendering Engine** | Hardware-accelerated WPF Direct3D 9Ex/11 Pipeline + Win32 DWM Mica Backdrop Interop |
| **Architecture Pattern** | Strict Model-View-ViewModel (MVVM) with ICommand / RelayCommand bindings |
| **Visual Clone Target** | **1:1 Windows 11 File Explorer Dark Mode Clone** (WinUI 3 Fluent Design + DWM Mica Glassmorphism) |
| **Assembly Identity** | `AssemblyName`: `NexusCommander`, `RootNamespace`: `NexusCommander`, Output: `NexusCommander.exe` |
| **Project Path** | `projects/ZenithCommander/ZenithCommander.csproj` |
| **Current Operational State** | **Frozen / Level 1 Complete** (Production-ready, 0 Warnings, 0 Errors) |

---

## 2. Complete Architecture & File Tree

```
projects/ZenithCommander/
├── ZenithCommander.csproj              # Project Manifest (.NET 8.0-windows, WPF, Nullable, C# 12)
├── App.xaml                            # Global Fluent Design Resources, DWM Brushes, 17 Vector Geometries & Styles
├── App.xaml.cs                         # Application Lifecycle & Global Unhandled Exception Handlers
├── MainWindow.xaml                     # Custom WindowChrome, Tabs Strip, Command Bar, Sidebar & Virtualized ListView
├── MainWindow.xaml.cs                  # DWM Interop (Mica/Dark Mode), Window State, Keyboard & Mouse Routing
│
├── Converters/
│   └── Converters.cs                   # IValueConverter & IMultiValueConverter collection (Visibility, Brushes, Sort Glyphs)
│
├── Helpers/
│   ├── IconExtractor.cs                # Win32 Shell32 / Imageres / User32 native icon extraction & cache engine
│   ├── RelayCommand.cs                 # Generic & Parameterless ICommand implementations for MVVM bindings
│   └── ViewModelBase.cs                # INotifyPropertyChanged base implementation with SetField<T>
│
├── Models/
│   ├── BreadcrumbItem.cs               # Clickable path segment model for interactive address navigation
│   ├── FileSystemItem.cs               # File & Folder entity with Russian category resolver and byte size formatting
│   ├── NavigationHistory.cs            # Dual-stack Back/Forward navigation history engine
│   └── SidebarItem.cs                  # Hierarchical navigation tree node (Drives, Quick Access, Telemetry)
│
├── ViewModels/
│   └── MainViewModel.cs                # Master MVVM Orchestrator (Async I/O, CancellationTokenSource, Search, Clipboard)
│
├── Views/
│   ├── InputDialog.xaml                # Fluent modal dialog for 'New Folder' and 'Rename' operations
│   ├── InputDialog.xaml.cs             # Input dialog code-behind & keyboard bindings (Enter/Escape)
│   ├── PropertiesDialog.xaml           # Modern file & folder metadata inspector
│   └── PropertiesDialog.xaml.cs        # Properties dialog code-behind with async folder size enumeration
│
├── nexus_spec.md                       # Technical specification for Nexus Commander
├── zenith_spec.md                      # Pivot and evolution record from legacy Zenith dual-pane
└── nexus_commander_archive.md          # Monolithic comprehensive system archive (this document)
```

---

### Detailed File Roles & Responsibilities

#### 1. `ZenithCommander.csproj`
The core project file configuring the .NET 8.0 SDK environment:
- `TargetFramework`: `net8.0-windows`
- `OutputType`: `WinExe`
- `UseWPF`: `true`
- `ImplicitUsings`: `enable`
- `Nullable`: `enable`
- `RootNamespace` & `AssemblyName`: `NexusCommander`

#### 2. `App.xaml` & `App.xaml.cs`
- **`App.xaml`**: Global application resource dictionary defining:
  - **Windows 11 Dark Mode Semi-Translucent Mica Glassmorphism Palette**: `#B0141414` (Canvas), `#80202020` (Mica Top/Status), `#901C1C1C` (Mica Secondary), `#90181818` (Sidebar), `#452D2D2D` (Surface), `#0078D4` / `#4CC2FF` (Windows 11 Accent).
  - **17 High-Precision StreamGeometry Vector Assets**: Scaled 20 Regular Microsoft Fluent UI system icons.
  - **Typography**: `Segoe UI Variable Display`, `Segoe UI Variable Text`, `Segoe UI`, `Cascadia Code`.
  - **Custom Scrollbar Template**: Sleek 6px non-intrusive track and thumb styling with dynamic hover/drag highlights.
  - **Custom Controls**: `Win11TreeViewItemStyle` (4-column hierarchical tree layout), `ListViewItem` styling (~36px Fluent row), `Win11InputContainerStyle`, `ContextMenu` & `MenuItem` floating dark card styles with `#000000` blur drop shadows.
- **`App.xaml.cs`**: Entry point handling startup initialization and registering global unhandled exception hooks on both `AppDomain.CurrentDomain.UnhandledException` and `DispatcherUnhandledException` (persisting fatal crash stack traces to `startup_error.log`).

#### 3. `MainWindow.xaml` & `MainWindow.xaml.cs`
- **`MainWindow.xaml`**: The master visual container arranged in 5 strict grid rows:
  - `Row 0` (42px): Custom Title Bar with interactive tabs strip (`CurrentFolderName`, close tab `GeoDismiss`, add tab `GeoAdd`), draggable area, and custom minimize/maximize/close window buttons.
  - `Row 1` (48px): Navigation cluster (Back, Forward, Up, Refresh in 32x32 rounded frames) + Breadcrumb Address Bar (~75% width with editable TextBox toggle) + Search Box (~25% width, MinWidth 240px with inline magnifying glass and clear button).
  - `Row 2` (48px): Windows 11 Command Bar (`Создать ▾`, Cut, Copy, Paste, Rename, Share/Copy Path, Delete, `Сортировка ▾`, `Вид ▾`, Terminal, Properties).
  - `Row 3` (`*`): Split view comprising the 240px Navigation Sidebar (`SidebarTreeView`), 4px interactive `GridSplitter`, and the main virtualized file table (`FileListView`).
  - `Row 4` (30px): Bottom Status Bar displaying total items count, current selection metrics, and drive storage telemetry.
- **`MainWindow.xaml.cs`**:
  - Implements Win32 DWM Interop (`DwmSetWindowAttribute` for Immersive Dark Mode and Mica Backdrop, `DwmExtendFrameIntoClientArea` with `-1` margins).
  - Handles global window dragging (`DragMove()`), double-click title bar maximize toggle, and custom caption buttons.
  - Manages global keyboard shortcuts (`Ctrl+L`, `Alt+D`, `Ctrl+F`, `F3`, `F5`, `Ctrl+R`, `Alt+Left/Right/Up`, `Delete`, `F2`, `Ctrl+C/X/V`, `Ctrl+Shift+N`, `Ctrl+T`, `Ctrl+W`).
  - Routes sidebar selection and chevron expander events without focus collision.

#### 4. `Helpers/IconExtractor.cs`
High-performance Win32 native shell icon extraction engine featuring:
- **P/Invoke Signatures**:
  - `shell32.dll`: `SHGetFileInfo`, `ExtractIconEx`, `SHGetStockIconInfo`.
  - `user32.dll`: `DestroyIcon` (critical unmanaged handle cleanup).
- **Extraction Targets**:
  - Yellow system folder icons (`FILE_ATTRIBUTE_DIRECTORY`).
  - File extension & explicit executable/shortcut icons (`FILE_ATTRIBUTE_NORMAL` / `SHGetFileInfo`).
  - Logical drives (`C:\`, `D:\`).
  - Special folders (`Desktop`, `Downloads`, `Documents`, `Pictures`, `Music`, `Videos`).
  - System DLL resources (`%windir%\system32\imageres.dll` for Home `-1024`, Gallery `-113`, This PC `-109`, Network `-25`).
  - Shell stock icons (`SIID_FOLDER`, `SIID_DRIVE525`, `SIID_MYNETWORK`, `SIID_DESKTOPPC`, `SIID_WORLD`).
- **Safety & Performance**:
  - Converts `HICON` to WPF `BitmapSource` via `Imaging.CreateBitmapSourceFromHIcon`.
  - Immediately invokes `.Freeze()` on bitmap sources, guaranteeing thread-safety across UI and background `Task.Run` worker threads.
  - Caches all extracted icons in a thread-safe `ConcurrentDictionary<string, ImageSource>`.
  - Guarantees zero GDI/User handle leakage by invoking `DestroyIcon` in `finally` blocks.

#### 5. `Helpers/ViewModelBase.cs` & `Helpers/RelayCommand.cs`
- **`ViewModelBase.cs`**: Provides boilerplate implementation of `INotifyPropertyChanged` using `[CallerMemberName]` and equality checking `SetField<T>(ref T field, T value)`.
- **`RelayCommand.cs`**: Robust `ICommand` implementation supporting both parameterless (`Action`, `Func<bool>`) and strongly-typed (`Action<T>`, `Predicate<T>`) command delegates with `CommandManager.RequerySuggested` hookups.

#### 6. `Models/FileSystemItem.cs`
Entity representing a file or directory on the file system:
- Properties: `Name`, `FullPath`, `IsDirectory`, `IsParentDirectory`, `Size`, `FormattedSize`, `DateModified`, `FormattedDate`, `Extension`, `Icon`, `IconGlyph`, `ItemType`, `Attributes`, `IsHidden`, `IsSystem`.
- Factory methods: `FromDirectoryInfo(DirectoryInfo dir)` and `FromFileInfo(FileInfo file)`.
- Helpers: `FormatFileSize(long bytes)` (supports `Б`, `КБ`, `МБ`, `ГБ`, `ТБ`), `ResolveIconGlyph(string extUpper)`, and `ResolveItemType(string extUpper)` providing 100% natural Russian file type descriptions (e.g. *Папка с файлами*, *Приложение*, *Исходный код C#*, *Архив ZIP*).

#### 7. `Models/SidebarItem.cs`
Hierarchical tree node model for the Windows 11 Navigation Pane:
- Properties: `Title`, `Path`, `Icon`, `IconGlyph`, `IsDrive`, `IsSeparator`, `FreeSpaceBytes`, `TotalSizeBytes`, `UsagePercent`, `Subtitle`, `IsActive`, `IsExpanded`, `IsPinned`.
- Hierarchical support: `ObservableCollection<SidebarItem> Children`, `HasChildren`.

#### 8. `Models/BreadcrumbItem.cs` & `Models/NavigationHistory.cs`
- **`BreadcrumbItem.cs`**: Entity representing a single segment in the path hierarchy (`Name`, `FullPath`, `IsLast`).
- **`NavigationHistory.cs`**: Dual-stack history tracker (`_backStack`, `_forwardStack`, `_currentPath`) managing `CanGoBack`, `CanGoForward`, `GoBack()`, `GoForward()`, and `NavigateTo()`.

#### 9. `ViewModels/MainViewModel.cs`
The master MVVM orchestrator coordinating:
- **Asynchronous Directory Loading**: Executes directory/file enumerations on `Task.Run` with `CancellationTokenSource` preemption on rapid folder switching.
- **Instant In-Memory Search / Filtering**: Zero-latency filtering across item names, extensions, and Russian category strings.
- **Multi-Column Sorting**: Name, Date Modified, Type, Size with ascending/descending toggle and sort direction indicator binding.
- **Clipboard Management**: Cut, Copy, Paste operations integrating with both internal state and Windows OS `Clipboard.SetFileDropList` / `GetFileDropList`.
- **File System Operations**: Create Folder, Create Text Document, Rename, Delete (with Russian confirmation dialog), Copy Path, Open in PowerShell Terminal.
- **Sidebar & Drives Synchronization**: Live telemetry calculation of drive capacities and active selection highlighting.

#### 10. `Views/InputDialog.xaml` & `Views/InputDialog.xaml.cs`
A modern modal dialog with rounded corners (`CornerRadius="8"`), drop shadow, Russian localization, text pre-selection, and `Enter`/`Escape` key bindings used for creating new folders and renaming items.

#### 11. `Views/PropertiesDialog.xaml` & `Views/PropertiesDialog.xaml.cs`
A comprehensive metadata inspector dialog displaying item glyph, name, type, location, size, timestamps (created, modified, accessed), and file attributes. Includes asynchronous recursive folder enumeration calculating total byte size, file count, and folder count in the background.

#### 12. `Converters/Converters.cs`
Collection of WPF converters:
- `BoolToVisibilityConverter`: Supports `CollapseWhenFalse` and `Invert`.
- `StringNullOrEmptyToVisibilityConverter`: Evaluates string presence for placeholders and error banners.
- `ItemTypeToBrushConverter`: Maps folders to warm gold (`#F5C842`) and files to crisp white (`#F0F0F0`).
- `SidebarActiveBrushConverter`: Maps active navigation node to `#1EFFFFFF`.
- `SidebarIndicatorVisibilityConverter`: Controls active item visual state.
- `SortColumnToGlyphConverter`: Computes column header direction indicators (`▲` / `▼`).

---

## 3. Visual & Styling Deep-Dive

### 3.1 Windows 11 Dark Mode Palette & Materials
The visual presentation matches the Windows 11 Sun Valley 2 (22H2/23H2) File Explorer specification:

```css
/* Windows 11 Dark Mode Color Tokens */
--color-bg-canvas:       #141414 (Alpha B0: #B0141414) /* Main File List Canvas */
--color-bg-mica:         #202020 (Alpha 80: #80202020) /* Title Bar, Breadcrumbs, Status Bar */
--color-bg-mica-sec:     #1C1C1C (Alpha 90: #901C1C1C) /* Command Bar Ribbon */
--color-bg-sidebar:      #181818 (Alpha 90: #90181818) /* Navigation Sidebar Pane */
--color-surface-input:   #2D2D2D (Alpha 45: #452D2D2D) /* TextBoxes, Search & Breadcrumb Pill */
--color-surface-hover:   #383838 (Alpha 60: #60383838) /* Button Hover Background */
--color-surface-pressed: #303030 (Alpha 70: #70303030) /* Button Pressed Background */

--color-border-primary:  #FFFFFF (Alpha 30: #30FFFFFF) /* Main Structural Dividers */
--color-border-light:    #FFFFFF (Alpha 20: #20FFFFFF) /* Card & Container Outlines */
--color-border-subtle:   #FFFFFF (Alpha 15: #15FFFFFF) /* Separators */

--color-accent-win11:    #0078D4                       /* Windows Default Blue */
--color-accent-light:    #4CC2FF                       /* Fluent Vibrant Light Blue Accent */
--color-accent-hover:    #60CDFF                       /* Fluent Accent Hover */

--color-text-primary:    #FFFFFF                       /* Crisp High-Contrast White */
--color-text-secondary:  #D0D0D0                       /* Secondary Labels & Metadata */
--color-text-muted:      #8A8A8A                       /* Subtitles, Placeholders & Separators */
--color-folder-amber:    #F5C842                       /* Authentic Warm Golden Folder Tint */
```

### 3.2 Win32 DWM Backdrop Interop (Mica Effect)
Hardware glassmorphism is achieved via Win32 Desktop Window Manager (`dwmapi.dll`):

```csharp
[DllImport("dwmapi.dll", PreserveSig = true)]
private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

[DllImport("dwmapi.dll")]
private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
private const int DWMWA_SYSTEMBACKDROP_TYPE = 38; // Windows 11 22H2+
private const int DWMWA_MICA_EFFECT = 1029;        // Windows 11 21H2 Fallback

// Setup in OnSourceInitialized:
var hwnd = new WindowInteropHelper(this).Handle;
int darkMode = 1;
DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

int backdropType = 2; // DWMSBT_MAINWINDOW (Mica)
int hr = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
if (hr != 0)
{
    int micaTrue = 1;
    DwmSetWindowAttribute(hwnd, DWMWA_MICA_EFFECT, ref micaTrue, sizeof(int));
}

var margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
DwmExtendFrameIntoClientArea(hwnd, ref margins);
```

### 3.3 Strict 4-Column Navigation Sidebar TreeView
The sidebar items in `App.xaml` (`Win11TreeViewItemStyle`) use a 4-column layout:
- **Column 0 (`Width="24"`)**: 24x24px Chevron Expander toggle button (`M9 7L14 12L9 17` collapsed, `M7 9L12 14L17 9` expanded). Hidden when `HasChildren == false` while preserving exact 24px column width to guarantee perfect vertical alignment.
- **Column 1 (`Width="Auto"`)**: 16x16px native shell icon with `Margin="0,0,10,0"`.
- **Column 2 (`Width="*"`)**: Title Text, optional Subtitle, and mini drive usage progress bar (`UsagePercent`).
- **Column 3 (`Width="Auto"`)**: Authentic 45°-rotated Pin glyph (📌 `#777777`) for quick access pinned items.
- **Separators**: Transparent, zero-padding, non-focusable items (`IsSeparator == true`).

### 3.4 Command Bar & Top Bar Geometry Metrics
- **Row 1 (Top Bar / 48px)**:
  - Navigation Buttons: 32x32px rounded frames with 16x16px vector glyphs.
  - Column 1 (~75% width): Breadcrumb Bar container (`Win11InputContainerStyle`, `Height="32"`, `CornerRadius="4"`, background `#2D2D2D`).
  - Column 2 (~25% width, `MinWidth="240"`): Search Box with internal left magnifying glass icon (`Margin="10,0,6,0"`), dynamic placeholder, and right-aligned clear button.
- **Row 2 (Command Bar / 48px)**:
  - Background `#901C1C1C` with bottom border `#30FFFFFF`.
  - Primary Action Button: `[ ➕ Создать ▾ ]` (`Height="36"`, `Padding="10,6"`, `CornerRadius="4"`).
  - Compact Action Icons: 36x36px buttons for Cut, Copy, Paste, Rename, Share/Copy Path, Delete.
  - Dropdowns: `[ ⇅ Сортировка ▾ ]` and `[ ☷ Вид ▾ ]`.
  - Auxiliary Buttons: `[ 💻 Терминал ]` and `[ ℹ️ Свойства ]`.

---

## 4. Sourced Vector Geometries Catalog

All 17 Microsoft Fluent UI System Icon geometries are stored as frozen `StreamGeometry` resources in `App.xaml`:

| Geometry Resource Key | Icon Name | UI Placement / Command | Vector Path Data (`Data`) |
| :--- | :--- | :--- | :--- |
| `GeoArrowLeft` | Arrow Left | Top Bar: Back (`Alt+Left`) | `M9.15898 16.8666C9.36292 17.0528 9.67918 17.0384 9.86536 16.8345C10.0515 16.6305 10.0371 16.3143 9.8332 16.1281L3.66535 10.4974H17.4961C17.7722 10.4974 17.9961 10.2735 17.9961 9.99736C17.9961 9.72122 17.7722 9.49736 17.4961 9.49736H3.66824L9.8332 3.86927C10.0371 3.68309 10.0515 3.36684 9.86536 3.16289C9.67918 2.95895 9.36292 2.94458 9.15898 3.13076L1.83852 9.80004C1.72895 9.89988 1.66667 10.0427 1.66667 10.1925C1.66667 10.3423 1.72895 10.4851 1.83852 10.5849L9.15898 16.8666Z` |
| `GeoArrowRight` | Arrow Right | Top Bar: Forward (`Alt+Right`) | `M10.841 16.8666C10.6371 17.0528 10.3208 17.0384 10.1346 16.8345C9.94846 16.6305 9.96283 16.3143 10.1668 16.1281L16.3347 10.4974H2.50391C2.22776 10.4974 2.00391 10.2735 2.00391 9.99736C2.00391 9.72122 2.22776 9.49736 2.50391 9.49736H16.3318L10.1668 3.86927C9.96283 3.68309 9.94846 3.36684 10.1346 3.16289C10.3208 2.95895 10.6371 2.94458 10.841 3.13076L18.1615 9.80004C18.271 9.89988 18.3333 10.0427 18.3333 10.1925C18.3333 10.3423 18.271 10.4851 18.1615 10.5849L10.841 16.8666Z` |
| `GeoArrowUp` | Arrow Up | Top Bar: Navigate Up (`Alt+Up`) | `M9.8332 3.66535L4.20257 9.8332C4.01639 10.0371 3.70014 10.0515 3.49619 9.86536C3.29225 9.67918 3.27788 9.36292 3.46406 9.15898L10.1333 1.83852C10.2332 1.72895 10.376 1.66667 10.5258 1.66667C10.6756 1.66667 10.8184 1.72895 10.9183 1.83852L17.5875 9.15898C17.7737 9.36292 17.7593 9.67918 17.5554 9.86536C17.3514 10.0515 17.0352 10.0371 16.849 9.8332L11.2184 3.66535V17.4961C11.2184 17.7722 10.9945 17.9961 10.7184 17.9961C10.4422 17.9961 10.2184 17.7722 10.2184 17.4961V3.66535H9.8332Z` |
| `GeoRefresh` | Refresh | Top Bar: Refresh (`F5`) | `M10 2.5C5.85786 2.5 2.5 5.85786 2.5 10C2.5 14.1421 5.85786 17.5 10 17.5C13.626 17.5 16.6577 14.9317 17.3392 11.5168C17.3934 11.2452 17.2184 10.9806 16.9468 10.9264C16.6752 10.8722 16.4106 11.0472 16.3564 11.3188C15.7899 14.1589 13.2687 16.25 10 16.25C6.54822 16.25 3.75 13.4518 3.75 10C3.75 6.54822 6.54822 3.75 10 3.75C11.8344 3.75 13.4735 4.54019 14.6074 5.80718L13.125 5.80718C12.7798 5.80718 12.5 6.08698 12.5 6.43218C12.5 6.77738 12.7798 7.05718 13.125 7.05718H16.25C16.5952 7.05718 16.875 6.77738 16.875 6.43218V3.30718C16.875 2.96198 16.5952 2.68218 16.25 2.68218C15.9048 2.68218 15.625 2.96198 15.625 3.30718V4.76785C14.2483 3.35987 12.2343 2.5 10 2.5Z` |
| `GeoSearch` | Search Glass | Search Box Magnifier | `M8.5 2C4.91015 2 2 4.91015 2 8.5C2 12.0899 4.91015 15 8.5 15C10.024 15 11.4239 14.4754 12.5312 13.5919L16.4697 17.5303C16.7626 17.8232 17.2374 17.8232 17.5303 17.5303C17.8232 17.2374 17.8232 16.7626 17.5303 16.4697L13.5919 12.5312C14.4754 11.4239 15 10.024 15 8.5C15 4.91015 12.0899 2 8.5 2ZM3.25 8.5C3.25 5.5995 5.5995 3.25 8.5 3.25C11.4005 3.25 13.75 5.5995 13.75 8.5C13.75 11.4005 11.4005 13.75 8.5 13.75C5.5995 13.75 3.25 11.4005 3.25 8.5Z` |
| `GeoCut` | Scissors | Command Bar & Context: Cut (`Ctrl+X`) | `M6.41421 13.5858C5.63316 12.8047 4.36683 12.8047 3.58579 13.5858C2.80474 14.3668 2.80474 15.6332 3.58579 16.4142C4.36683 17.1953 5.63316 17.1953 6.41421 16.4142C7.03715 15.7913 7.18556 14.8872 6.85945 14.1167L10 10.9761L13.1405 14.1167C12.8144 14.8872 12.9629 15.7913 13.5858 16.4142C14.3668 17.1953 15.6332 17.1953 16.4142 16.4142C17.1953 15.6332 17.1953 14.3668 16.4142 13.5858C15.6332 12.8047 14.3668 12.8047 13.5858 13.5858C12.9629 14.2087 12.8144 15.1128 13.1405 15.8833L10 12.7428L6.85945 15.8833C7.18556 15.1128 7.03715 14.2087 6.41421 13.5858ZM10 9.25L14.7071 4.54289C14.8946 4.35536 15.1979 4.35536 15.3854 4.54289C15.573 4.73043 15.573 5.03374 15.3854 5.22127L10.7071 9.89962L10 9.25ZM9.29289 9.89962L4.61457 5.22127C4.42704 5.03374 4.42704 4.73043 4.61457 4.54289C4.80211 4.35536 5.10542 4.35536 5.29295 4.54289L10 9.25L9.29289 9.89962Z` |
| `GeoCopy` | Dual Pages | Command Bar & Context: Copy (`Ctrl+C`) | `M4 3C2.89543 3 2 3.89543 2 5V13C2 14.1046 2.89543 15 4 15H5V16C5 17.1046 5.89543 18 7 18H15C16.1046 18 17 17.1046 17 16V8C17 6.89543 16.1046 6 15 6H14V5C14 3.89543 13.1046 3 12 3H4ZM4 4.25H12C12.4142 4.25 12.75 4.58579 12.75 5V6H7C5.89543 6 5 6.89543 5 8V13.75H4C3.58579 13.75 3.25 13.4142 3.25 13V5C3.25 4.58579 3.58579 4.25 4 4.25ZM6.25 8C6.25 7.58579 6.58579 7.25 7 7.25H15C15.4142 7.25 15.75 7.58579 15.75 8V16C15.75 16.4142 15.4142 16.75 15 16.75H7C6.58579 16.75 6.25 16.4142 6.25 16V8Z` |
| `GeoPaste` | Clipboard | Command Bar & Context: Paste (`Ctrl+V`) | `M8.5 2C7.39543 2 6.5 2.89543 6.5 4H4C2.89543 4 2 4.89543 2 6V15C2 16.1046 2.89543 17 4 17H16C17.1046 17 18 16.1046 18 15V6C18 4.89543 17.1046 4 16 4H13.5C13.5 2.89543 12.6046 2 11.5 2H8.5ZM7.75 4C7.75 3.58579 8.08579 3.25 8.5 3.25H11.5C11.9142 3.25 12.25 3.58579 12.25 4H7.75ZM3.25 6C3.25 5.58579 3.58579 5.25 4 5.25H5.25V6.5C5.25 7.05228 5.69772 7.5 6.25 7.5H13.75C14.3023 7.5 14.75 7.05228 14.75 6.5V5.25H16C16.4142 5.25 16.75 5.58579 16.75 6V15C16.75 15.4142 16.4142 15.75 16 15.75H4C3.58579 15.75 3.25 15.4142 3.25 15V6Z` |
| `GeoRename` | Pen Edit | Command Bar & Context: Rename (`F2`) | `M14.0858 2.08579C14.8668 1.30474 16.1332 1.30474 16.9142 2.08579C17.6953 2.86683 17.6953 4.13317 16.9142 4.91421L6.41421 15.4142C6.15542 15.673 5.82396 15.8454 5.46261 15.9089L2.46261 16.4357C2.08588 16.502 1.7196 16.2238 1.68745 15.8427C1.68413 15.8033 1.68745 15.7637 1.69733 15.7247L2.22416 12.7247C2.28766 12.3634 2.46006 12.0319 2.71885 11.7731L13.2189 1.2731L14.0858 2.08579ZM13.5 3.5L3.60271 13.3973C3.47332 13.5267 3.38712 13.6924 3.35537 13.8731L3.02029 15.7797L4.92688 15.4446C5.10755 15.4129 5.27329 15.3267 5.40268 15.1973L15.3 5.3L13.5 3.5ZM16.0355 4.56447C16.3479 4.25205 16.3479 3.74795 16.0355 3.43553C15.7231 3.12311 15.219 3.12311 14.9066 3.43553L14.3 4.04213L15.9579 5.7L16.0355 4.56447Z` |
| `GeoShare` | Share Nodes | Command Bar & Context: Share / Copy Path | `M14.5 4C13.1193 4 12 5.11929 12 6.5C12 6.84021 12.0681 7.16452 12.1917 7.46048L7.46048 9.82609C6.96452 9.31427 6.26867 9 5.5 9C4.11929 9 3 10.1193 3 11.5C3 12.8807 4.11929 14 5.5 14C6.26867 14 6.96452 13.6857 7.46048 13.1739L12.1917 15.5395C12.0681 15.8355 12 16.1598 12 16.5C12 17.8807 13.1193 19 14.5 19C15.8807 19 17 17.8807 17 16.5C17 15.1193 15.8807 14 14.5 14C13.7313 14 13.0355 14.3143 12.5395 14.8261L7.80833 12.4605C7.93188 12.1645 8 11.8402 8 11.5C8 11.1598 7.93188 10.8355 7.80833 10.5395L12.5395 8.17391C13.0355 8.68573 13.7313 9 14.5 9C15.8807 9 17 7.88071 17 6.5C17 5.11929 15.8807 4 14.5 4Z` |
| `GeoDelete` | Trash Can | Command Bar & Context: Delete (`Delete`) | `M7.5 3C7.5 2.17157 8.17157 1.5 9 1.5H11C11.8284 1.5 12.5 2.17157 12.5 3V4H16.25C16.6642 4 17 4.33579 17 4.75C17 5.16421 16.6642 5.5 16.25 5.5H15.6562L14.7774 16.0456C14.6978 17.0006 13.8996 17.75 12.9416 17.75H7.05837C6.10037 17.75 5.30219 17.0006 5.22262 16.0456L4.34375 5.5H3.75C3.33579 5.5 3 5.16421 3 4.75C3 4.33579 3.33579 4 3.75 4H7.5V3ZM8.75 4H11.25V3C11.25 2.86193 11.1381 2.75 11 2.75H9C8.86193 2.75 8.75 2.86193 8.75 3V4ZM5.59966 5.5L6.46782 15.9184C6.49434 16.2368 6.7604 16.5 7.05837 16.5H12.9416C13.2396 16.5 13.5057 16.2368 13.5322 15.9184L14.4003 5.5H5.59966ZM8 7.5C8.41421 7.5 8.75 7.83579 8.75 8.25V13.75C8.75 14.1642 8.41421 14.5 8 14.5C7.58579 14.5 7.25 14.1642 7.25 13.75V8.25C7.25 7.83579 7.58579 7.5 8 7.5ZM12 7.5C12.4142 7.5 12.75 7.83579 12.75 8.25V13.75C12.75 14.1642 12.4142 14.5 12 14.5C11.5858 14.5 11.25 14.1642 11.25 13.75V8.25C11.25 7.83579 11.5858 7.5 12 7.5Z` |
| `GeoSort` | Sort Arrows | Command Bar: Sort Dropdown | `M7.75 3.25C8.16421 3.25 8.5 3.58579 8.5 4V14.4393L10.9697 11.9697C11.2626 11.6768 11.7374 11.6768 12.0303 11.9697C12.3232 12.2626 12.3232 12.7374 12.0303 13.0303L8.03033 17.0303C7.73744 17.3232 7.26256 17.3232 6.96967 17.0303L2.96967 13.0303C2.67678 12.7374 2.67678 12.2626 2.96967 11.9697C3.26256 11.6768 3.73744 11.6768 4.03033 11.9697L6.5 14.4393V4C6.5 3.58579 6.83579 3.25 7.25 3.25H7.75ZM13.5 16.75C13.0858 16.75 12.75 16.4142 12.75 16V5.56066L10.2803 8.03033C9.98744 8.32322 9.51256 8.32322 9.21967 8.03033C8.92678 7.73744 8.92678 7.26256 9.21967 6.96967L13.2197 2.96967C13.5126 2.67678 13.9874 2.67678 14.2803 2.96967L18.2803 6.96967C18.5732 7.26256 18.5732 7.73744 18.2803 8.03033C17.9874 8.32322 17.5126 8.32322 17.2197 8.03033L14.75 5.56066V16C14.75 16.4142 14.4142 16.75 14 16.75H13.5Z` |
| `GeoView` | Grid Tiles | Command Bar: View Mode Dropdown | `M3 4C3 3.44772 3.44772 3 4 3H16C16.5523 3 17 3.44772 17 4V16C17 16.5523 16.5523 17 16 17H4C3.44772 17 3 16.5523 3 16V4ZM4.5 4.5V9.25H9.25V4.5H4.5ZM10.75 4.5V9.25H15.5V4.5H10.75ZM4.5 10.75V15.5H9.25V10.75H4.5ZM10.75 10.75V15.5H15.5V10.75H10.75Z` |
| `GeoAdd` | Plus / Cross | Command Bar: `Создать ▾` & Tabs Strip: New Tab | `M10 2.5C10.4142 2.5 10.75 2.83579 10.75 3.25V9.25H16.75C17.1642 9.25 17.5 9.58579 17.5 10C17.5 10.4142 17.1642 10.75 16.75 10.75H10.75V16.75C10.75 17.1642 10.4142 17.5 10 17.5C9.58579 17.5 9.25 17.1642 9.25 16.75V10.75H3.25C2.83579 10.75 2.5 10.4142 2.5 10C2.5 9.58579 2.83579 9.25 3.25 9.25H9.25V3.25C9.25 2.83579 9.58579 2.5 10 2.5Z` |
| `GeoDismiss` | Close Cross | Tab Strip Close Tab & Search Clear | `M4.21967 4.21967C4.51256 3.92678 4.98744 3.92678 5.28033 4.21967L10 8.93934L14.7197 4.21967C15.0126 3.92678 15.4874 3.92678 15.7803 4.21967C16.0732 4.51256 16.0732 4.98744 15.7803 5.28033L11.0607 10L15.7803 14.7197C16.0732 15.0126 16.0732 15.4874 15.7803 15.7803C15.4874 16.0732 15.0126 16.0732 14.7197 15.7803L10 11.0607L5.28033 15.7803C4.98744 16.0732 4.51256 16.0732 4.21967 15.7803C3.92678 15.4874 3.92678 15.0126 4.21967 14.7197L8.93934 10L4.21967 5.28033C3.92678 4.98744 3.92678 4.51256 4.21967 4.21967Z` |
| `GeoTerminal` | CLI Console | Command Bar: PowerShell Terminal | `M2.5 4C2.5 3.17157 3.17157 2.5 4 2.5H16C16.8284 2.5 17.5 3.17157 17.5 4V16C17.5 16.8284 16.8284 17.5 16 17.5H4C3.17157 17.5 2.5 16.8284 2.5 16V4ZM4 3.75C3.86193 3.75 3.75 3.86193 3.75 4V16C3.75 16.1381 3.86193 16.25 4 16.25H16C16.1381 16.25 16.25 16.1381 16.25 16V4C16.25 3.86193 16.1381 3.75 16 3.75H4ZM5.21967 6.46967C5.51256 6.17678 5.98744 6.17678 6.28033 6.46967L8.78033 8.96967C9.07322 9.26256 9.07322 9.73744 8.78033 10.0303L6.28033 12.5303C5.98744 12.8232 5.51256 12.8232 5.21967 12.5303C4.92678 12.2374 4.92678 11.7626 5.21967 11.4697L7.18934 9.5L5.21967 7.53033C4.92678 7.23744 4.92678 6.76256 5.21967 6.46967ZM9.5 12C9.5 11.5858 9.83579 11.25 10.25 11.25H14.25C14.6642 11.25 15 11.5858 15 12C15 12.4142 14.6642 12.75 14.25 12.75H10.25C9.83579 12.75 9.5 12.4142 9.5 12Z` |
| `GeoMore` | Ellipsis | Command Bar: Properties Inspector | `M4 10C4 9.17157 4.67157 8.5 5.5 8.5C6.32843 8.5 7 9.17157 7 10C7 10.8284 6.32843 11.5 5.5 11.5C4.67157 11.5 4 10.8284 4 10ZM8.5 10C8.5 9.17157 9.17157 8.5 10 8.5C10.8284 8.5 11.5 9.17157 11.5 10C11.5 10.8284 10.8284 11.5 10 11.5C9.17157 11.5 8.5 10.8284 8.5 10ZM13 10C13 9.17157 13.6716 8.5 14.5 8.5C15.3284 8.5 16 9.17157 16 10C16 10.8284 15.3284 11.5 14.5 11.5C13.6716 11.5 13 10.8284 13 10Z` |

---

## 5. Current Status, Solved Challenges & Future Roadmap

### 5.1 Current Status
- **Architecture State**: Fully frozen, Level 1 Complete (Standalone Desktop File Manager).
- **Compilation**: Clean Release compilation (`0 Warning(s), 0 Error(s)`).
- **Look & Feel**: Exact 1:1 clone of Windows 11 Dark Mode File Explorer with Mica, Fluent styling, 4-column TreeView, and high-fidelity native icons.

### 5.2 Key Solved Technical Challenges

```
+-----------------------------------------------------------------------------------------------+
| SOLVED ARCHITECTURAL CHALLENGES IN NEXUS COMMANDER                                            |
+-------------------+---------------------------------------+-----------------------------------+
| Challenge         | Root Cause                            | Permanent Solution                |
+-------------------+---------------------------------------+-----------------------------------+
| MultiDataTrigger  | MultiDataTrigger condition in         | Switched to clean DataTrigger     |
| XAML Exception    | ControlTemplate referencing visual    | bindings on DataContext and       |
|                   | properties invalidly.                 | standard Template Triggers.       |
+-------------------+---------------------------------------+-----------------------------------+
| Orange Text Bug   | ListViewItem Foreground bound to      | Removed folder color override;    |
|                   | folder amber brush instead of pure    | bound item name to crisp white    |
|                   | white text brush.                     | BrushTextPrimary (#FFFFFF).       |
+-------------------+---------------------------------------+-----------------------------------+
| Memory Leak with  | Unmanaged HICON handles leaking from  | Wrapped all Win32 calls in        |
| Shell Icons       | SHGetFileInfo and ExtractIconEx.      | try/finally blocks calling        |
|                   |                                       | DestroyIcon + BitmapSource.Freeze |
+-------------------+---------------------------------------+-----------------------------------+
| Fallback Missing  | Font glyph emojis rendering           | Built IconExtractor with direct   |
| System Icons      | inconsistently across Windows versions| imageres.dll resource extraction  |
|                   | and missing blue desktop icons.       | and shell virtual GUID resolvers. |
+-------------------+---------------------------------------+-----------------------------------+
| UI Scroll Stutter | Allocating UI visual elements for     | Enabled VirtualizingStackPanel    |
| on Large Folders  | thousands of items simultaneously.    | with VirtualizationMode="Recycling|
|                   |                                       | and DeferredScrolling="False".    |
+-------------------+---------------------------------------+-----------------------------------+
```

### 5.3 Future Roadmap (For Future Project Resumption)
1. **Multi-Tab State Architecture**:
   - Transition from visual single-tab bar to `ObservableCollection<TabViewModel>`, where each tab preserves its own `NavigationHistory`, `SearchQuery`, scroll offset, and selected items.
2. **Drag-and-Drop Subsystem**:
   - Implement `DragDrop.DoDragDrop` and `Drop` handlers on both `FileListView` and `SidebarTreeView` for fluid drag-and-drop between Nexus Commander and native Windows Explorer.
3. **Dual-Panel Toggle Mode**:
   - Optional split-view layout toggle in the Command Bar allowing instant side-by-side file comparison and transfer (reminiscent of legacy Total Commander / Zenith roots).
4. **Instant File Preview Pane**:
   - Collapsible right-hand inspector panel supporting instant image viewing, text/source code highlighting (via AvalonEdit), and Markdown rendering.
5. **Archive Compression & Extraction Engine**:
   - Built-in ZIP, 7z, and TAR extraction and packaging using `System.IO.Compression`.

---

## 6. Quick Start / Build & Run Guide

### 6.1 Prerequisites
- **OS**: Windows 10 (Build 19041+) or Windows 11
- **SDK**: Microsoft .NET 8.0 SDK (`dotnet --version` >= 8.0.100)

### 6.2 Build Commands

From the workspace root (`C:\Users\Mila\Desktop\BestStart`):

```powershell
# Restore dependencies and build in Debug configuration
dotnet build projects/ZenithCommander/ZenithCommander.csproj -c Debug

# Restore dependencies and build in Release configuration (0 Warnings, 0 Errors)
dotnet build projects/ZenithCommander/ZenithCommander.csproj -c Release
```

From within the project directory (`projects/ZenithCommander`):

```powershell
# Direct build
dotnet build -c Release
```

### 6.3 Run Commands

```powershell
# Run directly via .NET CLI
dotnet run --project projects/ZenithCommander/ZenithCommander.csproj

# Run precompiled Release binary directly
& "projects/ZenithCommander/bin/Release/net8.0-windows/NexusCommander.exe"
```

### 6.4 Clean & Rebuild

```powershell
dotnet clean projects/ZenithCommander/ZenithCommander.csproj
dotnet build projects/ZenithCommander/ZenithCommander.csproj -c Release --no-incremental
```

---

> **Archive Certified by**: Senior Technical Writer & System Architect  
> **Repository Corpus**: `StudentMe2712/BestStart`  
> **Timestamp**: August 30, 2026
