using System.Text.Json;
using SelectCast.Core.Conversion;
using SelectCast.Core.Converters;
using SelectCast.Core.Rates;
using Xunit;

namespace SelectCast.Core.Tests;

public class CurrencyConverterTests
{
    // Fixed rates relative to USD (1 USD = N). Chosen so conversions are exact (no rounding noise).
    private static readonly RateTable Table = new(
        new DateOnly(2026, 6, 25),
        "usd",
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["usd"] = 1m,
            ["eur"] = 0.5m,
            ["rub"] = 100m,
            ["kzt"] = 500m,
        });

    /// <summary>Deterministic provider — never touches the network.</summary>
    private sealed class FakeRates(RateTable? table) : IRatesProvider
    {
        public RateTable? Current { get; } = table;
        public Task RefreshAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private readonly CurrencyConverter _sut = new(new FakeRates(Table));

    private static string Line(ConversionResult r, string name)
        => r.Lines.Single(l => l.Label == name).Value;

    [Fact]
    public void Dollar_amount_converts_to_default_targets()
    {
        ConversionResult? r = _sut.TryConvert("$100");

        Assert.NotNull(r);
        Assert.Equal(ValueKind.Currency, r!.Type);
        Assert.Equal("50,000.00 KZT", Line(r, "KZT")); // 100 * 500
        Assert.Equal("10,000.00 RUB", Line(r, "RUB")); // 100 * 100
        Assert.Equal("50.00 EUR", Line(r, "EUR"));     // 100 * 0.5
        Assert.Equal("2026-06-25", Line(r, "Курс на"));
        Assert.DoesNotContain(r.Lines, l => l.Label == "USD"); // source currency is skipped
    }

    [Theory]
    [InlineData("$100", "50,000.00 KZT")]
    [InlineData("100 USD", "50,000.00 KZT")]
    [InlineData("100$", "50,000.00 KZT")]
    [InlineData("usd 100", "50,000.00 KZT")]
    [InlineData("$1,299.50", "649,750.00 KZT")] // comma thousands + dot decimal → 1299.50 * 500
    [InlineData("$2k", "1,000,000.00 KZT")]      // k suffix → 2000 * 500
    public void Parses_amount_and_currency(string input, string expectedKzt)
    {
        ConversionResult? r = _sut.TryConvert(input);

        Assert.NotNull(r);
        Assert.Equal(expectedKzt, Line(r!, "KZT"));
    }

    [Fact]
    public void Euro_with_k_suffix_cross_converts()
    {
        ConversionResult? r = _sut.TryConvert("€1.5k"); // 1500 EUR

        Assert.NotNull(r);
        Assert.Equal("3,000.00 USD", Line(r!, "USD"));       // 1500 / 0.5
        Assert.Equal("300,000.00 RUB", Line(r, "RUB"));      // 1500 * 100 / 0.5
        Assert.Equal("1,500,000.00 KZT", Line(r, "KZT"));    // 1500 * 500 / 0.5
        Assert.DoesNotContain(r.Lines, l => l.Label == "EUR"); // source skipped
    }

    [Theory]
    [InlineData("129")]      // no currency marker
    [InlineData("hello")]
    [InlineData("#FFF")]
    [InlineData("")]
    [InlineData("15:00 UTC")]
    public void Rejects_non_currency(string input)
    {
        Assert.Null(_sut.TryConvert(input));
    }

    [Fact]
    public void Reports_unavailable_when_no_rate_table()
    {
        var sut = new CurrencyConverter(new FakeRates(null));

        ConversionResult? r = sut.TryConvert("$100");

        Assert.NotNull(r);
        Assert.Equal(ValueKind.Currency, r!.Type);
        Assert.Contains(r.Lines, l => l.Value.Contains("недоступен"));
    }

    [Fact]
    public void Marks_stale_table()
    {
        var sut = new CurrencyConverter(new FakeRates(Table with { Stale = true }));

        ConversionResult? r = sut.TryConvert("$1");

        Assert.NotNull(r);
        Assert.Contains("возможно устарел", Line(r!, "Курс на"));
    }

    [Fact]
    public void Converts_from_json_cache_not_only_live_fetch()
    {
        // Reproduces the cache path: rates written to disk, then loaded by a fresh RatesService
        // with no network sources. System.Text.Json drops the OrdinalIgnoreCase comparer on the
        // Rates dictionary, so without normalization the converter (which looks up upper-cased
        // codes) would find nothing — the bug only ever appears from cache, never on first fetch.
        string tmp = Path.Combine(Path.GetTempPath(), $"selectcast_rates_{Guid.NewGuid():N}.json");
        File.WriteAllText(tmp, JsonSerializer.Serialize(Table));
        try
        {
            var svc = new RatesService(tmp, Array.Empty<Func<CancellationToken, Task<RateTable?>>>());
            var conv = new CurrencyConverter(svc);

            ConversionResult? r = conv.TryConvert("$100");

            Assert.NotNull(r);
            Assert.Equal("50,000.00 KZT", Line(r!, "KZT")); // 100 * 500, must work from cache too
        }
        finally
        {
            File.Delete(tmp);
        }
    }
}
