namespace Themia.Core.Widgets;

/// <summary>A key that moves the folder cursor ([S-1327]).</summary>
public enum FolderMove
{
    Up,
    Down,
    Left,
    Right,
    Home,
    End,
    PageUp,
    PageDown,
}

/// <summary>
/// [S-1327] The folder widget's selection, as Explorer has it: a set of selected items, an anchor a
/// Shift range runs from, and a focused item the keys move. Kept by full path rather than by row
/// index, so a re-read that adds or removes rows ([S-1333]) keeps what the user had selected.
/// <para>
/// Pure and UI-free: the view feeds it clicks, keys and rubber-band hits against the current order
/// (<see cref="SetOrder"/>) and draws whatever <see cref="IsSelected"/> says.
/// </para>
/// </summary>
public sealed class FolderSelection
{
    private readonly HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);
    private List<string> _order = [];
    private Dictionary<string, int> _index = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The item the keys move from, or null when nothing has been focused yet.</summary>
    public string? Focus { get; private set; }

    /// <summary>Where a Shift range starts.</summary>
    public string? Anchor { get; private set; }

    public int Count => _selected.Count;

    public bool IsEmpty => _selected.Count == 0;

    /// <summary>The focused item's row, or -1.</summary>
    public int FocusIndex => Focus is not null && _index.TryGetValue(Focus, out var i) ? i : -1;

    /// <summary>The selected paths, in display order.</summary>
    public IReadOnlyList<string> Selected => [.. _order.Where(_selected.Contains)];

    public bool IsSelected(string path) => _selected.Contains(path);

    /// <summary>
    /// The items as now drawn, in order. Selected items that are gone are dropped; a focus or anchor
    /// that is gone is cleared. Same names in the same folder keep their selection across a re-read.
    /// </summary>
    public void SetOrder(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        _order = [.. paths];
        _index = new Dictionary<string, int>(_order.Count, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < _order.Count; i++)
        {
            _index.TryAdd(_order[i], i);
        }

        _selected.RemoveWhere(path => !_index.ContainsKey(path));

        if (Focus is not null && !_index.ContainsKey(Focus))
        {
            Focus = null;
        }

        if (Anchor is not null && !_index.ContainsKey(Anchor))
        {
            Anchor = null;
        }
    }

    /// <summary>
    /// [S-1327] A click on row <paramref name="index"/>: plain selects only it; Ctrl toggles it; Shift
    /// selects the range from the anchor (Ctrl+Shift adds the range to what is selected).
    /// </summary>
    public void Click(int index, bool ctrl, bool shift)
    {
        if (!InRange(index))
        {
            return;
        }

        var path = _order[index];

        if (shift)
        {
            var anchor = Anchor is not null && _index.TryGetValue(Anchor, out var a) ? a : index;

            if (!ctrl)
            {
                _selected.Clear();
            }

            SelectRange(anchor, index);
            Anchor ??= _order[anchor];
            Focus = path;
            return;
        }

        if (ctrl)
        {
            if (!_selected.Remove(path))
            {
                _selected.Add(path);
            }

            Anchor = path;
            Focus = path;
            return;
        }

        _selected.Clear();
        _selected.Add(path);
        Anchor = path;
        Focus = path;
    }

    /// <summary>
    /// A right-click on a row: an unselected row becomes the selection (so the menu acts on what was
    /// clicked); a selected one keeps the whole selection, so a menu can act on several.
    /// </summary>
    public void Context(int index)
    {
        if (!InRange(index) || _selected.Contains(_order[index]))
        {
            return;
        }

        Click(index, ctrl: false, shift: false);
    }

    /// <summary>
    /// [S-1327] A key that moves the focus. <paramref name="columns"/> is how many items sit on a row
    /// (1 in a list), <paramref name="pageRows"/> how many rows a page is. Extend (Shift) grows the
    /// range from the anchor; otherwise the focused item alone is selected. Returns whether anything
    /// moved.
    /// </summary>
    public bool Move(FolderMove move, int columns, int pageRows, bool extend)
    {
        if (_order.Count == 0)
        {
            return false;
        }

        columns = Math.Max(1, columns);
        pageRows = Math.Max(1, pageRows);

        var from = FocusIndex;
        int to;

        if (from < 0)
        {
            // Nothing focused: the first key lands on the first (or, End, the last) item rather than
            // moving from an imaginary row.
            to = move is FolderMove.End ? _order.Count - 1 : 0;
        }
        else
        {
            to = move switch
            {
                FolderMove.Up => from - columns,
                FolderMove.Down => from + columns,
                FolderMove.Left => columns > 1 ? from - 1 : from,
                FolderMove.Right => columns > 1 ? from + 1 : from,
                FolderMove.Home => 0,
                FolderMove.End => _order.Count - 1,
                FolderMove.PageUp => from - (pageRows * columns),
                FolderMove.PageDown => from + (pageRows * columns),
                _ => from,
            };

            // Up from the top row, or Down from the last full row, stays put rather than wrapping or
            // jumping a column: Explorer clamps the same way.
            if (move is FolderMove.Up or FolderMove.Down && (to < 0 || to >= _order.Count))
            {
                to = move is FolderMove.Down && from / columns < (_order.Count - 1) / columns ? _order.Count - 1 : from;
            }

            to = Math.Clamp(to, 0, _order.Count - 1);
        }

        var path = _order[to];

        if (extend)
        {
            var anchor = Anchor is not null && _index.TryGetValue(Anchor, out var a) ? a : Math.Max(from, 0);
            Anchor = _order[anchor];
            _selected.Clear();
            SelectRange(anchor, to);
        }
        else
        {
            _selected.Clear();
            _selected.Add(path);
            Anchor = path;
        }

        var moved = Focus is null || !string.Equals(Focus, path, StringComparison.OrdinalIgnoreCase);
        Focus = path;
        return moved || extend;
    }

    /// <summary>[S-1327] Ctrl+A: everything shown.</summary>
    public void SelectAll()
    {
        _selected.Clear();
        _selected.UnionWith(_order);
        Focus ??= _order.Count > 0 ? _order[0] : null;
        Anchor ??= Focus;
    }

    /// <summary>
    /// [S-1328] A rubber band covering <paramref name="hits"/> (row indices), combined with what was
    /// selected when it started (<paramref name="before"/>) as <paramref name="mode"/> says ([S-6413]):
    /// replace, add (Shift) or toggle each covered item (Ctrl). The focus goes to the last covered item.
    /// </summary>
    public void Band(IEnumerable<int> hits, IReadOnlyCollection<string>? before, BandMode mode)
    {
        ArgumentNullException.ThrowIfNull(hits);

        _selected.Clear();

        if (mode != BandMode.Replace && before is not null)
        {
            _selected.UnionWith(before);
        }

        string? last = null;

        foreach (var i in hits)
        {
            if (!InRange(i))
            {
                continue;
            }

            var path = _order[i];

            if (mode == BandMode.Toggle && before is not null && before.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                _selected.Remove(path);
            }
            else
            {
                _selected.Add(path);
            }

            last = path;
        }

        if (last is not null)
        {
            Focus = last;
            Anchor ??= last;
        }
    }

    /// <summary>Escape, or a click outside: nothing selected, nothing focused.</summary>
    public void Clear()
    {
        _selected.Clear();
        Focus = null;
        Anchor = null;
    }

    /// <summary>Select exactly <paramref name="path"/> (a new folder about to be renamed, [S-1331]).</summary>
    public bool SelectOnly(string path)
    {
        if (!_index.ContainsKey(path))
        {
            return false;
        }

        _selected.Clear();
        _selected.Add(path);
        Focus = path;
        Anchor = path;
        return true;
    }

    private bool InRange(int index) => index >= 0 && index < _order.Count;

    private void SelectRange(int a, int b)
    {
        var lo = Math.Min(a, b);
        var hi = Math.Max(a, b);

        for (var i = lo; i <= hi; i++)
        {
            _selected.Add(_order[i]);
        }
    }
}
