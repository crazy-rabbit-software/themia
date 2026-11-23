using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.Items;

/// <summary>
/// [S-16946] Which parts a list draws, across all its items (a part the kind offers and the user did not leave out): the
/// items' one size follows it, so a part left out takes no room.
/// </summary>
/// <param name="BarAt">[S-17180] Where the bar stands: under the row, or at the item's end standing (the devices').</param>
/// <param name="Even">[S-17181] Every column of the list the same width (the devices'): a table's columns, a row's end.</param>
public readonly record struct ItemShape(bool Lead, bool Subtitle, bool End, bool Bar, bool Note, bool Reading = false, bool Action = false, ItemBarAt BarAt = ItemBarAt.Under, bool Even = false)
{
    /// <summary>
    /// The parts of <paramref name="offered"/> not in <paramref name="hidden"/>; a reading or an action at the end where the kind
    /// draws one, its bar where <paramref name="barAt"/> stands it, its columns <paramref name="even"/>.
    /// </summary>
    public static ItemShape Of(IReadOnlyCollection<ItemPart> offered, IReadOnlyCollection<ItemPart> hidden, bool reading = false, bool action = false, ItemBarAt barAt = ItemBarAt.Under, bool even = false)
    {
        ArgumentNullException.ThrowIfNull(offered);
        ArgumentNullException.ThrowIfNull(hidden);
        bool On(ItemPart part) => offered.Contains(part) && !hidden.Contains(part);
        return new ItemShape(On(ItemPart.Lead), On(ItemPart.Subtitle), On(ItemPart.End), On(ItemPart.Bar), On(ItemPart.Note), reading && On(ItemPart.End), action && On(ItemPart.End), barAt, even);
    }

    /// <summary>[S-17180] Whether a bar is drawn and stands at the item's end.</summary>
    public bool Side => Bar && BarAt == ItemBarAt.Side;

    /// <summary>[S-17180] Whether a bar is drawn under the row (a List or Column list item, a grid's card).</summary>
    public bool Under => Bar && BarAt == ItemBarAt.Under;

    /// <summary>Every part, a reading at the end, the bar standing at its end, the columns even: the device item.</summary>
    public static ItemShape Device { get; } = new(true, true, true, true, false, Reading: true, BarAt: ItemBarAt.Side, Even: true);
}

/// <summary>[S-16954] One item's box in its list, in DIPs from the card's top-left edge: its index in the list and where it stands.</summary>
public readonly record struct ItemPlace(int Index, double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;
}

/// <summary>
/// [S-16954] A list laid in its card: its arrangement, whether its lines run across (side by side) or down, its columns (a grid's),
/// the one pitch of a line along that axis with <see cref="Gap"/> between two, how many lines its items make, the first line
/// shown (scrolled to), a table's column titles' height over the rows, and the box of every item the card reaches from the
/// first line (the last cut by the card's edge).
/// </summary>
public sealed record ItemLay(ItemStyle Style, bool Across, int Columns, double Pitch, double Gap, int Lines, int First, double Header, IReadOnlyList<ItemPlace> Boxes)
{
    /// <summary>The room along the axis the lines take: the card's less a table's titles.</summary>
    public double Viewport { get; init; }

    /// <summary>A line's step: its pitch and the gap after it.</summary>
    public double Step => Pitch + Gap;

    /// <summary>The furthest first line: the step that brings the last line to the far edge (0: they all show).</summary>
    public int LastFirst => ItemScroll.LastFirst(Lines, Step, Viewport + Gap);
}

