using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Windows.Foundation;
using Themia.Core.Shell;

namespace Themia.App.Shell;

/// <summary>
/// [S-906]..[S-915] This display's view bar: what it offers, where it sits, the clicks and drags
/// on it, and its share of the hit region. The rules are in Core <see cref="ViewBarRules"/>; every
/// switch goes through <see cref="AppState.ShowViewOn"/> ([S-19]).
/// </summary>
internal sealed partial class DesktopWindow : IViewBarOwner
{
    /// <summary>The layer the bar is positioned on: above the widgets, under the ghosts and the toolbar.</summary>
    private readonly Canvas _barLayer = new();

    private readonly ViewBar _viewBar;

    /// <summary>[S-913] Where a bar being dragged would drop, drawn there live; null when no drag is on.</summary>
    private ViewBarAnchor? _barDragAnchor;

    /// <summary>Draw the bar for the current state and put it at its anchor.</summary>
    private void RefreshViewBar()
    {
        var state = _host.State;
        var settings = ViewBarRules.For(state, _monitor.Id);
        var show = ViewBarRules.Show(EditMode, settings, state.Views);
        var metrics = Metrics;

        if (show == ViewBarShow.None || !metrics.IsUsable)
        {
            _viewBar.Visibility = Visibility.Collapsed;
            return;
        }

        var anchor = _barDragAnchor ?? settings.Anchor;
        var shownId = CurrentViewId;
        var items = ViewBarRules.VisibleViews(state.Views, settings)
            .Select(v => new ViewBarItem(
                v.Id,
                v.Name,
                string.IsNullOrEmpty(v.Icon) ? null : v.Icon,
                v.Id == shownId,
                v.Id == state.DefaultViewForNewDisplays,
                v.Color))
            .ToList();

        // [S-917] The chips that fit along this display's work area at this anchor; the rest go
        // behind the overflow chip. Refit on every refresh: a resize, a DPI or work-area change, a
        // drag to another anchor, the toolbar coming and going, a view added.
        var area = GridGeometry.AllowedPixels(metrics);
        var room = ViewBarRules.Room(area, anchor, EditMode ? ToolbarRect(area) : null, settings.Style);

        _viewBar.Update(
            items,
            new ViewBarLook(settings.Size, settings.IconOnly, ViewBarRules.IsVertical(anchor, settings.Style), EditMode, show == ViewBarShow.HiddenPreview, settings.Style),
            Design,
            room);
        _viewBar.Visibility = Visibility.Visible;
        PositionViewBar(anchor);
    }

    /// <summary>[S-909] Put the bar at its anchor in the work area, clear of the edit toolbar.</summary>
    private void PositionViewBar(ViewBarAnchor anchor)
    {
        var metrics = Metrics;
        if (!metrics.IsUsable || _viewBar.Visibility != Visibility.Visible)
        {
            return;
        }

        _viewBar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _viewBar.DesiredSize;
        var area = GridGeometry.AllowedPixels(metrics);
        var (x, y) = ViewBarRules.Place(area, size.Width, size.Height, anchor, EditMode ? ToolbarRect(area) : null);

        Canvas.SetLeft(_viewBar, x);
        Canvas.SetTop(_viewBar, y);
    }

    /// <summary>
    /// The edit toolbar's rect in the surface's DIPs. Computed from its measured size and its
    /// alignment (centred along the top of the work area, [S-852]) rather than read back from
    /// layout, because it has just been made visible when edit mode starts and has not been laid
    /// out yet.
    /// </summary>
    private PixelRect ToolbarRect(PixelRect area)
    {
        _toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var margin = _toolbar.Margin;
        var width = Math.Max(0, _toolbar.DesiredSize.Width - margin.Left - margin.Right);
        var height = Math.Max(0, _toolbar.DesiredSize.Height - margin.Top - margin.Bottom);
        return new PixelRect(area.X + ((area.Width - width) / 2), area.Y + ChromeTopMargin, width, height);
    }

