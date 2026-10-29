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
    /// <summary>[S-5501] Themia's items show: the surfaces cover their displays and the item widgets have cards.</summary>
    public bool ShowsDesktopItems => State.Preferences.Desktop == DesktopShows.ThemiaItems;

    /// <summary>
    /// [S-5503] [S-5504] At start, before any surface: the choice applied. A choice that hides holds
    /// the list hidden (no flash of icons first); any other undoes what an earlier run that crashed or
    /// was killed may have left hidden.
    /// </summary>
    private void StartDesktop()
    {
        _command.ShellRestarted += DesktopIcons.ShellRestarted;
        _command.DesktopChanged += Files.HostDesktopItems.Instance.OnNotification;
        HostDesktopItems.Instance.Listed += OnDesktopListed;
        StartDesk();
        ApplyDesktop(picked: false);
    }

    /// <summary>[S-5504] Quit: the hook goes and Explorer's icons come back if this run hid them.</summary>
    private void StopDesktop()
    {
        _command.ShellRestarted -= DesktopIcons.ShellRestarted;
        _command.DesktopChanged -= Files.HostDesktopItems.Instance.OnNotification;
        HostDesktopItems.Instance.Listed -= OnDesktopListed;
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

        // [S-5511] The namespace is read and watched only while its items show.
        if (shows == DesktopShows.ThemiaItems)
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
        if (!plan.Changes(State))
        {
            return;
        }

        Mutate(state => ReconcileDesktop(state, plan), undo: null);
    }

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
        var unplaced = DesktopReconcile.Apply(
            state, plan, target, screen, (kind.DefaultWidth, kind.DefaultHeight), (item, rect) => NewItemWidget(kind.Id, item, rect),
            LandingSpots(plan.Arrivals), ScreenOfView);
        if (unplaced.Count > 0)
        {
            Log.Info(Scope, $"{unplaced.Count} new desktop item(s) found no free cell; Add widget lists them");
        }
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

        var widget = NewItemWidget(kind.Id, item, new FracRect(0, 0, kind.DefaultWidth / screen.Width, kind.DefaultHeight / screen.Height));
        var holder = new ViewState { Id = "desktop-item", Name = string.Empty, Widgets = [widget] };
        if (PlacementPayload.FromView(holder, [widget.Id], PlacementMode.Copy) is { } payload)
        {
            BeginPlacement(payload);
        }
    }
}
