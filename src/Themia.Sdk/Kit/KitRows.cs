using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Sdk.Kit;

/// <summary>
/// [S-2440] [S-14810] A kit list that states its rows: its config states how many rows a new card stands (its natural
/// height, [S-2438]) and how many lines each has; the card's height shows every row it reaches at their one size, the
/// last cut by its foot. Any kit list adopts it by implementing this on its config, stating <see cref="KitSizing.Sizing"/>
/// on its descriptor and adding <c>KitMenuLines.Rows</c> to its menu.
/// </summary>
public interface IKitRows
{
    /// <summary>How many rows a new card stands (its natural height): <see cref="KitSizing.MinRows"/>..<see cref="KitSizing.MaxRows"/>.</summary>
    int Rows { get; set; }

    /// <summary>How many lines each row has: 1 to the kind's most (2, or 3 where a row has a third: a preview).</summary>
    int Lines { get; set; }

    /// <summary>
    /// [S-2440] [S-11752] Whether the lines are the user's to choose now: a list whose rows always have the
    /// same lines (a device's two), or whose look draws one line only, offers no Lines line and shares none.
    /// </summary>
    bool OffersLines => true;
}

/// <summary>
/// [S-2440] What a kit list with rows of its own shows, for its plan: rows of a number of lines (its
/// <paramref name="Rows"/> its natural height's), in up to <paramref name="Columns"/> columns as the card's width holds them
/// ([S-11400], [S-14410]: <see cref="KitSizing.ColumnsFor"/>), with a table's column titles over them where
/// <paramref name="ColumnTitles"/> ([S-11401]).
/// </summary>
public readonly record struct KitRowsChoice(int Rows, int Lines, int Columns = 1, bool ColumnTitles = false);

/// <summary>[S-11401] A list's own width range, where a look needs another than the kit's (a table of columns).</summary>
public readonly record struct KitWidths(double Min, double Natural, double Max);

/// <summary>
/// [S-2440] [S-14500] The size of a kit list that states its rows: its rows (28 or 40 each by its lines) at their optimal
/// (whole grid cells, three a row of 28 on the default grid) from the card's top edge to its foot edge, under a table's
/// column titles and over the add row where it has them, each part whole cells; its Rows decide its natural height, and
/// its height takes every cell from one row up, the rows keeping their size, as many as it reaches ([S-14810], owner,
/// 2026-11-20: "the widget items should actually have a fixed size"). The width in its range. Pure, so every kit list shares it.
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

    /// <summary>[S-2440] The config's choice, held to its ranges: one column.</summary>
    public static KitRowsChoice Choice(IKitRows rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return new(ClampRows(rows.Rows), ClampLines(rows.Lines, MaxLines));
    }

    /// <summary>
    /// [S-2440] [S-14500] The height of the parts that stand with the rows, each its own whole cells of the default grid:
    /// <paramref name="over"/> (a kind's own line over its rows, as its natural length from the card's top edge: a day line),
    /// a table's column titles (<see cref="KitPlanner.TitlesOver"/>, [S-11401]) and the add row (a row of 28, [S-3850]).
    /// The rows reach the card's top edge where nothing stands over them and its foot edge where no add row stands under
    /// them: no inset over or under them.
    /// </summary>
    public static double Parts(KitRowsChoice choice, bool addFooter, double over = 0)
    {
        static double Part(double length) => length > 0 && double.IsFinite(length) ? ItemSize.Optimal(length, ItemGrid.Default()) : 0;
        return Part(over) + Part(choice.ColumnTitles ? KitPlanner.TitlesOver : 0) + Part(addFooter ? FamilyShape.Row1 : 0);
    }

    /// <summary>[S-14500] A row's optimal height on the default grid: its natural (28 or 40 by its lines) in whole cells.</summary>
    public static double RowOptimal(int lines) => ItemSize.Optimal(FamilyShape.RowHeight(lines), ItemGrid.Default());

    /// <summary>
    /// [S-2440] [S-14500] The sizing of a kit list showing <paramref name="shown"/> (the config's rows and the lines
    /// its rows really have: a one-line style has one): its width range (the kit's, or the look's own
    /// <paramref name="widths"/>; a card wide enough for two columns' least shows two, [S-11400], [S-14410]), its height its
    /// parts (<see cref="Parts"/>) and one row at least, its rows at natural, every cell between.
    /// </summary>
    public static WidgetSizing Sizing(KitRowsChoice shown, bool addFooter, KitWidths? widths = null, double over = 0)
    {
        var w = widths ?? new KitWidths(MinWidth, NaturalWidth, MaxWidth);
        var parts = Parts(shown, addFooter, over);
        var row = RowOptimal(shown.Lines);
        return WidgetSizing.Range(
            new(w.Min, parts + row),
            new(w.Natural, parts + (ClampRows(shown.Rows) * row)),
            new(w.Max * ClampColumns(shown.Columns), parts + (MaxRows * row))) with { Room = StretchRoom.List };
    }

    /// <summary>[S-11400] [S-14410] The columns a list <paramref name="width"/> wide shows: two where it holds two columns' least, else one.</summary>
    public static int ColumnsFor(double width, KitWidths? widths = null)
        => double.IsFinite(width) && width + LayoutMath.Tolerance >= MaxColumns * (widths ?? new KitWidths(MinWidth, NaturalWidth, MaxWidth)).Min ? MaxColumns : 1;
}
