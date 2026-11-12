using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-11505] The chart's sizes on the family's tokens, pure: headerless by default, the drawing in the
/// card's 12 inset on every side, 200..520 wide, one height per style (and, for the calendar, per
/// period: its weeks). The total over it is the reading atom's ([S-11500]).
/// </summary>
public static class ScreenTimeChartLayout
{
    public const double Narrowest = 200;

    public const double NaturalWidth = 300;

    public const double Widest = 520;

    /// <summary>[S-11505] The bars' own height, over their end labels.</summary>
    public const double BarHeight = 72;

    /// <summary>[S-11505] A calendar cell: a ring of its day's share, at every width (the columns stretch, the rings stay).</summary>
    public const double Cell = 20;

    /// <summary>[S-3331] The gap between bars: 8 for a week, 4 for a month.</summary>
    public static double BarGap(int days) => days <= 7 ? FamilySpace.S2 : FamilySpace.S1;

    /// <summary>[S-3331] The end labels' row under the bars: 4, then a Caption line.</summary>
    public static double LabelRow => FamilySpace.S1 + FamilyType.Caption.LineHeight;

    /// <summary>[S-3331] The calendar's weekday letters: a Caption line, then 4.</summary>
    public static double CalendarHeader => FamilyType.Caption.LineHeight + FamilySpace.S1;

    /// <summary>[S-11505] The weeks a calendar of <paramref name="period"/> draws: its days padded to whole rows of seven.</summary>
    public static int Weeks(ScreenTimePeriod period)
        => Math.Max(1, ScreenTimeRules.HeatmapCells(ScreenTimeRules.RangeLength(ScreenTimeRules.ChartPeriod(period))).Count / ScreenTimeRules.HeatmapColumns);

    /// <summary>[S-11505] The drawing's height: the bars and their labels, or the letters over the calendar's weeks.</summary>
    public static double Drawing(ScreenTimeChartStyle style, ScreenTimePeriod period)
    {
        if (style == ScreenTimeChartStyle.Calendar)
        {
            var weeks = Weeks(period);
            return CalendarHeader + (weeks * Cell) + ((weeks - 1) * FamilySpace.S1);
        }

        return BarHeight + LabelRow;
    }

    /// <summary>[S-11505] The chart's sizing, headerless: the drawing in the inset, one height, 200..520 wide.</summary>
    public static WidgetSizing Sizing(ScreenTimeChartStyle style, ScreenTimePeriod period)
    {
        var height = FamilySpace.Inset + Drawing(style, period) + FamilySpace.Inset;
        return WidgetSizing.Range(new(Narrowest, height), new(NaturalWidth, height), new(Widest, height));
    }
}
