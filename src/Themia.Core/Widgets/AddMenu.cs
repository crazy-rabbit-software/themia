namespace Themia.Core.Widgets;

/// <summary>One line of the add menu: a section's heading (<see cref="Entry"/> −1) or one of its entries.</summary>
/// <param name="Section">The section, in the order the menu shows them.</param>
/// <param name="Entry">The entry inside its section, or −1 for the section's heading.</param>
public readonly record struct AddMenuLine(int Section, int Entry)
{
    public bool IsHeading => Entry < 0;
}

/// <summary>
/// [S-3502] The add menu laid out: its columns, top to bottom, each a list of lines, and where every
/// entry stands. Entries are counted in reading order (down a column, then on into the next), the
/// order the keys walk.
/// </summary>
public sealed class AddMenuGrid
{
    private readonly (int Column, int Row)[] _spots;

    internal AddMenuGrid(IReadOnlyList<IReadOnlyList<AddMenuLine>> columns, int rowsPerColumn)
    {
        Columns = columns;
        RowsPerColumn = rowsPerColumn;

        var spots = new List<(int, int)>();
        for (var c = 0; c < columns.Count; c++)
        {
            for (var r = 0; r < columns[c].Count; r++)
            {
                if (!columns[c][r].IsHeading)
                {
                    spots.Add((c, r));
                }
            }
        }

        _spots = [.. spots];
    }

    /// <summary>The columns, left to right, each its lines top to bottom.</summary>
    public IReadOnlyList<IReadOnlyList<AddMenuLine>> Columns { get; }

    /// <summary>The most lines a column may hold: the menu's height in lines.</summary>
    public int RowsPerColumn { get; }

    /// <summary>How many entries (not headings) the menu shows.</summary>
    public int EntryCount => _spots.Length;

    /// <summary>Where the <paramref name="entry"/>th entry in reading order stands.</summary>
    public (int Column, int Row) SpotOf(int entry) => _spots[entry];

    /// <summary>The line of the <paramref name="entry"/>th entry in reading order.</summary>
    public AddMenuLine LineOf(int entry)
    {
        var (column, row) = _spots[entry];
        return Columns[column][row];
    }

    /// <summary>The reading-order number of the entry at a column and row, or −1 for a heading or nothing.</summary>
    public int EntryAt(int column, int row)
    {
        for (var i = 0; i < _spots.Length; i++)
        {
            if (_spots[i] == (column, row))
            {
                return i;
            }
        }

        return -1;
    }
}

/// <summary>
/// [S-3502..S-3505] The rules of the edit bar's add menu ([S-3501]): how its sections flow through
/// columns, how many columns it takes on a screen, what a search leaves and in which order, and how
/// the arrow keys move through it. Pure, so the dropdown is a drawing of what these return.
/// <para>
/// The sections flow as a newspaper's columns (after Karpo's add list): down one column and on at the
/// top of the next, a section carrying on where its column ends, so a long section (System) does not
/// make one column three times the height of the rest. A heading is never the last line of a column,
/// where it would head nothing.
/// </para>
/// </summary>
public static class AddMenuLayout
{
    /// <summary>[S-3503] The most columns the menu spreads across, however wide the screen.</summary>
    public const int MostColumns = 6;

    /// <summary>[S-3503] The share of the work area's height the menu's lines may take before it takes another column.</summary>
    public const double MostHeightShare = 0.7;

    /// <summary>[S-3503] The share of the work area's width the columns may take: never all of it.</summary>
    public const double MostWidthShare = 0.75;

    /// <summary>
    /// [S-3502] Flow the sections through columns of at most <paramref name="rowsPerColumn"/> lines,
    /// each section a heading line and then its entries. A section with no entries is left out, heading
    /// and all. Takes as many columns as that needs.
    /// </summary>
    /// <param name="sections">Each section's number of entries, in the order shown.</param>
    /// <param name="rowsPerColumn">Lines per column; at least two, so a heading always has room for an entry under it.</param>
    public static AddMenuGrid Flow(IReadOnlyList<int> sections, int rowsPerColumn)
    {
        ArgumentNullException.ThrowIfNull(sections);
        ArgumentOutOfRangeException.ThrowIfLessThan(rowsPerColumn, 2);

        var columns = new List<List<AddMenuLine>>();
        List<AddMenuLine>? column = null;

        for (var s = 0; s < sections.Count; s++)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sections[s]);
            if (sections[s] == 0)
            {
                continue;
            }

            // A heading needs its first entry under it in the same column.
            if (column is null || column.Count + 2 > rowsPerColumn)
            {
                column = [];
                columns.Add(column);
            }

            column.Add(new AddMenuLine(s, -1));

