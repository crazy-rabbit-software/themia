using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-1309] Times on a stock chart, in the exchange's own zone — the original's
/// <c>exchangeTime.ts</c>.
/// <para>
/// A New York share's day runs 09:30–16:00 whoever is looking at it; labelling its chart in the
/// reader's zone ("15:30–22:00") turns the session's shape into a puzzle. The zone comes from the
/// reply as an offset in seconds and a short name ("EDT") rather than as an IANA id: this app runs
/// without ICU (invariant globalization), so <c>TimeZoneInfo</c> cannot resolve "America/New_York"
/// on Windows, and the offset the exchange reports for today is the one its chart is drawn in.
/// Without an offset, times are local — the original's fallback.
/// </para>
/// </summary>
public static class ExchangeTime
{
    /// <summary>The widest real UTC offsets are −12 and +14 hours.</summary>
    private const int MaxOffsetSeconds = 14 * 3600;

    /// <summary>An offset the reply sent, if it is one: whole minutes, within ±14 h.</summary>
    public static TimeSpan? Offset(long? seconds)
        => seconds is { } value && Math.Abs(value) <= MaxOffsetSeconds && value % 60 == 0
            ? TimeSpan.FromSeconds(value)
            : null;

    /// <summary>A zone's short name ("EDT", "CET", "GMT+1"), if the text is one; else empty.</summary>
    public static string Zone(string? name)
    {
        var text = name?.Trim() ?? string.Empty;

        if (text.Length is 0 or > 6)
        {
            return string.Empty;
        }

        foreach (var c in text)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not '+' and not '-')
            {
                return string.Empty;
            }
        }

        return text;
    }

    /// <summary>Whether a range is labelled with clock times rather than dates.</summary>
    public static bool Intraday(QuoteRange range) => range == QuoteRange.Day;

    /// <summary>Whether dates on a range need their year ("Mar 4, 2025").</summary>
    public static bool WithYear(QuoteRange range) => range >= QuoteRange.TwoYears;

    /// <summary>One label: "14:30" for an intraday chart, "Mar 4" (or "Mar 4, 2025") otherwise.</summary>
    public static string Label(long epochSeconds, TimeSpan? offset, QuoteRange range, TimeSpan localOffset)
        => Label(Strings.Current, epochSeconds, offset, range, localOffset);

    /// <summary>The same label on <paramref name="locale"/>'s clock and calendar.</summary>
    public static string Label(Locale locale, long epochSeconds, TimeSpan? offset, QuoteRange range, TimeSpan localOffset)
    {
        var at = DateTimeOffset.FromUnixTimeSeconds(epochSeconds).ToOffset(offset ?? localOffset);
        var wall = at.DateTime;

        if (Intraday(range))
        {
            return LocaleFormat.Time(locale, wall);
        }

        if (range == QuoteRange.FiveDays)
        {
            return Strings.Format(
                locale,
                Str.WidgetsC_Stocks_WeekdayTime,
                ("weekday", LocaleFormat.WeekdayName(locale, wall.DayOfWeek, NameWidth.Short)),
                ("time", LocaleFormat.Time(locale, wall)));
        }

        return LocaleFormat.Date(locale, wall, WithYear(range) ? DateStyle.Medium : DateStyle.MonthDayShort);
    }

    /// <summary>
    /// [S-1311] The times on a chart: its start and its end, nothing between. Empty for fewer than
    /// two bars.
    /// </summary>
    public static IReadOnlyList<string> Labels(
        IReadOnlyList<QuoteBar> bars,
        TimeSpan? offset,
        QuoteRange range,
        TimeSpan localOffset)
        => Labels(Strings.Current, bars, offset, range, localOffset);

    public static IReadOnlyList<string> Labels(
        Locale locale,
        IReadOnlyList<QuoteBar> bars,
        TimeSpan? offset,
        QuoteRange range,
        TimeSpan localOffset)
    {
        ArgumentNullException.ThrowIfNull(bars);

        if (bars.Count < 2)
        {
            return [];
        }

        var first = Label(locale, bars[0].Time, offset, range, localOffset);
        var last = Label(locale, bars[^1].Time, offset, range, localOffset);

        return [first, last];
    }

    /// <summary>"Prices in EDT" style suffix for a tooltip, or empty without a zone name.</summary>
    public static string ZoneNote(string zone) => ZoneNote(Strings.Current, zone);

    public static string ZoneNote(Locale locale, string zone)
        => zone.Length == 0
            ? Strings.Get(locale, Str.WidgetsC_Stocks_Zone_Local)
            : Strings.Format(locale, Str.WidgetsC_Stocks_Zone_Exchange, ("zone", zone));
}
