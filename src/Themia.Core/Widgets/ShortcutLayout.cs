using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>[S-1382] How the grid view tiles a box.</summary>
/// <param name="Columns">Tiles per row; the tiles share the width between them.</param>
/// <param name="Rows">Whole rows that fit without scrolling.</param>
/// <param name="TileWidth">One tile's width, the columns having shared out the box.</param>
/// <param name="TileHeight">One tile's height (stretched to share the height out when asked).</param>
/// <param name="Icon">Icon size in DIPs.</param>
/// <param name="Labels">Names under the icons.</param>
/// <param name="Detail">A caption line under the name: the site, or the folder it is in.</param>
public readonly record struct ShortcutTilePlan(
    int Columns,
    int Rows,
    double TileWidth,
    double TileHeight,
    double Icon,
    bool Labels,
    bool Detail);

/// <summary>[S-1383] How the list and details views lay out their rows.</summary>
/// <param name="Columns">Columns of rows (a list on a very wide tile runs in two).</param>
/// <param name="RowHeight">One row.</param>
/// <param name="Icon">Icon size in DIPs.</param>
/// <param name="SecondLine">A list row carries its target as a caption line under the name.</param>
/// <param name="TargetColumn">The details view shows the target column.</param>
/// <param name="KindColumn">The details view shows the kind column.</param>
/// <param name="ColumnHeader">The details view labels its columns.</param>
public readonly record struct ShortcutRowPlan(
    int Columns,
    double RowHeight,
    double Icon,
    bool SecondLine,
    bool TargetColumn,
    bool KindColumn,
    bool ColumnHeader);

/// <summary>
/// The Shortcuts widget's disclosure ladder (S-1382..S-1384): what each view shows for the room it
/// has. Pure, so every tier is pinned by a test.
/// <para>
/// The grid is the default and the richest: its tiles keep the size the user chose and the columns
/// share the width out (so a wide tile shows more of them, not bigger ones); a large tile (L, XL)
/// steps the tiles up once; the names go — and then the tiles shrink — only when keeping them would
/// leave fewer than four items (or all of them, when there are fewer) on screen.
/// </para>
/// </summary>
public static class ShortcutLayout
{
    /// <summary>Space between two tiles, and between two parts of the widget.</summary>
    public const double Gap = 4;

    /// <summary>Padding inside a labelled tile, above the icon and below the name.</summary>
    public const double TilePad = 6;

    /// <summary>Padding inside an icon-only tile.</summary>
    public const double IconTilePad = 4;

    /// <summary>Between an icon and the name under it.</summary>
    public const double LabelGap = 4;

    /// <summary>How many items the grid tries to keep on screen before it sheds names and size.</summary>
    public const int KeepVisible = 4;

    /// <summary>[S-1382] The icon drawn in a tile of each size.</summary>
    public static double TileIcon(ShortcutItemSize size) => size switch
    {
        ShortcutItemSize.Small => 24,
        ShortcutItemSize.Large => 48,
        _ => 32,
    };

    /// <summary>[S-1382] The narrowest a labelled tile may be: room for a short name under the icon.</summary>
    public static double TileMinWidth(ShortcutItemSize size) => size switch
    {
        ShortcutItemSize.Small => 72,
        ShortcutItemSize.Large => 112,
        _ => 88,
    };

    /// <summary>
    /// [S-1382] The tile size the grid uses on <paramref name="area"/>: the user's, one step larger
    /// on a large tile (L, XL), where the ladder spends room on bigger tiles rather than empty rows.
    /// </summary>
    public static ShortcutItemSize GridSize(ShortcutItemSize chosen, WidgetArea area)
        => area.AtLeast(SizeClass.Expanded, SizeClass.Expanded) && chosen < ShortcutItemSize.Large ? chosen + 1 : chosen;

    /// <summary>[S-1382] The detail line under a tile's name appears only on the largest tiles (XL).</summary>
    public static bool GridDetail(WidgetArea area) => area.AtLeast(SizeClass.Huge, SizeClass.Huge);


    /// <summary>
    /// [S-1382] Tile <paramref name="count"/> items into a <paramref name="width"/> ×
    /// <paramref name="height"/> box. Tries, richest first: the size given with names (and the
    /// detail line when <paramref name="detail"/>), each smaller size with names, then icons only at
    /// each size down to a 16px last resort — and takes the first that shows at least
    /// <see cref="KeepVisible"/> items (or all of them) in at least two columns (one, for one item).
    /// </summary>
    public static ShortcutTilePlan Grid(
        double width,
        double height,
        int count,
        ShortcutItemSize size,
        bool iconOnly,
        bool stretch,
        bool detail)
    {
        var want = Math.Min(Math.Max(count, 1), KeepVisible);
        var minColumns = count >= 2 ? 2 : 1;
        var line = FluentType.Caption.LineHeight;

        Candidate? last = null;

        foreach (var candidate in Candidates(size, iconOnly, detail, line))
        {
            last = candidate;
            var columns = Disclosure.WholeRows(width, candidate.Width, Gap);
            var rows = Disclosure.WholeRows(height, candidate.Height, Gap);

            if (columns >= minColumns && rows >= 1 && columns * rows >= want)
            {
                return Plan(candidate, columns, rows, width, height, count, stretch);
            }
        }

        // Nothing fits the rule: the smallest tile, as many as there is room for, at least one.
        var smallest = last!.Value;
        return Plan(
            smallest,
            Math.Max(1, Disclosure.WholeRows(width, smallest.Width, Gap)),
            Math.Max(1, Disclosure.WholeRows(height, smallest.Height, Gap)),
            width,
            height,
            count,
            stretch);
    }

