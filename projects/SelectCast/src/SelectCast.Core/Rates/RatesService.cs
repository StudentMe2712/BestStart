using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace SelectCast.Core.Rates;

/// <summary>
/// Loads exchange rates with a local cache. Refreshes at most once per day, tries several
/// sources in order, and falls back to the (possibly stale) cache when offline. The only
/// network traffic is the rate request — never the selected text.
/// </summary>
public sealed class RatesService : IRatesProvider
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly string _cachePath;
    private readonly IReadOnlyList<Func<CancellationToken, Task<RateTable?>>> _sources;

    public RateTable? Current { get; private set; }

    public RatesService(
        string? cachePath = null,
        IReadOnlyList<Func<CancellationToken, Task<RateTable?>>>? sources = null)
    {
        _cachePath = cachePath ?? DefaultCachePath();
        _sources = sources ?? DefaultSources();
        Current = LoadCache();
    }

    public static string DefaultCachePath() => Path.Combine(AppPaths.DataDir(), "rates.json");

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        // Daily data — skip if we already have a fresh (non-stale) table from today (or later:
        // national banks publish the next business day's rate ahead of UTC midnight).
        if (Current is { Stale: false } cur && cur.Date >= DateOnly.FromDateTime(DateTime.UtcNow))
            return;

        foreach (Func<CancellationToken, Task<RateTable?>> source in _sources)
        {
            try
            {
                RateTable? table = await source(ct).ConfigureAwait(false);
                if (table is not null)
                {
                    Current = table with { Stale = false };
                    SaveCache(Current);
                    return;
                }
            }
            catch
            {
                // Source failed — try the next one.
            }
        }

        // Every source failed: keep the cached table but flag it as possibly stale.
        if (Current is not null)
            Current = Current with { Stale = true };
    }

    private RateTable? LoadCache()
    {
        try
        {
            if (!File.Exists(_cachePath))
                return null;

            RateTable? table = JsonSerializer.Deserialize<RateTable>(File.ReadAllText(_cachePath));
            if (table is null)
                return null;

            // JSON deserialization rebuilds Rates with a case-sensitive comparer. The converter
            // looks codes up upper-cased (USD, KZT) against the source's lower-case keys, so the
            // cached table must keep the OrdinalIgnoreCase lookup the live fetch gives it —
            // otherwise conversion silently breaks on every run after the first (and offline).
            return table with
            {
                Rates = new Dictionary<string, decimal>(table.Rates, StringComparer.OrdinalIgnoreCase),
            };
        }
        catch
        {
            return null;
        }
    }

    private void SaveCache(RateTable table)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
            File.WriteAllText(_cachePath, JsonSerializer.Serialize(table));
        }
        catch
        {
            // Cache write is best-effort.
        }
    }

    // CIS national banks first (per the product's CIS focus — KZT/RUB), then the worldwide
    // fawazahmed0 API as a fallback. Each source is normalised to base USD; the first that
    // returns a table wins.
    private static IReadOnlyList<Func<CancellationToken, Task<RateTable?>>> DefaultSources() =>
        new Func<CancellationToken, Task<RateTable?>>[]
        {
            ct => FetchNbkAsync("https://nationalbank.kz/rss/rates_all.xml", ct),
            ct => FetchCbrAsync("https://www.cbr.ru/scripts/XML_daily.asp", ct),
            ct => FetchFawazAsync("https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies/usd.min.json", ct),
            ct => FetchFawazAsync("https://latest.currency-api.pages.dev/v1/currencies/usd.min.json", ct),
        };

    /// <summary>National Bank of Kazakhstan RSS (base KZT, dot decimals).</summary>
    private static async Task<RateTable?> FetchNbkAsync(string url, CancellationToken ct)
    {
        string xml = await Http.GetStringAsync(url, ct).ConfigureAwait(false);
        return ParseNbk(xml);
    }

    /// <summary>Central Bank of Russia XML_daily (base RUB, windows-1251, comma decimals).</summary>
    private static async Task<RateTable?> FetchCbrAsync(string url, CancellationToken ct)
    {
        // CBR serves windows-1251. Codes and numbers are ASCII, so decode as Latin1 to avoid a
        // System.Text.Encoding.CodePages dependency — the Cyrillic <Name> garbles but is unused.
        byte[] bytes = await Http.GetByteArrayAsync(url, ct).ConfigureAwait(false);
        return ParseCbr(Encoding.Latin1.GetString(bytes));
    }

    /// <summary>
    /// Parses NBK RSS: each &lt;item&gt; has &lt;title&gt;CODE&lt;/title&gt;,
    /// &lt;description&gt;price&lt;/description&gt; (KZT per &lt;quant&gt; units), &lt;pubDate&gt;dd.MM.yyyy&lt;/pubDate&gt;.
    /// </summary>
    internal static RateTable? ParseNbk(string xml)
    {
        try
        {
            XDocument doc = XDocument.Parse(xml);
            var kztPerUnit = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            DateOnly date = DateOnly.FromDateTime(DateTime.UtcNow);
            bool dateSet = false;

            foreach (XElement item in doc.Descendants("item"))
            {
                string code = (item.Element("title")?.Value ?? string.Empty).Trim();
                if (code.Length != 3 ||
                    !decimal.TryParse(item.Element("description")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal price) ||
                    price <= 0)
                    continue;

                int quant = int.TryParse(item.Element("quant")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int q) && q > 0 ? q : 1;
                kztPerUnit[code] = price / quant;

                if (!dateSet)
                {
                    date = ParseDmy(item.Element("pubDate")?.Value);
                    dateSet = true;
                }
            }

            return BuildUsdBase(kztPerUnit, "KZT", date);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses CBR XML_daily: &lt;ValCurs Date="dd.MM.yyyy"&gt; with &lt;Valute&gt;&lt;CharCode&gt;,
    /// &lt;Nominal&gt;, &lt;Value&gt; (RUB per Nominal units, comma decimal).
    /// </summary>
    internal static RateTable? ParseCbr(string xml)
    {
        try
        {
            XDocument doc = XDocument.Parse(xml);
            var rubPerUnit = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            foreach (XElement v in doc.Descendants("Valute"))
            {
                string code = (v.Element("CharCode")?.Value ?? string.Empty).Trim();
                string value = (v.Element("Value")?.Value ?? string.Empty).Replace(',', '.');
                if (code.Length != 3 ||
                    !decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal price) ||
                    price <= 0)
                    continue;

                int nominal = int.TryParse(v.Element("Nominal")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n > 0 ? n : 1;
                rubPerUnit[code] = price / nominal;
            }

            DateOnly date = ParseDmy(doc.Root?.Attribute("Date")?.Value);
            return BuildUsdBase(rubPerUnit, "RUB", date);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Normalises a national table — <paramref name="localPerUnit"/>[code] = how many units of
    /// <paramref name="localCode"/> equal 1 unit of <c>code</c> — to base USD (1 USD = Rates[code]).
    /// Needs a USD anchor in the table; returns null otherwise so the next source is tried.
    /// </summary>
    private static RateTable? BuildUsdBase(Dictionary<string, decimal> localPerUnit, string localCode, DateOnly date)
    {
        if (!localPerUnit.TryGetValue("USD", out decimal usdInLocal) || usdInLocal <= 0)
            return null;

        var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["usd"] = 1m,
            [localCode] = usdInLocal, // 1 USD = usdInLocal units of the bank's own currency
        };

        foreach ((string code, decimal local) in localPerUnit)
        {
            if (local > 0)
                rates[code] = usdInLocal / local; // 1 USD = (usdInLocal / local) units of `code`
        }

        return new RateTable(date, "usd", rates);
    }

    private static DateOnly ParseDmy(string? s) =>
        DateOnly.TryParseExact(s, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly d)
            ? d
            : DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Parses the fawazahmed0 schema: { "date": "...", "usd": { code: rate, … } }.</summary>
    private static async Task<RateTable?> FetchFawazAsync(string url, CancellationToken ct)
    {
        string json = await Http.GetStringAsync(url, ct).ConfigureAwait(false);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        DateOnly date = root.TryGetProperty("date", out JsonElement d) && DateOnly.TryParse(d.GetString(), out DateOnly parsed)
            ? parsed
            : DateOnly.FromDateTime(DateTime.UtcNow);

        if (!root.TryGetProperty("usd", out JsonElement usd) || usd.ValueKind != JsonValueKind.Object)
            return null;

        var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["usd"] = 1m };
        foreach (JsonProperty p in usd.EnumerateObject())
        {
            if (p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetDecimal(out decimal r))
                rates[p.Name] = r;
        }

        return new RateTable(date, "usd", rates);
    }
}
