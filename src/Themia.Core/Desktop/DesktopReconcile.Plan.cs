using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>[S-5520] What one reconcile changes, worked out before anything is mutated.</summary>
/// <param name="Renames">The renames that touch a widget or the known set: (old key, new key).</param>
/// <param name="Gone">The ids of the item widgets whose item is no longer listed, in every view.</param>
/// <param name="Arrivals">The listed items not known before, in <see cref="DesktopItemOrder"/>: each is to get a widget.</param>
/// <param name="Known">The known set once applied: the listing's keys.</param>
public sealed record DesktopPlan(
    IReadOnlyList<(string Old, string New)> Renames,
    IReadOnlySet<string> Gone,
    IReadOnlyList<DesktopItem> Arrivals,
    IReadOnlyList<string> Known)
{
    /// <summary>Nothing to do: no rename, no removal, no arrival, and the known set already is the listing.</summary>
    public bool Changes(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Renames.Count > 0 || Gone.Count > 0 || Arrivals.Count > 0 || !Known.SequenceEqual(state.Desktop.Known, DesktopItemKeys.Comparer);
    }
}

public static partial class DesktopReconcile
{
    /// <summary>
    /// [S-5520] The reconcile of the state against a listing: (1) renames rewrite keys, (2) an item no
    /// longer listed loses its widgets in every view, (3) a listed item not known is an arrival, (4) the
    /// known set becomes the listing. Pure: nothing in <paramref name="state"/> changes.
    /// </summary>
    public static DesktopPlan Plan(AppState state, IReadOnlyList<DesktopItem> listing, IReadOnlyList<(string Old, string New)> renames)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(listing);
        ArgumentNullException.ThrowIfNull(renames);

        var listed = new Dictionary<string, DesktopItem>(DesktopItemKeys.Comparer);
        foreach (var item in listing)
        {
            listed.TryAdd(item.Key, item);
        }

        var known = new HashSet<string>(state.Desktop.Known, DesktopItemKeys.Comparer);

        // (1) Only a rename onto a listed item counts: one that moved the item off the desktop is a removal.
        var applied = new List<(string Old, string New)>();
        var renamedTo = new Dictionary<string, string>(DesktopItemKeys.Comparer);
        foreach (var (before, after) in renames)
        {
            if (DesktopItemKeys.Comparer.Equals(before, after) || !listed.ContainsKey(after))
            {
                continue;
            }

            var touches = known.Contains(before) || ItemWidgets(state).Any(w => DesktopItemKeys.Comparer.Equals(w.Config.Item, before));
            if (!touches)
            {
                continue;
            }

            applied.Add((before, after));
            renamedTo[before] = after;
            if (known.Remove(before))
            {
                known.Add(after);
            }
        }