/// <summary>
/// [S-16940]..[S-16945] [S-16633] [S-16634] The item base's sizes and how a list of items is laid (owner, 2026-11-20: "the widget
/// items should actually have a fixed size ... the last item will be cutoff to some extent then"; 2026-11-23: "optionally it
/// should be possible to add a block gap into the widget so that they are dymanic but it looks like different widgets").
/// Every item has one size in whole grid cells, from the parts the list draws: <b>List</b> the device item (the inset, the
/// lead's 36 line, 12, the bar's track, the inset: 80 on the default grid), down from the card's top edge the card's whole
/// width, or across where the card holds one item's height and two items' width; <b>Column list</b> a compact row (28, or
/// 40 with a second line, the bar's 8 under it); <b>Row list</b> a column of the parts side by side from the card's left
/// edge; <b>Grid</b> cards of one size from the card's left and top edges, the whole columns the width holds; <b>Table</b>
/// rows of 28 under the column titles. The block gap is grid cells between two items (not in a table). The first line
/// shown is the scroll's (<see cref="ItemScroll"/>). Pure.
/// </summary>
public static class ItemLayout
{
    /// <summary>[S-16634] The most grid blocks between two items.</summary>
    public const int MostGap = 4;

    /// <summary>[S-16633] A List item's natural width side by side: its icon, the reading held to "100%" and a name worth reading.</summary>
    public const double AcrossWidth = 240;

    /// <summary>[S-16942] A row list's column, where the kind states none: a name worth reading.</summary>
    public const double ColumnWidth = 140;

    /// <summary>[S-16944] A grid's card, where the kind states none.</summary>
    public const double CellWidth = 140;

    /// <summary>[S-16942] A row list's standing bar.</summary>
    public const double StandingBar = FamilyShape.IconLarge;

    /// <summary>[S-16634] A list's least width: an item's icon, reading and the start of its name.</summary>
    public const double LeastWidth = 220;

    /// <summary>[S-16634] A new list's width.</summary>
    public const double NaturalWidth = 300;

    /// <summary>[S-16634] The widest a list down grows.</summary>
    public const double MostWidth = 520;

    /// <summary>[S-16945] A table's widths: its columns read whole from 260, a new one 320, a sentence across a room at 720.</summary>
    public static KitWidths TableWidths { get; } = new(260, 320, 720);

    /// <summary>[S-16634] A new list's items.</summary>
    public const int DefaultRows = 3;

    /// <summary>[S-16944] A grid's columns at its natural and its most.</summary>
    public const int GridColumns = 3, MostGridColumns = 5;

    /// <summary>
    /// [S-16631] The List item's line of parts: the lead's (a reading's Big28 line, 36) where it leads or ends with a reading,
    /// else the title over its subtitle (Body and Caption), never under the action's button.
    /// </summary>
    public static double ListLine(ItemShape shape)
    {
        var line = shape.Lead || shape.Reading
            ? FamilyType.Big28.LineHeight
            : FamilyType.Body.LineHeight + (shape.Subtitle || shape.Note ? FamilyType.Caption.LineHeight : 0);
        return shape.Action ? Math.Max(line, FamilyShape.Button) : line;
    }

    /// <summary>
    /// [S-17180] The room a bar standing at the item's end takes across: 12 from the end's reading and its track.
    /// </summary>
    public const double SideBar = FamilySpace.S3 + FamilyShape.Track;

    /// <summary>
    /// [S-16633] [S-17180] A List item's natural height: the inset, its line of parts, 12 and the bar's track where it has one
    /// under the row, the inset under it (owner, 2026-11-23: "there is no proper spacing between the bottom border and the
    /// progress bar"): 76, 80 on the default grid; a bar standing at the item's end adds no height (owner, 2026-11-23: "it
    /// should be all the way on the right side as a vertical bar"): a device's 60.
    /// </summary>
    public static double ListNatural(ItemShape shape)
        => FamilySpace.ContentTop + ListLine(shape) + (shape.Under ? FamilySpace.S3 + FamilyShape.Track : 0) + FamilySpace.Inset;

    /// <summary>[S-16943] [S-17180] A Column list row's natural height: the family's row of one or two lines, the bar's 8 under it where it lies under.</summary>
    public static double CompactNatural(ItemShape shape)
        => FamilyShape.RowHeight(shape.Subtitle || shape.Note ? 2 : 1) + (shape.Under ? FamilySpace.S1 + FamilyShape.Track : 0);

