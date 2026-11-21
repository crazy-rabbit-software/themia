using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-1326] The tool line's parts ([S-6710], [S-12481]): at most two buttons — Back and Up in navigate mode, else
/// Search and Sort (Search alone in Details, whose column headers sort) — and the git badge as its
/// informing label (list and details, inside a repository).
/// </summary>
public readonly record struct FolderHeaderPlan(bool Back, bool Up, bool Search, bool Sort, bool Git)
{
    public static FolderHeaderPlan None => default;

    /// <summary>The buttons the line holds (never more than two, [S-6710]).</summary>
    public int Buttons => (Back ? 1 : 0) + (Up ? 1 : 0) + (Search ? 1 : 0) + (Sort ? 1 : 0);
}

/// <summary>[S-1323] One layout pass of the folder widget: what the body and the tool line show, from the config.</summary>
/// <param name="Breadcrumb">The path as a row of 28 above the items (the Path setting).</param>
/// <param name="SearchBox">The search box above the items (the Search box setting); one opened from the tool line stands in its label and takes no room ([S-13950]).</param>
/// <param name="ColumnHeader">The details table's column titles (the Column titles setting).</param>
/// <param name="Columns">The details table's columns after Name: every one chosen.</param>
/// <param name="Modified">The list's one value, the date modified (the Date modified setting).</param>
/// <param name="Grid">The cells of the grid and icons styles; null for the rows.</param>
/// <param name="Header">The tool line's parts.</param>
public sealed record FolderPlan(
    bool Breadcrumb,
    bool SearchBox,
    bool ColumnHeader,
    IReadOnlyList<FolderColumn> Columns,
    bool Modified,
    CellGrid? Grid,
    FolderHeaderPlan Header);

/// <summary>
/// [S-1323] [S-11203] The folder widget's layout on the family's blocks ([S-6711], [S-6713]): rows of 28
/// (one line, the file's own 16 icon as the lead) or a grid of cells (32). What stands over the items is
/// the config's (the search box, the path, the details' titles, the list's date), never the box's; the
/// box only decides how many of the folder's entries show, as a window does (the rest scrolls). The
/// heights are the family's: a row 28, the column titles a Caption line and 8, the search box 32 and 8,
/// the path 28; the card's bottom inset is kept.
/// </summary>
public static class FolderLayout
{
    /// <summary>A row: the Row block's one line ([S-6712]).</summary>
    public const double RowHeight = FamilyShape.Row1;

    /// <summary>A row's lead: the file's own icon at the family's 16.</summary>
    public const double Lead = FamilyShape.Icon;

    /// <summary>The path's row.</summary>
    public const double BreadcrumbHeight = FamilyShape.Row1;

    /// <summary>[S-12834] The search box: the family's control of 28, the items right under it (grown to a grid line in the view, <see cref="SearchBand"/>).</summary>
    public const double SearchHeight = FamilyShape.Button;

    /// <summary>
    /// [S-12834] The search box's band under the tool line (and the path's row) on <paramref name="grid"/>: the control of 28
    /// grown to end on a grid line, so the items start right under it (WR-1, WR-5).
    /// </summary>
    public static double SearchBand(ItemGrid grid, bool breadcrumb)
        => LineRules.Under(grid, FamilySpace.Inset + LineRules.Band(grid) + (breadcrumb ? CrumbBand(grid) : 0), SearchHeight);

    /// <summary>
    /// [S-14102] The path's band under the tool line on <paramref name="grid"/>: its row of 28 grown to end on a grid line, so
    /// what stands under it starts on one (WR-6; owner, 2026-11-20: no cell left over under the last row).
    /// </summary>
    public static double CrumbBand(ItemGrid grid) => LineRules.Under(grid, FamilySpace.Inset + LineRules.Band(grid), BreadcrumbHeight);

    /// <summary>[S-14102] The details table's titles' band under what stands over them (<paramref name="top"/> from the card's top edge), grown to end on a grid line.</summary>
    public static double HeaderBand(ItemGrid grid, double top) => LineRules.Under(grid, top, ColumnHeaderHeight);

