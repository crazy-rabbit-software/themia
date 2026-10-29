using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;
using static Themia.Core.Tests.StyleTiers;

namespace Themia.Widget.ExchangeRates.Tests;

/// <summary>
/// [S-5143] [S-5144] [S-5146] [S-6168] The exchange-rate widget: its pair's defaults and repair, and
/// both styles' layouts on the family's blocks.
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

    private static readonly ReadoutText Rate = new("1.0675", " EUR");

    private static RatesPlan Plan(WidgetArea area, ExchangeRatesStyle style) => RatesLayout.Rates(area, style, Rate, "+88.88%");

    [Fact]
    public void S5146_the_rate_climbs_28_40_68_with_the_area_and_stops()
    {
        Assert.Equal(FamilyType.Big28, Plan(Xs, ExchangeRatesStyle.Number).Rate);
        Assert.Equal(FamilyType.Big40, Plan(M, ExchangeRatesStyle.Number).Rate);
        Assert.Equal(FamilyType.Big68, Plan(Xl, ExchangeRatesStyle.Number).Rate);
        Assert.Equal(FamilyType.Big68, Plan(new WidgetArea(1600, 1200), ExchangeRatesStyle.Number).Rate);
    }

    [Fact]
    public void S5146_the_number_style_has_no_chart_at_any_size()
        => Assert.All(Tiers, tier => Assert.False(Plan(tier, ExchangeRatesStyle.Number).Chart));

    [Fact]
    public void S5146_a_narrow_tile_folds_the_change_under_the_rate_and_a_wide_one_keeps_it_beside()
    {
        Assert.False(Plan(Xs, ExchangeRatesStyle.Number).Fold.DeltaBeside);
        Assert.True(Plan(M, ExchangeRatesStyle.Number).Fold is { DeltaBeside: true, Unit: true });
    }

    [Fact]
    public void S6168_the_chart_runs_8_under_the_rate_down_to_the_cards_foot()
    {
        foreach (var tier in new[] { S, M, Wide, Tall, L, Xl })
        {
            var plan = Plan(tier, ExchangeRatesStyle.Chart);
            Assert.True(plan.Chart, $"{tier}");
            var under = plan.Under ? FamilyType.Under(plan.Rate).LineHeight : 0;
            Assert.Equal(plan.Rate.LineHeight + under + FamilySpace.S2, plan.ChartTop);
            Assert.Equal(tier.Height, plan.ChartTop + plan.ChartHeight, 6);
        }
    }

    [Fact]
    public void S6168_a_tile_too_short_for_the_chart_is_the_rate_alone()
        => Assert.False(Plan(Xs, ExchangeRatesStyle.Chart).Chart);
}
