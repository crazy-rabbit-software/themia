using Themia.Sdk;

namespace Themia.Core.Widgets;

/// <summary>[S-11708] A tile of the page as the keys walk it: its section and its place in that section.</summary>
public readonly record struct AddCursor(int Section, int Tile);

/// <summary>[S-11708] What a key did: where the keys are now (null: in the search box), and whether the key was the page's.</summary>
public readonly record struct AddKeyStep(AddCursor? Cursor, bool Handled);

/// <summary>
/// [S-11708] The add page's keys, pure: the arrows walk the tiles as they stand (each section a grid of
/// <c>columns</c> across, the sections one under another), Home and End to the first and the last,
/// Up from the first row back to the search box, Down from the box to the first tile; Ctrl+Tab and
/// Ctrl+Shift+Tab step the tabs round. Right to left the view swaps Left and Right before asking.
/// </summary>
public static class AddPageKeys
{
    /// <summary>
    /// Where <paramref name="key"/> takes the keys from <paramref name="at"/> (null: the search box) over
    /// sections of <paramref name="counts"/> tiles each, <paramref name="columns"/> across.
    /// </summary>
    public static AddKeyStep Move(IReadOnlyList<int> counts, int columns, AddCursor? at, WidgetKey key)
    {
        ArgumentNullException.ThrowIfNull(counts);
        columns = Math.Max(1, columns);

        var first = Next(counts, -1);
        if (at is not { } cursor || !Valid(counts, cursor))
        {
            // In the box: Down goes to the first tile; Left, Right, Home and End are the caret's.
            return key == WidgetKey.Down && first is { } top ? new AddKeyStep(new AddCursor(top, 0), true) : new AddKeyStep(at, false);
        }

        var count = counts[cursor.Section];
        var column = cursor.Tile % columns;
        switch (key)
        {
            case WidgetKey.Right:
                return Step(cursor.Tile + 1 < count
                    ? cursor with { Tile = cursor.Tile + 1 }
                    : Next(counts, cursor.Section) is { } after ? new AddCursor(after, 0) : cursor);

            case WidgetKey.Left:
                return Step(cursor.Tile > 0
                    ? cursor with { Tile = cursor.Tile - 1 }
                    : Previous(counts, cursor.Section) is { } before ? new AddCursor(before, counts[before] - 1) : cursor);

            case WidgetKey.Down:
                if (cursor.Tile + columns < count)
                {
                    return Step(cursor with { Tile = cursor.Tile + columns });
                }

                // The last row: the same column of the next section's first row, else its last tile.
                if (cursor.Tile / columns < (count - 1) / columns)
                {
                    return Step(cursor with { Tile = count - 1 });
                }

                return Step(Next(counts, cursor.Section) is { } below ? new AddCursor(below, Math.Min(column, counts[below] - 1)) : cursor);

            case WidgetKey.Up:
                if (cursor.Tile >= columns)
                {
                    return Step(cursor with { Tile = cursor.Tile - columns });
                }

                // The first row: the same column of the last row above, or back to the box.
                if (Previous(counts, cursor.Section) is { } above)
                {
                    var lastRow = (counts[above] - 1) / columns * columns;
                    return Step(new AddCursor(above, Math.Min(lastRow + column, counts[above] - 1)));
                }

                return new AddKeyStep(null, true);

            case WidgetKey.Home:
                return Step(new AddCursor(first!.Value, 0));

            case WidgetKey.End:
                var last = Previous(counts, counts.Count)!.Value;
                return Step(new AddCursor(last, counts[last] - 1));

            default:
                return new AddKeyStep(cursor, false);
        }
    }

    /// <summary>[S-11708] Ctrl+Tab (or Ctrl+Shift+Tab, <paramref name="back"/>) from tab <paramref name="current"/> of <paramref name="count"/>, round.</summary>
    public static int Tab(int count, int current, bool back)
        => count <= 0 ? 0 : ((current + (back ? -1 : 1)) % count + count) % count;

    private static AddKeyStep Step(AddCursor cursor) => new(cursor, true);

    private static bool Valid(IReadOnlyList<int> counts, AddCursor at)
        => at.Section >= 0 && at.Section < counts.Count && at.Tile >= 0 && at.Tile < counts[at.Section];

    /// <summary>The first section after <paramref name="section"/> that has a tile.</summary>
    private static int? Next(IReadOnlyList<int> counts, int section)
    {
        for (var s = section + 1; s < counts.Count; s++)
        {
            if (counts[s] > 0)
            {
                return s;
            }
        }

        return null;
    }

    /// <summary>The last section before <paramref name="section"/> that has a tile.</summary>
    private static int? Previous(IReadOnlyList<int> counts, int section)
    {
        for (var s = section - 1; s >= 0; s--)
        {
            if (counts[s] > 0)
            {
                return s;
            }
        }

        return null;
    }
}
