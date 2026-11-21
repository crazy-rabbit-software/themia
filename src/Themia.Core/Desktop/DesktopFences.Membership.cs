using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>
/// [S-13000] [S-13001] [S-13042] The fences' membership: which fence of a view holds which desktop item and its icon, items
/// moved into a fence or out of every fence, renamed or forgotten, and a loaded state's fences made whole.
/// </summary>
public static partial class DesktopFences
{
    /// <summary>[S-13001] Every key a fence of <paramref name="view"/> holds.</summary>
    public static HashSet<string> Held(ViewState view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var held = new HashSet<string>(DesktopItemKeys.Comparer);
        foreach (var on in On(view))
        {
            held.UnionWith(on.Fence.Items);
        }

        return held;
    }

    /// <summary>[S-13001] The fence of <paramref name="view"/> that holds <paramref name="key"/>, or null.</summary>
    public static FenceOn? Holding(ViewState view, string key)
    {
        ArgumentNullException.ThrowIfNull(view);
        foreach (var on in On(view))
        {
            if (on.Fence.Items.Contains(key, DesktopItemKeys.Comparer))
            {
                return on;
            }
        }

        return null;
    }

    /// <summary>
    /// [S-13041] [S-13047] The fence the widget <paramref name="widgetId"/> of <paramref name="view"/> stands for to the desktop's
    /// own icons: that fence itself, or the fence that holds that icon (its cell); null for any other widget. A move or a drag of
    /// the desktop's icons over either lands on the fence's cells, never in a fenced icon as a drop target.
    /// </summary>
    public static FenceOn? FenceAt(ViewState view, string widgetId)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (view.Find(widgetId) is { Config: IHoldsDesktopItems fence } widget)
        {
            return new FenceOn(widget, fence);
        }

