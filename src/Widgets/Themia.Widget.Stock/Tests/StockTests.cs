using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Stock.Tests;

/// <summary>
/// [S-1308] [S-1311] [S-11160] [S-11161] The price: what its config asks, its layout and its size.
/// </summary>
public sealed class StockTests
{
    private const double ChartWidth = 560;
    private const double ChartHeight = 220;

    [Fact]
    public void S1308_ExtendedBarsAreAskedForOnlyWhereTheyExist()
    {
        Assert.True(new StockConfig { ShowExtendedHours = true, Range = QuoteRange.FiveDays }.WantsPrePost);
        Assert.False(new StockConfig { ShowExtendedHours = true, Range = QuoteRange.Month }.WantsPrePost);
        Assert.False(new StockConfig { ShowExtendedHours = true, Market = QuoteMarket.Crypto }.WantsPrePost);
        Assert.False(new StockConfig { Range = QuoteRange.Day }.WantsPrePost);
    }

    [Fact]
    public void S11161_the_price_is_one_height_its_line_under_and_a_row_each_for_the_holding_and_the_extended_print()
    {
        WidgetSizing Of(StockConfig c) => StockLayout.Sizing(c);

        Assert.Equal(36 + 36 + 12, Of(new StockConfig()).Natural.Height);
        Assert.Equal(36 + 36 + 8 + 28 + 12, Of(new StockConfig { SharesOwned = 10 }).Natural.Height);
        Assert.Equal(36 + 36 + 8 + 56 + 12, Of(new StockConfig { SharesOwned = 10, ShowExtendedHours = true, Range = QuoteRange.Day }).Natural.Height);
        Assert.Equal(36 + 36 + 12, Of(new StockConfig { ShowExtendedHours = true, Range = QuoteRange.Year }).Natural.Height);
        Assert.True(Of(new StockConfig()).FixedHeight);
        Assert.Equal((240.0, 240.0, 360.0), (Of(new StockConfig()).Min.Width, Of(new StockConfig()).Natural.Width, Of(new StockConfig()).Max.Width));
    }

    [Fact]
    public void S1311_the_session_line_is_a_setting_off_by_default_never_a_spare_line()
    {
        WidgetSizing Of(StockConfig c) => StockLayout.Sizing(c);

        Assert.False(new StockConfig().ShowMarketState);
        Assert.Equal(36 + 36 + 16 + 12, Of(new StockConfig { ShowMarketState = true }).Natural.Height);
        Assert.Equal(36 + 36 + 12, Of(new StockConfig { ShowMarketState = true, Market = QuoteMarket.Crypto }).Natural.Height);
        Assert.True(((StockConfig)new StockConfig { ShowMarketState = true }.Clone()).ShowMarketState);
        Assert.Equal("Market open", MarketQuote.SessionLine(Themia.Core.Localization.Locale.En, MarketPhase.Open));
        Assert.Equal("Market closed", MarketQuote.SessionLine(Themia.Core.Localization.Locale.En, MarketPhase.Closed));
        Assert.Equal(string.Empty, MarketQuote.SessionLine(Themia.Core.Localization.Locale.En, MarketPhase.Unknown));

        // The narrowest card holds a price of eight figures and the widest change side by side.
        Assert.True(StockLayout.Fold(new ReadoutText("888,888.88"), StockLayout.Narrowest - 24).DeltaBeside);
    }

    [Fact]
    public void S1311_a_narrow_card_folds_the_change_under_the_price_and_a_wide_one_keeps_it_beside()
    {
        var price = new ReadoutText("420.27", " USD");
        Assert.False(StockLayout.Fold(price, 120).DeltaBeside);
        Assert.True(StockLayout.Fold(price, 360 - 24).DeltaBeside);
    }

    [Fact]
    public void S11160_the_price_and_its_chart_ask_one_quote_for_one_instrument()
    {
        var price = new StockConfig { Symbol = "msft", Range = QuoteRange.FiveDays, ShowExtendedHours = true };
        var copy = (StockConfig)price.Clone();
        Assert.Equal(price.Interest(), copy.Interest());
        Assert.Equal("MSFT", price.EffectiveSymbol);
    }
}