    /// <summary>[S-16942] A row list column's parts, top to bottom: the title, the lead, the end, the standing bar, the subtitle and the note, 4 apart.</summary>
    public static double StackNatural(ItemShape shape, bool standingBar)
    {
        var body = FamilyType.Body.LineHeight;
        var parts = body
            + (shape.Lead ? FamilySpace.S1 + FamilyShape.IconLarge : 0)
            + (shape.End ? FamilySpace.S1 + (shape.Action ? FamilyShape.Button : body) : 0)
            + (standingBar ? (shape.Bar ? FamilySpace.S1 + StandingBar : 0) : shape.Under ? FamilySpace.S1 + FamilyShape.Track : 0)
            + (shape.Subtitle ? FamilySpace.S1 + body : 0)
            + (shape.Note ? FamilySpace.S1 + FamilyType.Caption.LineHeight : 0);
        return parts;
    }

    /// <summary>[S-16942] A row list column's natural height: its parts and the inset over and under them, inside it.</summary>
    public static double ColumnNatural(ItemShape shape) => FamilySpace.ContentTop + StackNatural(shape, standingBar: true) + FamilySpace.Inset;

    /// <summary>[S-16944] [S-17180] A grid card's natural height: its parts (the bar a track under them, or none standing at its end) and 8 over and under them.</summary>
    public static double CellNatural(ItemShape shape) => StackNatural(shape, standingBar: false) + (2 * FamilySpace.S2);

    /// <summary>[S-16945] A table row's one height (28, 30 on the default grid), its column titles' too.</summary>
    public static double TableNatural => FamilyShape.RowHeight(1);

    /// <summary>[S-16954] An item's one size in <paramref name="style"/> on <paramref name="grid"/> (its natural in whole cells); 0 on the axis that follows the card.</summary>
    public static (double Width, double Height) Size(ItemStyle style, ItemShape shape, ItemCells cells, ItemGrid grid)
    {
        ArgumentNullException.ThrowIfNull(cells);
        return style switch
        {
            ItemStyle.RowList => (ItemSize.Optimal(cells.AcrossWidth ?? ColumnWidth, grid), ItemSize.Optimal(ColumnNatural(shape), grid)),
            ItemStyle.ColumnList => (0, ItemSize.Optimal(CompactNatural(shape), grid)),
            ItemStyle.Grid => (ItemSize.Optimal(cells.CellWidth ?? CellWidth, grid), ItemSize.Optimal(cells.CellHeight ?? CellNatural(shape), grid)),
            ItemStyle.Table => (0, ItemSize.Optimal(TableNatural, grid)),
            _ => (ItemSize.Optimal(AcrossWidth, grid), ItemSize.Optimal(ListNatural(shape), grid)),
        };
    }

    /// <summary>[S-16634] The gap between two items: <paramref name="blocks"/> grid cells (0..<see cref="MostGap"/>; 0 touching).</summary>
    public static double GapOf(int blocks, ItemGrid grid) => Math.Clamp(blocks, 0, MostGap) * grid.Pitch;

    /// <summary>[S-16945] Whether <paramref name="style"/> takes the block gap: every arrangement but the table, whose rows touch.</summary>
    public static bool TakesGap(ItemStyle style) => style != ItemStyle.Table;

    /// <summary>
    /// [S-16633] Whether <paramref name="count"/> List items stand side by side in a card <paramref name="width"/> ×
    /// <paramref name="height"/> (from its top edge): the card holds one item's height but not two, and two items' width.
    /// </summary>
    public static bool Across(int count, double width, double height, int gap, ItemShape shape, ItemGrid grid)
    {
        var g = GapOf(gap, grid);
        var (w, h) = Size(ItemStyle.List, shape, ItemCells.Base, grid);
        return count > 1
            && double.IsFinite(width) && double.IsFinite(height)
            && height + LayoutMath.Tolerance < (2 * h) + g
            && width + LayoutMath.Tolerance >= (2 * w) + g;
    }

