using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.ExchangeRate.Tests;

/// <summary>
/// [S-5143] [S-5144] [S-5146] [S-11151] The exchange rate: its pair's defaults and repair, its layout and size.
/// </summary>
public sealed class ExchangeRateTests
{
    [Fact]
    public void S5143_an_empty_base_follows_the_region_and_the_quote_defaults_to_the_euro_else_the_dollar()
    {
        var before = RegionCurrency.Country;
        try
        {
            RegionCurrency.Country = "CH";
            var config = new ExchangeRateConfig();

            Assert.Equal("CHF", config.EffectiveBase);
            Assert.Equal("EUR", config.EffectiveQuote);

            // A euro base: the euro is no quote of itself, so the dollar.
            RegionCurrency.Country = "DE";
            Assert.Equal("EUR", config.EffectiveBase);
            Assert.Equal("USD", config.EffectiveQuote);

            config.QuoteCurrency = "GBP";
            Assert.Equal("GBP", config.EffectiveQuote);

            // The base is never its own quote: the default for that base instead.
            config.BaseCurrency = "GBP";
            Assert.Equal("EUR", config.EffectiveQuote);
        }
        finally
        {
            RegionCurrency.Country = before;
        }
    }

    [Fact]
    public void S5144_repair_drops_unpublished_codes_loudly()
    {
        var config = new ExchangeRateConfig { BaseCurrency = "xyz", QuoteCurrency = "RUB" };
        var repairs = config.Repair();

        Assert.Equal(string.Empty, config.BaseCurrency);
        Assert.Equal(string.Empty, config.QuoteCurrency);
        Assert.Equal(2, repairs.Count);

        var cased = new ExchangeRateConfig { BaseCurrency = " chf ", QuoteCurrency = "usd" };
        Assert.Empty(cased.Repair());
        Assert.Equal(("CHF", "USD"), (cased.BaseCurrency, cased.QuoteCurrency));

        Assert.Empty(new ExchangeRateConfig().Repair());
    }

    [Fact]
    public void S5144_a_pair_round_trips_through_the_file()
    {
        var config = (ExchangeRateConfig)ConfigJson.Read("exchangeRate", """
            { "kind": "exchangeRate", "baseCurrency": "CHF", "quoteCurrency": "JPY" }
            """);

        Assert.Equal(("CHF", "JPY"), (config.BaseCurrency, config.QuoteCurrency));
        Assert.Equal("JPY", config.EffectiveQuote);
    }

    [Fact]
    public void S11151_the_rate_is_the_28_readout_and_its_line_under_at_one_height_160_to_320_wide()
    {
        var sizing = RatesLayout.Sizing();

        Assert.Equal(36 + 36 + 16 + 12, sizing.Natural.Height);
        Assert.True(sizing.FixedHeight);
        Assert.Equal((160.0, 200.0, 320.0), (sizing.Min.Width, sizing.Natural.Width, sizing.Max.Width));
    }

    [Fact]
    public void S5146_a_narrow_card_folds_the_change_under_the_rate_and_a_wide_one_keeps_it_beside()
    {
        var rate = new ReadoutText("1.0675", " EUR");
        Assert.False(RatesLayout.Fold(rate, 160 - 24).DeltaBeside);
        Assert.True(RatesLayout.Fold(rate, 320 - 24) is { DeltaBeside: true, Unit: true });
    }
}
