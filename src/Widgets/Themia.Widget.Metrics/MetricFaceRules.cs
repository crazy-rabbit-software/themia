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

    /// <summary>[S-16903] The glyph and name at the left and the value at the right, on one line centred down the card: no graph.</summary>
    Value = 1,
}

/// <summary>
/// [S-16321] The graph face at one size: the scale its head is drawn at, where the head's box stands (its first ink on the
/// inset), its height, where the plot starts, and whether the plot stands behind the head (a card too low for both).
/// </summary>
public readonly record struct GraphFace(double Scale, double HeadTop, double HeadHeight, double PlotTop, bool Behind);

/// <summary>[S-16903] The value face at one size: the scale of its row (the head's) and where the row's box stands.</summary>
public readonly record struct ValueFace(double Scale, double Left, double Top);

/// <summary>
/// [S-16321]..[S-16324] The face's rules, pure: one arrangement at every size (owner: "it should scale to the size properly but
/// it should not change its layout drastically"), its head scaled with the card, the graph under it to the card's sides and
/// foot, and behind it where the card is too low for both (owner: "the graph should still be shown the full width but it
/// should go under / behind the text"); the value style the head alone, one row centred down the card; and the sizes each holds.
/// </summary>
public static class MetricFaceRules
{
    /// <summary>[S-16321] The card the head is drawn at its own size in: 240 × 120.</summary>
    public const double NaturalWidth = 240;

    public const double NaturalHeight = 120;

    /// <summary>
    /// [S-16321] The head's least scale: its smallest text (the name, a Body line) at the 9 DIP the family reads to (WR-14: "for
    /// the widgets that can scale there should basically be no lower limit except for one that makes sense so you can still see
    /// the widget"); its glyph is held at its own floor under that (<see cref="GlyphSize"/>). And its most.
    /// </summary>
    public static double LeastScale => ScaleFloor.Least(FamilyType.BodySize);

    public const double MostScale = 2.5;

    /// <summary>[S-16321] The least share of its Title line the head's reading is drawn at: the name's size (Body over Title).</summary>
    public static double LeastShare => FamilyType.BodySize / FamilyType.TitleSize;

    /// <summary>
    /// [S-16321] The share of its Title line the head's reading is drawn at: whole where the name (whole) and the metric's widest
    /// reading fit side by side in <paramref name="room"/> (the head's width less its glyph and gaps), else as much as leaves the
    /// name its room, down to the name's own size (<see cref="LeastShare"/>), so the name keeps its room first (WR-7). Decided on
    /// the widest reading, never the one shown (WR-15), and changing with the room smoothly, never by a step.
    /// </summary>
    public static double ReadingShare(double room, double name, double widestAtTitle)
        => widestAtTitle > 0 ? Math.Clamp((room - name) / widestAtTitle, LeastShare, 1) : 1;

    /// <summary>
    /// [S-16325] The graph face's least width for a name and a widest reading of these widths (at the head's own size, a
    /// Title reading): both whole side by side at the head's least scale (text at 9 DIP, the glyph at 12) with the reading at
    /// its least share, the status mark's room and the insets round them; never under the grid's least widget.
    /// </summary>
    public static double GraphLeastWidth(double name, double widestAtTitle)
    {
        var k = LeastScale;
        var head = GlyphSize(k) + FamilySpace.GlyphGap + name + FamilySpace.ColumnGap + (widestAtTitle * LeastShare);
        return Math.Max(WidgetSizing.LeastScaled, Math.Ceiling((2 * FamilySpace.Inset) + (head * k) + MarkRoom));
    }

    /// <summary>
    /// [S-16325] [S-16327] The room the status's mark (its glyph) takes at the head's end, unscaled: the least head keeps the name
    /// whole beside it too, where a source fails.
    /// </summary>
    public const double MarkRoom = FamilyShape.Icon + FamilySpace.S2 + FamilySpace.GlyphGap;

    /// <summary>
    /// [S-16325] How much wider a text may draw than the family's width estimate says (its average advance, 0.58 em, against
    /// capitals' up to about 0.72 em): the sizing's estimate of a name or a word times this, so it is whole at the least.
    /// </summary>
    public const double EstimateSlack = 1.25;

    /// <summary>[S-16325] A name's or a word's width for the sizing: the family's estimate with its slack.</summary>
    public static double WordWidth(TypeStep step, int characters) => FamilyType.EstimateWidth(step, characters) * EstimateSlack;

    /// <summary>
    /// [S-16321] The head's glyph at scale <paramref name="scale"/>: its 16 DIP, drawn no smaller than the 12 a glyph is read to
    /// (WR-14), so under 0.75 it is drawn larger in the head to stay 12 on the card.
    /// </summary>
    public static double GlyphSize(double scale)
        => FamilyShape.Icon * Math.Max(1, ScaleFloor.Glyph / FamilyShape.Icon / Math.Max(1e-6, scale));

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

