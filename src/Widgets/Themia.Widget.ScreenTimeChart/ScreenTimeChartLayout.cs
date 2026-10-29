using Themia.Core.Model;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>What the screen-time chart shows at one size ([S-3332]).</summary>
/// <param name="Total">The Readout's step over the drawing, or null when only the drawing fits.</param>
/// <param name="Under">The line under the figure: the per-day average and the daily limit.</param>
/// <param name="Labels">The bars' two end labels (the first day and today) under them.</param>
/// <param name="Drawing">The height the bars (with their labels' row) or the calendar get, DIPs: whatever the rungs leave.</param>
public readonly record struct ScreenTimeChartPlan(TypeStep? Total, bool Under, bool Labels, double Drawing);

/// <summary>[S-3331] How the calendar's cells stand in the drawing.</summary>
/// <param name="Cell">A cell's side: a ring of its day from 16, a dot under it.</param>
/// <param name="Rows">The weeks shown, the latest ones (a drawing too short for every week shows the last).</param>
/// <param name="Header">Whether the weekday letters stand over the cells.</param>
public readonly record struct ScreenTimeCalendarPlan(double Cell, int Rows, bool Header);

/// <summary>[S-3332] The chart's ladder on the family's Readout and Chart ([S-6702], [S-6717]).</summary>
public static class ScreenTimeChartLayout
{
    /// <summary>The bars or the calendar never get less than this.</summary>
    public const double MinDrawing = 32;

    /// <summary>[S-3331] The calendar's cells: at most this big, rings from <see cref="RingCell"/>, dots down to <see cref="MinCell"/>.</summary>
    public const double MaxCell = 40;

    /// <summary>[S-3331] From this side a calendar cell is a ring of its day's share; under it a dot.</summary>
    public const double RingCell = FamilyShape.Icon;

    /// <summary>[S-3331] The smallest calendar cell.</summary>
    public const double MinCell = FamilySpace.S2;

    /// <summary>[S-3331] The gap between bars: 8 for a week, 4 for a month.</summary>
    public static double BarGap(int days) => days <= 7 ? FamilySpace.S2 : FamilySpace.S1;

    /// <summary>[S-3331] The end labels' row under the bars: 4, then a Caption line.</summary>
    public static double LabelRow => FamilySpace.S1 + FamilyType.Caption.LineHeight;

    /// <summary>[S-3331] The calendar's weekday letters: a Caption line, then 4.</summary>
    public static double CalendarHeader => FamilyType.Caption.LineHeight + FamilySpace.S1;

    /// <summary>
    /// [S-3332] The chart in the view's area less the card's bottom inset: the drawing first (never
    /// dropped, at least 32), then the Readout over it (28, 40 or 68 by the area, sized on
    /// <paramref name="worst"/>, 8 above the drawing), the bars' end labels, and the line under the
    /// figure; the drawing takes what is left.
    /// </summary>
    public static ScreenTimeChartPlan Chart(WidgetArea area, ScreenTimeChartStyle style, string worst)
    {
        if (!area.IsMeasured)
        {
            return new ScreenTimeChartPlan(null, false, false, MinDrawing);
        }

        var width = Math.Max(0, area.Width - (2 * FamilySpace.Inset));
        var height = Math.Max(0, area.Height - FamilySpace.Inset);
        var room = height - MinDrawing;

        TypeStep? total = null;
        var under = false;
        var text = new ReadoutText(worst, LineUnder: true);
        var step = ReadoutRules.Step(area, width, Math.Max(0, room - FamilySpace.S2), text);
        if (step.LineHeight + FamilySpace.S2 <= room)
        {
            total = step;
            room -= step.LineHeight + FamilySpace.S2;
        }

        var labels = style == ScreenTimeChartStyle.Bars && total is not null && room >= LabelRow;
        if (labels)
        {
            room -= LabelRow;
        }

        if (total is { } t && FamilyType.Under(t).LineHeight <= room)
        {
            under = true;
            room -= FamilyType.Under(t).LineHeight;
        }

        // The drawing had its floor reserved; it gets the floor back plus all that is left, and holds the labels' row.
        return new ScreenTimeChartPlan(total, under, labels, MinDrawing + Math.Max(0, room) + (labels ? LabelRow : 0));
    }

    /// <summary>
    /// [S-3331] The calendar's cells in a drawing of <paramref name="width"/> × <paramref name="height"/>
    /// for <paramref name="weeks"/> rows of seven, 4 apart: as big as the drawing lets them be (at most
    /// 40, on the 4 grid), the weekday letters over them while the cells stay rings (16 and up) with
    /// them; every week while a cell keeps 8, else the latest weeks that fit.
    /// </summary>
    public static ScreenTimeCalendarPlan Calendar(double width, double height, int weeks)
    {
        var rows = Math.Max(1, weeks);
        var gap = FamilySpace.S1;
        var across = (width - (6 * gap)) / 7;

        double Side(int shown, bool header) => Math.Min(across, (height - (header ? CalendarHeader : 0) - ((shown - 1) * gap)) / shown);

        // Every week first: as rings under their letters, as rings alone, as dots; then the latest weeks.
        for (var shown = rows; shown >= 1; shown--)
        {
            if (Side(shown, true) >= RingCell)
            {
                return new ScreenTimeCalendarPlan(OnGrid(Side(shown, true)), shown, true);
            }

            if (Side(shown, false) >= MinCell)
            {
                return new ScreenTimeCalendarPlan(OnGrid(Side(shown, false)), shown, false);
            }
        }

        return new ScreenTimeCalendarPlan(Math.Max(0, OnGrid(Side(1, false))), 1, false);
    }

    /// <summary>A cell's side on the 4 grid (at most 40): a whole number of device pixels at every scale, so the columns do not drift.</summary>
    private static double OnGrid(double side) => Math.Floor(Math.Min(MaxCell, side) / FamilySpace.S1) * FamilySpace.S1;
}
