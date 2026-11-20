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
/// [S-14500] Rows of one optimal length laid by <see cref="ItemStretch.Rows"/> (owner, 2026-11-20: "the items should have an
/// optimal size and then they may stretch a little to fill up the remaining space"): how many the room holds at their
/// optimal (<see cref="Shown"/>, never one squeezed in), the even share of the room each takes (<see cref="Pitch"/>, the
/// room over them: at least the optimal, less than one more item's worth), and the raster scale their edges fall on
/// (<see cref="Scale"/>), so they touch and fill the room to the pixel. <see cref="Foot"/> is an add row standing under
/// them, stretched as they are ([S-12807]).
/// </summary>
public readonly record struct RowReach(double Pitch, int Shown = int.MaxValue, double Scale = 1)
{
    /// <summary>[S-14500] The add row under the rows, stretched as they are; 0 where none stands there.</summary>
    public double Foot { get; init; }

    /// <summary>The length <paramref name="count"/> rows take, first box to last box, never more rows than the room holds.</summary>
    public double Length(int count) => count <= 0 ? 0 : Math.Min(count, Shown) * Pitch;

    /// <summary>[S-14500] The room the shown rows fill: every one of them, touching, from the first's start to the last's end.</summary>
    public double Room => Shown is > 0 and < int.MaxValue ? Shown * Pitch : Pitch;

    /// <summary>
    /// [S-14500] Where row <paramref name="index"/> starts: the shown rows' even shares with every edge on a whole device
    /// pixel, so rows the pixels cannot share evenly differ by a pixel at most and still end exactly on the room's end;
    /// past the shown rows (a list scrolled) the same again a room further on, so any run of as many rows as are shown
    /// fills the room exactly.
    /// </summary>
    public double Start(int index)
    {
        if (index <= 0 || !double.IsFinite(Pitch))
        {
            return 0;
        }

        if (Shown is <= 0 or int.MaxValue)
        {
            return ItemStretch.OnPixels(index * Pitch, Scale);
        }

        return ((index / Shown) * Room) + ItemStretch.OnPixels(index % Shown * Pitch, Scale);
    }

    /// <summary>[S-14500] Row <paramref name="index"/>'s length: from its start to the next one's (<see cref="Start"/>), whole device pixels.</summary>
    public double Height(int index) => Start(index + 1) - Start(index);
}

/// <summary>[S-12410] Where an item is drawn along the axis: its start and its length, in DIPs from the axis's start.</summary>
public readonly record struct ItemSlot(double Start, double Length)
{
    public double End => Start + Length;
}

/// <summary>
/// [S-12410] [S-12400] [S-12807] [S-14500] The one rule for a widget's items in a box (owner, 2026-11-19: "the items have
/// no spacing between them and they try to take up the whole space from top to bottom and left to right"; 2026-11-20:
/// "the items should have an optimal size and then they may stretch a little to fill up the remaining space. the
/// widgets should not have the size increments anymore but should be incrementable in steps of one"): an item that
/// grows (a row, a chart, a tile) has an optimal length, its natural length in whole grid cells (<see cref="ItemGrid"/>,
/// a row of 28 three cells); along an axis as many of them stand as the room holds at their optimal, and they share the
/// whole room evenly, so they touch one another and the room's ends with nothing left over, each less than one more
/// item's worth past its optimal and never under it. The items that do not grow (a text, a readout, a button) keep their
/// length. With nothing that grows, the items stand together, centred in the room (whole device pixels). Short of room
/// the items keep their optimal lengths one after another from the start: the stretch never shrinks an item
/// ([S-11800]). Every edge falls on a whole device pixel.
/// </summary>
public static class ItemStretch
{
    /// <summary><paramref name="length"/> on the nearest whole device pixel at <paramref name="scale"/>.</summary>
    public static double OnPixels(double length, double scale)
    {
        var s = double.IsFinite(scale) && scale > 0 ? scale : 1;
        return double.IsFinite(length) ? Math.Round(length * s, MidpointRounding.AwayFromZero) / s : length;
    }

    /// <summary>
    /// [S-14500] An item's optimal length: its natural length in whole cells of <paramref name="grid"/> (at least one), what
    /// it is drawn at in a room that holds just it, and what it stretches from.
    /// </summary>
    public static double Optimal(double natural, ItemGrid grid) => grid.Whole(double.IsFinite(natural) ? Math.Max(0, natural) : 0);

