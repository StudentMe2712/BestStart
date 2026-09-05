using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Stepwise.App.Services;
using Stepwise.App.ViewModels;
using Stepwise.Core.Engine;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Policy;
using Stepwise.Storage.Repositories;
using Stepwise.WindowsIntegration.Automation;
using Stepwise.WindowsIntegration.Capture;
using Stepwise.WindowsIntegration.Services;

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

    internal static IServiceProvider ConfigureServices(IServiceCollection? services = null)
    {
        services ??= new ServiceCollection();

        // Сервисы загрузки изображений (Разделы 12-13 specs/spec.md)
        services.AddSingleton<IImageLoaderService, ImageLoaderService>();

        // Локальное SQLite хранилище с поддержкой изоляции проекта (Раздел 18.14 specs/spec.md)
        string? projectDir = Environment.GetEnvironmentVariable("STEPWISE_PROJECT_DIR");
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--project", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                projectDir = args[i + 1];
                break;
            }
            if (args[i].StartsWith("--project=", StringComparison.OrdinalIgnoreCase))
            {
                projectDir = args[i].Substring("--project=".Length);
                break;
            }
        }

        var defaultProjectRoot = !string.IsNullOrWhiteSpace(projectDir)
            ? Path.GetFullPath(projectDir)
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Stepwise",
                "DefaultProject"
            );
        services.AddSingleton<IProjectRepository>(sp => new ProjectRepository(defaultProjectRoot));

        // Системные службы Windows Integration
        services.AddSingleton<IInputMonitoringService, InputMonitoringService>();
        services.AddSingleton<IActiveWindowTracker, ActiveWindowTracker>();
        services.AddSingleton<ISystemMetricsProvider, WindowsSystemMetricsProvider>();
        services.AddSingleton<IUIAutomationService, UIAutomationService>();
        services.AddSingleton<ITargetResolver, UIATargetResolver>();
        services.AddSingleton<IScreenCaptureService, ScreenCaptureService>();
        services.AddSingleton<ICaptureCoordinator, CaptureCoordinator>();

        // Ядро записи и политики Core (Stage 2 Recording Engine)
        services.AddSingleton<IEventCorrelator, EventCorrelator>();
        services.AddSingleton<IRecordingPolicy, DefaultRecordingPolicy>();
        services.AddSingleton<IStepDetector, StepDetector>();
        services.AddSingleton<IRecordingEngine, RecordingEngine>();

        // ViewModels
        services.AddSingleton<EditorViewModel>();
        services.AddSingleton<MainViewModel>();

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
