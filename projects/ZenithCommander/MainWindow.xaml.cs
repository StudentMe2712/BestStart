using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using NexusCommander.Models;
using NexusCommander.ViewModels;

namespace NexusCommander;

public partial class MainWindow : Window
{
    #region Win32 DWM Backdrop Interop

    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMWA_MICA_EFFECT = 1029;

    #endregion

    public MainWindow()
    {
        InitializeComponent();
        StateChanged += MainWindow_StateChanged;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            // 1. Enable Immersive Dark Mode
            int darkMode = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

            // 2. Enable Mica System Backdrop (Windows 11 22H2+ DWMSBT_MAINWINDOW = 2)
            int backdropType = 2; // DWMSBT_MAINWINDOW (Mica)
            int hr = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));

            // Fallback for Windows 11 21H2 (build 22000)
            if (hr != 0)
            {
                int micaTrue = 1;
                DwmSetWindowAttribute(hwnd, DWMWA_MICA_EFFECT, ref micaTrue, sizeof(int));
            }

            // 3. Extend Frame Into Client Area (-1 for full Mica window backdrop)
            var margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            DwmExtendFrameIntoClientArea(hwnd, ref margins);
        }
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            MaximizeIconText.Text = "🗗";
        }
        else
        {
            MaximizeIconText.Text = "🗖";
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
        }
        else if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleMaximize()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            MaximizeIconText.Text = "🗖";
        }
        else
        {
            WindowState = WindowState.Maximized;
            MaximizeIconText.Text = "🗗";
        }
    }

    private void BtnCreate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void BtnSort_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void BtnView_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is GridViewColumnHeader header)
        {
            if (header.Tag is string col && !string.IsNullOrWhiteSpace(col))
            {
                vm.ExecuteSortByColumn(col);
            }
        }
    }

    private void AddressBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.StartEditPath();
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                AddressTextBox.Focus();
                AddressTextBox.SelectAll();
            }));
            e.Handled = true;
        }
    }

    private void AddressTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        if (e.Key == Key.Enter)
        {
            vm.CommitEditPath();
            FileListView.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CancelEditPath();
            FileListView.Focus();
            e.Handled = true;
        }
    }

    private void AddressTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.IsEditingPath)
        {
            vm.CommitEditPath();
        }
    }

    private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.SearchQuery = string.Empty;
        }
    }

    private void FileListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.SelectedItem != null)
        {
            vm.OpenItem(vm.SelectedItem);
            e.Handled = true;
        }
    }

    private void FileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            var selectedList = FileListView.SelectedItems.Cast<FileSystemItem>();
            vm.UpdateSelection(selectedList);
        }
    }

    private void FileListView_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        if (e.Key == Key.Enter)
        {
            if (vm.SelectedItem != null)
            {
                vm.OpenItem(vm.SelectedItem);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Back)
        {
            vm.NavigateUp();
            e.Handled = true;
        }
        else if (e.Key == Key.F2)
        {
            vm.RenameCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            vm.DeleteCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        bool isTextBoxFocused = Keyboard.FocusedElement is TextBox;

        // Address Bar Shortcut: Ctrl+L or Alt+D
        if ((e.Key == Key.L && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) ||
            (e.Key == Key.D && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)))
        {
            vm.StartEditPath();
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                AddressTextBox.Focus();
                AddressTextBox.SelectAll();
            }));
            e.Handled = true;
            return;
        }

        // Search Box Shortcut: Ctrl+F or F3
        if ((e.Key == Key.F && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) ||
            (e.Key == Key.F3 && !isTextBoxFocused))
        {
            SearchTextBox.Focus();
            SearchTextBox.SelectAll();
            e.Handled = true;
            return;
        }

        // Refresh: F5 or Ctrl+R
        if (e.Key == Key.F5 || (e.Key == Key.R && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)))
        {
            _ = vm.RefreshAsync();
            e.Handled = true;
            return;
        }

        // Navigation Shortcuts: Alt+Left (Back), Alt+Right (Forward), Alt+Up (Up)
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            if (e.Key == Key.Left)
            {
                vm.GoBack();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Right)
            {
                vm.GoForward();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Up)
            {
                vm.NavigateUp();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Enter && !isTextBoxFocused)
            {
                vm.PropertiesCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        // Browser Back/Forward Keys
        if (e.Key == Key.BrowserBack)
        {
            vm.GoBack();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.BrowserForward)
        {
            vm.GoForward();
            e.Handled = true;
            return;
        }

        // Ctrl+Shift+N: New Folder
        if (e.Key == Key.N && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            vm.NewFolderCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Ctrl+T: New Tab
        if (e.Key == Key.T && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            vm.NewTabCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Ctrl+W: Close Tab
        if (e.Key == Key.W && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            vm.CloseTabCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // File list shortcuts when not typing in text boxes
        if (!isTextBoxFocused)
        {
            if (e.Key == Key.A && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                FileListView.SelectAll();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.C && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                vm.CopyCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.X && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                vm.CutCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                vm.PasteCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }
    }
}