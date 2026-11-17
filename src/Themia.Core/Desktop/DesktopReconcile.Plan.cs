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
    /// [S-5597] <paramref name="expected"/>: items an undo is putting back from the Recycle Bin, not listed yet: their
    /// widgets (brought back by the undo where they stood) stay and they stay known, so they are no arrival when they come.
    /// </summary>
    public static DesktopPlan Plan(AppState state, IReadOnlyList<DesktopItem> listing, IReadOnlyList<(string Old, string New)> renames, IReadOnlySet<string>? expected = null)
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
            if (!listed.ContainsKey(key) && expected?.Contains(key) != true)
            {
                gone.Add(widget.Id);
            }
        }

        // (3) and (4).
        var arrivals = DesktopItemOrder.Sort(listed.Values.Where(item => !known.Contains(item.Key)));
        var waiting = expected?.Where(key => !listed.ContainsKey(key)) ?? [];
        return new DesktopPlan(applied, gone, arrivals, [.. listed.Keys, .. waiting]);
    }

    /// <summary>
    /// [S-5520] [S-5521] [S-5526] Carry out <paramref name="plan"/> on <paramref name="state"/>: keys rewritten
    /// and gone widgets removed in every view; each arrival given a widget (<paramref name="create"/>) in every view
    /// set to show all desktop icons (<see cref="DesktopViews.Receives"/>), at the first free spot of the widget grid
    /// (<see cref="DesktopGrid.Arrive"/>, [S-11876]: the icon's box, <paramref name="cellDip"/> in DIPs with the gap,
    /// in whole grid cells of the view's display, <paramref name="screenOf"/>, or of <paramref name="screen"/> (the
    /// primary display's) for a view no display shows, overlapping no widget); never on a Chosen or None view; the
    /// known set set. Returns the arrivals left unplaced on some view because it had no free spot (known now,
    /// reachable from Add widget). With no usable <paramref name="screen"/> (no display yet) the arrivals are left
    /// unknown, so the next reconcile places them.
    /// <para>
    /// [S-5591] [S-5526] An arrival with a landing spot (<paramref name="spots"/>, by the arrival's position:
    /// a drop, a paste, New ›) lands on that spot's view, centred on its point on the grid or at the free spot
    /// nearest to it, on that view's display (<paramref name="screenOf"/>), whatever the view's value but None (a
    /// None view shows none, as Windows' desktop with its icons hidden); every other All view gets it at its first
    /// free spot. A spot whose view or display is gone falls back to the rule above.
    /// </para>
    /// </summary>
    public static IReadOnlyList<DesktopItem> Apply(
        AppState state,
        DesktopPlan plan,
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

        // [S-11876] One room per view for the whole pass: every arrival takes its spot in it, so two never share one.
        var rooms = new Dictionary<string, ((double W, double H) Size, List<FracRect> Occupied, HashSet<string> Shown)>(StringComparer.Ordinal);
        ((double W, double H) Size, List<FracRect> Occupied, HashSet<string> Shown) RoomOf(ViewState view, ScreenMetrics on)
        {
            if (!rooms.TryGetValue(view.Id, out var room))
            {
                room = (DesktopGrid.IconSize(new SizeDip(cellDip.Width, cellDip.Height), on), DesktopGrid.Occupied(view), new HashSet<string>(DesktopViews.Shown(view), DesktopItemKeys.Comparer));
                rooms[view.Id] = room;
            }

            return room;
        }

        void Land(ViewState view, ScreenMetrics on, DesktopItem item, double? x, double? y)
        {
            var room = RoomOf(view, on);
            if (room.Shown.Contains(item.Key))
            {
                return;
            }

            if (DesktopGrid.Arrive(room.Occupied, room.Size, x, y, on) is { } landed)
            {
                view.Widgets.Add(create(item, landed));
                room.Shown.Add(item.Key);
            }
            else if (!unplaced.Contains(item))
            {
                unplaced.Add(item);
            }
        }

        for (var i = 0; i < plan.Arrivals.Count; i++)
        {
            var item = plan.Arrivals[i];

            // [S-5591] Its own spot first: the view of the display it was dropped on, at the drop point.
            string? landedOn = null;
            if (spots is not null && i < spots.Count && spots[i] is { } spot
                && state.FindView(spot.ViewId) is { } spotView
                && screenOf?.Invoke(spot.ViewId) is { IsUsable: true } spotScreen)
            {
                landedOn = spotView.Id;
                if (DesktopViews.Holds(spotView))
                {
                    Land(spotView, spotScreen, item, spot.X, spot.Y);
                }
            }

            if (!screen.IsUsable)
            {
                if (landedOn is null)
                {
                    unknown.Add(item.Key);
                }

                continue;
            }

            // [S-5526] Every other view that shows all desktop icons, at its first free spot.
            foreach (var view in state.Views)
            {
                if (DesktopViews.Receives(view) && !string.Equals(view.Id, landedOn, StringComparison.Ordinal))
                {
                    Land(view, screenOf?.Invoke(view.Id) is { IsUsable: true } shownOn ? shownOn : screen, item, null, null);
                }
            }
        }

        state.Desktop.Known = [.. plan.Known.Where(key => !unknown.Contains(key))];
        return unplaced;
    }

    /// <summary>
    /// [S-5523] The listed items with no widget in <paramref name="view"/>, in <see cref="DesktopItemOrder"/>:
    /// what Add widget's "On the desktop" offers there (placing one on a Chosen view picks it, [S-5527]); none on a
    /// None view.
    /// </summary>
    public static IReadOnlyList<DesktopItem> Unplaced(ViewState view, IReadOnlyList<DesktopItem> listing)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(listing);

        // [S-5526] A view that shows no desktop icons is offered none.
        if (!DesktopViews.Holds(view))
        {
            return [];
        }

        var placed = DesktopViews.Shown(view);
        return DesktopItemOrder.Sort(listing.Where(item => !placed.Contains(item.Key)));
    }

    /// <summary>[S-5520] The widgets of desktop items (a key), never an own icon's: the reconcile leaves those alone.</summary>
    private static IEnumerable<(WidgetInstance Widget, IDesktopItemConfig Config)> ItemWidgets(AppState state)
    {
        foreach (var view in state.Views)
        {
            foreach (var widget in view.Widgets)
            {
                if (widget.Config is IDesktopItemConfig { Item.Length: > 0 } config)
                {
                    yield return (widget, config);
                }
            }
        }
    }
}
