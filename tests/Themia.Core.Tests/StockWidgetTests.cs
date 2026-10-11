using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1307]..[S-1311] The stock widget: search, extended hours, exchange time, markers, facts and the ladder.</summary>
public sealed class StockWidgetTests
{
    // ---- [S-1307] search ---------------------------------------------------------------------

    [Fact]
    public void S1307_OnlyChartableKindsSurvive_NamedByShortThenLongName()
    {
        var hits = MarketSearch.Listed(
        [
            new ListedCandidate("MSFT", "Microsoft Corporation", null, "EQUITY", "NASDAQ"),
            new ListedCandidate("MSFT250117C00400000", "MSFT Jan 2025 400 Call", null, "OPTION", "OPR"),
            new ListedCandidate("^GSPC", null, "S&P 500", "INDEX", "SNP"),
            new ListedCandidate("EURUSD=X", "EUR/USD", null, "CURRENCY", "CCY"),
            new ListedCandidate("bad symbol!", "Nope", null, "EQUITY", null),
        ]);

        Assert.Equal(["MSFT", "^GSPC", "EURUSD=X"], hits.Select(h => h.Symbol));
        Assert.Equal("S&P 500", hits[1].Name);
        Assert.Equal(AssetClass.Index, hits[1].Asset);
        Assert.Equal(AssetClass.Currency, hits[2].Asset);
        Assert.All(hits, h => Assert.Equal(QuoteMarket.Listed, h.Market));
    }

    [Fact]
    public void S1307_CoinsAreNamedWithTheirTicker()
    {
        var hit = Assert.Single(MarketSearch.Coins([new CoinCandidate("bitcoin", "Bitcoin", "btc")]));

        Assert.Equal("bitcoin", hit.Symbol);
        Assert.Equal("Bitcoin (BTC)", hit.Name);
        Assert.Equal(QuoteMarket.Crypto, hit.Market);
    }

    [Fact]
    public void S1307_ResultsAlternateAndStopAtTwelve()
    {
        var listed = Enumerable.Range(0, 8).Select(i => new SymbolHit($"S{i}", "s", QuoteMarket.Listed, AssetClass.Equity, "")).ToList();
        var coins = Enumerable.Range(0, 8).Select(i => new SymbolHit($"c{i}", "c", QuoteMarket.Crypto, AssetClass.Crypto, "")).ToList();

        var merged = MarketSearch.Merge(listed, coins);

        Assert.Equal(MarketSearch.MaxHits, merged.Count);
        Assert.Equal(["S0", "c0", "S1", "c1"], merged.Take(4).Select(h => h.Symbol));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("apple", true)]
    public void S1307_EmptyQueriesAreNotSent(string query, bool expected)
        => Assert.Equal(expected, MarketSearch.IsQuery(query));

    // ---- [S-1308] extended hours ---------------------------------------------------------------

    private static readonly SessionWindow Pre = new(1000, 2000);
    private static readonly SessionWindow Regular = new(2000, 5000);
    private static readonly SessionWindow Post = new(5000, 6000);

    private static IReadOnlyList<QuoteBar> Day() =>
    [
        new(1100, 99), new(1900, 101),
        new(2000, 100), new(4900, 104),
        new(5100, 103), new(5900, 102),
    ];

    [Fact]
    public void S1308_TheLastBarInEachExtendedWindowIsItsPrint()
    {
        var prints = MarketQuote.ExtendedQuotesOf(Day(), new TradingSessions(Pre, Regular, Post), regularClose: 104);

        Assert.Equal(101, prints.Pre!.Value.Price);
        Assert.Equal(102, prints.Post!.Value.Price);
        Assert.Equal((102 - 104) / 104.0 * 100, prints.Post!.Value.ChangePercent, 6);
        Assert.Equal(5900, prints.Post!.Value.At);
    }

    [Fact]
    public void S1308_NoCloseOrNoWindowMeansNoPrint()
    {
        Assert.Null(MarketQuote.ExtendedQuoteIn(Day(), Post, regularClose: null));
        Assert.Null(MarketQuote.ExtendedQuoteIn(Day(), Post, regularClose: 0));
        Assert.Null(MarketQuote.ExtendedQuoteIn(Day(), null, regularClose: 100));
        Assert.Null(MarketQuote.ExtendedQuoteIn([new(2500, 100)], Post, regularClose: 100));
    }

