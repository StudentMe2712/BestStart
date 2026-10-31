using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using SecondSelf.Core.Services;
using SecondSelf.Core.ViewModels;
using SecondSelf.Data;
using SecondSelf.Data.Migrations;
using SecondSelf.Data.Repositories;
using SecondSelf.UI.Views;

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
    public static WidgetWindow? WidgetWindow { get; private set; }
    public static DashboardWindow? DashboardWindow { get; private set; }

    public static void OpenDashboard()
    {
        DashboardWindow ??= Services.GetRequiredService<DashboardWindow>();
        DashboardWindow.Activate();
        DashboardWindow.AppWindow.Show();
    }

    public static void ToggleWidget()
    {
        if (WidgetWindow != null)
        {
            var vm = Services.GetRequiredService<WidgetViewModel>();
            vm.ToggleExpanded();
            WidgetWindow.Activate();
            WidgetWindow.AppWindow.Show();
        }
    }

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
        services.AddSingleton<IWindowStateService, WindowStateService>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<ISystemMonitorService, SystemMonitorService>();

        // Storage & Database
        services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<IDatabaseRecoveryService, DatabaseRecoveryService>();
        services.AddSingleton<IDatabaseMigrationService, DatabaseMigrationService>();

        // Repositories
        services.AddSingleton<ITaskRepository, TaskRepository>();
        services.AddSingleton<IDailyRitualRepository, DailyRitualRepository>();
        services.AddSingleton<IBigGoalRepository, BigGoalRepository>();
        services.AddSingleton<IWeeklyGoalRepository, WeeklyGoalRepository>();
        services.AddSingleton<IMonthlyRecapRepository, MonthlyRecapRepository>();

        // Video & Day Transition
        services.AddSingleton<IVideoLibraryService, VideoLibraryService>();
        services.AddSingleton<IFisherYatesShuffleService, FisherYatesShuffleService>();
        services.AddSingleton<IDayTransitionService, DayTransitionService>();
        services.AddSingleton<IVideoPlaybackService, VideoPlaybackService>();

        // ViewModels
        services.AddSingleton<WidgetViewModel>();
        services.AddTransient<VideoPlayerViewModel>();
        services.AddSingleton<TodayTabViewModel>();
        services.AddSingleton<GoalsTabViewModel>();
        services.AddSingleton<ProgressTabViewModel>();
        services.AddSingleton<ArchiveTabViewModel>();
        services.AddSingleton<SettingsTabViewModel>();
        services.AddSingleton<MonthlyRecapViewModel>();
        services.AddSingleton<WeeklyGoalModalViewModel>();
        services.AddSingleton<DashboardViewModel>();

        // Windows & Views
        services.AddSingleton<MainWindow>();
        services.AddSingleton<WidgetWindow>();
        services.AddSingleton<DashboardWindow>();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        await _host.StartAsync();

        // Проверка целостности SQLite и самовосстановление из бэкапа при повреждении
        var recoveryService = _host.Services.GetRequiredService<IDatabaseRecoveryService>();
        var recoveryResult = await recoveryService.CheckAndRecoverAsync();
        if (recoveryResult.WasCorrupted && recoveryResult.RestoredSuccessfully)
        {
            System.Diagnostics.Debug.WriteLine($"[DatabaseRecovery] {recoveryResult.Message}");
        }

        // Автоматический накат миграций при старте приложения
        var migrationService = _host.Services.GetRequiredService<IDatabaseMigrationService>();
        await migrationService.MigrateAsync();

        // Сканирование локальной видеотеки и назначение ритуала текущих суток
        var videoLibraryService = _host.Services.GetRequiredService<IVideoLibraryService>();
        await videoLibraryService.ScanLibraryAsync();

        var dayTransitionService = _host.Services.GetRequiredService<IDayTransitionService>();
        await dayTransitionService.GetOrAssignDailyRitualAsync();

        // Инициализация системного трея
        var trayService = _host.Services.GetRequiredService<ISystemTrayService>();
        trayService.Initialize();
        if (recoveryResult.WasCorrupted && recoveryResult.RestoredSuccessfully)
        {
            trayService.ShowNotification("SecondSelf", recoveryResult.Message);
        }
        trayService.ShowDashboardRequested += () =>
        {
            OpenDashboard();
        };
        trayService.ToggleWidgetRequested += () =>
        {
            if (WidgetWindow != null)
            {
                var vm = _host.Services.GetRequiredService<WidgetViewModel>();
                vm.ToggleExpanded();
                WidgetWindow.Activate();
            }
        };
        trayService.QuickAddTaskRequested += () =>
        {
            if (WidgetWindow != null)
            {
                var vm = _host.Services.GetRequiredService<WidgetViewModel>();
                vm.Expand();
                vm.StartAddingTask();
                WidgetWindow.Activate();
            }
        };
        trayService.ExitRequested += () =>
        {
            trayService.Dispose();
            Exit();
        };

        if (!_isStartupBackground)
        {
            WidgetWindow = _host.Services.GetRequiredService<WidgetWindow>();
            WidgetWindow.Activate();
        }

        // Автоматическая оптимизация памяти после инициализации (NFR-02: Working Set <= 65 MB)
        TrimWorkingSet();
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize")]
    private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

    /// <summary>
    /// Очистка неиспользуемых страниц памяти и принудительный тримминг Working Set (NFR-02 / NFR-03).
    /// </summary>
    public static void TrimWorkingSet()
    {
        try
        {
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, (IntPtr)(-1), (IntPtr)(-1));
            }
        }
        catch
        {
            // Подавляем исключения при тримминге страниц
        }
    }
}
