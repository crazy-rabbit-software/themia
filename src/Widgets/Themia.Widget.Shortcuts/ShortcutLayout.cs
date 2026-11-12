using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>[S-1383] How the list and details views lay out their rows, on the Row block ([S-6711]).</summary>
/// <param name="Lines">The rows' lines ([S-6712]): the name, and the target under it where the list has two.</param>
/// <param name="Lead">The icon before the name: 16 on a one-line row, 32 on a two-line one.</param>
/// <param name="TargetColumn">The details view shows the target column.</param>
/// <param name="KindColumn">The details view shows the kind column.</param>
public readonly record struct ShortcutRowPlan(int Lines, double Lead, bool TargetColumn, bool KindColumn)
{
    /// <summary>One row: 28 or 40 by its lines.</summary>
    public double RowHeight => FamilyShape.RowHeight(Lines);

    /// <summary>[S-1384] The details table labels its columns when it has a column besides the name.</summary>
    public bool ColumnHeader => TargetColumn || KindColumn;
}

/// <summary>
/// [S-11202] The Shortcuts widget's sizes and layout, from its config alone: the user's list has a
/// known length, so the size follows it. The grid is the config's columns of cells at the config's icon
/// size, its rows the shortcuts' (at most <see cref="MaxGridRows"/>, then it scrolls); the list and the
/// details table one row per shortcut (at most <see cref="MaxRows"/>, then they scroll). Nothing in it
/// follows the box. Pure, so every rule is pinned by a test.
/// </summary>
public static class ShortcutLayout
{
    /// <summary>[S-11202] The most rows a list or the details table is tall; more shortcuts scroll.</summary>
    public const int MaxRows = 12;

    /// <summary>[S-11202] The most rows of cells a grid is tall; more shortcuts scroll.</summary>
    public const int MaxGridRows = 6;

    /// <summary>[S-11202] The grid's columns: 1..8, a new one 4.</summary>
    public const int MinColumns = 1;

    public const int MaxColumns = 8;

    public const int DefaultColumns = 4;

    /// <summary>The search box (a 32 text box) and the 8 under it, when open.</summary>
    public const double SearchHeight = FamilyShape.Button + FamilySpace.S2;

    /// <summary>[S-11202] The details table's name column at its least: a name worth reading.</summary>
    public const double NameLeast = 120;

    /// <summary>[S-11202] The details table's target column at its least: a short path or a site.</summary>
    public const double TargetLeast = 160;

    /// <summary>The details view's kind column.</summary>
    public const double KindWidth = 120;

    /// <summary>A list's widths (the kit's: [S-2440]).</summary>
    public const double ListMin = KitSizing.MinWidth, ListNatural = KitSizing.NaturalWidth, ListMax = KitSizing.MaxWidth;

    /// <summary>How much wider than its least a details table may grow: past it a column is only empty.</summary>
    public const double DetailsSpare = 360;

    /// <summary>The grid's columns, held to their range.</summary>
    public static int ClampColumns(int columns) => Math.Clamp(columns, MinColumns, MaxColumns);

    /// <summary>[S-11202] The cells' icon: Small 32, Large 48.</summary>
    public static double CellIcon(ShortcutsIconSize size) => size == ShortcutsIconSize.Large ? FamilyShape.IconHuge : FamilyShape.IconLarge;

    /// <summary>[S-11202] The rows of cells <paramref name="count"/> shortcuts take in <paramref name="columns"/>, at least one, at most <see cref="MaxGridRows"/>.</summary>
    public static int GridRows(int count, int columns)
    {
        var across = ClampColumns(columns);
        return Math.Clamp((Math.Max(0, count) + across - 1) / across, 1, MaxGridRows);
    }

    /// <summary>[S-11202] The rows a list of <paramref name="count"/> shortcuts shows: one per shortcut, at least one, at most <see cref="MaxRows"/>.</summary>
    public static int ListRows(int count) => Math.Clamp(count, 1, MaxRows);

    /// <summary>
    /// [S-1382] [S-11202] The grid of <paramref name="config"/> for <paramref name="count"/> shortcuts in a
    /// content box <paramref name="width"/> wide (the card less its insets): its columns of cells at its
    /// icon over their names (none on a grid of icons alone), each cell at least its least width and the
    /// box shared out between them, and its rows.
    /// </summary>
    public static CellGrid Grid(ShortcutsConfig config, int count, double width)
    {
        ArgumentNullException.ThrowIfNull(config);
        var columns = ClampColumns(config.Columns);
        var icon = CellIcon(config.IconSize);
        var named = !config.GridIconOnly;
        var least = RowRules.CellLeast(icon, named);
        var cell = Math.Max(least, (Math.Max(0, width) - (FamilySpace.S3 * (columns - 1))) / columns);
        return new CellGrid(columns, GridRows(count, columns), cell, RowRules.CellHeight(icon, named), icon, named);
    }

