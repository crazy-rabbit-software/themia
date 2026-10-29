using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>[S-5560] A view shown now on a connected display, with that display's metrics (DIPs and work area).</summary>
public readonly record struct DesktopShownView(string ViewId, ScreenMetrics Screen);

/// <summary>[S-5560] A stand-in item widget to lay into the primary display's view: its item and its cell.</summary>
public readonly record struct DesktopFallbackAdd(string ViewId, DesktopItem Item, FracRect Rect);

/// <summary>
/// [S-5560]..[S-5562] What the displays as they are now change in the item widgets, worked out before
/// anything is mutated: stand-ins to lay in for a display unplugged, stand-ins to take away for one
/// back, item widgets to move off a work area that shrank, and the stand-in records once applied.
/// </summary>
public sealed record DesktopDisplayPlan(
    IReadOnlyList<DesktopFallbackAdd> Adds,
    IReadOnlySet<string> Removes,
    IReadOnlyList<(string WidgetId, FracRect Rect)> Moves,
    IReadOnlyList<DesktopFallback> Kept)
{
    /// <summary>Nothing to do: no widget laid in, taken away or moved, and no record dropped.</summary>
    public bool Changes(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Adds.Count > 0 || Removes.Count > 0 || Moves.Count > 0 || Kept.Count != (state.Desktop.Fallbacks?.Count ?? 0);
    }
}

/// <summary>
/// [S-5560]..[S-5562] The desktop's items across displays that come and go and work areas that change,
/// as pure rules. A view belongs to the displays that show it ([S-18], [S-19]); an item placed only in
/// the view of a display that is unplugged would be on no screen, so it gets a stand-in in the
/// primary display's view while that display is away, and the stand-in goes when the view shows the
/// item again. The view itself is never touched: its item widgets keep their cells for its return.
/// </summary>
public static class DesktopDisplays
{
    private const double Epsilon = 1e-9;

    /// <summary>
    /// [S-5560] [S-5561] [S-5562] The plan for the displays now.
    /// <paramref name="connected"/> are the ids of every connected display (switched off for Themia or
    /// not); <paramref name="shown"/> the views the live surfaces show; <paramref name="primary"/> the
    /// primary display's (null while it has no surface); <paramref name="listing"/> the desktop's items
    /// as last read; <paramref name="cellDip"/> the item widget's cell.
    /// <list type="number">
    /// <item>A stand-in that is gone, shows another item now or was moved off its cell is the user's
    /// own from then on (its record goes, the widget stays); one whose item another shown view shows
    /// again (its display is back) is taken away.</item>
    /// <item>The views of displays not connected (their stored assignments, [S-18]) that no live surface
    /// shows are stranded: each listed item placed there and in no shown view gets a stand-in in the
    /// primary display's view at its next free cell ([S-5521]), in the stranded view's own order (down
    /// each column, then the next).</item>
    /// <item>In every shown view, an item widget no longer whole inside its display's work area (the
    /// taskbar moved, grew or stopped hiding) moves to the free cell nearest after where it was
    /// (<see cref="DesktopCells.FreeFrom"/>), as Explorer keeps its icons off the taskbar; a work area that
    /// grows moves nothing.</item>
    /// </list>
    /// Pure: nothing in <paramref name="state"/> changes.
    /// </summary>
    public static DesktopDisplayPlan Plan(
        AppState state,
        IReadOnlyCollection<string> connected,
        IReadOnlyList<DesktopShownView> shown,
        DesktopShownView? primary,
        IReadOnlyList<DesktopItem> listing,
        (double Width, double Height) cellDip)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(connected);
        ArgumentNullException.ThrowIfNull(shown);
        ArgumentNullException.ThrowIfNull(listing);

        var shownIds = new HashSet<string>(shown.Select(s => s.ViewId), StringComparer.Ordinal);
        var removes = new HashSet<string>(StringComparer.Ordinal);
        var kept = new List<DesktopFallback>();

