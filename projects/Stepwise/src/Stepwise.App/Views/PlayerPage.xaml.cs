using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Stepwise.App.ViewModels;

namespace Stepwise.App.Views;

/// <summary>
/// Страница плеера для встроенной навигации внутри главного окна (MainWindow Shell).
/// </summary>
public sealed partial class PlayerPage : Page
{
    public PlayerViewModel ViewModel { get; }

    public PlayerPage()
    {
        ViewModel = App.Services.GetRequiredService<PlayerViewModel>();
        InitializeComponent();

        PlayerViewControl.ViewModel = ViewModel;

        Loaded += async (s, e) =>
        {
            if (ViewModel.TotalSteps == 0)
            {
                await ViewModel.InitializeAsync();
            }
        };
    }
}
