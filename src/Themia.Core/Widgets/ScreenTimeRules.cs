using System.Globalization;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>One wedge of the ring, one segment of a day's bar, or one legend row.</summary>
/// <param name="Key">The app key, or <see cref="ScreenTimeRules.OtherKey"/> for the remainder.</param>
/// <param name="Fraction">Share of the total, 0..1.</param>
/// <param name="Start">Where the segment starts, as a share of the total.</param>
public sealed record ScreenTimeSegment(
    string Key,
    string Name,
    string? Path,
    long Seconds,
    double Fraction,
    double Start,
    bool IsOther);

/// <summary>One slice of a day's stacked bar in a window.</summary>
/// <param name="Fraction">Share of the window's busiest day, so bars compare across the window.</param>
public sealed record ScreenTimeStackPart(string Key, long Seconds, double Fraction, bool IsOther);

/// <summary>How a figure stands against its daily limit.</summary>
/// <param name="Ratio">Share of the limit, uncapped: 1.4 is 40% over.</param>
public readonly record struct ScreenTimeGoal(long Limit, double Used, double Ratio, bool Over);

/// <summary>Whether a pinned day is today, yesterday, or just a date.</summary>
public enum ScreenTimeDayKind
{
    Today = 0,
    Yesterday = 1,
    Date = 2,
}

/// <summary>
/// What the screen-time widget draws, decided without drawing it: durations, the ring's wedges, the
/// window's bars and heat cells, hidden apps, averages, shares and limits.
/// </summary>
public static class ScreenTimeRules
{
    /// <summary>
    /// [S-1212] The fewest apps the drawing splits out by name when the legend lists fewer (or none):
    /// a ring of one app and a grey "Other" says less than the busiest few in their colours.
    /// </summary>
    public const int DrawingApps = 7;

    /// <summary>[S-452] The most the legend can be set to list.</summary>
    public const int MaxAppsLimit = 50;

    /// <summary>The key of the aggregate remainder wedge. Never a real app key: those are file stems.</summary>
    public const string OtherKey = "__other__";

    /// <summary>[S-454] The day navigation reaches back this many days from today.</summary>
    public const int MaxHistoryOffset = 89;

    /// <summary>[S-456] The heatmap is a week wide.</summary>
    public const int HeatmapColumns = 7;

    /// <summary>[S-452] <c>3h 24m</c> from an hour, <c>12m</c> from a minute, <c>&lt;1m</c> below.</summary>
    public static string FormatDuration(double seconds) => FormatDuration(Strings.Current, seconds);

    /// <summary><see cref="FormatDuration(double)"/> in an explicit language: "3h 24m", "3 Std. 24 Min.".</summary>
    public static string FormatDuration(Locale locale, double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 60)
        {
            return Strings.Get(locale, Str.WidgetsC_ScreenTime_Dur_LessThanMinute);
        }

        var whole = (long)seconds;

        if (whole >= 3600)
        {
            return Strings.Format(locale, Str.WidgetsC_ScreenTime_Dur_HoursMinutes, ("hours", whole / 3600), ("minutes", whole % 3600 / 60));
        }

