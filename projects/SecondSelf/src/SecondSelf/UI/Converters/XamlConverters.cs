using Microsoft.UI.Xaml;
using Windows.UI.Text;

namespace SecondSelf.UI.Converters;

/// <summary>
/// Статические методы-конвертеры для выражений {x:Bind} в окнах WinUI 3 Window.
/// Не требуют FrameworkElement converter lookup root.
/// </summary>
public static class XamlConverters
{
    public static Visibility ToVisibility(bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility ToInverseVisibility(bool value) =>
        value ? Visibility.Collapsed : Visibility.Visible;

    public static TextDecorations ToStrikethrough(bool value) =>
        value ? TextDecorations.Strikethrough : TextDecorations.None;
}
