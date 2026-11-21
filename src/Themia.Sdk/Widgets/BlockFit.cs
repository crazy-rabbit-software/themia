using Themia.Core.Model;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>[S-12808] How a one-readout block stands in its room: the scale it is drawn at and where its box's top goes.</summary>
public readonly record struct BlockLay(double Scale, double Top);

/// <summary>
/// [S-12808] The one rule for a one-readout block (owner, 2026-11-19: "Keep them together"). The block is planned in
/// the room its kind's natural box gives it, its parts together at their natural spacing; a larger room scales the
/// plan up as one, as the clock's face ([S-12804]), until its ink (<paramref name="ink"/>: the room in the plan over
/// its first ink and under its last) fills the room's height, its first ink on the content's top and its last on the
/// foot's inset, or until the room's width stops it (the plan's width grown with it), whichever comes first; where
/// the width stops it, the block stands centred on its ink in the height that is left. The width is the natural
/// room's, so at its natural box nothing scales. [S-12833] A smaller room scales it down as one, never under its least
/// scale (its smallest text at the legibility floor, <see cref="ScaleFloor"/>); there its ink is centred (its first ink
/// on the top when it does not fit).
/// </summary>
public static class BlockFit
{
    /// <summary>
    /// A block planned <paramref name="naturalWidth"/> × <paramref name="naturalHeight"/>, its ink inside by
    /// <paramref name="ink"/>, in a room <paramref name="width"/> × <paramref name="height"/>, never drawn under
    /// <paramref name="least"/> ([S-12833]: its smallest text at the legibility floor, <see cref="ScaleFloor"/>).
    /// </summary>
    public static BlockLay Lay(double width, double height, double naturalWidth, double naturalHeight, LineInk ink = default, double least = 1)
        => !(naturalHeight > 0) || !double.IsFinite(naturalHeight) ? new BlockLay(1, 0)
            : Lay(width, height, naturalWidth, double.IsFinite(ink.Over) ? ink.Over : 0, naturalHeight - (double.IsFinite(ink.Under) ? ink.Under : 0), least);

    /// <summary>
    /// A block planned <paramref name="naturalWidth"/> wide whose ink runs from <paramref name="inkTop"/> to
    /// <paramref name="inkBottom"/> in its plan (either may lie outside the plan's own box: parts that did not fit it).
    /// </summary>
    public static BlockLay Lay(double width, double height, double naturalWidth, double inkTop, double inkBottom, double least = 1)
    {
        if (!(naturalWidth > 0) || !double.IsFinite(naturalWidth) || !double.IsFinite(inkTop) || !double.IsFinite(inkBottom)
            || !(width > 0) || !(height > 0) || !double.IsFinite(width) || !double.IsFinite(height))
        {
            return new BlockLay(1, 0);
        }

        var span = inkBottom - inkTop;
        var fill = Math.Min(width / naturalWidth, span > 0 ? height / span : 1);
        var scale = Math.Max(ScaleFloor.Clamp(least), fill);
        var top = (height - (span * scale)) / 2;

        // Taller than the room even at its own size: its first ink on the top.
        return new BlockLay(scale, Math.Max(0, top) - (inkTop * scale));
    }

    /// <summary>
    /// The DIP a block's parts are given over their width (before the scale): laid out at exactly their width, layout
    /// rounding would trim a text to its ellipsis (the weather's "Can't reach the servi..."). [S-14301] It is laid beyond
    /// the room (<see cref="Across"/>), never in the scale, so a block the width holds reaches both side insets with its ink.
    /// </summary>
    public const double WidthSlack = 1;

    /// <summary>
    /// [S-14301] Where a block's plan stands across a room <paramref name="width"/> wide drawn at <paramref name="scale"/>:
    /// its box's <c>Left</c> in the room (DIPs) and its <c>Width</c> in the plan's own units. The plan is the room's width
    /// and <see cref="WidthSlack"/> more, the slack laid outside the room on the side its alignment leaves free (after the
    /// end for Left, before the start for Right, half each side centred), so a part at the start stands on the room's start,
    /// one at the end on its end and one centred on its middle (owner, 2026-11-20, of a reading that fills one axis:
    /// "Centred is fine"; WR-3 on the axis it fills: a block the width holds, its ink on both side insets).
    /// </summary>
    public static (double Left, double Width) Across(double width, double scale, ContentAlign align)
    {
        if (!(width > 0) || !double.IsFinite(width) || !(scale > 0) || !double.IsFinite(scale))
        {
            return (0, Math.Max(0, double.IsFinite(width) ? width : 0) + WidthSlack);
        }

        var over = WidthSlack * scale;
        var left = align switch
        {
            ContentAlign.Left => 0,
            ContentAlign.Right => -over,
            _ => -over / 2,
        };
        return (left, (width / scale) + WidthSlack);
    }
}
