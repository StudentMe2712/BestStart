using SelectCast.Core.Conversion;
using SelectCast.Core.Converters;

namespace SelectCast.Core.Detect;

/// <summary>
/// Routes a selected string to the first converter that recognises it, in priority order.
/// Color and units have unambiguous signatures, so they are tried early (e.g. <c>#3A7BD5</c>
/// is a color, not a "number with a hash"). Returns <see cref="ConversionResult.Unknown"/>
/// when nothing matches.
/// </summary>
public sealed class TypeDetector
{
    private readonly IReadOnlyList<IValueConverter> _converters;

    public TypeDetector(IEnumerable<IValueConverter>? converters = null)
        => _converters = converters?.ToList() ?? DefaultConverters();

    /// <summary>Default priority order. Time and Currency are appended in later stages.</summary>
    private static List<IValueConverter> DefaultConverters() => new()
    {
        new ColorConverter(),
        new UnitConverter(),
        // IMPORTANT: append Time/Currency AFTER Unit, so "-32F" routes to Unit and "$5" to Currency.
    };

    public ConversionResult Detect(string? input)
    {
        string s = input?.Trim() ?? string.Empty;
        if (s.Length == 0)
            return ConversionResult.Unknown;

        foreach (IValueConverter converter in _converters)
        {
            ConversionResult? result = converter.TryConvert(s);
            if (result is not null)
                return result;
        }

        return ConversionResult.Unknown;
    }
}