    /// <summary>
    /// [S-14500] How many items of <paramref name="natural"/> length a room <paramref name="available"/> long holds at their
    /// optimal (<see cref="Optimal"/>): at least <paramref name="least"/>, at most <paramref name="most"/>. What a view
    /// shows of a count that follows its card (a forecast's days, a planner's days, a stopwatch's laps).
    /// </summary>
    public static int Count(double available, double natural, ItemGrid grid, int least = 1, int most = int.MaxValue)
    {
        var optimal = Optimal(natural, grid);
        var holds = double.IsFinite(available) && available > 0 ? (int)Math.Floor((available / optimal) + LayoutMath.Tolerance / optimal) : 0;
        return Math.Clamp(holds, Math.Max(0, least), Math.Max(Math.Max(0, least), most));
    }

    /// <summary>
    /// [S-12807] [S-14500] Lay <paramref name="items"/> (with <paramref name="gap"/> between two, a family token) in a room
    /// <paramref name="available"/> long on <paramref name="grid"/>: the growing ones at their optimal and the room past
    /// every item's length shared evenly among them, so they end on the room's end. [S-12814] <paramref name="justify"/>:
    /// with nothing that grows, the room goes between the items (the first on the start, the last on the end; one item
    /// takes it all) instead of round them.
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

