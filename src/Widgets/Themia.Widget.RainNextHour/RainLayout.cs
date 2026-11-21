using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-5126] [S-11140] [S-6754] [S-14941] The rain graph's one layout and its size: the plot is the one chart's, the card's
/// edge to edge, a column per quarter hour on it, the inset over the columns' top (the sentence is a widget of its own,
/// <c>rainOutlook</c>); at its foot the window's two ends at the inset, 4 under the columns. One height; 220..420 wide
/// (the columns get wider).
/// </summary>
public static class RainLayout
{
    /// <summary>The columns' own height at the atom's height.</summary>
    public const double Bars = FamilyShape.IconHuge;

    public const double Narrowest = 220;

    public const double Widest = 420;

    /// <summary>[S-6754] [S-14941] What stands over the plot's top: the inset (the sentence is its own widget, [S-14942]).</summary>
    public static double Over => FamilySpace.Inset;

    /// <summary>
    /// [S-5126] Whether the window has rain to tell of (it starts, stops or goes on): only then the bars; a dry window or
    /// one too short to speak for the hour is the chart at its foot's hairline.
    /// </summary>
    public static bool Warns(RainHeadline headline) => headline is RainHeadline.StartsIn or RainHeadline.StopsIn or RainHeadline.Continues;

    /// <summary>
    /// [S-5125] [S-6754] What the one chart draws across a plot <paramref name="width"/> wide: a column per quarter hour
    /// in equal cells, each its share of the fixed scale (<see cref="RainRules.BarShare"/>; a dry one none, and with
    /// none the foot's hairline), the inset over the plot's top and the window's <paramref name="ends"/> at its foot.
    /// </summary>
    public static ChartModel Chart(IReadOnlyList<double> shares, ChartEnds ends, double width)
    {
        ArgumentNullException.ThrowIfNull(shares);
        return new ChartModel
        {
            Type = ChartType.Bars,
            Low = 0,
            High = 1,
            Over = Over,
            Ends = ends,
            Series = [.. shares.Select((share, i) => new ChartSample(HourGraphRules.X(i, shares.Count, width), share))],
        };
    }

    /// <summary>[S-11140] [S-14941] The rain graph's sizing: the inset over the columns, the ends' line under them.</summary>
    public static WidgetSizing Sizing()
    {
        var (_, foot) = ChartPlot.Keeps(Chart([], new ChartEnds(string.Empty, string.Empty), 0));
        var height = Over + Bars + foot;
        return WidgetSizing.Range(new(Narrowest, height), new(300, height), new(Widest, height));
    }
}
