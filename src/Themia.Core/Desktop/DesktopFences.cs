using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>[S-13000] One fence of a view: its widget and the config that holds its items.</summary>
public readonly record struct FenceOn(WidgetInstance Widget, IHoldsDesktopItems Fence);

/// <summary>
/// [S-13000]..[S-13014] [S-13040]..[S-13044] The fences' rules, pure (the owner, 2026-11-19: "a group of icons with a
/// header … build as a widget that can be created like fences does it"; and "the fence should basically just be a helper
/// to align the icon on the underlying engine without this transition in between"). A fence is a widget whose config holds
/// real desktop items by their keys (<see cref="IHoldsDesktopItems"/>); it lives on one view. On that view each item it
/// holds is a desktop icon like any other (its item widget, [S-13015]) that the fence lays on a cell of the desktop icon
/// grid in its body (<see cref="Lay"/>, <see cref="FenceCells"/>): the fence groups and constrains those cells, it draws no
/// item of its own. Which free icons a view shows (All, Chosen, None, [S-5525]) concerns the free desktop alone: an item in
/// a fence is always shown with its fence. Nothing here touches a file.
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
    /// the view's order, whose rules it matches (<see cref="FenceRules.Matches"/>); null when none takes it. [S-13023] A
    /// search fence holds no desktop item: never.
    /// </summary>
    public static FenceOn? Route(ViewState view, DesktopItem item)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(item);
        foreach (var on in On(view))
        {
            if (on.Fence.Source != FenceSource.Search && FenceRules.Matches(on.Fence.Rules, item))
            {
                return on;
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
    /// <paramref name="count"/> items without scrolling (<see cref="FenceGrid.Around"/>; <paramref name="cell"/> the icon's
    /// box in DIPs), snapped out to whole grid cells and kept on the display. Null with no icon or no usable screen.
    /// </summary>
    public static FracRect? AroundIcons(IReadOnlyList<FracRect> icons, int count, SizeDip cell, ScreenMetrics screen)
    {
        ArgumentNullException.ThrowIfNull(icons);
        if (icons.Count == 0 || !screen.IsUsable)
        {
            return null;
        }

        var title = FenceCells.TitleHeight(screen) * screen.Height;
        var left = icons.Min(r => r.X) * screen.Width;
        var top = icons.Min(r => r.Y) * screen.Height;
        var right = icons.Max(r => r.X + r.W) * screen.Width;
        var bottom = icons.Max(r => r.Y + r.H) * screen.Height;
        var need = FenceGrid.Around(count, cell);
        var width = Math.Max(right - left, need.Width);
        var height = Math.Max(bottom - top + title, need.Height - FenceGrid.TitleDip + title);
        var y = Math.Max(0, top - title);
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
    /// view's order) and leaves the others. Its rules are repaired (<see cref="FenceRules.Repair"/>), a negative scroll is
    /// the top. [S-13044] An item held with no icon on its view gets one (<paramref name="icon"/>, else <see cref="NewIcon"/>):
    /// its icon is the fence's (one per item, [S-13015]). Each repair is said. Items no longer on the desktop are the
    /// reconcile's ([S-13003]): load has no listing.
    /// </summary>
    public static IReadOnlyList<string> Repair(AppState state, Func<string, FracRect, WidgetInstance?>? icon = null)
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

            // [S-13044] A fence's items are icons of its view: one stored before they were (the fence drew its own) gets
            // its icon, at the fence's corner till the fence lays it on its cell. Once: a second load finds them.
            var icons = IconsByKey(view);
            var made = 0;
            foreach (var on in On(view).ToList())
            {
                foreach (var key in on.Fence.Items.ToList())
                {
                    if (icons.ContainsKey(key))
                    {
                        continue;
                    }

                    if ((icon ?? NewIcon)(key, new FracRect(on.Widget.X, on.Widget.Y, 0, 0)) is { } widget)
                    {
                        view.Widgets.Add(widget);
                        icons[key] = widget;
                        made++;
                    }
                }
            }

            if (made > 0)
            {
                repairs.Add($"view '{view.Id}': {made} fence item(s) stored without their icon got one (fences lay desktop icons now)");
            }
        }

        return repairs;
    }

    /// <summary>
    /// [S-13044] A desktop item's icon for <paramref name="key"/> at <paramref name="rect"/>: the build's one item kind
    /// (<see cref="IDesktopItemConfig"/>), or null in a build without it.
    /// </summary>
    public static WidgetInstance? NewIcon(string key, FracRect rect)
    {
        if (!ModuleCatalog.IsInstalled || ModuleCatalog.Widgets.FirstOrDefault(d => typeof(IDesktopItemConfig).IsAssignableFrom(d.ConfigType)) is not { } kind)
        {
            return null;
        }

        var widget = WidgetInstance.Create(kind.Id, rect);
        if (widget.Config is not IDesktopItemConfig config)
        {
            return null;
        }

        config.Item = key;
        return widget;
    }

    /// <summary>
    /// [S-13022] [S-13023] Every fence made to show what its source says, against the desktop's <paramref name="listing"/>: a
    /// collecting fence (<see cref="FenceSource.Desktop"/>) takes in every listed item its rules match that no fence before
    /// it on that view holds (from the free desktop and from other fences, <see cref="MoveIn"/>, at its end), and lets go of
    /// a listed item its rules no longer match; a search fence (<see cref="FenceSource.Search"/>) holds no desktop item and
    /// lets go of any it held. What was let go is returned by view, for the free desktop (the caller lays it at free spots,
    /// <see cref="LetGo"/>); an item another fence of that view holds now is not in it. Nothing listed is never taken: an
    /// item not in the listing stays for the reconcile ([S-13003]).
    /// </summary>
    public static IReadOnlyList<(ViewState View, IReadOnlyList<string> Keys)> Collect(AppState state, IReadOnlyList<DesktopItem> listing, Func<string, WidgetInstance?>? icon = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(listing);
        var listed = new Dictionary<string, DesktopItem>(DesktopItemKeys.Comparer);
        foreach (var item in listing)
        {
            listed.TryAdd(item.Key, item);
        }

        var released = new List<(ViewState, IReadOnlyList<string>)>();
        foreach (var view in state.Views)
        {
            var letGo = new List<string>();
            foreach (var on in On(view).ToList())
            {
                switch (on.Fence.Source)
                {
                    case FenceSource.Search when on.Fence.Items.Count > 0:
                        letGo.AddRange(on.Fence.Items);
                        on.Fence.Items.Clear();
                        break;

                    case FenceSource.Desktop:
                        var stale = on.Fence.Items.Where(key => listed.TryGetValue(key, out var item) && !FenceRules.Matches(on.Fence.Rules, item)).ToList();
                        if (stale.Count > 0)
                        {
                            on.Fence.Items.RemoveAll(stale.Contains);
                            letGo.AddRange(stale);
                        }

                        // Held before this one on the view: by an earlier fence (the first in the view's order keeps it).
                        var earlier = new HashSet<string>(DesktopItemKeys.Comparer);
                        foreach (var before in On(view).TakeWhile(other => !ReferenceEquals(other.Widget, on.Widget)))
                        {
                            earlier.UnionWith(before.Fence.Items);
                        }

                        var wanted = DesktopItemOrder.Sort(listed.Values.Where(item => FenceRules.Matches(on.Fence.Rules, item)
                            && !earlier.Contains(item.Key) && !on.Fence.Items.Contains(item.Key, DesktopItemKeys.Comparer)));
                        if (wanted.Count > 0)
                        {
                            MoveIn(view, on.Widget.Id, [.. wanted.Select(item => item.Key)], on.Fence.Items.Count, icon);
                        }

                        break;
                }
            }

            var held = Held(view);
            var free = letGo.Where(key => !held.Contains(key)).Distinct(DesktopItemKeys.Comparer).ToList();
            if (free.Count > 0)
            {
                released.Add((view, free));
            }
        }

        return released;
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

    // ---- the fence on the desktop icon grid ([S-13040]..[S-13043]) ------------------------------------------------------

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
    /// [S-13040] [S-13042] [S-13043] Every fence of <paramref name="view"/> laid on the desktop icon grid of
    /// <paramref name="screen"/> (<paramref name="box"/>: an icon's box there, whole grid cells, <see cref="DesktopGrid.IconSize"/>):
    /// an item whose icon has left the view (hidden, deleted, cut, moved to another display) leaves its fence; the fence's
    /// rect is its title bar's grid rows and whole boxes under it (<see cref="FenceCells.Snap"/>); its scroll is kept between
    /// the top and its last row; and each item's icon stands on its cell, in the fence's order, where the body shows it, or
    /// tucked in the fence's corner where it does not (scrolled away, rolled up: drawn by no surface, <see cref="Hidden"/>).
    /// A search fence holds nothing. Runs in every change of the state (the host's). Returns whether anything changed.
    /// </summary>
    public static bool Lay(ViewState view, ScreenMetrics screen, (double W, double H) box)
    {
        ArgumentNullException.ThrowIfNull(view);
        var changed = false;
        var icons = IconsByKey(view);
        var usable = screen.IsUsable && box.W > 0 && box.H > 0;
        foreach (var on in On(view).ToList())
        {
            if (on.Fence.Items.RemoveAll(key => !icons.ContainsKey(key)) > 0)
            {
                changed = true;
            }

            if (!usable)
            {
                continue;
            }

            var snapped = FenceCells.Snap(on.Widget.Rect, on.Fence.RolledUp, screen, box);
            if (!Same(snapped, on.Widget.Rect))
            {
                on.Widget.Rect = snapped;
                changed = true;
            }

            var layout = FenceCells.Of(on.Widget.Rect, on.Fence, screen, box).Clamped(on.Fence.Items.Count);
            if (layout.Scroll != on.Fence.Scroll)
            {
                on.Fence.Scroll = layout.Scroll;
                changed = true;
            }

            for (var i = 0; i < on.Fence.Items.Count; i++)
            {
                var icon = icons[on.Fence.Items[i]];
                var rect = layout.Shows(i) ? FenceCells.Cell(layout, i) : Tucked(on.Widget.Rect, box);
                if (!Same(rect, icon.Rect))
                {
                    icon.Rect = rect;
                    changed = true;
                }
            }
        }

        return changed;
    }

    /// <summary>Where an item's icon waits while its fence does not show it: the fence's corner, inside the fence.</summary>
    private static FracRect Tucked(FracRect fence, (double W, double H) box)
        => new(fence.X, fence.Y, Math.Min(box.W, fence.W), Math.Min(box.H, fence.H));

    /// <summary>
    /// [S-13042] [S-13043] The widget ids of the icons <paramref name="view"/>'s fences do not show now (scrolled away, rolled
    /// up): no surface draws them, nothing selects them.
    /// </summary>
    public static HashSet<string> Hidden(ViewState view, ScreenMetrics screen, (double W, double H) box)
    {
        ArgumentNullException.ThrowIfNull(view);
        var hidden = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (id, (on, index)) in Members(view))
        {
            if (on.Fence.RolledUp || !screen.IsUsable || !FenceCells.Of(on.Widget.Rect, on.Fence, screen, box).Clamped(on.Fence.Items.Count).Shows(index))
            {
                hidden.Add(id);
            }
        }

        return hidden;
    }

    /// <summary>
    /// [S-13042] The order <paramref name="view"/>'s widgets are painted in: the view's own ([S-874]), each fence's icons
    /// right over their fence, in its order.
    /// </summary>
    public static List<WidgetInstance> PaintOrder(ViewState view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var members = Members(view);
        if (members.Count == 0)
        {
            return [.. view.Widgets];
        }

        var byFence = members.GroupBy(m => m.Value.Fence.Widget.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.Value.Index).Select(m => m.Key).ToList(), StringComparer.Ordinal);
        var widgets = new Dictionary<string, WidgetInstance>(StringComparer.Ordinal);
        foreach (var widget in view.Widgets)
        {
            widgets.TryAdd(widget.Id, widget);
        }

        var order = new List<WidgetInstance>(view.Widgets.Count);
        foreach (var widget in view.Widgets)
        {
            if (members.ContainsKey(widget.Id))
            {
                continue;
            }

            order.Add(widget);
            if (byFence.TryGetValue(widget.Id, out var icons))
            {
                order.AddRange(icons.Select(id => widgets[id]));
            }
        }

        return order;
    }

    /// <summary>
    /// [S-13041] The one drop computation for the free desktop and a fence: where an icon moved to
    /// <paramref name="landing"/> (already on the widget grid, the move's) lands. On a fence's body (its centre on one of the
    /// body's cells; a fence <paramref name="moving"/> carries, a rolled-up or a search fence aside) the cell of the place it
    /// takes there: the cell under it, never past the items that stay (a fence fills its cells in order) nor a row the body
    /// does not show. Anywhere else the landing itself. Both are cells of the one grid.
    /// </summary>
    public static FracRect DropCell(ViewState view, FracRect landing, ScreenMetrics screen, (double W, double H) box, IReadOnlySet<string> moving)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(moving);
        return Under(view, landing, screen, box, moving) is { } at ? FenceCells.Cell(at.Layout, at.Slot) : landing;
    }

    /// <summary>[S-13041] The fence whose body <paramref name="landing"/>'s centre is on (the topmost), its layout and the place a drop there takes; null for none.</summary>
    private static (FenceOn Fence, FenceGrid.Layout Layout, int Slot)? Under(ViewState view, FracRect landing, ScreenMetrics screen, (double W, double H) box, IReadOnlySet<string> moving)
    {
        if (!screen.IsUsable || !landing.IsFinite || !(box.W > 0) || !(box.H > 0))
        {
            return null;
        }

        var (cx, cy) = (landing.X + (landing.W / 2), landing.Y + (landing.H / 2));
        var icons = IconsByKey(view);
        foreach (var on in On(view).Reverse())
        {
            if (moving.Contains(on.Widget.Id) || on.Fence.RolledUp || on.Fence.Source == FenceSource.Search)
            {
                continue;
            }

            var layout = FenceCells.Of(on.Widget.Rect, on.Fence, screen, box).Clamped(on.Fence.Items.Count);
            var slot = layout.SlotAt(cx, cy);
            if (slot < 0)
            {
                continue;
            }

            var staying = on.Fence.Items.Count(key => !(icons.TryGetValue(key, out var icon) && moving.Contains(icon.Id)));
            var first = layout.Scroll * layout.Columns;
            var last = first + Math.Max(0, layout.Shown - 1);
            return (on, layout, Math.Clamp(Math.Min(slot, staying), first, last));
        }

        return null;
    }

    /// <summary>
    /// [S-13041] A move of <paramref name="moved"/> (widget ids of <paramref name="view"/>, already at the rects they landed
    /// on) let go: each desktop item's icon among them that landed on a fence's body is that fence's, at the place its cell
    /// stands for (<see cref="DropCell"/>, several in reading order), leaving any fence it was in; one that landed anywhere
    /// else is a free icon where it stands. An icon carried with its own fence stays in it; an own icon is never a fence's.
    /// The caller lays the fences after (<see cref="Lay"/>). Returns how many icons changed fence or place in one.
    /// </summary>
    public static int Land(ViewState view, IReadOnlyCollection<string> moved, ScreenMetrics screen, (double W, double H) box)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(moved);
        var set = new HashSet<string>(moved, StringComparer.Ordinal);
        var landings = new List<(string Key, FracRect Rect, (FenceOn Fence, FenceGrid.Layout Layout, int Slot)? At)>();
        foreach (var widget in view.Widgets)
        {
            if (!set.Contains(widget.Id) || widget.Config is not IDesktopItemConfig { Item.Length: > 0 } config)
            {
                continue;
            }

            if (Holding(view, config.Item) is { } holder && set.Contains(holder.Widget.Id))
            {
                continue;
            }

            landings.Add((config.Item, widget.Rect, Under(view, widget.Rect, screen, box, set)));
        }

        if (landings.Count == 0)
        {
            return 0;
        }

        var before = Places(view, landings.Select(l => l.Key));
        TakeOut(view, landings.Select(l => l.Key));
        foreach (var (key, _, at) in landings.Where(l => l.At is not null).OrderBy(l => l.At!.Value.Slot).ThenBy(l => l.Rect.Y).ThenBy(l => l.Rect.X))
        {
            var items = at!.Value.Fence.Fence.Items;
            items.Insert(Math.Clamp(at.Value.Slot, 0, items.Count), key);
        }

        var after = Places(view, landings.Select(l => l.Key));
        return landings.Count(l => before[l.Key] != after[l.Key]);
    }

    /// <summary>Where each of <paramref name="keys"/> stands among <paramref name="view"/>'s fences: the fence's id and the place, or none.</summary>
    private static Dictionary<string, (string? Fence, int At)> Places(ViewState view, IEnumerable<string> keys)
    {
        var places = new Dictionary<string, (string?, int)>(DesktopItemKeys.Comparer);
        foreach (var key in keys)
        {
            places[key] = (null, -1);
            foreach (var on in On(view))
            {
                var at = on.Fence.Items.FindIndex(k => DesktopItemKeys.Comparer.Equals(k, key));
                if (at >= 0)
                {
                    places[key] = (on.Widget.Id, at);
                    break;
                }
            }
        }

        return places;
    }

    /// <summary>
    /// [S-13022] [S-13043] <paramref name="items"/> let go by the fences of <paramref name="view"/>: they leave every fence and
    /// their icons are laid again on the free desktop (<see cref="LayFree"/>: at the point, else the first free spots), never
    /// left where a fence had them. Returns the items that found no spot (Add widget lists them).
    /// </summary>
    public static IReadOnlyList<DesktopItem> LetGo(
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
        var keys = new HashSet<string>(items.Select(i => i.Key), DesktopItemKeys.Comparer);
        TakeOut(view, keys);
        view.Widgets.RemoveAll(w => w.Config is IDesktopItemConfig { Item.Length: > 0 } c && keys.Contains(c.Item));
        return LayFree(view, items, x, y, screen, cellDip, create);
    }

    /// <summary>
    /// [S-13009] The fence <paramref name="fence"/>, just taken off <paramref name="view"/>, puts its items back where it stood:
    /// the icons it showed stay on their cells (free icons now, exactly where they were), the ones it did not show (scrolled
    /// away, rolled up) are laid at the free spots nearest its corner (<paramref name="find"/> names an item from its key).
    /// Returns the items that found no spot.
    /// </summary>
    public static IReadOnlyList<DesktopItem> PutBack(
        ViewState view,
        WidgetInstance fence,
        ScreenMetrics screen,
        (double W, double H) box,
        (double Width, double Height) cellDip,
        Func<string, DesktopItem?> find,
        Func<DesktopItem, FracRect, WidgetInstance> create)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(fence);
        ArgumentNullException.ThrowIfNull(find);
        if (fence.Config is not IHoldsDesktopItems held || held.Items.Count == 0)
        {
            return [];
        }

        // Only icons still on the view: one taken away with the fence (deleted with it) stays away.
        var icons = IconsByKey(view);
        var layout = FenceCells.Of(fence.Rect, held, screen, box).Clamped(held.Items.Count);
        var away = held.Items.Where((key, i) => icons.ContainsKey(key) && (held.RolledUp || !screen.IsUsable || !layout.Shows(i))).Select(find).OfType<DesktopItem>().ToList();
        return away.Count == 0 ? [] : LetGo(view, away, fence.X + (box.W / 2), fence.Y + (box.H / 2), screen, cellDip, create);
    }

    private static bool Same(FracRect a, FracRect b)
        => Math.Abs(a.X - b.X) < 1e-9 && Math.Abs(a.Y - b.Y) < 1e-9 && Math.Abs(a.W - b.W) < 1e-9 && Math.Abs(a.H - b.H) < 1e-9;
}
