using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>[S-12808] How a one-readout block stands in its room: the scale it is drawn at and where its box's top goes.</summary>
public readonly record struct BlockLay(double Scale, double Top);

/// <summary>
/// [S-12814] How a block of parts fills its room: the <see cref="Scale"/> it is drawn at, the <see cref="Height"/> it is
/// laid out in (before the scale: the room's, shrunk by it, plus the first part's leading over its ink and the last
/// part's under it) and where its box's <see cref="Top"/> goes (the leading over the first ink above the room).
/// </summary>
public readonly record struct BlockFill(double Scale, double Height, double Top);

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
    /// <paramref name="least"/> ([S-12833]: its smallest text at the legibility floor, <see cref="ScaleFloor"/>) and never over
    /// <paramref name="most"/> (<see cref="FaceFit.Most"/>: the scale a part of it gave way at).
    /// </summary>
    public static BlockLay Lay(double width, double height, double naturalWidth, double naturalHeight, LineInk ink = default, double least = 1, double most = double.PositiveInfinity)
        => !(naturalHeight > 0) || !double.IsFinite(naturalHeight) ? new BlockLay(1, 0)
            : Lay(width, height, naturalWidth, double.IsFinite(ink.Over) ? ink.Over : 0, naturalHeight - (double.IsFinite(ink.Under) ? ink.Under : 0), least, most);

    /// <summary>
    /// A block planned <paramref name="naturalWidth"/> wide whose ink runs from <paramref name="inkTop"/> to
    /// <paramref name="inkBottom"/> in its plan (either may lie outside the plan's own box: parts that did not fit it).
    /// </summary>
    public static BlockLay Lay(double width, double height, double naturalWidth, double inkTop, double inkBottom, double least = 1, double most = double.PositiveInfinity)
    {
        if (!(naturalWidth > 0) || !double.IsFinite(naturalWidth) || !double.IsFinite(inkTop) || !double.IsFinite(inkBottom)
            || !(width > 0) || !(height > 0) || !double.IsFinite(width) || !double.IsFinite(height))
        {
            return new BlockLay(1, 0);
        }

        var span = inkBottom - inkTop;
        var fill = Math.Min(width / naturalWidth, span > 0 ? height / span : 1);
        var scale = Math.Max(ScaleFloor.Clamp(least), most > 0 && !double.IsNaN(most) ? Math.Min(most, fill) : fill);
        var top = (height - (span * scale)) / 2;

        // Taller than the room even at its own size: its first ink on the top.
        return new BlockLay(scale, Math.Max(0, top) - (inkTop * scale));
    }

    /// <summary>The DIP a block's parts are given over their width when the width holds its scale.</summary>
    public const double WidthSlack = 1;

    /// <summary>
    /// [S-12814] The rule for a one-readout block (owner, 2026-11-19: "the drive widget still has a fucking space at the
    /// top, maybe also at the bottom. make sure that this does not happen anywhere"). Only the parts with a width of
    /// their own count against the room's width (<paramref name="intrinsicWidth"/>: the reading, its caption, an icon;
    /// a bar, a slider, a graph spans whatever it is given). The block scales up as one to the largest size at which
    /// those parts fit the width and the parts packed together (<paramref name="packedHeight"/>, their ink
    /// <paramref name="ink"/> inside it) fit the height. It is then laid out in the whole height the scale leaves it,
    /// its first ink on the room's top and its last on its foot: what the parts do not take goes between them, never
    /// over the first ink or under the last. Never smaller than its own size: a room shorter than the parts keeps them
    /// packed from the top, the first ink on it. [S-12813] A block showing a placeholder ("--") is sized by the ink
    /// of the reading it stands for (<paramref name="ink"/>) and placed by the ink it draws (<paramref name="drawn"/>,
    /// the dashes' top lower than capitals): the same size as its readings, its dashes on the top.
    /// </summary>
    public static BlockFill Fill(double width, double height, double intrinsicWidth, double packedHeight, LineInk ink, LineInk? drawn = null, double least = 1)
    {
        static double Room(double v) => double.IsFinite(v) ? Math.Max(0, v) : 0;
        var over = Room(ink.Over);
        var under = Room(ink.Under);
        var placedOver = Room((drawn ?? ink).Over);
        var placedUnder = Room((drawn ?? ink).Under);
        if (!(packedHeight > 0) || !double.IsFinite(packedHeight) || !(width > 0) || !(height > 0) || !double.IsFinite(width) || !double.IsFinite(height))
        {
            return new BlockFill(1, double.IsFinite(packedHeight) ? Math.Max(0, packedHeight) : 0, -placedOver);
        }

        var span = Math.Max(1e-6, packedHeight - over - under);
        // A whole pixel of slack over the parts' width: laid out at exactly their width, layout rounding would trim a
        // text to its ellipsis (the weather's "Can't reach the servi...").
        var byWidth = intrinsicWidth > 0 && double.IsFinite(intrinsicWidth) ? width / (intrinsicWidth + WidthSlack) : double.PositiveInfinity;
        var scale = Math.Max(ScaleFloor.Clamp(least), Math.Min(byWidth, height / span));
        var laid = Math.Max(packedHeight, (height / scale) + placedOver + placedUnder);
        return new BlockFill(scale, laid, -placedOver * scale);
    }
}
