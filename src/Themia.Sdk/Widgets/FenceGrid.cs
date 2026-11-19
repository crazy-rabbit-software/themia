namespace Themia.Sdk;

/// <summary>
/// [S-13019] [S-13040] A fence's cells, as pure layout in any unit: fractions of its display for the host (the desktop
/// icon grid, <c>FenceCells</c>), DIPs for a picture of one. A fence is a region of the desktop's icon grid (the owner,
/// 2026-11-19: "the fence should basically just be a helper to align the icon on the underlying engine"): its title bar is
/// <see cref="TitleCells"/> rows of the widget grid, and under it its body is whole icon boxes (a desktop icon's box on the
/// grid), its items in reading order, as many to a row as the body holds, box against box from the body's top-left. A body
/// with more items than it holds shows whole rows from <see cref="Layout.Scroll"/>; rolled up it has none. There is no
/// other layout of a fence's items.
/// </summary>
public static class FenceGrid
{
    /// <summary>[S-13040] The title bar's height in rows of the widget grid ([S-06]): every fence's body starts on a grid line.</summary>
    public const int TitleCells = 3;

    /// <summary>
    /// [S-13012] The title bar as a sizing states it, in whole DIPs: grid-fitted (rounded up to whole cells) it is exactly
    /// <see cref="TitleCells"/> rows on every display. A grid cell is the screen's length over that length / 10 rounded,
    /// 9.5..10.5 DIPs on every real screen, and (3 − 1) × 10.5 &lt; 28 ≤ 3 × 9.5.
    /// </summary>
    public const double TitleDip = 28;

    /// <summary>The fewest columns a fence made round a selection is laid out with, and the most.</summary>
    public const int MinColumns = 1;

    public const int MaxColumns = 8;

    /// <summary>
    /// One fence's body as cells, in one unit: its top-left (<see cref="Left"/>, <see cref="Top"/>, under the title bar),
    /// its size, one icon box (<see cref="CellWidth"/> × <see cref="CellHeight"/>) and the first row it shows.
    /// </summary>
    public readonly record struct Layout(double Left, double Top, double Width, double Height, double CellWidth, double CellHeight, int Scroll)
    {
        /// <summary>How many boxes a row holds: at least one.</summary>
        public int Columns => Fit(Width, CellWidth, atLeastOne: true);

        /// <summary>How many whole rows the body shows: none while rolled up (no height).</summary>
        public int Rows => Fit(Height, CellHeight, atLeastOne: false);

        /// <summary>How many boxes the body shows at once.</summary>
        public int Shown => Columns * Rows;

        /// <summary>The furthest the body scrolls with <paramref name="count"/> items: the rows past what it shows.</summary>
        public int MaxScroll(int count) => Math.Max(0, RowsOf(count, Columns) - Rows);

        /// <summary>This layout with its scroll kept between none and <see cref="MaxScroll"/> for <paramref name="count"/> items.</summary>
        public Layout Clamped(int count) => this with { Scroll = Math.Clamp(Scroll, 0, MaxScroll(count)) };

        /// <summary>Whether item <paramref name="index"/> stands in a row the body shows now.</summary>
        public bool Shows(int index)
        {
            if (index < 0)
            {
                return false;
            }

            var row = (index / Columns) - Scroll;
            return row >= 0 && row < Rows;
        }

        /// <summary>
        /// The box of item <paramref name="index"/>: reading order, <see cref="Columns"/> to a row, from the body's top-left,
        /// the scroll's rows above it. Where the body does not show it the box is outside the body.
        /// </summary>
        public (double X, double Y, double W, double H) Cell(int index)
        {
            var i = Math.Max(0, index);
            var columns = Columns;
            return (Left + (i % columns * CellWidth), Top + (((i / columns) - Scroll) * CellHeight), CellWidth, CellHeight);
        }

        /// <summary>
        /// [S-13040] The place in the order the body's cell under (<paramref name="x"/>, <paramref name="y"/>) stands for (the
        /// scroll's rows counted), or -1 where the point is not on a cell the body shows.
        /// </summary>
        public int SlotAt(double x, double y)
        {
            if (CellWidth <= 0 || CellHeight <= 0 || !double.IsFinite(x) || !double.IsFinite(y))
            {
                return -1;
            }

            var column = (int)Math.Floor((x - Left) / CellWidth);
            var row = (int)Math.Floor((y - Top) / CellHeight);
            return column < 0 || column >= Columns || row < 0 || row >= Rows ? -1 : ((row + Scroll) * Columns) + column;
        }

        /// <summary>Whether (<paramref name="x"/>, <paramref name="y"/>) is inside the body's whole boxes.</summary>
        public bool Contains(double x, double y) => SlotAt(x, y) >= 0;
    }

    /// <summary>How many rows <paramref name="count"/> items take in <paramref name="columns"/>.</summary>
    public static int RowsOf(int count, int columns) => count <= 0 ? 0 : ((count - 1) / Math.Max(1, columns)) + 1;

    /// <summary>How many whole <paramref name="cell"/>s <paramref name="length"/> holds (float noise forgiven).</summary>
    private static int Fit(double length, double cell, bool atLeastOne)
    {
        var n = cell > 0 && double.IsFinite(length) ? (int)Math.Floor((length / cell) + 1e-6) : 0;
        return atLeastOne ? Math.Max(1, n) : Math.Max(0, n);
    }

    /// <summary>
    /// [S-13008] The DIP size of a fence that shows <paramref name="count"/> items without scrolling, as near square as the
    /// cells allow (<see cref="MinColumns"/>..<see cref="MaxColumns"/> across), with its title bar.
    /// </summary>
    public static SizeDip Around(int count, SizeDip cell)
    {
        var columns = Math.Clamp((int)Math.Ceiling(Math.Sqrt(Math.Max(1, count))), MinColumns, MaxColumns);
        return new SizeDip(columns * cell.Width, TitleDip + (RowsOf(Math.Max(1, count), columns) * cell.Height));
    }
}
