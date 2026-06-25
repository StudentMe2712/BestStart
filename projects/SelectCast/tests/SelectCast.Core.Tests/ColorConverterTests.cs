using SelectCast.Core.Conversion;
using SelectCast.Core.Converters;
using Xunit;

namespace SelectCast.Core.Tests;

public class ColorConverterTests
{
    private readonly ColorConverter _sut = new();

    [Theory]
    [InlineData("#3A7BD5", "#3A7BD5", "rgb(58, 123, 213)")]
    [InlineData("#fff", "#FFFFFF", "rgb(255, 255, 255)")]
    [InlineData("rgb(58, 123, 213)", "#3A7BD5", "rgb(58, 123, 213)")]
    [InlineData("rgba(58,123,213,0.5)", "#3A7BD5", "rgb(58, 123, 213)")]
    public void Parses_hex_and_rgb(string input, string expectedHex, string expectedRgb)
    {
        ConversionResult? r = _sut.TryConvert(input);

        Assert.NotNull(r);
        Assert.Equal(ValueKind.Color, r!.Type);
        Assert.Equal(expectedHex, r.Lines[0].Value);
        Assert.Equal(expectedRgb, r.Lines[1].Value);
    }

    [Theory]
    [InlineData("hsl(0, 0%, 100%)", "#FFFFFF")]
    [InlineData("hsl(0, 0%, 0%)", "#000000")]
    [InlineData("hsl(0, 100%, 50%)", "#FF0000")]
    public void Parses_hsl(string input, string expectedHex)
    {
        ConversionResult? r = _sut.TryConvert(input);

        Assert.NotNull(r);
        Assert.Equal(expectedHex, r!.Lines[0].Value);
    }

    [Fact]
    public void Emits_swatch_matching_rgb()
    {
        ConversionResult? r = _sut.TryConvert("#3A7BD5");

        Assert.NotNull(r!.Swatch);
        Assert.Equal((byte)58, r.Swatch!.Value.R);
        Assert.Equal((byte)123, r.Swatch.Value.G);
        Assert.Equal((byte)213, r.Swatch.Value.B);
    }

    [Fact]
    public void Hue_is_normalised_below_360()
    {
        // rgb(255,0,1) rounds hue up to 360 → must normalise to 0.
        ConversionResult? r = _sut.TryConvert("rgb(255, 0, 1)");

        Assert.NotNull(r);
        Assert.Equal("hsl(0, 100%, 50%)", r!.Lines[2].Value);
    }

    [Theory]
    [InlineData("#GG")]
    [InlineData("rgb(300, 0, 0)")]
    [InlineData("not a color")]
    [InlineData("129")]
    public void Rejects_non_colors(string input)
    {
        Assert.Null(_sut.TryConvert(input));
    }
}