        // (1) The records first: a stand-in whose item is back in a shown view goes.
        foreach (var record in state.Desktop.Fallbacks ?? [])
        {
            var widget = state.FindView(record.View)?.Widgets.Find(w => string.Equals(w.Id, record.Widget, StringComparison.Ordinal));
            if (widget?.Config is not IDesktopItemConfig config || !DesktopItemKeys.Comparer.Equals(config.Item, record.Item) || !Same(widget.Rect, record.Rect))
            {
                continue;
            }

            var back = shown.Any(s => !string.Equals(s.ViewId, record.View, StringComparison.Ordinal)
                && Holds(state.FindView(s.ViewId), record.Item, except: null));
            if (back)
            {
                removes.Add(record.Widget);
                continue;
            }

            kept.Add(record);
        }

        // (2) Stranded views: their items, shown nowhere, get stand-ins on the primary display.
        var adds = new List<DesktopFallbackAdd>();
        if (primary is { Screen.IsUsable: true } main && state.FindView(main.ViewId) is { } target)
        {
            var listed = new Dictionary<string, DesktopItem>(DesktopItemKeys.Comparer);
            foreach (var item in listing)
            {
                listed.TryAdd(item.Key, item);
            }

            var connectedIds = new HashSet<string>(connected, StringComparer.Ordinal);
            var stranded = state.MonitorViews
                .Where(pair => !connectedIds.Contains(pair.Key) && !shownIds.Contains(pair.Value))
                .Select(pair => pair.Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .Select(state.FindView)
                .OfType<ViewState>();

            var cell = new FracRect(0, 0, cellDip.Width / main.Screen.Width, cellDip.Height / main.Screen.Height);
            var occupied = target.Widgets.Where(w => !removes.Contains(w.Id)).Select(w => w.Rect).ToList();
            var added = new HashSet<string>(DesktopItemKeys.Comparer);
            foreach (var view in stranded)
            {
                foreach (var widget in view.Widgets.OrderBy(w => Math.Round(w.Rect.X, 6)).ThenBy(w => Math.Round(w.Rect.Y, 6)))
                {
                    if (widget.Config is not IDesktopItemConfig { Item: { Length: > 0 } key }
                        || !listed.TryGetValue(key, out var item)
                        || added.Contains(key)
                        || shown.Any(s => Holds(state.FindView(s.ViewId), key, except: removes)))
                    {
                        continue;
                    }

                    if (DesktopGrid.NextFree(occupied, cell, main.Screen.Allowed) is not { } free)
                    {
                        continue;
                    }

                    adds.Add(new DesktopFallbackAdd(target.Id, item, free));
                    occupied.Add(free);
                    added.Add(key);
                }
            }
        }

        // (3) The work area: an item widget the taskbar now covers moves to a free cell inside it.
        var moves = new List<(string, FracRect)>();
        var fitted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var view in shown.Where(s => s.Screen.IsUsable))
        {
            if (!fitted.Add(view.ViewId) || state.FindView(view.ViewId) is not { } held)
            {
                continue;
            }

            var area = view.Screen.Allowed;
            var occupied = held.Widgets.Where(w => !removes.Contains(w.Id)).Select(w => w.Rect).ToList();
            occupied.AddRange(adds.Where(a => a.ViewId == held.Id).Select(a => a.Rect));
            foreach (var widget in held.Widgets)
            {
                if (removes.Contains(widget.Id) || widget.Config is not IDesktopItemConfig || Inside(widget.Rect, area))
                {
                    continue;
                }

                occupied.Remove(widget.Rect);
                var x = Math.Clamp(widget.Rect.X + (widget.Rect.W / 2), area.X, area.Right - Epsilon);
                var y = Math.Clamp(widget.Rect.Y + (widget.Rect.H / 2), area.Y, area.Bottom - Epsilon);
                var size = new FracRect(0, 0, widget.Rect.W, widget.Rect.H);
                if (DesktopCells.FreeFrom(occupied, x, y, size, area) is { } spot)
                {
                    moves.Add((widget.Id, spot));
                    occupied.Add(spot);
                }
                else
                {
                    // No room inside: it stays where it was rather than cover another.
                    occupied.Add(widget.Rect);
                }
            }
        }

