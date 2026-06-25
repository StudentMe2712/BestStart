using SelectCast.Core.Conversion;
using SelectCast.Core.Detect;
using SelectCast.Core.Rates;
using Xunit;

namespace SelectCast.Core.Tests;

public class TypeDetectorTests
{
    private readonly TypeDetector _sut = new();

    private sealed class ConstantRates : IRatesProvider
    {
        public RateTable? Current { get; } = new(new DateOnly(2026, 6, 25), "usd",
            new Dictionary<string, decimal> { ["usd"] = 1m, ["kzt"] = 500m });
        public Task RefreshAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    [Theory]
    [InlineData("#3A7BD5", ValueKind.Color)]
    [InlineData("rgb(58, 123, 213)", ValueKind.Color)]
    [InlineData("72 °F", ValueKind.Unit)]
    [InlineData("5 ft 9", ValueKind.Unit)]
    [InlineData("10км", ValueKind.Unit)]
    [InlineData("3 PM EST", ValueKind.Time)]
    [InlineData("20:00 GMT+3", ValueKind.Time)]
    [InlineData("абракадабра", ValueKind.Unknown)]
    [InlineData("", ValueKind.Unknown)]
    public void Detects_type(string input, ValueKind expected)
    {
        Assert.Equal(expected, _sut.Detect(input).Type);
    }

    [Fact]
    public void Hash_color_is_not_mistaken_for_number()
    {
        // #3A7BD5 must resolve to Color, never Unknown/number.
        Assert.Equal(ValueKind.Color, _sut.Detect("#3A7BD5").Type);
    }

    [Fact]
    public void Currency_appended_without_breaking_earlier_routing()
    {
        // CreateDefault appends Currency last; earlier converters must still win their inputs.
        var detector = TypeDetector.CreateDefault(new ConstantRates());

        Assert.Equal(ValueKind.Currency, detector.Detect("$100").Type);
        Assert.Equal(ValueKind.Color, detector.Detect("#3A7BD5").Type);
        Assert.Equal(ValueKind.Unit, detector.Detect("72 °F").Type);
        Assert.Equal(ValueKind.Time, detector.Detect("3 PM EST").Type);
    }
}
