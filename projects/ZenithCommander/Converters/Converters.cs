using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace NexusCommander.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public bool CollapseWhenFalse { get; set; } = true;
    public bool Invert { get; set; } = false;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool b = value is bool flag && flag;
        if (Invert) b = !b;

        return b ? Visibility.Visible : (CollapseWhenFalse ? Visibility.Collapsed : Visibility.Hidden);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Visibility vis)
        {
            bool b = vis == Visibility.Visible;
            return Invert ? !b : b;
        }
        return false;
    }
}

public class StringNullOrEmptyToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; } = false;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isNullOrEmpty = string.IsNullOrEmpty(value as string);
        bool visible = Invert ? isNullOrEmpty : !isNullOrEmpty;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public class ItemTypeToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush FolderBrush = new SolidColorBrush(Color.FromRgb(0xEC, 0xC4, 0x8D)); // #ECC48D
    private static readonly SolidColorBrush FileBrush = new SolidColorBrush(Color.FromRgb(0xED, 0xED, 0xED));   // #EDEDED

    static ItemTypeToBrushConverter()
    {
        FolderBrush.Freeze();
        FileBrush.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isDirectory)
        {
            return isDirectory ? FolderBrush : FileBrush;
        }
        return FileBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public class SidebarActiveBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush ActiveBgBrush = new SolidColorBrush(Color.FromArgb(0x35, 0x3B, 0x82, 0xF6)); // Semi-transparent accent
    private static readonly SolidColorBrush InactiveBgBrush = new SolidColorBrush(Colors.Transparent);

    static SidebarActiveBrushConverter()
    {
        ActiveBgBrush.Freeze();
        InactiveBgBrush.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isActive = value is bool b && b;
        return isActive ? ActiveBgBrush : InactiveBgBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public class SidebarIndicatorVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isActive = value is bool b && b;
        return isActive ? Visibility.Visible : Visibility.Hidden;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}