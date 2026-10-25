using Microsoft.UI.Dispatching;
using Themia.App.Interop;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Core.Storage;

namespace Themia.App.Services;

/// <summary>
/// The displays: one widget surface per enabled monitor, kept in step with the monitors as they come
/// and go, finding the surface at a point, and dragging a widget from one display to another.
/// </summary>
internal sealed partial class AppHost
{
    /// <summary>Every connected display, enabled or not, as of the last display pass.</summary>
    private List<MonitorTarget> _displays = [];

    /// <summary>A display pass is already queued, so the next message need not queue another.</summary>
    private bool _displayPassQueued;

    /// <summary>
    /// Every connected display, including the ones Themia is switched off on — the Display menu's
    /// Show Themia on lists those too ([S-3545]), or there would be no way to switch them back on.
    /// </summary>
    public IReadOnlyList<MonitorTarget> Displays => _displays;

    /// <summary>A connected display by its stable id, or null.</summary>
    public MonitorTarget? FindDisplay(string id)
        => _displays.FindIndex(d => d.Id == id) is var at and >= 0 ? _displays[at] : null;

    /// <summary>The surface whose display contains a point, in physical screen pixels — taskbar included.</summary>
    public DesktopWindow? SurfaceAt(int x, int y)
    {
        foreach (var window in _windows)
        {
            if (window.Monitor.Contains(x, y))
            {
                return window;
            }
        }

        return null;
    }

    public DesktopWindow? SurfaceUnderCursor()
        => Win32.GetCursorPos(out var cursor) ? SurfaceAt(cursor.X, cursor.Y) : null;

    /// <summary>
    /// [S-848] Something about the displays changed: run one display pass soon.
    /// <para>
    /// Coalesced, not timed. Windows sends a burst of messages for one change — a monitor waking
    /// sends several <c>WM_DISPLAYCHANGE</c> while it link-trains, and dragging the taskbar sends a
    /// <c>WM_SETTINGCHANGE</c> per step — and the first one queues a pass at low priority, behind
    /// the rest of the burst already in the queue. Each pass re-reads the topology from scratch and
    /// is idempotent, so a message arriving after it simply queues the next.
    /// </para>
    /// </summary>
    public void RequestDisplayPass()
    {
        if (_disposed || _displayPassQueued)
        {
            return;
        }

        _displayPassQueued = true;
        if (!_dispatcher.TryEnqueue(DispatcherQueuePriority.Low, RunQueuedDisplayPass))
        {
            _displayPassQueued = false;
            Log.Error(Scope, "could not queue a display pass; the surfaces keep their placement until the next display change");
        }
    }

    private void RunQueuedDisplayPass()
    {
        _displayPassQueued = false;
        if (!_disposed)
        {
            ReconcileDisplays();
        }
    }

