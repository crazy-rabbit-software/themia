using Themia.Core.Model;
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

/// <summary>[S-1323] One layout pass of the folder widget: what the body and the header show at this size.</summary>
/// <param name="Breadcrumb">The path as a row of 32 above the items (L and XL).</param>
/// <param name="SearchBox">The open search box above the items.</param>
/// <param name="ColumnHeader">The details table's column titles.</param>
/// <param name="Columns">The details table's columns after Name that fit.</param>
/// <param name="Modified">The list's one value, the date modified, from a width of 180.</param>
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
/// [S-1323] The folder widget's ladder, a pure function of the slot and the settings, on the family's
/// blocks ([S-6711], [S-6713]): rows of 32 (one line, the file's own 16 icon as the lead) or a grid of
/// cells (32, 48 from an expanded width). <list type="bullet">
/// <item>Must show: one row (or one row of cells).</item>
/// <item>The search box when it is open (it was asked for).</item>
/// <item>Details: the column titles over the table, and the chosen columns that fit the width
/// ([S-1322]). List: the date modified as each row's value from a width of 180.</item>
/// <item>L and up (both axes Expanded): the path as a row above the items.</item>
/// <item>Everything left: rows (the list scrolls; the fade marks more below).</item>
/// </list>
/// The heights are the family's: a row 32, the column titles a Caption line and 8, the search box 32
/// and 8; the card's bottom inset is kept.
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

    /// <summary>[S-1323] The list's date from this width (a column of 180, as the kit's lists' time, [S-7031]).</summary>
    public const double ModifiedFrom = KitLadder.LeadNeedsWidth;

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

        // The card's bottom inset is not the items' to use.
        var room = new Disclosure(area.Width, Math.Max(0, area.Height - FamilySpace.Inset));
        var icon = RowRules.CellIcon(area);
        var named = config.Style == FolderStyle.Grid;
        room.Reserve(tiles ? RowRules.CellHeight(icon, named) : RowHeight);

        var search = searchOpen && room.Take(SearchHeight);
        var header = details && room.Take(ColumnHeaderHeight + RowHeight);
        var big = area.AtLeast(SizeClass.Expanded, SizeClass.Expanded);
        var breadcrumb = big && room.Take(BreadcrumbHeight + (2 * RowHeight));

        var textWidth = width - NameStart;
        IReadOnlyList<FolderColumn> columns = details ? FolderColumns.Fit(config.DetailColumns, textWidth) : [];
        CellGrid? grid = tiles ? RowRules.Cells(width, ItemsHeight(area, search, header, breadcrumb), icon, named) : null;

        return new FolderPlan(
            breadcrumb,
            search,
            header,
            columns,
            !tiles && !details && area.Width >= ModifiedFrom,
            grid,
            Header(config.Style, navigate, hasGit));
    }

    /// <summary>
    /// The height the items have: the view's area less what stands above them and the card's bottom
    /// inset. <see cref="Rows"/> of it are whole rows.
    /// </summary>
    public static double ItemsHeight(WidgetArea area, bool search, bool header, bool breadcrumb)
        => Math.Max(0, area.Height - FamilySpace.Inset
            - (search ? SearchHeight : 0) - (header ? ColumnHeaderHeight : 0) - (breadcrumb ? BreadcrumbHeight : 0));

    /// <summary>[S-6712] The whole rows of 32 in <paramref name="height"/>, at least one.</summary>
    public static int Rows(double height) => Math.Max(1, RowRules.Fit(height, 1));

    /// <summary>
    /// [S-1326] The header's parts ([S-6710]: at most two buttons): Back and Up in navigate mode, else
    /// Search, and Sort but in Details (its column titles sort); the git badge as the informing label in
    /// list and details. The header folds them by its own rule; Forward, Home and, in navigate mode,
    /// Search are on the card menu.
    /// </summary>
    public static FolderHeaderPlan Header(FolderStyle view, bool navigate, bool hasGit)
        => new(
            Back: navigate,
            Up: navigate,
            Search: !navigate,
            Sort: !navigate && view != FolderStyle.Details,
            Git: hasGit && view is FolderStyle.List or FolderStyle.Details);
}
