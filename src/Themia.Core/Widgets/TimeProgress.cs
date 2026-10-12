using Themia.Core.Model;
using Themia.Core.Office;

namespace Themia.Core.Widgets;

/// <summary>[S-4800] The four spans the Year progress widget measures, in the order it lists them.</summary>
public enum ProgressSpan
{
    Day = 0,
    Week = 1,
    Month = 2,
    Year = 3,
}

/// <summary>[S-4801] One span around an instant: where it began, where it ends and how much of it has gone.</summary>
/// <param name="Span">Which span.</param>
/// <param name="First">Its first local day.</param>
/// <param name="Next">The day after its last.</param>
/// <param name="Start">The instant it began (local midnight of <paramref name="First"/>).</param>
/// <param name="End">The instant it ends (local midnight after its last day).</param>
/// <param name="Fraction">How much has passed, 0..1.</param>
public readonly record struct SpanProgress(ProgressSpan Span, DateOnly First, DateOnly Next, DateTimeOffset Start, DateTimeOffset End, double Fraction)
{
    /// <summary>How many days it has: 28 to 31 for a month, 365 or 366 for a year.</summary>
    public int Days => Next.DayNumber - First.DayNumber;

    /// <summary>Which of its days <paramref name="day"/> is, from 1.</summary>
    public int DayOf(DateOnly day) => day.DayNumber - First.DayNumber + 1;

    /// <summary>The span's real length: a DST day is 23 or 25 hours, a leap year 366 days.</summary>
    public TimeSpan Length => End - Start;

    /// <summary>What is left of it after <paramref name="now"/>, never negative.</summary>
    public TimeSpan Left(DateTimeOffset now) => End > now ? End - now : TimeSpan.Zero;
}

/// <summary>
/// [S-4801] How much of the day, week, month and year has passed. Each span runs from one local
/// midnight to another (<see cref="CalendarRules.LocalMidnight"/>), and the fraction is elapsed over
/// real length, measured in instants: a 23-hour spring-forward day is half gone at 12:30 wall
/// clock, not at noon, and a leap year is 366 days. The week starts on the regional first day
/// ([S-3770]). Pure: the zone and the week start are arguments.
/// </summary>
public static class TimeProgress
{
    /// <summary>The spans in list order.</summary>
    public static IReadOnlyList<ProgressSpan> All { get; } = [ProgressSpan.Day, ProgressSpan.Week, ProgressSpan.Month, ProgressSpan.Year];

    /// <summary>[S-4801] The span <paramref name="span"/> holding <paramref name="now"/> in <paramref name="zone"/>.</summary>
    public static SpanProgress Of(ProgressSpan span, DateTimeOffset now, TimeZoneInfo zone, WeekStartDay weekStart)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var (first, next) = span switch
        {
            ProgressSpan.Day => (today, today.AddDays(1)),
            ProgressSpan.Week => (WeekStart(today, weekStart), WeekStart(today, weekStart).AddDays(7)),
            ProgressSpan.Month => (new DateOnly(today.Year, today.Month, 1), new DateOnly(today.Year, today.Month, 1).AddMonths(1)),
            _ => (new DateOnly(today.Year, 1, 1), new DateOnly(today.Year, 1, 1).AddYears(1)),
        };

        var start = CalendarRules.LocalMidnight(first, zone);
        var end = CalendarRules.LocalMidnight(next, zone);
        var length = (end - start).Ticks;
        var fraction = length <= 0 ? 0 : Math.Clamp((now - start).Ticks / (double)length, 0, 1);
        return new SpanProgress(span, first, next, start, end, fraction);
    }

    /// <summary>[S-4802] The first day of the week holding <paramref name="day"/>.</summary>
    public static DateOnly WeekStart(DateOnly day, WeekStartDay weekStart)
    {
        return CalendarRules.StartOfWeek(day, weekStart);
    }

    /// <summary>
    /// [S-4802] The week's number in its year. A Monday week is ISO 8601's (week 1 holds the first
    /// Thursday, so 29 Dec can be week 1 of the next year and 1 Jan week 53 of the last); a Sunday
    /// week counts as North America does: week 1 holds 1 January; so does a Saturday week (Iran).
    /// </summary>
    public static int WeekNumber(DateOnly day, WeekStartDay weekStart)
    {
        if (weekStart == WeekStartDay.Monday)
        {
            return System.Globalization.ISOWeek.GetWeekOfYear(day.ToDateTime(TimeOnly.MinValue));
        }

        var firstWeek = WeekStart(new DateOnly(day.Year, 1, 1), weekStart);
        return ((day.DayNumber - firstWeek.DayNumber) / 7) + 1;
    }

    /// <summary>The percentage shown for a fraction: whole percent, floored so 100 % is only ever the end.</summary>
    public static double Percent(double fraction, int decimals = 0)
    {
        var scale = Math.Pow(10, Math.Clamp(decimals, 0, 3));
        return Math.Floor(Math.Clamp(fraction, 0, 1) * 100 * scale) / scale;
    }
}

