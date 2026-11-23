using System.Text.Json.Serialization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Widget.Metrics;

/// <summary>[S-16320] The widget's two styles: the graph (the default) and the value alone, a design of its own.</summary>
[JsonConverter(typeof(StyleJsonConverter<MetricsStyle>))]
public enum MetricsStyle
{
    /// <summary>The name and glyph top left, the value top right, the graph under them to the card's edges and foot.</summary>
    Graph = 0,

    /// <summary>The glyph and name over the value, one block scaled to fill the card: no graph.</summary>
    Value = 1,
}

/// <summary>
/// [S-16321] The graph face at one size: the scale its head is drawn at, where the head's box stands (its first ink on the
/// inset), its height, where the plot starts, and whether the plot stands behind the head (a card too low for both).
/// </summary>
public readonly record struct GraphFace(double Scale, double HeadTop, double HeadHeight, double PlotTop, bool Behind);

/// <summary>[S-16323] The value face at one size: the scale of its block and where the block's box stands.</summary>
public readonly record struct ValueFace(double Scale, double Left, double Top);

/// <summary>
/// [S-16321]..[S-16324] The face's rules, pure: one arrangement at every size (owner: "it should scale to the size properly but
/// it should not change its layout drastically"), its head scaled with the card, the graph under it to the card's sides and
/// foot, and behind it where the card is too low for both (owner: "the graph should still be shown the full width but it
/// should go under / behind the text"); the value style a block of its own; and the sizes each holds.
/// </summary>
public static class MetricFaceRules
{
    /// <summary>[S-16321] The card the head is drawn at its own size in: 240 × 120.</summary>
    public const double NaturalWidth = 240;

    public const double NaturalHeight = 120;

    /// <summary>
    /// [S-16325] The graph style's least width: where, at the head's least scale, a short name ("CPU", "UV index") and the
    /// widest of its readings read whole side by side, a long one cut meaningfully (WR-14).
    /// </summary>
    public const double LeastWidth = 180;

    /// <summary>
    /// [S-16321] The head's least scale: its smallest text (the name, a Body line) at the 9 DIP the family reads to (WR-14: "for
    /// the widgets that can scale there should basically be no lower limit except for one that makes sense so you can still see
    /// the widget"); its glyph is held at its own floor under that (<see cref="GlyphSize"/>). And its most.
    /// </summary>
    public static double LeastScale => ScaleFloor.Least(FamilyType.BodySize);

    public const double MostScale = 2.5;

    /// <summary>
    /// [S-16321] The reading's line in the head: Title where the name (whole) and the metric's widest reading fit side by side in
    /// <paramref name="room"/> (the head's width less its glyph and gaps), else the name's own size, bold, so the name keeps its
    /// room first (WR-7: the caption and its reading together). Decided on the widest reading, never the one shown: a running
    /// value never changes it (WR-15).
    /// </summary>
    public static TypeStep ReadingStep(double room, double name, double widestAtTitle)
        => name + widestAtTitle <= room ? Head : FamilyType.BodyStrong;

    /// <summary>
    /// [S-16321] The head's glyph at scale <paramref name="scale"/>: its 16 DIP, drawn no smaller than the 12 a glyph is read to
    /// (WR-14), so under 0.75 it is drawn larger in the head to stay 12 on the card.
    /// </summary>
    public static double GlyphSize(double scale)
        => FamilyShape.Icon * Math.Max(1, ScaleFloor.Glyph / FamilyShape.Icon / Math.Max(1e-6, scale));

    /// <summary>[S-16323] The value block's least: its caption a Body line at 9.1, the family's least read (WR-14).</summary>
    public const double ValueLeast = 0.65;

    /// <summary>[S-16322] The least the graph keeps under the head before it goes behind it: under this the card is too low for both.</summary>
    public const double LeastPlot = 24;

    /// <summary>[S-16322] How much of the graph shows behind the head: a little (owner: "the graph should only be a little visible behind the text").</summary>
    public const double BehindShare = 0.32;

    /// <summary>[S-16321] The head's line: the value's (a Title line), the glyph and the name standing on its baseline.</summary>
    public static TypeStep Head => FamilyType.Title;

    /// <summary>[S-16321] The name's line.</summary>
    public static TypeStep Name => FamilyType.Body;

    /// <summary>[S-16321] How far the name's box stands under the head's top so both baselines are one.</summary>
    public static double NameDrop => (Head.LineHeight * LineInk.FaceBaseline) - (Name.LineHeight * LineInk.FaceBaseline);

    /// <summary>[S-16321] The head's ink over its box: the value's capitals or the glyph centred on the line, whichever is higher.</summary>
    public static LineInk HeadInk => LineInk.Of(Head).With(FamilyShape.Icon, Head.LineHeight);

    /// <summary>[S-16321] The head's scale on a card: the card's against the natural, the smaller axis, within the least and the most.</summary>
    public static double Scale(double width, double height)
    {
        if (!(width > 0) || !(height > 0))
        {
            return 1;
        }

        return Math.Clamp(Math.Min(width / NaturalWidth, height / NaturalHeight), LeastScale, MostScale);
    }

