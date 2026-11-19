using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-12410] One item along an axis: the length it is drawn at by itself (<see cref="Natural"/>) and whether it
/// grows with the room (<see cref="Grows"/>: a row, a chart's plot, a tile's face) or keeps its length (a text, a
/// readout, a button).
/// </summary>
public readonly record struct StretchItem(double Natural, bool Grows)
{
    /// <summary>[S-12807] A family row of <paramref name="lines"/> lines: its natural height, growing with the room.</summary>
    public static StretchItem Row(int lines) => new(FamilyShape.RowHeight(lines), true);

    /// <summary>An item that never stretches: a text line, a readout, a button.</summary>
    public static StretchItem Fixed(double natural) => new(natural, false);

    /// <summary>An item that takes any room it is given: a chart's plot, a tile's face.</summary>
    public static StretchItem Free(double natural) => new(natural, true);
}

/// <summary>
/// [S-12807] [S-12830] Rows of one pitch laid by <see cref="ItemStretch.Rows"/>: each row's height (whole grid cells),
/// the rows touching, and how many of them the room holds at that height (<see cref="Shown"/>: never a row squeezed in).
/// </summary>
public readonly record struct RowReach(double Pitch, int Shown = int.MaxValue)
{
    /// <summary>The length <paramref name="count"/> rows take, first box to last box, never more rows than the room holds.</summary>
    public double Length(int count) => count <= 0 ? 0 : Math.Min(count, Shown) * Pitch;
}

/// <summary>[S-12410] Where an item is drawn along the axis: its start and its length, in DIPs from the axis's start.</summary>
public readonly record struct ItemSlot(double Start, double Length)
{
    public double End => Start + Length;
}

/// <summary>
/// [S-12410] [S-12400] [S-12807] [S-12830] The one rule for a widget's items in a box (owner, 2026-11-19: "the items have
/// no spacing between them and they try to take up the whole space from top to bottom and left to right"; "items inside
/// widgets should be designed in a way that they match the size of the grid on the desktop ... a widget that is 10
/// blocks hight should not show 4 items because then the items would have an uneven size"): along an axis the items
/// that grow (rows, charts, tiles) are whole grid cells (<see cref="ItemGrid"/>), at least the cells their natural
/// length takes, and share the whole cells the room has over them evenly, so they touch one another and the room's
/// start; the cells left over (fewer than one for each) stand at the end, never before the first item or between two.
/// The items that do not grow (a text, a readout, a button) keep their length. With nothing that grows, the items stand
/// together, centred in the room (whole device pixels). Short of room the items keep their least lengths one after
/// another from the start: the stretch never shrinks an item ([S-11800]).
/// </summary>
public static class ItemStretch
{
    /// <summary>
    /// [S-12807] Lay <paramref name="items"/> (with <paramref name="gap"/> between two, a family token) in a room
    /// <paramref name="available"/> long on <paramref name="grid"/>. [S-12814] <paramref name="justify"/>: with nothing
    /// that grows, the room goes between the items (the first on the start, the last on the end; one item takes it all)
    /// instead of round them.
    /// </summary>
    public static ItemSlot[] Reach(double available, IReadOnlyList<StretchItem> items, ItemGrid grid, double gap = 0, bool justify = false)
    {
        ArgumentNullException.ThrowIfNull(items);
        var n = items.Count;
        var slots = new ItemSlot[n];
        if (n == 0)
        {
            return slots;
        }

        var scale = grid.Raster;
        gap = double.IsFinite(gap) ? Math.Max(0, gap) : 0;
        var least = new double[n];
        var need = (n - 1) * gap;
        var growing = 0;
        for (var i = 0; i < n; i++)
        {
            var natural = double.IsFinite(items[i].Natural) ? Math.Max(0, items[i].Natural) : 0;

            // [S-12830] A growing item is whole cells, at least those its natural length takes.
            least[i] = items[i].Grows ? grid.Whole(natural) : natural;
            need += least[i];
            growing += items[i].Grows ? 1 : 0;
        }

        var spare = double.IsFinite(available) ? available - need : 0;
        if (growing > 0)
        {
            // Every growing item the same whole cells more; what is left over stands at the end.
            var each = (grid.Within(Math.Max(0, spare)) / growing) * grid.Pitch;
            var start = 0.0;
            for (var i = 0; i < n; i++)
            {
                var length = least[i] + (items[i].Grows ? each : 0);
                slots[i] = new ItemSlot(start, length);
                start += length + gap;
            }

            return slots;
        }

        var pixels = spare > 0 ? Math.Floor((spare * scale) + 1e-6) : 0;
        var at = !justify && pixels > 0 ? Math.Floor(pixels / 2) / scale : 0;
        for (var i = 0; i < n; i++)
        {
            var length = least[i] + (justify && n == 1 ? pixels / scale : 0);
            slots[i] = new ItemSlot(at, length);
            at += length + gap + (justify && i < n - 1 ? Share(pixels, n - 1, i) / scale : 0);
        }

        return slots;
    }