        return Strings.Format(locale, Str.WidgetsC_ScreenTime_Dur_Minutes, ("minutes", whole / 60));
    }

    /// <summary>[S-457] A limit as it reads beside a figure: whole hours drop their minutes, "of 4h".</summary>
    public static string FormatLimit(long seconds) => FormatLimit(Strings.Current, seconds);

    /// <summary><see cref="FormatLimit(long)"/> in an explicit language.</summary>
    public static string FormatLimit(Locale locale, long seconds)
        => seconds >= 3600 && seconds % 3600 < 60
            ? Strings.Format(locale, Str.WidgetsC_ScreenTime_Dur_Hours, ("hours", LocaleFormat.Integer(locale, seconds / 3600)))
            : FormatDuration(locale, seconds);

    /// <summary>[S-1211] The setting clamped to 0..<see cref="MaxAppsLimit"/>; 0 is "as many as fit".</summary>
    public static int EffectiveMaxApps(int maxApps) => Math.Clamp(maxApps, 0, MaxAppsLimit);

    /// <summary>[S-1211] The most apps the legend may name: the setting, or the limit when it is 0 ("as many as fit").</summary>
    public static int Ceiling(int maxApps) => maxApps > 0 ? EffectiveMaxApps(maxApps) : MaxAppsLimit;

    /// <summary>
    /// [S-1211] Rows the legend would like: every app up to the ceiling, plus an "Other" row when
    /// apps are left over. What the tile has room for is decided by the layout, in whole rows.
    /// </summary>
    public static int LegendWanted(int maxApps, int appCount)
    {
        var apps = Math.Max(0, appCount);
        var ceiling = Ceiling(maxApps);
        return apps <= ceiling ? apps : ceiling + 1;
    }

    /// <summary>
    /// [S-1212] How many apps the drawing names, given the legend rows the tile could show. A legend
    /// with room for every app names them all (no "Other"); a shorter one leaves its last row to
    /// "Other". The drawing never names fewer than <see cref="DrawingApps"/> unless the setting
    /// says fewer — the list can be cut short by the tile, the ring should not be.
    /// </summary>
    /// <param name="maxApps">The setting: 0 for as many as fit, else a ceiling.</param>
    /// <param name="legendRows">Rows the legend actually shows (0 when it is off or nothing fits).</param>
    /// <param name="appCount">Apps there are to draw.</param>
    public static int DrawingCap(int maxApps, int legendRows, int appCount)
    {
        var apps = Math.Max(0, appCount);
        var rows = Math.Max(0, legendRows);
        var named = apps <= rows ? apps : Math.Max(0, rows - 1);
        return Math.Min(Ceiling(maxApps), Math.Max(DrawingApps, named));
    }

    /// <summary>
    /// [S-1210] A duration with the minutes dropped, for a ring too small for <c>3h 24m</c>: one
    /// decimal of hours under ten hours (<c>3.4h</c>), whole hours from ten, minutes under an hour.
    /// </summary>
    public static string CompactDuration(double seconds) => CompactDuration(Strings.Current, seconds);

    /// <summary><see cref="CompactDuration(double)"/> in an explicit language, with its decimal mark.</summary>
    public static string CompactDuration(Locale locale, double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 60)
        {
            return Strings.Get(locale, Str.WidgetsC_ScreenTime_Dur_LessThanMinute);
        }

        if (seconds < 3600)
        {
            return Strings.Format(locale, Str.WidgetsC_ScreenTime_Dur_Minutes, ("minutes", (long)seconds / 60));
        }

        // Rounded, not cut: 9h 58m reads "10h", never "9h".
        var hours = Math.Round(seconds / 3600, 1, MidpointRounding.AwayFromZero);
        var figure = hours < 10
            ? LocaleFormat.Number(locale, hours, 1)
            : LocaleFormat.Number(locale, Math.Round(seconds / 3600, MidpointRounding.AwayFromZero), 0);
        return Strings.Format(locale, Str.WidgetsC_ScreenTime_Dur_Hours, ("hours", figure));
    }

    /// <summary>
    /// [S-1210] The total in the ring's hole, and the type it is set in: the full duration at the
    /// largest step that fits, or — when even Caption cannot hold it — the compact one. Never text
    /// that the hole would clip. The character budget carries one spare character, since bold digits
    /// run wider than the average advance the estimate uses.
    /// </summary>
    public static (string Text, TypeStep Step) RingText(double seconds, double width, double height)
    {
        var full = FormatDuration(seconds);
        var step = FluentType.Fit(width, height, full.Length + 1, FluentType.Caption, FluentType.Title);

        if (FluentType.EstimateWidth(step, full.Length + 1) <= width)
        {
            return (full, step);
        }

        var compact = CompactDuration(seconds);
        return (compact, FluentType.Fit(width, height, compact.Length + 1, FluentType.Caption, FluentType.Title));
    }

    /// <summary>[S-1214] The <paramref name="count"/> dates ending at <paramref name="last"/>, oldest first: the week strip under a day.</summary>
    public static List<LocalDay> DaysEnding(LocalDay last, int count)
    {
        var days = new List<LocalDay>(Math.Max(0, count));

        for (var i = count - 1; i >= 0; i--)
        {
            days.Add(last.AddDays(-i));
        }

        return days;
    }

    /// <summary>[S-451] The styles that can draw a period: apps for a day, days for a window.</summary>
    public static IReadOnlyList<ScreenTimeStyle> StylesFor(ScreenTimePeriod period) => period == ScreenTimePeriod.Day
        ? [ScreenTimeStyle.Doughnut, ScreenTimeStyle.Bars, ScreenTimeStyle.None]
        : [ScreenTimeStyle.Bars, ScreenTimeStyle.Heatmap, ScreenTimeStyle.None];

    /// <summary>
    /// [S-451] A style the period can draw: an impossible pair — or an unknown value from a hand-edited
    /// file — becomes the period's first style, never an empty body.
    /// </summary>
    public static ScreenTimeStyle ClampStyle(ScreenTimePeriod period, ScreenTimeStyle style)
    {
        var allowed = StylesFor(period);
        return allowed.Contains(style) ? style : allowed[0];
    }

    /// <summary>[S-451] An unknown period from a hand-edited file reads as a day.</summary>
    public static ScreenTimePeriod ClampPeriod(ScreenTimePeriod period)
        => Enum.IsDefined(period) ? period : ScreenTimePeriod.Day;

    /// <summary>[S-451] Days a period covers: 1, 7 or 30.</summary>
    public static int RangeLength(ScreenTimePeriod period) => period switch
    {
        ScreenTimePeriod.Week => 7,
        ScreenTimePeriod.Month => 30,
        _ => 1,
    };

    /// <summary>[S-455] The hidden-app list, cleaned: no empties, no repeats.</summary>
    public static HashSet<string> Hidden(IEnumerable<string>? raw)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);

        foreach (var key in raw ?? [])
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                set.Add(key);
            }
        }

        return set;
    }

    /// <summary>[S-455] The apps that are not hidden.</summary>
    public static List<ScreenTimeApp> Visible(IEnumerable<ScreenTimeApp> apps, IReadOnlySet<string> hidden)
        => [.. apps.Where(app => !hidden.Contains(app.App))];

    /// <summary>[S-455] Seconds of the given apps; negatives count as none. What is shown always sums to what is drawn.</summary>
    public static long Sum(IEnumerable<ScreenTimeApp> apps) => apps.Sum(app => Math.Max(0, app.Seconds));

    /// <summary>[S-452] Busiest first, capped.</summary>
    public static List<ScreenTimeApp> Top(IEnumerable<ScreenTimeApp> apps, int maxApps)
        => [.. apps.OrderByDescending(app => app.Seconds).ThenBy(app => app.App, StringComparer.Ordinal).Take(Math.Max(0, maxApps))];

    /// <summary>
    /// [S-452] The ring's wedges: the busiest apps up to the cap, then one "Other" wedge for the rest,
    /// so the ring always sums to the whole. Offsets are worked out from cumulative seconds rather
    /// than summed fractions, so wedge edges meet without a seam. Empty when there is nothing to draw.
    /// </summary>
    public static List<ScreenTimeSegment> Segments(IEnumerable<ScreenTimeApp> apps, long total, int cap, string otherLabel)
    {
        var segments = new List<ScreenTimeSegment>();

        if (total <= 0)
        {
            return segments;
        }

        long covered = 0;

        foreach (var app in Top(apps, cap))
        {
            var seconds = Math.Max(0, app.Seconds);
            segments.Add(new ScreenTimeSegment(
                app.App,
                app.Name,
                app.Path,
                seconds,
                Math.Min(1, (double)seconds / total),
                Math.Min(1, (double)covered / total),
                false));

            covered += seconds;
        }

        var rest = total - covered;
        if (rest >= 1)
        {
            segments.Add(new ScreenTimeSegment(
                OtherKey,
                otherLabel,
                null,
                rest,
                Math.Min(1, (double)rest / total),
                Math.Min(1, (double)covered / total),
                true));
        }

        return segments;
    }

    /// <summary>[S-454] Each day's shown seconds, oldest first, re-derived without the hidden apps. Zero days stay.</summary>
    public static List<(string Date, long Seconds)> DayTotals(IEnumerable<ScreenTimeDay> days, IReadOnlySet<string> hidden)
        => [.. days.Select(day => (day.Date, Sum(Visible(day.Apps, hidden))))];

    /// <summary>[S-456] The window's busiest day, the scale both window drawings use.</summary>
    public static long Busiest(IEnumerable<(string Date, long Seconds)> totals)
        => totals.Aggregate(0L, (max, day) => Math.Max(max, Math.Max(0, day.Seconds)));

    /// <summary>
    /// [S-456] A heat cell's shade against the busiest day: 0 only for no use, otherwise quartiles,
    /// so any use at all shows and the busiest day is always the darkest.
    /// </summary>
    public static int HeatLevel(long seconds, long busiest)
    {
        if (seconds <= 0 || busiest <= 0)
        {
            return 0;
        }

        var quartile = (int)Math.Ceiling(Math.Min(seconds, busiest) / (double)busiest * 4);
        return Math.Clamp(quartile, 1, 4);
    }

    /// <summary>
    /// [S-456] The window padded with leading blanks so it ends on the last cell of a row: wrapped at
    /// seven, every column is then one weekday, decided by the window's own dates.
    /// </summary>
    public static List<int?> HeatmapCells(int count)
    {
        var lead = (HeatmapColumns - (count % HeatmapColumns)) % HeatmapColumns;
        var cells = new List<int?>(lead + count);

        for (var i = 0; i < lead; i++)
        {
            cells.Add(null);
        }

        for (var i = 0; i < count; i++)
        {
            cells.Add(i);
        }

        return cells;
    }

    /// <summary>
    /// [S-456] The window's apps summed into one list, busiest first, hidden apps left out: the
    /// window's legend, and the order every stacked bar is split in.
    /// </summary>
    public static List<ScreenTimeApp> Aggregate(IEnumerable<ScreenTimeDay> days, IReadOnlySet<string> hidden)
    {
        var byApp = new Dictionary<string, ScreenTimeApp>(StringComparer.Ordinal);

        foreach (var day in days)
        {
            foreach (var app in day.Apps)
            {
                if (hidden.Contains(app.App))
                {
                    continue;
                }

                var seconds = Math.Max(0, app.Seconds);
                byApp[app.App] = byApp.TryGetValue(app.App, out var known)
                    ? known with { Seconds = known.Seconds + seconds, Path = known.Path ?? app.Path }
                    : app with { Seconds = seconds };
            }
        }

        return [.. byApp.Values.OrderByDescending(app => app.Seconds).ThenBy(app => app.App, StringComparer.Ordinal)];
    }

    /// <summary>
    /// [S-456] One day split into stacked parts in the legend's order, so a colour is one app in
    /// every bar. Apps off the legend fold into its "Other" part, so a day's parts always add up to
    /// its shown total. Fractions are of the busiest day.
    /// </summary>
    public static List<ScreenTimeStackPart> Stack(
        IEnumerable<ScreenTimeApp> apps,
        IReadOnlyList<ScreenTimeSegment> legend,
        long busiest)
    {
        var parts = new List<ScreenTimeStackPart>();

        if (busiest <= 0)
        {
            return parts;
        }

        var bySeconds = new Dictionary<string, long>(StringComparer.Ordinal);
        long total = 0;

        foreach (var app in apps)
        {
            var seconds = Math.Max(0, app.Seconds);
            bySeconds[app.App] = bySeconds.GetValueOrDefault(app.App) + seconds;
            total += seconds;
        }

        long covered = 0;

        foreach (var segment in legend)
        {
            if (segment.IsOther)
            {
                continue;
            }

            var seconds = bySeconds.GetValueOrDefault(segment.Key);
            covered += seconds;

            if (seconds > 0)
            {
                parts.Add(new ScreenTimeStackPart(segment.Key, seconds, (double)seconds / busiest, false));
            }
        }

        var rest = total - covered;
        if (rest > 0 && legend.Any(segment => segment.IsOther))
        {
            parts.Add(new ScreenTimeStackPart(OtherKey, rest, (double)rest / busiest, true));
        }

        return parts;
    }

    /// <summary>
    /// [S-453] Per-day average over a window: the total divided by the window's length, so idle days
    /// count as zero and the per-app averages add up to the window's.
    /// </summary>
    public static double DailyAverage(double seconds, int days) => days > 0 && seconds > 0 ? seconds / days : 0;

    /// <summary>[S-453] An app's share of the shown total, 0..1; 0 when there is no total.</summary>
    public static double Share(long seconds, long total) => total > 0 && seconds > 0 ? Math.Min(1, (double)seconds / total) : 0;

    /// <summary>[S-453] The window's switch count.</summary>
    public static long TotalSwitches(IEnumerable<ScreenTimeDay> days) => days.Sum(day => Math.Max(0, day.Switches));

    /// <summary>[S-457] A limit is a positive number of seconds; anything else is "no limit", never a zero-second one.</summary>
    public static long? Limit(long seconds) => seconds > 0 ? seconds : null;

    /// <summary>[S-457] Per-app limits by key. Entries without a key or a positive limit are dropped; a repeated key keeps its last.</summary>
    public static Dictionary<string, long> AppLimits(IEnumerable<ScreenTimeAppLimit>? raw)
    {
        var limits = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var limit in raw ?? [])
        {
            if (limit is not null && !string.IsNullOrWhiteSpace(limit.App) && limit.Seconds > 0)
            {
                limits[limit.App.Trim()] = limit.Seconds;
            }
        }

        return limits;
    }

    /// <summary>[S-457] A figure against its limit; null when no limit is set, so nothing is drawn.</summary>
    public static ScreenTimeGoal? Goal(double used, long? limit)
    {
        if (limit is not { } l || l <= 0)
        {
            return null;
        }

        var u = Math.Max(0, used);
        return new ScreenTimeGoal(l, u, u / l, u > l);
    }

    /// <summary>[S-454] What a pinned day is relative to today. A null pin follows today.</summary>
    public static ScreenTimeDayKind Describe(string? pinned, LocalDay today)
    {
        if (pinned is null || !LocalDay.TryParse(pinned, out var day) || day >= today)
        {
            return ScreenTimeDayKind.Today;
        }

        return day == today.AddDays(-1) ? ScreenTimeDayKind.Yesterday : ScreenTimeDayKind.Date;
    }

    /// <summary>
    /// [S-454] Step the pinned day by <paramref name="delta"/> days, within today and
    /// <see cref="MaxHistoryOffset"/> days back. Reaching today unpins (null): "today" then follows the
    /// clock, while any other day stays the date it is, even across midnight.
    /// </summary>
    public static string? Step(string? pinned, LocalDay today, int delta)
    {
        var from = pinned is not null && LocalDay.TryParse(pinned, out var day) && day < today ? day : today;
        var oldest = today.AddDays(-MaxHistoryOffset);
        var to = from.AddDays(delta);

        if (to < oldest)
        {
            to = oldest;
        }

        return to >= today ? null : to.Key;
    }

    /// <summary>[S-454] Whether the navigation can go one day further back.</summary>
    public static bool CanGoBack(string? pinned, LocalDay today)
        => pinned is null || !LocalDay.TryParse(pinned, out var day) || day > today.AddDays(-MaxHistoryOffset);

    /// <summary>[S-454] The navigation's label: Today, Yesterday, or the weekday and date.</summary>
    public static string DayLabel(string? pinned, LocalDay today) => DayLabel(Strings.Current, pinned, today);

    /// <summary><see cref="DayLabel(string?, LocalDay)"/> in an explicit language.</summary>
    public static string DayLabel(Locale locale, string? pinned, LocalDay today) => Describe(pinned, today) switch
    {
        ScreenTimeDayKind.Yesterday => Strings.Get(locale, Str.Common_Yesterday),
        ScreenTimeDayKind.Date when LocalDay.TryParse(pinned, out var day) => LocaleFormat.Date(locale, day.Date, DateStyle.WeekdayMonthDayShort),
        _ => Strings.Get(locale, Str.Common_Today),
    };

    /// <summary>[S-454] What a window period is called in the header.</summary>
    public static string PeriodLabel(ScreenTimePeriod period) => (period switch
    {
        ScreenTimePeriod.Week => Str.Screentime_Last7Days,
        ScreenTimePeriod.Month => Str.Screentime_Last30Days,
        _ => Str.Common_Today,
    }).Text();

    /// <summary>[S-452] A colour per app, stable across days: a hash of its key picks the hue.</summary>
    /// <returns>The hue in degrees, 0..359.</returns>
    public static int Hue(string app)
    {
        // djb2, wrapped to 32 bits exactly as the original computed it, so an app keeps the colour it
        // had there.
        var hash = 5381;
        foreach (var c in app ?? string.Empty)
        {
            hash = unchecked((hash << 5) + hash + c);
        }

        return ((hash % 360) + 360) % 360;
    }

    /// <summary>An HSL colour as RGB, for the per-app hues. Saturation and lightness are 0..1.</summary>
    public static (byte R, byte G, byte B) HslToRgb(double hue, double saturation, double lightness)
    {
        var c = (1 - Math.Abs((2 * lightness) - 1)) * saturation;
        var h = ((hue % 360) + 360) % 360 / 60;
        var x = c * (1 - Math.Abs((h % 2) - 1));
        var m = lightness - (c / 2);

        var (r, g, b) = h switch
        {
            < 1 => (c, x, 0d),
            < 2 => (x, c, 0d),
            < 3 => (0d, c, x),
            < 4 => (0d, x, c),
            < 5 => (x, 0d, c),
            _ => (c, 0d, x),
        };

        return (ToByte(r + m), ToByte(g + m), ToByte(b + m));

        static byte ToByte(double v) => (byte)Math.Round(Math.Clamp(v, 0, 1) * 255);
    }

    /// <summary>The letter on an app's chip when it has no icon.</summary>
    public static string Initial(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        return trimmed.Length == 0 ? "?" : char.ToUpperInvariant(trimmed[0]).ToString();
    }
}
