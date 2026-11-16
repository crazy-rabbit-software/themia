using Themia.App.Interop;
using Themia.App.Services.Files;
using Themia.Core.Desktop;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Sdk;

namespace Themia.App.Services;

/// <summary>
/// [S-5500]..[S-5506] The desktop layer: Explorer's icons held hidden while Themia runs, across Explorer
/// restarts, and given back on quit; Themia's items read and watched; [S-5520] the
/// reconcile of every view's item widgets against the desktop as the shell lists it.
/// </summary>
internal sealed partial class AppHost
{

    /// <summary>
    /// [S-5503] At start, before any surface: Explorer's icon list held hidden (no flash of icons
    /// first), and the desktop's items read when they show.
    /// </summary>
    private void StartDesktop()
    {
        _command.ShellRestarted += OnDesktopShellRestarted;
        _command.DesktopChanged += Files.HostDesktopItems.Instance.OnNotification;
        HostDesktopItems.Instance.Listed += OnDesktopListed;
        _deskSizeSettled = State.Preferences.DesktopIconSize;
        StartDesk();
        DesktopIcons.Hold();

        // [S-5506] [S-5511] The desktop's namespace read and watched (the read reconciles as a first read
        // does); the icons always show. Explorer's own list stays hidden ([S-5503]).
        Files.HostDesktopItems.Instance.Start(_command.Handle, _dispatcher);
    }

    /// <summary>[S-5503] Explorer restarted: its new icon list is found, hooked and hidden (the re-hide cap starts over).</summary>
    private void OnDesktopShellRestarted() => DesktopIcons.Hold();

    /// <summary>[S-5504] Quit: the hook goes and Explorer's icons come back as Windows has them.</summary>
    private void StopDesktop()
    {
        _command.ShellRestarted -= OnDesktopShellRestarted;
        _command.DesktopChanged -= Files.HostDesktopItems.Instance.OnNotification;
        HostDesktopItems.Instance.Listed -= OnDesktopListed;
        StopDesk();
        Files.HostDesktopItems.Instance.Stop();
        DesktopIcons.Release();
    }

    /// <summary>[S-5593] The desktop's icon size as the icons were last settled for it.</summary>
    private DesktopIconSize _deskSizeSettled;

    /// <summary>[S-11875] An icon's box in DIPs at the icon size in force: what the desktop's Core rules size icons by.</summary>
    private SizeDip DeskCellDip => DeskCellAt(DesktopCell.Current);

    private static SizeDip DeskCellAt(DesktopIconSize size) => DesktopCell.Grid(size);

    /// <summary>[S-11875] An icon's box on a display: whole grid cells (<see cref="DesktopGrid.IconSize"/>).</summary>
    private (double W, double H) DeskIconSize(ScreenMetrics screen) => DesktopGrid.IconSize(DeskCellDip, screen);

    /// <summary>
    /// [S-5593] The desktop's icon size as the state has it, put in force (<see cref="DesktopCell.Apply"/>)
    /// inside the mutation that changed it, whoever made it (View ›, the General page, Ctrl + wheel, an
    /// import): every icon of a shown view keeps its place in the arrangement, which grows or shrinks with
    /// the icon (<see cref="DesktopGrid.Rescale"/>: no two overlap, [S-11887]), at its new size in whole grid cells ([S-11875]). A view
    /// no display shows keeps its rects; its icons are painted at the new size when it is shown. Returns
    /// whether the size changed.
    /// </summary>
    private bool ApplyDesktopIconSize()
    {
        var size = State.Preferences.DesktopIconSize;
        var from = DeskCellAt(DesktopCell.Current);
        if (!DesktopCell.Apply(size))
        {
            return false;
        }

        var to = DeskCellDip;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var surface in SurfacesNow())
        {
            // [S-854] A surface whose metrics are provisional rescales nothing.
            var metrics = surface.Screen;
            if (!seen.Add(surface.ViewId) || !surface.Writable || State.FindView(surface.ViewId) is not { } view)
            {
                continue;
            }

            // [S-11887] The view's icons as one arrangement: no two overlap, none lands on a widget.
            var (fromBox, toBox) = (DesktopGrid.IconSize(from, metrics), DesktopGrid.IconSize(to, metrics));
            var icons = view.Widgets.Where(w => w.Config is IDesktopItemConfig).ToList();
            var others = view.Widgets.Where(w => w.Config is not IDesktopItemConfig).Select(w => w.Rect).ToList();
            var rescaled = DesktopGrid.Rescale([.. icons.Select(w => w.Rect)], others, fromBox, toBox, metrics);
            for (var i = 0; i < icons.Count; i++)
            {
                icons[i].Rect = rescaled[i];
            }
        }