    /// <summary>
    /// [S-16954] Lay <paramref name="count"/> items in a card <paramref name="width"/> × <paramref name="height"/> (the room from
    /// the card's top edge to its foot) in <paramref name="style"/>: each its one fixed size from the card's start (a table's
    /// rows under its column titles where <paramref name="titles"/>), the gap between two, from line <paramref name="first"/>
    /// (held to the scroll's limits) every one the card reaches (at least one), the last cut by the card's edge. Every edge
    /// on a whole device pixel.
    /// </summary>
    public static ItemLay Lay(ItemStyle style, int count, double width, double height, int gap, ItemShape shape, ItemCells cells, ItemGrid grid, int first = 0, bool titles = false)
    {
        ArgumentNullException.ThrowIfNull(cells);
        count = Math.Max(0, count);
        width = double.IsFinite(width) ? Math.Max(0, width) : 0;
        height = double.IsFinite(height) ? Math.Max(0, height) : 0;
        var g = TakesGap(style) ? GapOf(gap, grid) : 0;
        var (w, h) = Size(style, shape, cells, grid);
        var raster = grid.Raster;

        bool across;
        int columns;
        double pitch, header = 0;
        switch (style)
        {
            case ItemStyle.RowList:
                (across, columns, pitch) = (true, 1, w);
                break;
            case ItemStyle.Grid:
                // [S-16944] The whole columns the width holds at the card's one width, the room past them empty (WR-4).
                columns = Math.Max(1, (int)Math.Floor((width + g + LayoutMath.Tolerance) / (w + g)));
                (across, pitch) = (false, h);
                break;
            case ItemStyle.Table:
                header = titles ? h : 0;
                (across, columns, pitch) = (false, 1, h);
                break;
            case ItemStyle.ColumnList:
                (across, columns, pitch) = (false, 1, h);
                break;
            default:
                across = Across(count, width, height, gap, shape, grid);
                (columns, pitch) = (1, across ? w : h);
                break;
        }

        var lines = columns > 1 ? (count + columns - 1) / columns : count;
        var viewport = Math.Max(0, (across ? width : height) - header);
        var lay = new ItemLay(style, across, columns, pitch, g, lines, 0, header, []) { Viewport = viewport };
        first = Math.Clamp(first, 0, lay.LastFirst);

        var boxes = new List<ItemPlace>();
        for (var line = first; line < lines; line++)
        {
            var at = line - first;
            var start = header + ItemSize.OnPixels(at * (pitch + g), raster);
            if (line > first && start >= header + viewport - LayoutMath.Tolerance)
            {
                break;
            }

            var length = header + ItemSize.OnPixels((at * (pitch + g)) + pitch, raster) - start;
            for (var c = 0; c < columns; c++)
            {
                var index = (line * columns) + c;
                if (index >= count)
                {
                    break;
                }

                if (across)
                {
                    boxes.Add(new ItemPlace(index, start, 0, length, style == ItemStyle.RowList ? Math.Max(height, 0) : h));
                }
                else if (style == ItemStyle.Grid)
                {
                    var x = ItemSize.OnPixels(c * (w + g), raster);
                    boxes.Add(new ItemPlace(index, x, start, ItemSize.OnPixels((c * (w + g)) + w, raster) - x, length));
                }
                else
                {
                    boxes.Add(new ItemPlace(index, 0, start, width, length));
                }
            }
        }

        return lay with { First = first, Boxes = boxes };
    }

