using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>A row of the menu as shown: its column (the top columns first, then each open submenu) and its row.</summary>
public readonly record struct AddSpot(int Column, int Row);

/// <summary>
/// [S-3514] Where the add menu's tree stands: which folders are open, as a cascade, and where the
/// highlight is. <see cref="Open"/> lists the folder rows opened, outermost first: the first in a top
/// column, each next one in the submenu the one before opened (column <c>top + i - 1</c>).
/// <see cref="Cursor"/> null is "on the search box".
/// </summary>
public sealed record AddMenuNav(IReadOnlyList<AddSpot> Open, AddSpot? Cursor)
{
    /// <summary>Nothing open, the search box has the keys: how the menu opens.</summary>
    public static AddMenuNav Start { get; } = new([], null);
}

/// <summary>What a key did in the add menu.</summary>
public enum AddMenuAction
{
    /// <summary>Not the menu's: the search box's own (the caret, a typed space).</summary>
    Unhandled,

    /// <summary>The menu moved, opened or closed a submenu.</summary>
    Moved,

    /// <summary>Add the widget on the cursor (<see cref="AddMenuStep.Kind"/>).</summary>
    Pick,

    /// <summary>Close the menu: Escape with nothing open.</summary>
    Close,
}

/// <summary>The menu after a key, and what to do.</summary>
public readonly record struct AddMenuStep(AddMenuNav Nav, AddMenuAction Action, WidgetKind Kind = default);

/// <summary>
/// [S-3514] The add menu's tree walked, pure, so the dropdown is a drawing of what these return:
/// which columns show for a state, what the pointer does, and what each key does.
/// </summary>
public static class AddMenuKeys
{
    /// <summary>
    /// The columns shown, left to right: the top columns, then each open folder's rows. An open
    /// spot that names no folder (the tree changed under it) ends the cascade there.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<AddRow>> Columns(IReadOnlyList<AddColumn> top, AddMenuNav nav)
    {
        ArgumentNullException.ThrowIfNull(top);
        ArgumentNullException.ThrowIfNull(nav);

        var columns = new List<IReadOnlyList<AddRow>>(top.Select(column => column.Rows));
        foreach (var spot in nav.Open)
        {
            if (FolderAt(columns, spot) is not { } folder)
            {
                break;
            }

            columns.Add(folder.Rows);
        }

        return columns;
    }

    /// <summary>The open folders, outermost first, as far as they name folders.</summary>
    public static IReadOnlyList<AddFolder> OpenFolders(IReadOnlyList<AddColumn> top, AddMenuNav nav)
    {
        ArgumentNullException.ThrowIfNull(nav);

        var columns = Columns(top, nav);
        var open = new List<AddFolder>();
        foreach (var spot in nav.Open)
        {
            if (FolderAt(columns, spot) is not { } folder)
            {
                break;
            }

            open.Add(folder);
        }

        return open;
    }

    /// <summary>
    /// [S-3514] The pointer rested on (or clicked) a row: the submenus of its column and beyond close,
    /// a folder row opens its own, and the highlight goes to it.
    /// </summary>
    public static AddMenuNav Point(IReadOnlyList<AddColumn> top, AddMenuNav nav, AddSpot spot)
    {
        ArgumentNullException.ThrowIfNull(top);
        ArgumentNullException.ThrowIfNull(nav);

        var columns = Columns(top, nav);
        if (RowAt(columns, spot) is not { } row || row is AddRule)
        {
            return nav;
        }

        var open = Below(top, nav, spot.Column);
        if (row is AddFolder)
        {
            open = [.. open, spot];
        }

        return new AddMenuNav(open, spot);
    }

