using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Stepwise.App.ViewModels;

namespace Stepwise.App.Views;

/// <summary>
/// Автономное окно воспроизведения инструкций (Player Window).
/// Поддерживает MicaBackdrop, аппаратные клавиши навигации (Left, Right, Home, End, Space, Escape)
/// и подписку на запрос закрытия окна от ViewModel.
/// </summary>
public sealed partial class PlayerWindow : Window
{
    public PlayerViewModel ViewModel { get; }

    public PlayerWindow() : this(App.Services.GetRequiredService<PlayerViewModel>())
    {
    }

    public PlayerWindow(PlayerViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();

        PlayerViewControl.ViewModel = ViewModel;

        ViewModel.RequestClose += OnRequestClose;
        Closed += OnWindowClosed;

        RootGrid.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnRootKeyDown), true);

        RootGrid.Loaded += (s, e) =>
        {
            RootGrid.Focus(FocusState.Programmatic);
        };
    }

    private void OnRequestClose()
    {
        Close();
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        ViewModel.RequestClose -= OnRequestClose;
    }

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Left:
                if (ViewModel.CanPrevious)
                {
                    ViewModel.PreviousCommand.Execute(null);
                    e.Handled = true;
                }
                break;

            case Windows.System.VirtualKey.Right:
                if (ViewModel.CanNext)
                {
                    ViewModel.NextCommand.Execute(null);
                    e.Handled = true;
                }
                break;

            case Windows.System.VirtualKey.Home:
                if (ViewModel.CanFirst)
                {
                    ViewModel.FirstCommand.Execute(null);
                    e.Handled = true;
                }
                break;

            case Windows.System.VirtualKey.End:
                if (ViewModel.CanLast)
                {
                    ViewModel.LastCommand.Execute(null);
                    e.Handled = true;
                }
                break;

            case Windows.System.VirtualKey.Space:
                if (ViewModel.CanPlay || ViewModel.CanPause)
                {
                    ViewModel.PlayPauseCommand.Execute(null);
                    e.Handled = true;
                }
                break;

            case Windows.System.VirtualKey.Escape:
                Close();
                e.Handled = true;
                break;
        }
    }
}