    /// <summary>
    /// [S-844] The one display pass: give every enabled display a surface over its whole monitor,
    /// close the surfaces whose display is gone or switched off, and re-place the rest.
    /// <para>
    /// Surfaces are matched by the display's stable id, never by enumeration order, so a monitor
    /// that comes back in a different order keeps its own surface, view and layout; a surface that
    /// survives keeps its cards, so a taskbar change restarts no widget.
    /// </para>
    /// </summary>
    public void ReconcileDisplays()
    {
        var monitors = Monitors.All();
        if (monitors.Count == 0)
        {
            // [S-846] Mid-change, not "no screens": build nothing, close nothing, wait for the next message.
            Log.Warn(Scope, "no displays reported; keeping the existing surfaces until the next display change");
            return;
        }

        _displays = monitors;
        var byId = new Dictionary<string, MonitorTarget>(StringComparer.Ordinal);
        foreach (var monitor in monitors)
        {
            byId.TryAdd(monitor.Id, monitor);
        }

        // A surface whose window died is closed and rebuilt rather than left as a husk.
        foreach (var dead in _windows.Where(w => !w.IsAlive).ToList())
        {
            Log.Warn(Scope, $"the surface for {dead.Monitor.GdiName} lost its window; rebuilding it");
            dead.CloseSurface();
            _windows.Remove(dead);
        }

        var rekeyed = AdoptResolvedIds(monitors, byId);

        var surfaces = new Dictionary<string, DisplayInfo>(StringComparer.Ordinal);
        foreach (var window in _windows)
        {
            surfaces[window.Monitor.Id] = window.Monitor.Info;
        }

        var plan = DisplayPlan.Reconcile(surfaces, [.. monitors.Select(m => m.Info)], State.DisabledMonitors);

        // [S-1705] Assignments before surfaces, so a new surface finds its view on its first layout.
        // An existing view, never a new one: views are the user's to make.
        var assigned = DisplayPlan.AssignViews(State, DisplayPlan.ViewKeys(plan.Enabled, State.MonitorViews));
        if (assigned > 0)
        {
            Log.Info(Scope, $"gave {assigned} display(s) an existing view");
            _store.SaveSoon(State);

            // [S-887] The system's doing, not the user's: not something Ctrl+Z should take away.
            AdoptLayout();
        }

        foreach (var id in plan.Close)
        {
            var window = _windows.Find(w => w.Monitor.Id == id);
            if (window is null)
            {
                continue;
            }

            window.CloseSurface();
            _windows.Remove(window);
            Log.Info(Scope, $"closed the surface for {window.Monitor.GdiName}");
        }

        foreach (var info in plan.Update)
        {
            if (_windows.Find(w => w.Monitor.Id == info.Id) is { } window && byId.TryGetValue(info.Id, out var monitor))
            {
                window.UpdateMonitor(monitor);
            }
        }

        foreach (var info in plan.Create)
        {
            if (!byId.TryGetValue(info.Id, out var monitor))
            {
                continue;
            }

            try
            {
                var window = new DesktopWindow(this, monitor);
                _windows.Add(window);
                ShowOrHold(window);

                // The id is what the state file keys this display by; the log is the one place it is shown.
                Log.Info(Scope, $"surface up for {monitor.GdiName} as '{monitor.Id}'");
            }
            catch (Exception ex)
            {
                // One bad display must not cost the user every other monitor's widgets.
                Log.Error(Scope, $"could not create the widget surface for {monitor.GdiName}", ex);
            }
        }

        // [S-849] Every surface is checked against its monitor whether or not the monitor changed: a
        // DPI change resizes the window to the rect Windows suggests, and that is not the monitor.
        foreach (var window in _windows)
        {
            window.EnsurePlacement();
            window.DisplaysPassed();
        }

        if (!plan.IsEmpty || assigned > 0 || rekeyed)
        {
            Log.Info(Scope, $"display pass: {_windows.Count} surface(s) on {monitors.Count} display(s) " +
                $"(+{plan.Create.Count} -{plan.Close.Count} ~{plan.Update.Count})");

            // The settings tree lists the displays, and a display may now show another view.
            StateChanged?.Invoke();
        }
    }

    /// <summary>
    /// [S-843] A display that ran under its ordinal and now reports its device path keeps its view,
    /// on/off switch, scale and view bar, and its surface: both are re-keyed to the path in this pass,
    /// before the plan is made. Otherwise the plan would see a new display, close the old surface
    /// (restarting its widgets) and show the default view on the new one.
    /// </summary>
    /// <returns>Whether any display was re-keyed, so Settings must re-read its display list.</returns>
    private bool AdoptResolvedIds(List<MonitorTarget> monitors, Dictionary<string, MonitorTarget> byId)
    {
        var rekeys = DisplayPlan.Rekeys([.. monitors.Select(m => (m.GdiName, m.Info))]);
        if (rekeys.Count == 0)
        {
            return false;
        }

        var rekeyed = false;

        if (DisplayPlan.AdoptResolvedIds(State, rekeys))
        {
            rekeyed = true;
            Log.Info(Scope, "moved display settings from their ordinal keys to the device paths the displays now report");
            _store.SaveSoon(State);

            // The system's doing, not the user's: not something Ctrl+Z should take away ([S-887]).
            AdoptLayout();
        }

        foreach (var (from, to) in DisplayPlan.SurfaceRekeys([.. _windows.Select(w => w.Monitor.Id)], rekeys))
        {
            if (_windows.Find(w => w.Monitor.Id == from) is { } window && byId.TryGetValue(to, out var monitor))
            {
                Log.Info(Scope, $"{monitor.GdiName} reported its device path; its surface is now '{to}'");
                window.UpdateMonitor(monitor);
                rekeyed = true;
            }
        }

        return rekeyed;
    }

