using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Stepwise.App.ViewModels;

namespace Stepwise.App.Views;

/// <summary>
/// Страница 3-панельного визуального редактора шагов руководства.
/// </summary>
public sealed partial class EditorPage : Page
{
    public EditorViewModel ViewModel { get; }

    public EditorPage()
    {
        ViewModel = App.Services.GetRequiredService<EditorViewModel>();
        InitializeComponent();

        Loaded += async (s, e) =>
        {
            if (!ViewModel.HasSteps)
            {
                await ViewModel.InitializeDefaultProjectAsync();
            }
        };
    }
}
