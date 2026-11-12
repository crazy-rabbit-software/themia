using Themia.Core.Model;

namespace Themia.Sdk.Kit;

/// <summary>
/// [S-2440] A kit list that shows a set number of rows: its config states how many rows it shows and
/// how many lines each has, and its size follows from them ([S-2438]); dragging its bottom edge sets
/// the rows ([S-2442]). Any kit list adopts it by implementing this on its config, stating
/// <see cref="KitSizing.Sizing"/> and <see cref="KitSizing.SizeSets"/> on its descriptor and adding
/// <c>KitMenuLines.Rows</c> to its menu.
/// </summary>
public interface IKitRows
{
    /// <summary>How many rows the list shows: <see cref="KitSizing.MinRows"/>..<see cref="KitSizing.MaxRows"/>; more items scroll.</summary>
    int Rows { get; set; }

    /// <summary>How many lines each row has: 1 to the kind's most (2, or 3 where a row has a third: a preview).</summary>
    int Lines { get; set; }
}

/// <summary>
/// [S-11400] A kit list that may show its rows in two columns side by side: the user's choice, never the
/// width's. Two columns double the width range; the rows count down each column.
/// </summary>
public interface IKitColumns
{
    /// <summary>1 or 2.</summary>
    int Columns { get; set; }
}

/// <summary>
/// [S-2440] What a kit list with rows of its own shows, for its plan: a number of rows of a number of
/// lines, in <paramref name="Columns"/> columns ([S-11400]), with a table's column titles over them
/// where <paramref name="ColumnTitles"/> ([S-11401]).
/// </summary>
public readonly record struct KitRowsChoice(int Rows, int Lines, int Columns = 1, bool ColumnTitles = false);

/// <summary>[S-11401] A list's own width range, where a look needs another than the kit's (a table of columns).</summary>
public readonly record struct KitWidths(double Min, double Natural, double Max);

/// <summary>
/// [S-2440] The size of a kit list that states its rows: the family's content top (36), its rows (28
/// or 40 each by its lines), the add footer (28) where it has one and the card's bottom inset (12);
/// the height stepped by a row, the width in its range. Pure, so every kit list shares it.
/// </summary>
public static class KitSizing
{
    public const int MinRows = 1;

    public const int MaxRows = 20;

    public const int DefaultRows = 6;

    /// <summary>The narrowest a list's row reads at: a check, a title worth reading and a short time.</summary>
    public const double MinWidth = 220;

    /// <summary>A new list's width.</summary>
    public const double NaturalWidth = 300;

    /// <summary>The widest a list grows: past it a row is a sentence across a room.</summary>
    public const double MaxWidth = 520;

    /// <summary>Rows held to their range; a stored count out of it is repaired to the nearest.</summary>
    public static int ClampRows(int rows) => Math.Clamp(rows, MinRows, MaxRows);

    /// <summary>[S-11400] The most lines a row has: the title, the subtitle and the preview.</summary>
    public const int MaxLines = 3;

    /// <summary>[S-11400] The most columns a list shows side by side.</summary>
    public const int MaxColumns = 2;

    /// <summary>Lines held to 1 or 2.</summary>
    public static int ClampLines(int lines) => Math.Clamp(lines, 1, 2);

    /// <summary>[S-11400] Lines held to 1..<paramref name="most"/> (a kind whose rows have a third line: 3).</summary>
    public static int ClampLines(int lines, int most) => Math.Clamp(lines, 1, Math.Clamp(most, 1, MaxLines));

    /// <summary>[S-11400] Columns held to 1 or 2.</summary>
    public static int ClampColumns(int columns) => Math.Clamp(columns, 1, MaxColumns);

    /// <summary>[S-2440] The config's choice, held to its ranges (its columns where it has them, [S-11400]).</summary>
    public static KitRowsChoice Choice(IKitRows rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return new(ClampRows(rows.Rows), ClampLines(rows.Lines, MaxLines), rows is IKitColumns c ? ClampColumns(c.Columns) : 1);
    }

    /// <summary>
    /// [S-2440] The height's steps: everything but the rows as the base (a table's column titles
    /// included, [S-11401]), a row as the step, the config's rows as the count.
    /// </summary>
    public static SizeSteps Steps(KitRowsChoice choice, bool addFooter)
        => new(
            FamilySpace.ContentTop + (choice.ColumnTitles ? KitLadder.ColumnHeaderHeight : 0) + (addFooter ? FamilyShape.Row1 : 0) + FamilySpace.Inset,
            FamilyShape.RowHeight(choice.Lines),
            MinRows,
            MaxRows,
            choice.Rows);

    /// <summary>
    /// [S-2440] The sizing of a kit list showing <paramref name="shown"/> (the config's rows and the lines
    /// its rows really have: a one-line style has one): its width range (the kit's, or the look's own
    /// <paramref name="widths"/>; times its columns, [S-11400]), its height in rows.
    /// </summary>
    public static WidgetSizing Sizing(KitRowsChoice shown, bool addFooter, KitWidths? widths = null)
    {
        var w = widths ?? new KitWidths(MinWidth, NaturalWidth, MaxWidth);
        var columns = ClampColumns(shown.Columns);
        return WidgetSizing.Range(new(w.Min * columns, 1), new(w.Natural * columns, 1), new(w.Max * columns, 1))
            .WithHeightSteps(Steps(shown, addFooter));
    }

    /// <summary>
    /// [S-2442] The config a list of <paramref name="size"/> means: the rows that height holds, its rows
    /// being as <paramref name="shown"/> says. A copy when they change, the same config otherwise; never
    /// the given one changed.
    /// </summary>
    public static WidgetConfig SizeSets(WidgetConfig config, SizeDip size, KitRowsChoice shown, bool addFooter)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config is not IKitRows rows)
        {
            return config;
        }

        var wanted = Steps(shown, addFooter).Nearest(size.Height);
        if (wanted == rows.Rows)
        {
            return config;
        }

        var copy = config.Clone();
        ((IKitRows)copy).Rows = wanted;
        return copy;
    }
}