        Log.Info(Scope, $"desktop icons are {size} now");
        return true;
    }

    /// <summary>
    /// [S-5593] The desktop's icon size changed (the state says so, whoever changed it): the displays'
    /// rules run again for the new size.
    /// </summary>
    private void SettleDesktopViewSettings()
    {
        if (_deskSizeSettled == State.Preferences.DesktopIconSize)
        {
            return;
        }

        _deskSizeSettled = State.Preferences.DesktopIconSize;
        SettleDesktopDisplays();
        RepaintDesk();
    }

    /// <summary>[S-5593] Another desktop icon size: one setting, stored and applied at once (the mutation puts it in force).</summary>
    public void SetDesktopIconSize(DesktopIconSize size)
    {
        if (State.Preferences.DesktopIconSize != size && Enum.IsDefined(size))
        {
            Mutate(s => s.Preferences.DesktopIconSize = size, undo: null);
            SettingsOutdated?.Invoke();
        }
    }

    /// <summary>[S-5593] Ctrl + mouse wheel on the desktop: one size up or down, as Explorer's.</summary>
    public void StepDesktopIconSize(int notches) => SetDesktopIconSize(DesktopCell.Step(State.Preferences.DesktopIconSize, notches));

    /// <summary>[S-5520] The item kind: the one widget whose config is an <see cref="IDesktopItemConfig"/>, or null in a build without it.</summary>
    private static WidgetDescriptor? ItemKind
        => ModuleCatalog.Widgets.FirstOrDefault(d => typeof(IDesktopItemConfig).IsAssignableFrom(d.ConfigType));

    /// <summary>
    /// [S-854] A read of the desktop that came in before every surface's metrics settled: its renames,
    /// replayed with the listing then held once they have (<see cref="SettleSurfaces"/>). Null while none waits.
    /// </summary>
    private List<(string Old, string New)>? _listingWaits;

    /// <summary>[S-5520] A read came back: reconciled as one change that is not an undo step.</summary>
    private void OnDesktopListed(IReadOnlyList<DesktopItem> listing, IReadOnlyList<(string Old, string New)> renames)
    {
        if (ItemKind is null)
        {
            return;
        }

        // [S-854] New items land on cells of the real grid only: before the surfaces settle, the read waits.
        if (!SurfaceMetrics.AllSettled(SurfacesNow()))
        {
            (_listingWaits ??= []).AddRange(renames);
            return;
        }

        var plan = DesktopReconcile.Plan(State, listing, renames);
        if (plan.Changes(State))
        {
            Mutate(state => ReconcileDesktop(state, plan), undo: null);
        }

        // [S-5552] What New › made is on the desktop now: its rename box opens, the item selected.
        if (_renameArrival is { } arrival)
        {
            _renameArrival = null;
            _desk.SelectWhenShown(arrival.Key);
            LayoutDesk();
            if (!BeginRename(arrival.Key, arrival.Widget))
            {
                Log.Info(Scope, "what New › made could not open its rename box");
            }
        }

        // [S-5560]..[S-5562] The listing may hold what an unplugged display's view needs a stand-in for.
        SettleDesktopDisplays();
    }

    /// <summary>[S-5552] The arrival New › made, to open its rename box once the reconcile that placed it is applied.</summary>
    private (string Key, string Widget)? _renameArrival;

    /// <summary>
    /// [S-5520] After an undo or redo, which may bring back a widget whose item has gone: the held
    /// listing reconciled in place (the caller rebases and saves).
    /// </summary>
    private void ReconcileDesktopAfterStep()
    {
        if (ItemKind is null || !HostDesktopItems.Instance.HasListing)
        {
            return;
        }

        // [S-854] Before the surfaces settle the held listing is reconciled once they have.
        if (!SurfaceMetrics.AllSettled(SurfacesNow()))
        {
            _listingWaits ??= [];
            return;
        }

        var plan = DesktopReconcile.Plan(State, HostDesktopItems.Instance.Items, []);
        if (plan.Changes(State))
        {
            ReconcileDesktop(State, plan);
        }
    }

    /// <summary>[S-5520] [S-5521] Apply a plan: new items land in the view of the primary display, at its next free cells.</summary>
    private void ReconcileDesktop(AppState state, DesktopPlan plan)
    {
        if (ItemKind is not { } kind)
        {
            return;
        }

        var primary = _windows.FirstOrDefault(w => w.IsAlive && w.Monitor.IsPrimary) ?? _windows.FirstOrDefault(w => w.IsAlive);
        var target = primary is null ? null : state.ViewForMonitor(primary.Monitor.Id).Id;
        var screen = primary?.Metrics ?? default;

        // [S-5591] A drop's, a paste's or New ›'s arrivals land where they were expected.
        var spots = LandingSpots(plan.Arrivals);
        var made = new Dictionary<string, string>(DesktopItemKeys.Comparer);
        var unplaced = DesktopReconcile.Apply(
            state, plan, target, screen, (DeskCellDip.Width, DeskCellDip.Height),
            (item, rect) =>
            {
                var widget = NewItemWidget(kind.Id, item, rect);
                made[item.Key] = widget.Id;
                return widget;
            },
            spots, ScreenOfView);

        // [S-5552] New ›'s arrival, if it landed, is renamed once this change is applied.
        for (var i = 0; i < spots.Count && i < plan.Arrivals.Count; i++)
        {
            if (spots[i] is { Renames: true } && made.TryGetValue(plan.Arrivals[i].Key, out var id))
            {
                _renameArrival = (plan.Arrivals[i].Key, id);
            }
        }
        if (unplaced.Count > 0)
        {
            Log.Info(Scope, $"{unplaced.Count} new desktop item(s) found no free cell; Add widget lists them");
        }
    }

    /// <summary>
    /// [S-5560]..[S-5562] The item widgets against the displays as they are: stand-ins for the items of an
    /// unplugged display's view on the primary display, taken away when that view shows them again, and
    /// items the work area no longer holds moved inside it. Not an undo step: the displays changed, not
    /// the arrangement. Runs after every display pass and every read of the desktop.
    /// </summary>
    private void SettleDesktopDisplays()
    {
        if (ItemKind is not { } kind || !HostDesktopItems.Instance.HasListing)
        {
            return;
        }

        // [S-854] Only on every surface's real metrics: a surface not settled would leave its view
        // looking shown nowhere, and its grid is not the real one. The settle runs this again then.
        var alive = _windows.Where(w => w.IsAlive).OrderByDescending(w => w.Monitor.IsPrimary).ToList();
        var surfaces = alive.Select(w => w.Surface).ToList();
        if (!SurfaceMetrics.AllSettled(surfaces))
        {
            return;
        }

        var shown = surfaces.Select(s => new DesktopShownView(s.ViewId, s.Screen)).ToList();
        DesktopShownView? primary = alive.FirstOrDefault(w => w.Monitor.IsPrimary) is { } main
            ? new DesktopShownView(main.CurrentViewId, main.Metrics)
            : null;
        var plan = DesktopDisplays.Plan(State, [.. _displays.Select(d => d.Id)], shown, primary, HostDesktopItems.Instance.Items, (DeskCellDip.Width, DeskCellDip.Height));
        if (plan.Changes(State))
        {
            Log.Info(Scope, $"desktop items on the displays: +{plan.Adds.Count} stand-in(s), -{plan.Removes.Count}, {plan.Moves.Count} moved into the work area");
            Mutate(state => DesktopDisplays.Apply(state, plan, (item, rect) => NewItemWidget(kind.Id, item, rect)), undo: null);
        }

    }

    /// <summary>
    /// [S-5594] [S-11876] View › Auto arrange icons: once, now, every desktop icon of the views shown packed
    /// on the widget grid from the work area's top-left corner in reading order round the other widgets
    /// (<see cref="DesktopGrid.Pack"/>), one undo step. No setting: what is dropped later lands where it is
    /// dropped, on the grid.
    /// </summary>
    public void AutoArrangeDesktopIcons()
    {
        var moves = DesktopGrid.Packs(State, SurfacesNow(), DeskCellDip);
        if (moves.Count == 0)
        {
            return;
        }

        Log.Info(Scope, $"{moves.Count} desktop icon(s) packed (Auto arrange icons)");
        Mutate(_ => MoveDesktopIcons(moves), undo: UndoAction.Move);
    }

    private static void MoveDesktopIcons(List<(WidgetInstance Widget, FracRect Rect)> moves)
    {
        foreach (var (widget, rect) in moves)
        {
            widget.Rect = rect;
        }
    }

    /// <summary>[S-5600] A new item widget: preset to its item (transparent and headerless by its kind's traits).</summary>
    internal static WidgetInstance NewItemWidget(WidgetId kind, DesktopItem item, FracRect rect)
    {
        var widget = WidgetInstance.Create(kind, rect);
        if (widget.Config is IDesktopItemConfig config)
        {
            config.Item = item.Key;
        }

        return widget;
    }

    /// <summary>[S-5523] What Add widget offers on <paramref name="viewId"/>: the listed items with no widget there.</summary>
    public IReadOnlyList<DesktopItem> UnplacedDesktopItems(string viewId)
        => State.FindView(viewId) is { } view
            ? DesktopReconcile.Unplaced(view, HostDesktopItems.Instance.Items)
            : [];

    /// <summary>[S-5523] Place the widget of one unplaced item like any widget, from where it lands.</summary>
    public void BeginPlacement(DesktopItem item, ScreenMetrics screen)
    {
        if (ItemKind is not { } kind || !screen.IsUsable)
        {
            return;
        }

        var (w, h) = DeskIconSize(screen);
        var widget = NewItemWidget(kind.Id, item, new FracRect(0, 0, w, h));
        var holder = new ViewState { Id = "desktop-item", Name = string.Empty, Widgets = [widget] };
        if (PlacementPayload.FromView(holder, [widget.Id], PlacementMode.Copy) is { } payload)
        {
            BeginPlacement(payload);
        }
    }
}
