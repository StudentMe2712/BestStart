using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.Text;

namespace SecondSelf.UI.Converters;

/// <summary>
/// Статические методы-конвертеры для выражений {x:Bind} в окнах WinUI 3 Window.
/// Не требуют FrameworkElement converter lookup root.
/// </summary>
public static class XamlConverters
{
    private static readonly SolidColorBrush EmeraldBgBrush = new(Color.FromArgb(0x2E, 0x26, 0xA6, 0x5B));
    private static readonly SolidColorBrush EmeraldFgBrush = new(Color.FromArgb(0xFF, 0x36, 0xD3, 0x99));
    private static readonly SolidColorBrush AmberBgBrush = new(Color.FromArgb(0x2E, 0xF5, 0x9E, 0x0B));
    private static readonly SolidColorBrush AmberFgBrush = new(Color.FromArgb(0xFF, 0xF5, 0x9E, 0x0B));

    public static Visibility ToVisibility(bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility ToInverseVisibility(bool value) =>
        value ? Visibility.Collapsed : Visibility.Visible;

    public static TextDecorations ToStrikethrough(bool value) =>
        value ? TextDecorations.Strikethrough : TextDecorations.None;

    public static SolidColorBrush ToStatusBackgroundBrush(string status) =>
        status == "Paused" ? AmberBgBrush : EmeraldBgBrush;

    public static SolidColorBrush ToStatusForegroundBrush(string status) =>
        status == "Paused" ? AmberFgBrush : EmeraldFgBrush;

    public static string ToPlayPauseGlyph(bool isPlaying) =>
        isPlaying ? "\uE769" : "\uE768";

    public static string ToMuteGlyph(bool isMuted) =>
        isMuted ? "\uE74F" : "\uE767";
}
