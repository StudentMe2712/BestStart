using System.Globalization;
using Microsoft.UI.Xaml.Data;
using UniversalMediaPlayer.UI.Helpers;

namespace UniversalMediaPlayer.App.Helpers;

public sealed class TimecodeValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double d)
        {
            return FormatHelper.FormatTimecode(d);
        }
        if (value is float f)
        {
            return FormatHelper.FormatTimecode(f);
        }
        if (value is int i)
        {
            return FormatHelper.FormatTimecode(i);
        }
        if (value != null && double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return FormatHelper.FormatTimecode(parsed);
        }
        return "00:00";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