    /// <summary>[S-3514] What <paramref name="key"/> does in the tree (nothing typed in the search box).</summary>
    public static AddMenuStep Move(IReadOnlyList<AddColumn> top, AddMenuNav nav, WidgetKey key)
    {
        ArgumentNullException.ThrowIfNull(top);
        ArgumentNullException.ThrowIfNull(nav);

        var columns = Columns(top, nav);
        if (nav.Cursor is not { } cursor || RowAt(columns, cursor) is null)
        {
            return key switch
            {
                WidgetKey.Down when First(columns, 0) is { } first => Moved(nav with { Cursor = first }),
                WidgetKey.Up or WidgetKey.Down => Moved(nav with { Cursor = null }),
                WidgetKey.Escape => Escape(top, nav with { Cursor = null }),
                _ => new AddMenuStep(nav, AddMenuAction.Unhandled),
            };
        }

        var row = RowAt(columns, cursor)!;
        var isTop = cursor.Column < top.Count;

        switch (key)
        {
            case WidgetKey.Down:
                return Moved(Vertical(top, nav, Step(columns, cursor, +1) ?? cursor));

            case WidgetKey.Up:
                return Step(columns, cursor, -1) is { } above
                    ? Moved(Vertical(top, nav, above))
                    : Moved(isTop ? new AddMenuNav(Below(top, nav, cursor.Column), null) : nav);

            case WidgetKey.Home when First(columns, cursor.Column) is { } first:
                return Moved(Vertical(top, nav, first));

            case WidgetKey.End when Last(columns, cursor.Column) is { } last:
                return Moved(Vertical(top, nav, last));

            case WidgetKey.Right or WidgetKey.Enter or WidgetKey.Space when row is AddFolder:
                return Moved(Enter(top, nav, cursor));

            case WidgetKey.Enter or WidgetKey.Space when row is AddPick pick:
                return new AddMenuStep(nav, AddMenuAction.Pick, pick.Kind);

            case WidgetKey.Right when isTop && cursor.Column + 1 < top.Count:
                return Moved(Across(top, nav, cursor, cursor.Column + 1));

            case WidgetKey.Right:
                return Moved(nav);

            case WidgetKey.Left when !isTop:
                return Moved(Back(top, nav, cursor.Column));

            case WidgetKey.Left when cursor.Column > 0:
                return Moved(Across(top, nav, cursor, cursor.Column - 1));

            case WidgetKey.Left:
                return Moved(nav);

            case WidgetKey.Escape:
                return Escape(top, nav);

            default:
                return new AddMenuStep(nav, AddMenuAction.Unhandled);
        }
    }

    /// <summary>
    /// [S-3515] The search's results, a flat list: Down from the box to the first, Up from the first
    /// back to the box, Home and End on a row to the ends. In the box, the caret keeps Left, Right,
    /// Home and End. Returns the new highlight (−1 the box), or null when the key is not the list's.
    /// </summary>
    public static int? MoveInList(int count, int current, WidgetKey key)
    {
        current = Math.Min(current, count - 1);
        if (count <= 0)
        {
            return key is WidgetKey.Up or WidgetKey.Down ? -1 : null;
        }

        return key switch
        {
            WidgetKey.Down => Math.Min(current + 1, count - 1),
            WidgetKey.Up => current <= 0 ? -1 : current - 1,
            WidgetKey.Home when current >= 0 => 0,
            WidgetKey.End when current >= 0 => count - 1,
            _ => null,
        };
    }

    private static AddMenuStep Moved(AddMenuNav nav) => new(nav, AddMenuAction.Moved);

    /// <summary>Escape closes the innermost submenu (the highlight going back to its row if it was in it); with none open, the menu.</summary>
    private static AddMenuStep Escape(IReadOnlyList<AddColumn> top, AddMenuNav nav)
    {
        var open = OpenFolders(top, nav).Count;
        if (open == 0)
        {
            return new AddMenuStep(nav, AddMenuAction.Close);
        }

        var parent = nav.Open[open - 1];
        var closing = top.Count + open - 1;
        var cursor = nav.Cursor is { } at && at.Column >= closing ? parent : nav.Cursor;
        return Moved(new AddMenuNav([.. nav.Open.Take(open - 1)], cursor));
    }

