using Themia.Core.Geometry;

namespace Themia.Core.Model;

/// <summary>
/// [S-1580]..[S-1583] The widgets edit mode acts on: one or many, always from <b>one</b> view.
/// <para>
/// One view, because everything a selection does — drag together, copy, delete, layer — is about
/// widgets that are drawn together on one display. Picking a widget on a display that shows another
/// view starts a new selection there instead of mixing the two.
/// </para>
/// <para>
/// Ordered by when each widget was picked; the last one is the <see cref="Primary"/>, the widget a
/// single-widget command (the picker's "Add to", Settings) means.
/// </para>
/// </summary>
public sealed class WidgetSelection
{
    private readonly List<string> _ids = [];

    /// <summary>The view the selected widgets are in; null when nothing is selected.</summary>
    public string? ViewId { get; private set; }

    /// <summary>The selected ids, in the order they were picked.</summary>
    public IReadOnlyList<string> Ids => _ids;

    /// <summary>The most recently picked widget, or null.</summary>
    public string? Primary => _ids.Count > 0 ? _ids[^1] : null;

    public int Count => _ids.Count;

    public bool IsEmpty => _ids.Count == 0;

    public bool Contains(string? id) => id is not null && _ids.Contains(id, StringComparer.Ordinal);

    /// <summary>[S-1580] Select exactly one widget, or nothing for a null id. Returns whether anything changed.</summary>
    public bool Set(string? viewId, string? id)
    {
        if (id is null || viewId is null)
        {
            return Clear();
        }

        return Replace(viewId, [id]);
    }

    /// <summary>[S-1580] Ctrl+click: add the widget, or take it out if it is in. Another view starts over.</summary>
    public bool Toggle(string viewId, string id)
    {
        ArgumentNullException.ThrowIfNull(viewId);
        ArgumentNullException.ThrowIfNull(id);

        if (!string.Equals(viewId, ViewId, StringComparison.Ordinal))
        {
            return Replace(viewId, [id]);
        }

        if (_ids.Remove(id))
        {
            if (_ids.Count == 0)
            {
                ViewId = null;
            }

            return true;
        }

        _ids.Add(id);
        return true;
    }

    /// <summary>Select exactly these widgets of one view (duplicates dropped, order kept).</summary>
    public bool Replace(string viewId, IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(viewId);
        ArgumentNullException.ThrowIfNull(ids);

        var next = ids.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();
        if (next.Count == 0)
        {
            return Clear();
        }

        if (string.Equals(viewId, ViewId, StringComparison.Ordinal) && next.SequenceEqual(_ids, StringComparer.Ordinal))
        {
            return false;
        }

        _ids.Clear();
        _ids.AddRange(next);
        ViewId = viewId;
        return true;
    }

    public bool Clear()
    {
        if (_ids.Count == 0 && ViewId is null)
        {
            return false;
        }

        _ids.Clear();
        ViewId = null;
        return true;
    }

    /// <summary>
    /// [S-1593] What a press on a widget selects at once: a press on a selected widget keeps the selection
    /// (the drag that may follow carries all of it); anything else selects the widget alone. Returns whether
    /// the selection changed; a press that kept it lets <see cref="Click"/> decide on release.
    /// </summary>
    public bool Press(ViewState view, string id)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(id);
        return !Keeps(view, id) && Replace(view.Id, [id]);
    }

    /// <summary>[S-1593] Whether a press on <paramref name="id"/> keeps the selection: it is selected already.</summary>
    public bool Keeps(ViewState view, string id)
    {
        ArgumentNullException.ThrowIfNull(view);
        return SameView(view) && Contains(id);
    }

    /// <summary>
    /// [S-1593] A click (released without moving) on a widget whose press kept the selection: the selection
    /// narrows to that widget. Returns whether anything changed.
    /// </summary>
    public bool Click(ViewState view, string id)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(id);
        return Replace(view.Id, [id]);
    }

    /// <summary>
    /// [S-1973] Tab in edit mode: the widget after the primary in reading order, alone; Shift+Tab goes back.
    /// False when there is nothing to land on.
    /// </summary>
    public bool Cycle(ViewState view, bool backwards, IReadOnlySet<string>? hidden = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        return SelectionRules.Cycle(view, SameView(view) ? Primary : null, backwards, hidden) is { } next && Replace(view.Id, [next]);
    }

    private bool SameView(ViewState view) => string.Equals(view.Id, ViewId, StringComparison.Ordinal);

    /// <summary>
    /// [S-1583] Drop what no longer exists in the selection's view — deleted, or moved to another
    /// view. A selection whose view is gone is cleared. Returns whether anything changed.
    /// </summary>
    public bool Prune(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (ViewId is null)
        {
            return false;
        }

        if (state.FindView(ViewId) is not { } view)
        {
            return Clear();
        }

        var removed = _ids.RemoveAll(id => view.Find(id) is null);
        if (_ids.Count == 0)
        {
            ViewId = null;
        }

        return removed > 0;
    }
}