    [Theory]
    [InlineData(MarketPhase.Pre, 101.0)]
    [InlineData(MarketPhase.Post, 102.0)]
    [InlineData(MarketPhase.Closed, 102.0)]
    [InlineData(MarketPhase.Open, null)]
    public void S1308_ThePhaseChoosesWhichPrintShows(MarketPhase phase, double? expected)
    {
        var prints = MarketQuote.ExtendedQuotesOf(Day(), new TradingSessions(Pre, Regular, Post), 104);

        Assert.Equal(expected, MarketQuote.CurrentExtended(prints, phase)?.Price);
    }

    [Fact]
    public void S1308_TheSeriesSplitsIntoRegularAndExtendedRuns()
    {
        var segments = MarketQuote.Segments(Day(), new TradingSessions(Pre, Regular, Post));

        Assert.Equal([(0, 2, true), (2, 4, false), (4, 6, true)], segments);
    }

    [Fact]
    public void S1308_WithoutAScheduleTheWholeSeriesIsOneRegularRun()
        => Assert.Equal([(0, 6, false)], MarketQuote.Segments(Day(), TradingSessions.None));

    [Fact]
    public void S1308_ExtendedBarsAreAskedForOnlyWhereTheyExist()
    {
        Assert.True(new StocksConfig { ShowExtendedHours = true, Range = QuoteRange.FiveDays }.WantsPrePost);
        Assert.False(new StocksConfig { ShowExtendedHours = true, Range = QuoteRange.Month }.WantsPrePost);
        Assert.False(new StocksConfig { ShowExtendedHours = true, Market = QuoteMarket.Crypto }.WantsPrePost);
        Assert.False(new StocksConfig { Range = QuoteRange.Day }.WantsPrePost);
    }

    // ---- [S-1309] exchange time --------------------------------------------------------------

    [Fact]
    public void S1309_IntradayLabelsAreTheExchangesClock()
    {
        // 13:30 UTC is 09:30 in New York (EDT, UTC-4), whatever the reader's own zone.
        var open = new DateTimeOffset(2026, 10, 2, 13, 30, 0, TimeSpan.Zero).ToUnixTimeSeconds();

        Assert.Equal("9:30 AM", ExchangeTime.Label(Locale.En, open, TimeSpan.FromHours(-4), QuoteRange.Day, TimeSpan.FromHours(2)));
        Assert.Equal("3:30 PM", ExchangeTime.Label(Locale.En, open, null, QuoteRange.Day, TimeSpan.FromHours(2)));
        Assert.Equal("09:30", ExchangeTime.Label(Locale.De, open, TimeSpan.FromHours(-4), QuoteRange.Day, TimeSpan.FromHours(2)));
    }

