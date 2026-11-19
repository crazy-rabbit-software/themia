using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>[S-13000] One fence of a view: its widget and the config that holds its items.</summary>
public readonly record struct FenceOn(WidgetInstance Widget, IHoldsDesktopItems Fence);

/// <summary>
/// [S-13000]..[S-13014] The fences' rules, pure (the owner, 2026-11-19: "a group of icons with a header … build as a
/// widget that can be created like fences does it"). A fence is a widget whose config holds real desktop items by
/// their keys (<see cref="IHoldsDesktopItems"/>); it lives on one view, and on that view its items are shown in it and
/// never on the free desktop ([S-13001]). Which free icons a view shows (All, Chosen, None, [S-5525]) concerns the free
/// desktop alone: an item in a fence is always shown with its fence. Nothing here touches a file.
/// </summary>
public static class DesktopFences
{
    /// <summary>The fences of <paramref name="view"/>, in its widgets' order (the order a rule is looked for in).</summary>
    public static IEnumerable<FenceOn> On(ViewState view)
    {
        ArgumentNullException.ThrowIfNull(view);
        foreach (var widget in view.Widgets)
        {
            if (widget.Config is IHoldsDesktopItems fence)
            {
                yield return new FenceOn(widget, fence);
            }
        }
    }

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
    /// [S-13004] The fence of <paramref name="view"/> a new <paramref name="item"/> arrives in by its rules: the first, in
    /// the view's order, whose rules it matches (<see cref="FenceRules.Matches"/>); null when none takes it.
    /// </summary>
    public static FenceOn? Route(ViewState view, DesktopItem item)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(item);
        foreach (var on in On(view))
        {
            if (FenceRules.Matches(on.Fence.Rules, item))
            {
                return on;
            }
        }