    /// <summary>
    /// [S-14102] What stands over the items, from the card's top edge, on <paramref name="grid"/>: the inset and the tool
    /// line, the path, the search box, the titles, each a band of whole cells.
    /// </summary>
    public static double Over(ItemGrid grid, bool search, bool header, bool breadcrumb)
    {
        var top = FamilySpace.Inset + LineRules.Band(grid) + (breadcrumb ? CrumbBand(grid) : 0) + (search ? SearchBand(grid, breadcrumb) : 0);
        return top + (header ? HeaderBand(grid, top) : 0);
    }

    /// <summary>The details table's titles: a Caption line and 8, as the kit's table ([S-7034]).</summary>
    public static double ColumnHeaderHeight => KitPlanner.ColumnHeaderHeight;

    /// <summary>Where a row's name starts inside its own inset: after the 16 lead and its gap.</summary>
    public static double NameStart => RowRules.NameStart(Lead);

    /// <summary>
    /// One layout pass. <paramref name="area"/> is the view's slot (the card less its top inset);
    /// <paramref name="anyFile"/> whether anything shown is a file ([S-6411]).
    /// </summary>
    public static FolderPlan Plan(WidgetArea area, FolderConfig config, int count, bool anyFile, bool navigate, bool hasGit)
    {
        ArgumentNullException.ThrowIfNull(config);

        var tiles = config.Style is FolderStyle.Grid or FolderStyle.Icons;
        var details = config.Style == FolderStyle.Details;
        var width = Math.Max(0, area.Width - (2 * FamilySpace.Inset));
        var search = config.SearchBox;
        var header = details && config.ColumnTitles;
        var named = config.Style == FolderStyle.Grid;
        // [S-14816] The cells from the card's left edge and the items' top to the card's foot, the last line cut by it.
        CellGrid? grid = tiles ? RowRules.Cells(area.Width, ItemsHeight(area, search, header, config.ShowPath, rows: true), CellIcon, named, area.Grid()) : null;

        return new FolderPlan(
            config.ShowPath,
            search,
            header,
            config.DetailColumns,
            config.Style == FolderStyle.List && config.ShowDate,
            grid,
            Header(config, navigate, hasGit));
    }

    /// <summary>[S-11203] The grid's and the icons' cell icon: the family's 32 at every size.</summary>
    public const double CellIcon = FamilyShape.IconLarge;

    /// <summary>[S-11203] The widest and tallest a folder grows: past them a desktop widget is a window.</summary>
    public static readonly SizeDip Largest = new(800, 640);

    /// <summary>[S-11203] The least a folder is: room for a short name and its date, and a few entries.</summary>
    public static readonly SizeDip Least = new(220, 160);

    /// <summary>
    /// [S-11203] The details table's least width: the insets, the lead, a name of 120 and every chosen
    /// column with its gap. It never drops a column for want of room.
    /// </summary>
    public static double DetailsLeast(FolderConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return (2 * FamilySpace.Inset) + NameStart + FolderColumns.NameMinWidth
            + config.DetailColumns.Sum(column => FolderColumns.Width(column) + FolderColumns.Gap);
    }

    /// <summary>
    /// [S-11203] The least height: the inset, the tool line, what the config stands over the items, one row of them
    /// (or one row of cells) and the bottom inset; never under <see cref="Least"/>.
    /// </summary>
    public static double LeastHeight(FolderConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var one = config.Style is FolderStyle.Grid or FolderStyle.Icons
            ? RowRules.CellHeight(CellIcon, config.Style == FolderStyle.Grid) + FamilySpace.S3
            : RowHeight;
        var over = ToolLineHeight + (config.SearchBox ? SearchHeight : 0) + (config.ShowPath ? BreadcrumbHeight : 0)
            + (config.Style == FolderStyle.Details && config.ColumnTitles ? ColumnHeaderHeight : 0);
        return Math.Max(Least.Height, FamilySpace.ContentTop + over + one + FamilySpace.Inset);
    }

