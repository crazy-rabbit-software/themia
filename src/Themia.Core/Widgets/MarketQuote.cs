using System.Globalization;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>What kind of thing a symbol names. Presentation only; it never selects an endpoint.</summary>
public enum AssetClass
{
    Equity = 0,
    Crypto,
    Currency,
    Commodity,
    Index,
}

/// <summary>Which session the listing exchange is in right now.</summary>
public enum MarketPhase
{
    /// <summary>The reply carried no usable schedule. Deliberately not the same as <see cref="Closed"/>.</summary>
    Unknown = 0,

    Pre,
    Open,
    Post,
    Closed,
}

/// <summary>A validated trading window, in epoch seconds, with <c>Start &lt; End</c>.</summary>
public readonly record struct SessionWindow(long Start, long End);

/// <summary>The day's sessions, each present only when the service sent a usable one.</summary>
/// <param name="Pre">Pre-market.</param>
/// <param name="Regular">The regular session. Its absence is what makes the phase unknowable.</param>
/// <param name="Post">After hours.</param>
public sealed record TradingSessions(SessionWindow? Pre, SessionWindow? Regular, SessionWindow? Post)
{
    public static TradingSessions None { get; } = new(null, null, null);
}

/// <summary>
/// Reading a quote: what the symbol is, how many decimals its price needs, and whether its exchange
/// is open.
/// <para>
/// All of it pure and all of it here, because all of it is arithmetic on numbers that arrived over
/// the wire from a service with no contract. The alternative — doing it inside the view — is how a
/// missing field becomes an exception on a render path.
/// </para>
/// </summary>
public static class MarketQuote
{
    /// <summary>
    /// Which kind of instrument a symbol names.
    /// <para>
    /// Two strengths of evidence, in order. A live search result carries the service's own quote
    /// type and it wins; a symbol restored from the state file carries nothing, so the fallback
    /// reads Yahoo's symbol grammar — <c>^</c> for an index, <c>=X</c> for a currency pair,
    /// <c>=F</c> for a futures contract. Which service quotes it (<paramref name="market"/>)
    /// outranks both: a coin id has no Yahoo grammar to read.
    /// </para>
    /// </summary>
    public static AssetClass Classify(string? symbol, QuoteMarket market, string? quoteType = null)
    {
        if (market == QuoteMarket.Crypto)
        {
            return AssetClass.Crypto;
        }

        switch ((quoteType ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "CURRENCY":
                return AssetClass.Currency;
            case "FUTURE":
                return AssetClass.Commodity;
            case "INDEX":
                return AssetClass.Index;
            case "CRYPTOCURRENCY":
                return AssetClass.Crypto;
            case "EQUITY":
            case "ETF":
            case "MUTUALFUND":
                return AssetClass.Equity;
            default:
                break;
        }

        var text = (symbol ?? string.Empty).Trim().ToUpperInvariant();

        if (text.StartsWith('^'))
        {
            return AssetClass.Index;
        }

        if (text.EndsWith("=X", StringComparison.Ordinal))
        {
            return AssetClass.Currency;
        }

        if (text.EndsWith("=F", StringComparison.Ordinal))
        {
            return AssetClass.Commodity;
        }

        return AssetClass.Equity;
    }

    /// <summary>Decimals implied by size alone: two above a unit, four below it.</summary>
    private static int MagnitudeDecimals(double price) => Math.Abs(price) >= 1 ? 2 : 4;

    /// <summary>
    /// How many decimals to print a price with.
    /// <para>
    /// The venue's own tick precision — two for a share, four for an FX pair — is the only thing
    /// that tells EUR/USD at 1.1567 apart from a stock at $1.16, and printing the pair as "1.16"
    /// throws away the entire signal. It is a remote number, so it is validated, and it may only
    /// ever <i>raise</i> precision: a sub-dollar instrument reported with a hint of 2 keeps the four
    /// places the magnitude rule already gave it.
    /// </para>
    /// </summary>
    public static int PriceDecimals(double price, int? priceHint)
    {
        var baseline = MagnitudeDecimals(price);

        if (priceHint is not { } hint || hint is < 0 or > 8)
        {
            return baseline;
        }

        return Math.Max(baseline, hint);
    }

    /// <summary>A price with grouped thousands and the decimals <see cref="PriceDecimals"/> chose.</summary>
    public static string Price(double? price, int? priceHint = null) => Price(Strings.Current, price, priceHint);

    /// <summary>The same, with <paramref name="locale"/>'s separators: "1,234.50" / "1.234,50".</summary>
    public static string Price(Locale locale, double? price, int? priceHint = null)
    {
        if (price is not { } value || !double.IsFinite(value))
        {
            return "--";
        }

        return LocaleFormat.Number(locale, value, PriceDecimals(value, priceHint));
    }

    /// <summary>
    /// A percentage move, always signed: <c>+1.24%</c>, <c>-0.30%</c>, <c>0.00%</c>.
    /// <para>
    /// The sign is carried in the text as well as in the colour, because a state a reader can only
    /// get from a colour is a state some readers do not get at all.
    /// </para>
    /// </summary>
    public static string ChangePercent(double? percent) => ChangePercent(Strings.Current, percent);

    public static string ChangePercent(Locale locale, double? percent)
    {
        if (percent is not { } value || !double.IsFinite(value))
        {
            return "--";
        }

        var sign = value > 0 ? "+" : value < 0 ? "-" : string.Empty;
        return sign + LocaleFormat.Percent(locale, Math.Abs(value), 2);
    }

    /// <summary>
    /// The percentage move from <paramref name="previousClose"/> to <paramref name="price"/>, or
    /// null when the baseline cannot carry one.
    /// </summary>
    public static double? PercentChange(double? price, double? previousClose)
    {
        if (price is not { } now || previousClose is not { } before)
        {
            return null;
        }

        if (!double.IsFinite(now) || !double.IsFinite(before) || before == 0)
        {
            return null;
        }

        var change = (now - before) / Math.Abs(before) * 100;
        return double.IsFinite(change) ? change : null;
    }

    /// <summary>
    /// A trading window, if the two numbers describe one.
    /// <para>
    /// Both bounds finite, and the end strictly after the start. Windows are validated
    /// independently by the caller so a broken after-hours window does not cost the regular one.
    /// </para>
    /// </summary>
    public static SessionWindow? Window(double? startEpochSeconds, double? endEpochSeconds)
    {
        if (startEpochSeconds is not { } start || endEpochSeconds is not { } end)
        {
            return null;
        }

        if (!double.IsFinite(start) || !double.IsFinite(end) || end <= start)
        {
            return null;
        }

        // Epoch seconds outside this range are not a schedule, they are a units mistake — a
        // millisecond timestamp, or a sentinel. Roughly 1970 to 2100.
        if (start is < 0 or > 4_102_444_800 || end is < 0 or > 4_102_444_800)
        {
            return null;
        }

        return new SessionWindow((long)start, (long)end);
    }

    /// <summary>
    /// Which session <paramref name="now"/> falls in.
    /// <para>
    /// Absolute instants only: the windows arrive already resolved for the exchange's own calendar,
    /// holidays and daylight saving, so there is no local time and no assumption that the reader
    /// sits in the exchange's zone. Windows are half-open, <c>[start, end)</c>, so the adjacent
    /// pre/regular/post bounds cannot both claim the same second.
    /// </para>
    /// <para>
    /// Without a usable regular window the answer is <see cref="MarketPhase.Unknown"/>, never
    /// <see cref="MarketPhase.Closed"/>. Saying "market closed" because the payload said nothing is
    /// the bug the original app filed: it showed a permanent closed badge on every symbol, all day,
    /// including mid-session.
    /// </para>
    /// </summary>
    public static MarketPhase Phase(TradingSessions? sessions, DateTimeOffset now)
    {
        if (sessions is null)
        {
            return MarketPhase.Unknown;
        }

        var seconds = now.ToUnixTimeSeconds();

        if (Contains(sessions.Regular, seconds))
        {
            return MarketPhase.Open;
        }

        if (Contains(sessions.Pre, seconds))
        {
            return MarketPhase.Pre;
        }

        if (Contains(sessions.Post, seconds))
        {
            return MarketPhase.Post;
        }

        // Knowing today's regular window is exactly what makes "closed" a statement rather than a
        // guess: outside it, and outside any extended window, nothing is trading.
        return sessions.Regular is not null ? MarketPhase.Closed : MarketPhase.Unknown;
    }

    /// <summary>The badge text for a phase, or empty when there is nothing worth saying.</summary>
    public static string PhaseLabel(MarketPhase phase) => PhaseLabel(Strings.Current, phase);

    public static string PhaseLabel(Locale locale, MarketPhase phase) => phase switch
    {
        MarketPhase.Pre => Strings.Get(locale, Str.Stocks_PreMarket),
        MarketPhase.Post => Strings.Get(locale, Str.Stocks_AfterHours),
        MarketPhase.Closed => Strings.Get(locale, Str.Stocks_MarketClosed),
        _ => string.Empty,
    };

    private static bool Contains(SessionWindow? window, long seconds)
        => window is { } w && seconds >= w.Start && seconds < w.End;

    /// <summary>
    /// Pair up the parallel timestamp and close arrays a chart endpoint returns, dropping any
    /// sample where either half is missing.
    /// <para>
    /// Both halves or neither. These services pad illiquid minutes with nulls, and dropping a close
    /// while keeping its timestamp shears the two arrays out of alignment for every later point —
    /// which draws a plausible-looking line that is wrong. A series out of time order is put back
    /// in order rather than drawn backwards.
    /// </para>
    /// </summary>
    public static IReadOnlyList<QuoteBar> Bars(IReadOnlyList<long?>? timestamps, IReadOnlyList<double?>? closes)
    {
        if (timestamps is null || closes is null)
        {
            return [];
        }

        var count = Math.Min(timestamps.Count, closes.Count);
        var kept = new List<QuoteBar>(count);

        for (var i = 0; i < count; i++)
        {
            if (timestamps[i] is { } time && time is > 0 and < 4_102_444_800
                && closes[i] is { } close && double.IsFinite(close))
            {
                kept.Add(new QuoteBar(time, close));
            }
        }

        if (!IsOrdered(kept))
        {
            kept.Sort(static (x, y) => x.Time.CompareTo(y.Time));
        }

        return kept;
    }

    /// <summary>The closes of a series, for scaling.</summary>
    public static IReadOnlyList<double> Closes(IReadOnlyList<QuoteBar> bars)
        => [.. (bars ?? []).Select(static bar => bar.Close)];

    private static bool IsOrdered(List<QuoteBar> bars)
    {
        for (var i = 1; i < bars.Count; i++)
        {
            if (bars[i].Time < bars[i - 1].Time)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// [S-1308] The last bar inside <paramref name="window"/>, against the regular close — the
    /// original's <c>extendedQuoteIn</c>. Null without a window, without a positive close, or with
    /// no bar in the window.
    /// </summary>
    public static ExtendedPrint? ExtendedQuoteIn(IReadOnlyList<QuoteBar> bars, SessionWindow? window, double? regularClose)
    {
        if (window is not { } w || regularClose is not { } close || !double.IsFinite(close) || close <= 0)
        {
            return null;
        }

        for (var i = bars.Count - 1; i >= 0; i--)
        {
            var bar = bars[i];

            if (bar.Time >= w.Start && bar.Time < w.End)
            {
                return new ExtendedPrint(bar.Close, (bar.Close - close) / close * 100, bar.Time);
            }
        }

        return null;
    }

    /// <summary>[S-1308] Today's pre-market and after-hours prints, each when one exists.</summary>
    public static ExtendedPrints ExtendedQuotesOf(IReadOnlyList<QuoteBar> bars, TradingSessions? sessions, double? regularClose)
        => sessions is null
            ? default
            : new ExtendedPrints(ExtendedQuoteIn(bars, sessions.Pre, regularClose), ExtendedQuoteIn(bars, sessions.Post, regularClose));

    /// <summary>
    /// [S-1308] The print to show under the price for the phase the market is in: the pre-market
    /// one before the bell, the after-hours one after it (and while closed, the latest of the two).
    /// </summary>
    public static ExtendedPrint? CurrentExtended(ExtendedPrints prints, MarketPhase phase) => phase switch
    {
        MarketPhase.Pre => prints.Pre,
        MarketPhase.Post => prints.Post,
        MarketPhase.Closed => prints.Post ?? prints.Pre,
        _ => null,
    };

    /// <summary>
    /// [S-1308] Split a series into runs inside and outside the regular session (half-open bounds),
    /// so an extended-hours stretch can be drawn dashed. One regular run without a usable window.
    /// </summary>
    public static IReadOnlyList<(int From, int To, bool Extended)> Segments(IReadOnlyList<QuoteBar> bars, TradingSessions? sessions)
    {
        if (bars.Count == 0)
        {
            return [];
        }

        if (sessions?.Regular is not { } regular)
        {
            return [(0, bars.Count, false)];
        }

        var segments = new List<(int, int, bool)>();
        var from = 0;
        var current = !Contains(regular, bars[0].Time);

        for (var i = 1; i < bars.Count; i++)
        {
            var extended = !Contains(regular, bars[i].Time);

            if (extended != current)
            {
                segments.Add((from, i, current));
                from = i;
                current = extended;
            }
        }

        segments.Add((from, bars.Count, current));
        return segments;
    }

    /// <summary>[S-1310] Where the series is lowest and highest (first occurrence of each), or null for none.</summary>
    public static (int Low, int High)? Extremes(IReadOnlyList<QuoteBar> bars)
    {
        if (bars is null || bars.Count == 0)
        {
            return null;
        }

        var low = 0;
        var high = 0;

        for (var i = 1; i < bars.Count; i++)
        {
            if (bars[i].Close < bars[low].Close)
            {
                low = i;
            }

            if (bars[i].Close > bars[high].Close)
            {
                high = i;
            }
        }

        return (low, high);
    }

    /// <summary>
    /// Where each close sits in 0..1 against the extremes of the series, for a sparkline.
    /// <para>
    /// Scaled to the series, not to zero: a share that spent the day between 181.20 and 182.40
    /// drawn against a zero baseline is a flat line, and the shape of the day is the only thing a
    /// sparkline has to say. A series with no spread draws down the middle rather than dividing by
    /// zero.
    /// </para>
    /// </summary>
    public static IReadOnlyList<double> Normalise(IReadOnlyList<double> closes)
    {
        if (closes.Count == 0)
        {
            return [];
        }

        var low = double.MaxValue;
        var high = double.MinValue;

        foreach (var close in closes)
        {
            low = Math.Min(low, close);
            high = Math.Max(high, close);
        }

        var span = high - low;
        var scaled = new double[closes.Count];

        for (var i = 0; i < closes.Count; i++)
        {
            scaled[i] = span > 0 ? (closes[i] - low) / span : 0.5;
        }

        return scaled;
    }
}
