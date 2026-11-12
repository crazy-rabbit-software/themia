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

    /// <summary>
    /// [S-2451] The group the selection is inside ("entered"): one of its members is selected alone, shows
    /// its own handles and moves on its own. Null when the selection is units (lone widgets and whole groups).
    /// </summary>
    public string? Entered { get; private set; }

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

        Entered = null;
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

        if (string.Equals(viewId, ViewId, StringComparison.Ordinal) && next.SequenceEqual(_ids, StringComparer.Ordinal) && Entered is null)
        {
            return false;
        }

        _ids.Clear();
        _ids.AddRange(next);
        ViewId = viewId;
        Entered = null;
        return true;
    }

    /// <summary>[S-2451] Enter a group: <paramref name="memberId"/> alone is selected, inside <paramref name="groupId"/>.</summary>
    public bool Enter(string viewId, string groupId, string memberId)
    {
        ArgumentNullException.ThrowIfNull(viewId);
        ArgumentNullException.ThrowIfNull(groupId);
        ArgumentNullException.ThrowIfNull(memberId);

        if (string.Equals(viewId, ViewId, StringComparison.Ordinal) && string.Equals(Entered, groupId, StringComparison.Ordinal)
            && _ids.Count == 1 && string.Equals(_ids[0], memberId, StringComparison.Ordinal))
        {
            return false;
        }

        _ids.Clear();
        _ids.Add(memberId);
        ViewId = viewId;
        Entered = groupId;
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
        Entered = null;
        return true;
    }

    /// <summary>
    /// [S-2451] What a press on a widget selects at once. Inside an entered group a press on another
    /// member enters that one; a press on a widget whose whole unit is selected keeps the selection (the
    /// drag that may follow carries all of it, [S-1593]); anything else selects the widget's unit: its
    /// whole group, or itself. Returns whether the selection changed; a press that kept it lets
    /// <see cref="Click"/> decide on release.
    /// </summary>
    public bool Press(ViewState view, string id)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(id);

        if (Entered is { } entered && SameView(view) && WidgetGroups.GroupOf(view, id)?.Id == entered)
        {
            return Enter(view.Id, entered, id);
        }

        if (Keeps(view, id))
        {
            return false;
        }

        return Replace(view.Id, WidgetGroups.Unit(view, id));
    }

    /// <summary>[S-2451] Whether a press on <paramref name="id"/> keeps the selection: its whole unit is in it already.</summary>
    public bool Keeps(ViewState view, string id)
    {
        ArgumentNullException.ThrowIfNull(view);
        return SameView(view) && Contains(id) && (Entered is not null || WidgetGroups.Unit(view, id).TrueForAll(Contains));
    }

    /// <summary>
    /// [S-2451] A click (released without moving, [S-1593]) on a widget whose press kept the selection: on a
    /// member of the group that is selected alone, it enters the group with that member; on a widget of a
    /// larger selection, it narrows the selection to the widget's unit. Returns whether anything changed.
    /// </summary>
    public bool Click(ViewState view, string id)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(id);

        if (Entered is not null)
        {
            return false;
        }

        var unit = WidgetGroups.Unit(view, id);
        if (WidgetGroups.GroupOf(view, id) is { } group && SameView(view) && _ids.Count == unit.Count && unit.TrueForAll(Contains))
        {
            return Enter(view.Id, group.Id, id);
        }

        return Replace(view.Id, unit);
    }

    /// <summary>
    /// [S-2451] A press on a resize handle: the card alone. Handles show only on a lone widget or an
    /// entered member, so inside an entered group this keeps it entered.
    /// </summary>
    public bool PressHandle(ViewState view, string id)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(id);

        return Entered is { } entered && SameView(view) && WidgetGroups.GroupOf(view, id)?.Id == entered
            ? Enter(view.Id, entered, id)
            : Replace(view.Id, [id]);
    }

    /// <summary>[S-2451] Ctrl+click: the widget's unit (its whole group) in or out of the selection. An entered group is left first.</summary>
    public bool ToggleUnit(ViewState view, string id)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(id);

        var unit = WidgetGroups.Unit(view, id);
        if (unit.Count == 0)
        {
            return false;
        }

        if (!SameView(view))
        {
            return Replace(view.Id, unit);
        }

        var current = Entered is { } entered ? WidgetGroups.Members(view, entered).Select(w => w.Id).ToList() : [.. _ids];
        var next = unit.TrueForAll(current.Contains)
            ? current.Where(i => !unit.Contains(i)).ToList()
            : [.. current, .. unit.Where(i => !current.Contains(i))];
        return next.Count == 0 ? Clear() : Replace(view.Id, next);
    }

    /// <summary>[S-2451] Escape steps out one level: an entered member to its whole group, anything else to nothing.</summary>
    public bool StepOut(ViewState? view)
    {
        if (Entered is { } entered && view is not null && SameView(view) && WidgetGroups.Members(view, entered) is { Count: > 0 } members)
        {
            return Replace(view.Id, members.Select(m => m.Id));
        }

        return Clear();
    }

    /// <summary>
    /// [S-2451] Tab in edit mode: the next unit in reading order (a group by its top-left corner), or,
    /// inside an entered group, the next member. Shift+Tab goes back. False when there is nothing to land on.
    /// </summary>
    public bool Cycle(ViewState view, bool backwards, IReadOnlySet<string>? hidden = null)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (Entered is { } entered && SameView(view))
        {
            var members = new ViewState { Id = view.Id, Name = view.Name, Widgets = WidgetGroups.Members(view, entered) };
            return SelectionRules.Cycle(members, Primary, backwards, hidden) is { } next && Enter(view.Id, entered, next);
        }

        var units = SelectionRules.UnitsInReadingOrder(view, hidden);
        if (units.Count == 0)
        {
            return false;
        }

        var at = SameView(view) && Primary is { } primary ? units.FindIndex(u => u.Contains(primary)) : -1;
        var to = at < 0
            ? (backwards ? units.Count - 1 : 0)
            : backwards ? (at - 1 + units.Count) % units.Count : (at + 1) % units.Count;
        return Replace(view.Id, units[to]);
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

        // [S-2451] An entered group that dissolved (or a member that left it) is entered no more.
        var left = Entered is { } entered && (_ids.Count == 0 || view.FindGroup(entered) is null
            || !_ids.TrueForAll(id => string.Equals(view.Find(id)?.Group, entered, StringComparison.Ordinal)));
        if (left)
        {
            Entered = null;
        }

        return removed > 0 || left;
    }
}

