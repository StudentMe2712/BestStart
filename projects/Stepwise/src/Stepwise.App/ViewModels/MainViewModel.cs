using CommunityToolkit.Mvvm.ComponentModel;

namespace Stepwise.App.ViewModels;

/// <summary>
/// ViewModel уровня приложения / Shell (MainWindow).
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _windowTitle = "Stepwise — Interactive Walkthrough Engine";

    [ObservableProperty]
    private string _statusMessage = "Готов к работе";

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private string _currentView = "Editor";
}
