using Themia.Sdk;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.Contacts;

/// <summary>[S-11406] What the contacts widget draws, from its config alone (<see cref="ContactsPlan.Of"/>).</summary>
/// <param name="Layout">The layout drawn: the chosen one, at every size.</param>
/// <param name="Columns">List and compact rows: one column; the grid: cells per line.</param>
/// <param name="ColumnWidth">A column's width (the grid: a cell's).</param>
/// <param name="Lines">[S-6712] The lines every row shows (1 to 3).</param>
/// <param name="Avatar">The list's rows lead with the person's 32 monogram.</param>
/// <param name="Aside">Compact rows carry the one field as their value.</param>
/// <param name="Icon">The grid: a cell's monogram (48).</param>
/// <param name="Pitch">A row's (a grid line's) height.</param>
/// <param name="Rows">How many rows (grid lines) show: what the card holds; the rest is a scroll away.</param>
public readonly record struct ContactsLayoutPlan(ContactsLayout Layout, int Columns, double ColumnWidth, int Lines, bool Avatar, bool Aside, double Icon, double Pitch, int Rows)
{
    /// <summary>[S-12807] [S-14810] The rows laid as <paramref name="reach"/> says: at their one fixed size, touching, as many as the room reaches.</summary>
    public ContactsLayoutPlan With(RowReach reach) => this with { Pitch = reach.Pitch, Rows = reach.Shown, Reach = reach };

    /// <summary>[S-14810] How the rows stand in the room: each one's edges on whole device pixels; none before the card is measured.</summary>
    public RowReach Reach { get; init; }

    /// <summary>[S-13950] The lines of the room over the list's first row: the search line that takes the first line, else none.</summary>
    public int First { get; init; }

    /// <summary>
    /// [S-14810] Row <paramref name="index"/>'s height: its one fixed size on whole device pixels (<see cref="RowReach.Height"/>,
    /// counted past <see cref="First"/>); the pitch before it is laid.
    /// </summary>
    public double RowAt(int index) => Reach.Pitch > 0 ? Reach.Height(index + First) : Pitch;

    /// <summary>[S-14810] Where the list's first row starts down the room: under the <see cref="First"/> lines over it.</summary>
    public double Top => Reach.Pitch > 0 ? Reach.Start(First) : First * Pitch;

    /// <summary>[S-14810] The length <paramref name="count"/> rows take from the list's first: as <see cref="RowAt"/> lays them.</summary>
    public double Span(int count) => count <= 0 ? 0 : Reach.Pitch > 0 ? Reach.Start(count + First) - Reach.Start(First) : count * Pitch;

    /// <summary>[S-14810] The room the list's rows stand in from its first: the card's to its foot, the last row cut by it.</summary>
    public double ListRoom => Reach.Pitch > 0 ? Math.Max(0, Reach.Room - Top) : double.PositiveInfinity;
}

/// <summary>
/// [S-11406] The contacts widget's plan and sizes on the family's parts ([S-6711]..[S-6713]), each
/// layout its own: list rows of the chosen lines (the name; then the role, else a way to reach the
/// person; then the address, else the number) led by a 32 monogram; compact rows of one line with the
/// one field as their value; the details table of one line under its column titles; the grid's cells
/// (<c>Row.Cell</c>: a 48 monogram over the name), the chosen cells a line and lines. The search box
/// over them where the config has it. No size decides any of it.
/// </summary>
public static class ContactsPlan
{
    /// <summary>[S-1275] [S-12834] The search box: one control of 28, the rows right under it (grown to a grid line in the view).</summary>
    public const double SearchHeight = FamilyShape.Button;

    /// <summary>[S-582] The details table's column titles: one Caption line and the gap under it.</summary>
    public static double HeaderHeight => FamilyType.Caption.LineHeight + FamilySpace.S2;

    /// <summary>
    /// [S-15775] What stands over the rows from the card's top edge, each a band of whole cells of <paramref name="grid"/>
    /// touching the next (WR-1: nothing over the first): the search field's band where it shows (<paramref name="field"/>),
    /// then the details table's column titles.
    /// </summary>
    public static double Bands(ContactsConfig config, bool field, ItemGrid grid)
    {
        ArgumentNullException.ThrowIfNull(config);
        return (field ? FieldBand(config, grid) : 0)
            + (config.EffectiveLayout == ContactsLayout.Details ? ItemSize.Optimal(HeaderHeight, grid) : 0);
    }

