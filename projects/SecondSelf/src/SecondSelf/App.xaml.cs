using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using SecondSelf.Core.Services;
using SecondSelf.Data;
using SecondSelf.Data.Migrations;
using SecondSelf.Data.Repositories;

namespace SecondSelf;

/// <summary>
/// Главный класс приложения WinUI 3 c настройкой Dependency Injection, IHost и жизненного цикла.
/// </summary>
public partial class App : Application
{
    private readonly IHost _host;
    private readonly bool _isStartupBackground;

    public static IServiceProvider Services => ((App)Current)._host.Services;
    public static MainWindow? MainWindow { get; private set; }

    public App(bool isStartupBackground = false)
    {
        _isStartupBackground = isStartupBackground;
        this.InitializeComponent();

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                ConfigureAppServices(services);
            })
            .Build();
    }

    private static void ConfigureAppServices(IServiceCollection services)
    {
        // Core & Event Bus
        services.AddSingleton<INotificationBus, NotificationBus>();
        services.AddSingleton<IProgressCalculationEngine, ProgressCalculationEngine>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<ISystemTrayService, SystemTrayService>();
        services.AddSingleton<IAutostartService, AutostartService>();

        // Storage & Database
        services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<IDatabaseMigrationService, DatabaseMigrationService>();

        // Repositories
        services.AddSingleton<ITaskRepository, TaskRepository>();
        services.AddSingleton<IDailyRitualRepository, DailyRitualRepository>();

        // Windows
        services.AddSingleton<MainWindow>();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        await _host.StartAsync();

        // Автоматический накат миграций при старте приложения
        var migrationService = _host.Services.GetRequiredService<IDatabaseMigrationService>();
        await migrationService.MigrateAsync();

        // Инициализация системного трея
        var trayService = _host.Services.GetRequiredService<ISystemTrayService>();
        trayService.Initialize();
        trayService.ShowDashboardRequested += () =>
        {
            MainWindow ??= _host.Services.GetRequiredService<MainWindow>();
            MainWindow.Activate();
        };
        trayService.ExitRequested += () =>
        {
            trayService.Dispose();
            Exit();
        };

        if (!_isStartupBackground)
        {
            MainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow.Activate();
        }
    }
}
