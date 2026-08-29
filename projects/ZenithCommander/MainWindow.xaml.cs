using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ZenithCommander.ViewModels;

namespace ZenithCommander;

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

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        // Check if focus is inside an editable TextBox
        bool isTextBoxFocused = Keyboard.FocusedElement is TextBox;

        if (e.Key == Key.Tab && (Keyboard.Modifiers == ModifierKeys.None))
        {
            // Switch panel focus
            vm.SwitchActivePanel();
            if (vm.ActivePanel == vm.LeftPanel)
            {
                LeftPanelControl.FocusListView();
            }
            else
            {
                RightPanelControl.FocusListView();
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F5 || (e.Key == Key.R && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)))
        {
            if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.F5)
            {
                // In Total Commander / Midnight Commander, F5 is Copy, but user requested:
                // "Keyboard bindings: F5 / Ctrl+R: refresh active panel" OR Bottom Bar "F5 Copy".
                // Let's support Ctrl+R for refresh and F5 for Copy, or if Ctrl is held then Refresh.
                // We'll execute Refresh on Ctrl+R or F5 when requested.
            }
            if (e.Key == Key.R && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                _ = vm.ActivePanel.RefreshAsync();
                e.Handled = true;
                return;
            }
        }

        if (!isTextBoxFocused)
        {
            switch (e.Key)
            {
                case Key.F3:
                    vm.ViewFileCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.F4:
                    vm.EditFileCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.F5:
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                    {
                        _ = vm.ActivePanel.RefreshAsync();
                    }
                    else
                    {
                        vm.CopyItemCommand.Execute(null);
                    }
                    e.Handled = true;
                    break;
                case Key.F6:
                    vm.MoveItemCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.F7:
                    vm.NewFolderCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.F8:
                case Key.Delete:
                    vm.DeleteItemCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.Back:
                    vm.ActivePanel.NavigateUp();
                    e.Handled = true;
                    break;
                case Key.Left when Keyboard.Modifiers.HasFlag(ModifierKeys.Alt):
                    vm.ActivePanel.NavigateUp();
                    e.Handled = true;
                    break;
            }
        }
    }
}