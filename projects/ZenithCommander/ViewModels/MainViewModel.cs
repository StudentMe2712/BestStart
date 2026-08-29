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
    private string _statusText = "Ready";
    private string _selectedStatusText = "No items selected";
    private string _driveSpaceText = string.Empty;
    private string? _errorMessage;
    private FileSystemItem? _selectedItem;
    private SidebarItem? _selectedSidebarItem;

    private List<FileSystemItem> _allItems = new();
    private List<string> _clipboardFiles = new();
    private bool _clipboardIsCut;

    public MainViewModel()
    {
        Items = new ObservableCollection<FileSystemItem>();
        SelectedItems = new ObservableCollection<FileSystemItem>();
        Breadcrumbs = new ObservableCollection<BreadcrumbItem>();
        QuickAccessItems = new ObservableCollection<SidebarItem>();
        DriveItems = new ObservableCollection<SidebarItem>();

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
            if (item != null && !string.IsNullOrWhiteSpace(item.Path))
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
        CopyCommand = new RelayCommand(ExecuteCopy);
        CutCommand = new RelayCommand(ExecuteCut);
        PasteCommand = new RelayCommand(ExecutePaste, () => CanPaste);
        DeleteCommand = new RelayCommand(ExecuteDelete);
        RenameCommand = new RelayCommand(ExecuteRename);
        CopyPathCommand = new RelayCommand(ExecuteCopyPath);
        PropertiesCommand = new RelayCommand(ExecuteProperties);
        OpenInTerminalCommand = new RelayCommand(ExecuteOpenInTerminal);
        ExitCommand = new RelayCommand(() => Application.Current.Shutdown());

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
                UpdateActiveSidebarHighlight(value);
            }
        }
    }

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
    public ObservableCollection<SidebarItem> QuickAccessItems { get; }
    public ObservableCollection<SidebarItem> DriveItems { get; }

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
    public ICommand CopyCommand { get; }
    public ICommand CutCommand { get; }
    public ICommand PasteCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand PropertiesCommand { get; }
    public ICommand OpenInTerminalCommand { get; }
    public ICommand ExitCommand { get; }

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
            ErrorMessage = $"Cannot navigate up: {ex.Message}";
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
                ErrorMessage = $"Directory does not exist: {targetPath}";
                return;
            }

            var dirInfo = new DirectoryInfo(normalizedPath);

            var (loadedList, folders, files, driveInfoStr) = await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                var results = new List<FileSystemItem>();
                int fCount = 0;
                int fileCount = 0;

                try
                {
                    var dirs = dirInfo.EnumerateDirectories();
                    var sortedDirs = dirs
                        .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(d =>
                        {
                            fCount++;
                            return FileSystemItem.FromDirectoryInfo(d);
                        });

                    results.AddRange(sortedDirs);
                }
                catch (UnauthorizedAccessException) { }
                catch (Exception ex) { Debug.WriteLine($"Dir enum error: {ex.Message}"); }

                token.ThrowIfCancellationRequested();

                try
                {
                    var fileEntries = dirInfo.EnumerateFiles();
                    var sortedFiles = fileEntries
                        .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(f =>
                        {
                            fileCount++;
                            return FileSystemItem.FromFileInfo(f);
                        });

                    results.AddRange(sortedFiles);
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
                            driveCapacity = string.Format(CultureInfo.InvariantCulture, "{0} {1:F1} GB free of {2:F1} GB",
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
            ErrorMessage = $"Access Denied: {uex.Message}";
            StatusText = "Access Denied";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading directory: {ex.Message}";
            StatusText = "Error";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplySearchFilter()
    {
        Items.Clear();

        if (string.IsNullOrWhiteSpace(_searchQuery))
        {
            foreach (var item in _allItems)
            {
                Items.Add(item);
            }

            int folderCount = _allItems.Count(i => i.IsDirectory);
            int fileCount = _allItems.Count(i => !i.IsDirectory);
            StatusText = $"{_allItems.Count} items ({folderCount} folders, {fileCount} files)";
        }
        else
        {
            string query = _searchQuery.Trim();
            var matches = _allItems
                .Where(i => i.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            i.Extension.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            i.ItemType.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var item in matches)
            {
                Items.Add(item);
            }

            StatusText = $"Filtered: showing {matches.Count} of {_allItems.Count} items";
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

    #region Sidebar (Quick Access & Drives)

    private void InitializeSidebar()
    {
        QuickAccessItems.Clear();

        // Pinned standard folders
        AddQuickAccessFolder("Desktop", "🖥️", Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        AddQuickAccessFolder("Downloads", "📥", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
        AddQuickAccessFolder("Documents", "📄", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        AddQuickAccessFolder("Pictures", "🖼️", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
        AddQuickAccessFolder("Music", "🎵", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
        AddQuickAccessFolder("Videos", "🎬", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
        AddQuickAccessFolder("Home", "🏠", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        RefreshDrives();
    }

    private void AddQuickAccessFolder(string title, string icon, string path)
    {
        if (Directory.Exists(path))
        {
            QuickAccessItems.Add(new SidebarItem
            {
                Title = title,
                IconGlyph = icon,
                Path = path,
                IsDrive = false,
                Section = "Quick Access"
            });
        }
    }

    private void RefreshDrives()
    {
        DriveItems.Clear();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    string label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "Local Disk" : drive.VolumeLabel;
                    string driveLetter = drive.Name.TrimEnd('\\');
                    double freeGb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                    double totalGb = drive.TotalSize / (1024.0 * 1024 * 1024);
                    double usagePercent = totalGb > 0 ? ((totalGb - freeGb) / totalGb) * 100.0 : 0;

                    DriveItems.Add(new SidebarItem
                    {
                        Title = $"{label} ({driveLetter})",
                        IconGlyph = "💾",
                        Path = drive.RootDirectory.FullName,
                        IsDrive = true,
                        Section = "Drives",
                        FreeSpaceBytes = drive.AvailableFreeSpace,
                        TotalSizeBytes = drive.TotalSize,
                        UsagePercent = usagePercent,
                        Subtitle = string.Format(CultureInfo.InvariantCulture, "{0:F1} GB free / {1:F1} GB", freeGb, totalGb)
                    });
                }
                else
                {
                    DriveItems.Add(new SidebarItem
                    {
                        Title = $"Drive ({drive.Name.TrimEnd('\\')})",
                        IconGlyph = "💾",
                        Path = drive.Name,
                        IsDrive = true,
                        Section = "Drives",
                        Subtitle = "Device Not Ready"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to enumerate drives: {ex.Message}");
        }
    }

    private void UpdateActiveSidebarHighlight(string currentPath)
    {
        foreach (var item in QuickAccessItems)
        {
            item.IsActive = string.Equals(item.Path, currentPath, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var item in DriveItems)
        {
            item.IsActive = string.Equals(item.Path.TrimEnd('\\'), currentPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
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

    private void UpdateSelectionStatus()
    {
        if (SelectedItems.Count == 0)
        {
            if (SelectedItem != null)
            {
                if (SelectedItem.IsDirectory)
                {
                    SelectedStatusText = $"1 folder selected (\"{SelectedItem.Name}\")";
                }
                else
                {
                    SelectedStatusText = $"1 file selected ({SelectedItem.FormattedSize})";
                }
            }
            else
            {
                SelectedStatusText = "No items selected";
            }
        }
        else if (SelectedItems.Count == 1)
        {
            var single = SelectedItems[0];
            if (single.IsDirectory)
            {
                SelectedStatusText = $"1 folder selected (\"{single.Name}\")";
            }
            else
            {
                SelectedStatusText = $"1 file selected ({single.FormattedSize})";
            }
        }
        else
        {
            long totalBytes = SelectedItems.Where(i => !i.IsDirectory && i.Size.HasValue).Sum(i => i.Size!.Value);
            int dirCount = SelectedItems.Count(i => i.IsDirectory);
            int fileCount = SelectedItems.Count(i => !i.IsDirectory);

            string sizeStr = totalBytes > 0 ? $" ({FileSystemItem.FormatFileSize(totalBytes)})" : "";
            SelectedStatusText = $"{SelectedItems.Count} items selected ({fileCount} files, {dirCount} folders){sizeStr}";
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
            ErrorMessage = $"Failed to open file: {ex.Message}";
        }
    }

    public void ExecuteNewFolder()
    {
        if (!Directory.Exists(CurrentPath)) return;

        var dialog = new InputDialog("New Folder", "Enter folder name:", "New folder")
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
                    MessageBox.Show($"A folder named '{folderName}' already exists.", "Nexus Commander", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Directory.CreateDirectory(newPath);
                _ = RefreshAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to create folder: {ex.Message}";
            }
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

        StatusText = $"Copied {targets.Count} item(s) to clipboard";
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

        StatusText = $"Cut {targets.Count} item(s) to clipboard";
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
                ErrorMessage = $"Paste failed for '{Path.GetFileName(src)}': {ex.Message}";
            }
        }

        if (_clipboardIsCut)
        {
            _clipboardFiles.Clear();
            _clipboardIsCut = false;
        }

        _ = RefreshAsync();
        StatusText = $"Pasted {successCount} item(s) into {CurrentPath}";
    }

    public void ExecuteDelete()
    {
        var targets = GetTargetItems();
        if (targets.Count == 0) return;

        string promptMsg = targets.Count == 1
            ? $"Are you sure you want to permanently delete '{targets[0].Name}'?"
            : $"Are you sure you want to permanently delete {targets.Count} items?";

        var result = MessageBox.Show(
            promptMsg,
            "Nexus Commander — Confirm Delete",
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
                ErrorMessage = $"Delete error on '{item.Name}': {ex.Message}";
            }
        }

        _ = RefreshAsync();
        StatusText = $"Deleted {deleted} item(s)";
    }

    public void ExecuteRename()
    {
        var item = SelectedItem;
        if (item == null) return;

        var dialog = new InputDialog("Rename", $"Enter new name for '{item.Name}':", item.Name)
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
                StatusText = $"Renamed '{item.Name}' to '{newName}'";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Rename failed: {ex.Message}";
            }
        }
    }

    public void ExecuteCopyPath()
    {
        string path = SelectedItem?.FullPath ?? CurrentPath;
        try
        {
            Clipboard.SetText(path);
            StatusText = $"Path copied: {path}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to copy path: {ex.Message}";
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
                ErrorMessage = $"Failed to launch terminal: {ex.Message}";
            }
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
        if (!dir.Exists) throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

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