/// <summary>[S-4803] What the Year progress widget shows at one size.</summary>
/// <param name="Hero">Only the year, as one large percentage over its bar (the smallest tiles).</param>
/// <param name="Columns">The spans stand side by side (a wide strip) rather than stacked.</param>
/// <param name="Rows">The spans shown, in list order (Day, Week, Month, Year).</param>
/// <param name="Details">Each span carries a second line ("Week 42", "Day 285 of 365").</param>
/// <param name="Headline">Over the rows, the year's percentage large (a tall or large tile).</param>
public readonly record struct YearProgressPlan(bool Hero, bool Columns, IReadOnlyList<ProgressSpan> Rows, bool Details, bool Headline = false);

/// <summary>
/// [S-4803] The Year progress ladder: the year's percentage is the must-show part. A tile too small
/// for two rows shows it alone, large; otherwise the spans are rows in the priority Year, Day, Month,
/// Week (drawn in list order), each a label, its percentage and a bar; once all four fit, each
/// takes a detail line, and with room for it the year's percentage stands large over them. A wide
/// strip stands the four side by side.
/// </summary>
public static class YearProgressLayout
{
    /// <summary>The bar's thickness.</summary>
    public const double Bar = 4;

    /// <summary>Between a row's text and its bar.</summary>
    public const double BarGap = 4;

    /// <summary>Between two rows.</summary>
    public const double RowGap = 8;

    /// <summary>Between two columns on a strip.</summary>
    public const double ColumnGap = 16;

    /// <summary>The narrowest column on a strip: "Month" and "100 %" side by side.</summary>
    public const double MinColumn = 96;

    /// <summary>Which rows a short tile keeps first.</summary>
    public static IReadOnlyList<ProgressSpan> Priority { get; } = [ProgressSpan.Year, ProgressSpan.Day, ProgressSpan.Month, ProgressSpan.Week];

    /// <summary>The headline: the year's percentage in TitleLarge over its caption.</summary>
    public static double HeadlineHeight => FluentType.TitleLarge.LineHeight + FluentType.Caption.LineHeight;

    /// <summary>One row's height: its text line, the gap and the bar.</summary>
    public static double RowHeight => FluentType.Body.LineHeight + BarGap + Bar;

    /// <param name="area">The content area, already inset.</param>
    public static YearProgressPlan Plan(WidgetArea area)
    {
        if (!area.IsMeasured)
        {
            return new YearProgressPlan(true, false, [ProgressSpan.Year], false);
        }

        var detail = FluentType.Caption.LineHeight;

        // A strip: four columns, each the label over a percentage and the bar.
        if (area.Shape == AreaShape.Wide && area.Width >= (4 * MinColumn) + (3 * ColumnGap))
        {
            var column = FluentType.Caption.LineHeight + FluentType.Subtitle.LineHeight + BarGap + Bar;
            if (column <= area.Height + Disclosure.Tolerance)
            {
                var room = new Disclosure(area.Width, area.Height);
                room.Reserve(column);
                return new YearProgressPlan(false, true, TimeProgress.All, room.Take(detail));
            }
        }

        // Too narrow for "Month" and "100 %" side by side: the year alone.
        if (area.Width < MinColumn + Disclosure.Tolerance)
        {
            return new YearProgressPlan(true, false, [ProgressSpan.Year], false);
        }

        var all = (4 * (RowHeight + detail)) + (3 * RowGap);
        if (all <= area.Height + Disclosure.Tolerance)
        {
            var headline = all + HeadlineHeight + RowGap <= area.Height + Disclosure.Tolerance;
            return new YearProgressPlan(false, false, TimeProgress.All, true, headline);
        }

        var count = new Disclosure(area).TakeRows(RowHeight, 4, RowGap);
        if (count < 2)
        {
            return new YearProgressPlan(true, false, [ProgressSpan.Year], false);
        }

        var shown = Priority.Take(count).ToHashSet();
        return new YearProgressPlan(false, false, [.. TimeProgress.All.Where(shown.Contains)], false);
    }
}
