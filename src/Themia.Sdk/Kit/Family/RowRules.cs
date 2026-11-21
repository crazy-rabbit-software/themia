using Themia.Core.Widgets;

namespace Themia.Sdk.Kit;

/// <summary>[S-6711] Which of a row's two texts is the information.</summary>
public enum RowKind
{
    /// <summary>A list's item (a task, a mail, a file, a contact, a feed's article): the name is read, a value (time, size) labels it.</summary>
    List,

    /// <summary>A fact (Humidity 71 %, Upload 38 KB/s): the value is read, the name labels it.</summary>
    Fact,
}

/// <summary>[S-6711] The colour roles of a row's parts.</summary>
public readonly record struct RowRoles(ColourRole Name, ColourRole Value, ColourRole Lead, ColourRole Second, ColourRole Third);

/// <summary>
/// [S-6711] The Row block: on one line, [lead: a 16 glyph, a 32 avatar, app or file icon, a check]
/// 8 [name, Body, at the widget's one text edge] 12 [an optional inline meter] 12 [value, Body,
/// end-aligned, tabular]; a second line in Caption secondary, a third in Caption tertiary; 28, 40 or
/// 56 tall by its lines, which its widget's config states ([S-6712]). No dividers; hover and selection through the item layer.
/// <para>
/// One rule for colour: what is read is primary. A fact row's value is primary and its name
/// secondary; a list row's name is primary and its value secondary. A widget's rows all lead or
/// none do, so every name starts at one x: 40 from the card's edge with 16 glyphs, 16 without.
/// </para>
/// </summary>
public static class RowRules
{
    /// <summary>
    /// The roles of a row of <paramref name="kind"/>; a row of a part that is missing on this PC is all
    /// disabled ([S-1940]). [S-2270] A <paramref name="quiet"/> list row (a read message in a list that
    /// tells unread from read by its names) has its name in the secondary role.
    /// </summary>
    public static RowRoles Roles(RowKind kind, bool available = true, bool quiet = false)
    {
        if (!available)
        {
            return new RowRoles(ColourRole.TextDisabled, ColourRole.TextDisabled, ColourRole.TextDisabled, ColourRole.TextDisabled, ColourRole.TextDisabled);
        }

        return kind == RowKind.Fact
            ? new RowRoles(ColourRole.TextSecondary, ColourRole.TextPrimary, ColourRole.TextSecondary, ColourRole.TextSecondary, ColourRole.TextTertiary)
            : new RowRoles(quiet ? ColourRole.TextSecondary : ColourRole.TextPrimary, ColourRole.TextSecondary, ColourRole.TextSecondary, ColourRole.TextSecondary, ColourRole.TextTertiary);
    }

    /// <summary>[S-6712] How many whole rows of <paramref name="lines"/> lines fit <paramref name="height"/>; never part of one.</summary>
    public static int Fit(double height, int lines)
        => double.IsFinite(height) && height > 0 ? (int)Math.Floor((height + 0.01) / FamilyShape.RowHeight(lines)) : 0;

    /// <summary>
    /// [S-6711] Where a row's name starts inside the row's own inset: after a 16 lead and its gap, after a
    /// 32 lead and its gap, or at the start.
    /// </summary>
    public static double NameStart(double lead) => lead > 0 ? lead + FamilySpace.GlyphGap : 0;

    /// <summary>[S-6713] A cell's height: its icon, 4, the name's Caption line.</summary>
    public static double CellHeight(double icon, bool named)
        => icon + (named ? FamilySpace.S1 + FamilyType.Caption.LineHeight : 0);

    /// <summary>
    /// [S-6713] The least width of a cell's lead and name: a named cell its icon and 16 either side (room
    /// for a short name in Caption), an unnamed one its icon alone.
    /// </summary>
    public static double CellLeast(double icon, bool named) => icon + (named ? 2 * FamilySpace.S4 : 0);

    /// <summary>[S-6713] [S-14816] A cell's box across: its lead and name with 8 either side, the padding inside the cell (WR-3).</summary>
    public static double CellBoxWidth(double icon, bool named) => CellLeast(icon, named) + (2 * FamilySpace.S2);

    /// <summary>[S-6713] [S-14816] A cell's box down: its lead and name with 8 over and under them, inside the cell.</summary>
    public static double CellBoxHeight(double icon, bool named) => CellHeight(icon, named) + (2 * FamilySpace.S2);

    /// <summary>
    /// [S-6713] [S-14816] A grid of cells in a box <paramref name="width"/> × <paramref name="height"/> (the card's width, from
    /// the items' top to the card's foot) on <paramref name="grid"/> (owner, 2026-11-21: "the weather forecast widget grid
    /// view items do not go from edge to edge, they have space everywhere. ... fix this, also for other widgets if
    /// applicable."): each cell's box one fixed size (<see cref="CellBoxWidth"/> × <see cref="CellBoxHeight"/> in whole grid
    /// cells, its hover and selection the whole box), touching, from the card's left edge and the items' top: as many
    /// columns as the width holds whole and the lines the height reaches, the last cut by the card's foot
    /// (<see cref="ItemSize.Cells"/>).
    /// </summary>
    public static CellGrid Cells(double width, double height, double icon, bool named, ItemGrid grid)
    {
        var reach = ItemSize.Cells(width, height, CellBoxWidth(icon, named), CellBoxHeight(icon, named), grid);
        return new CellGrid(reach.Columns, reach.Lines, reach.Across.Pitch, reach.Down.Pitch, icon, named) { Scale = grid.Raster };
    }
}

/// <summary>[S-6713] [S-14816] A grid of cells: its columns and the lines the box reaches, one cell's box and its lead.</summary>
/// <param name="Columns">Cells side by side, at least one.</param>
/// <param name="Rows">Lines of cells the box reaches, the last cut by its foot (more scroll).</param>
/// <param name="Width">A cell's box: its one width, the padding inside it.</param>
/// <param name="Height">A cell's box's one height.</param>
/// <param name="Icon">The lead's size.</param>
/// <param name="Named">Whether the cells show their names.</param>
public readonly record struct CellGrid(int Columns, int Rows, double Width, double Height, double Icon, bool Named)
{
    /// <summary>The display's raster scale the cells' edges fall on.</summary>
    public double Scale { get; init; } = 1;

    /// <summary>Where column <paramref name="column"/> starts from the card's left edge, on a whole device pixel.</summary>
    public double Left(int column) => ItemSize.OnPixels(Math.Max(0, column) * Width, Scale);

    /// <summary>Where line <paramref name="line"/> starts from the items' top, on a whole device pixel.</summary>
    public double Top(int line) => ItemSize.OnPixels(Math.Max(0, line) * Height, Scale);

    /// <summary>A cell's lead and name inside its box: the box less its padding.</summary>
    public double ContentWidth => Math.Max(0, Width - (2 * FamilySpace.S2));

    /// <summary>The cells the box shows.</summary>
    public int Visible => Columns * Rows;
}
