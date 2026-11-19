using Themia.Sdk;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-13617] A stop of the content as the keys walk it: its run and its place in that run. A run is a section's
/// tiles (a grid), or a family's head alone (a run of one).
/// </summary>
public readonly record struct AddCursor(int Section, int Tile);

/// <summary>
/// [S-13617] What a key did in the content: where the keys are now (null: in the search box, or on the rail when
/// <see cref="ToRail"/>), and whether the key was the page's.
/// </summary>
public readonly record struct AddKeyStep(AddCursor? Cursor, bool Handled)
{
    /// <summary>The keys went back to the rail's chosen item.</summary>
    public bool ToRail { get; init; }
}

/// <summary>[S-13617] Where a key on the rail takes the keys.</summary>
public enum AddRailMove
{
    /// <summary>Not the rail's key.</summary>
    None,

    /// <summary>To the rail's item <see cref="AddRailStep.Item"/>, which is shown (or stays where it is).</summary>
    Item,

    /// <summary>Up from the first item: the search box.</summary>
    Box,

    /// <summary>Right or Enter: into the content, its first stop.</summary>
    Content,
}

/// <summary>[S-13617] What a key on the rail did.</summary>
public readonly record struct AddRailStep(int Item, AddRailMove Move);

/// <summary>
/// [S-13617] The add page's keys, pure. On the <b>rail</b> Up and Down walk its items (the item reached is
/// shown), Home and End go to its first and last, Up from the first to the search box, Right or Enter into the
/// content. In the <b>content</b> the arrows walk the stops as they stand (each run a grid of <c>columns</c>
/// across, the runs one under another; a family's head a run of one over its stack): Left at a row's start goes
/// back to the rail; Home and End to the first and the last stop; Up from the first row to the search box; Down
/// from the box to the first stop. Ctrl+Tab and Ctrl+Shift+Tab step the rail round. Right to left the view swaps
/// Left and Right before asking.
/// </summary>
public static class AddPageKeys
{
    /// <summary>
    /// Where <paramref name="key"/> takes the keys from <paramref name="at"/> (null: the search box) over runs
    /// of <paramref name="counts"/> stops each, <paramref name="columns"/> across.
    /// </summary>
    public static AddKeyStep Move(IReadOnlyList<int> counts, int columns, AddCursor? at, WidgetKey key)
    {
        ArgumentNullException.ThrowIfNull(counts);
        columns = Math.Max(1, columns);

        var first = Next(counts, -1);
        if (at is not { } cursor || !Valid(counts, cursor))
        {
            // In the box: Down goes to the first stop; Left, Right, Home and End are the caret's.
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
                // A row's start: back to the rail.
                return column == 0 ? new AddKeyStep(null, true) { ToRail = true } : Step(cursor with { Tile = cursor.Tile - 1 });

            case WidgetKey.Down:
                if (cursor.Tile + columns < count)
                {
                    return Step(cursor with { Tile = cursor.Tile + columns });
                }

                // The last row: the same column of the next run's first row, else its last stop.
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

    /// <summary>[S-13617] Where <paramref name="key"/> takes the keys from the rail's item <paramref name="at"/> of <paramref name="count"/>.</summary>
    public static AddRailStep Rail(int count, int at, WidgetKey key)
    {
        if (count <= 0)
        {
            return new AddRailStep(0, AddRailMove.None);
        }

        at = Math.Clamp(at, 0, count - 1);
        return key switch
        {
            WidgetKey.Up => at > 0 ? new AddRailStep(at - 1, AddRailMove.Item) : new AddRailStep(at, AddRailMove.Box),
            WidgetKey.Down => new AddRailStep(Math.Min(at + 1, count - 1), AddRailMove.Item),
            WidgetKey.Home => new AddRailStep(0, AddRailMove.Item),
            WidgetKey.End => new AddRailStep(count - 1, AddRailMove.Item),
            WidgetKey.Right or WidgetKey.Enter => new AddRailStep(at, AddRailMove.Content),
            _ => new AddRailStep(at, AddRailMove.None),
        };
    }

    /// <summary>[S-13617] Ctrl+Tab (or Ctrl+Shift+Tab, <paramref name="back"/>) from rail item <paramref name="current"/> of <paramref name="count"/>, round.</summary>
    public static int Tab(int count, int current, bool back)
        => count <= 0 ? 0 : ((current + (back ? -1 : 1)) % count + count) % count;

    private static AddKeyStep Step(AddCursor cursor) => new(cursor, true);

    private static bool Valid(IReadOnlyList<int> counts, AddCursor at)
        => at.Section >= 0 && at.Section < counts.Count && at.Tile >= 0 && at.Tile < counts[at.Section];

    /// <summary>The first run after <paramref name="section"/> that has a stop.</summary>
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

    /// <summary>The last run before <paramref name="section"/> that has a stop.</summary>
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
