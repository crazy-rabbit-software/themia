using Themia.Core.Geometry;

namespace Themia.Core.Model;

/// <summary>
/// One connected display, as the display pass sees it.
/// </summary>
/// <param name="Id">[S-842] The stable id every per-display setting is keyed by.</param>
/// <param name="IdResolved">
/// [S-843] False when <paramref name="Id"/> is only the GDI ordinal because the monitor's device path
/// could not be read (see <see cref="DisplayIdentity.Resolve"/>).
/// </param>
/// <param name="Bounds">The whole monitor, in physical pixels. The surface covers exactly this ([S-840]).</param>
/// <param name="WorkArea">The monitor less the taskbar and other app bars, in physical pixels ([S-841]).</param>
/// <param name="IsPrimary">Whether this is the primary display.</param>
public readonly record struct DisplayInfo(string Id, bool IdResolved, PixelRect Bounds, PixelRect WorkArea, bool IsPrimary);

/// <summary>
/// [S-842] Which string names a display for good.
/// <para>
/// <c>\\.\DISPLAYn</c> is an ordinal, not an identity: Windows hands the numbers out again on every
/// topology change, so a monitor that was powered off overnight can come back as another number and
/// take the other screen's view, scale and on/off switch with it. The monitor's device interface path
/// (<c>\\?\DISPLAY#DEL40F6#5&amp;…&amp;UID4357#{e6f07b5f-…}</c>) names the panel on its connector, and
/// does not move.
/// </para>
/// </summary>
public static class DisplayIdentity
{
    private const string DevicePathPrefix = @"\\?\";

    /// <summary>
    /// The id for a monitor: its device path when it has one, otherwise its GDI name, flagged as
    /// unresolved ([S-843]). Lower-cased, because the same path has been reported in different cases
    /// by different calls and a key that changes case is a different key.
    /// </summary>
    public static (string Id, bool Resolved) Resolve(string? devicePath, string gdiName)
    {
        var path = devicePath?.Trim();
        if (!string.IsNullOrEmpty(path)
            && path.Length > DevicePathPrefix.Length
            && path.StartsWith(DevicePathPrefix, StringComparison.Ordinal))
        {
            return (path.ToLowerInvariant(), true);
        }

        return (string.IsNullOrWhiteSpace(gdiName) ? "display" : gdiName, false);
    }

    /// <summary>
    /// [S-843] The ids of one enumeration, no two alike: when monitors report the same device path,
    /// the first keeps it and every later one falls back to its own GDI name, unresolved. Without
    /// this the display pass keeps one of them and the other screen shows no widgets at all (classic
    /// #176, two monitors of the same make and model).
    /// </summary>
    public static List<(string Id, bool Resolved)> Separate(IReadOnlyList<(string Id, bool Resolved, string GdiName)> monitors)
    {
        ArgumentNullException.ThrowIfNull(monitors);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ids = new List<(string, bool)>(monitors.Count);

        foreach (var (id, resolved, gdi) in monitors)
        {
            ids.Add(seen.Add(id) || !resolved ? (id, resolved) : Resolve(null, gdi));
        }

        return ids;
    }
}

/// <summary>What one display pass has to do to the surfaces.</summary>
/// <param name="Enabled">The displays that should have a surface, after [S-845].</param>
/// <param name="Close">Ids of surfaces to close: gone, disabled, or re-identified.</param>
/// <param name="Create">Displays that need a surface and have none.</param>
/// <param name="Update">Displays whose surface stays but whose bounds, work area or primary flag changed.</param>
public sealed record DisplayPass(
    IReadOnlyList<DisplayInfo> Enabled,
    IReadOnlyList<string> Close,
    IReadOnlyList<DisplayInfo> Create,
    IReadOnlyList<DisplayInfo> Update)
{
    public bool IsEmpty => Close.Count == 0 && Create.Count == 0 && Update.Count == 0;
}

/// <summary>
/// [S-844] The one plan for which display gets a surface. Startup, a display change, the taskbar
/// moving and the per-display switch all go through <see cref="Reconcile"/>, keyed on the stable id.
/// </summary>
public static class DisplayPlan
{
    /// <summary>
    /// Drop repeated ids, keeping the first. Two monitors reporting one id would otherwise share a
    /// surface slot, and one of them would silently show nothing.
    /// </summary>
    public static List<DisplayInfo> Distinct(IReadOnlyList<DisplayInfo> connected)
    {
        ArgumentNullException.ThrowIfNull(connected);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        return [.. connected.Where(d => !string.IsNullOrEmpty(d.Id) && seen.Add(d.Id))];
    }

