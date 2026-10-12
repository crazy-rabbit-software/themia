using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// Reading a quote. Every number here arrived over the wire from a service with no contract, so the
/// cases that matter are the ones where a field is missing, zero, or the wrong shape entirely.
/// </summary>
public class MarketQuoteTests
{
    private static DateTimeOffset At(long epochSeconds) => DateTimeOffset.FromUnixTimeSeconds(epochSeconds);

    // ---- classification --------------------------------------------------------------------

    [Theory]
    [InlineData("AAPL", AssetClass.Equity)]
    [InlineData("^GSPC", AssetClass.Index)]
    [InlineData("EURUSD=X", AssetClass.Currency)]
    [InlineData("GC=F", AssetClass.Commodity)]
    public void A_persisted_symbol_is_classified_by_the_symbol_grammar(string symbol, AssetClass expected)
    {
        // A config entry carries only the symbol, so this is the fallback the app runs on after a
        // restart — and it has to agree with what the live search decided before it.
        Assert.Equal(expected, MarketQuote.Classify(symbol, QuoteMarket.Listed));
    }

    [Theory]
    [InlineData("ETF", AssetClass.Equity)]
    [InlineData("MUTUALFUND", AssetClass.Equity)]
    [InlineData("currency", AssetClass.Currency)]
    [InlineData("Future", AssetClass.Commodity)]
    [InlineData("INDEX", AssetClass.Index)]
    public void The_services_own_quote_type_wins_when_it_is_there(string quoteType, AssetClass expected)
        => Assert.Equal(expected, MarketQuote.Classify("XYZ", QuoteMarket.Listed, quoteType));

    [Fact]
    public void A_coin_id_is_crypto_whatever_it_looks_like()
    {
        // "bitcoin" has no Yahoo grammar to read, so the market selector outranks everything.
        Assert.Equal(AssetClass.Crypto, MarketQuote.Classify("bitcoin", QuoteMarket.Crypto));
        Assert.Equal(AssetClass.Crypto, MarketQuote.Classify("^weird=X", QuoteMarket.Crypto, "EQUITY"));
    }

    [Fact]
    public void An_unrecognised_quote_type_falls_through_to_the_grammar()
        => Assert.Equal(AssetClass.Index, MarketQuote.Classify("^DJI", QuoteMarket.Listed, "SOMETHING_NEW"));

    // ---- price precision -------------------------------------------------------------------

    [Theory]
    [InlineData(182.34, null, 2)]
    [InlineData(0.42, null, 4)]
    [InlineData(-0.42, null, 4)]
    public void Size_alone_decides_the_decimals_when_there_is_no_hint(double price, int? hint, int expected)
        => Assert.Equal(expected, MarketQuote.PriceDecimals(price, hint));

    [Fact]
    public void A_venue_hint_may_raise_the_precision()
    {
        // EUR/USD moves in the fourth decimal; printing 1.1567 as "1.16" throws away the signal.
        Assert.Equal(4, MarketQuote.PriceDecimals(1.1567, 4));
    }

    [Fact]
    public void A_venue_hint_may_never_lower_it()
    {
        // A sub-dollar instrument reported with a share's hint keeps the four places the magnitude
        // rule already gave it.
        Assert.Equal(4, MarketQuote.PriceDecimals(0.42, 2));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(9)]
    [InlineData(int.MaxValue)]
    public void A_hint_outside_the_plausible_range_is_ignored(int hint)
        => Assert.Equal(2, MarketQuote.PriceDecimals(182.34, hint));

    [Theory]
    [InlineData(1234.5, null, "1,234.50")]
    [InlineData(0.4242424, null, "0.4242")]
    [InlineData(1.1567, 4, "1.1567")]
    [InlineData(84470.0, null, "84,470.00")]
    public void A_price_is_grouped_and_printed_to_its_own_precision(double price, int? hint, string expected)
        => Assert.Equal(expected, MarketQuote.Price(Locale.En, price, hint));

