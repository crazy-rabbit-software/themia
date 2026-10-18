using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>What the screen-time chart shows at one size ([S-3332]).</summary>
/// <param name="Total">The readout's figure over the drawing, or null when only the drawing fits.</param>
/// <param name="Detail">The line under the figure: the per-day average and the daily limit.</param>
/// <param name="Labels">Weekday names under a week's bars.</param>
/// <param name="Drawing">The height the bars or the calendar get, DIPs: whatever the rungs leave.</param>
public readonly record struct ScreenTimeChartPlan(TypeStep? Total, bool Detail, bool Labels, double Drawing);

/// <summary>What the screen-time donut shows at one size ([S-3342]).</summary>
/// <param name="Ring">The ring's diameter, DIPs: never dropped.</param>
/// <param name="Thickness">The ring's stroke.</param>
/// <param name="Detail">A second line in the hole (the share, "of 4h"), when the hole has room for it.</param>
public readonly record struct ScreenTimeDonutPlan(double Ring, double Thickness, bool Detail);

/// <summary>What the screen-time app list shows at one size ([S-3352]).</summary>
/// <param name="Rows">Whole rows of the list.</param>
/// <param name="Average">Each app's per-day average beside its time (a window only).</param>
/// <param name="Percent">Each app's share of the total.</param>
/// <param name="Names">Each app's name beside its icon; without it a row is the icon and the time.</param>
public readonly record struct ScreenTimeAppsPlan(int Rows, bool Average, bool Percent, bool Names);

/// <summary>
/// The three screen-time widgets' ladders, decided without drawing ([S-3332], [S-3342], [S-3352]).
/// Concise first ([S-666]): each grows its own content — taller bars, a larger ring, more rows — and
/// never gains a part that is another widget's.
/// </summary>
public static class ScreenTimeLayout
{
    /// <summary>The inset the drawings keep from the card's sides.</summary>
    public const double Inset = 16;

    /// <summary>The gap between two shown parts, and above the card's bottom edge.</summary>
    public const double Gap = 8;

    /// <summary>The ring never shrinks below this: under it the total in its hole cannot be read.</summary>
    public const double MinRing = 56;

    /// <summary>A hole narrower than this carries the figure alone.</summary>
    public const double DetailHole = 72;

    /// <summary>The bars or the calendar never get less than this.</summary>
    public const double MinDrawing = 32;

    /// <summary>A day's column needs this much width to carry its weekday under it.</summary>
    public const double LabelledColumn = 16;

    /// <summary>From this width a list row has room for an app's average beside its time.</summary>
    public const double AverageFrom = 300;

    /// <summary>From this width a list row has room for an app's share.</summary>
    public const double PercentFrom = 200;

    /// <summary>
    /// From this width a list row has room for an app's name beside its icon and the longest time
    /// ("23h 59m"); under it the name was squeezed to a letter, so the row drops it for the time.
    /// </summary>
    public const double NamesFrom = 160;

    /// <summary>
    /// [S-3332] The chart: the drawing first (never dropped), then the figure over it, the weekday
    /// labels under a week's bars, and the detail line; the drawing takes what is left.
    /// </summary>
    public static ScreenTimeChartPlan Chart(WidgetArea area, int days, ScreenTimeChartStyle style)
    {
        if (!area.IsMeasured)
        {
            return new ScreenTimeChartPlan(null, false, false, MinDrawing);
        }

        var width = Math.Max(0, area.Width - (2 * Inset));
        var room = new Disclosure(width, Math.Max(0, area.Height - Gap), Gap);
        room.Reserve(MinDrawing);

        var step = area.HeightClass >= SizeClass.Expanded && area.WidthClass >= SizeClass.Regular ? FluentType.Subtitle : FluentType.BodyStrong;
        TypeStep? total = room.Take(step.LineHeight) ? step : null;

        var labelled = style == ScreenTimeChartStyle.Bars && days <= 7 && width / Math.Max(1, days) >= LabelledColumn;
        var labels = labelled && total is not null && room.Take(FluentType.Caption.LineHeight + 2);
        var detail = total is not null && room.Take(FluentType.Caption.LineHeight, 120);

        // The drawing had its floor reserved; it gets the floor back plus all that is left.
        return new ScreenTimeChartPlan(total, detail, labels, MinDrawing + room.Remaining);
    }

    /// <summary>
    /// [S-3342] The donut: one ring as large as the tile lets it be, the total in its hole, and the
    /// line under it once the hole is wide enough to read it.
    /// </summary>
    public static ScreenTimeDonutPlan Donut(WidgetArea area)
    {
        if (!area.IsMeasured)
        {
            return new ScreenTimeDonutPlan(MinRing, Thickness(MinRing), false);
        }

        var fits = Math.Min(area.Width - (2 * Inset), area.Height - Gap);
        var ring = Math.Floor(Math.Max(Math.Min(MinRing, Math.Max(0, area.Height - 2)), fits));
        var thickness = Thickness(ring);
        return new ScreenTimeDonutPlan(ring, thickness, ring - (2 * thickness) >= DetailHole);
    }

    /// <summary>[S-3342] The ring's stroke: a seventh of its size, never thinner than 8.</summary>
    public static double Thickness(double ring) => Math.Max(8, ring * 0.14);

    /// <summary>
    /// [S-3352] The list: as many whole rows as fit (at most <paramref name="wanted"/>), then the
    /// columns each row has width for — the time always, the name from 160, the share from 200, a
    /// window's average from 300.
    /// </summary>
    public static ScreenTimeAppsPlan Apps(WidgetArea area, double rowHeight, int wanted, bool window, bool showAverage, bool showPercent)
    {
        if (!area.IsMeasured)
        {
            return new ScreenTimeAppsPlan(0, false, false, false);
        }

        var rows = Math.Min(Math.Max(0, wanted), Disclosure.WholeRows(area.Height - Gap, rowHeight));
        return new ScreenTimeAppsPlan(
            rows,
            window && showAverage && area.Width >= AverageFrom,
            showPercent && area.Width >= PercentFrom,
            area.Width >= NamesFrom);
    }
}
