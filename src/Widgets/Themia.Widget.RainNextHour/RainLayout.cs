using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-5126] [S-11140] [S-6754] The rain atom's one layout and its size: the plot is the one chart's, the card's edge to
/// edge, a column per quarter hour on it; over its top the sentence on one Title line at the inset and its caption the
/// Body line under it, 8 to the columns; at its foot the window's two ends at the inset, 4 under the columns. One height;
/// 220..420 wide (the sentence and the caption end in their ellipsis where the card is too narrow, the columns get
/// wider).
/// </summary>
public static class RainLayout
{
    /// <summary>The columns' own height at the atom's height.</summary>
    public const double Bars = FamilyShape.IconHuge;

    public const double Narrowest = 220;

    public const double Widest = 420;

    /// <summary>[S-6754] What stands over the plot's top: the inset, the sentence's Title line, its caption's Body line and 8.</summary>
    public static double Over => FamilySpace.Inset + FamilyType.Title.LineHeight + FamilyType.Body.LineHeight + FamilySpace.S2;

    /// <summary>
    /// [S-16437] Where the sentence's Title line starts under the card's top edge: the inset less the room over its capitals,
    /// so the sentence's ink stands on the card's inset (WR-1: "there should not be a space at the top").
    /// </summary>
    public static double SentenceTop => FamilySpace.Inset - LineInk.Of(FamilyType.Title).Over;

    /// <summary>[S-16437] The sentence with no nowcast to read (WR-10: its reading as "--"), drawn unavailable.</summary>
    public const string NoAnswer = "--";

    /// <summary>
    /// [S-5126] Whether the window has rain to tell of (it starts, stops or goes on): only then the
    /// sentence, its caption and the bars; a dry window or one too short to speak for the hour is the
    /// Status row's one plain sentence.
    /// </summary>
    public static bool Warns(RainHeadline headline) => headline is RainHeadline.StartsIn or RainHeadline.StopsIn or RainHeadline.Continues;

    /// <summary>
    /// [S-5125] [S-6754] What the one chart draws across a plot <paramref name="width"/> wide: a column per quarter hour
    /// in equal cells, each its share of the fixed scale (<see cref="RainRules.BarShare"/>; a dry one none), the sentence
    /// over the plot's top and the window's <paramref name="ends"/> at its foot. [S-16436] Every quarter stands in its
    /// track from the band's top to the card's foot, a dry one its track alone, the ends written over the tracks' feet.
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
            Tracks = true,
            Series = [.. shares.Select((share, i) => new ChartSample(HourGraphRules.X(i, shares.Count, width), share))],
        };
    }

    /// <summary>[S-11140] The rain's sizing: the sentence and its caption over the columns, the ends' line under them.</summary>
    public static WidgetSizing Sizing()
    {
        var (_, foot) = ChartPlot.Keeps(Chart([], new ChartEnds(string.Empty, string.Empty), 0));
        var height = Over + Bars + foot;
        return WidgetSizing.Range(new(Narrowest, height), new(300, height), new(Widest, height));
    }
}
