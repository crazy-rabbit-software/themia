using System.Text.Json;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2776] [S-2777] "Shares owned": what a typed amount is kept as, what it is worth, and where it shows.</summary>
public sealed class StockHoldingTests
{
    // ---- [S-2776] the field ------------------------------------------------------------------

    [Theory]
    [InlineData("10", 10.0)]
    [InlineData(" 12.5 ", 12.5)]
    [InlineData("0.5", 0.5)]
    [InlineData(".25", 0.25)]
    [InlineData("3.", 3.0)]
    [InlineData("0.123456789", 0.12345679)]
    public void S2776_an_amount_is_read_with_a_point_in_english(string typed, double expected)
    {
        Assert.True(StockHolding.TryParse(Locale.En, typed, out var amount));
        Assert.Equal(expected, amount!.Value, 9);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("0.000")]
    public void S2776_empty_or_zero_is_no_holding(string? typed)
    {
        Assert.True(StockHolding.TryParse(Locale.En, typed, out var amount));
        Assert.Null(amount);
    }

    [Theory]
    [InlineData("-5")]
    [InlineData("+5")]
    [InlineData("1,000")]
    [InlineData("1.2.3")]
    [InlineData("ten")]
    [InlineData("5 shares")]
    [InlineData(".")]
    [InlineData("1e3")]
    [InlineData("NaN")]
    [InlineData("10000000000000")]
    public void S2776_anything_else_is_refused_not_coerced(string typed)
        => Assert.False(StockHolding.TryParse(Locale.En, typed, out _));

    [Fact]
    public void S2776_german_reads_a_comma_and_refuses_the_point_that_is_its_grouping()
    {
        Assert.True(StockHolding.TryParse(Locale.De, "12,5", out var amount));
        Assert.Equal(12.5, amount);

        // "1.000" is a thousand in German: refused rather than guessed as one.
        Assert.False(StockHolding.TryParse(Locale.De, "1.000", out _));
    }

    [Fact]
    public void S2776_a_language_whose_grouping_is_not_a_point_still_takes_one()
    {
        Assert.True(StockHolding.TryParse(Locale.Fr, "12,5", out var comma));
        Assert.True(StockHolding.TryParse(Locale.Fr, "12.5", out var point));
        Assert.Equal(12.5, comma);
        Assert.Equal(12.5, point);
    }

    [Fact]
    public void S2776_the_field_writes_the_amount_back_in_the_language_s_decimals()
    {
        Assert.Equal("12.5", StockHolding.Amount(Locale.En, 12.5));
        Assert.Equal("12,5", StockHolding.Amount(Locale.De, 12.5));
        Assert.Equal("1250", StockHolding.Amount(Locale.En, 1250));

        // Round trip: what the field shows reads back as the same amount.
        Assert.True(StockHolding.TryParse(Locale.De, StockHolding.Amount(Locale.De, 0.00012345), out var back));
        Assert.Equal(0.00012345, back!.Value, 9);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(1e13)]
    public void S2776_an_unusable_stored_amount_is_repaired_to_none_and_said(double stored)
    {
        var config = new StocksConfig { Symbol = "MSFT", SharesOwned = stored };

        var repairs = config.Repair();

        Assert.Null(config.SharesOwned);
        Assert.Single(repairs);
    }

    [Fact]
    public void S2776_a_stored_zero_is_none_without_a_complaint()
    {
        var config = new StocksConfig { SharesOwned = 0 };

        Assert.Empty(config.Repair());
        Assert.Null(config.SharesOwned);
    }

    [Fact]
    public void S2776_the_amount_survives_the_file_and_a_clone()
    {
        var config = new StocksConfig { Symbol = "MSFT", SharesOwned = 12.5 };

        Assert.Equal(12.5, ((StocksConfig)config.Clone()).SharesOwned);

        var json = JsonSerializer.Serialize<WidgetConfig>(config, AppStateJsonContext.Default.WidgetConfig);
        var back = Assert.IsType<StocksConfig>(JsonSerializer.Deserialize(json, AppStateJsonContext.Default.WidgetConfig));
        Assert.Equal(12.5, back.SharesOwned);
    }

    // ---- [S-2776] the sums -------------------------------------------------------------------

