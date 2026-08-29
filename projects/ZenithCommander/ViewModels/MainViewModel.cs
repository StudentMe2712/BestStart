using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using ZenithCommander.Helpers;
using ZenithCommander.Models;

namespace ZenithCommander.ViewModels;

public class MainViewModel : ViewModelBase
{
    private FilePanelViewModel _leftPanel;
    private FilePanelViewModel _rightPanel;
    private FilePanelViewModel _activePanel;

    public MainViewModel()
    {
        string systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        string userFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(userFolder) || !Directory.Exists(userFolder))
        {
            userFolder = systemDrive;
        }

        _leftPanel = new FilePanelViewModel(systemDrive, "LEFT PANEL [Source]");
        _rightPanel = new FilePanelViewModel(userFolder, "RIGHT PANEL [Target]");

        _activePanel = _leftPanel;
        _leftPanel.IsActive = true;
        _rightPanel.IsActive = false;

        SwitchPanelCommand = new RelayCommand(SwitchActivePanel);
        RefreshActiveCommand = new RelayCommand(async () => await _activePanel.RefreshAsync());
        NavigateUpActiveCommand = new RelayCommand(() => _activePanel.NavigateUp());
        OpenActiveCommand = new RelayCommand(() => _activePanel.OpenItem(_activePanel.SelectedItem));

        ViewFileCommand = new RelayCommand(ExecuteViewFile);
        EditFileCommand = new RelayCommand(ExecuteEditFile);
        CopyItemCommand = new RelayCommand(ExecuteCopyItem);
        MoveItemCommand = new RelayCommand(ExecuteMoveItem);
        NewFolderCommand = new RelayCommand(ExecuteNewFolder);
        DeleteItemCommand = new RelayCommand(ExecuteDeleteItem);
        ExitCommand = new RelayCommand(() => Application.Current.Shutdown());
    }

    public FilePanelViewModel LeftPanel
    {
        get => _leftPanel;
        set => SetField(ref _leftPanel, value);
    }

    public FilePanelViewModel RightPanel
    {
        get => _rightPanel;
        set => SetField(ref _rightPanel, value);
    }

    public FilePanelViewModel ActivePanel
    {
        get => _activePanel;
        set
        {
            if (SetField(ref _activePanel, value))
            {
                LeftPanel.IsActive = (_activePanel == LeftPanel);
                RightPanel.IsActive = (_activePanel == RightPanel);
            }
        }
    }

    public FilePanelViewModel InactivePanel => ActivePanel == LeftPanel ? RightPanel : LeftPanel;

    public ICommand SwitchPanelCommand { get; }
    public ICommand RefreshActiveCommand { get; }
    public ICommand NavigateUpActiveCommand { get; }
    public ICommand OpenActiveCommand { get; }
    public ICommand ViewFileCommand { get; }
    public ICommand EditFileCommand { get; }
    public ICommand CopyItemCommand { get; }
    public ICommand MoveItemCommand { get; }
    public ICommand NewFolderCommand { get; }
    public ICommand DeleteItemCommand { get; }
    public ICommand ExitCommand { get; }

    public void SetActivePanel(FilePanelViewModel panel)
    {
        if (ActivePanel != panel)
        {
            ActivePanel = panel;
        }
    }

    public void SwitchActivePanel()
    {
        ActivePanel = (ActivePanel == LeftPanel) ? RightPanel : LeftPanel;
    }

    private void ExecuteViewFile()
    {
        var item = ActivePanel.SelectedItem;
        if (item == null || item.IsParentDirectory) return;

        if (item.IsDirectory)
        {
            _ = ActivePanel.LoadDirectoryAsync(item.FullPath);
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
            ActivePanel.ErrorMessage = $"Failed to view file: {ex.Message}";
        }
    }

    private void ExecuteEditFile()
    {
        var item = ActivePanel.SelectedItem;
        if (item == null || item.IsParentDirectory || item.IsDirectory) return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = $"\"{item.FullPath}\"",
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            ActivePanel.ErrorMessage = $"Failed to open in editor: {ex.Message}";
        }
    }

    private void ExecuteCopyItem()
    {
        var item = ActivePanel.SelectedItem;
        if (item == null || item.IsParentDirectory) return;

        string targetDir = InactivePanel.CurrentPath;
        if (!Directory.Exists(targetDir)) return;

        string destPath = Path.Combine(targetDir, item.Name);

        var result = MessageBox.Show(
            $"Copy '{item.Name}' to '{targetDir}'?",
            "Zenith Commander — Copy (F5)",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            if (item.IsDirectory)
            {
                CopyDirectory(item.FullPath, destPath);
            }
            else
            {
                File.Copy(item.FullPath, destPath, overwrite: true);
            }
            _ = InactivePanel.RefreshAsync();
        }
        catch (Exception ex)
        {
            ActivePanel.ErrorMessage = $"Copy failed: {ex.Message}";
        }
    }

    private void ExecuteMoveItem()
    {
        var item = ActivePanel.SelectedItem;
        if (item == null || item.IsParentDirectory) return;

        string targetDir = InactivePanel.CurrentPath;
        if (!Directory.Exists(targetDir)) return;

        string destPath = Path.Combine(targetDir, item.Name);

        var result = MessageBox.Show(
            $"Move '{item.Name}' to '{targetDir}'?",
            "Zenith Commander — Move (F6)",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            if (item.IsDirectory)
            {
                Directory.Move(item.FullPath, destPath);
            }
            else
            {
                File.Move(item.FullPath, destPath, overwrite: true);
            }
            _ = ActivePanel.RefreshAsync();
            _ = InactivePanel.RefreshAsync();
        }
        catch (Exception ex)
        {
            ActivePanel.ErrorMessage = $"Move failed: {ex.Message}";
        }
    }

    private void ExecuteNewFolder()
    {
        string currentDir = ActivePanel.CurrentPath;
        if (!Directory.Exists(currentDir)) return;

        // Generate unique default name
        string defaultName = "New Folder";
        string targetPath = Path.Combine(currentDir, defaultName);
        int counter = 1;
        while (Directory.Exists(targetPath))
        {
            targetPath = Path.Combine(currentDir, $"{defaultName} ({counter++})");
        }

        try
        {
            Directory.CreateDirectory(targetPath);
            _ = ActivePanel.RefreshAsync();
        }
        catch (Exception ex)
        {
            ActivePanel.ErrorMessage = $"Failed to create folder: {ex.Message}";
        }
    }

    private void ExecuteDeleteItem()
    {
        var item = ActivePanel.SelectedItem;
        if (item == null || item.IsParentDirectory) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete '{item.Name}'?",
            "Zenith Commander — Delete (F8)",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

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
            _ = ActivePanel.RefreshAsync();
        }
        catch (Exception ex)
        {
            ActivePanel.ErrorMessage = $"Delete failed: {ex.Message}";
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists) throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

        Directory.CreateDirectory(destinationDir);

        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath, overwrite: true);
        }

        foreach (DirectoryInfo subDir in dir.GetDirectories())
        {
            string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir);
        }
    }
}