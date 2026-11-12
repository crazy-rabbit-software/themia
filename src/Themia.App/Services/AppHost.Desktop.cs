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
/// [S-5500]..[S-5506] The desktop layer: what the desktop shows (⚙'s one choice) applied to Explorer's
/// icons, held across Explorer restarts and given back on quit; [S-5520] the reconcile of every view's
/// item widgets against the desktop as the shell lists it.
/// </summary>
internal sealed partial class AppHost
{
    /// <summary>
    /// [S-5501] Themia's items show: the surfaces cover their displays and the item widgets have cards.
    /// [S-5575] Not while another program holds the desktop's icons.
    /// </summary>
    public bool ShowsDesktopItems => DesktopCoexistence.ShowsItems(State.Preferences.Desktop, _desktopHolder);

    /// <summary>[S-5575] Who else holds the desktop's windows, read when the choice is applied and when Explorer restarts.</summary>
    private DesktopHolder _desktopHolder = DesktopHolder.None;

    /// <summary>[S-5575] ⚙ › On the desktop's note reads it.</summary>
    public DesktopHolder DesktopHolder => _desktopHolder;

    /// <summary>
    /// [S-5503] [S-5504] At start, before any surface: the choice applied. A choice that hides holds
    /// the list hidden (no flash of icons first); any other undoes what an earlier run that crashed or
    /// was killed may have left hidden.
    /// </summary>
    private void StartDesktop()
    {
        _command.ShellRestarted += OnDesktopShellRestarted;
        _command.DesktopChanged += Files.HostDesktopItems.Instance.OnNotification;
        HostDesktopItems.Instance.Listed += OnDesktopListed;
        DesktopIcons.GaveUp += OnDesktopIconsKeptShown;
        StartDesk();
        ApplyDesktop(picked: false);
    }

    /// <summary>[S-5503] [S-5575] Explorer restarted: its new windows read again and the choice applied to them (the re-hide cap starts over).</summary>
    private void OnDesktopShellRestarted()
    {
        DesktopIcons.ForgetKeptShown();
        ApplyDesktop(picked: false);
    }

    /// <summary>[S-5575] Something keeps Explorer's icons shown past the cap: Themia's items stand down, said under the choice.</summary>
    private void OnDesktopIconsKeptShown() => ApplyDesktop(picked: false);

    /// <summary>[S-5504] Quit: the hook goes and Explorer's icons come back if this run hid them.</summary>
    private void StopDesktop()
    {
        _command.ShellRestarted -= OnDesktopShellRestarted;
        _command.DesktopChanged -= Files.HostDesktopItems.Instance.OnNotification;
        HostDesktopItems.Instance.Listed -= OnDesktopListed;
        DesktopIcons.GaveUp -= OnDesktopIconsKeptShown;
        StopDesk();
        Files.HostDesktopItems.Instance.Stop();
        DesktopIcons.RestoreIfHidden();
    }

    /// <summary>
    /// [S-5503] [S-5505] The choice applied to Explorer's icons: hidden for the session under Themia's
    /// items and Nothing, else shown again as Windows has them, and, on the user's pick of Windows'
    /// icons while Windows hides them, Windows' own setting asked once.
    /// </summary>
    private void ApplyDesktop(bool picked)
    {
        var shows = State.Preferences.Desktop;
        var showedItems = ShowsDesktopItems;
        var before = _desktopHolder;

        // [S-5575] The user's pick tries once more; then who else holds the desktop, read from its windows.
        if (picked)
        {
            DesktopIcons.ForgetKeptShown();
        }

        _desktopHolder = DesktopCoexistence.Classify(DesktopIcons.ReadFacts());
        if (_desktopHolder != before)
        {
            Log.Info(Scope, $"the desktop is held by: {_desktopHolder}");
        }

        // [S-5511] The namespace is read and watched only while its items show.
        if (ShowsDesktopItems)
        {
            Files.HostDesktopItems.Instance.Start(_command.Handle, _dispatcher);
        }
        else
        {
            // [S-5506] No item widget shows: nothing is selected, renamed or expected to land.
            Files.HostDesktopItems.Instance.Stop();
            _desk.Clear();
            _landings.Clear();
            _renamingWidget = null;
            _renamingKey = null;
            DesktopKeysEnd(giveBack: false);
        }

        // [S-5501] [S-5575] The surfaces' regions and the item cards follow when what shows changed by itself.
        if (!picked && (ShowsDesktopItems != showedItems || _desktopHolder != before))
        {
            StateChanged?.Invoke();
            SettingsOutdated?.Invoke();
        }

        // [S-5575] Never fight another program for the desktop: no hide, and nothing shown that it hid.
        if (DesktopIconRules.Hides(shows) && !DesktopCoexistence.MayHide(shows, _desktopHolder))
        {
            DesktopIcons.Unhold();
            return;
        }

        if (DesktopIconRules.Hides(shows))
        {
            DesktopIcons.Hold();
            return;
        }

        DesktopIcons.Release();
        if (picked && DesktopIconRules.AsksWindows(shows, DesktopIcons.WindowsHides()))
        {
            DesktopIcons.Request(true);
        }
    }

