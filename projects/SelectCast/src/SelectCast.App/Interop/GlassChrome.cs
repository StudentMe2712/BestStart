using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace SelectCast.App.Interop;

/// <summary>
/// Gives a window the Windows 11 acrylic system backdrop + rounded corners, so the translucent
/// "glass" panels in XAML blur the desktop behind them. Making WPF's own backbuffer transparent
/// is what lets the DWM backdrop show through. Unknown DWM attributes are ignored by older
/// Windows, so this degrades gracefully (the translucent panels just stop being blurred).
/// </summary>
internal static class GlassChrome
{
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMSBT_TRANSIENTWINDOW = 4; // acrylic
    private const int DWMWCP_ROUND = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    /// <summary>Enables acrylic on an existing HWND and clears WPF's opaque backbuffer.</summary>
    public static void Apply(nint hwnd)
    {
        HwndSource? source = HwndSource.FromHwnd(hwnd);
        if (source?.CompositionTarget is not null)
            source.CompositionTarget.BackgroundColor = Colors.Transparent;

        int backdrop = DWMSBT_TRANSIENTWINDOW;
        DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));

        int corner = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
    }

    /// <summary>Ensures the window's handle exists, then applies acrylic.</summary>
    public static void Apply(Window window)
        => Apply(new WindowInteropHelper(window).EnsureHandle());
}
