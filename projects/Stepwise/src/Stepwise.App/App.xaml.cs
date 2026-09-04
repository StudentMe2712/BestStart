using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Stepwise.App.Services;
using Stepwise.App.ViewModels;
using Stepwise.Core.Interfaces;
using Stepwise.Storage.Repositories;

namespace Stepwise.App;

/// <summary>
/// Главный класс приложения WinUI 3 c настройкой Dependency Injection и жизненного цикла.
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static MainWindow? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Сервисы загрузки изображений (Разделы 12-13 specs/spec.md)
        services.AddSingleton<IImageLoaderService, ImageLoaderService>();

        // Локальное SQLite хранилище
        var defaultProjectRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Stepwise",
            "DefaultProject"
        );
        services.AddSingleton<IProjectRepository>(sp => new ProjectRepository(defaultProjectRoot));

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<EditorViewModel>();

        // Views
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = Services.GetRequiredService<MainWindow>();
        MainWindow.Activate();
    }
}