    /// <summary>
    /// [S-11203] A folder keeps a free size, as a file browser's window does: 220..800 wide (the details
    /// table from the width its columns need) and 160..640 high (more if what stands over the items needs
    /// it); more room shows more of the folder's entries and the rest scrolls. A new one: the list, the grid
    /// and the icons 300 × 280; the details table its least width and 60 more, 280 high. [S-14500] Every cell between:
    /// the list's and the table's rows the card holds stretch to fill it.
    /// </summary>
    public static WidgetSizing Sizing(FolderConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var leastWidth = config.Style == FolderStyle.Details ? Math.Max(Least.Width, DetailsLeast(config)) : Least.Width;
        var leastHeight = LeastHeight(config);
        var natural = config.Style == FolderStyle.Details ? leastWidth + 60 : 300;
        return WidgetSizing.Range(
            new(leastWidth, leastHeight),
            new(Math.Min(natural, Largest.Width), Math.Max(NaturalHeight(config), leastHeight)),
            new(Math.Max(leastWidth, Largest.Width), Math.Max(leastHeight, Largest.Height)));
    }

    /// <summary>
    /// [S-11203] A new folder's height: the whole rows (or rows of cells) that come nearest 280 under
    /// what stands over them, and not a part of one more, so no room at its foot is spare.
    /// </summary>
    public static double NaturalHeight(FolderConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var tiles = config.Style is FolderStyle.Grid or FolderStyle.Icons;
        var (row, gap) = tiles ? (RowRules.CellHeight(CellIcon, config.Style == FolderStyle.Grid), FamilySpace.S3) : (RowHeight, 0.0);
        var over = ToolLineHeight + (config.SearchBox ? SearchHeight : 0) + (config.ShowPath ? BreadcrumbHeight : 0)
            + (config.Style == FolderStyle.Details && config.ColumnTitles ? ColumnHeaderHeight : 0);
        var room = 280 - FamilySpace.ContentTop - FamilySpace.Inset - over;
        var rows = Math.Max(1, (int)Math.Round((room + gap) / (row + gap)));
        return FamilySpace.ContentTop + over + (rows * (row + gap)) - gap + FamilySpace.Inset;
    }

    /// <summary>
    /// [S-1326] [S-12481] [S-12834] The folder's tool line over everything (its name, the ring, the git badge, two buttons) and
    /// the gap under it: its whole-cell band on the default grid, for the sizing (the view lays the band of its card's grid).
    /// </summary>
    public static double ToolLineHeight => LineRules.NaturalBand;

    /// <summary>
    /// The height the items have: the view's area less what stands above them (the tool line first) and, for the grid's and
    /// the icons' cells, the card's bottom inset (their marks stand inside it). [S-14102] A list's or a table's rows run to
    /// the card's foot edge (<paramref name="rows"/>). <see cref="Rows"/> of it are whole rows.
    /// </summary>
    public static double ItemsHeight(WidgetArea area, bool search, bool header, bool breadcrumb, bool rows = false)
        => Math.Max(0, area.Height + FamilySpace.Inset - Over(area.Grid(), search, header, breadcrumb) - (rows ? 0 : FamilySpace.Inset));

    /// <summary>[S-6712] The whole rows of 28 in <paramref name="height"/>, at least one.</summary>
    public static int Rows(double height) => Math.Max(1, RowRules.Fit(height, 1));

    /// <summary>
    /// [S-12807] [S-14810] The list's rows in the items' <paramref name="height"/> (<see cref="ItemsHeight"/>, down to the
    /// card's foot): rows of 28 at their one fixed size (three cells), touching, as many as it reaches, the last cut by the
    /// card's foot (the cells of the grid and the icons keep the inset: their marks stand inside it).
    /// </summary>
    public static RowReach Reach(double height, ItemGrid grid)
        => ItemSize.Reach(height, RowHeight, grid);

    /// <summary>
    /// [S-1326] The tool line's parts ([S-6710]: at most two buttons): Back and Up in navigate mode, else
    /// Search, and Sort but in Details (its column titles sort); the git badge as the informing label in
    /// list and details. The line folds them by its own rule; Forward, Home and, in navigate mode,
    /// Search are on the card menu.
    /// </summary>
    public static FolderHeaderPlan Header(FolderConfig config, bool navigate, bool hasGit)
    {
        ArgumentNullException.ThrowIfNull(config);
        var view = config.Style;

        // [S-11203] A search box that always stands needs no button to open it.
        return new(
            Back: navigate,
            Up: navigate,
            Search: !navigate && !config.SearchBox,
            Sort: !navigate && view != FolderStyle.Details,
            Git: hasGit && view is FolderStyle.List or FolderStyle.Details);
    }
}
