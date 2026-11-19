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
/// room's, so at its natural box nothing scales. It never shrinks: a room smaller than the natural one keeps the
/// block at its own size, its ink centred (its first ink on the top when it does not fit).
/// </summary>
public static class BlockFit
{
    /// <summary>A block planned <paramref name="naturalWidth"/> × <paramref name="naturalHeight"/>, its ink inside by <paramref name="ink"/>, in a room <paramref name="width"/> × <paramref name="height"/>.</summary>
    public static BlockLay Lay(double width, double height, double naturalWidth, double naturalHeight, LineInk ink = default)
        => !(naturalHeight > 0) || !double.IsFinite(naturalHeight) ? new BlockLay(1, 0)
            : Lay(width, height, naturalWidth, double.IsFinite(ink.Over) ? ink.Over : 0, naturalHeight - (double.IsFinite(ink.Under) ? ink.Under : 0));

    /// <summary>
    /// A block planned <paramref name="naturalWidth"/> wide whose ink runs from <paramref name="inkTop"/> to
    /// <paramref name="inkBottom"/> in its plan (either may lie outside the plan's own box: parts that did not fit it).
    /// </summary>
    public static BlockLay Lay(double width, double height, double naturalWidth, double inkTop, double inkBottom)
    {
        if (!(naturalWidth > 0) || !double.IsFinite(naturalWidth) || !double.IsFinite(inkTop) || !double.IsFinite(inkBottom)
            || !(width > 0) || !(height > 0) || !double.IsFinite(width) || !double.IsFinite(height))
        {
            return new BlockLay(1, 0);
        }

        var span = inkBottom - inkTop;
        var scale = Math.Max(1, Math.Min(width / naturalWidth, span > 0 ? height / span : 1));
        var top = (height - (span * scale)) / 2;

        // Taller than the room even at its own size: its first ink on the top.
        return new BlockLay(scale, Math.Max(0, top) - (inkTop * scale));
    }
}
