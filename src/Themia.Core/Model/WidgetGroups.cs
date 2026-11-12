using Themia.Core.Geometry;

namespace Themia.Core.Model;

/// <summary>
/// [S-2450]..[S-2456] Groups: an optional lock over two or more widgets of one view. A group is not a
/// card: its members stay ordinary widgets (they touch and join as any cards do, [S-2240]), and the
/// group only makes them select, move, copy, cut, delete and layer as one. Pure functions over a view.
/// <para>
/// A group of fewer than two members does not exist. <see cref="Repair"/> is the one rule that says
/// so: load validation logs what it dissolved, and every mutation runs it too (<see cref="Settle"/>), so
/// deleting, cutting or ungrouping all but one member leaves that one a lone widget.
/// </para>
/// </summary>
public static class WidgetGroups
{
    /// <summary>[S-2450] The group a widget is in, or null for a lone widget (or one not in the view).</summary>
    public static WidgetGroup? GroupOf(ViewState view, string? widgetId)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view.Find(widgetId) is { Group: { } groupId } ? view.FindGroup(groupId) : null;
    }

    /// <summary>[S-2450] A group's members, in view (paint) order.</summary>
    public static List<WidgetInstance> Members(ViewState view, string? groupId)
    {
        ArgumentNullException.ThrowIfNull(view);
        return groupId is null ? [] : [.. view.Widgets.Where(w => string.Equals(w.Group, groupId, StringComparison.Ordinal))];
    }

    /// <summary>[S-2451] The unit a widget is selected with: its group's members, or itself alone. Empty when it is not in the view.</summary>
    public static List<string> Unit(ViewState view, string? widgetId)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (view.Find(widgetId) is not { } widget)
        {
            return [];
        }

        return GroupOf(view, widget.Id) is { } group ? [.. Members(view, group.Id).Select(w => w.Id)] : [widget.Id];
    }

    /// <summary>[S-2451] The ids grown to whole units (a member brings its group), in view order. Ids not in the view are dropped.</summary>
    public static List<string> Units(ViewState view, IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(ids);

        var picked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            picked.UnionWith(Unit(view, id));
        }

        return [.. view.Widgets.Where(w => picked.Contains(w.Id)).Select(w => w.Id)];
    }

    /// <summary>
    /// [S-2452] The groups all of whose members are among <paramref name="ids"/>: the groups a selection
    /// holds whole, which draw one frame round their members ([S-2453]) and travel whole in a copy.
    /// </summary>
    public static List<WidgetGroup> Whole(ViewState view, IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(ids);

        var picked = new HashSet<string>(ids, StringComparer.Ordinal);
        return [.. view.Groups.Where(g => Members(view, g.Id) is { Count: > 0 } members && members.TrueForAll(m => picked.Contains(m.Id)))];
    }

    /// <summary>[S-2453] The box round a group's members, or null for a group with none in the view.</summary>
    public static FracRect? Bounds(ViewState view, string groupId)
    {
        var members = Members(view, groupId);
        return members.Count == 0 ? null : BlockGeometry.Bounds([.. members.Select(m => m.Rect)]);
    }

    /// <summary>
    /// [S-2454] Whether <see cref="Group"/> would make a group: the ids grown to whole units hold two
    /// or more widgets and are not already exactly one group.
    /// </summary>
    public static bool CanGroup(ViewState view, IEnumerable<string> ids)
    {
        var units = Units(view, ids);
        if (units.Count < 2)
        {
            return false;
        }

        var groups = units.Select(id => view.Find(id)!.Group).Distinct(StringComparer.Ordinal).ToList();
        return groups.Count != 1 || groups[0] is null;
    }

    /// <summary>
    /// [S-2454] Ctrl+G: one new group of the widgets (a member brings its whole group). Groups among
    /// them merge into it, and it takes the name of the first of them (in view order) that had one.
    /// Returns the new group, or null when there is nothing to group (<see cref="CanGroup"/>).
    /// </summary>
    public static WidgetGroup? Group(ViewState view, IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (!CanGroup(view, ids))
        {
            return null;
        }

        var units = Units(view, ids);
        var merged = units
            .Select(id => view.Find(id)!.Group)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Select(view.FindGroup)
            .OfType<WidgetGroup>()
            .ToList();

        var group = WidgetGroup.Create(merged.Find(g => !string.IsNullOrWhiteSpace(g.Name))?.Name);
        view.Groups.RemoveAll(merged.Contains);
        view.Groups.Add(group);
        foreach (var id in units)
        {
            view.Find(id)!.Group = group.Id;
        }

        return group;
    }

    /// <summary>[S-2454] Whether any of the widgets is in a group, so Ungroup has something to do.</summary>
    public static bool CanUngroup(ViewState view, IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(ids);
        return ids.Any(id => GroupOf(view, id) is not null);
    }

    /// <summary>[S-2454] Ctrl+Shift+G: every group any of the widgets is in dissolves; its members stay where they are. Returns whether anything changed.</summary>
    public static bool Ungroup(ViewState view, IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(ids);

        var groups = ids.Select(id => GroupOf(view, id)).OfType<WidgetGroup>().Distinct().ToList();
        foreach (var group in groups)
        {
            foreach (var member in Members(view, group.Id))
            {
                member.Group = null;
            }

            view.Groups.Remove(group);
        }

        return groups.Count > 0;
    }

    /// <summary>
    /// [S-2454] "Remove from group" on an entered member: it alone leaves; a group left with one member
    /// dissolves (<see cref="Settle"/>). Returns whether anything changed.
    /// </summary>
    public static bool RemoveFromGroup(ViewState view, string widgetId)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (view.Find(widgetId) is not { Group: not null } widget)
        {
            return false;
        }

        widget.Group = null;
        Repair(view);
        return true;
    }

    /// <summary>
    /// [S-2450] The groups' invariants over one view, repaired in place; says what it repaired, for the
    /// load log. A group with a blank or repeated id is dropped; a blank name is none; a widget naming a
    /// group the view does not have is lone; a group of fewer than two members dissolves.
    /// </summary>
    public static List<string> Repair(ViewState view)
    {
        ArgumentNullException.ThrowIfNull(view);

        var repairs = new List<string>();
        view.Groups ??= [];

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < view.Groups.Count; i++)
        {
            var group = view.Groups[i];
            if (group is null || string.IsNullOrWhiteSpace(group.Id) || !seen.Add(group.Id))
            {
                view.Groups.RemoveAt(i--);
                repairs.Add($"view '{view.Id}': dropped a group with a missing or repeated id");
                continue;
            }

            if (group.Name is { } name && string.IsNullOrWhiteSpace(name))
            {
                group.Name = null;
                repairs.Add($"view '{view.Id}': group '{group.Id}' had a blank name");
            }
            else if (group.Name is { } named && named != named.Trim())
            {
                group.Name = named.Trim();
            }
        }

        foreach (var widget in view.Widgets)
        {
            if (widget.Group is { } groupId && !seen.Contains(groupId))
            {
                widget.Group = null;
                repairs.Add($"widget '{widget.Id}' named a group view '{view.Id}' does not have");
            }
        }

        foreach (var group in view.Groups.ToList())
        {
            var members = Members(view, group.Id);
            if (members.Count >= 2)
            {
                continue;
            }

            foreach (var member in members)
            {
                member.Group = null;
            }

            view.Groups.Remove(group);
            repairs.Add($"view '{view.Id}': group '{group.Id}' had fewer than two widgets and was dissolved");
        }

        return repairs;
    }

    /// <summary>
    /// [S-2450] After any change: <see cref="Repair"/> over every view, silently. The one place that
    /// dissolves a group a delete, a cut or a move left with a single member.
    /// </summary>
    public static void Settle(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (var view in state.Views)
        {
            // ignore: what a mutation leaves behind is repaired, not reported; the load path logs its own
            _ = Repair(view);
        }
    }

    /// <summary>
    /// [S-2455] Fresh ids for copies of whole groups: each group id of <paramref name="groups"/> mapped
    /// to a new id, the name kept. A copy of a group is a new group.
    /// </summary>
    public static Dictionary<string, WidgetGroup> FreshCopies(IEnumerable<WidgetGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        return groups.ToDictionary(g => g.Id, g => new WidgetGroup { Id = Guid.NewGuid().ToString("n"), Name = g.Name }, StringComparer.Ordinal);
    }
}