    /// <summary>[S-912] The bar's box in the surface's DIPs, for the hit region; null when it is not drawn.</summary>
    private PixelRect? ViewBarBox()
    {
        if (_viewBar.Visibility != Visibility.Visible)
        {
            return null;
        }

        var width = _viewBar.ActualWidth > 0 ? _viewBar.ActualWidth : _viewBar.DesiredSize.Width;
        var height = _viewBar.ActualHeight > 0 ? _viewBar.ActualHeight : _viewBar.DesiredSize.Height;
        return width > 0 && height > 0 ? new PixelRect(Canvas.GetLeft(_viewBar), Canvas.GetTop(_viewBar), width, height) : null;
    }

    /// <summary>The bar's real size arrived (or changed with a rename): move it and its hit region with it.</summary>
    private void OnViewBarSizeChanged(object sender, SizeChangedEventArgs args)
    {
        PositionViewBar(_barDragAnchor ?? ViewBarRules.For(_host.State, _monitor.Id).Anchor);
        ApplyHitRegion();
    }

    // ---- IViewBarOwner -----------------------------------------------------------------------

    /// <inheritdoc />
    public void SwitchTo(string viewId)
    {
        if (viewId != CurrentViewId)
        {
            _host.ShowView(_monitor.Id, viewId);
        }
    }

    /// <inheritdoc />
    public void CommitReorder(string draggedId, string targetId) => _host.ReorderView(draggedId, targetId);

    /// <summary>[S-915] Duplicate the view (and show the copy here), or open its settings ([S-3546]: View ▾ at its submenu) — the original's chip menu.</summary>
    public void ShowChipMenu(FrameworkElement chip, string viewId, RightTappedRoutedEventArgs args)
    {
        var monitorId = _monitor.Id;
        ItemMenu.Show(chip, args,
        [
            new ItemMenuEntry(Glyphs.Duplicate, Str.App_DuplicateView.Text(), () => _host.DuplicateView(viewId, monitorId), Quick: QuickCommand.Duplicate),
            new ItemMenuEntry(Glyphs.Settings, Str.App_ViewSettingsBtn.Text(), () => OpenViewSettings(viewId), Quick: QuickCommand.Settings),
        ]);
    }

    /// <summary>
    /// [S-918] / [S-5402] The views that did not fit (or, from a drop-down, every view on the bar), in
    /// a menu opening into the display and no taller than the work area there. A standard flyout on
    /// the surface: it opens without the surface taking the keyboard outside edit mode, and a click
    /// outside it closes it.
    /// </summary>
    public void ShowViewMenu(FrameworkElement button, IReadOnlyList<ViewBarItem> views)
    {
        var metrics = Metrics;
        if (!metrics.IsUsable || ViewBarBox() is not { } bar)
        {
            return;
        }

        var side = ViewBarRules.MenuSide(_barDragAnchor ?? ViewBarRules.For(_host.State, _monitor.Id).Anchor);
        var menu = ViewBar.ViewMenu(views, SwitchTo, ViewBarRules.MenuHeight(GridGeometry.AllowedPixels(metrics), bar, side));
        menu.ShowAt(button, new FlyoutShowOptions { ShowMode = FlyoutShowMode.Standard, Placement = ViewBar.MenuPlacement(side) });
    }

    /// <inheritdoc />
    public void BarDragMoved(Point at)
    {
        var metrics = Metrics;
        if (!metrics.IsUsable)
        {
            return;
        }

        var anchor = ViewBarRules.PickAnchor(GridGeometry.AllowedPixels(metrics), at.X, at.Y);
        if (anchor != _barDragAnchor)
        {
            _barDragAnchor = anchor;
            RefreshViewBar();
        }
    }

    /// <summary>[S-913] Dropped: the anchor under the pointer becomes this display's, saved like any other setting.</summary>
    public void BarDragEnded(Point at, bool moved)
    {
        var metrics = Metrics;
        _barDragAnchor = null;

        if (moved && metrics.IsUsable)
        {
            var anchor = ViewBarRules.PickAnchor(GridGeometry.AllowedPixels(metrics), at.X, at.Y);
            var monitorId = _monitor.Id;
            _host.Mutate(state => ViewBarRules.Update(state, monitorId, bar => bar.Anchor = anchor));
            return;
        }

        RefreshViewBar();
    }
}