    /// <summary>[S-5520] The item kind: the one widget whose config is an <see cref="IDesktopItemConfig"/>, or null in a build without it.</summary>
    private static WidgetDescriptor? ItemKind
        => ModuleCatalog.Widgets.FirstOrDefault(d => typeof(IDesktopItemConfig).IsAssignableFrom(d.ConfigType));

    /// <summary>[S-5520] A read came back: reconciled as one change that is not an undo step.</summary>
    private void OnDesktopListed(IReadOnlyList<DesktopItem> listing, IReadOnlyList<(string Old, string New)> renames)
    {
        if (!ShowsDesktopItems || ItemKind is null)
        {
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
        if (!ShowsDesktopItems || ItemKind is null || !HostDesktopItems.Instance.HasListing)
        {
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
            state, plan, target, screen, (kind.Natural.Width, kind.Natural.Height),
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
        if (!ShowsDesktopItems || ItemKind is not { } kind || !HostDesktopItems.Instance.HasListing)
        {
            return;
        }

        var alive = _windows.Where(w => w.IsAlive).OrderByDescending(w => w.Monitor.IsPrimary).ToList();
        var shown = alive.Where(w => w.Metrics.IsUsable).Select(w => new DesktopShownView(w.CurrentViewId, w.Metrics)).ToList();
        DesktopShownView? primary = alive.FirstOrDefault(w => w.Monitor.IsPrimary && w.Metrics.IsUsable) is { } main
            ? new DesktopShownView(main.CurrentViewId, main.Metrics)
            : null;
        var plan = DesktopDisplays.Plan(State, [.. _displays.Select(d => d.Id)], shown, primary, HostDesktopItems.Instance.Items, (kind.Natural.Width, kind.Natural.Height));
        if (!plan.Changes(State))
        {
            return;
        }

        Log.Info(Scope, $"desktop items on the displays: +{plan.Adds.Count} stand-in(s), -{plan.Removes.Count}, {plan.Moves.Count} moved into the work area");
        Mutate(state => DesktopDisplays.Apply(state, plan, (item, rect) => NewItemWidget(kind.Id, item, rect)), undo: null);
    }

    /// <summary>[S-5600] A new item widget: preset to its item, transparent, headerless.</summary>
    internal static WidgetInstance NewItemWidget(WidgetId kind, DesktopItem item, FracRect rect)
    {
        var widget = WidgetInstance.Create(kind, rect);
        if (widget.Config is IDesktopItemConfig config)
        {
            config.Item = item.Key;
        }

        widget.Background = FillChoice.Transparent();
        return widget;
    }

    /// <summary>[S-5523] What Add widget offers on <paramref name="viewId"/>: the listed items with no widget there.</summary>
    public IReadOnlyList<DesktopItem> UnplacedDesktopItems(string viewId)
        => ShowsDesktopItems && State.FindView(viewId) is { } view
            ? DesktopReconcile.Unplaced(view, HostDesktopItems.Instance.Items)
            : [];

    /// <summary>[S-5523] Place the widget of one unplaced item like any widget, from where it lands.</summary>
    public void BeginPlacement(DesktopItem item, ScreenMetrics screen)
    {
        if (ItemKind is not { } kind || !screen.IsUsable)
        {
            return;
        }

        var widget = NewItemWidget(kind.Id, item, new FracRect(0, 0, kind.Natural.Width / screen.Width, kind.Natural.Height / screen.Height));
        var holder = new ViewState { Id = "desktop-item", Name = string.Empty, Widgets = [widget] };
        if (PlacementPayload.FromView(holder, [widget.Id], PlacementMode.Copy) is { } payload)
        {
            BeginPlacement(payload);
        }
    }
}
