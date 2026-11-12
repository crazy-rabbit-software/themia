using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-1326] The header's parts ([S-6710]): at most two buttons — Back and Up in navigate mode, else
/// Search and Sort (Search alone in Details, whose column headers sort) — and the git badge as its
/// informing label (list and details, inside a repository).
/// </summary>
public readonly record struct FolderHeaderPlan(bool Back, bool Up, bool Search, bool Sort, bool Git)
{
    public static FolderHeaderPlan None => default;

    /// <summary>The buttons the header holds (never more than two, [S-6710]).</summary>
    public int Buttons => (Back ? 1 : 0) + (Up ? 1 : 0) + (Search ? 1 : 0) + (Sort ? 1 : 0);
}

/// <summary>[S-1323] One layout pass of the folder widget: what the body and the header show, from the config.</summary>
/// <param name="Breadcrumb">The path as a row of 28 above the items (the Path setting).</param>
/// <param name="SearchBox">The search box above the items (the Search box setting, or opened from the header).</param>
/// <param name="ColumnHeader">The details table's column titles (the Column titles setting).</param>
/// <param name="Columns">The details table's columns after Name: every one chosen.</param>
/// <param name="Modified">The list's one value, the date modified (the Date modified setting).</param>
/// <param name="Grid">The cells of the grid and icons styles; null for the rows.</param>
/// <param name="Header">The header's parts.</param>
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

    /// <summary>The search box (a 32 text box) and the 8 under it.</summary>
    public const double SearchHeight = FamilyShape.Button + FamilySpace.S2;

    /// <summary>The details table's titles: a Caption line and 8, as the kit's table ([S-7034]).</summary>
    public static double ColumnHeaderHeight => KitLadder.ColumnHeaderHeight;

    /// <summary>Where a row's name starts inside its own inset: after the 16 lead and its gap.</summary>
    public static double NameStart => RowRules.NameStart(Lead);

    /// <summary>
    /// One layout pass. <paramref name="area"/> is the view's slot (the card below its header);
    /// <paramref name="anyFile"/> whether anything shown is a file ([S-6411]).
    /// </summary>
    public static FolderPlan Plan(WidgetArea area, FolderConfig config, int count, bool anyFile, bool searchOpen, bool navigate, bool hasGit)
    {
        ArgumentNullException.ThrowIfNull(config);

        var tiles = config.Style is FolderStyle.Grid or FolderStyle.Icons;
        var details = config.Style == FolderStyle.Details;
        var width = Math.Max(0, area.Width - (2 * FamilySpace.Inset));
        var search = config.SearchBox || searchOpen;
        var header = details && config.ColumnTitles;
        var named = config.Style == FolderStyle.Grid;
        CellGrid? grid = tiles ? RowRules.Cells(width, ItemsHeight(area, search, header, config.ShowPath), CellIcon, named) : null;

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
    /// [S-11203] The least height: the header, what the config stands over the items, one row of them
    /// (or one row of cells) and the bottom inset; never under <see cref="Least"/>.
    /// </summary>
    public static double LeastHeight(FolderConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var one = config.Style is FolderStyle.Grid or FolderStyle.Icons
            ? RowRules.CellHeight(CellIcon, config.Style == FolderStyle.Grid) + FamilySpace.S3
            : RowHeight;
        var over = (config.SearchBox ? SearchHeight : 0) + (config.ShowPath ? BreadcrumbHeight : 0)
            + (config.Style == FolderStyle.Details && config.ColumnTitles ? ColumnHeaderHeight : 0);
        return Math.Max(Least.Height, FamilySpace.ContentTop + over + one + FamilySpace.Inset);
    }

    /// <summary>
    /// [S-11203] A folder keeps a free size, as a file browser's window does: 220..800 wide (the details
    /// table from the width its columns need) and 160..640 high (more if what stands over the items needs
    /// it); more room shows more of the folder's entries and the rest scrolls. A new one: the list, the grid
    /// and the icons 300 × 280; the details table its least width and 60 more, 280 high.
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
        var over = (config.SearchBox ? SearchHeight : 0) + (config.ShowPath ? BreadcrumbHeight : 0)
            + (config.Style == FolderStyle.Details && config.ColumnTitles ? ColumnHeaderHeight : 0);
        var room = 280 - FamilySpace.ContentTop - FamilySpace.Inset - over;
        var rows = Math.Max(1, (int)Math.Round((room + gap) / (row + gap)));
        return FamilySpace.ContentTop + over + (rows * (row + gap)) - gap + FamilySpace.Inset;
    }

    /// <summary>
    /// The height the items have: the view's area less what stands above them and the card's bottom
    /// inset. <see cref="Rows"/> of it are whole rows.
    /// </summary>
    public static double ItemsHeight(WidgetArea area, bool search, bool header, bool breadcrumb)
        => Math.Max(0, area.Height - FamilySpace.Inset
            - (search ? SearchHeight : 0) - (header ? ColumnHeaderHeight : 0) - (breadcrumb ? BreadcrumbHeight : 0));

    /// <summary>[S-6712] The whole rows of 28 in <paramref name="height"/>, at least one.</summary>
    public static int Rows(double height) => Math.Max(1, RowRules.Fit(height, 1));

    /// <summary>
    /// [S-1326] The header's parts ([S-6710]: at most two buttons): Back and Up in navigate mode, else
    /// Search, and Sort but in Details (its column titles sort); the git badge as the informing label in
    /// list and details. The header folds them by its own rule; Forward, Home and, in navigate mode,
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