    /// <summary>
    /// [S-1383] [S-11202] Rows for the list and details views ([S-6711], [S-6712]): the list's rows have the
    /// target as a second line where its Lines are two (a 32 icon then, 16 on one line); the details table is
    /// one line with its Target and Kind columns as its config says.
    /// </summary>
    public static ShortcutRowPlan Rows(ShortcutsConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Style == ShortcutsStyle.Details)
        {
            return new ShortcutRowPlan(1, FamilyShape.Icon, config.ShowTarget, config.ShowKind);
        }

        var lines = Math.Clamp(config.Lines, 1, 2);
        return new ShortcutRowPlan(lines, lines > 1 ? FamilyShape.IconLarge : FamilyShape.Icon, false, false);
    }

    /// <summary>[S-11202] The details table's least width: the insets, the lead, the name and each column it has, 12 apart.</summary>
    public static double DetailsLeast(ShortcutRowPlan plan)
        => (2 * FamilySpace.Inset) + RowRules.NameStart(plan.Lead) + NameLeast
           + (plan.TargetColumn ? FamilySpace.ColumnGap + TargetLeast : 0)
           + (plan.KindColumn ? FamilySpace.ColumnGap + KindWidth : 0);

    /// <summary>The grid's width steps: the insets less a gap as the base, a cell and its gap as the step, the config's columns as the count.</summary>
    public static SizeSteps ColumnSteps(ShortcutsConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var least = RowRules.CellLeast(CellIcon(config.IconSize), !config.GridIconOnly);
        return new SizeSteps((2 * FamilySpace.Inset) - FamilySpace.S3, least + FamilySpace.S3, MinColumns, MaxColumns, ClampColumns(config.Columns));
    }

    /// <summary>
    /// [S-11202] The sizing of <paramref name="config"/> (with its default top header): the grid's width
    /// in steps of a column (the config's columns) and its one height, its rows of cells; a list or
    /// a details table one height, its rows (and the details' column titles), and a width range.
    /// </summary>
    public static WidgetSizing Sizing(ShortcutsConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var count = config.Items.Count;

        if (config.Style == ShortcutsStyle.Grid)
        {
            var named = !config.GridIconOnly;
            var pitch = RowRules.CellHeight(CellIcon(config.IconSize), named) + FamilySpace.S3;
            var height = FamilySpace.ContentTop + (GridRows(count, config.Columns) * pitch);
            return WidgetSizing.Range(new(1, height), new(1, height), new(1, height)).WithWidthSteps(ColumnSteps(config));
        }

        var plan = Rows(config);
        var rows = FamilySpace.ContentTop + (plan.ColumnHeader ? KitLadder.ColumnHeaderHeight : 0) + (ListRows(count) * plan.RowHeight) + FamilySpace.Inset;
        if (config.Style == ShortcutsStyle.List)
        {
            return WidgetSizing.Range(new(ListMin, rows), new(ListNatural, rows), new(ListMax, rows));
        }

        var least = DetailsLeast(plan);
        return WidgetSizing.Range(new(least, rows), new(least + (DetailsSpare / 3), rows), new(least + DetailsSpare, rows));
    }

    /// <summary>
    /// [S-11202] The config a grid <paramref name="size"/> wide means: the columns that width holds. A
    /// copy when they change; the same config otherwise, and for a list or a table, whose size follows
    /// nothing a resize can set.
    /// </summary>
    public static WidgetConfig SizeSets(ShortcutsConfig config, SizeDip size)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Style != ShortcutsStyle.Grid)
        {
            return config;
        }

        var columns = ColumnSteps(config).Nearest(size.Width);
        if (columns == config.Columns)
        {
            return config;
        }

        // A copy of the same shortcuts, not a duplicate: the items keep their ids (selection keys on them).
        var copy = (ShortcutsConfig)config.Clone();
        for (var i = 0; i < copy.Items.Count; i++)
        {
            copy.Items[i].Id = config.Items[i].Id;
        }

        copy.Columns = columns;
        return copy;
    }
}
