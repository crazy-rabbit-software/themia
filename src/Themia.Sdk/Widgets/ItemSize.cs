using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-14810] One part down a stack: its natural length and whether it is a repeated item (<see cref="IsRow"/>: a family
/// row, laid at its optimal, <see cref="ItemSize.Optimal"/>) or a part that keeps its natural length (a text, a readout,
/// a button).
/// </summary>
public readonly record struct StackItem(double Natural, bool IsRow)
{
    /// <summary>[S-12807] A family row of <paramref name="lines"/> lines.</summary>
    public static StackItem Row(int lines) => new(FamilyShape.RowHeight(lines), true);

    /// <summary>A part that keeps its natural length: a text line, a readout, a button.</summary>
    public static StackItem Fixed(double natural) => new(natural, false);
}

/// <summary>
/// [S-14810] Rows of one fixed size in a room (owner, 2026-11-20: "the widget items should actually have a fixed size.
/// then when you change the height of a widget the items should not grow or shrink to fit all the way from the top to the
/// bottom, they should just keep their size and the last item will be cutoff to some extent then."): every row
/// <see cref="Pitch"/> long (its optimal, whole cells), from the room's start, touching; <see cref="Room"/> the length the
/// rows stand in (a list's viewport: the card's foot, or the add row's top); <see cref="Shown"/> the rows the room reaches,
/// the last of them cut by its end where the room does not hold it whole; <see cref="Scale"/> the raster their edges fall
/// on. <see cref="Foot"/> is an add row standing under them at its own optimal ([S-14811]).
/// </summary>
public readonly record struct RowReach(double Pitch, int Shown = 1, double Scale = 1)
{
    private readonly double _room;

    /// <summary>[S-14811] The add row under the rows at its optimal; 0 where none stands there.</summary>
    public double Foot { get; init; }

    /// <summary>[S-14810] The length the rows stand in, from the first's start; the rows' own length where none was given.</summary>
    public double Room
    {
        get => _room > 0 ? _room : Math.Max(1, Shown) * Pitch;
        init => _room = double.IsFinite(value) ? Math.Max(0, value) : 0;
    }

    /// <summary>[S-14810] Where row <paramref name="index"/> starts: the rows before it at the pitch, on a whole device pixel.</summary>
    public double Start(int index)
        => index <= 0 || !double.IsFinite(Pitch) ? 0 : ItemSize.OnPixels(index * Pitch, Scale);

    /// <summary>[S-14810] Row <paramref name="index"/>'s length: from its start to the next one's, whole device pixels.</summary>
    public double Height(int index) => Start(index + 1) - Start(index);

    /// <summary>
    /// [S-14810] What a list of <paramref name="count"/> rows shows of them: the rows' length, at most the room (a longer
    /// list scrolls, its last row in sight cut by the room's end); the height a list's viewport takes.
    /// </summary>
    public double Span(int count) => Math.Min(Start(Math.Max(0, count)), Room);
}

/// <summary>
/// [S-14816] A grid of cells of one fixed size in a box (<see cref="ItemSize.Cells"/>): <see cref="Across"/> the columns,
/// every one the box's width reaches from its left edge, the last cut by its right edge; <see cref="Down"/> the lines, every one the box's height
/// reaches from its top edge, the last cut by its foot. Both touching, every edge on a whole device pixel.
/// </summary>
public readonly record struct CellReach(RowReach Across, RowReach Down)
{
    /// <summary>The columns.</summary>
    public int Columns => Across.Shown;

    /// <summary>The lines the box reaches, the last of them cut where the box does not hold it whole.</summary>
    public int Lines => Down.Shown;

    /// <summary>The cells the box shows: every column of every line it reaches.</summary>
    public int Count => Columns * Lines;
}

/// <summary>[S-12410] Where an item is drawn along the axis: its start and its length, in DIPs from the axis's start.</summary>
public readonly record struct ItemSlot(double Start, double Length)
{
    public double End => Start + Length;
}

/// <summary>
/// [S-14810] [S-12807] [S-12400] The one rule for a widget's repeated items in a box (owner, 2026-11-19: "the items have no
/// spacing between them"; 2026-11-20: "the widget items should actually have a fixed size. then when you change the height
/// of a widget the items should not grow or shrink to fit all the way from the top to the bottom, they should just keep
/// their size and the last item will be cutoff to some extent then."): a repeated item (a row, a day, an hour, a lap, a
/// column) has one fixed size, its optimal: its natural length in whole grid cells (<see cref="ItemGrid"/>, a row of 28
/// three cells). Along the axis it repeats the items stand at that size from the room's start, touching, as many as the
/// room reaches; the last one the room does not hold whole is cut by its end, and a room larger than its items stays
/// empty past the last. Nothing grows or shrinks with the card. A part that is no repeated item (a text, a readout, a
/// button) keeps its length; parts with no row among them stand together, centred. Every edge falls on a whole device
/// pixel. The month's day grid is the card's grid, not a list, and shares the card's width (<see cref="Columns"/>).
/// </summary>
public static class ItemSize
{
    /// <summary><paramref name="length"/> on the nearest whole device pixel at <paramref name="scale"/>.</summary>
    public static double OnPixels(double length, double scale)
    {
        var s = double.IsFinite(scale) && scale > 0 ? scale : 1;
        return double.IsFinite(length) ? Math.Round(length * s, MidpointRounding.AwayFromZero) / s : length;
    }

    /// <summary>
    /// [S-14810] An item's one size: its natural length in whole cells of <paramref name="grid"/> (at least one), at every
    /// size of its card.
    /// </summary>
    public static double Optimal(double natural, ItemGrid grid) => grid.Whole(double.IsFinite(natural) ? Math.Max(0, natural) : 0);

    /// <summary>
    /// [S-14810] How many items of <paramref name="natural"/> length a room <paramref name="available"/> long shows at
    /// their optimal (<see cref="Optimal"/>): every one it reaches, the last one cut where the room does not hold it whole;
    /// at least <paramref name="least"/>, at most <paramref name="most"/>. What a view shows of a count that follows its card
    /// (a forecast's days, a planner's days, a stopwatch's laps, a calendar's hours).
    /// </summary>
    public static int Count(double available, double natural, ItemGrid grid, int least = 1, int most = int.MaxValue)
    {
        var optimal = Optimal(natural, grid);
        var reaches = double.IsFinite(available) && available > LayoutMath.Tolerance
            ? (int)Math.Ceiling((available - LayoutMath.Tolerance) / optimal)
            : 0;
        return Math.Clamp(reaches, Math.Max(0, least), Math.Max(Math.Max(0, least), most));
    }

    /// <summary>
    /// [S-14810] [S-12807] Lay <paramref name="items"/> (with <paramref name="gap"/> between two, a family token) in a room
    /// <paramref name="available"/> long on <paramref name="grid"/>: a row at its optimal, any other part at its natural,
    /// one after another from the room's start; the room past the last stays empty and a part past the room's end is cut
    /// by it. With no row among them the parts stand together, centred; [S-12814] <paramref name="justify"/>: the room goes
    /// between them instead (the first on the start, the last on the end; one part takes it all), a readout block's.
    /// </summary>
    public static ItemSlot[] Lay(double available, IReadOnlyList<StackItem> items, ItemGrid grid, double gap = 0, bool justify = false)
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
        var lengths = new double[n];
        var need = (n - 1) * gap;
        var rows = false;
        for (var i = 0; i < n; i++)
        {
            var natural = double.IsFinite(items[i].Natural) ? Math.Max(0, items[i].Natural) : 0;
            lengths[i] = items[i].IsRow ? Optimal(natural, grid) : natural;
            need += lengths[i];
            rows |= items[i].IsRow;
        }

        if (rows)
        {
            var start = 0.0;
            for (var i = 0; i < n; i++)
            {
                var from = OnPixels(start, scale);
                var to = OnPixels(start + lengths[i], scale);
                slots[i] = new ItemSlot(from, to - from);
                start += lengths[i] + gap;
            }

            return slots;
        }

        var spare = double.IsFinite(available) ? available - need : 0;
        var pixels = spare > 0 ? Math.Floor((spare * scale) + 1e-6) : 0;
        var at = !justify && pixels > 0 ? Math.Floor(pixels / 2) / scale : 0;
        for (var i = 0; i < n; i++)
        {
            var length = lengths[i] + (justify && n == 1 ? pixels / scale : 0);
            slots[i] = new ItemSlot(at, length);
            at += length + gap + (justify && i < n - 1 ? Share(pixels, n - 1, i) / scale : 0);
        }

        return slots;
    }

    /// <summary>
    /// [S-14810] [S-12807] Rows of <paramref name="natural"/> height in a room <paramref name="available"/> long on
    /// <paramref name="grid"/>: each at its optimal from the room's start, touching, as many as the room reaches (the last
    /// cut by its end where the room does not hold it whole, at least one). A room not measured lays one row, its own length.
    /// </summary>
    public static RowReach Reach(double available, double natural, ItemGrid grid)
    {
        var optimal = Optimal(natural, grid);
        return !double.IsFinite(available) || available <= 0
            ? new RowReach(optimal, 1, grid.Raster)
            : new RowReach(optimal, Count(available, natural, grid), grid.Raster) { Room = available };
    }

    /// <summary>
    /// [S-14810] [S-12815] A list's rows of <paramref name="lines"/> lines in a view <paramref name="height"/> tall (the card's
    /// inset over it; less <paramref name="reserved"/> for what stands over the rows, in whole cells): their boxes touching,
    /// from the card's top edge (<paramref name="edge"/>: the list stands <see cref="FamilySpace.Inset"/> over the view's
    /// top, <c>ListEdge</c>; else from the view's top, under a search box, a tool line or column titles) at their optimal,
    /// the last one the card's foot reaches cut by it. A list gives its rows <see cref="RowReach.Height"/>, its viewport
    /// <see cref="RowReach.Span"/> and scrolls the rest; fewer items leave the room under the last empty.
    /// </summary>
    public static RowReach Rows(double height, int lines, ItemGrid grid, double reserved = 0, bool edge = true)
        => Reach(height + (edge ? FamilySpace.Inset : 0) - (reserved > 0 ? grid.Whole(reserved) : 0), FamilyShape.RowHeight(lines), grid);

    /// <summary>
    /// [S-14811] [S-3850] <see cref="Rows"/> with an add row of <paramref name="footLines"/> lines under them: every row and
    /// the add row at their optimal (<see cref="RowReach.Foot"/>), the rows' room the card's less the add row's, so a list
    /// longer than its card is cut by the add row and the add row stands on the card's foot; a shorter one has the add row
    /// right under its last row (the view places it) and the room under it empty.
    /// </summary>
    public static RowReach RowsAbove(double height, int lines, ItemGrid grid, int footLines = 1)
    {
        var foot = Optimal(FamilyShape.RowHeight(footLines), grid);
        return Reach(height + FamilySpace.Inset - foot, FamilyShape.RowHeight(lines), grid) with { Foot = foot };
    }

    /// <summary>
    /// [S-14812] [S-12809] The month's <paramref name="count"/> day columns across a card <paramref name="width"/> wide: the
    /// month is a grid of the card, not a list (WR-9: "the calendar cells and numbers fill the card"), so its columns share
    /// the width evenly from the card's left edge to its right, each edge on a whole device pixel at
    /// <paramref name="grid"/>'s raster scale (the first ones a pixel more where the pixels do not share evenly).
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

    /// <summary>
    /// [S-14816] A grid of cells of <paramref name="naturalWidth"/> × <paramref name="naturalHeight"/> (their content and the
    /// padding inside them) in a box <paramref name="width"/> × <paramref name="height"/> on <paramref name="grid"/> (owner,
    /// 2026-11-21: "the weather forecast widget grid view items do not go from edge to edge, they have space everywhere."):
    /// each cell its optimal on both axes, at every size of its card; the cells touch, from the box's left and top edges (the
    /// card's: no inset round them, the inset is the cells' own padding, WR-3). Both ways as a list's rows (WR-4, owner
    /// 2026-11-20: "they should just keep their size and the last item will be cutoff to some extent then"): across, every
    /// column the width reaches (<paramref name="least"/>..<paramref name="most"/>), the last cut by the card's right edge,
    /// never the room past the last whole one left empty (WR-3: a grid's cells reach from edge to edge); down, every line
    /// the height reaches, the last cut by the foot, at least one.
    /// </summary>
    public static CellReach Cells(double width, double height, double naturalWidth, double naturalHeight, ItemGrid grid, int least = 1, int most = int.MaxValue)
    {
        var room = double.IsFinite(width) && width > 0 ? width : double.NaN;
        var across = double.IsFinite(room)
            ? new RowReach(Optimal(naturalWidth, grid), Count(room, naturalWidth, grid, Math.Max(1, least), most), grid.Raster) { Room = room }
            : new RowReach(Optimal(naturalWidth, grid), Math.Max(1, least), grid.Raster);
        return new CellReach(across, Reach(height, naturalHeight, grid));
    }

    /// <summary>
    /// [S-16438] A list of items wrapped into lines of cells (a forecast's days, a year's months) in a box
    /// <paramref name="width"/> × <paramref name="height"/>: each cell its optimal on both axes at every size of its card,
    /// touching from the box's left and top edges (<see cref="Cells"/>); across only the whole columns the width holds
    /// (<paramref name="least"/>..<paramref name="most"/>), never one in the middle of the list cut by the right edge while
    /// later ones stand under it (WR-4: "the last item will be cutoff to some extent then"), the room past the last whole
    /// column, less than a cell, left empty at the right edge (owner, 2026-11-23: "Fixed size, gap at right"); down every
    /// line the height reaches, the last cut by the foot. A box not measured across lays <paramref name="least"/> columns.
    /// </summary>
    public static CellReach Wrapped(double width, double height, double naturalWidth, double naturalHeight, ItemGrid grid, int least = 1, int most = int.MaxValue)
    {
        var reach = Cells(width, height, naturalWidth, naturalHeight, grid, least, most);
        if (!double.IsFinite(width) || !(width > 0))
        {
            return reach;
        }

        var pitch = reach.Across.Pitch;
        var floor = Math.Max(1, least);
        var whole = Math.Clamp((int)Math.Floor((width + LayoutMath.Tolerance) / pitch), floor, Math.Max(floor, most));
        return reach with { Across = new RowReach(pitch, whole, grid.Raster) { Room = width } };
    }

    /// <summary>[S-14810] Rows of <paramref name="lines"/> lines before a list's box is measured: one at its optimal.</summary>
    public static RowReach Natural(int lines, ItemGrid grid) => new(Optimal(FamilyShape.RowHeight(lines), grid), 1, grid.Raster);

    /// <summary>The <paramref name="i"/>th of <paramref name="parts"/> whole-pixel shares of <paramref name="pixels"/>, the first ones a pixel more.</summary>
    private static double Share(double pixels, int parts, int i)
    {
        var each = Math.Floor(pixels / parts);
        return each + (i < pixels - (each * parts) ? 1 : 0);
    }
}
