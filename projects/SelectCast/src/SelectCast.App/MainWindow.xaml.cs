using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using SelectCast.App.Interop;
using SelectCast.Core;

namespace SelectCast.App;

public partial class MainWindow : Window
{
    private HotkeyService? _hotkey;

    public MainWindow()
    {
        InitializeComponent();
        Tagline.Text = SelectCastInfo.Tagline;

        Deactivated += (_, _) => Hide();
        PreviewKeyDown += OnPreviewKeyDown;

        // Force the HWND now so the global hotkey can register while the window stays hidden.
        nint hwnd = new WindowInteropHelper(this).EnsureHandle();
        _hotkey = new HotkeyService(hwnd);
        _hotkey.Pressed += ShowLauncher;

        bool ok = _hotkey.Register(
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT,
            NativeMethods.VK_C);

        if (!ok)
        {
            MessageBox.Show(
                "Не удалось зарегистрировать хоткей Ctrl+Alt+C (возможно, он занят другим приложением).",
                SelectCastInfo.ProductName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ShowLauncher()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Focus();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _hotkey?.Dispose();
        base.OnClosed(e);
    }
}
