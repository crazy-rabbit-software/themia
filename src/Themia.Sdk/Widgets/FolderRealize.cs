namespace Themia.Core.Widgets;

/// <summary>
/// [S-1887] How many of a folder's items the folder widget builds: the ones in view and one more
/// viewport below, never all of them.
/// <para>
/// Every row or tile is a small tree of XAML elements, and at about 150 KB of native memory each a
/// folder of 140 items shown at the seven showcase sizes cost 170 MB, for a handful of rows anyone
/// could see. Rows have a fixed height (tiles a fixed cell), so the list's full height is known
/// without building it: what is not built yet is a spacer of exactly that height, the scroll range
/// is right from the start, and the items are built in order, whole rows at a time, as the view
/// scrolls toward them. Nothing built is taken down again until the next full redraw, so the
/// selection, the keyboard cursor and the drag that hold an item never see it vanish.
/// </para>
/// </summary>
public static class FolderRealize
{
    /// <summary>How far below the visible part to build ahead, in viewports.</summary>
    public const double Lookahead = 1.0;

    /// <summary>The viewport assumed before the list has been measured, in rows.</summary>
    public const int UnmeasuredRows = 12;

    /// <summary>
    /// How many items to have built (a count from the first), given the list scrolled to
    /// <paramref name="offset"/> with <paramref name="viewport"/> DIPs in view, items
    /// <paramref name="itemHeight"/> tall laid <paramref name="columns"/> to a row. At least
    /// <paramref name="realized"/> (nothing is taken back) and through <paramref name="focus"/> (the
    /// keyboard cursor's item, or -1), at most <paramref name="shown"/>.
    /// </summary>
    public static int Target(int shown, int realized, double offset, double viewport, double itemHeight, int columns, int focus)
    {
        if (shown <= 0)
        {
            return 0;
        }

        var perRow = Math.Max(1, columns);
        var rowHeight = double.IsFinite(itemHeight) && itemHeight > 0 ? itemHeight : 1;
        var view = double.IsFinite(viewport) && viewport > 0 ? viewport : rowHeight * UnmeasuredRows;
        var top = double.IsFinite(offset) && offset > 0 ? offset : 0;

        var bottom = top + (view * (1 + Lookahead));
        var rows = (long)Math.Ceiling(bottom / rowHeight);
        var wanted = rows * perRow;

        // The cursor's row whole, so the item it is on is never the last half-built one.
        if (focus >= 0)
        {
            wanted = Math.Max(wanted, ((focus / perRow) + 1L) * perRow);
        }

        wanted = Math.Max(wanted, Math.Max(0, realized));
        return (int)Math.Min(wanted, shown);
    }

    /// <summary>
    /// The height of everything not built yet: the rows after the last built one. Zero once all
    /// <paramref name="shown"/> items are built.
    /// </summary>
    public static double SpacerHeight(int shown, int realized, double itemHeight, int columns)
    {
        if (shown <= 0 || realized >= shown || !double.IsFinite(itemHeight) || itemHeight <= 0)
        {
            return 0;
        }

        var perRow = Math.Max(1, columns);
        var allRows = (shown + perRow - 1) / perRow;
        var builtRows = (Math.Max(0, realized) + perRow - 1) / perRow;
        return (allRows - builtRows) * itemHeight;
    }
}
