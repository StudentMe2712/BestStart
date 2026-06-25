using SelectCast.Core.Conversion;
using SelectCast.Core.Converters;
using Xunit;

namespace SelectCast.Core.Tests;

public class TimeConverterTests
{
    private readonly TimeConverter _sut = new();

    private static string Line(ConversionResult r, string name)
        => r.Lines.Single(l => l.Label == name).Value;

    [Fact]
    public void Three_pm_est_to_astana_and_moscow()
    {
        ConversionResult? r = _sut.TryConvert("3 PM EST");

        Assert.NotNull(r);
        Assert.Equal(ValueKind.Time, r!.Type);
        Assert.Equal("01:00 (+1)", Line(r, "Астана")); // 15:00 −05:00 → 20:00Z → +5 next day
        Assert.Equal("23:00", Line(r, "Москва"));      // → +3
        Assert.Equal("20:00", Line(r, "UTC"));
    }

    [Theory]
    [InlineData("15:00 UTC", "15:00")]
    [InlineData("9am PST", "17:00")]   // 09:00 −08:00 → 17:00Z
    [InlineData("20:00 GMT+3", "17:00")] // 20:00 +03:00 → 17:00Z
    [InlineData("12:00 AM UTC", "00:00")] // midnight
    [InlineData("12:00 PM UTC", "12:00")] // noon
    [InlineData("0:00 UTC", "00:00")]
    public void Various_formats_to_utc(string input, string expectedUtc)
    {
        ConversionResult? r = _sut.TryConvert(input);

        Assert.NotNull(r);
        Assert.Equal(expectedUtc, Line(r!, "UTC"));
    }

    [Fact]
    public void Dst_zone_resolved_via_timezoneinfo()
    {
        TimeZoneInfo uk = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time"); // London, observes BST
        var london = new[] { new TimeZoneTarget("Лондон", Zone: uk) };

        var summer = new TimeConverter(london, () => new DateTime(2026, 7, 1));
        var winter = new TimeConverter(london, () => new DateTime(2026, 1, 1));

        Assert.Equal("13:00", summer.TryConvert("12:00 UTC")!.Lines[0].Value); // BST (+1)
        Assert.Equal("12:00", winter.TryConvert("12:00 UTC")!.Lines[0].Value); // GMT
    }

    [Theory]
    [InlineData("129")]
    [InlineData("15")]
    [InlineData("5 ft 9")]
    [InlineData("#FFF")]
    [InlineData("hello")]
    [InlineData("25:00 UTC")]
    public void Rejects_non_times(string input)
    {
        Assert.Null(_sut.TryConvert(input));
    }
}
