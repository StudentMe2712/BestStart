using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ZenithCommander.Models;
using ZenithCommander.ViewModels;

namespace ZenithCommander.Controls;

public partial class FilePanelView : UserControl
{
    public FilePanelView()
    {
        InitializeComponent();
    }

    public void FocusListView()
    {
        FileListView.Focus();
    }

    private void UserControl_GotFocus(object sender, RoutedEventArgs e)
    {
        NotifyActivePanel();
    }

    private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        NotifyActivePanel();
    }

    private void NotifyActivePanel()
    {
        if (DataContext is FilePanelViewModel vm)
        {
            if (Application.Current.MainWindow?.DataContext is MainViewModel mainVm)
            {
                mainVm.SetActivePanel(vm);
            }
            else
            {
                vm.IsActive = true;
            }
        }
    }

    private void FileListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is FilePanelViewModel vm && vm.SelectedItem != null)
        {
            vm.OpenItem(vm.SelectedItem);
            e.Handled = true;
        }
    }

    private void FileListView_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not FilePanelViewModel vm) return;

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
    }

    private void PathTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (DataContext is FilePanelViewModel vm && sender is TextBox tb)
            {
                _ = vm.LoadDirectoryAsync(tb.Text);
                FileListView.Focus();
                e.Handled = true;
            }
        }
    }

    private void FileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is FilePanelViewModel vm)
        {
            vm.SelectedItems.Clear();
            foreach (var item in FileListView.SelectedItems)
            {
                if (item is FileSystemItem fsi)
                {
                    vm.SelectedItems.Add(fsi);
                }
            }
        }
    }
}