    /// <summary>
    /// [S-16634] [S-16954] A list's sizing in <paramref name="style"/>: <paramref name="rows"/> items at its natural (the gap
    /// between two where it is on; a grid's in lines of three, a row list's side by side), one item at its least, twenty at
    /// its most; a list down 220..520 wide, a new one 300 (a table's <see cref="TableWidths"/>); every cell between.
    /// </summary>
    public static WidgetSizing Sizing(ItemStyle style, int rows, int gap, ItemShape shape, ItemCells? cells = null, bool titles = false)
    {
        cells ??= ItemCells.Base;
        var grid = ItemGrid.Default();
        rows = KitSizing.ClampRows(rows);
        var g = TakesGap(style) ? GapOf(gap, grid) : 0;
        var (w, h) = Size(style, shape, cells, grid);
        double Run(int n, double each) => (n * each) + (Math.Max(0, n - 1) * g);
        switch (style)
        {
            case ItemStyle.RowList:
                // [S-16942] Its columns from the card's left edge, one height: its parts' column. [S-17101] A taller card would
                // only stand room under them: the height is the column's.
                return WidgetSizing.Range(new(w, h), new(Run(rows, w), h), new(Run(KitSizing.MaxRows, w), h)) with { KeepsHeight = true };

            case ItemStyle.Grid:
            {
                var across = Math.Min(GridColumns, rows);
                var down = (rows + across - 1) / across;
                return WidgetSizing.Range(new(w, h), new(Run(across, w), Run(down, h)), new(Run(MostGridColumns, w), Run((KitSizing.MaxRows + GridColumns - 1) / GridColumns, h)));
            }

            case ItemStyle.Table:
            {
                var over = titles ? h : 0;
                var widths = TableWidths;
                return WidgetSizing.Range(
                    new(widths.Min, over + h),
                    new(widths.Natural, over + (rows * h)),
                    new(widths.Max, over + (KitSizing.MaxRows * h))) with { Room = StretchRoom.List };
            }

            default:
                return WidgetSizing.Range(
                    new(LeastWidth, h),
                    new(NaturalWidth, Run(rows, h)),
                    new(MostWidth, Run(KitSizing.MaxRows, h))) with { Room = StretchRoom.List };
        }
    }

    /// <summary>[S-16954] <paramref name="config"/>'s sizing for a kind whose items have <paramref name="offered"/>'s parts.</summary>
    public static WidgetSizing Sizing(IItemList config, IReadOnlyList<ItemPart> offered, bool reading = false, bool action = false, ItemCells? cells = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        return Sizing(config.Arrangement, config.Rows, config.Gap, ItemShape.Of(offered, config.HiddenParts, reading, action), cells, config.ColumnTitles);
    }

    /// <summary>
    /// [S-16637] One item's face's sizing (a Drive, a Bluetooth device, a Meeting, a Contact): one List item from the card's top
    /// edge, the card one item tall, 220..520 wide, a new one 300; never narrower than its lead, its end and the start of its
    /// subtitle (<paramref name="widestLine"/> up to 96 DIP: past it the subtitle is cut with its ellipsis) and
    /// <paramref name="end"/> wide at its end. [S-17101] One item that does not repeat: the card is as tall as it, never taller.
    /// </summary>
    public static WidgetSizing Face(ItemShape shape, double widestLine, double end)
    {
        var h = ItemSize.Optimal(ListNatural(shape), ItemGrid.Default());
        var lead = shape.Lead ? FamilyType.Big28.LineHeight + FamilySpace.GlyphGap : 0;
        var side = shape.Side ? SideBar : 0;
        var least = Math.Max(LeastWidth, Math.Ceiling(Math.Min(widestLine, FamilySpace.ColumnGap * 8) + lead + FamilySpace.S3 + end + side + (2 * FamilySpace.Inset)));
        return WidgetSizing.Range(new(least, h), new(Math.Max(NaturalWidth, least), h), new(Math.Max(MostWidth, least), h)) with { KeepsHeight = true };
    }

    /// <summary>
    /// [S-17181] The one width of a list's end column where its columns are even (owner, 2026-11-23: "the columns in the device
    /// view should also all get the same width"): the widest end any of its items draws, so every item's name and details
    /// take the same room and their readings line up.
    /// </summary>
    public static double EvenEnd(IEnumerable<double> ends)
        => Math.Ceiling((ends ?? []).Where(double.IsFinite).DefaultIfEmpty(0).Max());

    /// <summary>[S-16631] A reading's widest form ("100%") at 28, estimated.</summary>
    public static double ReadingWidth => Math.Ceiling(HeroFit.Ems("100%") * FamilyType.Big28.FontSize);

    /// <summary>[S-16637] The widest of <paramref name="lines"/> on the item's subtitle (Caption), estimated in DIPs.</summary>
    public static double WidestLine(IEnumerable<string> lines) => (lines ?? []).Select(HeroFit.Ems).DefaultIfEmpty(0).Max() * FamilyType.Caption.FontSize;
}
