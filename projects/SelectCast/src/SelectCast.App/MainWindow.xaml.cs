using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using SelectCast.App.Interop;
using SelectCast.Core;
using SelectCast.Core.Capture;

namespace SelectCast.App;

public partial class MainWindow : Window
{
    private readonly SelectionCaptureService _capture = new();
    private HotkeyService? _hotkey;
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();
        StatusLine.Text = SelectCastInfo.Tagline;

        Deactivated += (_, _) => Hide();
        PreviewKeyDown += OnPreviewKeyDown;

        // Force the HWND now so the global hotkey can register while the window stays hidden.
        nint hwnd = new WindowInteropHelper(this).EnsureHandle();
        _hotkey = new HotkeyService(hwnd);
        _hotkey.Pressed += OnHotkeyPressed;

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

    // async void event handler with an internal try/catch is the correct pattern for a UI
    // event (not a fire-and-forget discard): exceptions are observed and surfaced here.
    private async void OnHotkeyPressed()
    {
        if (_busy)
            return;

        _busy = true;
        try
        {
            CaptureResult result = await _capture.CaptureAsync();
            ShowResult(result);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Сбой захвата: " + ex.Message, SelectCastInfo.ProductName,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _busy = false;
        }
    }

    private void ShowResult(CaptureResult r)
    {
        if (r.HasText)
        {
            StatusLine.Text = "Захвачено:";
            InputBox.Text = r.Text;
        }
        else
        {
            StatusLine.Text = r.Status switch
            {
                CaptureStatus.NoSelection => "Нет выделения — введите текст вручную:",
                CaptureStatus.NonText => "Выделение нетекстовое — введите текст вручную:",
                CaptureStatus.Blocked => "Ввод заблокирован — введите текст вручную:",
                _ => "Не удалось захватить — введите текст вручную:",
            };
            InputBox.Text = string.Empty;
        }

        ShowLauncher();
        InputBox.Focus();
        InputBox.SelectAll();
    }

    private void ShowLauncher()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
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
