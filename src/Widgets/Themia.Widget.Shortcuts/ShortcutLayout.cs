using Themia.Core.Model;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>[S-1383] How the list and details views lay out their rows, on the Row block ([S-6711]).</summary>
/// <param name="Columns">Columns of rows (a list on a tile wide enough for two of 320 runs in two).</param>
/// <param name="Lines">The rows' lines ([S-6712]): the name, and the target under it from a regular tile (the list).</param>
/// <param name="Lead">The icon before the name: 16 on a one-line row, 32 on a taller one.</param>
/// <param name="TargetColumn">The details view shows the target column.</param>
/// <param name="KindColumn">The details view shows the kind column.</param>
/// <param name="ColumnHeader">The details view labels its columns.</param>
public readonly record struct ShortcutRowPlan(
    int Columns,
    int Lines,
    double Lead,
    bool TargetColumn,
    bool KindColumn,
    bool ColumnHeader)
{
    /// <summary>One row: 28, 40 or 56 by its lines.</summary>
    public double RowHeight => FamilyShape.RowHeight(Lines);
}

/// <summary>
/// The Shortcuts widget's ladder (S-1382..S-1384) on the family's blocks: what each view shows for the
/// room it has. Pure, so every tier is pinned by a test.
/// <para>
/// The grid is the default and the richest: Row.Cell cells ([S-6713], <see cref="RowRules.Cells"/>)
/// of the family's icon (32, 48 from an expanded width) over their names; the names go — and then the
/// icons step down to 32 and to the last-resort 16 — only when keeping them would leave fewer than four
/// items (or all of them, when there are fewer) on screen.
/// </para>
/// </summary>
public static class ShortcutLayout
{
    /// <summary>How many items the grid tries to keep on screen before it sheds names and size.</summary>
    public const int KeepVisible = 4;

    /// <summary>The search box (a 32 text box) and the 8 under it, when open.</summary>
    public const double SearchHeight = FamilyShape.Button + FamilySpace.S2;

    /// <summary>A list keeps each of its columns at least this wide (the kit's column, [S-7031]).</summary>
    public const double MinListColumn = 320;

    /// <summary>The details view's kind column.</summary>
    public const double KindWidth = 120;

    /// <summary>
    /// [S-1382] The cells for <paramref name="count"/> items in a content box <paramref name="width"/> ×
    /// <paramref name="height"/> (the card less its inset and what stands above the items). Tries,
    /// richest first: named cells at the family's icon for <paramref name="area"/> (unless
    /// <paramref name="iconOnly"/>), then unnamed at that icon, at 32, and at 16 as the last resort —
    /// and takes the first that shows at least <see cref="KeepVisible"/> items (or all of them) in at
    /// least two columns (one, for one item).
    /// </summary>
    public static CellGrid Grid(WidgetArea area, double width, double height, int count, bool iconOnly)
    {
        var want = Math.Min(Math.Max(count, 1), KeepVisible);
        var minColumns = count >= 2 ? 2 : 1;
        var icon = RowRules.CellIcon(area);

        CellGrid? last = null;
        foreach (var (size, named) in Candidates(icon, iconOnly))
        {
            var grid = RowRules.Cells(width, height, size, named);
            last = grid;
            if (grid.Columns >= minColumns && grid.Rows >= 1 && grid.Visible >= want)
            {
                return grid;
            }
        }

        return last!.Value;
    }

    private static IEnumerable<(double Icon, bool Named)> Candidates(double icon, bool iconOnly)
    {
        if (!iconOnly)
        {
            yield return (icon, true);
        }

        yield return (icon, false);
        if (icon > FamilyShape.IconLarge)
        {
            yield return (FamilyShape.IconLarge, false);
        }

        yield return (FamilyShape.Icon, false);
    }

    /// <summary>
    /// [S-1383] Rows for the list and details views ([S-6711], [S-6712]): the list's rows show the
    /// target as a second line where the tile is regular both ways, the details table one line; a
    /// one-line row leads with a 16 icon, a taller one with 32. A list runs in two columns where each
    /// keeps 320; the details view adds the target column from an expanded width, the kind column on
    /// the widest, and labels its columns when it has the target column on a regular height.
    /// </summary>
    public static ShortcutRowPlan Rows(WidgetArea area, bool details)
    {
        var lines = RowRules.Lines(area, details ? 1 : 2);
        var target = details && area.WidthClass >= SizeClass.Expanded;

        return new ShortcutRowPlan(
            Columns: !details && area.Width >= 2 * MinListColumn ? 2 : 1,
            Lines: lines,
            Lead: lines > 1 ? FamilyShape.IconLarge : FamilyShape.Icon,
            TargetColumn: target,
            KindColumn: details && area.WidthClass >= SizeClass.Huge,
            ColumnHeader: target && area.HeightClass >= SizeClass.Regular);
    }
}