    [Fact]
    public void S1106_a_price_uses_the_language_s_separators()
    {
        Assert.Equal("1.234,50", MarketQuote.Price(Locale.De, 1234.5));
        Assert.Equal("+1,24 %", MarketQuote.ChangePercent(Locale.De, 1.2356));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    public void A_missing_price_prints_and_never_throws(double? price)
        => Assert.Equal("--", MarketQuote.Price(Locale.En, price));

    // ---- change --------------------------------------------------------------------------

    [Theory]
    [InlineData(1.2356, "+1.24%")]
    [InlineData(-0.3, "-0.30%")]
    [InlineData(0.0, "0.00%")]
    public void A_move_carries_its_sign_in_the_text_and_not_only_in_the_colour(double percent, string expected)
    {
        // A state a reader can only get from a colour is a state some readers never get.
        Assert.Equal(expected, MarketQuote.ChangePercent(Locale.En, percent));
    }

    [Fact]
    public void A_percentage_change_needs_a_baseline_that_can_carry_one()
    {
        Assert.Null(MarketQuote.PercentChange(100, 0));
        Assert.Null(MarketQuote.PercentChange(100, null));
        Assert.Null(MarketQuote.PercentChange(null, 100));
        Assert.Null(MarketQuote.PercentChange(double.NaN, 100));
    }

    [Fact]
    public void A_percentage_change_is_measured_against_the_previous_close()
        => Assert.Equal(1.0, MarketQuote.PercentChange(101, 100)!.Value, 6);

    // ---- sessions --------------------------------------------------------------------------

    [Fact]
    public void A_window_needs_two_finite_bounds_in_order()
    {
        Assert.NotNull(MarketQuote.Window(1_790_256_600, 1_790_280_000));
        Assert.Null(MarketQuote.Window(1_790_280_000, 1_790_256_600));
        Assert.Null(MarketQuote.Window(1_790_256_600, 1_790_256_600));
        Assert.Null(MarketQuote.Window(null, 1_790_280_000));
        Assert.Null(MarketQuote.Window(double.NaN, 1_790_280_000));
    }

    [Fact]
    public void A_millisecond_timestamp_is_a_units_mistake_and_not_a_schedule()
    {
        // 1790256600000 is the year 58,000. Accepting it would put the "market closed" badge on a
        // widget forever.
        Assert.Null(MarketQuote.Window(1_790_256_600_000, 1_790_280_000_000));
    }

    [Fact]
    public void Inside_the_regular_window_the_market_is_open()
    {
        var sessions = Sessions();

        Assert.Equal(MarketPhase.Open, MarketQuote.Phase(sessions, At(1_790_260_000)));
    }

    [Fact]
    public void The_windows_are_half_open_so_two_of_them_never_claim_the_same_second()
    {
        var sessions = Sessions();

        // 1_790_256_600 is the end of pre and the start of regular.
        Assert.Equal(MarketPhase.Open, MarketQuote.Phase(sessions, At(1_790_256_600)));
        Assert.Equal(MarketPhase.Pre, MarketQuote.Phase(sessions, At(1_790_256_599)));
    }

    [Fact]
    public void Outside_every_window_but_with_a_known_schedule_the_market_is_closed()
        => Assert.Equal(MarketPhase.Closed, MarketQuote.Phase(Sessions(), At(1_790_300_000)));

    [Fact]
    public void Without_a_regular_window_the_phase_is_unknown_and_never_closed()
    {
        // The original app's bug: a payload that says nothing about the calendar was rendered as
        // "market closed", which put a permanent closed badge on every symbol, all day, including
        // mid-session.
        Assert.Equal(MarketPhase.Unknown, MarketQuote.Phase(TradingSessions.None, At(1_790_260_000)));
        Assert.Equal(MarketPhase.Unknown, MarketQuote.Phase(null, At(1_790_260_000)));
    }

    /// <summary>
    /// The original's issue #100 ("Market closed" on a US ticker while the market was open). The
    /// phase is a comparison of instants, so the reader's zone and the weeks where US and European
    /// daylight saving disagree (2026-03-09: New York on EDT, Zurich still on CET) cannot move it.
    /// </summary>
    [Theory]
    [InlineData(1)]   // Zurich, CET
    [InlineData(-4)]  // New York, EDT
    [InlineData(9)]   // Tokyo
    [InlineData(-10)] // Honolulu
    public void Issue100_the_phase_is_the_exchanges_whatever_the_readers_zone(int readerOffsetHours)
    {
        // 09:30-16:00 EDT on Monday 2026-03-09 is 13:30-20:00 UTC.
        var open = new DateTimeOffset(2026, 3, 9, 13, 30, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        var close = new DateTimeOffset(2026, 3, 9, 20, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        var sessions = new TradingSessions(MarketQuote.Window(open - 5.5 * 3600, open), MarketQuote.Window(open, close), MarketQuote.Window(close, close + 4 * 3600));
        var offset = TimeSpan.FromHours(readerOffsetHours);

        DateTimeOffset Reader(int hourUtc, int minute) => new DateTimeOffset(2026, 3, 9, hourUtc, minute, 0, TimeSpan.Zero).ToOffset(offset);

        Assert.Equal(MarketPhase.Open, MarketQuote.Phase(sessions, Reader(13, 45)));  // 09:45 EDT
        Assert.Equal(MarketPhase.Open, MarketQuote.Phase(sessions, Reader(19, 59)));  // 15:59 EDT
        Assert.Equal(MarketPhase.Pre, MarketQuote.Phase(sessions, Reader(13, 15)));   // 09:15 EDT
        Assert.Equal(MarketPhase.Post, MarketQuote.Phase(sessions, Reader(20, 30)));  // 16:30 EDT
    }

    [Fact]
    public void A_broken_after_hours_window_does_not_cost_the_regular_one()
    {
        var sessions = new TradingSessions(null, MarketQuote.Window(1_790_256_600, 1_790_280_000), null);

        Assert.Equal(MarketPhase.Open, MarketQuote.Phase(sessions, At(1_790_260_000)));
        Assert.Equal(MarketPhase.Closed, MarketQuote.Phase(sessions, At(1_790_285_000)));
    }

    [Theory]
    [InlineData(MarketPhase.Open, "")]
    [InlineData(MarketPhase.Unknown, "")]
    [InlineData(MarketPhase.Closed, "Market closed")]
    [InlineData(MarketPhase.Pre, "Pre-market")]
    [InlineData(MarketPhase.Post, "After hours")]
    public void Only_the_phases_worth_saying_get_a_badge(MarketPhase phase, string expected)
        => Assert.Equal(expected, MarketQuote.PhaseLabel(Locale.En, phase));

    [Fact]
    public void S1100_the_badge_is_in_the_language_asked_for()
        => Assert.Equal("Markt geschlossen", MarketQuote.PhaseLabel(Locale.De, MarketPhase.Closed));

    // ---- the series ------------------------------------------------------------------------

    [Fact]
    public void A_padded_null_close_is_dropped_rather_than_read_as_zero()
    {
        // These services pad illiquid minutes with nulls. A non-nullable bind would turn each one
        // into a price of 0.00 and draw a chart that falls off a cliff every few bars.
        var bars = MarketQuote.Bars([100, 160, 220, 280, 340], [181.2, null, 182.4, double.NaN, 183.0]);

        Assert.Equal([181.2, 182.4, 183.0], MarketQuote.Closes(bars));
        Assert.Equal([100L, 220L, 340L], bars.Select(b => b.Time));
    }

    [Fact]
    public void An_absent_series_is_empty_rather_than_null()
        => Assert.Empty(MarketQuote.Bars(null, null));

    [Fact]
    public void The_series_is_scaled_to_itself_and_not_to_zero()
    {
        // A share that spent the day between 181.20 and 182.40 drawn against a zero baseline is a
        // flat line, and the shape of the day is the only thing a sparkline has to say.
        var scaled = MarketQuote.Normalise([181.2, 181.8, 182.4]);

        Assert.Equal(0, scaled[0], 6);
        Assert.Equal(0.5, scaled[1], 6);
        Assert.Equal(1, scaled[2], 6);
    }

    [Fact]
    public void A_flat_series_draws_down_the_middle_rather_than_dividing_by_zero()
    {
        var scaled = MarketQuote.Normalise([5.0, 5.0, 5.0]);

        Assert.All(scaled, value => Assert.Equal(0.5, value, 6));
    }

    [Fact]
    public void Normalising_nothing_yields_nothing()
        => Assert.Empty(MarketQuote.Normalise([]));

    private static TradingSessions Sessions() => new(
        MarketQuote.Window(1_790_236_800, 1_790_256_600),
        MarketQuote.Window(1_790_256_600, 1_790_280_000),
        MarketQuote.Window(1_790_280_000, 1_790_294_400));
}
