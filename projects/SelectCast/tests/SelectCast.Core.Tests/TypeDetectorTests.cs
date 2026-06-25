using SelectCast.Core.Conversion;
using SelectCast.Core.Detect;
using Xunit;

namespace SelectCast.Core.Tests;

public class TypeDetectorTests
{
    private readonly TypeDetector _sut = new();

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
}
