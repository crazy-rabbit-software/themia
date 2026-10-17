namespace Themia.Core.Widgets;

/// <summary>
/// [S-661] Progressive disclosure: which of a widget's optional parts fit the height it has, asked
/// in priority order, so a tile shows more the bigger it is and never draws a part clipped.
/// <para>
/// A view describes its ladder top to bottom in code — the thing it must show, then each extra in
/// the order it should appear as the tile grows — and asks this budget for each one in turn:
/// </para>
/// <code>
///   var room = new Disclosure(area.Width, area.Height, gap: Fluent.Space2);
///   room.Reserve(minimumHero);                     // the part that is always there
///   var date    = room.Take(dateLine);              // then the extras, most wanted first
///   var details = area.HeightClass >= SizeClass.Expanded &amp;&amp; room.Take(detailLine);
///   var rows    = room.TakeRows(rowHeight, items.Count);  // a list takes whatever is left
/// </code>
/// <para>
/// It is a <b>ladder</b>: once one part has not fitted, no later part is shown either, even a
/// smaller one. That keeps disclosure monotonic — growing a tile only ever adds parts, in the
/// order the view listed them, and a part never appears at one size only to vanish one size up
/// because something above it arrived. A part that is skipped for lack of <i>width</i> does not
/// close the ladder: it is not competing for the height, and the parts under it still are.
/// </para>
/// <para>
/// One budget per layout pass. It is a class rather than a struct so it cannot be copied by
/// accident and have two halves spend the same room.
/// </para>
/// </summary>
public sealed class Disclosure
{
    /// <summary>Layout rounding: a part that is short by a fraction of a pixel still fits.</summary>
    public const double Tolerance = 0.5;

    private readonly double _gap;
    private int _taken;
    private bool _closed;

    /// <param name="width">The width the parts are laid out across, in DIPs.</param>
    /// <param name="height">The height they share, in DIPs.</param>
    /// <param name="gap">Space the view puts between two shown parts (a panel's spacing).</param>
    public Disclosure(double width, double height, double gap = 0)
    {
        Width = Usable(width);
        Remaining = Usable(height);
        _gap = Math.Max(0, Usable(gap));
    }

    /// <summary>Budget over a whole content area.</summary>
    public Disclosure(WidgetArea area, double gap = 0)
        : this(area.Width, area.Height, gap)
    {
    }

    public double Width { get; }

    /// <summary>Height not yet spent, never negative.</summary>
    public double Remaining { get; private set; }

    /// <summary>
    /// Spend room on a part that is always shown, whether or not it fits. It still comes out of the
    /// budget — what is left for the extras is what the essential part leaves. Returns whether it
    /// fitted, for a caller that wants to shrink it.
    /// </summary>
    public bool Reserve(double height)
    {
        var cost = Cost(height);
        var fits = cost <= Remaining + Tolerance;
        Remaining = Math.Max(0, Remaining - cost);
        _taken++;
        return fits;
    }

    /// <summary>
    /// Show an optional part if it fits whole in what is left, and spend its room if so.
    /// </summary>
    /// <param name="height">The part's height in DIPs (its line height, not its font size).</param>
    /// <param name="minWidth">The narrowest it can be and still say anything; 0 for any width.</param>
    public bool Take(double height, double minWidth = 0)
    {
        if (_closed)
        {
            return false;
        }

        // Too narrow is a property of this part, not of the ladder: the parts after it are not
        // competing for width with it, so they are still asked.
        if (minWidth > 0 && Width + Tolerance < minWidth)
        {
            return false;
        }

        var cost = Cost(height);

        if (cost > Remaining + Tolerance)
        {
            _closed = true;
            return false;
        }

        Remaining = Math.Max(0, Remaining - cost);
        _taken++;
        return true;
    }

    /// <summary>
    /// [S-1622] Whether a part of <paramref name="height"/> would fit now, without taking it or
    /// closing the ladder: for a rung that has somewhere else to go (beside another part) when it
    /// does not fit under the rest.
    /// </summary>
    public bool Fits(double height) => !_closed && Cost(height) <= Remaining + Tolerance;

    /// <summary>
    /// [S-1622] A rung offered in several sizes, richest first: take the first that fits whole and
    /// return its index. Only when none fits is it a miss, which closes the ladder like
    /// <see cref="Take"/>'s; −1 then (and when the ladder was already closed or nothing was offered).
    /// </summary>
    public int TakeFirst(IReadOnlyList<double> heights)
    {
        ArgumentNullException.ThrowIfNull(heights);

        if (_closed || heights.Count == 0)
        {
            return -1;
        }

        for (var i = 0; i < heights.Count; i++)
        {
            var cost = Cost(heights[i]);

            if (cost <= Remaining + Tolerance)
            {
                Remaining = Math.Max(0, Remaining - cost);
                _taken++;
                return i;
            }
        }

        _closed = true;
        return -1;
    }

    /// <summary>
    /// [S-662] A list's share: as many whole rows of <paramref name="rowHeight"/> as fit in what is
    /// left (at most <paramref name="wanted"/>), spent from the budget. A list is normally the last
    /// thing on a ladder and takes the rest, so the count grows with the tile and the last row is
    /// never cut in half.
    /// </summary>
    /// <param name="rowHeight">One row, in DIPs.</param>
    /// <param name="wanted">How many rows there are to show.</param>
    /// <param name="rowGap">Space between two rows.</param>
    public int TakeRows(double rowHeight, int wanted, double rowGap = 0)
    {
        if (_closed || wanted <= 0)
        {
            return 0;
        }

        var room = Remaining - (_taken > 0 ? _gap : 0);
        var rows = Math.Min(wanted, WholeRows(room, rowHeight, rowGap));

        if (rows == 0)
        {
            _closed = true;
            return 0;
        }

        Remaining = Math.Max(0, room - (rows * rowHeight) - ((rows - 1) * Math.Max(0, rowGap)));
        _taken++;
        return rows;
    }

    /// <summary>
    /// [S-662] How many whole rows of <paramref name="rowHeight"/>, <paramref name="gap"/> apart,
    /// fit in <paramref name="height"/>. Zero for a box too short for one, or one not measured yet.
    /// The single home of this arithmetic: lists, chip cells and line budgets all count rows the
    /// same way, with the same rounding tolerance.
    /// </summary>
    public static int WholeRows(double height, double rowHeight, double gap = 0)
    {
        if (!double.IsFinite(height) || !double.IsFinite(rowHeight) || rowHeight <= 0 || height <= 0)
        {
            return 0;
        }

        var step = rowHeight + Math.Max(0, double.IsFinite(gap) ? gap : 0);
        var rows = Math.Floor((height + Math.Max(0, step - rowHeight) + Tolerance) / step);
        return rows >= int.MaxValue ? int.MaxValue : (int)rows;
    }

    private double Cost(double height) => Usable(height) + (_taken > 0 ? _gap : 0);

    private static double Usable(double value) => double.IsFinite(value) && value > 0 ? value : 0;
}