    /// <summary>
    /// [S-15785] The search field's band: a line of the list's own at a row's one size (the list's rows of its lines, a
    /// one-line row elsewhere; the search line opened in the list's first line is that size too), never under the family's
    /// control of 28 in whole cells (WR-11: "Rows look like rows everywhere").
    /// </summary>
    public static double FieldBand(ContactsConfig config, ItemGrid grid)
    {
        ArgumentNullException.ThrowIfNull(config);
        var row = config.EffectiveLayout == ContactsLayout.List ? FamilyShape.RowHeight(KitSizing.ClampLines(config.Lines, KitSizing.MaxLines)) : FamilyShape.Row1;
        return Math.Max(ItemSize.Optimal(row, grid), ItemSize.Optimal(SearchHeight, grid));
    }

    /// <summary>[S-11406] The grid's monogram: one size, the cells' line and the card's width follow it.</summary>
    public const double CellIcon = FamilyShape.IconHuge;

    /// <summary>[S-11406] A grid cell's width: its monogram and 24 either side for the name.</summary>
    public const double CellWidth = CellIcon + (2 * FamilySpace.S6);

    /// <summary>[S-11406] A grid line's height: the monogram, 4, the name's Caption line.</summary>
    public static double CellHeight => RowRules.CellHeight(CellIcon, named: true);

    /// <summary>[S-14817] A grid cell's box: its monogram and name with 6 either side, inside the cell (WR-3), in whole grid cells.</summary>
    public const double CellNaturalWidth = CellWidth + FamilySpace.ColumnGap;

    /// <summary>[S-14817] A grid cell's box's height: the monogram and name with 6 over and under, inside the cell.</summary>
    public static double CellNaturalHeight => CellHeight + FamilySpace.ColumnGap;

    /// <summary>[S-582] The narrowest a details column says anything in.</summary>
    public const double DetailColumn = 88;

    /// <param name="scale">The display's rasterization scale: a stretched row stays whole device pixels.</param>
    public static ContactsLayoutPlan Of(WidgetArea area, ContactsConfig config, double scale = 1)
    {
        ArgumentNullException.ThrowIfNull(config);
        var width = Math.Max(0, area.Width);
        var rows = KitSizing.ClampRows(config.Rows);
        var lines = KitSizing.ClampLines(config.Lines, KitSizing.MaxLines);
        var over = Bands(config, config.SearchBox, area.Grid(scale));

        return config.EffectiveLayout switch
        {
            ContactsLayout.Grid => Cells(area, config),
            ContactsLayout.Details => new ContactsLayoutPlan(ContactsLayout.Details, 1, width, 1, false, false, 0, FamilyShape.Row1, rows).With(Reach(area, over, 1, scale)),
            ContactsLayout.Compact => new ContactsLayoutPlan(ContactsLayout.Compact, 1, width, 1, false, true, 0, FamilyShape.Row1, rows).With(Reach(area, over, 1, scale)),
            _ => new ContactsLayoutPlan(ContactsLayout.List, 1, width, lines, true, false, 0, FamilyShape.RowHeight(lines), rows).With(Reach(area, over, lines, scale)),
        };
    }

    /// <summary>
    /// [S-11406] [S-14410] [S-14810] [S-14817] The grid in <paramref name="area"/>: its cells at their one fixed size, touching,
    /// from the card's left edge and from its top edge (under the search box where it shows), every column its width
    /// reaches, the last cut by its right edge (at least 2, no most: how many of the same item follows the card,
    /// [S-14410]) and as many lines as its height reaches, the last cut by the
    /// card's foot (<see cref="ItemSize.Cells"/>); before the box is measured, a new one's grid.
    /// </summary>
    private static ContactsLayoutPlan Cells(WidgetArea area, ContactsConfig config)
    {
        var grid = area.Grid();
        if (!area.IsMeasured)
        {
            return new ContactsLayoutPlan(ContactsLayout.Grid, ContactsConfig.DefaultGridColumns, ItemSize.Optimal(CellNaturalWidth, grid), 1, false, false, CellIcon, ItemSize.Optimal(CellNaturalHeight, grid), Math.Clamp(config.GridRows, 1, ContactsConfig.MaxGrid));
        }

        var down = area.Height + FamilySpace.Inset - Bands(config, config.SearchBox, grid);
        var cells = ItemSize.Cells(area.Width, down, CellNaturalWidth, CellNaturalHeight, grid, 2);
        return new ContactsLayoutPlan(ContactsLayout.Grid, cells.Columns, cells.Across.Pitch, 1, false, false, CellIcon, cells.Down.Pitch, cells.Lines);
    }