    /// <summary>
    /// [S-845] The connected displays Themia shows on: every one not switched off. If that leaves
    /// none — the only enabled display was unplugged — the primary is shown anyway, without changing
    /// the setting: with no surface there is no edit mode and no way back to Settings.
    /// </summary>
    public static List<DisplayInfo> Enabled(IReadOnlyList<DisplayInfo> connected, IReadOnlyCollection<string> disabled)
    {
        ArgumentNullException.ThrowIfNull(disabled);

        var all = Distinct(connected);
        var enabled = all.Where(d => !disabled.Contains(d.Id)).ToList();

        if (enabled.Count == 0 && all.Count > 0)
        {
            enabled.Add(all.Find(d => d.IsPrimary) is { IsPrimary: true } primary ? primary : all[0]);
        }

        return enabled;
    }

    /// <summary>[S-845] Whether a display may be switched off: only while another connected display stays on.</summary>
    public static bool CanDisable(IReadOnlyList<DisplayInfo> connected, IReadOnlyCollection<string> disabled, string id)
    {
        ArgumentNullException.ThrowIfNull(disabled);

        return Distinct(connected).Any(d => d.Id != id && !disabled.Contains(d.Id));
    }

    /// <summary>
    /// Compare the surfaces that exist with the displays that should have one.
    /// </summary>
    /// <param name="surfaces">Each live surface's id and the display it was last placed for.</param>
    /// <param name="connected">The displays Windows reports now. Empty means "unknown", not "none" ([S-846]).</param>
    /// <param name="disabled">The ids switched off ([S-845]).</param>
    public static DisplayPass Reconcile(
        IReadOnlyDictionary<string, DisplayInfo> surfaces,
        IReadOnlyList<DisplayInfo> connected,
        IReadOnlyCollection<string> disabled)
    {
        ArgumentNullException.ThrowIfNull(surfaces);
        ArgumentNullException.ThrowIfNull(connected);

        if (connected.Count == 0)
        {
            // [S-846] An empty enumeration is a display driver mid-change, not a desk with no screens.
            // Building nothing and closing nothing leaves it to the next display message.
            return new DisplayPass([], [], [], []);
        }

        var enabled = Enabled(connected, disabled);
        var wanted = enabled.ToDictionary(d => d.Id, StringComparer.Ordinal);

        var close = surfaces.Keys.Where(id => !wanted.ContainsKey(id)).Order(StringComparer.Ordinal).ToList();
        var create = enabled.Where(d => !surfaces.ContainsKey(d.Id)).ToList();
        var update = enabled.Where(d => surfaces.TryGetValue(d.Id, out var was) && was != d).ToList();

        return new DisplayPass(enabled, close, create, update);
    }

    /// <summary>
    /// [S-843] The displays that may be given a view assignment. A display whose id did not resolve
    /// is only included when it already has one: a real panel answers with its ordinal for a moment
    /// while it wakes, and interning that ordinal would store a binding under a key the next display
    /// change abandons. A permanently path-less display (a virtual or remote one) keeps the binding
    /// it was given.
    /// </summary>
    public static List<string> ViewKeys(IReadOnlyList<DisplayInfo> enabled, IReadOnlyDictionary<string, string> monitorViews)
    {
        ArgumentNullException.ThrowIfNull(enabled);
        ArgumentNullException.ThrowIfNull(monitorViews);

        return [.. enabled.Where(d => d.IdResolved || monitorViews.ContainsKey(d.Id)).Select(d => d.Id)];
    }

    /// <summary>
    /// [S-847]/[S-1705] Give every display in <paramref name="displayIds"/> that has no view (or one
    /// naming a deleted view) an existing view: the one chosen for new displays
    /// (<see cref="AppState.DefaultViewForNewDisplays"/>), else the active view — the last one the
    /// user showed anywhere ([S-19]). A display that has a view keeps it.
    /// <para>
    /// Never creates a view. Views are made by the user only; a monitor plugged in used to get an
    /// empty "Display n" view of its own, which the user had to find and delete. The assignment is
    /// stored rather than left to fall back to the active view, so switching another display later
    /// does not drag this one along.
    /// </para>
    /// </summary>
    /// <returns>How many displays were given a view, so the caller knows whether to save.</returns>
    public static int AssignViews(AppState state, IReadOnlyList<string> displayIds)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(displayIds);

