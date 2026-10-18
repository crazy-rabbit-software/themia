using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>What the screen-time chart shows at one size ([S-3332]).</summary>
/// <param name="Total">The readout's figure over the drawing, or null when only the drawing fits.</param>
/// <param name="Detail">The line under the figure: the per-day average and the daily limit.</param>
/// <param name="Labels">Weekday names under a week's bars.</param>
/// <param name="Drawing">The height the bars or the calendar get, DIPs: whatever the rungs leave.</param>
public readonly record struct ScreenTimeChartPlan(TypeStep? Total, bool Detail, bool Labels, double Drawing);

/// <summary>[S-3332] The chart's ladder.</summary>
public static class ScreenTimeChartLayout
{
    /// <summary>The bars or the calendar never get less than this.</summary>
    public const double MinDrawing = 32;

    /// <summary>A day's column needs this much width to carry its weekday under it.</summary>
    public const double LabelledColumn = 16;

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

        var width = Math.Max(0, area.Width - (2 * ScreenTimeLayout.Inset));
        var room = new Disclosure(width, Math.Max(0, area.Height - ScreenTimeLayout.Gap), ScreenTimeLayout.Gap);
        room.Reserve(MinDrawing);

        var step = area.HeightClass >= SizeClass.Expanded && area.WidthClass >= SizeClass.Regular ? FluentType.Subtitle : FluentType.BodyStrong;
        TypeStep? total = room.Take(step.LineHeight) ? step : null;

        var labelled = style == ScreenTimeChartStyle.Bars && days <= 7 && width / Math.Max(1, days) >= LabelledColumn;
        var labels = labelled && total is not null && room.Take(FluentType.Caption.LineHeight + 2);
        var detail = total is not null && room.Take(FluentType.Caption.LineHeight, 120);

        // The drawing had its floor reserved; it gets the floor back plus all that is left.
        return new ScreenTimeChartPlan(total, detail, labels, MinDrawing + room.Remaining);
    }
}