        return null;
    }

    /// <summary>
    /// [S-13005] [S-13008] <paramref name="keys"/> go into the fence <paramref name="fenceId"/> of <paramref name="view"/>
    /// before item <paramref name="index"/> of it (clamped; the order of <paramref name="keys"/> kept): each leaves the free
    /// desktop of that view (its item widget goes) and every other fence there, and one already in this fence moves to the
    /// new place. Own icons, shell items' keys aside, are never keys here (the caller passes desktop items only). Returns
    /// how many keys the fence holds that it did not hold before.
    /// </summary>
    public static int MoveIn(ViewState view, string fenceId, IReadOnlyList<string> keys, int index)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(keys);
        var target = On(view).FirstOrDefault(on => string.Equals(on.Widget.Id, fenceId, StringComparison.Ordinal));
        if (target.Fence is null)
        {
            return 0;
        }

        var moving = keys.Where(DesktopItemKeys.IsValid).Distinct(DesktopItemKeys.Comparer).ToList();
        if (moving.Count == 0)
        {
            return 0;
        }

        var set = new HashSet<string>(moving, DesktopItemKeys.Comparer);
        var before = target.Fence.Items.Count(set.Contains);

        // The place is counted among the items that stay, so a move inside the fence lands where it was pointed at.
        var items = target.Fence.Items;
        var at = Math.Clamp(index, 0, items.Count);
        var stays = items.Take(at).Count(key => !set.Contains(key));

        view.Widgets.RemoveAll(w => w.Config is IDesktopItemConfig { Item.Length: > 0 } c && set.Contains(c.Item));
        foreach (var on in On(view))
        {
            on.Fence.Items.RemoveAll(set.Contains);
        }

        target.Fence.Items.InsertRange(Math.Clamp(stays, 0, target.Fence.Items.Count), moving);
        return moving.Count - before;
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

    /// <summary>
    /// [S-13006] [S-13009] Lay <paramref name="items"/> on the free desktop of <paramref name="view"/> by the widget grid's
    /// arrival rule ([S-11876], [S-11887]): centred on (<paramref name="x"/>, <paramref name="y"/>) or the free spot nearest
    /// it, one after another, else (no point) at the first free spots in reading order; never on a widget or another icon.
    /// Returns the items that found no spot (they stay off the free desktop; Add widget lists them).
    /// </summary>
    public static IReadOnlyList<DesktopItem> LayFree(
        ViewState view,
        IReadOnlyList<DesktopItem> items,
        double? x,
        double? y,
        ScreenMetrics screen,
        (double Width, double Height) cellDip,
        Func<DesktopItem, FracRect, WidgetInstance> create)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(create);
        if (!screen.IsUsable)
        {
            return items;
        }

        var size = DesktopGrid.IconSize(new SizeDip(cellDip.Width, cellDip.Height), screen);
        var occupied = DesktopGrid.Occupied(view);
        var unplaced = new List<DesktopItem>();
        var shown = new HashSet<string>(DesktopViews.Shown(view), DesktopItemKeys.Comparer);
        foreach (var item in items)
        {
            // [S-13015] One icon per item on a view: one shown already keeps its place.
            if (!shown.Add(item.Key))
            {
                continue;
            }

            if (DesktopGrid.Arrive(occupied, size, x, y, screen) is { } spot)
            {
                view.Widgets.Add(create(item, spot));
            }
            else
            {
                unplaced.Add(item);
            }
        }

        return unplaced;
    }

    /// <summary>
    /// [S-13008] The rect of a fence made round <paramref name="icons"/> (the selected free icons' rects on a display of
    /// <paramref name="screen"/>): their bounding box with the title bar added above it, at least the size that shows all
    /// <paramref name="count"/> items without scrolling (<see cref="FenceGrid.Around"/>), snapped out to whole grid cells and
    /// kept on the display. Null with no icon or no usable screen.
    /// </summary>
    public static FracRect? AroundIcons(IReadOnlyList<FracRect> icons, int count, SizeDip cell, ScreenMetrics screen)
    {
        ArgumentNullException.ThrowIfNull(icons);
        if (icons.Count == 0 || !screen.IsUsable)
        {
            return null;
        }

        var left = icons.Min(r => r.X) * screen.Width;
        var top = icons.Min(r => r.Y) * screen.Height;
        var right = icons.Max(r => r.X + r.W) * screen.Width;
        var bottom = icons.Max(r => r.Y + r.H) * screen.Height;
        var need = FenceGrid.Around(count, cell);
        var width = Math.Max(right - left, need.Width);
        var height = Math.Max(bottom - top + FenceGrid.TitleHeight, need.Height);
        var y = Math.Max(0, top - FenceGrid.TitleHeight);
        return Snap(new FracRect(left / screen.Width, y / screen.Height, width / screen.Width, height / screen.Height), screen);
    }

    /// <summary>
    /// [S-13007] A rectangle drawn on empty desktop (fractions of its display) as a fence's rect: snapped out to whole grid
    /// cells, at least <paramref name="min"/> DIPs, kept on the display. Null with no usable screen.
    /// </summary>
    public static FracRect? FromRect(FracRect drawn, SizeDip min, ScreenMetrics screen)
    {
        if (!screen.IsUsable || !drawn.IsFinite)
        {
            return null;
        }

        var w = Math.Max(drawn.W, min.Width / screen.Width);
        var h = Math.Max(drawn.H, min.Height / screen.Height);
        return Snap(drawn with { W = w, H = h }, screen);
    }

    /// <summary>Out to whole cells of the widget grid ([S-06]: the corner down to its line, the size up), then kept on the display ([S-09]).</summary>
    internal static FracRect Snap(FracRect rect, ScreenMetrics screen)
    {
        var (cols, rows) = GridGeometry.GridDims(screen);
        double Down(double f, int n) => Math.Floor((f * n) + 1e-9) / n;
        double Up(double f, int n) => Math.Min(1, Math.Ceiling((f * n) - 1e-9) / n);
        return GridGeometry.Clamp(new FracRect(Down(rect.X, cols), Down(rect.Y, rows), Up(rect.W, cols), Up(rect.H, rows)), screen);
    }

    /// <summary>
    /// [S-13002] Load: every fence's items made whole and no item in two places of one view. In each fence an invalid key
    /// (<see cref="DesktopItemKeys.IsValid"/>) and a repeat go; an item in two fences of one view stays in the first (the
    /// view's order) and leaves the others; an item a fence holds has no free icon on that view (the free one goes: the
    /// fence was the user's choice). Its rules are repaired (<see cref="FenceRules.Repair"/>). Each repair is said.
    /// Items no longer on the desktop are the reconcile's ([S-13003]): load has no listing.
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

                if (on.Fence.Sort is { } sort && !Enum.IsDefined(sort))
                {
                    on.Fence.Sort = null;
                    repairs.Add($"fence '{on.Widget.Id}' had an unknown sort; it keeps its order");
                }
            }

            var free = view.Widgets.RemoveAll(w => w.Config is IDesktopItemConfig { Item.Length: > 0 } c && seen.Contains(c.Item));
            if (free > 0)
            {
                repairs.Add($"view '{view.Id}' showed {free} item(s) both free and in a fence; the free icon(s) dropped");
            }
        }

        return repairs;
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
}
