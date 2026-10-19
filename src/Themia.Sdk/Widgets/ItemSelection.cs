namespace Themia.Core.Widgets;

/// <summary>A key that moves the selection's focus ([S-6623]).</summary>
public enum SelectionMove
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
/// [S-6620]..[S-6626] The selection of a widget's items, as the Windows desktop and File Explorer
/// keep theirs: a set of selected items, an anchor a Shift range runs from, and a focused item the
/// keys move. One rule set for every widget that selects items (the folder's files, the shortcuts);
/// a view feeds it presses, keys and rubber-band hits against the order it draws
/// (<see cref="SetOrder"/>) and paints what <see cref="IsSelected"/>, <see cref="Focus"/> and
/// <see cref="Cues"/> say through the one look ([S-6627]).
/// <para>
/// Kept by id rather than by row index, so a re-read or a redraw that adds or removes items keeps
/// what the user had selected, and an id that is no longer shown is dropped rather than acted on.
/// Pure and UI-free.
/// </para>
/// </summary>
public sealed class ItemSelection
{
    private readonly StringComparer _comparer;
    private readonly HashSet<string> _selected;
    private List<string> _order = [];
    private Dictionary<string, int> _index;

    /// <summary>The selection a rubber band started from, and how the band combines with it ([S-6625]).</summary>
    private HashSet<string>? _bandBase;

    private BandMode _bandMode;

    /// <summary>A plain press on a selected item narrows the selection on release, not press ([S-6621]).</summary>
    private int? _deferred;

    /// <param name="comparer">How ids compare: paths ignore case, the shortcuts' ids do not.</param>
    public ItemSelection(StringComparer? comparer = null)
    {
        _comparer = comparer ?? StringComparer.Ordinal;
        _selected = new HashSet<string>(_comparer);
        _index = new Dictionary<string, int>(_comparer);
    }

    /// <summary>The item the keys move from (and F2 and the Menu key act on), or null when none is focused.</summary>
    public string? Focus { get; private set; }

    /// <summary>Where a Shift range starts.</summary>
    public string? Anchor { get; private set; }

    /// <summary>
    /// [S-6628] Whether the focus is shown: only once the keys have moved it, as Windows draws its
    /// focus rectangle; a press hides it again.
    /// </summary>
    public bool Cues { get; private set; }

    public int Count => _selected.Count;

    public bool IsEmpty => _selected.Count == 0;

    /// <summary>The focused item's position in the order, or -1.</summary>
    public int FocusIndex => Focus is not null && _index.TryGetValue(Focus, out var i) ? i : -1;

    /// <summary>The selected ids, in display order.</summary>
    public IReadOnlyList<string> Selected => [.. _order.Where(_selected.Contains)];

    public bool IsSelected(string id) => _selected.Contains(id);

    public bool IsFocus(string id) => Focus is not null && _comparer.Equals(Focus, id);

    /// <summary>
    /// The items as now drawn, in order. Selected items that are gone are dropped; a focus or anchor
    /// that is gone is cleared. The same ids keep their selection across a redraw.
    /// </summary>
    public void SetOrder(IReadOnlyList<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        _order = [.. ids];
        _index = new Dictionary<string, int>(_order.Count, _comparer);

        for (var i = 0; i < _order.Count; i++)
        {
            _index.TryAdd(_order[i], i);
        }

        _selected.RemoveWhere(id => !_index.ContainsKey(id));
        _bandBase?.RemoveWhere(id => !_index.ContainsKey(id));
        _deferred = null;

        if (Focus is not null && !_index.ContainsKey(Focus))
        {
            Focus = null;
        }

        if (Anchor is not null && !_index.ContainsKey(Anchor))
        {
            Anchor = null;
        }
    }

    /// <summary>The position of <paramref name="id"/> in the order, or -1.</summary>
    public int IndexOf(string id) => _index.TryGetValue(id, out var i) ? i : -1;

    /// <summary>
    /// [S-6621] A left press on item <paramref name="index"/>. A plain press on an item already
    /// selected changes nothing yet, so dragging it drags the whole selection; the release
    /// (<see cref="Release"/>) narrows to it if no drag ran. Anything else is a click at once.
    /// Hides the focus cue.
    /// </summary>
    public void Press(int index, bool ctrl, bool shift)
    {
        Cues = false;
        _deferred = null;

        if (!InRange(index))
        {
            return;
        }

        if (!ctrl && !shift && _selected.Contains(_order[index]))
        {
            _deferred = index;
            Focus = _order[index];
            return;
        }

        Click(index, ctrl, shift);
    }

    /// <summary>[S-6621] The release of a press that was neither a drag nor a band: a deferred press narrows now.</summary>
    public void Release(int index)
    {
        if (_deferred is { } pressed && pressed == index && InRange(index))
        {
            Click(index, ctrl: false, shift: false);
        }

        _deferred = null;
    }

    /// <summary>A drag ran from the press: the deferred narrowing is dropped and the selection kept.</summary>
    public void CancelPress() => _deferred = null;

