using Themia.Core.Geometry;

namespace Themia.Sdk;

/// <summary>
/// [S-10902] [S-13008] A fence's own small grid, as pure layout: the title bar on top, then its items in the desktop's
/// one cell (<see cref="DesktopCell"/>, at the icon size in force) in reading order, as many to a row as the body's width
/// holds, the body scrolling when they are more than it shows. The view draws by it and the host sizes a new fence by it,
/// so the two never disagree on where an item is.
/// </summary>
public static class FenceGrid
{
    /// <summary>The title bar's height, in DIPs: the fence's own, never the Header widget's.</summary>
    public const double TitleHeight = 32;

    /// <summary>The body's inset round its cells, every side.</summary>
    public const double Pad = 4;

    /// <summary>The fewest columns a fence made round a selection is laid out with, and the most.</summary>
    public const int MinColumns = 1;

    public const int MaxColumns = 8;

    /// <summary>How many cells of <paramref name="cell"/> a body <paramref name="width"/> wide holds across: at least one.</summary>
    public static int Columns(double width, SizeDip cell)
        => cell.Width <= 0 || !double.IsFinite(width) ? 1 : Math.Max(1, (int)Math.Floor((width - (2 * Pad)) / cell.Width));

    /// <summary>How many rows <paramref name="count"/> items take in <paramref name="columns"/>.</summary>
    public static int Rows(int count, int columns) => count <= 0 ? 0 : ((count - 1) / Math.Max(1, columns)) + 1;

    /// <summary>
    /// The cell of item <paramref name="index"/> in the body's own DIPs (its top-left the body's, under the title bar):
    /// reading order, <paramref name="columns"/> to a row, the row centred in a body <paramref name="width"/> wide.
    /// </summary>
    public static PixelRect Cell(int index, int columns, SizeDip cell, double width)
    {
        columns = Math.Max(1, columns);
        var used = columns * cell.Width;
        var left = Math.Max(Pad, (width - used) / 2);
        return new PixelRect(left + ((index % columns) * cell.Width), Pad + ((index / columns) * cell.Height), cell.Width, cell.Height);
    }

    /// <summary>The body's content height for <paramref name="count"/> items: their rows and the inset above and below.</summary>
    public static double ContentHeight(int count, int columns, SizeDip cell) => (2 * Pad) + (Rows(count, columns) * cell.Height);

    /// <summary>The item under the body point (<paramref name="x"/>, <paramref name="y"/> in the body's content DIPs, scrolled in), or -1.</summary>
    public static int IndexAt(double x, double y, int columns, SizeDip cell, double width, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var r = Cell(i, columns, cell, width);
            if (x >= r.X && x < r.X + r.Width && y >= r.Y && y < r.Y + r.Height)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// [S-13005] Where a drop at the body point goes in the order: before the cell under it when the point is in its
    /// first half (across), else after it; past the last item, or below every row, the end.
    /// </summary>
    public static int InsertAt(double x, double y, int columns, SizeDip cell, double width, int count)
    {
        columns = Math.Max(1, columns);
        if (count == 0 || cell.Width <= 0 || cell.Height <= 0)
        {
            return 0;
        }

        var first = Cell(0, columns, cell, width);
        var row = (int)Math.Floor((y - first.Y) / cell.Height);
        if (row < 0)
        {
            return 0;
        }

        if (row >= Rows(count, columns))
        {
            return count;
        }

        var across = (x - first.X) / cell.Width;
        var column = Math.Clamp((int)Math.Floor(across + 0.5), 0, columns);
        return Math.Clamp((row * columns) + column, 0, count);
    }

    /// <summary>
    /// [S-13008] The DIP size of a fence that shows <paramref name="count"/> items without scrolling, as near square as the
    /// cells allow (<see cref="MinColumns"/>..<see cref="MaxColumns"/> across), with its title bar.
    /// </summary>
    public static SizeDip Around(int count, SizeDip cell)
    {
        var columns = Math.Clamp((int)Math.Ceiling(Math.Sqrt(Math.Max(1, count))), MinColumns, MaxColumns);
        return new SizeDip((columns * cell.Width) + (2 * Pad), TitleHeight + ContentHeight(Math.Max(1, count), columns, cell));
    }
}