            for (var e = 0; e < sections[s]; e++)
            {
                if (column.Count >= rowsPerColumn)
                {
                    column = [];
                    columns.Add(column);
                }

                column.Add(new AddMenuLine(s, e));
            }
        }

        return new AddMenuGrid([.. columns], rowsPerColumn);
    }

    /// <summary>
    /// [S-3502] The fewest lines per column that fit the sections into <paramref name="columns"/>
    /// columns: the columns as even as the headings allow.
    /// </summary>
    public static int RowsFor(IReadOnlyList<int> sections, int columns)
    {
        ArgumentNullException.ThrowIfNull(sections);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);

        var lines = Lines(sections);
        var rows = Math.Max(2, (lines + columns - 1) / columns);

        // A heading pushed to the next column can cost a column; one more line each wins it back.
        // Terminates: at `lines` rows everything is one column.
        while (rows < lines && Flow(sections, rows).Columns.Count > columns)
        {
            rows++;
        }

        return rows;
    }

    /// <summary>
    /// [S-3503] How many columns the menu takes, and their height in lines, in a work area of
    /// <paramref name="areaWidth"/> × <paramref name="areaHeight"/>: the fewest columns that keep the
    /// lines within <see cref="MostHeightShare"/> of the height, up to <see cref="MostColumns"/> and to
    /// as many as <see cref="MostWidthShare"/> of the width holds. Where even those do not, it takes
    /// them, and the menu scrolls.
    /// </summary>
    /// <param name="sections">Each section's number of entries.</param>
    /// <param name="columnWidth">One column's width, in the unit of the area.</param>
    /// <param name="columnGap">The space between two columns.</param>
    /// <param name="lineHeight">One line's height (a heading's and an entry's are one).</param>
    public static (int Columns, int Rows) Fit(IReadOnlyList<int> sections, double areaWidth, double areaHeight, double columnWidth, double columnGap, double lineHeight)
    {
        ArgumentNullException.ThrowIfNull(sections);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columnWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineHeight);

        var across = (int)Math.Floor(((Math.Max(0, areaWidth) * MostWidthShare) + columnGap) / (columnWidth + columnGap));
        var most = Math.Clamp(across, 1, MostColumns);
        var tallest = Math.Max(2, (int)Math.Floor(Math.Max(0, areaHeight) * MostHeightShare / lineHeight));

        for (var c = 1; c <= most; c++)
        {
            var rows = RowsFor(sections, c);
            if (rows <= tallest)
            {
                return (Flow(sections, rows).Columns.Count, rows);
            }
        }

        var fallback = RowsFor(sections, most);
        return (Flow(sections, fallback).Columns.Count, fallback);
    }

    /// <summary>
    /// [S-3504] What the menu lists for a query, grouped into its sections. With nothing typed: every
    /// item, in its sections, both in the order given (the catalogue's). With a query: only the items
    /// <see cref="WidgetSearch"/> matches, best first — the sections ordered by their best match and the
    /// items inside each by rank — so the first entry in reading order is the best match.
    /// </summary>
    public static List<(TSection Section, List<T> Items)> Filter<T, TSection>(IEnumerable<(IndexedWidget Index, T Value, TSection Section)> items, string? query)
        where TSection : notnull
    {
        ArgumentNullException.ThrowIfNull(items);

        var all = items.ToList();
        IEnumerable<(T Value, TSection Section)> ordered = WidgetSearch.Tokenize(query).Length == 0
            ? all.Select(item => (item.Value, item.Section))
            : WidgetSearch.Rank(all.Select(item => (item.Index, (item.Value, item.Section))), query);

        var result = new List<(TSection Section, List<T> Items)>();
        var at = new Dictionary<TSection, int>();

        foreach (var (value, section) in ordered)
        {
            if (!at.TryGetValue(section, out var index))
            {
                index = result.Count;
                at[section] = index;
                result.Add((section, []));
            }

            result[index].Items.Add(value);
        }

        return result;
    }

    private static int Lines(IReadOnlyList<int> sections)
    {
        var lines = 0;
        foreach (var count in sections)
        {
            lines += count > 0 ? count + 1 : 0;
        }

        return lines;
    }
}

/// <summary>
/// [S-3505] The add menu's keys, after Karpo's: the highlight is a number in reading order, −1 for
/// "on the search box". Up and Down walk reading order — on into the next column at a column's foot,
/// back into the box above the first entry. Left and Right go to the nearest entry of the column
/// beside, by row. Home and End go to the first and last entry. In the box (−1), Left, Right, Home
/// and End are the text's own (the caret), so they are not the menu's.
/// </summary>
public static class AddMenuKeys
{
    /// <summary>The highlight after <paramref name="key"/>, or null when the key is not the menu's.</summary>
    /// <param name="grid">The menu as shown.</param>
    /// <param name="current">The highlighted entry, or −1 on the search box.</param>
    public static int? Move(AddMenuGrid grid, int current, WidgetKey key)
    {
        ArgumentNullException.ThrowIfNull(grid);

        var count = grid.EntryCount;
        if (current >= count)
        {
            current = count - 1;
        }

        if (count == 0)
        {
            return key is WidgetKey.Up or WidgetKey.Down ? -1 : null;
        }

        return key switch
        {
            WidgetKey.Down => Math.Min(current + 1, count - 1),
            WidgetKey.Up => current <= 0 ? -1 : current - 1,
            WidgetKey.Home when current >= 0 => 0,
            WidgetKey.End when current >= 0 => count - 1,
            WidgetKey.Left when current >= 0 => Beside(grid, current, -1),
            WidgetKey.Right when current >= 0 => Beside(grid, current, +1),
            _ => null,
        };
    }

    /// <summary>The entry in the column <paramref name="step"/> away whose row is nearest; ties go up. Stays at the edges.</summary>
    private static int Beside(AddMenuGrid grid, int current, int step)
    {
        var (column, row) = grid.SpotOf(current);
        var target = column + step;
        if (target < 0 || target >= grid.Columns.Count)
        {
            return current;
        }

        var best = -1;
        var bestDistance = int.MaxValue;
        var lines = grid.Columns[target];

        for (var r = 0; r < lines.Count; r++)
        {
            if (lines[r].IsHeading)
            {
                continue;
            }

            var distance = Math.Abs(r - row);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = grid.EntryAt(target, r);
            }
        }

        return best >= 0 ? best : current;
    }
}
