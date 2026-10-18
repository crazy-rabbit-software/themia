using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>[S-1323] How tiles sit in the grid and icons views.</summary>
/// <param name="Columns">Tiles per row: as many minimum-width tiles as fit, at least one.</param>
/// <param name="CellWidth">Each tile stretched so the row fills the width exactly.</param>
/// <param name="CellHeight">The preset height.</param>
/// <param name="Rows">How many rows the items take.</param>
/// <param name="Icon">The icon (and thumbnail) size inside a tile.</param>
public readonly record struct GridPlan(int Columns, double CellWidth, double CellHeight, int Rows, double Icon);

/// <summary>[S-1326] Which header buttons the card's header has room for, beside the title.</summary>
public readonly record struct FolderHeaderPlan(bool Up, bool Back, bool Search, bool Sort, bool Forward, bool Git, bool Home)
{
    public static FolderHeaderPlan None => default;
}

/// <summary>[S-1323] One layout pass of the folder widget: what the body and the header show at this size.</summary>
public sealed record FolderPlan(
    bool Breadcrumb,
    bool SearchBox,
    bool ColumnHeader,
    IReadOnlyList<FolderColumn> Columns,
    double RowHeight,
    double IconSize,
    bool SmallText,
    GridPlan? Grid,
    FolderHeaderPlan Header);

/// <summary>
/// [S-1323] The folder widget's disclosure ladder, a pure function of the slot and the settings, so
/// the shedding order is tested rather than eyeballed.
/// <list type="bullet">
/// <item>Must show: one row (or one row of tiles) of icon + name.</item>
/// <item>The search box when it is open (it was asked for).</item>
/// <item>Details: the column header, and the chosen columns that fit the width ([S-1322]). List: a
/// trailing Modified from 180 DIPs, Size from 400, Type from 720 — the same columns, unlabelled.</item>
/// <item>L and up (both axes Expanded): a breadcrumb of the path above the list. No status footer
/// ("24 items · 2 selected", retired: concise widgets).</item>
/// <item>XL (Huge width): grid tiles one preset larger, so a big tile shows bigger thumbnails rather
/// than a sea of small ones.</item>
/// <item>Everything left: rows (the list scrolls; the fade marks more below).</item>
/// </list>
/// </summary>
public static class FolderLayout
{
    public const double BreadcrumbHeight = 28;
    public const double SearchHeight = 32;
    public const double ColumnHeaderHeight = 24;
    public const double HeaderButton = 28;
    public const double GitBadgeWidth = 96;

    /// <summary>Side padding inside a row, as Fluent's list rows have it.</summary>
    public const double RowInset = 16;

    /// <summary>The icon's width plus its gap to the name.</summary>
    public static double IconLead(FolderItemSize size) => IconSize(size) + 8;

    public static double RowHeight(FolderItemSize size) => size switch
    {
        FolderItemSize.ExtraSmall => 24,
        FolderItemSize.Small => 28,
        FolderItemSize.Large => 40,
        _ => 32,
    };

    public static double IconSize(FolderItemSize size) => size switch
    {
        FolderItemSize.ExtraSmall => 12,
        FolderItemSize.Small => 14,
        FolderItemSize.Large => 20,
        _ => 16,
    };

    /// <summary>The original's grid presets: minimum column width, row height, icon size.</summary>
    public static (double Column, double Row, double Icon) Tile(FolderTileSize size) => size switch
    {
        FolderTileSize.Medium => (110, 120, 48),
        FolderTileSize.Large => (150, 160, 64),
        _ => (80, 90, 32),
    };

    /// <summary>
    /// [S-1323] Tiles for <paramref name="count"/> items in a row of <paramref name="width"/>. Icons-only
    /// tiles shrink to the icon plus padding; the preset height holds and the grid scrolls.
    /// </summary>
    public static GridPlan Grid(double width, int count, FolderTileSize size, bool labelled)
    {
        var (column, row, icon) = Tile(size);

        if (!labelled)
        {
            column = icon + 24;
            row = icon + 24;
        }

        var columns = Math.Max(1, (int)Math.Floor((Math.Max(width, 0) + Disclosure.Tolerance) / column));
        var cellWidth = width > 0 ? width / columns : column;
        var rows = count <= 0 ? 0 : (count + columns - 1) / columns;

        return new GridPlan(columns, cellWidth, row, rows, icon);
    }

