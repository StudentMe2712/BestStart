using System.IO;
using System.Net.Http;
using System.Text.Json;

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

    public static string DefaultCachePath()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SelectCast");
        return Path.Combine(dir, "rates.json");
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        // Daily data — skip if we already have a fresh (non-stale) table from today.
        if (Current is { Stale: false } cur && cur.Date == DateOnly.FromDateTime(DateTime.UtcNow))
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

    private static IReadOnlyList<Func<CancellationToken, Task<RateTable?>>> DefaultSources() =>
        new Func<CancellationToken, Task<RateTable?>>[]
        {
            ct => FetchFawazAsync("https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies/usd.min.json", ct),
            ct => FetchFawazAsync("https://latest.currency-api.pages.dev/v1/currencies/usd.min.json", ct),
        };

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
