using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Stepwise.App.ViewModels;
using Stepwise.App.Views;

namespace Stepwise.App.Services;

/// <summary>
/// Реализация IPlayerWindowService для управления автономным окном воспроизведения.
/// Обеспечивает потокобезопасное открытие окна на UI-потоке, инициализацию и освобождение ресурсов при закрытии.
/// </summary>
public sealed class PlayerWindowService : IPlayerWindowService
{
    private readonly IServiceProvider _serviceProvider;
    private PlayerWindow? _playerWindow;

    public PlayerWindowService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public void ShowPlayerWindow(string? projectPath = null)
    {
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        if (dispatcher != null && !dispatcher.HasThreadAccess)
        {
            dispatcher.TryEnqueue(() => ShowPlayerWindow(projectPath));
            return;
        }

        if (_playerWindow == null)
        {
            var viewModel = _serviceProvider.GetRequiredService<PlayerViewModel>();
            _playerWindow = new PlayerWindow(viewModel);
            _playerWindow.Closed += (s, e) =>
            {
                _playerWindow = null;
            };

            _ = viewModel.InitializeAsync(projectPath);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(projectPath))
            {
                _ = _playerWindow.ViewModel.InitializeAsync(projectPath);
            }
        }

        _playerWindow.Activate();
    }

    public void ClosePlayerWindow()
    {
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        if (dispatcher != null && !dispatcher.HasThreadAccess)
        {
            dispatcher.TryEnqueue(() => ClosePlayerWindow());
            return;
        }

        _playerWindow?.Close();
        _playerWindow = null;
    }
}