        var assigned = 0;
        foreach (var display in displayIds)
        {
            if (state.MonitorViews.TryGetValue(display, out var existing) && state.FindView(existing) is not null)
            {
                continue;
            }

            var view = state.FindView(state.DefaultViewForNewDisplays) ?? state.ActiveView();
            state.MonitorViews[display] = view.Id;
            assigned++;
        }

        return assigned;
    }

    /// <summary>
    /// [S-843] The ordinal fallback keys that now belong to a resolved id: one per connected display
    /// that reports a device path, from the id it had while it reported none (its GDI ordinal) to that
    /// path. A display whose ordinal appears twice is skipped after the first, as Windows lists them.
    /// </summary>
    /// <param name="connected">Each connected display with its GDI ordinal (<c>\\.\DISPLAY1</c>).</param>
    public static List<DisplayRekey> Rekeys(IReadOnlyList<(string Ordinal, DisplayInfo Display)> connected)
    {
        ArgumentNullException.ThrowIfNull(connected);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var rekeys = new List<DisplayRekey>();
        foreach (var (ordinal, display) in connected)
        {
            if (!display.IdResolved || string.IsNullOrEmpty(display.Id))
            {
                continue;
            }

            // Exactly the id the display had while unresolved, so the key matches what was stored.
            var fallback = DisplayIdentity.Resolve(null, ordinal).Id;
            if (fallback != display.Id && seen.Add(fallback))
            {
                rekeys.Add(new DisplayRekey(fallback, display.Id));
            }
        }

        return rekeys;
    }

    /// <summary>
    /// [S-843] "Its ordinal until it reports one": when a display that ran under its ordinal reports
    /// its device path, everything stored under the ordinal moves to the path — its view, its on/off
    /// switch, its scale and its view bar — so the screen looks the same before and after.
    /// <para>
    /// Per setting, the ordinal's entry replaces the path's: it is what the screen showed while it
    /// was keyed by the ordinal, and the path's entry was invisible in that time. An ordinal with no
    /// entry moves nothing, so a panel that answered with its ordinal for a moment while waking (and
    /// was never given a view, see <see cref="ViewKeys"/>) keeps its own settings.
    /// </para>
    /// </summary>
    /// <returns>Whether anything moved, i.e. whether the state needs saving.</returns>
    public static bool AdoptResolvedIds(AppState state, IReadOnlyList<DisplayRekey> rekeys)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(rekeys);

        var changed = false;
        foreach (var (from, to) in rekeys)
        {
            changed |= Move(state.MonitorViews, from, to);
            changed |= Move(state.MonitorScales, from, to);
            changed |= Move(state.ViewBars, from, to);

            if (state.DisabledMonitors.RemoveAll(id => id == from) > 0)
            {
                if (!state.DisabledMonitors.Contains(to))
                {
                    state.DisabledMonitors.Add(to);
                }

                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// [S-843] The live surfaces to re-key in place: those still under an ordinal that now resolved,
    /// unless a surface under the resolved id already exists (then the plan closes the stale one).
    /// Re-keyed rather than replaced, so the display's widgets are not restarted.
    /// </summary>
    public static List<DisplayRekey> SurfaceRekeys(IReadOnlyCollection<string> surfaceIds, IReadOnlyList<DisplayRekey> rekeys)
    {
        ArgumentNullException.ThrowIfNull(surfaceIds);
        ArgumentNullException.ThrowIfNull(rekeys);

        return [.. rekeys.Where(r => surfaceIds.Contains(r.From) && !surfaceIds.Contains(r.To))];
    }

    private static bool Move<T>(Dictionary<string, T> map, string from, string to)
    {
        if (!map.Remove(from, out var value))
        {
            return false;
        }

        map[to] = value;
        return true;
    }
}

/// <summary>[S-843] A display's settings key moving from its ordinal fallback to its resolved device path.</summary>
/// <param name="From">The ordinal id the display was keyed by while it reported no device path.</param>
/// <param name="To">The resolved id ([S-842]).</param>
public readonly record struct DisplayRekey(string From, string To);
