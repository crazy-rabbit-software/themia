using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Windows.Foundation;
using Windows.Graphics;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Themia.App.Shell;

/// <summary>
/// Layout: which view this display shows, positioning the cards in it, the hit region (the work area) and the snap
/// grid overlay.
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>[S-3500] The edit bar's and the placement line's gap under the top of the work area: 8 DIPs, as Karpo's edit bar.</summary>
    private const double ChromeTopMargin = Fluent.Space2;

    /// <summary>The rasterization scale the last layout ran at, to tell a DPI change from any other root change.</summary>
    private double _laidOutScale;

    private ViewState ViewForThisMonitor(AppState state) => state.ViewForMonitor(_monitor.Id);

    /// <summary>
    /// [S-854] This surface's metrics are the real ones: its root loaded with the scale it renders at
    /// known, and no scale change waiting for the display pass that puts the window back over its
    /// monitor. Until then a layout is drawn at provisional metrics and nothing writes geometry by them.
    /// </summary>
    private bool _metricsSettled;

    /// <summary>[S-854] This surface as what writes geometry sees it (<see cref="SurfaceMetrics"/>).</summary>
    internal SurfaceMetrics Surface
        => new(CurrentViewId, Metrics, _host.State.MonitorScales.TryGetValue(_monitor.Id, out var own) ? own : 1.0, _metricsSettled && !_closed);

    /// <summary>
    /// [S-854] Settle when the real scale is known (the root loaded, its scale positive) and tell the host,
    /// which runs what waited for real metrics (unless <paramref name="tellHost"/> is false: the display
    /// pass, which settles them itself once every surface is through it). Idempotent: a surface already
    /// settled tells nobody.
    /// </summary>
    private void TrySettleMetrics(bool tellHost = true)
    {
        if (_metricsSettled || _closed || SurfaceMetrics.SettledScale(_root.IsLoaded, _root.XamlRoot?.RasterizationScale) is null)
        {
            return;
        }

        _metricsSettled = true;
        Log.Debug(Scope, $"metrics settled {_monitor.GdiName}: {Metrics.Width:0}x{Metrics.Height:0} scale={RasterScale:0.00}");
        if (tellHost)
        {
            _host.SettleSurfaces();
        }
    }

    /// <summary>
    /// Once the content is live the XamlRoot exists, so the real scale is finally knowable. Lay out
    /// again here and on every subsequent scale change.
    /// </summary>
    private void OnRootLoaded(object sender, RoutedEventArgs args)
    {
        if (_root.XamlRoot is { } xamlRoot)
        {
            _laidOutScale = xamlRoot.RasterizationScale;
            xamlRoot.Changed += (_, _) =>
            {
                // [S-849] A display-scale change has also resized this window to a rect Windows
                // scaled from the old one. The display pass puts it back over the monitor.
                var scale = _root.XamlRoot?.RasterizationScale ?? _laidOutScale;
                if (scale != _laidOutScale)
                {
                    _laidOutScale = scale;

                    // [S-854] Provisional again until that pass has put the window back (DisplaysPassed).
                    _metricsSettled = false;
                    _host.RequestDisplayPass();
                }

                Relayout();
            };
        }

        Relayout();

        // [S-854] After the first layout at the real metrics: the host's waiting writers run on them.
        TrySettleMetrics();
    }

    private void OnSelectionChanged()
    {
        foreach (var card in _cards)
        {
            card.ApplySelection();
        }


        // [S-2343] A new selection: its entry on the bar, its flyout opened once.
        SyncProperties();
        ShowEditBar();

        // [S-2460] Several selected: the frame round them.
        SyncFrame();
    }

    private void OnStateChanged(StateChange change)
    {
        ApplyTheme();
        SyncEditBar();
        Relayout(change);

        // A card built again (an undo, a paste of a copy) starts unfaded: the cut keeps its fade.
        FadeLeaving();
    }

    private void OnEditModeChanged(bool editing)
    {
        if (editing)
        {
            // [S-3593] The bar's buttons and its add menu are built as edit mode starts.
            if (!_editBar.Built)
            {
                _editBar.Fill(BarCommands());
            }

            SyncProperties();
            ShowEditBar();
        }
        else
        {
            // [S-3521] Done, or edit mode left any other way, closes the edit bar's open menu;
            // [S-3593] and what the edit-mode menus drew goes with them.
            ReleaseEditMenus();
        }

        _editBar.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;

        _gridOverlay.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;

        // [S-15501] The repeaters' outlines come and go with edit mode.
        RefreshRepeaters();

        foreach (var card in _cards)
        {
            card.ApplyEditMode(editing);
        }

        FadeLeaving();
        SyncFrame();

        // [S-802] Edit mode ends a keyboard lease: the keys are the edit shortcuts now.
        if (editing)
        {
            _host.KeyboardEndOn(this);
        }

        // Edit mode needs real focus for the keyboard shortcuts; normal mode goes back to being
        // part of the desktop.
        ApplyInteractivity();

        // [S-907] The bar gains its grip in edit mode, or appears faded where the user hid it.
        RefreshViewBar();
        PlaceFlyout();

        if (editing)
        {
            // [S-34f] Focus on an activatable surface activates it: never one a full-screen app covers.
            if (DesktopZOrder.MayTakeForeground(_hwnd, _monitor.Bounds))
            {
                _root.Focus(FocusState.Programmatic);
            }

            DrawGridOverlay();
        }
    }

    /// <summary>
    /// [S-874] Stack the cards in the view's paint order: the view's widget list, which "bring to
    /// front" and "send to back" rearrange ([S-13042]: a fence's icons right over their fence,
    /// <see cref="Themia.Core.Desktop.DesktopFences.PaintOrder"/>). Through <c>Canvas.ZIndex</c>, never by moving elements in
    /// the tree, so nothing is unloaded and reloaded — a view that reacts to Unloaded would otherwise
    /// restart.
    /// </summary>
    private void ApplyPaintOrder(ViewState view)
    {
        var order = Themia.Core.Desktop.DesktopFences.PaintOrder(_host.FencesOf(view));
        for (var i = 0; i < order.Count; i++)
        {
            var model = order[i];
            if (_cards.Find(c => ReferenceEquals(c.Model, model)) is { } card)
            {
                Canvas.SetZIndex(card, i);
            }
        }
    }

    /// <summary>The card showing widget <paramref name="widgetId"/> on this surface, if any.</summary>
    internal WidgetCard? CardFor(string widgetId)
        => _closed ? null : _cards.Find(c => string.Equals(c.Model.Id, widgetId, StringComparison.Ordinal));

    /// <inheritdoc />
    public void OnCardFrameChanged(WidgetCard card)
    {
        // A card still being built (its view hiding itself during Attach) is positioned by the
        // relayout that is building it.
        if (_closed || !_cards.Contains(card))
        {
            return;
        }

        PositionCard(card, card.Model.Rect);
        JoinCards();
        PlaceFlyout();
    }

    /// <inheritdoc />
    public void RequestZOrder(WidgetCard card, bool front) => _host.ZOrderSelected(front);

    /// <summary>Rebuild the cards for this monitor's view and position them, every card's config and design applied again: the full pass.</summary>
    private void Relayout() => Relayout(StateChange.All);

    /// <summary>
    /// Rebuild the cards for this monitor's view and position them. [S-15660] A kept card applies its config again only
    /// where <paramref name="change"/> says it changed (every card when the preferences moved, or the pass is whole), and
    /// is painted again where the design moved; every card is laid where it stands.
    /// </summary>
    private void Relayout(StateChange change)
    {
        // [S-928] A suspended surface has no cards and must not grow them back until it resumes.
        if (_closed || _suspended)
        {
            return;
        }

        // [S-1017] A show from an earlier pass ends at rest before this one touches its cards.
        EndRunningIntro();

        var view = ViewForThisMonitor(_host.State);
        var wanted = view.Widgets;

        // [S-15502] What the repeaters draw, read before any card is built or given its config: a member's config is its row's.
        var rowsBefore = _repeaterPlan.Templates;
        PlanRepeaters(view);

        // [S-15529] [S-15660] A container's part shows its container's settings (its place, its first day) and its row's
        // value: it applies its config again where its container's changed or its row did, though its own did not.
        var rowChanged = RowsChanged(view, change, rowsBefore);

        // Reuse the cards that survived the change: rebuilding every card on every state change
        // would restart each widget's tick subscription and flash the whole surface. [S-1882] The
        // cards that go are released before any new one is built, so a view switch never holds the
        // old view and the new one at once.
        var cards = WidgetLiveness.Reconcile(_cards, c => c.Model, wanted);

        foreach (var stale in cards.Release)
        {
            // [S-802] A card holding the keyboard and now gone must not keep the surface focusable
            // forever. Before Detach, so its view hears the Escape while it is still whole.
            _host.KeyboardRemoved(stale);

            stale.Detach();
            _widgetLayer.Children.Remove(stale);
        }

        // [S-1882] The cards just taken off hold native trees only a collection releases.
        if (cards.Release.Count > 0 && _root.DispatcherQueue is { } queue)
        {
            ReleaseSweep.Request(queue);
        }

        var kept = new List<WidgetCard>(cards.Slots.Count);

        foreach (var (model, existing) in cards.Slots)
        {
            if (existing is not null)
            {
                if (change.Reapplies(model.Id) || rowChanged.Contains(model.Id))
                {
                    existing.ApplyConfig();
                }
                else if (change.Design)
                {
                    existing.ApplyDesignChange();
                }

                kept.Add(existing);
                continue;
            }

            var card = new WidgetCard(this, model);
            _widgetLayer.Children.Add(card);
            _markerLayer.Add(card);
            kept.Add(card);
        }

        _cards.Clear();
        _cards.AddRange(kept);

        ApplyPaintOrder(view);

        // [S-13042] [S-13043] The icons a fence does not show now (scrolled away, rolled up) are drawn by no card.
        var fenced = _host.FenceHidden(view, Metrics);
        foreach (var card in _cards)
        {
            card.Visibility = fenced.Contains(card.Model.Id) ? Visibility.Collapsed : Visibility.Visible;
        }

        foreach (var card in _cards)
        {
            // [S-2363] Edit mode's look paints the selection too (ApplyEditMode runs ApplySelection): once.
            card.ApplyEditMode(EditMode);
            card.ApplyContentScale(ContentScale(card.Model));

            // [S-1972] A card a run of held arrows is moving stays where the run put it until the run lands.
            PositionCard(card, Standing(card));
        }

        // [S-15502] Each copied row's widgets, over the real cards' paint order.
        LayCopies();

        JoinCards();

        // [S-11890] The local grid's lines for this screen, drawn now so the first move draws nothing.
        _localGrid.Prepare(Metrics, RasterScale);

        Log.Debug(Scope, $"relayout {_monitor.GdiName}: view='{view.Name}' widgets={wanted.Count} " +
            $"metrics={Metrics.Width:0}x{Metrics.Height:0} scale={RasterScale:0.00} edit={EditMode}");

        ApplyChromeInsets();
        RefreshViewBar();
        PlaceFlyout();
        PlaceWidgetGallery();

        if (EditMode)
        {
            DrawGridOverlay();
        }

        // [S-1001] Last, once every card is in place: an armed pass (startup, a view change) plays its intro.
        PlayArmedIntro(view);
    }

    /// <summary>
    /// [S-852] Keep the edit bar and the placement hint inside the work area. The surface spans
    /// the whole monitor, so with the taskbar at the top they would otherwise sit underneath it.
    /// </summary>
    private void ApplyChromeInsets()
    {
        var metrics = Metrics;
        if (!metrics.IsUsable)
        {
            return;
        }

        var allowed = WorkArea;
        var margin = new Thickness(
            allowed.X,
            allowed.Y + ChromeTopMargin,
            Math.Max(0, metrics.Width - (allowed.X + allowed.Width)),
            0);

        _editBar.Margin = margin;
        _placementHint.Margin = margin;
    }

    private void PositionCard(WidgetCard card, FracRect rect)
    {
        var metrics = Metrics;
        if (!metrics.IsUsable)
        {
            return;
        }

        // [S-2446] A widget is painted in its stored rect fitted into its sizing on this
        // display (another monitor, DPI or scale), anchored at its top-left and kept in reach. The
        // stored rect is not rewritten by being shown: only a gesture or a config change does that.
        // [S-13028] A card gliding to its stored rect is drawn where the glide stands (unfitted on the way: a fence's roll
        // passes heights its pinned sizing would not let it stand at).
        var stored = rect == card.Model.Rect;
        rect = SizeFit.Shown(rect, card.Limits, metrics);
        if (stored)
        {
            rect = Gliding(card, rect);
        }

        card.SetLiveRect(rect);
        card.ApplyResizable();

        // The card occupies the whole CELL. The gap is applied inside it (by the join that always
        // follows, [S-2249]), so the edit outline and handles trace the cell rather than the painted
        // surface, and changing the gap does not move or resize anything.
        // [S-11872] Every edge on a whole device pixel, so two cards that meet share one.
        var cell = GridGeometry.PaintedCell(rect, metrics, RasterScale);
        Canvas.SetLeft(card, cell.X);
        Canvas.SetTop(card, cell.Y);
        card.Width = cell.Width;
        card.Height = cell.Height;
    }

    /// <summary>
    /// [S-2240] [S-2244] Join the cards that touch, by the one path the scene renderer takes too. A card
    /// being dragged or resized is free (every corner round, its whole border) until it is dropped,
    /// and its former neighbours get their corners back at once. Runs whenever the layout is applied:
    /// a relayout (a view switch, a design change such as the gap), a move, a resize, a nudge.
    /// </summary>
    private void JoinCards()
    {
        // [S-15502] A copied row's widgets join as the template's do: touching atoms one panel.
        WidgetCard.Join(_copies.Count == 0 ? _cards : [.. _cards, .. _copies.Values], Metrics, RasterScale, EditMode, JoinSet);

        // [S-2460] The selection's frame round where its cards now stand.
        SyncFrame();
    }

    /// <summary>
    /// [S-2244] [S-2457] Which cards a card may join: a resized one none (null); the cards a drag carries
    /// (a selection) one another (1), free of the cards standing still (0); [S-2460] so the members of a block resized.
    /// </summary>
    private int? JoinSet(WidgetCard card)
        => card.IsResizing ? null
            : _move is { Moved: true } move && move.Cards.Exists(c => ReferenceEquals(c.Card, card)) ? 1
            : _blockResize is { Stepped: true } resize && resize.Holds(card) ? 1
            : 0;

    /// <summary>
    /// [S-21] [S-851] The surface is cut to its display's work area, one rectangle, always: Themia
    /// owns the whole desktop ([S-5501]), and never the taskbar. The surface spans the whole monitor
    /// ([S-840]); the taskbar and a dock are above it in every state ([S-20], [S-33f]), and the region
    /// keeps it out of what they reserved whatever the z-order is. The region clips painting as well as
    /// input, so a widget the taskbar has grown over is not drawn over it either. Applied when the window is made and whenever its display's bounds or
    /// work area change (<see cref="UpdateMonitor"/>, <see cref="EnsurePlacement"/>); nothing on the
    /// surface changes it.
    /// </summary>
    private void ApplyHitRegion()
    {
        if (!Win32.GetWindowRect(_hwnd, out var window))
        {
            return;
        }

        // The region is in physical pixels relative to the WINDOW.
        var work = _monitor.WorkArea;
        DesktopLayer.SetHitRegion(_hwnd, ToRegionRect(GridGeometry.SurfaceRegion(
            new DeviceRect(window.Left, window.Top, window.Width, window.Height),
            new DeviceRect(work.Left, work.Top, work.Width, work.Height))));
    }

    private static Win32.Rect ToRegionRect(DeviceRect r) => new()
    {
        Left = (int)Math.Round(r.X),
        Top = (int)Math.Round(r.Y),
        Right = (int)Math.Round(r.X + r.Width),
        Bottom = (int)Math.Round(r.Y + r.Height),
    };

    /// <summary>[S-06a]/[S-06c] The grid, drawn whole only while editing, on the surface's own pixels.</summary>
    private void DrawGridOverlay()
    {
        var metrics = Metrics;
        if (!metrics.IsUsable || !EditMode)
        {
            _gridOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        _gridOverlay.Draw(metrics, RasterScale);
        _gridOverlay.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// [S-11874] The local grid round <paramref name="target"/> while a block moves, or faded out with null. [S-11891]
    /// In edit mode, whose whole grid is drawn, only the target's box lit.
    /// </summary>
    private void ShowLocalGrid(FracRect? target) => _localGrid.Show(target, Metrics, RasterScale, lines: !EditMode);
}