    [Fact]
    public void S2776_the_worth_is_the_amount_times_the_price_to_the_cent()
    {
        var worth = StockHolding.Worth(10, 420.27, "USD");

        Assert.Equal(new Money(4202.70, "USD"), worth);
        Assert.Equal("4,202.70 USD", StockHolding.Sum(Locale.En, worth!.Value));
        Assert.Equal("4.202,70 USD", StockHolding.Sum(Locale.De, worth.Value));
    }

    [Fact]
    public void S2776_a_price_in_pence_is_counted_in_pounds()
    {
        Assert.Equal(new Money(4123.40, "GBP"), StockHolding.Worth(1000, 412.34, "GBp"));
        Assert.Equal(new Money(41.23, "GBP"), StockHolding.Worth(10, 412.34, "GBX"));
        Assert.Equal("ZAR", StockHolding.Currency("ZAc").Code);
        Assert.Equal("ILS", StockHolding.Currency("ILA").Code);

        // Case matters: GBP is already pounds.
        Assert.Equal(new Money(4123.40, "GBP"), StockHolding.Worth(10, 412.34, "GBP"));
    }

    [Fact]
    public void S2776_no_holding_or_no_price_is_no_worth()
    {
        Assert.Null(StockHolding.Worth(null, 420, "USD"));
        Assert.Null(StockHolding.Worth(10, null, "USD"));
        Assert.Null(StockHolding.Worth(10, double.NaN, "USD"));
    }

    [Fact]
    public void S2776_the_move_is_against_the_change_s_own_baseline()
    {
        // 420 is +5% on a 400 baseline: 10 shares moved +200.
        Assert.Equal(new Money(200, "USD"), StockHolding.Change(10, 420, 5, "USD"));

        // A fall keeps its sign, as the percent does ([S-2775]).
        var fall = StockHolding.Change(10, 380, -5, "USD")!.Value;
        Assert.True(fall.Value < 0);
        Assert.Equal("-200.00", StockHolding.SignedSum(Locale.En, fall));
        Assert.Equal("+200.00", StockHolding.SignedSum(Locale.En, new Money(200, "USD")));
        Assert.Equal("0.00", StockHolding.SignedSum(Locale.En, new Money(0.001, "USD")));
    }

    [Fact]
    public void S2776_no_change_is_no_move()
    {
        Assert.Null(StockHolding.Change(10, 420, null, "USD"));
        Assert.Null(StockHolding.Change(10, 420, -100, "USD"));
        Assert.Null(StockHolding.Change(null, 420, 5, "USD"));
    }

    // ---- [S-2777] where it shows -------------------------------------------------------------

    [Fact]
    public void S2777_the_caption_is_the_worth_alone_when_narrow_and_names_the_amount_when_wide()
    {
        var worth = new Money(4202.70, "USD");

        Assert.Equal("4,202.70 USD", StockHolding.Caption(Locale.En, 10, worth, 200));
        Assert.Equal("10 held · 4,202.70 USD", StockHolding.Caption(Locale.En, 10, worth, 320));
        Assert.Equal("0.5 held · 4,202.70 USD", StockHolding.Caption(Locale.En, 0.5, worth, 320));
    }

    private static StockPlan Plan(double width, double height, bool holding)
        => StockLayout.Plan(new WidgetArea(width, height), new StockInputs(6, 10, Chart: true, Extended: false, MinMax: true, Holding: holding));

    [Fact]
    public void S2777_the_extra_small_tile_sheds_the_holding()
        => Assert.False(Plan(120, 76, holding: true).Holding);

    [Theory]
    [InlineData(200, 106)]
    [InlineData(320, 196)]
    [InlineData(200, 356)]
    [InlineData(560, 376)]
    [InlineData(800, 516)]
    [InlineData(480, 116)]
    public void S2777_every_tile_with_room_shows_it_and_the_chart_pays_for_it(double width, double height)
    {
        var with = Plan(width, height, holding: true);
        var without = Plan(width, height, holding: false);

        Assert.True(with.Holding, $"{width}x{height}");
        Assert.True(with.ChartHeight <= without.ChartHeight, $"{width}x{height}");
    }

    [Fact]
    public void S2777_no_holding_asks_no_line()
        => Assert.False(Plan(560, 376, holding: false).Holding);
}
