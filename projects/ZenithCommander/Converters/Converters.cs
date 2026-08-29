using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ZenithCommander.Converters;

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
    private static readonly SolidColorBrush ParentDirBrush = new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA)); // #60A5FA
    private static readonly SolidColorBrush FileBrush = new SolidColorBrush(Color.FromRgb(0x89, 0xDD, 0xFF)); // #89DDFF

    static ItemTypeToBrushConverter()
    {
        FolderBrush.Freeze();
        ParentDirBrush.Freeze();
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

public class ActiveBorderBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush ActiveBrush = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)); // #3B82F6 Accent
    private static readonly SolidColorBrush InactiveBrush = new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x42)); // #3E3E42 Border

    static ActiveBorderBrushConverter()
    {
        ActiveBrush.Freeze();
        InactiveBrush.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isActive = value is bool b && b;
        return isActive ? ActiveBrush : InactiveBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public class ActivePanelBackgroundConverter : IValueConverter
{
    private static readonly SolidColorBrush ActiveHeaderBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x2A)); // Active Header
    private static readonly SolidColorBrush InactiveHeaderBrush = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x22)); // Inactive Header

    static ActivePanelBackgroundConverter()
    {
        ActiveHeaderBrush.Freeze();
        InactiveHeaderBrush.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isActive = value is bool b && b;
        return isActive ? ActiveHeaderBrush : InactiveHeaderBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}