    /// <summary>
    /// [S-845] Switch Themia on or off for one display. Refused (false) when it would leave no
    /// display on: nothing would be left to reach edit mode or Settings from.
    /// </summary>
    public bool SetDisplayEnabled(string id, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (!enabled && !DisplayPlan.CanDisable([.. _displays.Select(d => d.Info)], State.DisabledMonitors, id))
        {
            Log.Warn(Scope, "refused to switch off the last display Themia is shown on");
            return false;
        }

        Mutate(state =>
        {
            state.DisabledMonitors.RemoveAll(d => d == id);
            if (!enabled)
            {
                state.DisabledMonitors.Add(id);
            }
        });

        ReconcileDisplays();
        return true;
    }

    /// <summary>
    /// [S-850] Set one display's widget scale. 1 is stored as no entry, so only the displays the user
    /// changed are saved.
    /// </summary>
    public void SetDisplayScale(string id, double scale)
    {
        ArgumentNullException.ThrowIfNull(id);

        var clamped = double.IsFinite(scale)
            ? Math.Clamp(scale, AppStateStore.MinMonitorScale, AppStateStore.MaxMonitorScale)
            : 1.0;

        Mutate(state =>
        {
            if (Math.Abs(clamped - 1.0) < 0.001)
            {
                state.MonitorScales.Remove(id);
            }
            else
            {
                state.MonitorScales[id] = clamped;
            }
        });
    }

    /// <summary>The displays that may hold a view assignment ([S-843]), for mutations that re-run [S-1705].</summary>
    private List<string> DisplayViewKeys()
        => DisplayPlan.ViewKeys(
            DisplayPlan.Enabled([.. _displays.Select(d => d.Info)], State.DisabledMonitors),
            State.MonitorViews);

    /// <summary>
    /// [S-78]/[S-1591] While widgets are dragged, show where they would land if released now.
    /// <para>
    /// The dragged cards cannot follow the pointer onto another display — their rects are clamped to
    /// their own screen ([S-09]) — so without this the selection sticks to the edge of the first
    /// screen while the pointer is already on the second, and the drop is a surprise. The ghost
    /// appears on the display that would receive them, and only when it really would ([S-76]).
    /// </para>
    /// </summary>
    /// <param name="payload">A move of the dragged widgets, anchored on the one the pointer holds.</param>
    /// <returns>True when the drop would hand the widgets to another display.</returns>
    public bool PreviewHandOff(DesktopWindow source, PlacementPayload payload)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(payload);

        var target = HandOffTarget(source, out var cursor);

        foreach (var window in _windows)
        {
            if (ReferenceEquals(window, target))
            {
                window.ShowGhosts(window.OutlinesAt(cursor, payload));
            }
            else
            {
                window.HideGhost();
            }
        }

        return target is not null;
    }

    /// <summary>
    /// [S-76]/[S-77] The display a drag released now would hand its widgets to: the one under the
    /// pointer, when it is not the source and shows a different view. Null otherwise.
    /// </summary>
    public DesktopWindow? HandOffTarget(DesktopWindow source, out Win32.Point cursor)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (Win32.GetCursorPos(out cursor)
            && SurfaceAt(cursor.X, cursor.Y) is { } under
            && WidgetTransfer.ShouldHandOff(ReferenceEquals(under, source), source.CurrentViewId, under.CurrentViewId))
        {
            return under;
        }

        return null;
    }

    /// <summary>Take down every drag ghost. Called when a drag ends, however it ends.</summary>
    public void EndHandOffPreview()
    {
        foreach (var window in _windows)
        {
            window.HideGhost();
        }
    }
}