        return new DesktopDisplayPlan(adds, removes, moves, kept);
    }

    /// <summary>
    /// [S-5560]..[S-5562] Carry out <paramref name="plan"/>: stand-ins taken away, widgets moved, new
    /// stand-ins made by <paramref name="create"/> and recorded with their cells.
    /// </summary>
    public static void Apply(AppState state, DesktopDisplayPlan plan, Func<DesktopItem, FracRect, WidgetInstance> create)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(create);

        if (plan.Removes.Count > 0)
        {
            foreach (var view in state.Views)
            {
                view.Widgets.RemoveAll(w => plan.Removes.Contains(w.Id));
            }
        }

        var byId = state.Views.SelectMany(v => v.Widgets).GroupBy(w => w.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (var (id, rect) in plan.Moves)
        {
            if (byId.TryGetValue(id, out var widget))
            {
                widget.Rect = rect;

                // A stand-in moved by the system keeps its record, at its new cell.
                if (plan.Kept.FirstOrDefault(r => r.Widget == id) is { } record)
                {
                    record.Rect = rect;
                }
            }
        }

        var records = new List<DesktopFallback>(plan.Kept);
        foreach (var add in plan.Adds)
        {
            if (state.FindView(add.ViewId) is not { } view)
            {
                continue;
            }

            var widget = create(add.Item, add.Rect);
            view.Widgets.Add(widget);
            records.Add(new DesktopFallback { Widget = widget.Id, View = view.Id, Item = add.Item.Key, Rect = add.Rect });
        }

        state.Desktop.Fallbacks = records.Count > 0 ? records : null;
    }

    /// <summary>
    /// [S-5560] Keep the stand-in records whole at load: one with an empty id, view or key, or a cell that
    /// is no finite rect, is dropped loudly; an empty list is no list.
    /// </summary>
    public static IReadOnlyList<string> RepairFallbacks(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Desktop?.Fallbacks is not { } records)
        {
            return [];
        }

        var kept = records.Where(r => r is not null
            && !string.IsNullOrWhiteSpace(r.Widget) && !string.IsNullOrWhiteSpace(r.View) && DesktopItemKeys.IsValid(r.Item)
            && double.IsFinite(r.Rect.X) && double.IsFinite(r.Rect.Y) && double.IsFinite(r.Rect.W) && double.IsFinite(r.Rect.H)
            && r.Rect.W > 0 && r.Rect.H > 0).ToList();
        var dropped = records.Count - kept.Count;
        state.Desktop.Fallbacks = kept.Count > 0 ? kept : null;
        return dropped > 0 ? [$"{dropped} desktop stand-in record(s) were incomplete; dropped"] : [];
    }

    private static bool Holds(ViewState? view, string key, IReadOnlySet<string>? except)
        => view is not null && view.Widgets.Any(w => (except is null || !except.Contains(w.Id))
            && w.Config is IDesktopItemConfig c && DesktopItemKeys.Comparer.Equals(c.Item, key));

    private static bool Same(FracRect a, FracRect b)
        => Math.Abs(a.X - b.X) < 1e-6 && Math.Abs(a.Y - b.Y) < 1e-6 && Math.Abs(a.W - b.W) < 1e-6 && Math.Abs(a.H - b.H) < 1e-6;

    private static bool Inside(FracRect r, FracRect area)
        => r.X >= area.X - Epsilon && r.Y >= area.Y - Epsilon && r.Right <= area.Right + Epsilon && r.Bottom <= area.Bottom + Epsilon;
}
