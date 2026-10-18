namespace Themia.Core.Widgets;

/// <summary>
/// [S-1390] Which shortcuts are selected, and where the keyboard cursor is (original S-40..S-42,
/// S-63). Keyed by item id, so a selection survives a reorder, a search and a redraw, and an id
/// that is no longer shown is pruned rather than acted on.
/// <para>
/// Every method takes the <i>visible</i> order: a range, a select-all and an arrow step all walk
/// what is on screen, so a search narrows them too.
/// </para>
/// </summary>
public sealed class ShortcutSelection
{
    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);

    /// <summary>The selection a rubber band started from (Ctrl held: the band adds to it).</summary>
    private HashSet<string>? _bandBase;

    /// <summary>Where a Shift range is measured from.</summary>
    public string? Anchor { get; private set; }

    /// <summary>The item the arrows move from, and the one F2 / the Menu key act on.</summary>
    public string? Cursor { get; private set; }

    public int Count => _ids.Count;

    public bool Contains(string id) => _ids.Contains(id);

    /// <summary>The selected ids in visible order.</summary>
    public IReadOnlyList<string> Ordered(IReadOnlyList<string> order) => order.Where(_ids.Contains).ToList();

    /// <summary>
    /// A left press on an item: plain selects only it, Ctrl toggles it, Shift selects the range from
    /// the anchor (inclusive), Ctrl+Shift adds that range.
    /// </summary>
    public void Click(IReadOnlyList<string> order, string id, bool ctrl, bool shift)
    {
        if (shift && Anchor is { } anchor && order.Contains(anchor) && order.Contains(id))
        {
            if (!ctrl)
            {
                _ids.Clear();
            }

            AddRange(order, anchor, id);
            Cursor = id;
            return;
        }

        if (ctrl)
        {
            if (!_ids.Remove(id))
            {
                _ids.Add(id);
            }

            Anchor = id;
            Cursor = id;
            return;
        }

        _ids.Clear();
        _ids.Add(id);
        Anchor = id;
        Cursor = id;
    }

    /// <summary>
    /// A right press: an item outside the selection becomes the whole selection, so the menu acts on
    /// what the pointer is on; one inside it keeps the selection, so "Open all" can follow.
    /// </summary>
    public void ContextPress(string id)
    {
        if (_ids.Contains(id))
        {
            Cursor = id;
            return;
        }

        _ids.Clear();
        _ids.Add(id);
        Anchor = id;
        Cursor = id;
    }

    public void SelectAll(IReadOnlyList<string> order)
    {
        _ids.Clear();

        foreach (var id in order)
        {
            _ids.Add(id);
        }

        Cursor ??= order.Count > 0 ? order[0] : null;
        Anchor ??= Cursor;
    }

    /// <summary>Drop the selection and the cursor. Returns whether there was anything to drop.</summary>
    public bool Clear()
    {
        var had = _ids.Count > 0 || Cursor is not null;
        _ids.Clear();
        Anchor = null;
        Cursor = null;
        _bandBase = null;
        return had;
    }

    /// <summary>Forget ids no longer in <paramref name="order"/> — removed, or hidden by a search.</summary>
    public void Prune(IReadOnlyList<string> order)
    {
        var present = new HashSet<string>(order, StringComparer.Ordinal);
        _ids.RemoveWhere(id => !present.Contains(id));

        if (Anchor is { } anchor && !present.Contains(anchor))
        {
            Anchor = null;
        }

        if (Cursor is { } cursor && !present.Contains(cursor))
        {
            Cursor = null;
        }
    }

    /// <summary>A rubber band starts: remember what it adds to (Ctrl) or start from nothing.</summary>
    public void BeginBand(bool additive)
    {
        _bandBase = additive ? new HashSet<string>(_ids, StringComparer.Ordinal) : [];

        if (!additive)
        {
            _ids.Clear();
        }
    }

    /// <summary>
    /// The band moved: the selection is its base plus every item the band now touches — live, so an
    /// item the band leaves again drops out.
    /// </summary>
    public void Band(IReadOnlyList<string> order, IEnumerable<string> touched)
    {
        _ids.Clear();

        if (_bandBase is { } start)
        {
            _ids.UnionWith(start);
        }

        string? last = null;

        foreach (var id in touched)
        {
            if (order.Contains(id))
            {
                _ids.Add(id);
                last = id;
            }
        }

        if (last is not null)
        {
            Cursor = last;
            Anchor ??= last;
        }
    }

    public void EndBand() => _bandBase = null;

    /// <summary>
    /// An arrow, Home, End or a page key: move the cursor and select only it. In a grid Up and Down
    /// step a whole row (<paramref name="columns"/>), Left and Right one item; a list has one column.
    /// With no cursor yet, a forward key lands on the first item and a backward one on the last.
    /// Returns whether the key was used.
    /// </summary>
    public bool Move(IReadOnlyList<string> order, WidgetKey key, int columns, int pageRows = 4)
    {
        var navigates = key is WidgetKey.Left or WidgetKey.Right or WidgetKey.Up or WidgetKey.Down
            or WidgetKey.Home or WidgetKey.End or WidgetKey.PageUp or WidgetKey.PageDown;

        if (!navigates || order.Count == 0)
        {
            return false;
        }

        columns = Math.Max(1, columns);
        var at = Cursor is { } cursor ? IndexOf(order, cursor) : -1;
        var last = order.Count - 1;

        int? next = key switch
        {
            WidgetKey.Home => 0,
            WidgetKey.End => last,
            _ when at < 0 => key is WidgetKey.Up or WidgetKey.Left or WidgetKey.PageUp ? last : 0,
            WidgetKey.Left => Math.Max(0, at - 1),
            WidgetKey.Right => Math.Min(last, at + 1),
            WidgetKey.Up => at - columns >= 0 ? at - columns : at,
            WidgetKey.Down => Down(at, columns, last),
            WidgetKey.PageUp => Math.Max(at % columns, at - (columns * Math.Max(1, pageRows))),
            WidgetKey.PageDown => Math.Min(last, at + (columns * Math.Max(1, pageRows))),
            _ => null,
        };

        if (next is not { } target)
        {
            return false;
        }

        _ids.Clear();
        _ids.Add(order[target]);
        Cursor = order[target];
        Anchor = Cursor;
        return true;
    }

    /// <summary>Space: toggle the item under the cursor in or out of the selection.</summary>
    public bool ToggleCursor()
    {
        if (Cursor is not { } id)
        {
            return false;
        }

        if (!_ids.Remove(id))
        {
            _ids.Add(id);
        }

        Anchor = id;
        return true;
    }

    /// <summary>
    /// Down in a grid: a row down; past the last full row, onto the last item if a later row exists
    /// at all (as Explorer does), otherwise stay.
    /// </summary>
    private static int Down(int at, int columns, int last)
    {
        if (at + columns <= last)
        {
            return at + columns;
        }

        var row = at / columns;
        var lastRow = last / columns;
        return lastRow > row ? last : at;
    }

    private void AddRange(IReadOnlyList<string> order, string from, string to)
    {
        var a = IndexOf(order, from);
        var b = IndexOf(order, to);

        for (var i = Math.Min(a, b); i <= Math.Max(a, b); i++)
        {
            _ids.Add(order[i]);
        }
    }

    private static int IndexOf(IReadOnlyList<string> order, string id)
    {
        for (var i = 0; i < order.Count; i++)
        {
            if (string.Equals(order[i], id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
