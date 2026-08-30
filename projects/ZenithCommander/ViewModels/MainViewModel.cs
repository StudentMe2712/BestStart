using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using NexusCommander.Helpers;
using NexusCommander.Models;
using NexusCommander.Views;

namespace NexusCommander.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly NavigationHistory _history = new();
    private CancellationTokenSource? _loadCts;

    private string _currentPath = string.Empty;
    private string _pathInputText = string.Empty;
    private bool _isEditingPath;
    private string _searchQuery = string.Empty;
    private bool _isLoading;
    private string _statusText = "Готово";
    private string _selectedStatusText = "Элементы не выбраны";
    private string _driveSpaceText = string.Empty;
    private string? _errorMessage;
    private FileSystemItem? _selectedItem;
    private SidebarItem? _selectedSidebarItem;

    private string _sortColumn = "Name"; // "Name", "Date", "Type", "Size"
    private bool _sortAscending = true;
    private bool _showHiddenFiles = false;

    private List<FileSystemItem> _allItems = new();
    private List<string> _clipboardFiles = new();
    private bool _clipboardIsCut;

    public MainViewModel()
    {
        Items = new ObservableCollection<FileSystemItem>();
        SelectedItems = new ObservableCollection<FileSystemItem>();
        Breadcrumbs = new ObservableCollection<BreadcrumbItem>();
        SidebarNodes = new ObservableCollection<SidebarItem>();

        // Navigation Commands
        GoBackCommand = new RelayCommand(GoBack, () => CanGoBack);
        GoForwardCommand = new RelayCommand(GoForward, () => CanGoForward);
        NavigateUpCommand = new RelayCommand(NavigateUp, () => CanGoUp);
        RefreshCommand = new RelayCommand(async () => await RefreshAsync());
        NavigateToPathCommand = new RelayCommand<string>(path =>
        {
            if (!string.IsNullOrWhiteSpace(path))
                _ = NavigateToPathAsync(path);
        });
        NavigateBreadcrumbCommand = new RelayCommand<BreadcrumbItem>(crumb =>
        {
            if (crumb != null && !string.IsNullOrWhiteSpace(crumb.FullPath))
                _ = NavigateToPathAsync(crumb.FullPath);
        });
        SelectSidebarItemCommand = new RelayCommand<SidebarItem>(item =>
        {
            if (item != null && !item.IsSeparator && !string.IsNullOrWhiteSpace(item.Path))
            {
                SelectedSidebarItem = item;
                _ = NavigateToPathAsync(item.Path);
            }
        });

        // Address Bar Commands
        StartEditPathCommand = new RelayCommand(StartEditPath);
        CommitEditPathCommand = new RelayCommand(CommitEditPath);
        CancelEditPathCommand = new RelayCommand(CancelEditPath);

        // File Operation Commands
        OpenItemCommand = new RelayCommand<FileSystemItem>(OpenItem);
        NewFolderCommand = new RelayCommand(ExecuteNewFolder);
        NewTextFileCommand = new RelayCommand(ExecuteNewTextFile);
        CopyCommand = new RelayCommand(ExecuteCopy);
        CutCommand = new RelayCommand(ExecuteCut);
        PasteCommand = new RelayCommand(ExecutePaste, () => CanPaste);
        DeleteCommand = new RelayCommand(ExecuteDelete);
        RenameCommand = new RelayCommand(ExecuteRename);
        CopyPathCommand = new RelayCommand(ExecuteCopyPath);
        PropertiesCommand = new RelayCommand(ExecuteProperties);
        OpenInTerminalCommand = new RelayCommand(ExecuteOpenInTerminal);
        SelectAllCommand = new RelayCommand(ExecuteSelectAll);
        ExitCommand = new RelayCommand(() => Application.Current.Shutdown());

        // Sorting Commands
        SortByColumnCommand = new RelayCommand<string>(ExecuteSortByColumn);
        SortByNameCommand = new RelayCommand(() => ExecuteSortByColumn("Name"));
        SortByDateCommand = new RelayCommand(() => ExecuteSortByColumn("Date"));
        SortByTypeCommand = new RelayCommand(() => ExecuteSortByColumn("Type"));
        SortBySizeCommand = new RelayCommand(() => ExecuteSortByColumn("Size"));
        ToggleSortDirectionCommand = new RelayCommand(ToggleSortDirection);
        ToggleShowHiddenFilesCommand = new RelayCommand(ToggleShowHiddenFiles);

        // Tab Commands
        NewTabCommand = new RelayCommand(ExecuteNewTab);
        CloseTabCommand = new RelayCommand(ExecuteCloseTab);

        // Initialize Sidebar & Initial Folder
        InitializeSidebar();

        string initialFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(initialFolder) || !Directory.Exists(initialFolder))
        {
            initialFolder = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        }

        _ = NavigateToPathAsync(initialFolder);
    }

    #region Properties

    public string CurrentPath
    {
        get => _currentPath;
        private set
        {
            if (SetField(ref _currentPath, value))
            {
                PathInputText = value;
                OnPropertyChanged(nameof(CanGoUp));
                OnPropertyChanged(nameof(CurrentFolderName));
                OnPropertyChanged(nameof(SearchPlaceholder));
                UpdateActiveSidebarHighlight(value);
            }
        }
    }

    public string CurrentFolderName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(CurrentPath)) return "Этот компьютер";
            try
            {
                var dir = new DirectoryInfo(CurrentPath);
                if (dir.Parent == null) return dir.FullName.TrimEnd('\\');
                return dir.Name;
            }
            catch
            {
                return "Папка";
            }
        }
    }

    public string SearchPlaceholder => $"Поиск в \"{CurrentFolderName}\"";

    public System.Windows.Media.ImageSource? FolderIcon => IconExtractor.GetFolderIcon(true);

    public string PathInputText
    {
        get => _pathInputText;
        set => SetField(ref _pathInputText, value);
    }

    public bool IsEditingPath
    {
        get => _isEditingPath;
        set => SetField(ref _isEditingPath, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetField(ref _searchQuery, value))
            {
                ApplySearchFilter();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetField(ref _isLoading, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public string SelectedStatusText
    {
        get => _selectedStatusText;
        set => SetField(ref _selectedStatusText, value);
    }

    public string DriveSpaceText
    {
        get => _driveSpaceText;
        set => SetField(ref _driveSpaceText, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetField(ref _errorMessage, value);
    }

    public FileSystemItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetField(ref _selectedItem, value))
            {
                UpdateSelectionStatus();
            }
        }
    }

    public SidebarItem? SelectedSidebarItem
    {
        get => _selectedSidebarItem;
        set => SetField(ref _selectedSidebarItem, value);
    }

    public string SortColumn
    {
        get => _sortColumn;
        set
        {
            if (SetField(ref _sortColumn, value))
            {
                ApplySearchFilter();
            }
        }
    }

    public bool SortAscending
    {
        get => _sortAscending;
        set
        {
            if (SetField(ref _sortAscending, value))
            {
                ApplySearchFilter();
            }
        }
    }

    public bool ShowHiddenFiles
    {
        get => _showHiddenFiles;
        set
        {
            if (SetField(ref _showHiddenFiles, value))
            {
                _ = RefreshAsync();
            }
        }
    }

    public bool CanGoBack => _history.CanGoBack;
    public bool CanGoForward => _history.CanGoForward;

    public bool CanGoUp
    {
        get
        {
            if (string.IsNullOrWhiteSpace(CurrentPath)) return false;
            try
            {
                var dir = new DirectoryInfo(CurrentPath);
                return dir.Parent != null;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool CanPaste
    {
        get
        {
            if (_clipboardFiles.Count > 0) return true;
            try
            {
                return Clipboard.ContainsFileDropList();
            }
            catch
            {
                return false;
            }
        }
    }

    public ObservableCollection<FileSystemItem> Items { get; }
    public ObservableCollection<FileSystemItem> SelectedItems { get; }
    public ObservableCollection<BreadcrumbItem> Breadcrumbs { get; }
    public ObservableCollection<SidebarItem> SidebarNodes { get; }

    #endregion

    #region Commands

    public ICommand GoBackCommand { get; }
    public ICommand GoForwardCommand { get; }
    public ICommand NavigateUpCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand NavigateToPathCommand { get; }
    public ICommand NavigateBreadcrumbCommand { get; }
    public ICommand SelectSidebarItemCommand { get; }

    public ICommand StartEditPathCommand { get; }
    public ICommand CommitEditPathCommand { get; }
    public ICommand CancelEditPathCommand { get; }

    public ICommand OpenItemCommand { get; }
    public ICommand NewFolderCommand { get; }
    public ICommand NewTextFileCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand CutCommand { get; }
    public ICommand PasteCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand PropertiesCommand { get; }
    public ICommand OpenInTerminalCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand ExitCommand { get; }

    public ICommand SortByColumnCommand { get; }
    public ICommand SortByNameCommand { get; }
    public ICommand SortByDateCommand { get; }
    public ICommand SortByTypeCommand { get; }
    public ICommand SortBySizeCommand { get; }
    public ICommand ToggleSortDirectionCommand { get; }
    public ICommand ToggleShowHiddenFilesCommand { get; }

    public ICommand NewTabCommand { get; }
    public ICommand CloseTabCommand { get; }

    #endregion

    #region Navigation & Directory Loading

    public void GoBack()
    {
        string? prev = _history.GoBack();
        if (!string.IsNullOrEmpty(prev))
        {
            _ = NavigateToPathAsync(prev, addToHistory: false);
        }
    }

    public void GoForward()
    {
        string? next = _history.GoForward();
        if (!string.IsNullOrEmpty(next))
        {
            _ = NavigateToPathAsync(next, addToHistory: false);
        }
    }

    public void NavigateUp()
    {
        if (string.IsNullOrWhiteSpace(CurrentPath)) return;
        try
        {
            var dir = new DirectoryInfo(CurrentPath);
            if (dir.Parent != null)
            {
                _ = NavigateToPathAsync(dir.Parent.FullName);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Не удалось перейти на уровень вверх: {ex.Message}";
        }
    }

    public async Task RefreshAsync()
    {
        await NavigateToPathAsync(CurrentPath, addToHistory: false);
        RefreshDrives();
    }

    public async Task NavigateToPathAsync(string targetPath, bool addToHistory = true)
    {
        if (string.IsNullOrWhiteSpace(targetPath)) return;

        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        IsLoading = true;
        ErrorMessage = null;
        IsEditingPath = false;

        try
        {
            string normalizedPath = Path.GetFullPath(targetPath);
            if (!Directory.Exists(normalizedPath))
            {
                ErrorMessage = $"Папка не найдена: {targetPath}";
                return;
            }

            var dirInfo = new DirectoryInfo(normalizedPath);
            bool includeHidden = _showHiddenFiles;

            var (loadedList, folders, files, driveInfoStr) = await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                var results = new List<FileSystemItem>();
                int fCount = 0;
                int fileCount = 0;

                try
                {
                    var dirs = dirInfo.EnumerateDirectories();
                    foreach (var d in dirs)
                    {
                        if (!includeHidden && (d.Attributes & FileAttributes.Hidden) != 0)
                            continue;

                        fCount++;
                        results.Add(FileSystemItem.FromDirectoryInfo(d));
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (Exception ex) { Debug.WriteLine($"Dir enum error: {ex.Message}"); }

                token.ThrowIfCancellationRequested();

                try
                {
                    var fileEntries = dirInfo.EnumerateFiles();
                    foreach (var f in fileEntries)
                    {
                        if (!includeHidden && (f.Attributes & FileAttributes.Hidden) != 0)
                            continue;

                        fileCount++;
                        results.Add(FileSystemItem.FromFileInfo(f));
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (Exception ex) { Debug.WriteLine($"File enum error: {ex.Message}"); }

                token.ThrowIfCancellationRequested();

                string driveCapacity = string.Empty;
                try
                {
                    string? root = Path.GetPathRoot(normalizedPath);
                    if (!string.IsNullOrEmpty(root))
                    {
                        var drive = new DriveInfo(root);
                        if (drive.IsReady)
                        {
                            double freeGb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                            double totalGb = drive.TotalSize / (1024.0 * 1024 * 1024);
                            driveCapacity = string.Format(CultureInfo.InvariantCulture, "{0} {1:F1} ГБ свободно из {2:F1} ГБ",
                                root.TrimEnd('\\'), freeGb, totalGb);
                        }
                    }
                }
                catch { }

                return (results, fCount, fileCount, driveCapacity);
            }, token);

            if (token.IsCancellationRequested) return;

            // Commit state to ViewModel
            _allItems = loadedList;
            CurrentPath = normalizedPath;
            DriveSpaceText = driveInfoStr;

            if (addToHistory)
            {
                _history.NavigateTo(normalizedPath);
            }

            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoForward));
            OnPropertyChanged(nameof(CanGoUp));

            BuildBreadcrumbs(normalizedPath);
            ApplySearchFilter();

            if (Items.Count > 0)
            {
                SelectedItem = Items[0];
            }
            else
            {
                SelectedItem = null;
                UpdateSelectionStatus();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on quick switching
        }
        catch (UnauthorizedAccessException uex)
        {
            ErrorMessage = $"Отказано в доступе к папке: {uex.Message}";
            StatusText = "Отказано в доступе";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ошибка ввода-вывода: {ex.Message}";
            StatusText = "Ошибка ввода-вывода";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void ExecuteSortByColumn(string? column)
    {
        if (string.IsNullOrWhiteSpace(column)) return;

        if (string.Equals(_sortColumn, column, StringComparison.OrdinalIgnoreCase))
        {
            SortAscending = !SortAscending;
        }
        else
        {
            _sortColumn = column;
            SortAscending = true;
            OnPropertyChanged(nameof(SortColumn));
        }

        ApplySearchFilter();
    }

    public void ToggleSortDirection()
    {
        SortAscending = !SortAscending;
    }

    public void ToggleShowHiddenFiles()
    {
        ShowHiddenFiles = !ShowHiddenFiles;
    }

    private void ApplySearchFilter()
    {
        Items.Clear();

        List<FileSystemItem> sourceList;

        if (string.IsNullOrWhiteSpace(_searchQuery))
        {
            sourceList = _allItems;
            int folderCount = _allItems.Count(i => i.IsDirectory);
            int fileCount = _allItems.Count(i => !i.IsDirectory);
            StatusText = $"{_allItems.Count} элементов (папок: {folderCount}, файлов: {fileCount})";
        }
        else
        {
            string query = _searchQuery.Trim();
            sourceList = _allItems
                .Where(i => i.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            i.Extension.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            i.ItemType.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            StatusText = $"Найдено: {sourceList.Count} из {_allItems.Count}";
        }

        // Apply Sorting (folders first, then files sorted by column)
        var dirs = sourceList.Where(i => i.IsDirectory);
        var files = sourceList.Where(i => !i.IsDirectory);

        IEnumerable<FileSystemItem> sortedDirs;
        IEnumerable<FileSystemItem> sortedFiles;

        switch (_sortColumn.ToLowerInvariant())
        {
            case "date":
                sortedDirs = _sortAscending
                    ? dirs.OrderBy(d => d.DateModified ?? DateTime.MinValue)
                    : dirs.OrderByDescending(d => d.DateModified ?? DateTime.MinValue);
                sortedFiles = _sortAscending
                    ? files.OrderBy(f => f.DateModified ?? DateTime.MinValue)
                    : files.OrderByDescending(f => f.DateModified ?? DateTime.MinValue);
                break;

            case "type":
                sortedDirs = _sortAscending
                    ? dirs.OrderBy(d => d.ItemType, StringComparer.OrdinalIgnoreCase)
                    : dirs.OrderByDescending(d => d.ItemType, StringComparer.OrdinalIgnoreCase);
                sortedFiles = _sortAscending
                    ? files.OrderBy(f => f.ItemType, StringComparer.OrdinalIgnoreCase)
                    : files.OrderByDescending(f => f.ItemType, StringComparer.OrdinalIgnoreCase);
                break;

            case "size":
                sortedDirs = dirs;
                sortedFiles = _sortAscending
                    ? files.OrderBy(f => f.Size ?? 0)
                    : files.OrderByDescending(f => f.Size ?? 0);
                break;

            case "name":
            default:
                sortedDirs = _sortAscending
                    ? dirs.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                    : dirs.OrderByDescending(d => d.Name, StringComparer.OrdinalIgnoreCase);
                sortedFiles = _sortAscending
                    ? files.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                    : files.OrderByDescending(f => f.Name, StringComparer.OrdinalIgnoreCase);
                break;
        }

        foreach (var d in sortedDirs)
        {
            Items.Add(d);
        }
        foreach (var f in sortedFiles)
        {
            Items.Add(f);
        }
    }

    private void BuildBreadcrumbs(string path)
    {
        Breadcrumbs.Clear();
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var segments = new List<BreadcrumbItem>();
            var dir = new DirectoryInfo(path);

            var curr = dir;
            while (curr != null)
            {
                string name = curr.Name;
                if (curr.Parent == null) // Drive root
                {
                    name = curr.FullName.TrimEnd('\\');
                }

                segments.Insert(0, new BreadcrumbItem
                {
                    Name = name,
                    FullPath = curr.FullName
                });

                curr = curr.Parent;
            }

            if (segments.Count > 0)
            {
                segments[^1].IsLast = true;
            }

            foreach (var s in segments)
            {
                Breadcrumbs.Add(s);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Breadcrumb build error: {ex.Message}");
        }
    }

    #endregion

    #region Address Bar Edit Mode

    public void StartEditPath()
    {
        PathInputText = CurrentPath;
        IsEditingPath = true;
    }

    public void CommitEditPath()
    {
        IsEditingPath = false;
        if (!string.IsNullOrWhiteSpace(PathInputText) && !string.Equals(PathInputText, CurrentPath, StringComparison.OrdinalIgnoreCase))
        {
            _ = NavigateToPathAsync(PathInputText);
        }
    }

    public void CancelEditPath()
    {
        PathInputText = CurrentPath;
        IsEditingPath = false;
    }

    #endregion

    #region Sidebar (Windows 11 Hierarchical TreeView)

    private void InitializeSidebar()
    {
        SidebarNodes.Clear();

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string downloads = Path.Combine(userProfile, "Downloads");
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        string videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

        // 1. Главная
        SidebarNodes.Add(new SidebarItem
        {
            Title = "Главная",
            Path = userProfile,
            Icon = IconExtractor.GetHomeIcon(true) ?? IconExtractor.GetFolderIcon(true),
            IconGlyph = "🏠",
            IsPinned = false
        });

        // 2. Галерея
        SidebarNodes.Add(new SidebarItem
        {
            Title = "Галерея",
            Path = Directory.Exists(pictures) ? pictures : userProfile,
            Icon = IconExtractor.GetGalleryIcon(true) ?? IconExtractor.GetSpecialFolderIcon(Environment.SpecialFolder.MyPictures) ?? IconExtractor.GetFolderIcon(true),
            IconGlyph = "🖼️",
            IsPinned = false
        });

        // 3. Separator
        SidebarNodes.Add(new SidebarItem { IsSeparator = true });

        // 4. Рабочий стол
        SidebarNodes.Add(new SidebarItem
        {
            Title = "Рабочий стол",
            Path = desktop,
            Icon = IconExtractor.GetSpecialFolderIcon(Environment.SpecialFolder.Desktop) ?? IconExtractor.GetFolderIcon(true),
            IconGlyph = "🖥️",
            IsPinned = true
        });

        // 5. Загрузки
        SidebarNodes.Add(new SidebarItem
        {
            Title = "Загрузки",
            Path = Directory.Exists(downloads) ? downloads : userProfile,
            Icon = IconExtractor.GetCustomFolderIcon(downloads) ?? IconExtractor.GetFolderIcon(true),
            IconGlyph = "📥",
            IsPinned = true
        });

        // 6. Документы
        SidebarNodes.Add(new SidebarItem
        {
            Title = "Документы",
            Path = documents,
            Icon = IconExtractor.GetSpecialFolderIcon(Environment.SpecialFolder.MyDocuments) ?? IconExtractor.GetFolderIcon(true),
            IconGlyph = "📄",
            IsPinned = true
        });

        // 7. Изображения
        SidebarNodes.Add(new SidebarItem
        {
            Title = "Изображения",
            Path = pictures,
            Icon = IconExtractor.GetSpecialFolderIcon(Environment.SpecialFolder.MyPictures) ?? IconExtractor.GetFolderIcon(true),
            IconGlyph = "🌄",
            IsPinned = true
        });

        // 8. Музыка
        SidebarNodes.Add(new SidebarItem
        {
            Title = "Музыка",
            Path = music,
            Icon = IconExtractor.GetSpecialFolderIcon(Environment.SpecialFolder.MyMusic) ?? IconExtractor.GetFolderIcon(true),
            IconGlyph = "🎵",
            IsPinned = true
        });

        // 9. Видео
        SidebarNodes.Add(new SidebarItem
        {
            Title = "Видео",
            Path = videos,
            Icon = IconExtractor.GetSpecialFolderIcon(Environment.SpecialFolder.MyVideos) ?? IconExtractor.GetFolderIcon(true),
            IconGlyph = "🎬",
            IsPinned = true
        });

        // 10. Separator
        SidebarNodes.Add(new SidebarItem { IsSeparator = true });

        // 11. Этот компьютер
        var thisPcNode = new SidebarItem
        {
            Title = "Этот компьютер",
            IsExpanded = true,
            Path = null,
            Icon = IconExtractor.GetThisPcIcon(true) ?? IconExtractor.GetFolderIcon(true),
            IconGlyph = "💻",
            IsPinned = false
        };
        PopulateDrives(thisPcNode);
        SidebarNodes.Add(thisPcNode);

        // 12. Separator
        SidebarNodes.Add(new SidebarItem { IsSeparator = true });

        // 13. Сеть
        SidebarNodes.Add(new SidebarItem
        {
            Title = "Сеть",
            Path = null,
            Icon = IconExtractor.GetNetworkIcon(true) ?? IconExtractor.GetFolderIcon(true),
            IconGlyph = "🌐",
            IsPinned = false
        });
    }

    private void PopulateDrives(SidebarItem thisPcNode)
    {
        thisPcNode.Children.Clear();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                var driveIcon = IconExtractor.GetDriveIcon(drive.Name, true);
                if (drive.IsReady)
                {
                    string label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "Локальный диск" : drive.VolumeLabel;
                    string driveLetter = drive.Name.TrimEnd('\\');
                    double freeGb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                    double totalGb = drive.TotalSize / (1024.0 * 1024 * 1024);
                    double usagePercent = totalGb > 0 ? ((totalGb - freeGb) / totalGb) * 100.0 : 0;

                    thisPcNode.Children.Add(new SidebarItem
                    {
                        Title = $"{label} ({driveLetter})",
                        IconGlyph = "💾",
                        Icon = driveIcon,
                        Path = drive.RootDirectory.FullName,
                        IsDrive = true,
                        FreeSpaceBytes = drive.AvailableFreeSpace,
                        TotalSizeBytes = drive.TotalSize,
                        UsagePercent = usagePercent,
                        Subtitle = string.Format(CultureInfo.InvariantCulture, "{0:F1} ГБ свободно из {1:F1} ГБ", freeGb, totalGb)
                    });
                }
                else
                {
                    thisPcNode.Children.Add(new SidebarItem
                    {
                        Title = $"Диск ({drive.Name.TrimEnd('\\')})",
                        IconGlyph = "💾",
                        Icon = driveIcon,
                        Path = drive.Name,
                        IsDrive = true,
                        Subtitle = "Устройство не готово"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to enumerate drives: {ex.Message}");
        }
    }

    private void RefreshDrives()
    {
        var thisPc = SidebarNodes.FirstOrDefault(n => n.Title == "Этот компьютер");
        if (thisPc != null)
        {
            PopulateDrives(thisPc);
        }
    }

    private void UpdateActiveSidebarHighlight(string currentPath)
    {
        if (string.IsNullOrWhiteSpace(currentPath))
        {
            SetAllInactive(SidebarNodes);
            return;
        }

        string normalized = currentPath.TrimEnd('\\');
        SetNodeHighlightRecursive(SidebarNodes, normalized);
    }

    private bool SetNodeHighlightRecursive(IEnumerable<SidebarItem> nodes, string normalizedCurrentPath)
    {
        bool anyMatched = false;
        foreach (var item in nodes)
        {
            if (item.IsSeparator)
            {
                item.IsActive = false;
                continue;
            }

            bool match = false;
            if (!string.IsNullOrWhiteSpace(item.Path))
            {
                string itemPath = item.Path.TrimEnd('\\');
                match = string.Equals(itemPath, normalizedCurrentPath, StringComparison.OrdinalIgnoreCase);
            }

            item.IsActive = match;
            if (match) anyMatched = true;

            if (item.Children.Count > 0)
            {
                if (SetNodeHighlightRecursive(item.Children, normalizedCurrentPath))
                {
                    anyMatched = true;
                }
            }
        }
        return anyMatched;
    }

    private void SetAllInactive(IEnumerable<SidebarItem> nodes)
    {
        foreach (var item in nodes)
        {
            item.IsActive = false;
            if (item.Children.Count > 0)
            {
                SetAllInactive(item.Children);
            }
        }
    }

    #endregion

    #region Selection & Status Handling

    public void UpdateSelection(IEnumerable<FileSystemItem> selected)
    {
        SelectedItems.Clear();
        foreach (var item in selected)
        {
            SelectedItems.Add(item);
        }
        UpdateSelectionStatus();
    }

    public void ExecuteSelectAll()
    {
        SelectedItems.Clear();
        foreach (var item in Items)
        {
            SelectedItems.Add(item);
        }
        UpdateSelectionStatus();
    }

    private void UpdateSelectionStatus()
    {
        if (SelectedItems.Count == 0)
        {
            if (SelectedItem != null)
            {
                if (SelectedItem.IsDirectory)
                {
                    SelectedStatusText = $"1 папка выбрана (\"{SelectedItem.Name}\")";
                }
                else
                {
                    SelectedStatusText = $"1 файл выбран ({SelectedItem.FormattedSize})";
                }
            }
            else
            {
                SelectedStatusText = "Элементы не выбраны";
            }
        }
        else if (SelectedItems.Count == 1)
        {
            var single = SelectedItems[0];
            if (single.IsDirectory)
            {
                SelectedStatusText = $"1 папка выбрана (\"{single.Name}\")";
            }
            else
            {
                SelectedStatusText = $"1 файл выбран ({single.FormattedSize})";
            }
        }
        else
        {
            long totalBytes = SelectedItems.Where(i => !i.IsDirectory && i.Size.HasValue).Sum(i => i.Size!.Value);
            int dirCount = SelectedItems.Count(i => i.IsDirectory);
            int fileCount = SelectedItems.Count(i => !i.IsDirectory);

            string sizeStr = totalBytes > 0 ? $" ({FileSystemItem.FormatFileSize(totalBytes)})" : "";
            SelectedStatusText = $"Выбрано: {SelectedItems.Count} ({fileCount} файлов, {dirCount} папок{sizeStr})";
        }
    }

    #endregion

    #region File & Folder Operations

    public void OpenItem(FileSystemItem? item)
    {
        item ??= SelectedItem;
        if (item == null) return;

        if (item.IsDirectory)
        {
            _ = NavigateToPathAsync(item.FullPath);
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = item.FullPath,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Не удалось открыть файл: {ex.Message}";
        }
    }

    public void ExecuteNewFolder()
    {
        if (!Directory.Exists(CurrentPath)) return;

        var dialog = new InputDialog("Создание новой папки", "Введите имя новой папки:", "Новая папка")
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true)
        {
            string folderName = dialog.InputText.Trim();
            if (string.IsNullOrWhiteSpace(folderName)) return;

            string newPath = Path.Combine(CurrentPath, folderName);
            try
            {
                if (Directory.Exists(newPath))
                {
                    MessageBox.Show($"Папка с именем '{folderName}' уже существует.", "Nexus Commander", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Directory.CreateDirectory(newPath);
                _ = RefreshAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Не удалось создать папку: {ex.Message}";
            }
        }
    }

    public void ExecuteNewTextFile()
    {
        if (!Directory.Exists(CurrentPath)) return;

        try
        {
            string baseName = "Новый текстовый документ";
            string ext = ".txt";
            string candidate = Path.Combine(CurrentPath, baseName + ext);
            int counter = 2;

            while (File.Exists(candidate))
            {
                candidate = Path.Combine(CurrentPath, $"{baseName} ({counter}){ext}");
                counter++;
            }

            File.WriteAllText(candidate, string.Empty);
            _ = RefreshAsync();
            StatusText = $"Создан файл: {Path.GetFileName(candidate)}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Не удалось создать файл: {ex.Message}";
        }
    }

    public void ExecuteCopy()
    {
        var targets = GetTargetItems();
        if (targets.Count == 0) return;

        _clipboardFiles = targets.Select(t => t.FullPath).ToList();
        _clipboardIsCut = false;

        try
        {
            var sc = new StringCollection();
            sc.AddRange(_clipboardFiles.ToArray());
            Clipboard.SetFileDropList(sc);
        }
        catch { }

        StatusText = $"Скопировано в буфер обмена: {targets.Count} элемент(ов)";
    }

    public void ExecuteCut()
    {
        var targets = GetTargetItems();
        if (targets.Count == 0) return;

        _clipboardFiles = targets.Select(t => t.FullPath).ToList();
        _clipboardIsCut = true;

        try
        {
            var sc = new StringCollection();
            sc.AddRange(_clipboardFiles.ToArray());
            Clipboard.SetFileDropList(sc);
        }
        catch { }

        StatusText = $"Вырезано в буфер обмена: {targets.Count} элемент(ов)";
    }

    public void ExecutePaste()
    {
        if (!Directory.Exists(CurrentPath)) return;

        List<string> sourcePaths = new();
        if (_clipboardFiles.Count > 0)
        {
            sourcePaths.AddRange(_clipboardFiles);
        }
        else
        {
            try
            {
                if (Clipboard.ContainsFileDropList())
                {
                    var fileDropList = Clipboard.GetFileDropList();
                    foreach (string? p in fileDropList)
                    {
                        if (!string.IsNullOrEmpty(p)) sourcePaths.Add(p);
                    }
                }
            }
            catch { }
        }

        if (sourcePaths.Count == 0) return;

        int successCount = 0;
        foreach (var src in sourcePaths)
        {
            try
            {
                if (Directory.Exists(src))
                {
                    string dirName = Path.GetFileName(src);
                    string dest = Path.Combine(CurrentPath, dirName);
                    if (_clipboardIsCut)
                    {
                        Directory.Move(src, dest);
                    }
                    else
                    {
                        CopyDirectoryRecursive(src, dest);
                    }
                    successCount++;
                }
                else if (File.Exists(src))
                {
                    string fileName = Path.GetFileName(src);
                    string dest = Path.Combine(CurrentPath, fileName);
                    if (_clipboardIsCut)
                    {
                        File.Move(src, dest, overwrite: true);
                    }
                    else
                    {
                        File.Copy(src, dest, overwrite: true);
                    }
                    successCount++;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Ошибка вставки для '{Path.GetFileName(src)}': {ex.Message}";
            }
        }

        if (_clipboardIsCut)
        {
            _clipboardFiles.Clear();
            _clipboardIsCut = false;
        }

        _ = RefreshAsync();
        StatusText = $"Вставлено элементов: {successCount} в {CurrentPath}";
    }

    public void ExecuteDelete()
    {
        var targets = GetTargetItems();
        if (targets.Count == 0) return;

        string promptMsg = targets.Count == 1
            ? $"Вы уверены, что хотите удалить \"{targets[0].Name}\"?"
            : $"Вы уверены, что хотите удалить {targets.Count} элементов?";

        var result = MessageBox.Show(
            promptMsg,
            "Подтверждение удаления",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        int deleted = 0;
        foreach (var item in targets)
        {
            try
            {
                if (item.IsDirectory)
                {
                    Directory.Delete(item.FullPath, recursive: true);
                }
                else
                {
                    File.Delete(item.FullPath);
                }
                deleted++;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Ошибка удаления '{item.Name}': {ex.Message}";
            }
        }

        _ = RefreshAsync();
        StatusText = $"Удалено элементов: {deleted}";
    }

    public void ExecuteRename()
    {
        var item = SelectedItem;
        if (item == null) return;

        var dialog = new InputDialog("Переименование", $"Введите новое имя для '{item.Name}':", item.Name)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true)
        {
            string newName = dialog.InputText.Trim();
            if (string.IsNullOrWhiteSpace(newName) || newName == item.Name) return;

            string parentDir = Path.GetDirectoryName(item.FullPath) ?? CurrentPath;
            string newPath = Path.Combine(parentDir, newName);

            try
            {
                if (item.IsDirectory)
                {
                    Directory.Move(item.FullPath, newPath);
                }
                else
                {
                    File.Move(item.FullPath, newPath);
                }
                _ = RefreshAsync();
                StatusText = $"'{item.Name}' переименован в '{newName}'";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Ошибка переименования: {ex.Message}";
            }
        }
    }

    public void ExecuteCopyPath()
    {
        string path = SelectedItem?.FullPath ?? CurrentPath;
        try
        {
            Clipboard.SetText(path);
            StatusText = $"Путь скопирован: {path}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Не удалось скопировать путь: {ex.Message}";
        }
    }

    public void ExecuteProperties()
    {
        FileSystemItem? target = SelectedItem;
        if (target == null && Directory.Exists(CurrentPath))
        {
            target = FileSystemItem.FromDirectoryInfo(new DirectoryInfo(CurrentPath));
        }

        if (target == null) return;

        var propDialog = new PropertiesDialog(target)
        {
            Owner = Application.Current.MainWindow
        };
        propDialog.ShowDialog();
    }

    public void ExecuteOpenInTerminal()
    {
        if (!Directory.Exists(CurrentPath)) return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                WorkingDirectory = CurrentPath,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch
        {
            try
            {
                var psiCmd = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    WorkingDirectory = CurrentPath,
                    UseShellExecute = true
                };
                Process.Start(psiCmd);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Не удалось запустить терминал: {ex.Message}";
            }
        }
    }

    public void ExecuteNewTab()
    {
        // Navigate to home / profile or refresh current
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (Directory.Exists(home))
        {
            _ = NavigateToPathAsync(home);
        }
    }

    public void ExecuteCloseTab()
    {
        if (CanGoUp)
        {
            NavigateUp();
        }
    }

    private List<FileSystemItem> GetTargetItems()
    {
        if (SelectedItems.Count > 0)
        {
            return SelectedItems.ToList();
        }
        if (SelectedItem != null)
        {
            return new List<FileSystemItem> { SelectedItem };
        }
        return new List<FileSystemItem>();
    }

    private static void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists) throw new DirectoryNotFoundException($"Папка не найдена: {sourceDir}");

        Directory.CreateDirectory(destDir);

        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destDir, file.Name);
            file.CopyTo(targetFilePath, overwrite: true);
        }

        foreach (DirectoryInfo subDir in dir.GetDirectories())
        {
            string newDest = Path.Combine(destDir, subDir.Name);
            CopyDirectoryRecursive(subDir.FullName, newDest);
        }
    }

    #endregion
}