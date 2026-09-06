using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Stepwise.App.Services;
using Stepwise.App.ViewModels;
using Stepwise.App.Views;
using Stepwise.Core.Interfaces;

namespace Stepwise.App;

/// <summary>
/// Главное окно приложения с поддержкой MicaBackdrop, AppTitleBar и боковой навигацией.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly IGlobalHotkeyService? _hotkeyService;

    public MainViewModel ViewModel { get; }

    public MainWindow() : this(App.Services.GetRequiredService<MainViewModel>())
    {
    }

    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        _hotkeyService = App.Services.GetService<IGlobalHotkeyService>();
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // Регистрация глобальных горячих клавиш (Ctrl+Shift+R и F9)
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (hwnd != nint.Zero && _hotkeyService != null)
        {
            _hotkeyService.Register(hwnd);
            _hotkeyService.RecordingHotkeyPressed += OnRecordingHotkeyPressed;
        }

        Closed += (s, e) =>
        {
            if (_hotkeyService != null)
            {
                _hotkeyService.RecordingHotkeyPressed -= OnRecordingHotkeyPressed;
                _hotkeyService.Unregister();
                _hotkeyService.Dispose();
            }
            (ViewModel as IDisposable)?.Dispose();
        };

        if (NavView.MenuItems.Count > 0)
        {
            NavView.SelectedItem = NavView.MenuItems[0];
        }
        ContentFrame.Navigate(typeof(EditorPage));
    }

    private void OnRecordingHotkeyPressed(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            await ViewModel.ToggleRecordingAsync();
        });
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
            else if (tag == "Player")
            {
                ContentFrame.Navigate(typeof(PlayerPage));
                ViewModel.CurrentView = "Player";
            }
            else if (tag == "Record")
            {
                ViewModel.CurrentView = "Record";
            }
        }
    }

    private void BtnOpenPlayer_Click(object sender, RoutedEventArgs e)
    {
        var playerWindowService = App.Services.GetService<IPlayerWindowService>();
        playerWindowService?.ShowPlayerWindow();
    }
}