            // [S-14500] A growing item starts at its optimal: whole cells, at least those its natural length takes.
            least[i] = items[i].Grows ? Optimal(natural, grid) : natural;
            need += least[i];
            growing += items[i].Grows ? 1 : 0;
        }

        var spare = double.IsFinite(available) ? available - need : 0;
        if (growing > 0)
        {
            // Every growing item the same share more; their edges on whole device pixels, the last ending on the room's end.
            var each = spare > 0 ? spare / growing : 0;
            var start = 0.0;
            for (var i = 0; i < n; i++)
            {
                var length = least[i] + (items[i].Grows ? each : 0);
                var from = OnPixels(start, scale);
                var to = OnPixels(start + length, scale);
                slots[i] = new ItemSlot(from, to - from);
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
    /// [S-14500] How many more rows like the growing ones of <paramref name="items"/> a room <paramref name="available"/>
    /// long holds past them at their optimal: the empty rows a list's room has at its foot ("this is fine if it happens"),
    /// laid as rows so the shown ones keep the pitch the whole room gives. None where the growing items are not alike
    /// rows (a row and a chart), where none grows, or where the room holds no more.
    /// </summary>
    public static int SpareRows(double available, IReadOnlyList<StretchItem> items, ItemGrid grid, double gap = 0)
    {
        ArgumentNullException.ThrowIfNull(items);
        var growing = items.Where(i => i.Grows).Select(i => Optimal(i.Natural, grid)).ToList();
        if (growing.Count == 0 || growing.Any(o => Math.Abs(o - growing[0]) > 1e-6) || !double.IsFinite(available))
        {
            return 0;
        }

        gap = double.IsFinite(gap) ? Math.Max(0, gap) : 0;
        var need = items.Sum(i => i.Grows ? Optimal(i.Natural, grid) : (double.IsFinite(i.Natural) ? Math.Max(0, i.Natural) : 0)) + ((items.Count - 1) * gap);
        var row = growing[0] + gap;
        return Math.Max(0, (int)Math.Floor(((available - need) / row) + (LayoutMath.Tolerance / row)));
    }

    /// <summary>
    /// [S-12807] [S-14500] Like rows of <paramref name="natural"/> height in a room <paramref name="available"/> long on
    /// <paramref name="grid"/>: as many as the room holds at their optimal (never one squeezed in, at least one), sharing
    /// the room evenly, so they touch and end on the room's end with nothing left over. A room shorter than one row lays
    /// one at its optimal; one that holds its natural length but not its whole cells (a card at a content scale under one,
    /// whose cells are larger in its own DIPs) one filling it.
    /// </summary>
    public static RowReach Reach(double available, double natural, ItemGrid grid)
    {
        var optimal = Optimal(natural, grid);
        if (!double.IsFinite(available) || available < Math.Min(optimal, natural) - LayoutMath.Tolerance)
        {
            return new RowReach(optimal, 1, grid.Raster);
        }

        if (available < optimal - LayoutMath.Tolerance)
        {
            return new RowReach(available, 1, grid.Raster);
        }

        var shown = Count(available, natural, grid);
        return new RowReach(available / shown, shown, grid.Raster);
    }

    /// <summary>
    /// [S-12807] [S-12815] [S-14500] A list's rows of <paramref name="lines"/> lines in a view <paramref name="height"/> tall
    /// (the card's inset over it; less <paramref name="reserved"/> for what stands under or over the rows, in whole cells):
    /// their boxes touching, from the card's top edge (<paramref name="edge"/>: the list stands
    /// <see cref="FamilySpace.Inset"/> over the view's top, <c>ListEdge</c>; else from the view's top, under a search box, a
    /// tool line or column titles) to its foot edge. A list applies <see cref="RowReach.Height"/> to its rows and shows
    /// <see cref="RowReach.Shown"/> of them, scrolling the rest; fewer items leave the rest of those rows empty at the foot.
    /// </summary>
    public static RowReach Rows(double height, int lines, ItemGrid grid, double reserved = 0, bool edge = true)
        => Reach(height + (edge ? FamilySpace.Inset : 0) - (reserved > 0 ? grid.Whole(reserved) : 0), FamilyShape.RowHeight(lines), grid);

    /// <summary>
    /// [S-12807] [S-14500] <see cref="Rows"/> with an add row of <paramref name="footLines"/> lines standing right under them,
    /// on the card's foot edge ([S-3850]): the rows as many as the room holds over the add row's optimal, and rows and add
    /// row stretched alike to fill it (<see cref="RowReach.Foot"/>), so a list of one line has rows and add row of one
    /// height. A card that holds the two at their natural lengths but not at their whole cells (a content scale under one)
    /// has them share it alike.
    /// </summary>
    public static RowReach RowsAbove(double height, int lines, ItemGrid grid, int footLines = 1)
    {
        var available = height + FamilySpace.Inset;
        var row = Optimal(FamilyShape.RowHeight(lines), grid);
        var foot = Optimal(FamilyShape.RowHeight(footLines), grid);
        if (!double.IsFinite(available) || available < FamilyShape.RowHeight(lines) + FamilyShape.RowHeight(footLines) - LayoutMath.Tolerance)
        {
            return new RowReach(row, 1, grid.Raster) { Foot = foot };
        }

        var shown = Math.Max(1, (int)Math.Floor(((available - foot) / row) + (LayoutMath.Tolerance / row)));
        var rows = OnPixels(available * shown * row / ((shown * row) + foot), grid.Raster);
        return new RowReach(rows / shown, shown, grid.Raster) { Foot = available - rows };
    }

    /// <summary>
    /// [S-12830] [S-12834] [S-14500] <paramref name="count"/> columns across a card <paramref name="width"/> wide, from its
    /// left edge to its right (WR-3), sharing the width evenly, each edge on a whole device pixel at
    /// <paramref name="grid"/>'s raster scale (the first ones a pixel more where the pixels do not share evenly). The month's
    /// days, the week planner's days.
    /// </summary>
    public static double[] Columns(double width, int count, ItemGrid grid)
    {
        width = double.IsFinite(width) ? Math.Max(0, width) : 0;
        var columns = new double[Math.Max(0, count)];
        if (columns.Length == 0)
        {
            return columns;
        }

        var reach = new RowReach(width / columns.Length, columns.Length, grid.Raster);
        for (var c = 0; c < columns.Length; c++)
        {
            columns[c] = c == columns.Length - 1 ? width - reach.Start(c) : reach.Height(c);
        }

        return columns;
    }

    /// <summary>[S-12807] [S-14500] Rows of <paramref name="lines"/> lines before a list's box is measured: one at its optimal.</summary>
    public static RowReach Natural(int lines, ItemGrid grid) => new(Optimal(FamilyShape.RowHeight(lines), grid), 1, grid.Raster);

    /// <summary>The <paramref name="i"/>th of <paramref name="parts"/> whole-pixel shares of <paramref name="pixels"/>, the first ones a pixel more.</summary>
    private static double Share(double pixels, int parts, int i)
    {
        var each = Math.Floor(pixels / parts);
        return each + (i < pixels - (each * parts) ? 1 : 0);
    }
}
