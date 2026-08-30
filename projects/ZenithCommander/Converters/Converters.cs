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
    private static readonly SolidColorBrush FolderBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0xC8, 0x42)); // Windows 11 Warm Golden Folder
    private static readonly SolidColorBrush FileBrush = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));   // Clean White / Light Gray

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
    // Windows 11 Fluent active item soft highlight: #2D3540
    private static readonly SolidColorBrush ActiveBgBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x4C, 0xC2, 0xFF));
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

public class SortColumnToGlyphConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 3 &&
            values[0] is string currentSortColumn &&
            values[1] is bool sortAscending &&
            values[2] is string headerColumn)
        {
            if (string.Equals(currentSortColumn, headerColumn, StringComparison.OrdinalIgnoreCase))
            {
                return sortAscending ? " ▲" : " ▼";
            }
        }
        return string.Empty;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}