    /// <summary>
    /// [S-6620] A click on item <paramref name="index"/>: plain selects only it; Ctrl toggles it;
    /// Shift selects the range from the anchor (Ctrl+Shift adds the range to what is selected).
    /// Every click focuses the item.
    /// </summary>
    public void Click(int index, bool ctrl, bool shift)
    {
        if (!InRange(index))
        {
            return;
        }

        var id = _order[index];

        if (shift)
        {
            var anchor = Anchor is not null && _index.TryGetValue(Anchor, out var a) ? a : index;

            if (!ctrl)
            {
                _selected.Clear();
            }

            SelectRange(anchor, index);
            Anchor ??= _order[anchor];
            Focus = id;
            return;
        }

        if (ctrl)
        {
            if (!_selected.Remove(id))
            {
                _selected.Add(id);
            }

            Anchor = id;
            Focus = id;
            return;
        }

        _selected.Clear();
        _selected.Add(id);
        Anchor = id;
        Focus = id;
    }

    /// <summary>
    /// [S-6622] A right press on an item: one outside the selection becomes the whole selection (so the
    /// menu acts on what was clicked); one inside keeps the selection, so the menu acts on all of it.
    /// </summary>
    public void Context(int index)
    {
        Cues = false;

        if (!InRange(index))
        {
            return;
        }

        if (_selected.Contains(_order[index]))
        {
            Focus = _order[index];
            return;
        }

        Click(index, ctrl: false, shift: false);
    }

    /// <summary>
    /// [S-6623] A key that moves the focus. <paramref name="columns"/> is how many items sit on a row
    /// (1 in a list), <paramref name="pageRows"/> how many rows a page is. <paramref name="extend"/>
    /// (Shift) selects the range from the anchor; <paramref name="focusOnly"/> (Ctrl) moves the focus
    /// and leaves the selection; otherwise the focused item alone is selected. With nothing focused the
    /// first key lands on the first item (End on the last). Shows the focus cue. Returns whether a
    /// key was used: always, with items.
    /// </summary>
    public bool Move(SelectionMove move, int columns, int pageRows, bool extend = false, bool focusOnly = false)
    {
        if (_order.Count == 0)
        {
            return false;
        }

        Cues = true;
        columns = Math.Max(1, columns);
        pageRows = Math.Max(1, pageRows);

        var from = FocusIndex;
        int to;

        if (from < 0)
        {
            to = move is SelectionMove.End ? _order.Count - 1 : 0;
        }
        else
        {
            to = move switch
            {
                SelectionMove.Up => from - columns,
                SelectionMove.Down => from + columns,
                SelectionMove.Left => columns > 1 ? from - 1 : from,
                SelectionMove.Right => columns > 1 ? from + 1 : from,
                SelectionMove.Home => 0,
                SelectionMove.End => _order.Count - 1,
                SelectionMove.PageUp => from - (pageRows * columns),
                SelectionMove.PageDown => from + (pageRows * columns),
                _ => from,
            };

            // Up from the top row stays put; Down past the last full row goes to the last item when a
            // later row exists at all, as the desktop and Explorer clamp.
            if (move is SelectionMove.Up or SelectionMove.Down && (to < 0 || to >= _order.Count))
            {
                to = move is SelectionMove.Down && from / columns < (_order.Count - 1) / columns ? _order.Count - 1 : from;
            }

            to = Math.Clamp(to, 0, _order.Count - 1);
        }

        var id = _order[to];

        if (focusOnly)
        {
            // Ctrl+arrow: the focus moves, the selection and its anchor stay.
        }
        else if (extend)
        {
            var anchor = Anchor is not null && _index.TryGetValue(Anchor, out var a) ? a : Math.Max(from, 0);
            Anchor = _order[anchor];
            _selected.Clear();
            SelectRange(anchor, to);
        }
        else
        {
            _selected.Clear();
            _selected.Add(id);
            Anchor = id;
        }

        Focus = id;
        return true;
    }

    /// <summary>[S-6623] Space and Ctrl+Space: the focused item in or out of the selection. False with nothing focused.</summary>
    public bool ToggleFocus()
    {
        if (Focus is not { } id || !_index.ContainsKey(id))
        {
            return false;
        }

        Cues = true;

        if (!_selected.Remove(id))
        {
            _selected.Add(id);
        }

        Anchor = id;
        return true;
    }

    /// <summary>[S-6623] Type-ahead found item <paramref name="index"/>: it alone is selected and focused, with the cue.</summary>
    public void Type(int index)
    {
        if (!InRange(index))
        {
            return;
        }

        Click(index, ctrl: false, shift: false);
        Cues = true;
    }

    /// <summary>[S-6623] Ctrl+A: everything shown.</summary>
    public void SelectAll()
    {
        _selected.Clear();
        _selected.UnionWith(_order);
        Focus ??= _order.Count > 0 ? _order[0] : null;
        Anchor ??= Focus;
    }