    /// <summary>
    /// [S-12807] [S-12830] <paramref name="count"/> like rows of <paramref name="natural"/> height in a room
    /// <paramref name="available"/> long on <paramref name="grid"/>: as many as the room holds at the whole cells their
    /// natural height takes (never one squeezed in at less), each as many whole cells as the room has for them, all
    /// alike; the cells left over stand at the end. The rows touch.
    /// </summary>
    public static RowReach Reach(double available, int count, double natural, ItemGrid grid)
    {
        natural = double.IsFinite(natural) ? Math.Max(0, natural) : 0;
        var least = grid.Cells(natural);
        if (count <= 0 || !double.IsFinite(available))
        {
            return new RowReach(least * grid.Pitch, Math.Max(0, count));
        }

        var shown = Math.Clamp(grid.Within(available) / least, 1, count);
        var cells = Math.Max(least, grid.Within(available) / shown);
        return new RowReach(cells * grid.Pitch, shown);
    }

    /// <summary>
    /// [S-12807] [S-12815] [S-12830] A list's <paramref name="count"/> rows of <paramref name="lines"/> lines in a view
    /// <paramref name="height"/> tall (the card's inset over it; less <paramref name="reserved"/> for what stands under
    /// or over the rows, in whole cells): their boxes touching, whole grid cells, from the card's top edge
    /// (<paramref name="edge"/>: the list stands <see cref="FamilySpace.Inset"/> over the view's top, <c>ListEdge</c>;
    /// else from the view's top, under a search box, a tool line or column titles) down, the cells left over at the
    /// card's foot. A list applies it as its rows' height (<see cref="RowReach.Pitch"/>) and shows
    /// <see cref="RowReach.Shown"/> of them, scrolling the rest.
    /// </summary>
    public static RowReach Rows(double height, int count, int lines, ItemGrid grid, double reserved = 0, bool edge = true)
        => Reach(height + (edge ? FamilySpace.Inset : 0) - (reserved > 0 ? grid.Whole(reserved) : 0), count, FamilyShape.RowHeight(lines), grid);

    /// <summary>[S-12807] <see cref="Rows"/> with an add row of <paramref name="footLines"/> lines standing right under them, on the card's foot edge ([S-3850]).</summary>
    public static RowReach RowsAbove(double height, int count, int lines, ItemGrid grid, int footLines = 1)
        => Rows(height, count, lines, grid, FamilyShape.RowHeight(footLines));

    /// <summary>
    /// [S-12830] [S-12834] <paramref name="count"/> columns across a card <paramref name="width"/> wide, from its left edge to
    /// its right (WR-3), on <paramref name="grid"/>: each whole cells (WR-6), the width's whole cells shared as evenly as
    /// they go (the first ones a cell more), the last column taking what of the width is not a whole cell; a width of
    /// fewer whole cells than columns is shared evenly. The month's days, the week planner's days.
    /// </summary>
    public static double[] Columns(double width, int count, ItemGrid grid)
    {
        width = double.IsFinite(width) ? Math.Max(0, width) : 0;
        var columns = new double[Math.Max(0, count)];
        if (columns.Length == 0)
        {
            return columns;
        }

        var cells = grid.Within(width);
        if (cells < columns.Length)
        {
            Array.Fill(columns, width / columns.Length);
            return columns;
        }

        var each = cells / columns.Length;
        var extra = cells % columns.Length;
        for (var c = 0; c < columns.Length; c++)
        {
            columns[c] = (each + (c < extra ? 1 : 0)) * grid.Pitch;
        }

        columns[^1] += width - (cells * grid.Pitch);
        return columns;
    }

    /// <summary>[S-12807] Rows of <paramref name="lines"/> lines before a list's box is measured: the whole cells they take.</summary>
    public static RowReach Natural(int lines, ItemGrid grid) => new(grid.Whole(FamilyShape.RowHeight(lines)));

    /// <summary>The <paramref name="i"/>th of <paramref name="parts"/> whole-pixel shares of <paramref name="pixels"/>, the first ones a pixel more.</summary>
    private static double Share(double pixels, int parts, int i)
    {
        var each = Math.Floor(pixels / parts);
        return each + (i < pixels - (each * parts) ? 1 : 0);
    }
}
