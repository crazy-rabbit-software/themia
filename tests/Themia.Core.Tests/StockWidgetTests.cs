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
    public void S1309_LabelsAreTheStartAndTheEndOnly()
    {
        IReadOnlyList<QuoteBar> bars = [new(0, 1), new(3600, 2), new(7200, 3)];

        Assert.Equal(["00:00", "02:00"], ExchangeTime.Labels(Locale.De, bars, TimeSpan.Zero, QuoteRange.Day, TimeSpan.Zero));
        Assert.Empty(ExchangeTime.Labels(Locale.De, [new(0, 1)], null, QuoteRange.Day, TimeSpan.Zero));
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

    // ---- [S-2775] one baseline for the colour, the change and the line ---------------------------

    /// <summary>
    /// [S-2775] Regression (AAPL red over a rising line): the change was measured from the previous
    /// close while the line started at the first bar of the day. A day that opened below the close
    /// and climbed back, but not all the way, drew a rising line in the falling colour. The line now
    /// starts at the same baseline the change is measured from.
    /// </summary>
    [Fact]
    public void S2775_AGapDownDayThatClimbsBackDrawsFromThePreviousClose()
    {
        IReadOnlyList<QuoteBar> bars = [new(1000, 226.0), new(1300, 227.4), new(1600, 228.9), new(1900, 229.5)];
        var baseline = MarketQuote.ListedBaseline(QuoteRange.Day, chartPreviousClose: 230.0, previousClose: 230.0);

        var trace = MarketQuote.Trace(bars, price: 229.5, baseline);

        Assert.True(trace.ChangePercent < 0);
        Assert.Equal(230.0, trace.Bars[0].Close);
        Assert.Equal(Math.Sign(trace.ChangePercent!.Value), Math.Sign(trace.Bars[^1].Close - trace.Bars[0].Close));
        Assert.Equal(bars.Count + 1, trace.Bars.Count);
    }

    [Fact]
    public void S2775_WithoutABaselineTheLinesFirstPointIsIt()
    {
        IReadOnlyList<QuoteBar> bars = [new(1000, 100.0), new(2000, 110.0)];

        var trace = MarketQuote.Trace(bars, price: 110.0, baseline: null);

        Assert.Equal(10.0, trace.ChangePercent!.Value, 6);
        Assert.Equal(bars, trace.Bars);
    }

    [Fact]
    public void S2775_ThePreviousDaysCloseIsTheBaselineOnlyForOneDay()
    {
        Assert.Equal(230.0, MarketQuote.ListedBaseline(QuoteRange.Day, null, 230.0));
        Assert.Null(MarketQuote.ListedBaseline(QuoteRange.Year, null, 230.0));
        Assert.Equal(180.0, MarketQuote.ListedBaseline(QuoteRange.Year, 180.0, 230.0));
        Assert.Null(MarketQuote.ListedBaseline(QuoteRange.Day, double.NaN, null));
    }

    [Fact]
    public void S2775_ABaselineEqualToTheFirstBarAddsNoPoint()
    {
        IReadOnlyList<QuoteBar> bars = [new(1000, 100.0), new(2000, 95.0)];

        var trace = MarketQuote.Trace(bars, price: 95.0, baseline: 100.0);

        Assert.Equal(bars, trace.Bars);
        Assert.Equal(-5.0, trace.ChangePercent!.Value, 6);
    }

    [Fact]
    public void S2775_ACoinsChangeIsOverItsChart_NotAlwaysTheLast24Hours()
    {
        IReadOnlyList<QuoteBar> fiveDays = [new(1000, 100.0), new(2000, 104.0), new(3000, 110.0)];

        Assert.Equal(10.0, MarketQuote.CoinTrace(QuoteRange.FiveDays, fiveDays, 110.0, change24H: -2.0).ChangePercent!.Value, 6);
        Assert.Equal(-2.0, MarketQuote.CoinTrace(QuoteRange.Day, [], 110.0, change24H: -2.0).ChangePercent);
        Assert.Null(MarketQuote.CoinTrace(QuoteRange.FiveDays, [], 110.0, change24H: -2.0).ChangePercent);
    }

    [Fact]
    public void S2775_NoBarsAndNoBaselineIsNoChange()
        => Assert.Null(MarketQuote.Trace([], price: 100.0, baseline: null).ChangePercent);
}
