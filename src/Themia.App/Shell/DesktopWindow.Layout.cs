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
/// Layout: which view this display shows, positioning the cards in it, the hit region and the snap
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

        // [S-3508] A new selection brings its properties menu back beside it.
        _propertiesDismissed = false;
        SyncProperties();
        ShowToolbar();
    }

    private void OnStateChanged()
    {
        ApplyTheme();
        SyncToolbar();
        Relayout();

        // A card built again (an undo, a paste of a copy) starts unfaded: the cut keeps its fade.
        FadeLeaving();
    }

    private void OnEditModeChanged(bool editing)
    {
        if (editing)
        {
            _propertiesDismissed = false;
            SyncProperties();
            ShowToolbar();
        }
        else
        {
            // [S-3521] Done, or edit mode left any other way, closes the edit bar's open menu.
            CloseBarMenu(focusButton: false);
        }

        _toolbar.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;

        _gridOverlay.Visibility = editing && Design.GridSnap ? Visibility.Visible : Visibility.Collapsed;

        foreach (var card in _cards)
        {
            card.ApplyEditMode(editing);
        }

        FadeLeaving();

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
        PlaceProperties();
        ApplyHitRegion();

        if (editing)
        {
            _root.Focus(FocusState.Programmatic);
            DrawGridOverlay();
        }
    }

    /// <summary>
    /// [S-874] Stack the cards in the view's paint order: the view's widget list, which "bring to
    /// front" and "send to back" rearrange. Through <c>Canvas.ZIndex</c>, never by moving elements in
    /// the tree, so nothing is unloaded and reloaded — a view that reacts to Unloaded would otherwise
    /// restart.
    /// </summary>
    private void ApplyPaintOrder(ViewState view)
    {
        for (var i = 0; i < view.Widgets.Count; i++)
        {
            var model = view.Widgets[i];
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
        PlaceProperties();
        ApplyHitRegion();
    }

    /// <inheritdoc />
    public void RequestZOrder(WidgetCard card, bool front) => _host.ZOrderSelected(front);

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

        ApplyPaintOrder(view);

        foreach (var card in _cards)
        {
            card.ApplyEditMode(EditMode);
            card.ApplySelection();
            card.ApplyContentScale(ContentScale(card.Model));
            PositionCard(card, card.Model.Rect);
        }

        Log.Debug(Scope, $"relayout {_monitor.GdiName}: view='{view.Name}' widgets={wanted.Count} " +
            $"metrics={Metrics.Width:0}x{Metrics.Height:0} scale={RasterScale:0.00} edit={EditMode}");

        ApplyChromeInsets();
        RefreshViewBar();
        PlaceProperties();
        PlaceBarMenu();
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
        var cell = GridGeometry.ToPixels(rect, metrics);
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

        foreach (var card in _cards)
        {
            if (card.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            // The mask traces the PAINTED surface, not the cell: the gap is empty desktop, so a
            // click there should reach the desktop rather than an invisible widget edge. And it
            // follows paint ([S-872]): a hidden widget takes nothing.
            var painted = GridGeometry.Inset(GridGeometry.ToPixels(card.Model.Rect, metrics), Design.WidgetGapPx);
            if (CardFrame.Hit(painted, card.IsShown(EditMode)) is not { } box)
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

    /// <summary>[S-06a]/[S-06c] The snap grid, drawn only while editing, on the surface's own pixels.</summary>
    private void DrawGridOverlay()
    {
        var metrics = Metrics;
        if (!metrics.IsUsable || !Design.GridSnap)
        {
            _gridOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        _gridOverlay.Draw(metrics, RasterScale);
        _gridOverlay.Visibility = Visibility.Visible;
    }
}