    /// <summary>
    /// [S-16321] [S-16322] The graph face: the head at its scale with its first ink on the card's inset (WR-1), the plot from a
    /// small gap under it to the card's foot, side to side. Where that leaves the plot less than <see cref="LeastPlot"/>, the
    /// plot is the card's whole box behind the head (<see cref="GraphFace.Behind"/>) and the head stands in the middle of a
    /// card too low for its inset over and under it. With no graph to draw (<paramref name="graph"/> false) the head alone.
    /// </summary>
    public static GraphFace Graph(double width, double height, bool graph = true)
    {
        var k = Scale(width, height);
        var line = Head.LineHeight * k;
        var top = FamilySpace.Inset - (HeadInk.Over * k);
        var plot = top + line + (FamilySpace.S1 * k);
        var behind = graph && height - plot < LeastPlot;
        if (behind)
        {
            // The ink over and under the head alike where the inset does not fit over and under it.
            var ink = line - ((HeadInk.Over + HeadInk.Under) * k);
            top = Math.Min(top, ((height - ink) / 2) - (HeadInk.Over * k));
            plot = 0;
        }

        return new GraphFace(k, top, line, plot, behind);
    }

    /// <summary>
    /// [S-16323] The value face: its block (the caption over the value, <paramref name="blockWidth"/> × <paramref name="blockHeight"/>
    /// at scale 1, <paramref name="ink"/> the room over its first ink and under its last, <paramref name="inkLeft"/> and
    /// <paramref name="inkRight"/> the room beside it) scaled as one until the width or the
    /// height stops it, never under <see cref="ValueLeast"/>; its ink on the insets of the axis that stops it, centred on the
    /// other (the owner's "Centred is fine").
    /// </summary>
    public static ValueFace Value(double width, double height, double blockWidth, double blockHeight, LineInk ink, double inkLeft = 0, double inkRight = 0)
    {
        var inkHeight = Math.Max(1, blockHeight - ink.Over - ink.Under);
        var inkWidth = Math.Max(1, blockWidth - inkLeft - inkRight);
        var across = Math.Max(1, width - (2 * FamilySpace.Inset)) / inkWidth;
        var down = Math.Max(1, height - (2 * FamilySpace.Inset)) / inkHeight;
        var k = Math.Max(ValueLeast, Math.Min(across, down));
        var left = ((width - (inkWidth * k)) / 2) - (inkLeft * k);
        var top = ((height - (inkHeight * k)) / 2) - (ink.Over * k);
        return new ValueFace(k, left, top);
    }

    /// <summary>
    /// [S-16324] The text a value is measured by: every figure an 8 (the widest), so a running reading's block keeps its size
    /// while its figures change (WR-15).
    /// </summary>
    public static string Template(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return string.Create(text.Length, text, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = char.IsAsciiDigit(source[i]) ? '8' : source[i];
            }
        });
    }

    /// <summary>
    /// [S-16325] The sizes a style holds: the graph's least where the name and the value still read over the graph behind them
    /// (the head at its least scale, its text at 9 DIP), as low as the grid's least widget (WR-14); its natural the head at its own size over a plot three times its
    /// height; the value's least its block at its least scale, as wide as the catalog's widest reading needs there (a pair's
    /// two, "888 / 888 MB/s", <see cref="WidestReading"/>: one least for every metric, so a card switched to another metric reads
    /// in the box it has), its natural the block at its own. On the 10-DIP grid across; relaxed past that by the host.
    /// </summary>
    public static WidgetSizing Sizing(MetricsStyle style) => Sizing(style, WidestReading);

    /// <summary>[S-16325] The longest of every metric's widest readings, in characters (English).</summary>
    public static int WidestReading { get; } = MetricCatalog.All
        .SelectMany(m => m.Feeds.Select(f => MetricFormat.Widest(Themia.Core.Localization.Locale.En, m, f.Shape).Length))
        .Max();

    /// <summary>[S-16325] <see cref="Sizing(MetricsStyle)"/> for a widest reading of <paramref name="widest"/> characters.</summary>
    public static WidgetSizing Sizing(MetricsStyle style, int widest)
    {
        if (style != MetricsStyle.Value)
        {
            return WidgetSizing.Range(new(LeastWidth, WidgetSizing.LeastScaled), new(NaturalWidth, NaturalHeight), new(480, 240));
        }

        var block = FamilyType.EstimateWidth(FamilyType.Big28, Math.Max(1, widest));
        double Whole(double length) => Math.Ceiling(length / ItemGrid.DefaultCell) * ItemGrid.DefaultCell;
        var least = Math.Max(140, Whole((2 * FamilySpace.Inset) + (block * ValueLeast)));
        var natural = Math.Max(200, Whole((2 * FamilySpace.Inset) + block));
        return WidgetSizing.Range(new(least, 60), new(natural, 90), new(natural * 2, 180));
    }
}
