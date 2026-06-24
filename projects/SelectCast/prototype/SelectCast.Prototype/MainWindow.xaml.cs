using System.Windows;
using System.Windows.Interop;
using SelectCast.Prototype.Capture;
using SelectCast.Prototype.Interop;

namespace SelectCast.Prototype;

public partial class MainWindow : Window
{
    private const int HotkeyId = 0x4343; // arbitrary, unique within this window

    private readonly SelectionCaptureService _capture = new();
    private HwndSource? _source;
    private nint _hwnd;
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);

        bool ok = NativeMethods.RegisterHotKey(
            _hwnd,
            HotkeyId,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT,
            NativeMethods.VK_C);

        HotkeyStatus.Text = ok
            ? "Хоткей зарегистрирован: Ctrl+Alt+C"
            : "НЕ удалось зарегистрировать Ctrl+Alt+C (возможно занят другим приложением)";
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            _ = OnHotkeyAsync();
        }

        return nint.Zero;
    }

    private async Task OnHotkeyAsync()
    {
        if (_busy)
            return;

        _busy = true;
        try
        {
            StatusText.Text = "Захват…";
            CaptureResult result = await _capture.CaptureAsync();
            RenderResult(result);
        }
        finally
        {
            _busy = false;
        }
    }

    private void RenderResult(CaptureResult r)
    {
        StatusText.Text = r.Status switch
        {
            CaptureStatus.Captured => "OK: текст захвачен",
            CaptureStatus.NoSelection => "Fallback: нет выделения",
            CaptureStatus.NonText => "Нетекстовое выделение",
            _ => "Ошибка захвата",
        };

        CapturedText.Text = r.Text ?? string.Empty;
        TimingText.Text = $"Тайминг буфера: {r.ElapsedMs} мс";
        LogBox.Text = r.Diagnostics;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_hwnd != nint.Zero)
            NativeMethods.UnregisterHotKey(_hwnd, HotkeyId);

        _source?.RemoveHook(WndProc);
    }
}
