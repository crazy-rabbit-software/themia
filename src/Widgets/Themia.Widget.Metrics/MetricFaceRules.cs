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

    /// <summary>[S-16321] The head's least scale (its name a Body line at 10.5, over the 9 the family reads to, WR-14) and its most.</summary>
    public const double LeastScale = 0.75;

    public const double MostScale = 2.5;

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
    /// at scale 1, <paramref name="ink"/> the room over its first ink and under its last) scaled as one until the width or the
    /// height stops it, never under <see cref="LeastScale"/>; its ink on the insets of the axis that stops it, centred on the
    /// other (the owner's "Centred is fine").
    /// </summary>
    public static ValueFace Value(double width, double height, double blockWidth, double blockHeight, LineInk ink)
    {
        var inkHeight = Math.Max(1, blockHeight - ink.Over - ink.Under);
        var across = Math.Max(1, width - (2 * FamilySpace.Inset)) / Math.Max(1, blockWidth);
        var down = Math.Max(1, height - (2 * FamilySpace.Inset)) / inkHeight;
        var k = Math.Max(LeastScale, Math.Min(across, down));
        var left = (width - (blockWidth * k)) / 2;
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
    /// [S-16325] The sizes a style holds, on the 10-DIP grid: the graph's least where the name, the value and a sliver of graph
    /// behind them still read (a head at its least scale), its natural the head at its own size over a plot three times its
    /// height; the value's least its block at its least scale, its natural the block at its own. Relaxed past that by the host.
    /// </summary>
    public static WidgetSizing Sizing(MetricsStyle style) => style switch
    {
        MetricsStyle.Value => WidgetSizing.Range(new(120, 60), new(200, 90), new(400, 180)),
        _ => WidgetSizing.Range(new(160, 40), new(NaturalWidth, NaturalHeight), new(480, 240)),
    };
}
