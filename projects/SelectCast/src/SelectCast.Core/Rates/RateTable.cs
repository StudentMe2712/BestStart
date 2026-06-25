namespace SelectCast.Core.Rates;

/// <summary>A snapshot of exchange rates relative to <see cref="Base"/> (e.g. 1 USD = Rates[code]).</summary>
public sealed record RateTable(
    DateOnly Date,
    string Base,
    IReadOnlyDictionary<string, decimal> Rates,
    bool Stale = false);

/// <summary>Supplies the latest known rate table. Only rate fetching touches the network.</summary>
public interface IRatesProvider
{
    /// <summary>The cached table (from disk or last fetch), or null if none is available yet.</summary>
    RateTable? Current { get; }

    /// <summary>Refreshes rates if stale (≤ once/day); falls back across sources, then to cache.</summary>
    Task RefreshAsync(CancellationToken ct = default);
}
