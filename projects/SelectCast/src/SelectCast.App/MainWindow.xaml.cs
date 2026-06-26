using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using SelectCast.App.Interop;
using SelectCast.Core;
using SelectCast.Core.Capture;
using SelectCast.Core.Conversion;
using SelectCast.Core.Detect;
using SelectCast.Core.Rates;
using SelectCast.Core.Settings;

namespace SelectCast.App;

public partial class MainWindow : Window
{
    private readonly SelectionCaptureService _capture = new();
    private readonly RatesService _rates = new();
    private readonly SettingsService _settings = new();
    private readonly TypeDetector _detector;
    private AppSettings _appSettings;
    private HotkeyService? _hotkey;
    private bool _busy;
    private bool _exiting;

    public MainWindow()
    {
        InitializeComponent();
        StatusLine.Text = SelectCastInfo.Tagline;

        _appSettings = _settings.Load();
        _detector = TypeDetector.CreateDefault(_rates);
        RefreshRates(); // background: fetch today's rates if stale; offline falls back to cache

        Deactivated += (_, _) => Hide();
        PreviewKeyDown += OnPreviewKeyDown;

        // Force the HWND now so the global hotkey can register while the window stays hidden.
        nint hwnd = new WindowInteropHelper(this).EnsureHandle();
        _hotkey = new HotkeyService(hwnd);
        _hotkey.Pressed += OnHotkeyPressed;

        if (!ApplyHotkey(_appSettings.HotkeyModifiers, _appSettings.HotkeyVk))
        {
            MessageBox.Show(
                $"Не удалось зарегистрировать хоткей {HotkeyCapture.Format(_appSettings.HotkeyModifiers, _appSettings.HotkeyVk)} "
                + "(возможно, он занят другим приложением). Измените его в настройках.",
                SelectCastInfo.ProductName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>(Re)registers the global hotkey; MOD_NOREPEAT is an implementation detail added here.</summary>
    private bool ApplyHotkey(uint modifiers, uint vk)
    {
        bool ok = _hotkey is not null && _hotkey.Reregister(modifiers | NativeMethods.MOD_NOREPEAT, vk);
        if (ok)
            _appSettings = _appSettings with { HotkeyModifiers = modifiers, HotkeyVk = vk };
        return ok;
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => OpenSettings();

    /// <summary>Opens the settings dialog (also called from the tray menu).</summary>
    public void OpenSettings()
    {
        var window = new SettingsWindow(_appSettings, ApplyHotkey, _settings);
        window.ShowDialog();
        _appSettings = _settings.Load(); // re-sync in case settings changed
    }

    /// <summary>Shows the launcher with an empty input for manual entry (tray "Открыть").</summary>
    public void ShowForManualEntry()
    {
        StatusLine.Text = "Введите текст для конвертации:";
        InputBox.Text = string.Empty;
        ClearResults();
        ShowLauncher();
        InputBox.Focus();
    }

    /// <summary>Real shutdown (tray "Выход"); lets <see cref="OnClosing"/> through.</summary>
    public void ExitApp()
    {
        _exiting = true;
        Application.Current.Shutdown();
    }

    // Fetch today's rates in the background. Offline or a failed fetch is non-fatal: the
    // converter falls back to the cached table (or reports "недоступен"), so nothing is
    // surfaced here. If a currency result is already on screen, re-run it once fresh rates land.
    private async void RefreshRates()
    {
        try
        {
            await _rates.RefreshAsync();
            if (!string.IsNullOrWhiteSpace(InputBox.Text))
                Convert(InputBox.Text);
        }
        catch
        {
            // Best-effort warm-up; offline is an expected state, not an error to report.
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
            ShowCapture(result);
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

    private void ShowCapture(CaptureResult r)
    {
        if (r.HasText)
        {
            InputBox.Text = r.Text;
            Convert(r.Text!);
        }
        else
        {
            InputBox.Text = string.Empty;
            StatusLine.Text = r.Status switch
            {
                CaptureStatus.NoSelection => "Нет выделения — введите текст вручную:",
                CaptureStatus.NonText => "Выделение нетекстовое — введите текст вручную:",
                CaptureStatus.Blocked => "Ввод заблокирован — введите текст вручную:",
                _ => "Не удалось захватить — введите текст вручную:",
            };
            ClearResults();
        }

        ShowLauncher();
        InputBox.Focus();
        InputBox.SelectAll();
    }

    private void Convert(string text)
    {
        ConversionResult res = _detector.Detect(text);
        StatusLine.Text = res.Type == ValueKind.Unknown
            ? "Не распознано — проверьте ввод:"
            : res.Title;

        ResultsList.ItemsSource = res.Lines;

        if (res.Swatch is ColorSwatch sw)
        {
            Swatch.Background = new SolidColorBrush(Color.FromRgb(sw.R, sw.G, sw.B));
            Swatch.Visibility = Visibility.Visible;
        }
        else
        {
            Swatch.Visibility = Visibility.Collapsed;
        }
    }

    private void ClearResults()
    {
        ResultsList.ItemsSource = null;
        Swatch.Visibility = Visibility.Collapsed;
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Convert(InputBox.Text);
            e.Handled = true;
        }
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

    // Closing (X / Alt+F4) hides to the tray instead of exiting: the global hotkey lives on this
    // window's HWND, so the window must survive. Real exit goes through the tray (ExitApp).
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_exiting)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _hotkey?.Dispose();
        base.OnClosed(e);
    }
}