/// <summary>
/// [S-1581]/[S-1582] Which widgets a gesture picks, and which of the picked ones a command acts on.
/// Pure functions over a view.
/// </summary>
public static class SelectionRules
{
    /// <summary>
    /// [S-31b] [S-11877] [S-11881] [S-11889] Whether a widget draws its marks: always in edit mode; on the locked
    /// desktop while a block moves (<paramref name="moveAlpha"/>: its strength, 1 moving, the local grid's near where
    /// the block lands), and while it is selected (its four corners) unless it is a desktop <paramref name="icon"/>: a
    /// selected, hovered or highlighted icon shows its highlight alone (owner, 2026-11-16: "when highlighting a folder it
    /// should not show the corners"), its corners only where a widget's are about placing (edit mode, a move).
    /// </summary>
    public static bool MarksShown(bool editing, bool selected, double? moveAlpha, bool icon)
        => editing || moveAlpha is not null || (selected && !icon);

    /// <summary>
    /// [S-1581] The widgets a rubber band touches: every widget whose cell overlaps it by any area,
    /// in view order. <paramref name="hidden"/> are left out (cards that draw nothing). A band of no
    /// area touches nothing.
    /// </summary>
    public static List<string> Lasso(ViewState view, FracRect band, IReadOnlySet<string>? hidden = null)
    {
        ArgumentNullException.ThrowIfNull(view);

        var hits = new List<string>();
        if (!band.IsFinite || band.W <= 0 || band.H <= 0)
        {
            return hits;
        }

        foreach (var widget in view.Widgets)
        {
            if (hidden?.Contains(widget.Id) == true)
            {
                continue;
            }

            var r = widget.Rect;
            if (r.X < band.Right && band.X < r.Right && r.Y < band.Bottom && band.Y < r.Bottom)
            {
                hits.Add(widget.Id);
            }
        }

        return hits;
    }

    /// <summary>
    /// [S-1597] Which card a press at (<paramref name="x"/>, <paramref name="y"/>) goes to in edit mode. A selected
    /// card's handles and body come first (owner, 2026-11-17: "when a widget is selected even if it clips behind
    /// another one i should be able to grab all its edges and corners to resize it or move it"): the topmost
    /// selected card whose box, grown by <paramref name="grip"/> on every side (its handles stand across its
    /// edges), holds the point; else the topmost card whose box holds it; null over no card. The drawing order
    /// stays: <paramref name="cards"/> in paint order, the last on top.
    /// </summary>
    public static string? PressTarget(IReadOnlyList<(string Id, PixelRect Box)> cards, IReadOnlySet<string> selected, double x, double y, double grip)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(selected);

        var reach = double.IsFinite(grip) && grip > 0 ? grip : 0;
        static bool Holds(PixelRect box, double x, double y, double grow)
            => x >= box.X - grow && x < box.X + box.Width + grow && y >= box.Y - grow && y < box.Y + box.Height + grow;

        for (var i = cards.Count - 1; i >= 0; i--)
        {
            if (selected.Contains(cards[i].Id) && Holds(cards[i].Box, x, y, reach))
            {
                return cards[i].Id;
            }
        }

        for (var i = cards.Count - 1; i >= 0; i--)
        {
            if (Holds(cards[i].Box, x, y, 0))
            {
                return cards[i].Id;
            }
        }

        return null;
    }

    /// <summary>[S-1582] Ctrl+A: every widget of the view, less the hidden ones.</summary>
    public static List<string> All(ViewState view, IReadOnlySet<string>? hidden = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        return [.. view.Widgets.Where(w => hidden?.Contains(w.Id) != true).Select(w => w.Id)];
    }

    /// <summary>
    /// [S-1973] Tab in edit mode: the widget after <paramref name="currentId"/> in reading order (top
    /// edge, then left edge, then view order), wrapping at the end. From nothing (or a widget that is
    /// not in the view) Tab lands on the first, Shift+Tab on the last. Hidden widgets are skipped;
    /// null when the view has none to land on.
    /// </summary>
    public static string? Cycle(ViewState view, string? currentId, bool backwards, IReadOnlySet<string>? hidden = null)
    {
        ArgumentNullException.ThrowIfNull(view);

        var order = view.Widgets
            .Select((w, index) => (Widget: w, Index: index))
            .Where(x => hidden?.Contains(x.Widget.Id) != true)
            .OrderBy(x => x.Widget.Y)
            .ThenBy(x => x.Widget.X)
            .ThenBy(x => x.Index)
            .Select(x => x.Widget.Id)
            .ToList();

        if (order.Count == 0)
        {
            return null;
        }

        var at = currentId is null ? -1 : order.IndexOf(currentId);
        if (at < 0)
        {
            return backwards ? order[^1] : order[0];
        }

        var next = backwards ? (at - 1 + order.Count) % order.Count : (at + 1) % order.Count;
        return order[next];
    }

    /// <summary>
    /// [S-1584]/[S-1588] The selected widgets that are in the view, in view order: what a drag moves
    /// and what a copy or move takes. Ids not in the view are ignored.
    /// </summary>
    public static List<string> InView(ViewState view, IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(ids);

        var picked = new HashSet<string>(ids, StringComparer.Ordinal);
        return [.. view.Widgets.Where(w => picked.Contains(w.Id)).Select(w => w.Id)];
    }
}
