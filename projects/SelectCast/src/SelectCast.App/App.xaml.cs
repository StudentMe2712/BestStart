using System.Windows;
using SelectCast.Core;

namespace SelectCast.App;

public partial class App : Application
{
    private MainWindow? _window;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Background launcher: the window lives hidden and is summoned by the global hotkey.
        // Closing it (X / Alt+F4) exits the app; a tray-based quit arrives in Stage 6.
        try
        {
            _window = new MainWindow();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Ошибка запуска: " + ex.Message, SelectCastInfo.ProductName,
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
