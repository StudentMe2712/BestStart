using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Stepwise.App.ViewModels;
using Stepwise.App.Views;

namespace Stepwise.App;

/// <summary>
/// Главное окно приложения с поддержкой MicaBackdrop, AppTitleBar и боковой навигацией.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow() : this(App.Services.GetRequiredService<MainViewModel>())
    {
    }

    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        if (NavView.MenuItems.Count > 0)
        {
            NavView.SelectedItem = NavView.MenuItems[0];
        }
        ContentFrame.Navigate(typeof(EditorPage));
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            if (tag == "Editor")
            {
                ContentFrame.Navigate(typeof(EditorPage));
                ViewModel.CurrentView = "Editor";
            }
            else if (tag == "Record")
            {
                ViewModel.CurrentView = "Record";
            }
        }
    }
}
