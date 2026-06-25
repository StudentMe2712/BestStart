using System.Globalization;
using System.Text.RegularExpressions;
using SelectCast.Core.Conversion;

namespace SelectCast.Core.Converters;

/// <summary>
/// Length / weight / temperature converter. Imperial input → metric and vice versa.
/// Handles compound feet+inches (<c>5 ft 9</c>), comma or dot decimals, and RU/EN unit tokens.
/// </summary>
public sealed partial class UnitConverter : IValueConverter
{
    public ValueKind Type => ValueKind.Unit;

    // Length factors to metres; weight factors to kilograms.
    private static readonly Dictionary<string, double> LengthToMetre = new()
    {
        ["mm"] = 0.001,
        ["cm"] = 0.01,
        ["m"] = 1.0,
        ["km"] = 1000.0,
        ["in"] = 0.0254,
        ["ft"] = 0.3048,
        ["yd"] = 0.9144,
        ["mi"] = 1609.344,
    };

    private static readonly Dictionary<string, double> WeightToKg = new()
    {
        ["g"] = 0.001,
        ["kg"] = 1.0,
        ["oz"] = 0.028349523125,
        ["lb"] = 0.45359237,
    };

    // Token (lower-case, °/spaces stripped) → canonical unit.
    private static readonly Dictionary<string, string> Tokens = new()
    {
        ["mm"] = "mm",
        ["millimeter"] = "mm",
        ["millimeters"] = "mm",
        ["мм"] = "mm",
        ["cm"] = "cm",
        ["centimeter"] = "cm",
        ["centimeters"] = "cm",
        ["см"] = "cm",
        ["m"] = "m",
        ["meter"] = "m",
        ["meters"] = "m",
        ["metre"] = "m",
        ["м"] = "m",
        ["km"] = "km",
        ["kilometer"] = "km",
        ["kilometers"] = "km",
        ["км"] = "km",
        ["in"] = "in",
        ["inch"] = "in",
        ["inches"] = "in",
        ["\""] = "in",
        ["″"] = "in",
        ["ft"] = "ft",
        ["foot"] = "ft",
        ["feet"] = "ft",
        ["'"] = "ft",
        ["′"] = "ft",
        ["yd"] = "yd",
        ["yard"] = "yd",
        ["yards"] = "yd",
        ["mi"] = "mi",
        ["mile"] = "mi",
        ["miles"] = "mi",
        ["g"] = "g",
        ["gram"] = "g",
        ["grams"] = "g",
        ["г"] = "g",
        ["kg"] = "kg",
        ["kilogram"] = "kg",
        ["kilograms"] = "kg",
        ["кг"] = "kg",
        ["oz"] = "oz",
        ["ounce"] = "oz",
        ["ounces"] = "oz",
        ["lb"] = "lb",
        ["lbs"] = "lb",
        ["pound"] = "lb",
        ["pounds"] = "lb",
        ["фунт"] = "lb",
    };

    public ConversionResult? TryConvert(string input)
    {
        string s = input.Trim().Replace("°", "").ToLowerInvariant();

        return TryTemperature(s)
            ?? TryCompoundFeetInches(s)
            ?? TrySingle(s);
    }

    private static ConversionResult? TryTemperature(string s)
    {
        Match m = TempRegex().Match(s);
        if (!m.Success || !TryNum(m.Groups[1].Value, out double value))
            return null;

        string unit = m.Groups[2].Value;
        bool isF = unit is "f" or "fahrenheit";
        if (isF)
        {
            double c = (value - 32) * 5.0 / 9.0;
            return UnitResult("Температура", new ResultLine("Цельсий", $"{Fmt(c)} °C"));
        }

        double f = value * 9.0 / 5.0 + 32;
        return UnitResult("Температура", new ResultLine("Фаренгейт", $"{Fmt(f)} °F"));
    }

    private static ConversionResult? TryCompoundFeetInches(string s)
    {
        Match m = FeetInchesRegex().Match(s);
        if (!m.Success || !TryNum(m.Groups[1].Value, out double feet) || !TryNum(m.Groups[2].Value, out double inches))
            return null;

        double metres = (feet * 12 + inches) * 0.0254;
        return BuildLength(metres, "ft");
    }

    private static ConversionResult? TrySingle(string s)
    {
        Match m = SingleRegex().Match(s);
        if (!m.Success || !TryNum(m.Groups[1].Value, out double value))
            return null;

        if (!Tokens.TryGetValue(m.Groups[2].Value, out string? canonical))
            return null;

        if (LengthToMetre.TryGetValue(canonical, out double toMetre))
            return BuildLength(value * toMetre, canonical);

        if (WeightToKg.TryGetValue(canonical, out double toKg))
            return BuildWeight(value * toKg, canonical);

        return null;
    }

    private static ConversionResult BuildLength(double metres, string source)
    {
        string[] targets = source switch
        {
            "ft" => new[] { "cm", "m" },
            "in" => new[] { "cm" },
            "yd" => new[] { "m" },
            "mi" => new[] { "km" },
            "mm" or "cm" => new[] { "in" },
            "m" => new[] { "ft" },
            "km" => new[] { "mi" },
            _ => new[] { "m" },
        };

        var lines = new List<ResultLine>();
        foreach (string t in targets)
            if (LengthToMetre.TryGetValue(t, out double factor))
                lines.Add(new ResultLine(LengthLabel(t), $"{Fmt(metres / factor)} {t}"));

        return new ConversionResult(ValueKind.Unit, "Единицы", lines);
    }

    private static ConversionResult BuildWeight(double kg, string source)
    {
        string target = source switch
        {
            "lb" => "kg",
            "oz" => "g",
            "kg" => "lb",
            _ => "oz",
        };

        var line = new ResultLine(WeightLabel(target), $"{Fmt(kg / WeightToKg[target])} {target}");
        return new ConversionResult(ValueKind.Unit, "Единицы", new List<ResultLine> { line });
    }

    private static ConversionResult UnitResult(string title, ResultLine line)
        => new(ValueKind.Unit, title, new List<ResultLine> { line });

    private static string LengthLabel(string u) => u switch
    {
        "mm" => "Миллиметры",
        "cm" => "Сантиметры",
        "m" => "Метры",
        "km" => "Километры",
        "in" => "Дюймы",
        "ft" => "Футы",
        "yd" => "Ярды",
        "mi" => "Мили",
        _ => u,
    };

    private static string WeightLabel(string u) => u switch
    {
        "g" => "Граммы",
        "kg" => "Килограммы",
        "oz" => "Унции",
        "lb" => "Фунты",
        _ => u,
    };

    private static bool TryNum(string raw, out double value)
        => double.TryParse(raw.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static string Fmt(double v) => Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^(-?\d+(?:[.,]\d+)?)\s*(f|c|fahrenheit|celsius)$")]
    private static partial Regex TempRegex();

    [GeneratedRegex(@"^(\d+(?:[.,]\d+)?)\s*(?:ft|feet|foot|'|′)\s*(\d+(?:[.,]\d+)?)\s*(?:in|inch|inches|""|″)?$")]
    private static partial Regex FeetInchesRegex();

    [GeneratedRegex(@"^(-?\d+(?:[.,]\d+)?)\s*([a-zа-я""'″′]+)$")]
    private static partial Regex SingleRegex();
}
