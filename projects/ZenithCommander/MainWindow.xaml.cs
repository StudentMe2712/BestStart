using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NexusCommander.Models;
using NexusCommander.ViewModels;

namespace NexusCommander;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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

        // File list shortcuts when not typing in text boxes
        if (!isTextBoxFocused)
        {
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