    /// <summary>
    /// [S-6625] A rubber band starts: remember the selection it combines with as <paramref name="mode"/>
    /// says (Replace starts from nothing). Hides the focus cue.
    /// </summary>
    public void BeginBand(BandMode mode)
    {
        Cues = false;
        _deferred = null;
        _bandMode = mode;
        _bandBase = mode == BandMode.Replace ? [] : new HashSet<string>(_selected, _comparer);

        if (mode == BandMode.Replace)
        {
            _selected.Clear();
        }
    }

    /// <summary>
    /// [S-6625] The band now covers <paramref name="hits"/> (positions in the order): the selection is
    /// its start combined with them — Replace: the hits; Add: the start plus the hits; Toggle: each hit
    /// flipped against the start. Live: an item the band leaves again goes back to how it was. The
    /// focus goes to the last item covered. A band that was never begun replaces.
    /// </summary>
    public void Band(IEnumerable<int> hits)
    {
        ArgumentNullException.ThrowIfNull(hits);

        var start = _bandBase ?? [];
        _selected.Clear();

        if (_bandMode != BandMode.Replace)
        {
            _selected.UnionWith(start);
        }

        string? last = null;

        foreach (var i in hits)
        {
            if (!InRange(i))
            {
                continue;
            }

            var id = _order[i];

            if (_bandMode == BandMode.Toggle && start.Contains(id))
            {
                _selected.Remove(id);
            }
            else
            {
                _selected.Add(id);
            }

            last = id;
        }

        if (last is not null)
        {
            Focus = last;
            Anchor ??= last;
        }
    }

    /// <summary>The band was let go: what it selected stays.</summary>
    public void EndBand()
    {
        _bandBase = null;
        _bandMode = BandMode.Replace;
    }

    /// <summary>[S-6620] Escape, or a click on the empty space: nothing selected, nothing focused. Returns whether anything was.</summary>
    public bool Clear()
    {
        var had = _selected.Count > 0 || Focus is not null;
        _selected.Clear();
        Focus = null;
        Anchor = null;
        Cues = false;
        _deferred = null;
        _bandBase = null;
        return had;
    }

    /// <summary>
    /// [S-6626] The widget lost the keyboard to another window or widget: the selection stays (drawn
    /// inactive, [S-6627]), the focus cue goes, a pending press is forgotten.
    /// </summary>
    public void Deactivate()
    {
        Cues = false;
        _deferred = null;
        _bandBase = null;
    }

    /// <summary>Select exactly <paramref name="id"/> and focus it (a new folder about to be renamed).</summary>
    public bool SelectOnly(string id)
    {
        if (!_index.ContainsKey(id))
        {
            return false;
        }

        _selected.Clear();
        _selected.Add(id);
        Focus = id;
        Anchor = id;
        return true;
    }

    /// <summary>
    /// [S-6623] The selection keys, the same in every widget that selects items: arrows (Shift extends,
    /// Ctrl moves the focus only), Home/End, Page Up/Down, Space and Ctrl+Space toggle the focused item,
    /// Ctrl+A selects all. <paramref name="shift"/> is whether Shift is held now (the lease hands
    /// Shift+Up as Up). Returns whether the key was one of these and was used; anything else
    /// (Enter, Delete, F2, Escape, the Menu key) is the widget's own.
    /// </summary>
    public bool Key(WidgetKey key, bool shift, int columns, int pageRows) => key switch
    {
        WidgetKey.Up => Move(SelectionMove.Up, columns, pageRows, shift),
        WidgetKey.Down => Move(SelectionMove.Down, columns, pageRows, shift),
        WidgetKey.Left => Move(SelectionMove.Left, columns, pageRows),
        WidgetKey.Right => Move(SelectionMove.Right, columns, pageRows),
        WidgetKey.ShiftLeft => Move(SelectionMove.Left, columns, pageRows, extend: true),
        WidgetKey.ShiftRight => Move(SelectionMove.Right, columns, pageRows, extend: true),
        WidgetKey.Home => Move(SelectionMove.Home, columns, pageRows, shift),
        WidgetKey.End => Move(SelectionMove.End, columns, pageRows, shift),
        WidgetKey.PageUp => Move(SelectionMove.PageUp, columns, pageRows, shift),
        WidgetKey.PageDown => Move(SelectionMove.PageDown, columns, pageRows, shift),
        WidgetKey.FocusUp => Move(SelectionMove.Up, columns, pageRows, focusOnly: true),
        WidgetKey.FocusDown => Move(SelectionMove.Down, columns, pageRows, focusOnly: true),
        WidgetKey.FocusLeft => Move(SelectionMove.Left, columns, pageRows, focusOnly: true),
        WidgetKey.FocusRight => Move(SelectionMove.Right, columns, pageRows, focusOnly: true),
        WidgetKey.Space or WidgetKey.ToggleSelection => ToggleFocus(),
        WidgetKey.SelectAll => SelectAllUsed(),
        _ => false,
    };

    private bool SelectAllUsed()
    {
        if (_order.Count == 0)
        {
            return false;
        }

        SelectAll();
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
