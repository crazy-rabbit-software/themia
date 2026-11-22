using Themia.Core.Links;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-11505] [S-15772] The chart's sizes on the family's tokens, pure: its day line ([S-454]) and the drawing (the bars:
/// the card's whole box, their words in the inset, [S-6754]; the calendar: its head row and its weeks of day cells, from
/// the card's top-left edge), 200..520 wide, one height per style (and, for the calendar, per period: its weeks). The total
/// over it is the reading atom's ([S-11500]).
/// </summary>
public static class ScreenTimeChartLayout
{
    public const double Narrowest = 200;

    public const double NaturalWidth = 300;

    public const double Widest = 520;

    /// <summary>[S-11505] The bars' own height, over their end labels.</summary>
    public const double BarHeight = 72;

    /// <summary>[S-11505] [S-15772] A calendar ring's natural side; its cell is the ring and its padding in whole grid cells.</summary>
    public const double Cell = 20;

    /// <summary>[S-3331] [S-15772] The room between two bars: 8 for a week, 4 for a month, the padding inside their day columns (half each side).</summary>
    public static double BarGap(int days) => days <= 7 ? FamilySpace.S2 : FamilySpace.S1;

    /// <summary>[S-3331] The end labels' row: 4, then a Caption line.</summary>
    public static double LabelRow => FamilySpace.S1 + FamilyType.Caption.LineHeight;

    /// <summary>[S-15772] A calendar day cell's box on <paramref name="grid"/>: its ring and the 8 round it in whole cells, at every size of the card.</summary>
    public static double CellBox(ItemGrid grid) => ItemSize.Optimal(Cell + FamilySpace.S2, grid);

    /// <summary>[S-15772] The ring drawn in a cell's box: the box less its padding (4 on each side).</summary>
    public static double Ring(ItemGrid grid) => CellBox(grid) - FamilySpace.S2;

    /// <summary>[S-15772] The calendar's weekday letters' row on <paramref name="grid"/>: a Caption line in whole cells.</summary>
    public static double HeadRow(ItemGrid grid) => ItemSize.Optimal(FamilyType.Caption.LineHeight, grid);

    /// <summary>[S-11505] The weeks a calendar of <paramref name="period"/> draws: its days padded to whole rows of seven.</summary>
    public static int Weeks(ScreenTimePeriod period)
        => Math.Max(1, ScreenTimeRules.HeatmapCells(ScreenTimeRules.RangeLength(ScreenTimeRules.ChartPeriod(period))).Count / ScreenTimeRules.HeatmapColumns);

    /// <summary>[S-15772] The calendar's height on <paramref name="grid"/>: the letters' row over its weeks of cells, touching.</summary>
    public static double CalendarHeight(ScreenTimePeriod period, ItemGrid grid) => HeadRow(grid) + (Weeks(period) * CellBox(grid));

    /// <summary>[S-11505] The drawing's height under <see cref="Top"/>: the bars (the inset over them where nothing stands there) and their labels' row in the foot's inset, or the calendar.</summary>
    public static double Drawing(ScreenTimeChartStyle style, ScreenTimePeriod period)
        => style == ScreenTimeChartStyle.Calendar
            ? CalendarHeight(period, ItemGrid.Default())
            : (Top(period) > 0 ? 0 : FamilySpace.Inset) + BarHeight + LabelRow + FamilySpace.Inset;

    /// <summary>[S-454] [S-12481] [S-12803] The day line over the drawing for a day (its line and the gap under it), none for a window.</summary>
    public static double DayLine(ScreenTimePeriod period) => ScreenTimeRules.DayLine(period);

    /// <summary>[S-6754] [S-15772] What stands over the drawing: the day line under the inset where there is one; none, and the drawing reaches the top edge.</summary>
    public static double Top(ScreenTimePeriod period) => DayLine(period) is var line and > 0 ? FamilySpace.Inset + line : 0;

    /// <summary>[S-11505] The chart's sizing: its day line over the drawing, one height, 200..520 wide.</summary>
    public static WidgetSizing Sizing(ScreenTimeChartStyle style, ScreenTimePeriod period)
    {
        var height = Top(period) + Drawing(style, period);
        return WidgetSizing.Range(new(Narrowest, height), new(NaturalWidth, height), new(Widest, height));
    }

    /// <summary>
    /// [S-3333] [S-15773] How strong an app's part of a day's bar stands (on its tone's own <paramref name="alpha"/>): the
    /// part pointed at or picked is that one app on that one day (WR-13: "Hover lights exactly the item under the pointer";
    /// never the same app's parts on every other day, which share an app as emails share a sender), whole; every other
    /// part at the links' "less" while one is (0.6 pointed, 0.3 picked); all at their tone while none is.
    /// </summary>
    public static double PartStrength(double alpha, ChartPart part, ChartPart? pointed, ChartPart? picked)
        => part == picked || part == pointed ? 1 : alpha * LinkEmphasis.Others(pointed is not null, picked is not null);
}

/// <summary>[S-15773] One app's part of one day's bar: the item a pointer points at in the chart.</summary>
/// <param name="App">The app's key (or Other's).</param>
/// <param name="Day">The day's index in the window, oldest first.</param>
public readonly record struct ChartPart(string App, int Day);
