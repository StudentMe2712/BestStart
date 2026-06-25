namespace SelectCast.Core.Conversion;

/// <summary>Recognised kind of a selected value.</summary>
public enum ValueKind
{
    Currency,
    Time,
    Unit,
    Color,
    Unknown,
}

/// <summary>One copyable line of a conversion result (label + the value to copy).</summary>
public sealed record ResultLine(string Label, string Value);

/// <summary>Optional RGB swatch for a color preview.</summary>
public readonly record struct ColorSwatch(byte R, byte G, byte B);

/// <summary>Outcome of converting a selected value.</summary>
public sealed record ConversionResult(
    ValueKind Type,
    string Title,
    IReadOnlyList<ResultLine> Lines,
    ColorSwatch? Swatch = null)
{
    public static ConversionResult Unknown { get; } =
        new(ValueKind.Unknown, "Не распознано", Array.Empty<ResultLine>());
}

/// <summary>
/// A converter for one <see cref="ValueKind"/>: returns a result when it recognises the
/// input, or <c>null</c> so the detector can try the next converter.
/// </summary>
public interface IValueConverter
{
    ValueKind Type { get; }

    ConversionResult? TryConvert(string input);
}