    /// <summary>
    /// One layout pass. <paramref name="area"/> is the view's slot (the card below its header);
    /// <paramref name="anyFile"/> whether anything shown is a file ([S-6411]).
    /// </summary>
    public static FolderPlan Plan(WidgetArea area, FolderConfig config, int count, bool anyFile, bool searchOpen, bool navigate, bool hasGit)
    {
        ArgumentNullException.ThrowIfNull(config);

        var grid = config.Style is FolderStyle.Grid or FolderStyle.Icons;
        var rowHeight = RowHeight(config.Rows);
        var room = new Disclosure(area);

        // Rung 0: one row of something. Tiles reserve their own height, rows theirs.
        var tileSize = config.Tiles;

        if (grid && area.WidthClass >= SizeClass.Huge && tileSize < FolderTileSize.Large)
        {
            tileSize++;
        }

        var labelled = config.Style == FolderStyle.Grid;
        var firstRow = grid
            ? Math.Min(Grid(area.Width, 1, tileSize, labelled).CellHeight, Math.Max(area.Height, 0))
            : rowHeight;

        room.Reserve(firstRow);

        var search = searchOpen && room.Take(SearchHeight);
        var details = config.Style == FolderStyle.Details;
        var header = details && room.Take(ColumnHeaderHeight + rowHeight);
        var big = area.AtLeast(SizeClass.Expanded, SizeClass.Expanded);
        var breadcrumb = big && room.Take(BreadcrumbHeight + (2 * rowHeight));

        var textWidth = area.Width - (2 * RowInset) - IconLead(config.Rows);

        IReadOnlyList<FolderColumn> columns = details
            ? FolderColumns.Fit(config.DetailColumns, textWidth)
            : grid ? [] : ListColumns(area, textWidth, anyFile);

        return new FolderPlan(
            breadcrumb,
            search,
            header,
            columns,
            rowHeight,
            IconSize(config.Rows),
            config.Rows <= FolderItemSize.Small,
            grid ? Grid(area.Width - 8, count, tileSize, labelled) : null,
            Header(area.Width, config.Style, navigate, hasGit));
    }

    /// <summary>
    /// [S-1323] The list view's unlabelled trailing columns: Modified from a Regular width, Size from
    /// Expanded, Type from Huge — each only when it still leaves the name its minimum.
    /// <para>
    /// [S-6411] Unlabelled, a column that a row leaves blank is invisible space: so Size is dropped
    /// when nothing shown is a file (folders have no size), and Modified, the one every row fills,
    /// is drawn last, so the rightmost text ends at the row's right inset at every width. Shown
    /// right to left by priority: Type, Size, Modified.
    /// </para>
    /// </summary>
    public static IReadOnlyList<FolderColumn> ListColumns(WidgetArea area, double textWidth, bool anyFile)
    {
        List<FolderColumn> wanted = [];

        if (area.WidthClass >= SizeClass.Regular)
        {
            wanted.Add(FolderColumn.Modified);
        }

        if (area.WidthClass >= SizeClass.Expanded && anyFile)
        {
            wanted.Add(FolderColumn.Size);
        }

        if (area.WidthClass >= SizeClass.Huge)
        {
            wanted.Add(FolderColumn.Type);
        }

        var fitted = FolderColumns.Fit(wanted, textWidth);
        return [.. fitted.Reverse()];
    }

    /// <summary>
    /// [S-1326] Header buttons by priority until the title would drop below its minimum: Up and Back
    /// (navigate mode), Search, Sort (not in details, whose column headers sort), Forward, the git
    /// badge, Home. <paramref name="width"/> is the card's width.
    /// </summary>
    public static FolderHeaderPlan Header(double width, FolderStyle view, bool navigate, bool hasGit)
    {
        // Card insets (2 × 16), the header icon and its gap (24), the title's minimum (72), the
        // busy ring's slot (20).
        var left = width - 32 - 24 - 72 - 20;

        bool Take(bool wanted, double cost)
        {
            if (!wanted || cost > left)
            {
                return false;
            }

            left -= cost;
            return true;
        }

        var up = Take(navigate, HeaderButton);
        var back = Take(navigate, HeaderButton);
        var search = Take(true, HeaderButton);
        var sort = Take(view != FolderStyle.Details, HeaderButton);
        var forward = Take(navigate, HeaderButton);
        var git = Take(hasGit && (view is FolderStyle.List or FolderStyle.Details), GitBadgeWidth);
        var home = Take(navigate, HeaderButton);

        return new FolderHeaderPlan(up, back, search, sort, forward, git, home);
    }
}
