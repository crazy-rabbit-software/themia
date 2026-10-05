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
using Themia.Core.Widgets;
using Windows.Foundation;
using Windows.Graphics;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Themia.App.Shell;

/// <summary>
/// Layout: which view this display shows, positioning the cards in it, containers, the hit region
/// and the snap grid overlay.
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>Top margin of the edit toolbar and the placement hint, below the top of the work area.</summary>
    private const double ChromeTopMargin = 24;

    /// <summary>The rasterization scale the last layout ran at, to tell a DPI change from any other root change.</summary>
    private double _laidOutScale;

    private ViewState ViewForThisMonitor(AppState state) => state.ViewForMonitor(_monitor.Id);

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
                    _host.RequestDisplayPass();
                }

                Relayout();
            };
        }

        Relayout();
    }

    private void OnSelectionChanged()
    {
        foreach (var card in _cards)
        {
            card.ApplySelection();
        }
    }

    private void OnStateChanged()
    {
        ApplyTheme();
        SyncToolbar();
        Relayout();
    }

    private void OnEditModeChanged(bool editing)
    {
        _toolbar.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        _gridOverlay.Visibility = editing && Design.GridSnap ? Visibility.Visible : Visibility.Collapsed;

        foreach (var card in _cards)
        {
            card.ApplyEditMode(editing);
        }

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
        ApplyHitRegion();

        if (editing)
        {
            _root.Focus(FocusState.Programmatic);
            DrawGridOverlay();
        }
    }

    /// <summary>
    /// Hand every container the cards it holds, and take those cards off the grid.
    /// <para>
    /// Containers are handled after the cards exist and before anything is positioned, because a
    /// container needs its children as finished cards and the grid needs to know which children it
    /// is no longer responsible for. The container does not create them: a widget inside a stack is
    /// the same object with the same lifetime as one on the desktop, so nothing has to be
    /// special-cased to keep it ticking.
    /// </para>
    /// </summary>
    private void ArrangeContainers(ViewState view)
    {
        // Cleared first: a widget that has just left a stack would otherwise keep the suppressed
        // header the stack gave it, and nothing would ever give it back.
        foreach (var card in _cards)
        {
            card.SuppressHeader(false);
        }

        foreach (var card in _cards)
        {
            if (card.View is not IWidgetContainerView container)
            {
                continue;
            }

            var owns = WidgetTree.OwnsChildGeometry(WidgetTree.ContainerOf(card.Model.Kind));
            var children = new List<FrameworkElement>();

            foreach (var child in view.ChildrenOf(card.Model.Id))
            {
                var childCard = _cards.Find(c => ReferenceEquals(c.Model, child));
                if (childCard is null)
                {
                    continue;
                }

                // An element has one parent in the visual tree, so a child a container is about to
                // draw has to leave the widget layer first.
                if (owns)
                {
                    _widgetLayer.Children.Remove(childCard);
                }

                // [S-867] A stack row is the child's header (the original drew the open child as a
                // bare body), so the card must not repeat it. A tab strip shows the active child's
                // own live header under the tabs, as the original did, and a group does not name
                // its children at all, so theirs stays.
                childCard.SuppressHeader(WidgetTree.ContainerOf(card.Model.Kind) == ContainerKind.Stack);

                children.Add(childCard);
            }

            container.SetChildren(children);
        }

        ApplyPaintOrder(view);
    }

    /// <summary>
    /// [S-874] Stack the cards the grid places in the view's paint order: the user's "bring to front"
    /// and "send to back", with every group directly under its lowest member ([S-415]). Through
    /// <c>Canvas.ZIndex</c>, never by moving elements in the tree, so nothing is unloaded and
    /// reloaded — a view that reacts to Unloaded would otherwise restart.
    /// </summary>
    private void ApplyPaintOrder(ViewState view)
    {
        var order = view.PaintOrder();
        for (var i = 0; i < order.Count; i++)
        {
            var model = order[i];
            if (_cards.Find(c => ReferenceEquals(c.Model, model)) is { } card)
            {
                Canvas.SetZIndex(card, i);
            }
        }
    }

    /// <summary>Whether a container draws this widget, rather than the grid placing it.</summary>
    private static bool IsArrangedByContainer(ViewState view, WidgetInstance widget) => !view.IsGridPlaced(widget);

    /// <summary>Rebuild the cards for this monitor's view and position them.</summary>
    private void Relayout()
    {
        // [S-928] A suspended surface has no cards and must not grow them back until it resumes.
        if (_closed || _suspended)
        {
            return;
        }

        var view = ViewForThisMonitor(_host.State);
        var wanted = view.Widgets;

        // Reuse the cards that survived the change: rebuilding every card on every state change
        // would restart each widget's tick subscription and flash the whole surface. [S-1882] The
        // cards that go are released before any new one is built, so a view switch never holds the
        // old view and the new one at once.
        var change = WidgetLiveness.Reconcile(_cards, c => c.Model, wanted);

        foreach (var stale in change.Release)
        {
            // [S-802] A card holding the keyboard and now gone must not keep the surface focusable
            // forever. Before Detach, so its view hears the Escape while it is still whole.
            _host.KeyboardRemoved(stale);

            stale.Detach();
            _widgetLayer.Children.Remove(stale);
        }

        // [S-1882] The cards just taken off hold native trees only a collection releases.
        if (change.Release.Count > 0 && _root.DispatcherQueue is { } queue)
        {
            ReleaseSweep.Request(queue);
        }

        var kept = new List<WidgetCard>(change.Slots.Count);

        foreach (var (model, existing) in change.Slots)
        {
            if (existing is not null)
            {
                existing.ApplyConfig();
                kept.Add(existing);
                continue;
            }

            var card = new WidgetCard(this, model);
            _widgetLayer.Children.Add(card);
            kept.Add(card);
        }

        _cards.Clear();
        _cards.AddRange(kept);

        ArrangeContainers(view);

        var contentScale = ContentScale;

        foreach (var card in _cards)
        {
            card.ApplyEditMode(EditMode);
            card.ApplySelection();
            card.ApplyContentScale(contentScale);

            // A widget a stack or a tab strip has taken over is drawn by that container, inside
            // its rect. Positioning it on the grid as well would leave a second copy somewhere
            // else on the desktop.
            if (!IsArrangedByContainer(view, card.Model))
            {
                PositionCard(card, card.Model.Rect);
            }
        }

        Log.Debug(Scope, $"relayout {_monitor.GdiName}: view='{view.Name}' widgets={wanted.Count} " +
            $"metrics={Metrics.Width:0}x{Metrics.Height:0} scale={RasterScale:0.00} edit={EditMode}");

        ApplyChromeInsets();
        RefreshViewBar();
        ApplyHitRegion();

        if (EditMode)
        {
            DrawGridOverlay();
        }

        // [S-1001] Last, once every card is in place: an armed pass (startup, a view change) plays its intro.
        PlayArmedIntro(view);
    }

    /// <summary>
    /// [S-852] Keep the edit toolbar and the placement hint inside the work area. The surface spans
    /// the whole monitor, so with the taskbar at the top they would otherwise sit underneath it.
    /// </summary>
    private void ApplyChromeInsets()
    {
        var metrics = Metrics;
        if (!metrics.IsUsable)
        {
            return;
        }

        var allowed = GridGeometry.AllowedPixels(metrics);
        var margin = new Thickness(
            allowed.X,
            allowed.Y + ChromeTopMargin,
            Math.Max(0, metrics.Width - (allowed.X + allowed.Width)),
            0);

        _toolbar.Margin = margin;
        _placementHint.Margin = margin;
    }

    private void PositionCard(WidgetCard card, FracRect rect)
    {
        var metrics = Metrics;
        if (!metrics.IsUsable)
        {
            return;
        }

        card.SetLiveRect(rect);

        // The card occupies the whole CELL. The gap is applied inside it, so the edit outline and
        // handles trace the cell rather than the painted surface, and changing the gap does not
        // move or resize anything.
        var cell = DrawnCell(card, rect, metrics);
        Canvas.SetLeft(card, cell.X);
        Canvas.SetTop(card, cell.Y);
        card.Width = cell.Width;
        card.Height = cell.Height;
        card.ApplyGap(Design.WidgetGapPx);
    }

    /// <summary>
    /// Restrict the window to the widgets themselves, so every click that is not on a widget lands
    /// on the desktop underneath. Without this the surface would be an invisible full-screen pane
    /// that swallows every desktop icon double-click.
    /// <para>
    /// [S-851] Never over the taskbar. The surface spans the whole monitor ([S-840]), so the region is
    /// always cut to the work area: in edit mode it is the work area itself rather than the whole
    /// window, and a widget the taskbar has since grown over is clipped there rather than moved. The
    /// taskbar is topmost and normally wins anyway — but while Show Desktop lifts the surface to the
    /// top of the topmost band ([S-33]) the region is all that keeps the taskbar clickable. The region
    /// clips painting as well as input, so a covered part is not drawn over the taskbar either.
    /// </para>
    /// </summary>
    private void ApplyHitRegion()
    {
        var metrics = Metrics;
        if (!metrics.IsUsable || !Win32.GetWindowRect(_hwnd, out var window))
        {
            return;
        }

        // The region is in physical pixels relative to the WINDOW; XAML laid the cards out in DIPs
        // relative to the CLIENT area. Convert both.
        var scale = RasterScale;
        var (offsetX, offsetY) = ClientToWindowOffset();

        var work = _monitor.WorkArea;
        var allowed = new PixelRect(work.Left - window.Left, work.Top - window.Top, work.Width, work.Height);

        // [S-1005] The hyperspace veil spans the work area while it plays, so the region does too.
        if (EditMode || MotionCoversScreen)
        {
            DesktopLayer.SetHitRegion(_hwnd, [ToRegionRect(allowed)]);
            return;
        }

        var rects = new List<Win32.Rect>(_cards.Count);
        var view = ViewForThisMonitor(_host.State);

        foreach (var card in _cards)
        {
            // [S-872] A child a stack or tab strip draws is inside its container's rect already —
            // its own stored rect is only where it returns to, and masking it left a dead patch of
            // desktop there. A member of a collapsed group draws nothing.
            if (IsArrangedByContainer(view, card.Model) || card.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            // The mask traces the PAINTED surface, not the cell: the gap is empty desktop, so a
            // click there should reach the desktop rather than an invisible widget edge. And it
            // follows paint ([S-872]): a hidden widget takes nothing, a see-through group its bar.
            var painted = GridGeometry.Inset(DrawnCell(card, card.Model.Rect, metrics), Design.WidgetGapPx);
            if (CardFrame.Hit(painted, card.HitInfo(EditMode)) is not { } box)
            {
                continue;
            }

            var physical = PixelRect.FromEdges(
                offsetX + Math.Floor(box.X * scale),
                offsetY + Math.Floor(box.Y * scale),
                offsetX + Math.Ceiling((box.X + box.Width) * scale),
                offsetY + Math.Ceiling((box.Y + box.Height) * scale));

            if (GridGeometry.Intersect(physical, allowed) is { } visible)
            {
                rects.Add(ToRegionRect(visible));
            }
        }

        // [S-912] The view bar takes clicks too; everything else around it stays the desktop.
        if (ViewBarBox() is { } bar)
        {
            var physical = PixelRect.FromEdges(
                offsetX + Math.Floor(bar.X * scale),
                offsetY + Math.Floor(bar.Y * scale),
                offsetX + Math.Ceiling((bar.X + bar.Width) * scale),
                offsetY + Math.Ceiling((bar.Y + bar.Height) * scale));

            if (GridGeometry.Intersect(physical, allowed) is { } visible)
            {
                rects.Add(ToRegionRect(visible));
            }
        }

        // [S-880] Notices paint and take clicks only inside the region, like everything else here.
        if (NoticeBox() is { } notices)
        {
            var physical = PixelRect.FromEdges(
                offsetX + Math.Floor(notices.X * scale),
                offsetY + Math.Floor(notices.Y * scale),
                offsetX + Math.Ceiling((notices.X + notices.Width) * scale),
                offsetY + Math.Ceiling((notices.Y + notices.Height) * scale));

            if (GridGeometry.Intersect(physical, allowed) is { } visible)
            {
                rects.Add(ToRegionRect(visible));
            }
        }

        DesktopLayer.SetHitRegion(_hwnd, rects);
    }

    private static Win32.Rect ToRegionRect(PixelRect r) => new()
    {
        Left = (int)Math.Round(r.X),
        Top = (int)Math.Round(r.Y),
        Right = (int)Math.Round(r.X + r.Width),
        Bottom = (int)Math.Round(r.Y + r.Height),
    };

    /// <summary>
    /// [S-06a] The snap grid, drawn only while editing. One <see cref="Path"/> holding every line
    /// rather than a few thousand rectangles: the overlay is a hint, not a scene graph.
    /// </summary>
    private void DrawGridOverlay()
    {
        var metrics = Metrics;
        if (!metrics.IsUsable || !Design.GridSnap)
        {
            _gridOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        var (cols, rows) = GridGeometry.GridDims(metrics);
        var group = new GeometryGroup();

        for (var c = 1; c < cols; c++)
        {
            var x = Math.Round(c * metrics.Width / cols) + 0.5;
            group.Children.Add(new LineGeometry { StartPoint = new Point(x, 0), EndPoint = new Point(x, metrics.Height) });
        }

        for (var r = 1; r < rows; r++)
        {
            var y = Math.Round(r * metrics.Height / rows) + 0.5;
            group.Children.Add(new LineGeometry { StartPoint = new Point(0, y), EndPoint = new Point(metrics.Width, y) });
        }

        _gridOverlay.Data = group;
        _gridOverlay.Visibility = Visibility.Visible;
    }
}