/// <summary>
/// [S-1581]/[S-1582] Which widgets a gesture picks, and which of the picked ones a command acts on.
/// Pure functions over a view.
/// </summary>
public static class SelectionRules
{
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

        // [S-2451] A band that touches one member takes its whole group.
        return WidgetGroups.Units(view, hits);
    }

    /// <summary>
    /// [S-2451] The view's units (a lone widget, or a group's members) in reading order: a unit by its
    /// top edge, then its left edge, then the first of it in view order. Hidden widgets are left out.
    /// </summary>
    public static List<List<string>> UnitsInReadingOrder(ViewState view, IReadOnlySet<string>? hidden = null)
    {
        ArgumentNullException.ThrowIfNull(view);

        var units = new List<(List<string> Ids, double Y, double X, int Index)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < view.Widgets.Count; i++)
        {
            var widget = view.Widgets[i];
            if (hidden?.Contains(widget.Id) == true || !seen.Add(widget.Group ?? "\0" + widget.Id))
            {
                continue;
            }

            var members = widget.Group is { } g
                ? WidgetGroups.Members(view, g).Where(m => hidden?.Contains(m.Id) != true).ToList()
                : [widget];
            units.Add(([.. members.Select(m => m.Id)], members.Min(m => m.Y), members.Min(m => m.X), i));
        }

        return [.. units.OrderBy(u => u.Y).ThenBy(u => u.X).ThenBy(u => u.Index).Select(u => u.Ids)];
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