    /// <summary>
    /// [S-1383] Rows for the list and details views. The row size steps down once on a short tile
    /// (XS, S, Wide); a list on a large tile (L, XL) shows each target as a caption line under the
    /// name and, on the widest (XL) and on a wide strip, runs in two columns; the details view adds the target column
    /// from Wide up, the kind column on the widest, and labels its columns when it has the target
    /// column and room for a header.
    /// </summary>
    public static ShortcutRowPlan Rows(WidgetArea area, ShortcutItemSize size, bool details)
    {
        var effective = area.HeightClass == SizeClass.Compact && size > ShortcutItemSize.Small ? size - 1 : size;

        var (row, icon) = effective switch
        {
            ShortcutItemSize.Small => (32.0, 16.0),
            ShortcutItemSize.Large => (48.0, 24.0),
            _ => (40.0, 20.0),
        };

        var large = area.AtLeast(SizeClass.Expanded, SizeClass.Expanded);
        var secondLine = !details && large;

        if (secondLine)
        {
            row = Math.Max(row, FluentType.Body.LineHeight + FluentType.Caption.LineHeight + 12);
        }

        var target = details && area.WidthClass >= SizeClass.Expanded;

        return new ShortcutRowPlan(
            Columns: TwoColumns(area, details) ? 2 : 1,
            RowHeight: row,
            Icon: icon,
            SecondLine: secondLine,
            TargetColumn: target,
            KindColumn: details && area.WidthClass >= SizeClass.Huge,
            ColumnHeader: target && area.HeightClass >= SizeClass.Regular);
    }

    /// <summary>
    /// [S-1382] Whether the grid sits centred as a group: every item is on screen at once and the
    /// tiles are not stretched to fill the height (stretched tiles fill it already).
    /// </summary>
    public static bool Centred(ShortcutTilePlan plan, int count, bool stretch)
        => !stretch && count > 0 && (int)Math.Ceiling(count / (double)Math.Max(1, plan.Columns)) <= plan.Rows;

    /// <summary>
    /// A list runs in two columns on the widest tiles and on a wide strip (a short tile with the
    /// width of two lists), where one column would leave most of the width empty.
    /// </summary>
    private static bool TwoColumns(WidgetArea area, bool details)
        => !details && (area.WidthClass >= SizeClass.Huge
            || (area.WidthClass >= SizeClass.Expanded && area.HeightClass == SizeClass.Compact));

    private static ShortcutTilePlan Plan(Candidate c, int columns, int rows, double width, double height, int count, bool stretch)
    {
        var tileWidth = Math.Max(c.Width, (width - (Gap * (columns - 1))) / columns);
        var tileHeight = c.Height;

        // Stretch shares the height out between the rows there are — only when they all fit; a list
        // that scrolls has no height to share.
        var used = (int)Math.Ceiling(Math.Max(count, 1) / (double)columns);

        if (stretch && used <= rows && used > 0)
        {
            tileHeight = Math.Max(c.Height, (height - (Gap * (used - 1))) / used);
        }

        return new ShortcutTilePlan(columns, rows, tileWidth, tileHeight, c.Icon, c.Labels, c.Detail);
    }

    private static IEnumerable<Candidate> Candidates(ShortcutItemSize size, bool iconOnly, bool detail, double line)
    {
        if (!iconOnly)
        {
            for (var s = size; s >= ShortcutItemSize.Small; s--)
            {
                var icon = TileIcon(s);
                var labelled = (TilePad * 2) + icon + LabelGap + line;

                if (detail)
                {
                    yield return new Candidate(TileMinWidth(s), labelled + line, icon, true, true);
                }

                yield return new Candidate(TileMinWidth(s), labelled, icon, true, false);
            }
        }

        for (var s = size; s >= ShortcutItemSize.Small; s--)
        {
            var icon = TileIcon(s);
            yield return new Candidate(icon + (IconTilePad * 3), icon + (IconTilePad * 2), icon, false, false);
        }

        yield return new Candidate(16 + (IconTilePad * 3), 16 + (IconTilePad * 2), 16, false, false);
    }

    private readonly record struct Candidate(double Width, double Height, double Icon, bool Labels, bool Detail);
}
