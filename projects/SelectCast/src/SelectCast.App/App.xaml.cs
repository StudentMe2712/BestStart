using System.Threading;
using System.Windows;
using SelectCast.Core;

namespace SelectCast.App;

public partial class App : Application
{
    private Mutex? _mutex;
    private MainWindow? _window;
    private TrayIcon? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single instance: autostart plus a manual launch must not register the hotkey twice.
        _mutex = new Mutex(initiallyOwned: true, @"Local\SelectCast.SingleInstance", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show("SelectCast уже запущен — значок в системном трее.",
                SelectCastInfo.ProductName, MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Background launcher: the window lives hidden and is summoned by the global hotkey or the
        // tray. ShutdownMode is OnExplicitShutdown, so closing the window only hides it.
        try
        {
            _window = new MainWindow();
            _tray = new TrayIcon(
                onOpen: () => _window!.ShowForManualEntry(),
                onSettings: () => _window!.OpenSettings(),
                onExit: () => _window!.ExitApp());
        }
        catch (Exception ex)
        {
            MessageBox.Show("Ошибка запуска: " + ex.Message, SelectCastInfo.ProductName,
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
