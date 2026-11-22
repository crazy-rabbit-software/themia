using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Series;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-15500] One repeater of a view: its widget and the config that holds its members.</summary>
public readonly record struct RepeaterOn(WidgetInstance Widget, IRepeater Repeater);

/// <summary>
/// [S-15502] A widget drawn again in a copied row: the template member it copies, where it stands, the value index each
/// enclosing repeater gives it (-1: that repeater has no value), and the repeater a press on it stands for (the outermost
/// one whose copied row it is in, [S-15526]).
/// </summary>
public sealed record RepeaterCopy(string Key, WidgetInstance Template, FracRect Rect, IReadOnlyDictionary<string, int> Path, string Owner);

/// <summary>[S-15520] A repeater with no value: where its sentence stands (the slot after its template row) and what it says.</summary>
public sealed record RepeaterStatus(string RepeaterId, FracRect Rect, Str None);

/// <summary>
/// [S-15502] What a view's repeaters draw besides their real widgets: every copied row's widgets, the value path of each
/// real template member, and each empty repeater's status.
/// </summary>
public sealed record RepeaterPlan(
    IReadOnlyList<RepeaterCopy> Copies,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Templates,
    IReadOnlyList<RepeaterStatus> Statuses,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Values)
{
    public static RepeaterPlan Empty { get; } = new([], new Dictionary<string, IReadOnlyDictionary<string, int>>(), [], new Dictionary<string, IReadOnlyList<string>>());
}

/// <summary>
/// [S-15500]..[S-15532] The repeaters' rules, pure (the owner, 2026-11-21: "it should be a real container, somewhat like the
/// fences widget does it"). A repeater is a widget whose config holds widgets of its view by id (<see cref="IRepeater"/>):
/// its template row. Each member stays a real widget of the view; the repeater draws the row again for each value of its
/// one series (<see cref="Plan"/>), each copy the template's config with the row's value in its bound slots
/// (<see cref="Resolve"/>). Membership is decided by where a widget is let go (<see cref="Land"/>), kept whole at load
/// (<see cref="Repair"/>) and in every change (<see cref="Lay"/>).
/// </summary>
public static class Repeaters
{
    /// <summary>The repeaters of <paramref name="view"/>, in its widgets' order.</summary>
    public static IEnumerable<RepeaterOn> On(ViewState view)
    {
        ArgumentNullException.ThrowIfNull(view);
        foreach (var widget in view.Widgets)
        {
            if (widget.Config is IRepeater repeater)
            {
                yield return new RepeaterOn(widget, repeater);
            }
        }
    }

    /// <summary>
    /// [S-15528] Whether <paramref name="widget"/> can be a member of any repeater: never a fence, a desktop item's widget or
    /// (owner, 2026-11-21: "nesting repeaters in itself should also not be possible anymore") another repeater.
    /// </summary>
    public static bool CanHold(WidgetInstance widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        return widget.Config is not (IHoldsDesktopItems or IDesktopItemConfig or IRepeater);
    }

    /// <summary>
    /// [S-15528] Whether <paramref name="repeater"/> can hold <paramref name="widget"/> (owner, 2026-11-21: "you should not be
    /// able to place any widget on there, there is basically a selection of widgets that make sense in that specific
    /// context"): one that can be a member and takes the repeater's value (a slot of its type: for the forecast, a day).
    /// </summary>
    public static bool CanHold(IRepeater repeater, WidgetInstance widget)
    {
        ArgumentNullException.ThrowIfNull(repeater);
        return CanHold(widget) && repeater.Accepts(widget.Config);
    }

    /// <summary>[S-15500] Each member of a repeater of <paramref name="view"/>, by id, to the repeater that holds it (the first in the view's order).</summary>
    public static Dictionary<string, RepeaterOn> Parents(ViewState view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var parents = new Dictionary<string, RepeaterOn>(StringComparer.Ordinal);
        foreach (var on in On(view))
        {
            foreach (var id in on.Repeater.Members)
            {
                parents.TryAdd(id, on);
            }
        }

        return parents;
    }

    /// <summary>[S-15531] The repeaters that hold <paramref name="widgetId"/>, the nearest first; a cycle ends the chain.</summary>
    public static List<RepeaterOn> Chain(ViewState view, string widgetId) => Chain(Parents(view), widgetId);

    private static List<RepeaterOn> Chain(Dictionary<string, RepeaterOn> parents, string widgetId)
    {
        var chain = new List<RepeaterOn>();
        var seen = new HashSet<string>(StringComparer.Ordinal) { widgetId };
        var id = widgetId;
        while (parents.TryGetValue(id, out var parent) && seen.Add(parent.Widget.Id))
        {
            chain.Add(parent);
            id = parent.Widget.Id;
        }

        return chain;
    }

    /// <summary>[S-15527] Every widget <paramref name="repeaterId"/> holds, its members' members too; the repeater itself not.</summary>
    public static List<string> Descendants(ViewState view, string repeaterId)
    {
        ArgumentNullException.ThrowIfNull(view);
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal) { repeaterId };
        var queue = new Queue<string>([repeaterId]);
        while (queue.Count > 0)
        {
            if (view.Find(queue.Dequeue())?.Config is not IRepeater repeater)
            {
                continue;
            }

            foreach (var id in repeater.Members)
            {
                if (view.Find(id) is not null && seen.Add(id))
                {
                    found.Add(id);
                    queue.Enqueue(id);
                }
            }
        }