        return Members(view).TryGetValue(widgetId, out var member) ? member.Fence : null;
    }

    /// <summary>The fence widget <paramref name="fenceId"/> and its view, or null.</summary>
    public static (ViewState View, FenceOn Fence)? Find(AppState state, string fenceId)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (var view in state.Views)
        {
            foreach (var widget in view.Widgets)
            {
                if (string.Equals(widget.Id, fenceId, StringComparison.Ordinal) && widget.Config is IHoldsDesktopItems fence)
                {
                    return (view, new FenceOn(widget, fence));
                }
            }
        }

        return null;
    }

    /// <summary>
    /// [S-13005] [S-13008] <paramref name="keys"/> go into the fence <paramref name="fenceId"/> of <paramref name="view"/>
    /// before item <paramref name="index"/> of it (clamped; the order of <paramref name="keys"/> kept): each leaves the free
    /// desktop of that view and every other fence there, and one already in this fence moves to the new place. [S-13042]
    /// Its icon is its item widget on that view, kept (the fence lays it on its cell, <see cref="Lay"/>); one with no icon
    /// there gets one from <paramref name="icon"/> (none given: it is not held, as <see cref="Lay"/> would drop it). Own
    /// icons, shell items' keys aside, are never keys here (the caller passes desktop items only). Returns how many keys the
    /// fence holds that it did not hold before.
    /// </summary>
    public static int MoveIn(ViewState view, string fenceId, IReadOnlyList<string> keys, int index, Func<string, WidgetInstance?>? icon = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(keys);
        var target = On(view).FirstOrDefault(on => string.Equals(on.Widget.Id, fenceId, StringComparison.Ordinal));
        if (target.Fence is null || target.Fence.Source == FenceSource.Search)
        {
            return 0;
        }

        var moving = keys.Where(DesktopItemKeys.IsValid).Distinct(DesktopItemKeys.Comparer).ToList();
        if (moving.Count == 0)
        {
            return 0;
        }

        var set = new HashSet<string>(moving, DesktopItemKeys.Comparer);
        var held = new HashSet<string>(target.Fence.Items, DesktopItemKeys.Comparer);

        // The place is counted among the items that stay, so a move inside the fence lands where it was pointed at.
        var items = target.Fence.Items;
        var at = Math.Clamp(index, 0, items.Count);
        var stays = items.Take(at).Count(key => !set.Contains(key));

        foreach (var on in On(view))
        {
            on.Fence.Items.RemoveAll(set.Contains);
        }

        // [S-13042] Each item's own icon becomes the fence's; one with none on this view gets one, or stays out.
        var icons = IconsByKey(view);
        foreach (var key in moving.ToList())
        {
            if (icons.ContainsKey(key))
            {
                continue;
            }

            if (icon?.Invoke(key) is { } made)
            {
                view.Widgets.Add(made);
                icons[key] = made;
            }
            else
            {
                moving.Remove(key);
            }
        }

        target.Fence.Items.InsertRange(Math.Clamp(stays, 0, target.Fence.Items.Count), moving);
        return moving.Count(key => !held.Contains(key));
    }

    /// <summary>[S-13006] [S-13009] <paramref name="keys"/> leave every fence of <paramref name="view"/>. Returns the keys that left one.</summary>
    public static IReadOnlyList<string> TakeOut(ViewState view, IEnumerable<string> keys)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(keys);
        var set = new HashSet<string>(keys, DesktopItemKeys.Comparer);
        var left = new List<string>();
        foreach (var on in On(view))
        {
            left.AddRange(on.Fence.Items.Where(set.Contains));
            on.Fence.Items.RemoveAll(set.Contains);
        }

        return left;
    }

    /// <summary>[S-13003] A rename heard from the shell: the key is rewritten in every fence that holds the old one, in place.</summary>
    public static int Rename(AppState state, string before, string after)
    {
        ArgumentNullException.ThrowIfNull(state);
        var count = 0;
        foreach (var on in state.Views.SelectMany(On))
        {
            var items = on.Fence.Items;
            for (var i = 0; i < items.Count; i++)
            {
                if (DesktopItemKeys.Comparer.Equals(items[i], before))
                {
                    items[i] = after;
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>[S-13003] Items gone from the desktop leave every fence. Returns how many entries went.</summary>
    public static int Forget(AppState state, IReadOnlySet<string> gone)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gone);
        return gone.Count == 0 ? 0 : state.Views.SelectMany(On).Sum(on => on.Fence.Items.RemoveAll(gone.Contains));
    }

    /// <summary>Every key any fence of any view holds.</summary>
    public static IEnumerable<string> AllHeld(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Views.SelectMany(On).SelectMany(on => on.Fence.Items);
    }

    /// <summary>[S-13015] Each desktop item's icon on <paramref name="view"/> by its key (the first, the one a view keeps).</summary>
    private static Dictionary<string, WidgetInstance> IconsByKey(ViewState view)
    {
        var icons = new Dictionary<string, WidgetInstance>(DesktopItemKeys.Comparer);
        foreach (var widget in view.Widgets)
        {
            if (widget.Config is IDesktopItemConfig { Item.Length: > 0 } config)
            {
                icons.TryAdd(config.Item, widget);
            }
        }

        return icons;
    }

    /// <summary>
    /// [S-13042] The icons of <paramref name="view"/> its fences hold, by widget id: the fence and the item's place in its
    /// order. Every other item widget of the view is a free icon.
    /// </summary>
    public static Dictionary<string, (FenceOn Fence, int Index)> Members(ViewState view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var icons = IconsByKey(view);
        var members = new Dictionary<string, (FenceOn, int)>(StringComparer.Ordinal);
        foreach (var on in On(view))
        {
            for (var i = 0; i < on.Fence.Items.Count; i++)
            {
                if (icons.TryGetValue(on.Fence.Items[i], out var icon))
                {
                    members.TryAdd(icon.Id, (on, i));
                }
            }
        }

        return members;
    }

    /// <summary>[S-13042] The free desktop icons of <paramref name="view"/> (an own icon too): every item widget no fence holds.</summary>
    public static List<WidgetInstance> FreeIcons(ViewState view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var members = Members(view);
        return [.. view.Widgets.Where(w => w.Config is IDesktopItemConfig && !members.ContainsKey(w.Id))];
    }

    /// <summary>
    /// [S-13002] Load: every fence's items made whole and no item in two places of one view. In each fence an invalid key
    /// (<see cref="DesktopItemKeys.IsValid"/>) and a repeat go; an item in two fences of one view stays in the first (the
    /// view's order) and leaves the others. Its rules are repaired (<see cref="FenceRules.Repair"/>), a negative scroll is
    /// the top. Each repair is said. An item held with no icon on its view leaves its fence when it is laid
    /// (<see cref="Lay"/>, the one rule); items no longer on the desktop are the reconcile's ([S-13003]): load has no listing.
    /// </summary>
    public static IReadOnlyList<string> Repair(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var repairs = new List<string>();
        foreach (var view in state.Views)
        {
            var seen = new HashSet<string>(DesktopItemKeys.Comparer);
            foreach (var on in On(view).ToList())
            {
                on.Fence.Items ??= [];
                var kept = new List<string>(on.Fence.Items.Count);
                var invalid = 0;
                var twice = 0;
                foreach (var key in on.Fence.Items)
                {
                    if (!DesktopItemKeys.IsValid(key))
                    {
                        invalid++;
                    }
                    else if (!seen.Add(key))
                    {
                        twice++;
                    }
                    else
                    {
                        kept.Add(key);
                    }
                }

                if (invalid + twice > 0)
                {
                    on.Fence.Items = kept;
                    repairs.Add($"fence '{on.Widget.Id}' on view '{view.Id}' held {invalid} invalid and {twice} repeated item(s) (an item in two fences stays in the first); dropped");
                }

                if (on.Fence.Rules is { } rules)
                {
                    repairs.AddRange(FenceRules.Repair(rules).Select(r => $"fence '{on.Widget.Id}': {r}"));
                }

                if (on.Fence.OpenHeight is < 0 or > 1 || !double.IsFinite(on.Fence.OpenHeight))
                {
                    on.Fence.OpenHeight = 0;
                    repairs.Add($"fence '{on.Widget.Id}' had an unusable height to unroll to; it unrolls to its own");
                }

                if (on.Fence.Scroll < 0)
                {
                    on.Fence.Scroll = 0;
                    repairs.Add($"fence '{on.Widget.Id}' had a negative scroll; it shows its top");
                }

                if (on.Fence.Sort is { } sort && !Enum.IsDefined(sort))
                {
                    on.Fence.Sort = null;
                    repairs.Add($"fence '{on.Widget.Id}' had an unknown sort; it keeps its order");
                }
            }
        }

        return repairs;
    }
}