    /// <summary>Open the folder at <paramref name="spot"/> and go to its first row.</summary>
    private static AddMenuNav Enter(IReadOnlyList<AddColumn> top, AddMenuNav nav, AddSpot spot)
    {
        var opened = Point(top, nav, spot);
        var column = top.Count + opened.Open.Count - 1;
        return First(Columns(top, opened), column) is { } first ? opened with { Cursor = first } : opened;
    }

    /// <summary>Close the submenu in <paramref name="column"/> (and any beyond it); the highlight goes back to the row that opened it.</summary>
    private static AddMenuNav Back(IReadOnlyList<AddColumn> top, AddMenuNav nav, int column)
    {
        var index = column - top.Count;
        var parent = nav.Open[index];
        return new AddMenuNav([.. nav.Open.Take(index)], parent);
    }

    /// <summary>To the nearest row of the top column <paramref name="target"/>, every submenu closed.</summary>
    private static AddMenuNav Across(IReadOnlyList<AddColumn> top, AddMenuNav nav, AddSpot from, int target)
    {
        var closed = new AddMenuNav([], from);
        var columns = Columns(top, closed);
        return Nearest(columns, target, from.Row) is { } spot ? closed with { Cursor = spot } : nav;
    }

    /// <summary>A move up or down a column: the submenus opened from it or beyond close.</summary>
    private static AddMenuNav Vertical(IReadOnlyList<AddColumn> top, AddMenuNav nav, AddSpot to) =>
        to == nav.Cursor ? nav : new AddMenuNav(Below(top, nav, to.Column), to);

    /// <summary>The open spots that stand left of <paramref name="column"/>: what stays when a row in it is chosen.</summary>
    private static AddSpot[] Below(IReadOnlyList<AddColumn> top, AddMenuNav nav, int column)
    {
        // A top column's row closes every submenu: the cascade hangs off one top row at a time.
        if (column < top.Count)
        {
            return [];
        }

        // The submenu in column c was opened by Open[c - top]: it and those before it stay.
        return [.. nav.Open.Take(column - top.Count + 1)];
    }

    private static AddRow? RowAt(IReadOnlyList<IReadOnlyList<AddRow>> columns, AddSpot spot) =>
        spot.Column >= 0 && spot.Column < columns.Count && spot.Row >= 0 && spot.Row < columns[spot.Column].Count
            ? columns[spot.Column][spot.Row]
            : null;

    private static AddFolder? FolderAt(IReadOnlyList<IReadOnlyList<AddRow>> columns, AddSpot spot) => RowAt(columns, spot) as AddFolder;

    private static AddSpot? Step(IReadOnlyList<IReadOnlyList<AddRow>> columns, AddSpot from, int step)
    {
        var rows = columns[from.Column];
        for (var r = from.Row + step; r >= 0 && r < rows.Count; r += step)
        {
            if (rows[r] is not AddRule)
            {
                return new AddSpot(from.Column, r);
            }
        }

        return null;
    }

    private static AddSpot? First(IReadOnlyList<IReadOnlyList<AddRow>> columns, int column) =>
        column < columns.Count ? Step(columns, new AddSpot(column, -1), +1) : null;

    private static AddSpot? Last(IReadOnlyList<IReadOnlyList<AddRow>> columns, int column) =>
        column < columns.Count ? Step(columns, new AddSpot(column, columns[column].Count), -1) : null;

    /// <summary>The row of <paramref name="column"/> nearest <paramref name="row"/>; ties go up.</summary>
    private static AddSpot? Nearest(IReadOnlyList<IReadOnlyList<AddRow>> columns, int column, int row)
    {
        if (column < 0 || column >= columns.Count)
        {
            return null;
        }

        AddSpot? best = null;
        var distance = int.MaxValue;
        var rows = columns[column];
        for (var r = 0; r < rows.Count; r++)
        {
            if (rows[r] is AddRule || Math.Abs(r - row) >= distance)
            {
                continue;
            }

            distance = Math.Abs(r - row);
            best = new AddSpot(column, r);
        }

        return best;
    }
}