        return found;
    }

    /// <summary>[S-15500] [S-15527] <paramref name="ids"/> and everything each of them holds, each once, in that order.</summary>
    public static List<string> WithDescendants(ViewState view, IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(ids);
        var all = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            if (seen.Add(id))
            {
                all.Add(id);
            }

            foreach (var held in Descendants(view, id))
            {
                if (seen.Add(held))
                {
                    all.Add(held);
                }
            }
        }

        return all;
    }

    /// <summary>[S-15525] The outermost repeater that holds <paramref name="widgetId"/>, or null for a widget no repeater holds.</summary>
    public static RepeaterOn? Outermost(ViewState view, string widgetId)
        => Chain(view, widgetId) is { Count: > 0 } chain ? chain[^1] : null;

    /// <summary>The members of <paramref name="on"/> that are widgets of <paramref name="view"/>, in its order.</summary>
    public static List<WidgetInstance> Template(ViewState view, RepeaterOn on)
    {
        ArgumentNullException.ThrowIfNull(view);
        return [.. on.Repeater.Members.Select(view.Find).OfType<WidgetInstance>()];
    }

    /// <summary>[S-15503] The slots of <paramref name="on"/> on <paramref name="screen"/>.</summary>
    public static RepeaterGrid Grid(ViewState view, RepeaterOn on, ScreenMetrics screen)
        => RepeaterGrid.Of(on.Widget.Rect, Template(view, on).Select(w => w.Rect), on.Repeater, screen);

    /// <summary>A bind's key: <c>widgetId/slotKey</c>.</summary>
    public static string BindKey(string widgetId, SeriesSlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);
        return widgetId + "/" + slot.Key;
    }

    /// <summary>[S-15531] The repeater that gives <paramref name="widget"/>'s <paramref name="slot"/> its value: the nearest of its type; null for none.</summary>
    public static RepeaterOn? Binder(ViewState view, WidgetInstance widget, SeriesSlot slot)
    {
        ArgumentNullException.ThrowIfNull(widget);
        ArgumentNullException.ThrowIfNull(slot);
        foreach (var on in Chain(view, widget.Id))
        {
            if (on.Repeater.RepeatBy == slot.Type)
            {
                return on;
            }
        }

        return null;
    }

    /// <summary>
    /// [S-15512] Whether <paramref name="slot"/> of <paramref name="widget"/> takes its repeater's value: there is one of its
    /// type, it was not taken out by hand, and it is the widget's one slot of that type or the one chosen.
    /// </summary>
    public static bool IsBound(ViewState view, WidgetInstance widget, SeriesSlot slot)
        => Binder(view, widget, slot) is { } binder && IsBound(binder, widget, slot);

    private static bool IsBound(RepeaterOn binder, WidgetInstance widget, SeriesSlot slot)
    {
        var key = BindKey(widget.Id, slot);
        if (binder.Repeater.Unbound.Contains(key, StringComparer.Ordinal))
        {
            return false;
        }

        return SeriesSlots.Of(widget.Config, slot.Type).Count == 1 || binder.Repeater.Chosen.Contains(key, StringComparer.Ordinal);
    }

    /// <summary>[S-15512] Every slot of <paramref name="widget"/> a repeater gives its value, with that repeater.</summary>
    public static List<(SeriesSlot Slot, RepeaterOn Binder)> Bound(ViewState view, WidgetInstance widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        var bound = new List<(SeriesSlot, RepeaterOn)>();
        foreach (var slot in SeriesSlots.Of(widget.Config))
        {
            if (Binder(view, widget, slot) is { } binder && IsBound(binder, widget, slot))
            {
                bound.Add((slot, binder));
            }
        }

        return bound;
    }

    /// <summary>[S-15512] Take <paramref name="slot"/> of <paramref name="widget"/> out of its repeater's series: it keeps the value it holds. Whether anything changed.</summary>
    public static bool Unbind(ViewState view, WidgetInstance widget, SeriesSlot slot)
    {
        if (Binder(view, widget, slot) is not { } binder || !IsBound(binder, widget, slot))
        {
            return false;
        }

        var key = BindKey(widget.Id, slot);
        binder.Repeater.Chosen.RemoveAll(k => k == key);
        binder.Repeater.Unbound.Add(key);
        return true;
    }

    /// <summary>
    /// [S-15512] Put <paramref name="slot"/> of <paramref name="widget"/> back in its repeater's series; of a widget with two
    /// slots of the type, this one is the chosen one (the other leaves the series). Whether anything changed.
    /// </summary>
    public static bool Bind(ViewState view, WidgetInstance widget, SeriesSlot slot)
    {
        if (Binder(view, widget, slot) is not { } binder || IsBound(binder, widget, slot))
        {
            return false;
        }

        var key = BindKey(widget.Id, slot);
        binder.Repeater.Unbound.RemoveAll(k => k == key);
        if (SeriesSlots.Of(widget.Config, slot.Type).Count > 1)
        {
            var prefix = widget.Id + "/";
            binder.Repeater.Chosen.RemoveAll(k => k.StartsWith(prefix, StringComparison.Ordinal));
            binder.Repeater.Chosen.Add(key);
        }

        return true;
    }

    /// <summary>[S-15511] The values of each repeater of <paramref name="view"/>, read once.</summary>
    public static Dictionary<string, IReadOnlyList<string>> ValuesOf(ViewState view)
        => On(view).ToDictionary(on => on.Widget.Id, on => SeriesSources.ValuesOf(on.Repeater), StringComparer.Ordinal);

    /// <summary>
    /// [S-15511] [S-15520] <paramref name="config"/> (a member's, at <paramref name="widget"/>) as row <paramref name="path"/>
    /// shows it: a copy with each bound slot set to its repeater's value at that row, or put in its own empty state where that
    /// repeater has none there. The stored config is never touched; a widget with no bound slot comes back as it is.
    /// </summary>
    public static WidgetConfig Resolve(ViewState view, WidgetInstance widget, WidgetConfig config, IReadOnlyDictionary<string, int> path, IReadOnlyDictionary<string, IReadOnlyList<string>> values)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(values);
        var bound = Bound(view, widget);
        var placer = Placer(view, widget, config);
        if (bound.Count == 0 && placer is null)
        {
            return config;
        }

        var resolved = config.Clone();
        if (placer is not null)
        {
            Place(placer, (IPlaced)resolved);
        }

        foreach (var (slot, binder) in bound)
        {
            var index = path.TryGetValue(binder.Widget.Id, out var at) ? at : -1;
            if (values.TryGetValue(binder.Widget.Id, out var list) && index >= 0 && index < list.Count && slot.Write(resolved, list[index]))
            {
                continue;
            }

            slot.Blank(resolved);
        }

        return resolved;
    }

    /// <summary>
    /// [S-15529] The container whose place <paramref name="widget"/> shows (owner, 2026-11-21: "settings like location should
    /// not be settable per widget"): the nearest that holds it and has a place, where its config has one too; else null.
    /// </summary>
    public static IPlaced? Placer(ViewState view, WidgetInstance widget, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(widget);
        return config is IPlaced ? Chain(view, widget.Id).Select(c => c.Repeater).OfType<IPlaced>().FirstOrDefault() : null;
    }

    /// <summary>[S-15529] <paramref name="from"/>'s place written on <paramref name="to"/>: where, its name, the device's or not.</summary>
    public static void Place(IPlaced from, IPlaced to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        to.UseDeviceLocation = from.UseDeviceLocation;
        to.Latitude = from.Latitude;
        to.Longitude = from.Longitude;
        to.PlaceName = from.PlaceName;
    }

    /// <summary>
    /// [S-15511] <paramref name="widget"/>'s config as every row of its repeaters can show it: one resolved copy for each
    /// combination of its binders' values (at most <paramref name="most"/>), or the config alone where no slot is bound. What
    /// the sync keeps fresh for a member: each place's weather, each day's forecast, whether or not a row shows it now.
    /// </summary>
    public static List<WidgetConfig> EveryRow(ViewState view, WidgetInstance widget, IReadOnlyDictionary<string, IReadOnlyList<string>> values, int most = 64)
    {
        ArgumentNullException.ThrowIfNull(widget);
        ArgumentNullException.ThrowIfNull(values);
        var binders = Bound(view, widget).Select(b => b.Binder.Widget.Id).Distinct(StringComparer.Ordinal).ToList();
        if (binders.Count == 0)
        {
            return [Resolve(view, widget, widget.Config, new Dictionary<string, int>(StringComparer.Ordinal), values)];
        }

        var paths = new List<Dictionary<string, int>> { new(StringComparer.Ordinal) };
        foreach (var binder in binders)
        {
            var count = Math.Max(1, values.TryGetValue(binder, out var list) ? list.Count : 0);
            paths = [.. paths.SelectMany(p => Enumerable.Range(0, count).Select(i => new Dictionary<string, int>(p, StringComparer.Ordinal) { [binder] = i })).Take(most)];
        }

        return [.. paths.Select(p => Resolve(view, widget, widget.Config, p, values))];
    }

    /// <summary>
    /// [S-15502] [S-15520] [S-15530] What <paramref name="view"/>'s repeaters draw on <paramref name="screen"/>: for each
    /// outermost repeater, each slot it shows (from its scroll, at most its values; one while it has none), its members
    /// again there (a member that is a repeater laid out there in turn), the real template members' value paths, and the
    /// status of each repeater with no value in the slot after its template.
    /// </summary>
    public static RepeaterPlan Plan(ViewState view, ScreenMetrics screen, IReadOnlyDictionary<string, IReadOnlyList<string>>? values = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        var repeaters = On(view).ToList();
        if (repeaters.Count == 0)
        {
            return RepeaterPlan.Empty;
        }

        values ??= ValuesOf(view);
        var parents = Parents(view);
        var copies = new List<RepeaterCopy>();
        var templates = new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.Ordinal);
        var statuses = new List<RepeaterStatus>();
        foreach (var on in repeaters)
        {
            if (!parents.ContainsKey(on.Widget.Id))
            {
                Expand(on, (0, 0), new Dictionary<string, int>(StringComparer.Ordinal), null, new HashSet<string>(StringComparer.Ordinal));
            }
        }

        return new RepeaterPlan(copies, templates, statuses, values);

        void Expand(RepeaterOn on, (double X, double Y) offset, Dictionary<string, int> path, string? owner, HashSet<string> open)
        {
            if (!open.Add(on.Widget.Id))
            {
                return;
            }

            var grid = Grid(view, on, screen);
            var list = values.TryGetValue(on.Widget.Id, out var v) ? v : [];
            var first = grid.First(on.Repeater.Scroll, list.Count);
            var shown = list.Count == 0 ? 1 : Math.Max(1, Math.Min(grid.Fit, list.Count - first));
            for (var slot = 0; slot < shown; slot++)
            {
                var (dx, dy) = grid.Offset(slot);
                var at = (offset.X + dx, offset.Y + dy);
                var rowPath = new Dictionary<string, int>(path, StringComparer.Ordinal) { [on.Widget.Id] = list.Count == 0 ? -1 : first + slot };
                var rowOwner = owner ?? (slot == 0 ? null : on.Widget.Id);
                foreach (var member in Template(view, on))
                {
                    if (member.Config is IRepeater inner)
                    {
                        Expand(new RepeaterOn(member, inner), at, rowPath, rowOwner, open);
                    }
                    else if (rowOwner is null)
                    {
                        templates[member.Id] = rowPath;
                    }
                    else
                    {
                        var key = member.Id + "@" + string.Join(",", rowPath.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + ":" + p.Value));
                        copies.Add(new RepeaterCopy(key, member, Moved(member.Rect, at), rowPath, rowOwner));
                    }
                }
            }

            if (list.Count == 0 && SeriesSources.For(on.Repeater) is { } source)
            {
                statuses.Add(new RepeaterStatus(on.Widget.Id, Moved(grid.Slot(grid.Fit > 1 ? 1 : 0), offset), source.None));
            }

            open.Remove(on.Widget.Id);
        }
    }

    /// <summary>
    /// [S-15501] [S-15520] What <paramref name="on"/>'s own view draws on <paramref name="screen"/>: each slot it shows (its
    /// outline in edit mode) and, while its source has no value, the source's sentence in the slot after its template; every
    /// box as fractions of its card.
    /// </summary>
    public static RepeaterShown ShownOf(ViewState view, RepeaterOn on, ScreenMetrics screen, IReadOnlyDictionary<string, IReadOnlyList<string>> values, bool editing)
        => ShownOf(view, on, screen, values, editing, out _);

    /// <summary>
    /// [S-13922] The days <paramref name="on"/> shows on <paramref name="screen"/>, each its slot on the display (the first its
    /// day box): what edit mode outlines, on the desktop's own layer, where the cards are.
    /// </summary>
    public static List<FracRect> SlotsOf(ViewState view, RepeaterOn on, ScreenMetrics screen, IReadOnlyDictionary<string, IReadOnlyList<string>> values)
    {
        ShownOf(view, on, screen, values, editing: true, out var slots);
        return slots;
    }

    private static RepeaterShown ShownOf(ViewState view, RepeaterOn on, ScreenMetrics screen, IReadOnlyDictionary<string, IReadOnlyList<string>> values, bool editing, out List<FracRect> onScreen)
    {
        onScreen = [];
        ArgumentNullException.ThrowIfNull(values);
        var box = on.Widget.Rect;
        if (!screen.IsUsable || !(box.W > 0) || !(box.H > 0))
        {
            return new RepeaterShown([], null, null, editing);
        }

        RepeaterBox Inside(FracRect r) => new((r.X - box.X) / box.W, (r.Y - box.Y) / box.H, r.W / box.W, r.H / box.H);

        // [S-15520] Holding nothing yet, it says how it is used, on its top, rather than show nothing (WR-10).
        if (on.Repeater.Members.Count == 0)
        {
            return new RepeaterShown([new RepeaterBox(0, 0, 1, 1)], new RepeaterBox(0, 0, 1, 1), Str.Series_NoMembers.Text(), editing);
        }

        var grid = Grid(view, on, screen);
        var list = values.TryGetValue(on.Widget.Id, out var v) ? v : [];
        var first = grid.First(on.Repeater.Scroll, list.Count);
        var shown = list.Count == 0 ? 1 : Math.Max(1, Math.Min(grid.Fit, list.Count - first));
        onScreen = [.. Enumerable.Range(0, shown).Select(grid.Slot)];
        var slots = onScreen.Select(Inside).ToList();
        if (list.Count > 0 || SeriesSources.For(on.Repeater) is not { } source)
        {
            return new RepeaterShown(slots, null, null, editing);
        }

        return new RepeaterShown(slots, Inside(grid.Slot(grid.Fit > 1 ? 1 : 0)), source.None.Text(), editing);
    }

    /// <summary>
    /// [S-15502] A picture's widgets (a scene, a ready-made group's preview) as the desktop draws them, each paired with the
    /// widget it came from: a member with its first row's config, after it each copy of it (a widget of its own, the row's
    /// config, the row's place), a repeater with what its own view draws; the configs are copies, the widgets given are not
    /// touched. A list with no repeater comes back as it is.
    /// </summary>
    public static List<(WidgetInstance Drawn, WidgetInstance From)> Bake(IReadOnlyList<WidgetInstance> widgets, ScreenMetrics screen, bool editing)
    {
        ArgumentNullException.ThrowIfNull(widgets);
        if (!widgets.Any(w => w.Config is IRepeater) || !screen.IsUsable)
        {
            return [.. widgets.Select(w => (w, w))];
        }

        var view = ViewState.Create("bake");
        view.Widgets.AddRange(widgets);
        var plan = Plan(view, screen);
        var copies = plan.Copies.ToLookup(c => c.Template.Id, StringComparer.Ordinal);
        var baked = new List<(WidgetInstance, WidgetInstance)>(widgets.Count + plan.Copies.Count);
        foreach (var widget in widgets)
        {
            if (widget.Config is IRepeater repeater)
            {
                var shown = widget.Config.Clone();
                ((IRepeater)shown).Shown = ShownOf(view, new RepeaterOn(widget, repeater), screen, plan.Values, editing);
                baked.Add((Drawn(widget, widget.Id, shown, widget.Rect), widget));
                continue;
            }

            var config = plan.Templates.TryGetValue(widget.Id, out var path) ? Pictured(Resolve(view, widget, widget.Config, path, plan.Values), widget) : widget.Config;
            baked.Add((ReferenceEquals(config, widget.Config) ? widget : Drawn(widget, widget.Id, config, widget.Rect), widget));
            foreach (var copy in copies[widget.Id])
            {
                baked.Add((Drawn(widget, copy.Key, Pictured(Resolve(view, widget, widget.Config, copy.Path, plan.Values), widget), copy.Rect), widget));
            }
        }

        return baked;

        // A picture has no device to ask: a part given its container's "my location" is drawn with no place set (its sample's).
        WidgetConfig Pictured(WidgetConfig resolved, WidgetInstance widget)
        {
            if (!ReferenceEquals(resolved, widget.Config) && resolved is IPlaced { UseDeviceLocation: true } placed && Placer(view, widget, widget.Config) is not null)
            {
                placed.UseDeviceLocation = false;
            }

            return resolved;
        }

        static WidgetInstance Drawn(WidgetInstance from, string id, WidgetConfig config, FracRect rect)
        {
            var drawn = new WidgetInstance { Id = id, Config = config };
            drawn.CopyArrangementFrom(from);
            drawn.Rect = rect;
            return drawn;
        }
    }

    private static FracRect Moved(FracRect rect, (double X, double Y) by) => rect with { X = rect.X + by.X, Y = rect.Y + by.Y };

    /// <summary>
    /// [S-15500] [S-15503] [S-15515] [S-15532] Every repeater of <paramref name="view"/> kept whole in a change: a member that is
    /// no longer a widget of the view, can no longer be held, is held twice or would make a cycle leaves; a hand bind of a widget
    /// it no longer holds goes; its box is at least its template row (from its corner); its scroll stays between the top and its
    /// last row. Runs in every change of the state (the host's). Returns whether anything changed.
    /// </summary>
    public static bool Lay(ViewState view, ScreenMetrics screen)
    {
        ArgumentNullException.ThrowIfNull(view);
        var changed = KeepWhole(view, null);
        if (!screen.IsUsable)
        {
            return changed;
        }

        var values = ValuesOf(view);
        var (cols, rows) = GridGeometry.GridDims(screen);
        foreach (var on in On(view).ToList())
        {
            // [S-13912] [S-13916] The day box and its container's tied side: transparent, always (it has no body).
            if (on.Widget.Background.Kind != FillKind.Transparent)
            {
                on.Widget.Background = FillChoice.Transparent();
                changed = true;
            }

            changed |= Tie(view, on, screen);

            var grid = Grid(view, on, screen);
            var box = on.Widget.Rect;
            var fitted = box with { W = Math.Max(box.W, grid.ExtentW), H = Math.Max(box.H, grid.ExtentH) };
            if (on.Repeater.Members.Count > 0 && (fitted.W > box.W + 1e-12 || fitted.H > box.H + 1e-12))
            {
                on.Widget.Rect = fitted;
                grid = Grid(view, on, screen);
                changed = true;
            }

            var count = values.TryGetValue(on.Widget.Id, out var list) ? list.Count : 0;
            var scroll = Math.Clamp(on.Repeater.Scroll, 0, grid.MaxScroll(count));
            if (scroll != on.Repeater.Scroll)
            {
                on.Repeater.Scroll = scroll;
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// [S-13912] The day box of <paramref name="on"/> in whole grid cells on <paramref name="screen"/>: its set size, or where
    /// none is set its parts' reach from its corner; one cell at least.
    /// </summary>
    public static (int W, int H) DaySize(ViewState view, RepeaterOn on, ScreenMetrics screen)
    {
        var (cols, rows) = GridGeometry.GridDims(screen);
        var (reachW, reachH) = Reach(view, on, cols, rows);
        return (Math.Max(1, on.Repeater.DayWidth > 0 ? on.Repeater.DayWidth : reachW), Math.Max(1, on.Repeater.DayHeight > 0 ? on.Repeater.DayHeight : reachH));
    }

    /// <summary>How far <paramref name="on"/>'s parts reach from its corner, in whole cells (0 with none).</summary>
    private static (int W, int H) Reach(ViewState view, RepeaterOn on, int cols, int rows)
    {
        var parts = Template(view, on);
        if (parts.Count == 0)
        {
            return (0, 0);
        }

        var box = on.Widget.Rect;
        return ((int)Math.Ceiling(((parts.Max(p => p.Rect.Right) - box.X) * cols) - 1e-6), (int)Math.Ceiling(((parts.Max(p => p.Rect.Bottom) - box.Y) * rows) - 1e-6));
    }

    /// <summary>
    /// [S-13916] <paramref name="on"/>'s day box set to <paramref name="width"/> × <paramref name="height"/> cells (owner,
    /// 2026-11-21: "when you resize the width the widgets inside get resized accordingly and when you resize the row width the
    /// widgets and the widget gets resized too"; "when resizing the row sometimes a gap appears between two widgets that
    /// previously touched, this should not be"): its parts resized with it as the selection's block is ([S-2460],
    /// <see cref="BlockResize"/>: about its corner, every edge they share kept shared, every edge on a whole cell, each part
    /// in its own sizing; a part at its least or most stops the box there), and its tied side (Rows: its width; Columns: its
    /// height) the day box's. Returns whether anything changed.
    /// </summary>
    public static bool Resize(ViewState view, RepeaterOn on, int width, int height, ScreenMetrics screen)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (!screen.IsUsable)
        {
            return false;
        }

        var (cols, rows) = GridGeometry.GridDims(screen);
        var (w0, h0) = DaySize(view, on, screen);
        var day = (W: Math.Max(1, width), H: Math.Max(1, height));
        var changed = false;
        var box = on.Widget.Rect;
        var parts = Template(view, on);
        if (parts.Count > 0 && day != (w0, h0))
        {
            // The day box itself is the block's first member, free: the block's bounds are the box, its corner the anchor.
            FracRect[] start = [new FracRect(box.X, box.Y, w0 / (double)cols, h0 / (double)rows), .. parts.Select(p => p.Rect)];
            SizeLimits?[] limits = [null, .. parts.Select(p => LimitsOf(p, screen))];
            var laid = BlockResize.Resize(start, limits, ResizeHandle.BottomRight, (day.W - w0) / (double)cols, (day.H - h0) / (double)rows, screen);
            for (var i = 0; i < parts.Count; i++)
            {
                if (parts[i].Rect != laid[i + 1])
                {
                    parts[i].Rect = laid[i + 1];
                    changed = true;
                }
            }

            day = (Math.Max(1, (int)Math.Round(laid[0].W * cols)), Math.Max(1, (int)Math.Round(laid[0].H * rows)));
        }

        if ((on.Repeater.DayWidth, on.Repeater.DayHeight) != day)
        {
            (on.Repeater.DayWidth, on.Repeater.DayHeight) = day;
            changed = true;
        }

        var tied = on.Repeater.Direction switch
        {
            RepeatDirection.Down => box with { W = day.W / (double)cols },
            RepeatDirection.Across => box with { H = day.H / (double)rows },
            _ => box,
        };
        if (tied != box)
        {
            on.Widget.Rect = tied;
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// [S-13915] A container placed bare starts with its first parts (<see cref="IRepeater.FirstParts"/>): side by side from its
    /// corner, each its size in whole cells, its day box where they reach and its tied side with it. Returns the parts made.
    /// </summary>
    public static List<WidgetInstance> Seed(ViewState view, RepeaterOn on, ScreenMetrics screen)
    {
        ArgumentNullException.ThrowIfNull(view);
        var made = new List<WidgetInstance>();
        if (!screen.IsUsable || on.Repeater.Members.Count > 0)
        {
            return made;
        }

        var (cols, rows) = GridGeometry.GridDims(screen);
        var x = 0;
        foreach (var part in on.Repeater.FirstParts)
        {
            if (!ModuleCatalog.TryGet(new WidgetId(part.Kind), out _))
            {
                continue;
            }

            var box = on.Widget.Rect;
            var widget = WidgetInstance.Create(new WidgetId(part.Kind), new FracRect(box.X + (x / (double)cols), box.Y, part.Width / (double)cols, part.Height / (double)rows));
            widget.Align = part.Align;
            view.Widgets.Add(widget);
            on.Repeater.Members.Add(widget.Id);
            made.Add(widget);
            x += part.Width;
        }

        if (made.Count > 0)
        {
            var (w, h) = DaySize(view, on, screen);
            Resize(view, on, w, h, screen);

            // [S-13918] Room for every day it shows.
            FitNatural(view, on, screen);
        }

        return made;
    }

    /// <summary>A part's sizing on <paramref name="screen"/> (its kind's, at its own scale); null for a config no module declares.</summary>
    private static SizeLimits? LimitsOf(WidgetInstance part, ScreenMetrics screen)
        => ModuleCatalog.TryOf(part.Config, out var kind) ? SizeFit.Limits(kind.Sizing(part.Config), screen, WidgetScale.For(part, 1)) : null;

    /// <summary>
    /// [S-13916] The day box and the container's tied side kept one: a part moved or grown past the day box grows it (and the
    /// tied side with it); else a tied side the person resized (the container's width in Rows, its height in Columns) scales
    /// the day box and its parts to it. A container with no part keeps its box.
    /// </summary>
    private static bool Tie(ViewState view, RepeaterOn on, ScreenMetrics screen)
    {
        var (cols, rows) = GridGeometry.GridDims(screen);
        var (reachW, reachH) = Reach(view, on, cols, rows);
        if (reachW == 0)
        {
            return false;
        }

        var (w, h) = DaySize(view, on, screen);
        if (reachW > w || reachH > h)
        {
            // Grown, not scaled: the parts stay where they were put.
            (on.Repeater.DayWidth, on.Repeater.DayHeight) = (Math.Max(w, reachW), Math.Max(h, reachH));
            Tied(view, on, screen, on.Repeater.DayWidth, on.Repeater.DayHeight);
            return true;
        }

        var box = on.Widget.Rect;
        var side = on.Repeater.Direction switch
        {
            RepeatDirection.Down => ((int)Math.Round(box.W * cols), h),
            RepeatDirection.Across => (w, (int)Math.Round(box.H * rows)),
            _ => (w, h),
        };
        return Resize(view, on, side.Item1, side.Item2, screen);
    }

    /// <summary>The tied side set to the day box grown by its parts, its parts where they are.</summary>
    private static bool Tied(ViewState view, RepeaterOn on, ScreenMetrics screen, int width, int height)
    {
        var (cols, rows) = GridGeometry.GridDims(screen);
        var box = on.Widget.Rect;
        var tied = on.Repeater.Direction switch
        {
            RepeatDirection.Down => box with { W = width / (double)cols },
            RepeatDirection.Across => box with { H = height / (double)rows },
            _ => box,
        };
        on.Widget.Rect = tied;
        return tied != box;
    }

    /// <summary>
    /// [S-15500] [S-15532] The membership made whole: members that are widgets of the view, holdable, held once (the first
    /// repeater in the view's order keeps one), no repeater in itself or its ancestor; hand binds only of widgets still held.
    /// <paramref name="said"/>, where given, hears each repair.
    /// </summary>
    private static bool KeepWhole(ViewState view, List<string>? said)
    {
        var changed = false;
        var held = new HashSet<string>(StringComparer.Ordinal);
        var parents = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var on in On(view).ToList())
        {
            on.Repeater.Members ??= [];
            on.Repeater.Unbound ??= [];
            on.Repeater.Chosen ??= [];
            on.Repeater.Picked ??= [];
            var kept = new List<string>(on.Repeater.Members.Count);
            foreach (var id in on.Repeater.Members)
            {
                var widget = view.Find(id);
                if (widget is null || !CanHold(on.Repeater, widget) || id == on.Widget.Id || !held.Add(id) || Cycles(parents, on.Widget.Id, id))
                {
                    continue;
                }

                parents[id] = on.Widget.Id;
                kept.Add(id);
            }

            if (kept.Count != on.Repeater.Members.Count)
            {
                said?.Add($"repeater '{on.Widget.Id}' on view '{view.Id}' held {on.Repeater.Members.Count - kept.Count} widget(s) it cannot (gone, a fence or desktop icon, held twice, itself or its ancestor); dropped");
                on.Repeater.Members = kept;
                changed = true;
            }
        }

        foreach (var on in On(view).ToList())
        {
            var within = new HashSet<string>(Descendants(view, on.Widget.Id), StringComparer.Ordinal);
            bool Stale(string key) => key.IndexOf('/', StringComparison.Ordinal) is var at and > 0
                ? !within.Contains(key[..at]) || SeriesSlots.Find(key[(at + 1)..]) is null
                : true;
            var unbound = on.Repeater.Unbound.RemoveAll(Stale);
            var chosen = on.Repeater.Chosen.RemoveAll(Stale);
            if (unbound + chosen > 0)
            {
                said?.Add($"repeater '{on.Widget.Id}' on view '{view.Id}' named {unbound + chosen} bind(s) of widgets it does not hold; dropped");
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>Whether putting <paramref name="child"/> in <paramref name="parent"/> makes a cycle: the parent is the child or held by it.</summary>
    private static bool Cycles(Dictionary<string, string> parents, string parent, string child)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var id = parent; id is not null && seen.Add(id); id = parents.TryGetValue(id, out var up) ? up : null)
        {
            if (id == child)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// [S-15500] Load: every repeater made whole (<see cref="KeepWhole"/>) and its settings in range (spacing, start and scroll
    /// from 0, a step from 1, a source of its type or none, picked values that read as its type). Each repair is said.
    /// </summary>
    public static IReadOnlyList<string> Repair(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var said = new List<string>();
        foreach (var view in state.Views)
        {
            KeepWhole(view, said);
            foreach (var on in On(view))
            {
                var r = on.Repeater;
                var fixes = new List<string>();
                if (r.Spacing < 0 || r.ColumnSpacing < 0)
                {
                    (r.Spacing, r.ColumnSpacing) = (Math.Max(0, r.Spacing), Math.Max(0, r.ColumnSpacing));
                    fixes.Add("a negative spacing");
                }

                if (r.Start < 0 || r.Step < 1 || r.Scroll < 0)
                {
                    (r.Start, r.Step, r.Scroll) = (Math.Max(0, r.Start), Math.Max(1, r.Step), Math.Max(0, r.Scroll));
                    fixes.Add("a range or scroll out of range");
                }

                if (r.Values is not null && (SeriesSources.Find(r.Values) is not { } source || source.Type != r.RepeatBy))
                {
                    r.Values = null;
                    fixes.Add("a source not of its type");
                }

                if (r.RepeatBy is { } type && r.Picked.RemoveAll(t => !SeriesTokens.Reads(type, t)) > 0)
                {
                    fixes.Add("picked values that do not read");
                }

                if (fixes.Count > 0)
                {
                    said.Add($"repeater '{on.Widget.Id}' on view '{view.Id}': {string.Join(", ", fixes)}; repaired");
                }
            }
        }

        return said;
    }

    /// <summary>
    /// [S-15526] [S-15528] A move of <paramref name="moved"/> (widget ids of <paramref name="view"/>, at the rects they landed on)
    /// let go. Each moved widget that is not carried with a repeater that holds it: let go on a repeater's box (the innermost under
    /// its centre that can hold it, never itself or one it holds) it is that repeater's, put on the template row at the same place
    /// within the slot it landed on; let go anywhere else it leaves the repeater that held it, its bound slots keeping the value of
    /// that repeater's first row. Members join at the end of the template. Returns whether membership or a place changed.
    /// </summary>
    public static bool Land(ViewState view, IReadOnlyCollection<string> moved, ScreenMetrics screen) => Land(view, moved, screen, freeze: true);

    /// <summary>
    /// [S-15533] A move under way, drawn as if let go now (owner, 2026-11-21: "they should immeadiately update the repeated
    /// items when i move something"): <paramref name="view"/> with the moving widgets at <paramref name="landed"/> (id to the
    /// rect the block lands on), landed (<see cref="Land"/>: joining or leaving a repeater) and laid (<see cref="Lay"/>), and
    /// what its repeaters would draw. Nothing of <paramref name="view"/> changes: the widgets are new instances, each
    /// repeater's config a clone, every other config the same object (a copy's card keeps its view) and never written (a
    /// widget leaving keeps its slots until the drop).
    /// </summary>
    public static (ViewState View, RepeaterPlan Plan) Preview(ViewState view, IReadOnlyDictionary<string, FracRect> landed, ScreenMetrics screen,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? values = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(landed);
        var scratch = new ViewState
        {
            Id = view.Id,
            Name = view.Name,
            Widgets = [.. view.Widgets.Select(w =>
            {
                var copy = new WidgetInstance { Id = w.Id, Config = w.Config is IRepeater ? w.Config.Clone() : w.Config };
                copy.CopyArrangementFrom(w);
                copy.Rect = landed.TryGetValue(w.Id, out var at) ? at : w.Rect;
                return copy;
            })],
        };

        Land(scratch, [.. landed.Keys], screen, freeze: false);
        Lay(scratch, screen);
        return (scratch, Plan(scratch, screen, values));
    }

    private static bool Land(ViewState view, IReadOnlyCollection<string> moved, ScreenMetrics screen, bool freeze)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(moved);
        if (!screen.IsUsable || (!view.Widgets.Exists(w => w.Config is IRepeater)))
        {
            return false;
        }

        var set = new HashSet<string>(moved, StringComparer.Ordinal);
        var parents = Parents(view);
        var changed = false;
        var values = ValuesOf(view);
        foreach (var id in moved)
        {
            if (view.Find(id) is not { } widget || !CanHold(widget))
            {
                continue;
            }

            var chain = Chain(parents, id);
            if (chain.Exists(p => set.Contains(p.Widget.Id)))
            {
                continue;
            }

            var (cx, cy) = (widget.Rect.X + (widget.Rect.W / 2), widget.Rect.Y + (widget.Rect.H / 2));
            var target = Under(view, widget, cx, cy, screen);
            var parent = chain.Count > 0 ? chain[0] : (RepeaterOn?)null;
            if (target is null)
            {
                if (parent is { } from)
                {
                    if (freeze)
                    {
                        Freeze(view, widget, values);
                    }

                    from.Repeater.Members.Remove(id);
                    parents = Parents(view);
                    changed = true;
                }

                continue;
            }

            // The same place in the day box as in the day it was let go on, never before the container's corner (the day box
            // is measured from it).
            var (into, grid, slot) = target.Value;
            var (dx, dy) = grid.Offset(slot);
            var landed = widget.Rect with { X = Math.Max(into.Widget.Rect.X, widget.Rect.X - dx), Y = Math.Max(into.Widget.Rect.Y, widget.Rect.Y - dy) };
            if (landed != widget.Rect)
            {
                widget.Rect = landed;
                changed = true;
            }

            if (parent is { } was && was.Widget.Id == into.Widget.Id)
            {
                continue;
            }

            if (parent is { } old)
            {
                if (freeze)
                {
                    Freeze(view, widget, values);
                }

                old.Repeater.Members.Remove(id);
            }

            into.Repeater.Members.Add(id);
            parents = Parents(view);
            changed = true;
        }

        return changed;
    }

    /// <summary>The innermost repeater whose box holds the point that can take <paramref name="widget"/>, its slots and the slot there.</summary>
    private static (RepeaterOn On, RepeaterGrid Grid, int Slot)? Under(ViewState view, WidgetInstance widget, double x, double y, ScreenMetrics screen)
    {
        var own = new HashSet<string>(Descendants(view, widget.Id), StringComparer.Ordinal) { widget.Id };
        var parents = Parents(view);
        (RepeaterOn On, RepeaterGrid Grid, int Slot)? best = null;
        var bestDepth = -1;
        foreach (var on in On(view))
        {
            if (own.Contains(on.Widget.Id) || !CanHold(on.Repeater, widget))
            {
                continue;
            }

            var grid = Grid(view, on, screen);
            var slot = grid.SlotAt(x, y);
            var depth = Chain(parents, on.Widget.Id).Count;
            if (slot >= 0 && depth > bestDepth)
            {
                (best, bestDepth) = ((on, grid, slot), depth);
            }
        }

        return best;
    }

    /// <summary>[S-15526] A widget leaving its repeaters keeps, in each bound slot, the value its repeater's first row shows.</summary>
    private static void Freeze(ViewState view, WidgetInstance widget, IReadOnlyDictionary<string, IReadOnlyList<string>> values)
    {
        if (Placer(view, widget, widget.Config) is { } placer)
        {
            Place(placer, (IPlaced)widget.Config);
        }

        foreach (var (slot, binder) in Bound(view, widget))
        {
            if (values.TryGetValue(binder.Widget.Id, out var list) && list.Count > 0)
            {
                slot.Write(widget.Config, list[Math.Clamp(binder.Repeater.Scroll, 0, list.Count - 1)]);
            }
        }
    }

    /// <summary>
    /// [S-15500] Copies of widgets (a paste, a duplicated view) under new ids: each copied repeater's members, hand binds and
    /// choices renamed through <paramref name="ids"/> (old id to new); a member left out of the copy is left out of it.
    /// </summary>
    public static void Remap(IEnumerable<WidgetInstance> copies, IReadOnlyDictionary<string, string> ids)
    {
        ArgumentNullException.ThrowIfNull(copies);
        ArgumentNullException.ThrowIfNull(ids);
        foreach (var copy in copies)
        {
            if (copy.Config is not IRepeater repeater)
            {
                continue;
            }

            repeater.Members = [.. repeater.Members.Where(ids.ContainsKey).Select(id => ids[id])];
            repeater.Unbound = Renamed(repeater.Unbound);
            repeater.Chosen = Renamed(repeater.Chosen);
        }

        List<string> Renamed(List<string> keys) =>
        [
            .. keys.Select(k => k.IndexOf('/', StringComparison.Ordinal) is var at and > 0 && ids.TryGetValue(k[..at], out var to) ? to + k[at..] : null)
                .OfType<string>(),
        ];
    }

    /// <summary>[S-15514] The settings that make a repeater's natural size: what it repeats by and where its values come from.</summary>
    public static string SourceNote(IRepeater repeater)
    {
        ArgumentNullException.ThrowIfNull(repeater);
        return string.Join('\u001F', repeater.RepeatBy?.ToString() ?? "-", repeater.Values ?? "-", repeater.Start, repeater.Step, repeater.Direction, string.Join('\u001E', repeater.Picked));
    }

    /// <summary>
    /// [S-15514] <paramref name="on"/> put at its natural on <paramref name="screen"/> for the values it has now (a change of what it
    /// repeats by or where the values come from is a config change, [S-14503]). Whether its box changed.
    /// </summary>
    public static bool FitNatural(ViewState view, RepeaterOn on, ScreenMetrics screen)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (!screen.IsUsable || on.Repeater.Members.Count == 0)
        {
            return false;
        }

        var natural = Grid(view, on, screen).Natural(SeriesSources.ValuesOf(on.Repeater).Count);
        if (Math.Abs(natural.W - on.Widget.Rect.W) < 1e-12 && Math.Abs(natural.H - on.Widget.Rect.H) < 1e-12)
        {
            return false;
        }

        on.Widget.Rect = natural;
        on.Repeater.Scroll = 0;
        return true;
    }
}