    [Fact]
    public void S1309_LongerRangesAreDated_WithTheYearFromTwoYears()
    {
        var at = new DateTimeOffset(2025, 3, 4, 20, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

        Assert.Equal("Mar 4", ExchangeTime.Label(Locale.En, at, TimeSpan.FromHours(-5), QuoteRange.Year, TimeSpan.Zero));
        Assert.Equal("Mar 4, 2025", ExchangeTime.Label(Locale.En, at, TimeSpan.FromHours(-5), QuoteRange.FiveYears, TimeSpan.Zero));
        Assert.Equal("Tue 3:00 PM", ExchangeTime.Label(Locale.En, at, TimeSpan.FromHours(-5), QuoteRange.FiveDays, TimeSpan.Zero));
        Assert.Equal("4. März", ExchangeTime.Label(Locale.De, at, TimeSpan.FromHours(-5), QuoteRange.Year, TimeSpan.Zero));
        Assert.Equal("Di. 15:00", ExchangeTime.Label(Locale.De, at, TimeSpan.FromHours(-5), QuoteRange.FiveDays, TimeSpan.Zero));
    }

    [Fact]
    public void S1309_LabelsAreStartAndEnd_PlusTheMiddleOnAWideChart()
    {
        IReadOnlyList<QuoteBar> bars = [new(0, 1), new(3600, 2), new(7200, 3)];

        Assert.Equal(["00:00", "02:00"], ExchangeTime.Labels(Locale.De, bars, TimeSpan.Zero, QuoteRange.Day, TimeSpan.Zero, withMiddle: false));
        Assert.Equal(["00:00", "01:00", "02:00"], ExchangeTime.Labels(Locale.De, bars, TimeSpan.Zero, QuoteRange.Day, TimeSpan.Zero, withMiddle: true));
        Assert.Empty(ExchangeTime.Labels(Locale.De, [new(0, 1)], null, QuoteRange.Day, TimeSpan.Zero, withMiddle: true));
    }

    [Theory]
    [InlineData(-14400L, true)]
    [InlineData(19800L, true)]
    [InlineData(-14401L, false)]
    [InlineData(60L * 60 * 15, false)]
    public void S1309_AnOffsetIsWholeMinutesWithinFourteenHours(long seconds, bool usable)
        => Assert.Equal(usable, ExchangeTime.Offset(seconds) is not null);

    [Theory]
    [InlineData("EDT", "EDT")]
    [InlineData("GMT+1", "GMT+1")]
    [InlineData("<b>x", "")]
    [InlineData("TOOLONGZONE", "")]
    [InlineData(null, "")]
    public void S1309_AZoneNameIsShortAndPlain(string? name, string expected)
        => Assert.Equal(expected, ExchangeTime.Zone(name));

    // ---- [S-1310] markers and colours ----------------------------------------------------------

    [Fact]
    public void S1310_ExtremesAreTheFirstLowestAndHighest()
    {
        IReadOnlyList<QuoteBar> bars = [new(1, 5), new(2, 3), new(3, 9), new(4, 3), new(5, 9)];

        Assert.Equal((1, 2), MarketQuote.Extremes(bars));
        Assert.Null(MarketQuote.Extremes([]));
    }

    [Fact]
    public void S1310_AHandEditedColourThatIsNotAColourGoesBackToTheTheme()
    {
        var config = new StocksConfig { UpColor = "#22aa44", DownColor = "red" };

        var repairs = config.Repair();

        Assert.Equal("#22AA44", config.UpColor);
        Assert.Equal(string.Empty, config.DownColor);
        Assert.Single(repairs);
    }

    // ---- [S-1311] the ladder -------------------------------------------------------------------

    private static StockPlan Plan(double width, double height, bool chart = true)
        => StockLayout.Plan(new WidgetArea(width, height), new StockInputs(6, 10, chart, false, true));

    [Fact]
    public void S1311_TheSmallestTileKeepsThePriceAndChange_AndSheds()
    {
        var plan = Plan(120, 76);

        Assert.False(plan.Currency);
        Assert.False(plan.Chart);
    }

    [Fact]
    public void S1311_AStripPutsTheChartBesideTheReadout()
    {
        var plan = Plan(480, 116);

        Assert.True(plan.Beside);
        Assert.True(plan.Chart);
        Assert.True(plan.ChartHeight >= 76);
    }

    [Fact]
    public void S1311_ALargeTileGrowsTheChart_NotJustThePrice()
    {
        var plan = Plan(560, 376);

        Assert.True(plan.ChartHeight > 120, $"chart {plan.ChartHeight}");
        Assert.True(plan.Labels && plan.MiddleLabel && plan.Axis && plan.MinMax);
        Assert.True(plan.Price.FontSize <= FluentType.TitleLarge.FontSize);
    }

    /// <summary>
    /// [S-2770] No facts and no range chips at any size: what the readout does not use is the
    /// chart's, so the parts add up to the tile (less the gaps), from the tall column to the largest.
    /// </summary>
    [Theory]
    [InlineData(200, 356)]
    [InlineData(320, 196)]
    [InlineData(560, 376)]
    [InlineData(800, 516)]
    public void S2770_TheChartTakesEverythingTheReadoutLeaves(double width, double height)
    {
        var plan = Plan(width, height);

        var used = plan.Price.LineHeight + (plan.ExtrasUnder ? plan.Change.LineHeight : 0)
            + plan.ChartHeight + (plan.Labels ? FluentType.Caption.LineHeight : 0);

        Assert.True(plan.Chart);
        Assert.True(used >= height - (4 * StockLayout.Gap) - 1, $"{width}x{height}: {used} of {height}");
    }

    [Fact]
    public void S1311_WithoutAChartThePriceTakesTheTile()
    {
        var plan = Plan(560, 376, chart: false);

        Assert.False(plan.Chart);
        Assert.True(plan.Price.FontSize >= FluentType.TitleLarge.FontSize);
    }

    [Theory]
    [InlineData(120, 76)]
    [InlineData(200, 106)]
    [InlineData(320, 196)]
    [InlineData(480, 116)]
    [InlineData(200, 356)]
    [InlineData(560, 376)]
    [InlineData(800, 516)]
    public void S1311_NothingOverflowsTheTile(double width, double height)
    {
        var plan = Plan(width, height);

        var used = plan.Beside
            ? Math.Max(plan.Price.LineHeight + plan.Change.LineHeight, plan.ChartHeight)
            : plan.Price.LineHeight + (plan.ExtrasUnder ? plan.Change.LineHeight : 0)
                + (plan.Chart ? plan.ChartHeight : 0)
                + (plan.Labels ? FluentType.Caption.LineHeight : 0);

        Assert.True(used <= height + 1, $"{width}x{height}: {used}");
    }

    // ---- [S-1310] markers against the price axis ---------------------------------------------

    private const double ChartWidth = 560;
    private const double ChartHeight = 220;
    private static readonly double Pad = FluentType.Caption.LineHeight;
    private static readonly double Usable = ChartHeight - (2 * Pad);
    private static readonly string[] AxisPrices = ["412.30", "410.15", "408.00"];

    [Fact]
    public void S1310_AHighMarkerNearTheEndHidesTheAxisTopLabel_NotDrawnOverIt()
    {
        // The period's high is the last bar: its price sits at the top right, where the axis's top is.
        var plan = StockChartLabels.Place(ChartWidth, ChartHeight, Pad, Usable,
            [new ChartMarker(ChartWidth, Pad, "412.30", Above: true), new ChartMarker(ChartWidth * 0.3, Pad + Usable, "408.00", Above: false)],
            AxisPrices);

        Assert.Null(plan.Axis[0]);
        Assert.NotNull(plan.Axis[1]);
        Assert.NotNull(plan.Axis[2]);
        foreach (var label in plan.Axis.OfType<LabelBox>())
        {
            Assert.All(plan.Markers, marker => Assert.False(marker.Collides(label, StockChartLabels.Clearance)));
        }
    }

    [Fact]
    public void S1310_ALowMarkerNearTheEndHidesTheAxisBottomLabel()
    {
        var plan = StockChartLabels.Place(ChartWidth, ChartHeight, Pad, Usable,
            [new ChartMarker(ChartWidth * 0.1, Pad, "412.30", Above: true), new ChartMarker(ChartWidth - 20, Pad + Usable, "408.00", Above: false)],
            AxisPrices);

        Assert.NotNull(plan.Axis[0]);
        Assert.Null(plan.Axis[2]);
    }

    [Fact]
    public void S1310_MarkersAwayFromTheAxisLeaveEveryAxisLabel()
    {
        var plan = StockChartLabels.Place(ChartWidth, ChartHeight, Pad, Usable,
            [new ChartMarker(40, Pad, "412.30", Above: true), new ChartMarker(120, Pad + Usable, "408.00", Above: false)],
            AxisPrices);

        Assert.All(plan.Axis, label => Assert.NotNull(label));
        Assert.Equal(2, plan.Markers.Count);
    }

    [Fact]
    public void S1311_WithoutMarkersTheAxisIsWhole_AndInsideTheChart()
    {
        var plan = StockChartLabels.Place(ChartWidth, ChartHeight, Pad, Usable, [], AxisPrices);

        Assert.All(plan.Axis, label =>
        {
            Assert.NotNull(label);
            Assert.True(label.Value.Top >= 0 && label.Value.Bottom <= ChartHeight);
            Assert.True(label.Value.Right <= ChartWidth - StockLayout.Inset + 0.001);
        });
    }
}
