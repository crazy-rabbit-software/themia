using System.Globalization;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.Kit;

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

/// <summary>
/// [S-2272] How one app's part is drawn: in the accent at a ramp step of its rank, or (Other, and every
/// app past the ramp) in the tertiary text role at the family's wash. The alpha goes on the shape, never
/// on the shared brush.
/// </summary>
/// <param name="Accent">The accent role; false for the tertiary text role.</param>
/// <param name="Alpha">The shape's opacity, 0..1.</param>
public readonly record struct ScreenTimeTone(bool Accent, double Alpha);

/// <summary>Whether a pinned day is today, yesterday, or just a date.</summary>
/// <summary>[S-1210] The donut's figure: its text, its step, and whether the line under it is shown.</summary>
public readonly record struct ScreenTimeFigure(string Text, TypeStep Step, bool Under);

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
    /// [S-3331] [S-3341] How many apps the chart's stacks and the donut's ring split out by name; the
    /// rest is one "Other". Past seven colours a ring or a stack stops telling apps apart.
    /// </summary>
    public const int DrawingApps = 7;

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
            return Strings.Format(locale, Str.WidgetsC_ScreenTime_Dur_LessThanMinute, ("minutes", 1));
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

    /// <summary>
    /// [S-3351] How many apps a list of <paramref name="rows"/> rows names: all of them when they fit,
    /// else all but the last row, which is "Other" — so the rows always add up to the total.
    /// </summary>
    public static int ListNamed(int rows, int appCount)
    {
        var apps = Math.Max(0, appCount);
        var shown = Math.Max(0, rows);
        return apps <= shown ? apps : Math.Max(0, shown - 1);
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
            return Strings.Format(locale, Str.WidgetsC_ScreenTime_Dur_LessThanMinute, ("minutes", 1));
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
    /// [S-1210] [S-6702] The donut's figure in its hole, on the family's steps: the full duration on the
    /// largest Readout step up to <paramref name="largest"/> (28, or 40 where the donut's Size says so, [S-11515]) whose line,
    /// and the line under it when <paramref name="under"/> asks for one, stands inside the hole as the
    /// rectangle a circle of <paramref name="hole"/> holds; else the compact duration ("4.6h") on the
    /// largest that does; else the compact one in BodyStrong, the family's floor for text. The line under
    /// is shown only where its step's line fits too, so the value never gives way to it.
    /// </summary>
    public static ScreenTimeFigure Figure(double seconds, TypeStep largest, double hole, bool under)
    {
        var ceiling = FamilyType.BaseOf(largest);
        foreach (var text in new[] { FormatDuration(seconds), CompactDuration(seconds) })
        {
            ScreenTimeFigure? best = null;
            foreach (var step in FamilyType.Big.Where(s => FamilyType.BaseOf(s) <= ceiling))
            {
                var value = new ReadoutText(text);
                if (InHole(ReadoutRules.Width(step, value), step.LineHeight, hole))
                {
                    var withUnder = under && InHole(ReadoutRules.Width(step, value), ReadoutRules.Height(step, value with { LineUnder = true }), hole);
                    best = new ScreenTimeFigure(text, step, withUnder);
                }
            }

            if (best is { } figure)
            {
                return figure;
            }
        }

        return new ScreenTimeFigure(CompactDuration(seconds), FamilyType.BodyStrong, false);
    }

    /// <summary>Whether a box <paramref name="width"/> × <paramref name="height"/> centred in a circle of <paramref name="diameter"/> stays inside it.</summary>
    public static bool InHole(double width, double height, double diameter)
        => diameter > 0 && (width * width) + (height * height) <= diameter * diameter;

    /// <summary>[S-1210] How wide a box <paramref name="height"/> tall may be, centred in a circle of <paramref name="diameter"/>: the figure's lines are cut to it.</summary>
    public static double HoleWidth(double height, double diameter)
        => diameter > height ? Math.Sqrt((diameter * diameter) - (height * height)) : 0;

    /// <summary>[S-3321] An unknown period from a hand-edited file reads as a day.</summary>
    public static ScreenTimePeriod ClampPeriod(ScreenTimePeriod period)
        => Enum.IsDefined(period) ? period : ScreenTimePeriod.Day;

    /// <summary>[S-3330] The chart draws days: a day period, or an unknown one, reads as the week.</summary>
    public static ScreenTimePeriod ChartPeriod(ScreenTimePeriod period)
        => period == ScreenTimePeriod.Month ? ScreenTimePeriod.Month : ScreenTimePeriod.Week;

    /// <summary>[S-451] Days a period covers: 1, 7 or 30.</summary>
    public static int RangeLength(ScreenTimePeriod period) => period switch
    {
        ScreenTimePeriod.Week => 7,
        ScreenTimePeriod.Month => 30,
        _ => 1,
    };

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
    /// [S-1115] The day count is a placeholder, so it is written in the language's own digits.
    public static string PeriodLabel(ScreenTimePeriod period) => period switch
    {
        ScreenTimePeriod.Week => Str.Screentime_Last7Days.Format(("count", RangeLength(period))),
        ScreenTimePeriod.Month => Str.Screentime_Last30Days.Format(("count", RangeLength(period))),
        _ => Str.Common_Today.Text(),
    };

    /// <summary>[S-3310] What a screen-time widget emphasises: the hover when there is one, else the selection.</summary>
    public static string? Focus(string? hovered, string? selected) => hovered ?? selected;

    /// <summary>
    /// [S-3312] Where a focused app lands among the parts a widget draws: on its own part, else —
    /// when it is one of the period's <paramref name="apps"/> folded into "Other" — on the Other
    /// part, else nowhere (hidden, or not used in this period). An app linked from a longer list
    /// still lights something in a ring of seven.
    /// </summary>
    public static string? FocusPart(string? focus, IReadOnlyList<ScreenTimeSegment> parts, IEnumerable<ScreenTimeApp> apps)
    {
        if (focus is null)
        {
            return null;
        }

        if (parts.Any(part => part.Key == focus))
        {
            return focus;
        }

        return parts.Any(part => part.IsOther) && apps.Any(app => app.App == focus) ? OtherKey : null;
    }

    /// <summary>
    /// [S-2272] The accent's ramp by rank: the app with the most time of the period at 100 %, then 72 %,
    /// 50 % and 32 %. Every app past it and Other are the tertiary text role at the family's wash.
    /// </summary>
    public static IReadOnlyList<double> Ramp { get; } = [1, 0.72, 0.50, 0.32];

    /// <summary>[S-2272] The tone of the app at <paramref name="rank"/> (0 the busiest), or of Other.</summary>
    public static ScreenTimeTone Tone(int rank, bool isOther = false)
        => !isOther && rank >= 0 && rank < Ramp.Count ? new(true, Ramp[rank]) : new(false, FamilyShape.Wash);

    /// <summary>
    /// [S-2272] Each part's tone by its rank among what one widget shows (<paramref name="segments"/>,
    /// busiest first as <see cref="Segments"/> orders them, ties by key), so an app has one tone in every
    /// bar of the period, the legend's dot and the ring. Other keeps its own tone.
    /// </summary>
    public static IReadOnlyDictionary<string, ScreenTimeTone> Tones(IEnumerable<ScreenTimeSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var tones = new Dictionary<string, ScreenTimeTone>(StringComparer.Ordinal);
        var rank = 0;
        foreach (var segment in segments)
        {
            tones[segment.Key] = segment.IsOther ? Tone(-1, isOther: true) : Tone(rank++);
        }

        return tones;
    }

    /// <summary>[S-2272] The tone of <paramref name="key"/> in <paramref name="tones"/>; a key not among them is drawn as Other.</summary>
    public static ScreenTimeTone ToneOf(IReadOnlyDictionary<string, ScreenTimeTone> tones, string key)
        => tones is not null && tones.TryGetValue(key, out var tone) ? tone : Tone(-1, isOther: true);

    /// <summary>
    /// [S-2272] [S-3312] [S-11902] A part's opacity against the view's links (each already landed on a part,
    /// <see cref="FocusPart"/>): the part pointed at or picked at full strength; every other its tone's
    /// alpha times the links' "less" (<see cref="LinkEmphasis.Others"/>: 0.3 while one is picked, 0.6 while
    /// one is only pointed at); its tone's alpha while nothing is.
    /// </summary>
    public static double Opacity(ScreenTimeTone tone, string key, string? pointedPart, string? pickedPart)
        => LinkEmphasis.Light(key, pointedPart, pickedPart) != LinkLight.None
            ? 1
            : tone.Alpha * LinkEmphasis.Others(pointedPart is not null, pickedPart is not null);

    /// <summary>The letter on an app's chip when it has no icon.</summary>
    public static string Initial(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        return trimmed.Length == 0 ? "?" : char.ToUpperInvariant(trimmed[0]).ToString();
    }
}
