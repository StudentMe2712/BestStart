using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Stepwise.App.ViewModels;

namespace Stepwise.App.Views;

/// <summary>
/// Представление интерактивного плеера пошаговых инструкций (Player View).
/// Реализует компоновку строго в соответствии со спецификацией:
/// Верхняя панель заголовка со статусом, центральный просмотрщик скриншота с оверлеем и нижняя панель навигации.
/// </summary>
public sealed partial class PlayerView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(PlayerViewModel),
            typeof(PlayerView),
            new PropertyMetadata(null, (d, e) => ((PlayerView)d).Bindings.Update()));

    public PlayerViewModel? ViewModel
    {
        get => (PlayerViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public PlayerView()
    {
        InitializeComponent();

        DataContextChanged += (s, e) =>
        {
            if (DataContext is PlayerViewModel vm)
            {
                ViewModel = vm;
            }
        };

        Loaded += (s, e) =>
        {
            if (ViewModel == null)
            {
                if (DataContext is PlayerViewModel vm)
                {
                    ViewModel = vm;
                }
                else if (App.Services != null)
                {
                    ViewModel = App.Services.GetService<PlayerViewModel>();
                }
            }
        };
    }
}
