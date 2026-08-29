using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ZenithCommander.Helpers;
using ZenithCommander.Models;

namespace ZenithCommander.ViewModels;

public class FilePanelViewModel : ViewModelBase
{
    private string _currentPath = string.Empty;
    private string _previousValidPath = string.Empty;
    private FileSystemItem? _selectedItem;
    private string? _selectedDrive;
    private bool _isLoading;
    private string _statusText = "Ready";
    private string _panelTitle = "Panel";
    private bool _isActive;
    private string? _errorMessage;

    public FilePanelViewModel(string initialPath, string title)
    {
        _panelTitle = title;
        Items = new ObservableCollection<FileSystemItem>();
        SelectedItems = new ObservableCollection<FileSystemItem>();
        Drives = new ObservableCollection<string>();

        NavigateUpCommand = new RelayCommand(() => NavigateUp(), () => CanNavigateUp);
        RefreshCommand = new RelayCommand(async () => await RefreshAsync());
        ChangeDriveCommand = new RelayCommand<string>(async drive =>
        {
            if (!string.IsNullOrWhiteSpace(drive))
                await ChangeDriveAsync(drive);
        });
        OpenItemCommand = new RelayCommand<FileSystemItem>(item => OpenItem(item));

        LoadDrives();
        _ = LoadDirectoryAsync(initialPath);
    }

    public string CurrentPath
    {
        get => _currentPath;
        set
        {
            if (SetField(ref _currentPath, value))
            {
                OnPropertyChanged(nameof(CanNavigateUp));
            }
        }
    }

    public ObservableCollection<FileSystemItem> Items { get; }

    public ObservableCollection<FileSystemItem> SelectedItems { get; }

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

    public ObservableCollection<string> Drives { get; }

    public string? SelectedDrive
    {
        get => _selectedDrive;
        set => SetField(ref _selectedDrive, value);
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

    public string PanelTitle
    {
        get => _panelTitle;
        set => SetField(ref _panelTitle, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetField(ref _isActive, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetField(ref _errorMessage, value);
    }

    public bool CanNavigateUp
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

    public ICommand NavigateUpCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ChangeDriveCommand { get; }
    public ICommand OpenItemCommand { get; }

    public void LoadDrives()
    {
        Drives.Clear();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady || drive.DriveType == DriveType.Fixed || drive.DriveType == DriveType.Removable)
                {
                    string driveName = drive.Name.TrimEnd('\\');
                    Drives.Add(driveName);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to enumerate drives: {ex.Message}");
            if (Drives.Count == 0)
            {
                Drives.Add("C:");
            }
        }
    }

    public async Task ChangeDriveAsync(string drive)
    {
        string path = drive.EndsWith(":") ? drive + "\\" : drive;
        await LoadDirectoryAsync(path);
    }

    public void NavigateUp()
    {
        if (string.IsNullOrWhiteSpace(CurrentPath)) return;

        try
        {
            var dir = new DirectoryInfo(CurrentPath);
            if (dir.Parent != null)
            {
                _ = LoadDirectoryAsync(dir.Parent.FullName);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Cannot navigate up: {ex.Message}";
        }
    }

    public async Task RefreshAsync()
    {
        await LoadDirectoryAsync(CurrentPath);
    }

    public async Task LoadDirectoryAsync(string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
            return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            string normalizedPath = Path.GetFullPath(targetPath);
            if (!Directory.Exists(normalizedPath))
            {
                ErrorMessage = $"Directory does not exist: {targetPath}";
                if (!string.IsNullOrEmpty(_previousValidPath) && Directory.Exists(_previousValidPath))
                {
                    CurrentPath = _previousValidPath;
                }
                return;
            }

            var dirInfo = new DirectoryInfo(normalizedPath);

            var (loadedItems, folderCount, fileCount, freeSpaceInfo) = await Task.Run(() =>
            {
                var resultList = new List<FileSystemItem>();

                // Add parent directory item if not root
                if (dirInfo.Parent != null)
                {
                    resultList.Add(FileSystemItem.CreateParentDirectory(dirInfo.Parent.FullName));
                }

                int folders = 0;
                int files = 0;

                try
                {
                    var dirInfos = dirInfo.EnumerateDirectories();
                    var sortedDirs = dirInfos
                        .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(d =>
                        {
                            folders++;
                            return FileSystemItem.FromDirectoryInfo(d);
                        });

                    resultList.AddRange(sortedDirs);
                }
                catch (UnauthorizedAccessException)
                {
                    // If directory listing partially fails, keep parent
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Directory enumeration error: {ex.Message}");
                }

                try
                {
                    var fileInfos = dirInfo.EnumerateFiles();
                    var sortedFiles = fileInfos
                        .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(f =>
                        {
                            files++;
                            return FileSystemItem.FromFileInfo(f);
                        });

                    resultList.AddRange(sortedFiles);
                }
                catch (UnauthorizedAccessException)
                {
                    // Access issue on files
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"File enumeration error: {ex.Message}");
                }

                string spaceInfo = string.Empty;
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
                            spaceInfo = string.Format(CultureInfo.InvariantCulture, " | Free: {0:F1} GB / {1:F1} GB", freeGb, totalGb);
                        }
                    }
                }
                catch
                {
                    // Drive info unavailable
                }

                return (resultList, folders, files, spaceInfo);
            });

            // Update UI on main thread
            Items.Clear();
            foreach (var item in loadedItems)
            {
                Items.Add(item);
            }

            CurrentPath = normalizedPath;
            _previousValidPath = normalizedPath;

            // Sync SelectedDrive
            string? rootPath = Path.GetPathRoot(normalizedPath)?.TrimEnd('\\');
            SelectedDrive = rootPath;

            StatusText = string.Format(CultureInfo.InvariantCulture, "{0} items ({1} folders, {2} files){3}",
                folderCount + fileCount, folderCount, fileCount, freeSpaceInfo);

            OnPropertyChanged(nameof(CanNavigateUp));

            // Default selection to first item if available
            if (Items.Count > 0)
            {
                SelectedItem = Items[0];
            }
            else
            {
                SelectedItem = null;
            }
        }
        catch (UnauthorizedAccessException uex)
        {
            ErrorMessage = $"Access Denied: {uex.Message}";
            StatusText = "Access Denied";
            if (!string.IsNullOrEmpty(_previousValidPath) && _previousValidPath != targetPath)
            {
                CurrentPath = _previousValidPath;
            }
        }
        catch (SecurityException sex)
        {
            ErrorMessage = $"Security Exception: {sex.Message}";
            StatusText = "Security Error";
        }
        catch (DirectoryNotFoundException dex)
        {
            ErrorMessage = $"Directory not found: {dex.Message}";
            StatusText = "Not Found";
        }
        catch (IOException ioex)
        {
            ErrorMessage = $"I/O Error: {ioex.Message}";
            StatusText = "I/O Error";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
            StatusText = "Error";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void OpenItem(FileSystemItem? item)
    {
        item ??= SelectedItem;
        if (item == null) return;

        if (item.IsParentDirectory)
        {
            NavigateUp();
            return;
        }

        if (item.IsDirectory)
        {
            _ = LoadDirectoryAsync(item.FullPath);
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

    private void UpdateSelectionStatus()
    {
        if (SelectedItem == null) return;

        if (SelectedItem.IsParentDirectory)
        {
            // Do not override general status text
        }
        else if (SelectedItem.IsDirectory)
        {
            // folder selected
        }
        else if (SelectedItem.Size.HasValue)
        {
            // file selected
        }
    }
}