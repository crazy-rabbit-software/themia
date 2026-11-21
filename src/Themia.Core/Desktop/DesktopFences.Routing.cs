using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>
/// [S-13004] [S-13006] [S-13009] [S-13022] The fences' routing: the fence a new item arrives in by its rules, what a
/// collecting fence takes in and lets go, and items let go laid on the free desktop.
/// </summary>
public static partial class DesktopFences
{
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
    /// [S-15666] What <see cref="Collect"/> reads of the state besides the listing, as one text: every fence of each view that
    /// has a collecting or a search fence (its id, source, rules and items, in the view's order). <see cref="Collect"/> run
    /// once leaves nothing for a second run to do: while the stamp and the listing are the ones it ran on, it is not run
    /// again (an item taken out by hand changes the items, so it comes back while it matches). Null when no view has such a
    /// fence (nothing to collect).
    /// </summary>
    public static string? CollectStamp(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        System.Text.StringBuilder? stamp = null;
        foreach (var view in state.Views)
        {
            var fences = On(view).ToList();
            if (!fences.Exists(on => on.Fence.Source != FenceSource.Held))
            {
                continue;
            }

            stamp ??= new System.Text.StringBuilder();
            stamp.Append(view.Id).Append('\u001e');
            foreach (var on in fences)
            {
                var rules = on.Fence.Rules;
                stamp.Append(on.Widget.Id).Append('\u001f').Append(on.Fence.Source).Append('\u001f')
                    .AppendJoin(',', rules?.Types ?? []).Append('\u001f')
                    .AppendJoin(',', rules?.Extensions ?? []).Append('\u001f')
                    .Append(rules?.NameContains).Append('\u001f')
                    .AppendJoin('\u001f', on.Fence.Items).Append('\u001e');
            }
        }

        return stamp?.ToString();
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
        var occupied = Occupancy.Of(DesktopGrid.Occupied(view), screen);
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
}
