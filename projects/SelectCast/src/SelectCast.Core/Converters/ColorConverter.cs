using System.Globalization;
using System.Text.RegularExpressions;
using SelectCast.Core.Conversion;

namespace SelectCast.Core.Converters;

/// <summary>
/// Recognises <c>#RRGGBB</c>, <c>#RGB</c>, <c>rgb(r,g,b)</c>, <c>rgba(r,g,b,a)</c>,
/// <c>hsl(h,s%,l%)</c> and emits HEX + RGB + HSL together with an RGB swatch.
/// </summary>
public sealed partial class ColorConverter : IValueConverter
{
    public ValueKind Type => ValueKind.Color;

    public ConversionResult? TryConvert(string input)
    {
        string s = input.Trim();

        if (TryParseHex(s, out byte r, out byte g, out byte b)
            || TryParseRgb(s, out r, out g, out b)
            || TryParseHsl(s, out r, out g, out b))
        {
            return Build(r, g, b);
        }

        return null;
    }

    private static ConversionResult Build(byte r, byte g, byte b)
    {
        (int h, int s, int l) = RgbToHsl(r, g, b);

        var lines = new List<ResultLine>
        {
            new("HEX", $"#{r:X2}{g:X2}{b:X2}"),
            new("RGB", $"rgb({r}, {g}, {b})"),
            new("HSL", $"hsl({h}, {s}%, {l}%)"),
        };

        return new ConversionResult(ValueKind.Color, "Цвет", lines, new ColorSwatch(r, g, b));
    }

    private static bool TryParseHex(string s, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        Match m = HexRegex().Match(s);
        if (!m.Success)
            return false;

        string hex = m.Groups[1].Value;
        if (hex.Length == 3)
            hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);

        r = Convert.ToByte(hex.Substring(0, 2), 16);
        g = Convert.ToByte(hex.Substring(2, 2), 16);
        b = Convert.ToByte(hex.Substring(4, 2), 16);
        return true;
    }

    private static bool TryParseRgb(string s, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        Match m = RgbRegex().Match(s);
        if (!m.Success)
            return false;

        if (!TryByte(m.Groups[1].Value, out r)
            || !TryByte(m.Groups[2].Value, out g)
            || !TryByte(m.Groups[3].Value, out b))
            return false;

        return true;
    }

    private static bool TryParseHsl(string s, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        Match m = HslRegex().Match(s);
        if (!m.Success)
            return false;

        if (!int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int h)
            || !int.TryParse(m.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sp)
            || !int.TryParse(m.Groups[3].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int lp))
            return false;

        if (h is < 0 or > 360 || sp is < 0 or > 100 || lp is < 0 or > 100)
            return false;

        (r, g, b) = HslToRgb(h % 360, sp / 100.0, lp / 100.0);
        return true;
    }

    private static bool TryByte(string value, out byte b)
    {
        b = 0;
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) || n is < 0 or > 255)
            return false;
        b = (byte)n;
        return true;
    }

    private static (int H, int S, int L) RgbToHsl(byte r8, byte g8, byte b8)
    {
        double r = r8 / 255.0, g = g8 / 255.0, b = b8 / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) / 2.0;
        double h = 0, s = 0;

        if (max > min)
        {
            double d = max - min;
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);

            if (max == r)
                h = (g - b) / d + (g < b ? 6 : 0);
            else if (max == g)
                h = (b - r) / d + 2;
            else
                h = (r - g) / d + 4;

            h *= 60;
        }

        // % 360 normalises hues that round up to 360 (e.g. rgb(255,0,1)) back to 0.
        return ((int)Math.Round(h) % 360, (int)Math.Round(s * 100), (int)Math.Round(l * 100));
    }

    private static (byte R, byte G, byte B) HslToRgb(int hDeg, double s, double l)
    {
        double h = hDeg / 360.0;

        if (s == 0)
        {
            byte v = (byte)Math.Round(l * 255);
            return (v, v, v);
        }

        double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        double p = 2 * l - q;

        double r = HueToChannel(p, q, h + 1.0 / 3.0);
        double g = HueToChannel(p, q, h);
        double b = HueToChannel(p, q, h - 1.0 / 3.0);

        return ((byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
    }

    private static double HueToChannel(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
        return p;
    }

    [GeneratedRegex(@"^#([0-9a-fA-F]{6}|[0-9a-fA-F]{3})$")]
    private static partial Regex HexRegex();

    [GeneratedRegex(@"^rgba?\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*(?:,\s*[\d.]+\s*)?\)$", RegexOptions.IgnoreCase)]
    private static partial Regex RgbRegex();

    [GeneratedRegex(@"^hsla?\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*%\s*,\s*(\d{1,3})\s*%\s*(?:,\s*[\d.]+\s*)?\)$", RegexOptions.IgnoreCase)]
    private static partial Regex HslRegex();
}