        // (2) Every view's item widgets, by the key they will have after the renames.
        var gone = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (widget, config) in ItemWidgets(state).Select(w => (w.Widget, w.Config)))
        {
            var key = renamedTo.TryGetValue(config.Item, out var renamed) ? renamed : config.Item;
            if (!listed.ContainsKey(key))
            {
                gone.Add(widget.Id);
            }
        }

        // (3) and (4).
        var arrivals = DesktopItemOrder.Sort(listed.Values.Where(item => !known.Contains(item.Key)));
        return new DesktopPlan(applied, gone, arrivals, [.. listed.Keys]);
    }

    /// <summary>
    /// [S-5520] [S-5521] Carry out <paramref name="plan"/> on <paramref name="state"/>: keys rewritten
    /// and gone widgets removed in every view; each arrival given a widget (<paramref name="create"/>) in
    /// the view <paramref name="targetViewId"/> at its next free cell of <paramref name="cellDip"/> on
    /// <paramref name="screen"/>; the known set set. Returns the arrivals left unplaced because the view
    /// had no free cell (known now, reachable from Add widget). With no target view (no display yet) the
    /// arrivals are left unknown, so the next reconcile places them.
    /// <para>
    /// [S-5591] An arrival with a landing spot (<paramref name="spots"/>, by the arrival's position:
    /// a drop, a paste, New ›) goes to that spot's view instead, at the cell under its point or the next
    /// free one after it (<see cref="DesktopCells.FreeFrom"/>) on that view's display
    /// (<paramref name="screenOf"/>); a spot whose view or display is gone falls back to the rule above.
    /// </para>
    /// </summary>
    public static IReadOnlyList<DesktopItem> Apply(
        AppState state,
        DesktopPlan plan,
        string? targetViewId,
        ScreenMetrics screen,
        (double Width, double Height) cellDip,
        Func<DesktopItem, FracRect, WidgetInstance> create,
        IReadOnlyList<DesktopLandingSpot?>? spots = null,
        Func<string, ScreenMetrics?>? screenOf = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(create);

        foreach (var (before, after) in plan.Renames)
        {
            foreach (var (_, config) in ItemWidgets(state))
            {
                if (DesktopItemKeys.Comparer.Equals(config.Item, before))
                {
                    config.Item = after;
                }
            }
        }

        if (plan.Gone.Count > 0)
        {
            foreach (var view in state.Views)
            {
                view.Widgets.RemoveAll(w => plan.Gone.Contains(w.Id));
            }
        }

        var unplaced = new List<DesktopItem>();
        var unknown = new HashSet<string>(DesktopItemKeys.Comparer);
        var target = state.FindView(targetViewId);
        for (var i = 0; i < plan.Arrivals.Count; i++)
        {
            var item = plan.Arrivals[i];

            // [S-5591] Its own spot first: the view of the display it was dropped on, at the drop point.
            if (spots is not null && i < spots.Count && spots[i] is { } spot
                && state.FindView(spot.ViewId) is { } spotView
                && screenOf?.Invoke(spot.ViewId) is { IsUsable: true } spotScreen)
            {
                var spotCell = new FracRect(0, 0, cellDip.Width / spotScreen.Width, cellDip.Height / spotScreen.Height);
                FracRect[] taken = [.. spotView.Widgets.Select(w => w.Rect)];
                var landed = spot.X is { } x && spot.Y is { } y
                    ? DesktopCells.FreeFrom(taken, x, y, spotCell, spotScreen.Allowed)
                    : DesktopGrid.NextFree(taken, spotCell, spotScreen.Allowed);
                if (landed is { } at)
                {
                    spotView.Widgets.Add(create(item, at));
                }
                else
                {
                    unplaced.Add(item);
                }

                continue;
            }

            if (target is null || !screen.IsUsable)
            {
                unknown.Add(item.Key);
                continue;
            }

            var cell = new FracRect(0, 0, cellDip.Width / screen.Width, cellDip.Height / screen.Height);
            if (DesktopGrid.NextFree([.. target.Widgets.Select(w => w.Rect)], cell, screen.Allowed) is { } free)
            {
                target.Widgets.Add(create(item, free));
            }
            else
            {
                unplaced.Add(item);
            }
        }

        state.Desktop.Known = [.. plan.Known.Where(key => !unknown.Contains(key))];
        return unplaced;
    }

    /// <summary>[S-5506] The widgets of <paramref name="widgets"/> a surface builds cards for: the item widgets only while Themia's items show.</summary>
    public static IReadOnlyList<WidgetInstance> Shown(IReadOnlyList<WidgetInstance> widgets, bool showsItems)
    {
        ArgumentNullException.ThrowIfNull(widgets);
        return showsItems ? widgets : [.. widgets.Where(w => w.Config is not IDesktopItemConfig)];
    }

    /// <summary>
    /// [S-5523] The listed items with no widget in <paramref name="view"/>, in <see cref="DesktopItemOrder"/>:
    /// what Add widget's "On the desktop" offers there.
    /// </summary>
    public static IReadOnlyList<DesktopItem> Unplaced(ViewState view, IReadOnlyList<DesktopItem> listing)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(listing);

        var placed = new HashSet<string>(
            view.Widgets.Select(w => w.Config).OfType<IDesktopItemConfig>().Select(c => c.Item),
            DesktopItemKeys.Comparer);
        return DesktopItemOrder.Sort(listing.Where(item => !placed.Contains(item.Key)));
    }

    private static IEnumerable<(WidgetInstance Widget, IDesktopItemConfig Config)> ItemWidgets(AppState state)
    {
        foreach (var view in state.Views)
        {
            foreach (var widget in view.Widgets)
            {
                if (widget.Config is IDesktopItemConfig config)
                {
                    yield return (widget, config);
                }
            }
        }
    }
}

/// <summary>[S-5521] The desktop's grid of item cells, as fractions of a display.</summary>
public static class DesktopGrid
{
    private const double Epsilon = 1e-9;

    /// <summary>
    /// [S-5521] The first cell of <paramref name="cell"/>'s size, from <paramref name="area"/>'s top-left
    /// corner down a column and then the next column to the right, that overlaps none of
    /// <paramref name="occupied"/>; null when every whole cell in the area is taken.
    /// </summary>
    public static FracRect? NextFree(IReadOnlyList<FracRect> occupied, FracRect cell, FracRect area)
    {
        ArgumentNullException.ThrowIfNull(occupied);
        if (cell.W <= 0 || cell.H <= 0 || !double.IsFinite(cell.W) || !double.IsFinite(cell.H))
        {
            return null;
        }

        for (var x = area.X; x + cell.W <= area.Right + Epsilon; x += cell.W)
        {
            for (var y = area.Y; y + cell.H <= area.Bottom + Epsilon; y += cell.H)
            {
                var candidate = new FracRect(x, y, cell.W, cell.H);
                if (!occupied.Any(taken => Overlaps(taken, candidate)))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static bool Overlaps(FracRect a, FracRect b)
        => a.X < b.Right - Epsilon && b.X < a.Right - Epsilon && a.Y < b.Bottom - Epsilon && b.Y < a.Bottom - Epsilon;
}
