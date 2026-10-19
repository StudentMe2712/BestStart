using System.Windows;
using PersonalProvocateur.App.Services;
using PersonalProvocateur.App.Storage;
using PersonalProvocateur.App.UI;
using PersonalProvocateur.App.WindowsInterop;

namespace PersonalProvocateur.App;

/// <summary>
/// Main application entry point managing services, lifetime, single-instance enforcement, and tray icon.
/// </summary>
public partial class App : System.Windows.Application
{
    private SingleInstanceGuard? _singleInstanceGuard;
    private SettingsStore? _settingsStore;
    private VideoLibraryService? _videoLibraryService;
    private RandomReminderScheduler? _scheduler;
    private OverlayCoordinator? _overlayCoordinator;
    private TrayIconService? _trayIconService;
    private GlobalHotkeyService? _hotkeyService;
    private SystemPowerWatcher? _powerWatcher;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Single Instance Check (ADR-001 / Spec 2.1)
        _singleInstanceGuard = new SingleInstanceGuard();
        if (!_singleInstanceGuard.TryAcquire())
        {
            _singleInstanceGuard.SignalExistingInstance();
            Shutdown();
            return;
        }

        _singleInstanceGuard.StartListening(OpenSettingsWindow);

        // 2. Core Stores and Services
        _settingsStore = new SettingsStore();
        _videoLibraryService = new VideoLibraryService(_settingsStore);
        _scheduler = new RandomReminderScheduler();
        _overlayCoordinator = new OverlayCoordinator(_scheduler, _videoLibraryService, _settingsStore);

        // 3. Scheduler trigger wiring
        _scheduler.TimerTriggered += (s, args) => _overlayCoordinator.TriggerProvocation(isManualTest: false);

        // 4. Tray Icon Service
        _trayIconService = new TrayIconService(_overlayCoordinator, _scheduler, _settingsStore, OpenSettingsWindow);

        // 5. Global Hotkeys Service
        _hotkeyService = new GlobalHotkeyService();
        RegisterHotkeys();
        _settingsStore.SettingsChanged += (s, args) => RegisterHotkeys();

        // 6. System Power & Session Event Watcher
        _powerWatcher = new SystemPowerWatcher(_scheduler, _settingsStore);

        // 7. Initial cycle start
        if (!_settingsStore.CurrentSettings.IsPaused)
        {
            var min = _settingsStore.CurrentSettings.MinIntervalMinutes;
            var max = _settingsStore.CurrentSettings.MaxIntervalMinutes;
            _scheduler.StartNextCycle(min, max);
        }

        // Open settings if requested via command line
        if (e.Args.Contains("--settings", StringComparer.OrdinalIgnoreCase))
        {
            OpenSettingsWindow();
        }
    }

    private void RegisterHotkeys()
    {
        if (_hotkeyService == null || _settingsStore == null || _overlayCoordinator == null)
        {
            return;
        }

        var triggerKey = _settingsStore.CurrentSettings.TriggerHotkey;
        var panicKey = _settingsStore.CurrentSettings.PanicHotkey;

        if (!string.IsNullOrWhiteSpace(triggerKey))
        {
            _hotkeyService.RegisterTriggerHotkey(triggerKey, () => _overlayCoordinator.TriggerProvocation(isManualTest: true));
        }

        if (!string.IsNullOrWhiteSpace(panicKey))
        {
            _hotkeyService.RegisterPanicHotkey(panicKey, () => _overlayCoordinator.EmergencyDismiss());
        }
    }

    private void OpenSettingsWindow()
    {
        if (_settingsStore == null || _videoLibraryService == null || _overlayCoordinator == null)
        {
            return;
        }

        if (_settingsWindow != null && _settingsWindow.IsLoaded)
        {
            if (_settingsWindow.WindowState == WindowState.Minimized)
            {
                _settingsWindow.WindowState = WindowState.Normal;
            }
            _settingsWindow.Activate();
            _settingsWindow.Focus();
            return;
        }

        _settingsWindow = new SettingsWindow(_settingsStore, _videoLibraryService, _overlayCoordinator);
        _settingsWindow.Closed += (s, e) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _trayIconService?.Dispose();
            _hotkeyService?.Dispose();
            _powerWatcher?.Dispose();
            _overlayCoordinator?.Dispose();
            _scheduler?.Dispose();
            _singleInstanceGuard?.Dispose();
        }
        catch
        {
            // Suppress cleanup exceptions during process shutdown
        }

        base.OnExit(e);
    }
}
