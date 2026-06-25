using System.Globalization;
using System.Text.RegularExpressions;
using SelectCast.Core.Conversion;
using SelectCast.Core.Rates;

namespace SelectCast.Core.Converters;

/// <summary>
/// Recognises amounts like <c>$129</c>, <c>129 USD</c>, <c>129$</c>, <c>1 299,50 ₽</c>, <c>€1.5k</c>
/// and converts to target currencies (default KZT, RUB, USD, EUR) using cached rates, with the
/// rate date shown for transparency.
/// </summary>
public sealed partial class CurrencyConverter : IValueConverter
{
    public ValueKind Type => ValueKind.Currency;

    private static readonly IReadOnlyList<string> DefaultTargets = new[] { "KZT", "RUB", "USD", "EUR" };

    private static readonly Dictionary<char, string> Symbols = new()
    {
        ['$'] = "USD",
        ['€'] = "EUR",
        ['₽'] = "RUB",
        ['₸'] = "KZT",
        ['£'] = "GBP",
        ['¥'] = "JPY",
    };

    private static readonly HashSet<string> KnownCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "USD", "EUR", "RUB", "KZT", "GBP", "JPY", "CNY", "CHF", "CAD", "AUD", "TRY", "UAH", "BYN", "PLN", "INR", "AED",
    };

    private readonly IRatesProvider _rates;
    private readonly IReadOnlyList<string> _targets;

    public CurrencyConverter(IRatesProvider rates, IReadOnlyList<string>? targets = null)
    {
        _rates = rates;
        _targets = targets ?? DefaultTargets;
    }

    public ConversionResult? TryConvert(string input)
    {
        if (!TryParse(input, out string source, out decimal amount))
            return null;

        RateTable? table = _rates.Current;
        if (table is null)
        {
            return new ConversionResult(ValueKind.Currency, "Валюта",
                new List<ResultLine> { new("Курс", "недоступен (нет данных)") });
        }

        var lines = new List<ResultLine>();
        foreach (string target in _targets)
        {
            if (string.Equals(target, source, StringComparison.OrdinalIgnoreCase))
                continue;
            if (TryCross(table, amount, source, target, out decimal value))
                lines.Add(new ResultLine(target, $"{value.ToString("N2", CultureInfo.InvariantCulture)} {target}"));
        }

        if (lines.Count == 0)
            lines.Add(new ResultLine("Курс", $"нет данных для {source}"));

        string stale = table.Stale ? " (возможно устарел)" : string.Empty;
        lines.Add(new ResultLine("Курс на", table.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + stale));

        return new ConversionResult(ValueKind.Currency, "Валюта", lines);
    }

    private static bool TryCross(RateTable t, decimal amount, string from, string to, out decimal value)
    {
        value = 0;
        if (!t.Rates.TryGetValue(from, out decimal rFrom) || !t.Rates.TryGetValue(to, out decimal rTo) || rFrom == 0)
            return false;
        value = amount * rTo / rFrom;
        return true;
    }

    private static bool TryParse(string input, out string currency, out decimal amount)
    {
        currency = string.Empty;
        amount = 0;
        string s = input.Trim();
        if (s.Length == 0)
            return false;

        // Currency by symbol …
        foreach ((char sym, string code) in Symbols)
        {
            if (s.Contains(sym))
            {
                currency = code;
                s = s.Replace(sym.ToString(), "");
                break;
            }
        }

        // … or by a known 3-letter code.
        if (currency.Length == 0)
        {
            Match cm = CodeRegex().Match(s);
            if (cm.Success && KnownCodes.Contains(cm.Value))
            {
                currency = cm.Value.ToUpperInvariant();
                s = s.Remove(cm.Index, cm.Length);
            }
        }

        if (currency.Length == 0)
            return false;

        return TryAmount(s, out amount);
    }

    private static bool TryAmount(string raw, out decimal amount)
    {
        amount = 0;
        string s = raw.Trim();

        decimal multiplier = 1m;
        if (s.Length > 0 && char.ToLowerInvariant(s[^1]) is 'k' or 'm' or 'к' or 'м')
        {
            multiplier = char.ToLowerInvariant(s[^1]) is 'k' or 'к' ? 1_000m : 1_000_000m;
            s = s[..^1].Trim();
        }

        s = s.Replace(" ", "").Replace(" ", "");
        if (s.Length == 0 || !DigitsRegex().IsMatch(s))
            return false;

        s = NormalizeSeparators(s);

        if (!decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal value))
            return false;

        amount = value * multiplier;
        return true;
    }

    private static string NormalizeSeparators(string s)
    {
        bool hasComma = s.Contains(',');
        bool hasDot = s.Contains('.');

        if (hasComma && hasDot)
        {
            // Rightmost separator is the decimal point; the other groups thousands.
            char dec = s.LastIndexOf(',') > s.LastIndexOf('.') ? ',' : '.';
            char thousands = dec == ',' ? '.' : ',';
            return s.Replace(thousands.ToString(), "").Replace(dec, '.');
        }

        char sep = hasComma ? ',' : hasDot ? '.' : '\0';
        if (sep == '\0')
            return s;

        int count = s.Count(c => c == sep);
        int afterLast = s.Length - 1 - s.LastIndexOf(sep);
        // Single separator with exactly 3 trailing digits → thousands ("1,299"); otherwise decimal.
        bool isThousands = count > 1 || (count == 1 && afterLast == 3);
        return isThousands ? s.Replace(sep.ToString(), "") : s.Replace(sep, '.');
    }

    [GeneratedRegex(@"[a-zA-Z]{3}")]
    private static partial Regex CodeRegex();

    [GeneratedRegex(@"^[\d.,]+$")]
    private static partial Regex DigitsRegex();
}