    /// <summary>
    /// [S-12807] [S-14810] [S-15775] The rows under <paramref name="over"/> (the bands from the card's top edge: the search
    /// field's, the column titles', <see cref="Bands"/>): at their one fixed size, touching, from the card's top edge or right
    /// under those bands, as many as the card reaches, the last cut by its foot; one before the box is measured.
    /// </summary>
    private static RowReach Reach(WidgetArea area, double over, int lines, double scale)
        => area.IsMeasured
            ? ItemSize.Rows(area.Height, lines, area.Grid(scale), over)
            : ItemSize.Natural(lines, area.Grid(scale));

    /// <summary>
    /// [S-11406] Each layout's own sizes, with the search box where it is on: List and Compact the
    /// kit's widths (220..520, a new one 300), their height the rows; Details as wide as its columns
    /// need (88 a column) to 720, its height the column titles and the rows; the Grid its cells, a new one
    /// <see cref="ContactsConfig.DefaultGridColumns"/> a line (the card's: [S-14410]) and its rows down. [S-14500] Every
    /// cell between; [S-14810] the items keep their size at every one.
    /// </summary>
    public static WidgetSizing Sizing(ContactsConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.EffectiveLayout == ContactsLayout.Grid)
        {
            // [S-14817] [S-15775] The cells alone, touching, from the card's edges (under the search field's band where it shows).
            var cells = ItemGrid.Default();
            var above = Bands(config, config.SearchBox, cells);
            double Across(int n) => n * ItemSize.Optimal(CellNaturalWidth, cells);
            double Down(int n) => above + (n * ItemSize.Optimal(CellNaturalHeight, cells));
            var rows = Math.Clamp(config.GridRows, 1, ContactsConfig.MaxGrid);
            return WidgetSizing.Range(new(Across(2), Down(1)), new(Across(ContactsConfig.DefaultGridColumns), Down(rows)), new(Across(ContactsConfig.MaxGrid), Down(ContactsConfig.MaxGrid)));
        }

        var widths = config.EffectiveLayout == ContactsLayout.Details ? DetailWidths(config.EffectiveColumns.Count) : new KitWidths(KitSizing.MinWidth, KitSizing.NaturalWidth, KitSizing.MaxWidth);
        var (over, row) = Parts(config);
        return WidgetSizing.Range(
            new(widths.Min, over + row),
            new(widths.Natural, over + (KitSizing.ClampRows(config.Rows) * row)),
            new(widths.Max, over + (KitSizing.MaxRows * row)));
    }

    /// <summary>[S-582] A details table reads from two columns of 88 (or its own count) to 720.</summary>
    public static KitWidths DetailWidths(int columns)
    {
        var min = (2 * FamilySpace.Inset) + (Math.Max(2, columns) * DetailColumn);
        var natural = Math.Max(min, 420);
        return new KitWidths(min, natural, Math.Max(natural, 720));
    }

    /// <summary>
    /// [S-14810] [S-15775] What stands over the rows (the search field's band, the column titles' band, from the card's top
    /// edge, as <see cref="Reach"/> lays them) and a row's optimal, on the default grid.
    /// </summary>
    private static (double Over, double Row) Parts(ContactsConfig config)
    {
        var layout = config.EffectiveLayout;
        var grid = ItemGrid.Default();
        var row = layout == ContactsLayout.List ? FamilyShape.RowHeight(KitSizing.ClampLines(config.Lines, KitSizing.MaxLines)) : FamilyShape.Row1;
        return (Bands(config, config.SearchBox, grid), ItemSize.Optimal(row, grid));
    }

    /// <summary>
    /// [S-1270] A list row's lines under the name: the role, else a way to reach the person; then the
    /// address, else the number (each only once). As many as the plan's lines leave room for.
    /// </summary>
    public static (string? Second, string? Third) Lines(Contact contact, int lines)
    {
        ArgumentNullException.ThrowIfNull(contact);

        var role = ContactRules.Role(contact);
        var email = contact.Email?.Trim() is { Length: > 0 } e ? e : null;
        var phone = contact.Phone?.Trim() is { Length: > 0 } p ? p : null;
        var reach = email ?? phone;
        string?[] all = role.Length > 0 ? [role, reach] : [reach, email is not null ? phone : null];
        var shown = all.Where(s => s is not null).ToList();
        return (lines >= 2 && shown.Count > 0 ? shown[0] : null, lines >= 3 && shown.Count > 1 ? shown[1] : null);
    }
}
