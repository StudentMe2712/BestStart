using SelectCast.Core.Rates;
using Xunit;

namespace SelectCast.Core.Tests;

public class RateSourceTests
{
    private static decimal Rate(RateTable t, string code) => t.Rates[code];

    private const string NbkXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <rss version="2.0"><channel>
          <title>Official exchange rates of National Bank of Republic Kazakhstan</title>
          <item><title>USD</title><pubDate>26.06.2026</pubDate><description>485.4</description><quant>1</quant></item>
          <item><title>EUR</title><pubDate>26.06.2026</pubDate><description>550.69</description><quant>1</quant></item>
          <item><title>RUB</title><pubDate>26.06.2026</pubDate><description>6.41</description><quant>1</quant></item>
          <item><title>AMD</title><pubDate>26.06.2026</pubDate><description>13.23</description><quant>10</quant></item>
        </channel></rss>
        """;

    // CBR serves windows-1251; the declaration is informational once decoded to a string.
    private const string CbrXml = """
        <?xml version="1.0" encoding="windows-1251"?>
        <ValCurs Date="26.06.2026" name="Foreign Currency Market">
          <Valute ID="R01235"><NumCode>840</NumCode><CharCode>USD</CharCode><Nominal>1</Nominal><Name>Dollar</Name><Value>78,90</Value></Valute>
          <Valute ID="R01239"><NumCode>978</NumCode><CharCode>EUR</CharCode><Nominal>1</Nominal><Name>Euro</Name><Value>92,30</Value></Valute>
          <Valute ID="R01335"><NumCode>398</NumCode><CharCode>KZT</CharCode><Nominal>100</Nominal><Name>Tenge</Name><Value>16,24</Value></Valute>
        </ValCurs>
        """;

    [Fact]
    public void Nbk_normalizes_kzt_table_to_usd_base()
    {
        RateTable? t = RatesService.ParseNbk(NbkXml);

        Assert.NotNull(t);
        Assert.Equal("usd", t!.Base, ignoreCase: true);
        Assert.Equal(new DateOnly(2026, 6, 26), t.Date);
        Assert.Equal(1m, Rate(t, "USD"));
        Assert.Equal(485.4m, Rate(t, "KZT"));                 // 1 USD = 485.4 KZT
        Assert.Equal(485.4m / 550.69m, Rate(t, "EUR"), 8);    // via KZT
        Assert.Equal(485.4m / 6.41m, Rate(t, "RUB"), 8);
        Assert.Equal(485.4m / (13.23m / 10m), Rate(t, "AMD"), 8); // quant=10 honoured
    }

    [Fact]
    public void Cbr_normalizes_rub_table_to_usd_base()
    {
        RateTable? t = RatesService.ParseCbr(CbrXml);

        Assert.NotNull(t);
        Assert.Equal("usd", t!.Base, ignoreCase: true);
        Assert.Equal(new DateOnly(2026, 6, 26), t.Date);
        Assert.Equal(1m, Rate(t, "USD"));
        Assert.Equal(78.90m, Rate(t, "RUB"));                  // 1 USD = 78.90 RUB
        Assert.Equal(78.90m / 92.30m, Rate(t, "EUR"), 8);
        Assert.Equal(78.90m / (16.24m / 100m), Rate(t, "KZT"), 8); // Nominal=100 honoured
    }

    [Fact]
    public void Nbk_without_usd_anchor_returns_null()
    {
        const string noUsd = """
            <rss><channel>
              <item><title>EUR</title><pubDate>26.06.2026</pubDate><description>550.69</description><quant>1</quant></item>
            </channel></rss>
            """;

        Assert.Null(RatesService.ParseNbk(noUsd));
    }

    [Fact]
    public void Garbage_returns_null_not_throws()
    {
        Assert.Null(RatesService.ParseNbk("not xml at all"));
        Assert.Null(RatesService.ParseCbr("<unrelated/>"));
    }
}
