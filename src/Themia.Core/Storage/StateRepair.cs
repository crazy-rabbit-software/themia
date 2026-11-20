using Themia.Core.Model;

namespace Themia.Core.Storage;

/// <summary>
/// [S-1050] The walk that brings a read state back inside its invariants, reporting what had to change. Pure and
/// total: it repairs rather than rejects, because a half-valid layout is still the user's layout. Each type repairs
/// itself (<see cref="DesignState.Repair"/>, <see cref="AppPreferences.Repair"/>, <see cref="ViewState.Repair"/> with
/// its widgets', <see cref="ViewBarSettings.Repair"/>, <see cref="DesktopState.Repair"/>); the walk holds what spans
/// them: a missing part, ids unique among the views, what points at a view, the display settings and the desktop's
/// rules over the views.
/// </summary>
public static class StateRepair
{
    /// <summary>Bring a deserialised document back inside its invariants, reporting what had to change.</summary>
    public static List<string> Repair(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var repairs = new List<string>();

        state.Design ??= new DesignState();
        state.Views ??= [];
        state.MonitorViews ??= [];

        repairs.AddRange(state.Design.Repair());

        if (state.Preferences is null)
        {
            // A hand-edited null: repair to the defaults, loudly.
            state.Preferences = new AppPreferences();
            repairs.Add("preferences were null; reset to defaults");
        }
        else
        {
            repairs.AddRange(state.Preferences.Repair());
        }

        if (state.Views.Count == 0)
        {
            state.Views.Add(ViewState.Create("Desktop"));
            repairs.Add("no views; seeded one");
        }

        var seenViewIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var view in state.Views)
        {
            if (string.IsNullOrWhiteSpace(view.Id) || !seenViewIds.Add(view.Id))
            {
                view.Id = Guid.NewGuid().ToString("n");
                seenViewIds.Add(view.Id);
                repairs.Add("view had a missing or duplicate id");
            }

            repairs.AddRange(view.Repair());

            // [S-5525] Which desktop icons it shows: an unknown value is All; a None view holds no item widget.
            repairs.AddRange(Desktop.DesktopViews.Repair(view));
        }

        if (state.FindView(state.ActiveViewId) is null)
        {
            state.ActiveViewId = state.Views[0].Id;
            repairs.Add("active view id was dangling");
        }

        foreach (var monitorId in state.MonitorViews.Keys.ToList())
        {
            if (state.FindView(state.MonitorViews[monitorId]) is null)
            {
                state.MonitorViews.Remove(monitorId);
                repairs.Add($"monitor '{monitorId}' pointed at a deleted view");
            }
        }

        repairs.AddRange(RepairDisplays(state));

        state.SchemaVersion = AppState.CurrentSchemaVersion;
        return repairs;
    }

    /// <summary>
    /// [S-705] The per-display settings. Runs after the views are repaired, because the default view
    /// for new displays has to name one that still exists.
    /// </summary>
    private static List<string> RepairDisplays(AppState state)
    {
        var repairs = new List<string>();

        if (state.DefaultViewForNewDisplays is not null && state.FindView(state.DefaultViewForNewDisplays) is null)
        {
            state.DefaultViewForNewDisplays = null;
            repairs.Add("the default view for new displays was deleted; new displays show the active view");
        }

        state.DisabledMonitors ??= [];
        var disabled = state.DisabledMonitors
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (disabled.Count != state.DisabledMonitors.Count)
        {
            repairs.Add("the disabled displays held blank or repeated ids");
        }

        state.DisabledMonitors = disabled;

        state.MonitorScales ??= [];
        foreach (var monitorId in state.MonitorScales.Keys.ToList())
        {
            var scale = state.MonitorScales[monitorId];
            if (string.IsNullOrWhiteSpace(monitorId) || !double.IsFinite(scale))
            {
                state.MonitorScales.Remove(monitorId);
                repairs.Add("dropped an unreadable display scale");
                continue;
            }

            var clamped = Math.Clamp(scale, MinMonitorScale, MaxMonitorScale);
            if (clamped != scale)
            {
                state.MonitorScales[monitorId] = clamped;
                repairs.Add($"display scale for '{monitorId}' was outside {MinMonitorScale:0.#}–{MaxMonitorScale:0.#}");
            }
        }

        // [S-906] The per-display view bars: an unreadable entry is dropped (the display falls back to the defaults).
        state.ViewBars ??= [];
        foreach (var monitorId in state.ViewBars.Keys.ToList())
        {
            var bar = state.ViewBars[monitorId];
            if (string.IsNullOrWhiteSpace(monitorId) || bar is null)
            {
                state.ViewBars.Remove(monitorId);
                repairs.Add("dropped an unreadable view bar entry");
                continue;
            }

            repairs.AddRange(bar.Repair(monitorId, id => state.FindView(id) is not null));
        }

        repairs.AddRange(Desktop.DesktopReconcile.RepairKnown(state));

        // [S-13002] Every fence's items whole, and no item in two places of one view.
        repairs.AddRange(Desktop.DesktopFences.Repair(state));

        repairs.AddRange(state.Desktop.Repair());
        repairs.AddRange(Desktop.DesktopDisplays.RepairFallbacks(state));
        return repairs;
    }

    /// <summary>The per-display scale range, as in the original's display settings (50 % to 200 %).</summary>
    public const double MinMonitorScale = 0.5;

    public const double MaxMonitorScale = 2.0;
}
