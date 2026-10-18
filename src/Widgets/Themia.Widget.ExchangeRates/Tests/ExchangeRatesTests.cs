using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Xunit;
using static Themia.Core.Tests.StyleTiers;

namespace Themia.Widget.ExchangeRates.Tests;

/// <summary>
/// [S-5143] [S-5144] [S-5146] [S-6168] The exchange-rate widget: its pair's defaults and repair, and
/// both styles' ladders. Moved with the widget from Core's OnlineWidgetsTests and WeatherStylesTests,
/// unchanged in what they assert.
/// </summary>
public sealed class ExchangeRatesTests
{
    [Fact]
    public void S5143_an_empty_base_follows_the_region_and_the_quote_defaults_to_the_euro_else_the_dollar()
    {
        var before = RegionCurrency.Country;
        try
        {
            RegionCurrency.Country = "CH";
            var config = new ExchangeRatesConfig();

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
        var config = new ExchangeRatesConfig { BaseCurrency = "xyz", QuoteCurrency = "RUB" };
        var repairs = config.Repair();

        Assert.Equal(string.Empty, config.BaseCurrency);
        Assert.Equal(string.Empty, config.QuoteCurrency);
        Assert.Equal(2, repairs.Count);

        var cased = new ExchangeRatesConfig { BaseCurrency = " chf ", QuoteCurrency = "usd" };
        Assert.Empty(cased.Repair());
        Assert.Equal(("CHF", "USD"), (cased.BaseCurrency, cased.QuoteCurrency));

        Assert.Empty(new ExchangeRatesConfig().Repair());
    }

    [Fact]
    public void S5144_a_pair_round_trips_through_the_file()
    {
        var config = (ExchangeRatesConfig)ConfigJson.Read("exchangeRates", """
            { "kind": "exchangeRates", "baseCurrency": "CHF", "quoteCurrency": "JPY", "style": "chart" }
            """);

        Assert.Equal(("CHF", "JPY", ExchangeRatesStyle.Chart), (config.BaseCurrency, config.QuoteCurrency, config.Style));
        Assert.Equal("JPY", config.EffectiveQuote);
    }

    [Fact]
    public void S5146_the_number_style_is_the_rate_alone_on_the_whole_tile()
    {
        var floor = FluentType.Subtitle.LineHeight + FluentType.Caption.LineHeight;

        foreach (var area in new[] { new WidgetArea(160, 120), new WidgetArea(440, 116), new WidgetArea(420, 420) })
        {
            var plan = RatesLayout.Rates(area, ExchangeRatesStyle.Number, floor);
            Assert.Equal(area.Height - (2 * OnlineLayouts.VerticalInset), plan.HeroRoom);
            Assert.Equal(0, plan.ChartHeight);
            Assert.False(plan.Beside);
        }
    }

    private static RatesPlan RatesChart(WidgetArea area)
        => RatesLayout.Rates(area, ExchangeRatesStyle.Chart, FluentType.Subtitle.LineHeight + FluentType.Caption.LineHeight);

    [Fact]
    public void S6168_the_chart_style_is_the_rate_over_its_chart()
    {
        foreach (var tier in new[] { M, Tall, L, Xl })
        {
            var plan = RatesChart(tier);
            Assert.False(plan.Beside);
            Assert.True(plan.ChartHeight >= RatesLayout.HeroChartMin, $"{tier}: chart {plan.ChartHeight}");
        }
    }

    [Fact]
    public void S6168_a_strip_puts_the_chart_beside_the_hero_with_the_strip_s_height()
    {
        var plan = RatesChart(Wide);

        Assert.True(plan.Beside);
        Assert.True(plan.HeroWidth >= 150);
        Assert.True(plan.ChartHeight >= Wide.Height);
    }

    [Fact]
    public void S6168_a_tile_too_small_for_the_chart_is_the_hero_alone()
    {
        var plan = RatesChart(Xs);

        Assert.Equal(0, plan.ChartHeight);
        Assert.False(plan.Beside);
    }
}