    /// <summary>[S-16903] The head's ink down at its own size: the line less the room over its first ink and under its last.</summary>
    public static double HeadInkHeight => Head.LineHeight - HeadInk.Over - HeadInk.Under;

    /// <summary>
    /// [S-16903] The value face's row at its own size for a name and a widest reading of these widths (the reading a Title, whole):
    /// the glyph, the name, the reading's held box and the gaps between them.
    /// </summary>
    public static double RowWidth(double name, double widestAtTitle)
        => FamilyShape.Icon + FamilySpace.GlyphGap + name + FamilySpace.ColumnGap + widestAtTitle;

    /// <summary>
    /// [S-16903] The value face (owner, 2026-11-23: "when only the metric value is shown it should show the title one the left
    /// and metric on the right. left and right aligned respectively."): the head's one row, the glyph's ink and the name from
    /// the left inset, the reading's held box's ink ending on the right inset (<paramref name="room"/> the status mark's beside
    /// it), scaled as one until the width (the row <paramref name="row"/> wide at its own size, less the room beside its ink,
    /// <paramref name="inkLeft"/> and <paramref name="inkRight"/>) or the height (its ink, <paramref name="ink"/> the room over
    /// and under it in its line, between the insets) stops it, never under the head's least; its ink centred down the card.
    /// What the width leaves stands between the name and the reading.
    /// </summary>
    public static ValueFace Row(double width, double height, double row, double room = 0, LineInk? ink = null, double inkLeft = 0, double inkRight = 0)
    {
        if (!(width > 0) || !(height > 0))
        {
            return new ValueFace(1, FamilySpace.Inset, FamilySpace.Inset);
        }

        var line = ink ?? HeadInk;
        var inkHeight = Math.Max(1, Head.LineHeight - line.Over - line.Under);
        var across = Math.Max(1, width - (2 * FamilySpace.Inset) - room) / Math.Max(1, row - inkLeft - inkRight);
        var down = Math.Max(1, height - (2 * FamilySpace.Inset)) / inkHeight;
        var k = Math.Max(LeastScale, Math.Min(across, down));
        var top = ((height - (inkHeight * k)) / 2) - (line.Over * k);
        return new ValueFace(k, FamilySpace.Inset - (inkLeft * k), top);
    }

    /// <summary>[S-16325] Where a new value face lands at least: 200 wide.</summary>
    public const double ValueNaturalWidth = 200;

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
    /// [S-16325] The sizes a style holds for a metric whose name and widest reading are <paramref name="name"/> and
    /// <paramref name="widest"/> wide at the face's own sizes (a Body name, a Title reading): its least where both read whole at
    /// the 9 DIP text floor and the 12 DIP glyph beside the status mark's room (WR-14: no lower limit but one where the widget is
    /// still read; the grid's least widget the floor), its natural the face at its own size (the graph's 240 × 120 at least; the
    /// value's row one line tall in its insets, 200 wide at least). Relaxed past that by the host.
    /// </summary>
    public static WidgetSizing Sizing(MetricsStyle style, double name, double widest)
    {
        if (style != MetricsStyle.Value)
        {
            var least = GraphLeastWidth(name, widest);
            var natural = Math.Max(NaturalWidth, Math.Ceiling(least / LeastScale));
            return WidgetSizing.Range(new(least, WidgetSizing.LeastScaled), new(natural, NaturalHeight), new(natural * 2, NaturalHeight * 2));
        }

        // [S-16903] The value face is the head's row alone: as wide as the graph's least, as tall as its ink at the least scale.
        var min = new SizeDip(GraphLeastWidth(name, widest), Math.Max(WidgetSizing.LeastScaled, Math.Ceiling((2 * FamilySpace.Inset) + (HeadInkHeight * LeastScale))));
        var own = new SizeDip(
            Math.Max(min.Width, Math.Max(ValueNaturalWidth, Math.Ceiling((2 * FamilySpace.Inset) + RowWidth(name, widest) + MarkRoom))),
            Math.Max(min.Height, Math.Ceiling((2 * FamilySpace.Inset) + Head.LineHeight)));
        // [S-17100] The row scales as one until the width or its height stops it: a card no taller than the row's shape fills
        // at its width (the room between the name and the value on a wider one is the owner's).
        return WidgetSizing.Range(min, own, new(own.Width * 2, own.Height * 2)).ScalesAsOne(new SizeDip(RowWidth(name, widest) + MarkRoom, Head.LineHeight));
    }
}
