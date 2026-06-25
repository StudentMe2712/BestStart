using SelectCast.Core.Conversion;
using SelectCast.Core.Converters;
using Xunit;

namespace SelectCast.Core.Tests;

public class UnitConverterTests
{
    private readonly UnitConverter _sut = new();

    [Fact]
    public void Compound_feet_inches_to_cm_and_m()
    {
        ConversionResult? r = _sut.TryConvert("5 ft 9");

        Assert.NotNull(r);
        Assert.Equal(ValueKind.Unit, r!.Type);
        Assert.Equal("175.26 cm", r.Lines[0].Value);
        Assert.Equal("1.75 m", r.Lines[1].Value);
    }

    [Theory]
    [InlineData("72 °F", "22.22 °C")]
    [InlineData("72F", "22.22 °C")]
    [InlineData("0 C", "32 °F")]
    [InlineData("100 °C", "212 °F")]
    public void Temperature(string input, string expected)
    {
        ConversionResult? r = _sut.TryConvert(input);

        Assert.NotNull(r);
        Assert.Equal(expected, r!.Lines[0].Value);
    }

    [Theory]
    [InlineData("10км", "6.21 mi")]
    [InlineData("1 mi", "1.61 km")]
    [InlineData("12 in", "30.48 cm")]
    [InlineData("10 lb", "4.54 kg")]
    [InlineData("100 g", "3.53 oz")]
    public void Length_and_weight(string input, string expectedFirst)
    {
        ConversionResult? r = _sut.TryConvert(input);

        Assert.NotNull(r);
        Assert.Equal(expectedFirst, r!.Lines[0].Value);
    }

    [Theory]
    [InlineData("9″", "22.86 cm")]      // U+2033 double prime → inches
    [InlineData("5′ 9″", "175.26 cm")]  // U+2032/U+2033 compound feet+inches
    public void Unicode_prime_marks(string input, string expectedFirst)
    {
        ConversionResult? r = _sut.TryConvert(input);

        Assert.NotNull(r);
        Assert.Equal(expectedFirst, r!.Lines[0].Value);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("129")]
    [InlineData("#FFF")]
    public void Rejects_non_units(string input)
    {
        Assert.Null(_sut.TryConvert(input));
    }
}
