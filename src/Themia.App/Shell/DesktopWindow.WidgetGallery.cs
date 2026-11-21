using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>
/// [S-14910]..[S-14917] The widget gallery on this display: opened from its button beside Add widget (the add page stays
/// as it is), over the work area under the bar, one of the bar's flyouts (one at a time); a drag off it is the one
/// placement session, landing where the pointer lets go, the gallery stepping aside while it is aimed and coming back after.
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>[S-14911] The gallery's layer: over the cards, their marks and the ghost, under the bar and its flyouts.</summary>
    private readonly Canvas _galleryLayer = new();

    /// <summary>[S-14910] The gallery, while it is open on this display.</summary>
    private WidgetGalleryView? _widgetGallery;

    /// <summary>[S-14916] A placement the gallery's drag began: it lands where the pointer lets go, and the gallery stays for it.</summary>
    private bool _galleryDragging;

    private bool _galleryReleaseHooked;

#if DEBUG
    /// <summary>Debug only: the gallery open here, for its render and its check.</summary>
    internal WidgetGalleryView? DebugGallery => _widgetGallery;

    /// <summary>Debug only: a flyout of the bar other than the gallery is open here (the selection's page over it would be).</summary>
    internal bool BarMenuOpenOtherThanGallery => _flyout is not null || _addPage is not null;
#endif

    /// <summary>[S-14910] The bar's Widget gallery: the gallery opened, or closed again.</summary>
    private void ToggleWidgetGallery()
    {
        if (_widgetGallery is not null)
        {
            CloseWidgetGallery();
        }
        else
        {
            OpenWidgetGallery();
        }
    }

    /// <summary>
    /// [S-14910] [S-14912] The gallery over this display's work area: every widget the add page offers, by its subjects,
    /// built afresh each time it opens ([S-1109]). Only in edit mode and not while placing; any other flyout closes first.
    /// </summary>
    internal void OpenWidgetGallery()
    {
        if (_closed || !EditMode || _host.Placing is not null || _widgetGallery is not null)
        {
            return;
        }

        CloseFlyout(focusEntry: false);
        var page = AddPage.Of([.. ModuleCatalog.Widgets.Select(d => d.Id)], ReadyMadeGroups.Offered());
        var gallery = new WidgetGalleryView(new WidgetGalleryCommands(BeginGalleryDrag), Backdrop, _host.State.Design, WidgetGallery.Of(page));
        _widgetGallery = gallery;
        _galleryLayer.Children.Add(gallery);
        PlaceWidgetGallery();
        ShowEditBar();
        Log.Info(Scope, $"widget gallery open on {_monitor.GdiName}: {gallery.Screens.Count} screen(s)");
    }

    /// <summary>[S-14910] Close the gallery, its cards and samples with it; never while its own drag is aimed.</summary>
    private void CloseWidgetGallery()
    {
        if (_widgetGallery is not { } gallery || _galleryDragging)
        {
            return;
        }

        _galleryLayer.Children.Remove(gallery);
        gallery.Dispose();
        _widgetGallery = null;
        ShowEditBar();
    }

    /// <summary>[S-14911] Over the work area under the bar, on this display's grid.</summary>
    private void PlaceWidgetGallery()
    {
        var metrics = Metrics;
        if (_closed || _widgetGallery is not { } gallery || !metrics.IsUsable)
        {
            return;
        }

        var area = WorkArea;
        var bar = EditBarRect(area);
        gallery.Place(metrics, RasterScale, area, bar.Y + bar.Height, GridGeometry.AllowedDips(metrics), SizesNow.Scale);
    }

    /// <summary>
    /// [S-14916] Widgets dragged off the gallery: the placement of new widgets laid as they stood starts at once, held where
    /// the pointer held them, and lands where it lets go; the gallery steps aside meanwhile, its picked cards following the ghost.
    /// </summary>
    private void BeginGalleryDrag(ViewState screen, IReadOnlyList<string> ids, Point at)
    {
        if (_closed || _widgetGallery is not { } gallery || _host.Placing is not null)
        {
            return;
        }

        var (x, y) = FractionOfDip(at);
        if (PlacementPayload.ForGallery(screen, ids, x, y) is not { } payload)
        {
            return;
        }

        if (!_galleryReleaseHooked)
        {
            _galleryReleaseHooked = true;
            _root.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnGalleryDragReleased), handledEventsToo: true);
        }

        _galleryDragging = true;
        gallery.BeginFollow(payload.RootIds);
        _host.BeginPlacement(payload);
        if (_host.Placing is null)
        {
            // [S-1065] Refused before the ghost appears: the gallery stays as it was.
            _galleryDragging = false;
            gallery.EndFollow();
        }
    }

    /// <summary>[S-14916] The drag's release: the placement lands at that point.</summary>
    private void OnGalleryDragReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_galleryDragging)
        {
            return;
        }

        if (_host.Placing is not null && EditMode)
        {
            var (x, y) = FractionOfDip(e.GetCurrentPoint(_root).Position);
            _host.CompletePlacement(this, x, y);
            e.Handled = true;
        }
    }

    /// <summary>
    /// [S-14916] The ghost moved: the gallery's cards picked up stand on it, the real cards in place of its outlines (a card's
    /// fill is see-through: an outline under it would show). False when no gallery drag is under way: the outlines show.
    /// </summary>
    private bool FollowGalleryDrag(IReadOnlyList<GhostOutline> outlines)
    {
        if (!_galleryDragging || _widgetGallery is not { } gallery)
        {
            return false;
        }

        gallery.Follow(outlines);
        return true;
    }

    /// <summary>[S-14916] The placement the gallery's drag began is over (landed or given up): the gallery comes back as it stood.</summary>
    private void GalleryAfterPlacing()
    {
        if (!_galleryDragging || _host.Placing is not null)
        {
            return;
        }

        _galleryDragging = false;
        _widgetGallery?.EndFollow();
    }

    /// <summary>[S-14913] A key while the gallery is open: its own first (the screens, Ctrl+A, Escape on a pick).</summary>
    private bool GalleryKey(KeyRoutedEventArgs args)
        => _widgetGallery is { } gallery && _host.Placing is null && gallery.HandleKey(args.Key, IsDown(Windows.System.VirtualKey.Control));

#if DEBUG
    /// <summary>Debug only: a drag off the gallery begun as its pointer begins one, for the gallery's check.</summary>
    internal void BeginGalleryDragForCheck(ViewState screen, IReadOnlyList<string> ids, Point at) => BeginGalleryDrag(screen, ids, at);

    /// <summary>Debug only: the pointer moved while placing, as the surface's own move hands it on.</summary>
    internal IReadOnlyList<GhostOutline> MovePlacementForCheck(Point at)
    {
        if (_host.Placing is not { } payload)
        {
            return [];
        }

        var (x, y) = FractionOfDip(at);
        var outlines = payload.Outlines(x, y, Metrics, SizesNow);
        if (!FollowGalleryDrag(outlines))
        {
            ShowGhosts(outlines);
        }

        return outlines;
    }

    /// <summary>Debug only: the pointer let go while placing, as the gallery's release lands it.</summary>
    internal bool ReleasePlacementForCheck(Point at)
    {
        if (!_galleryDragging || _host.Placing is null)
        {
            return false;
        }

        var (x, y) = FractionOfDip(at);
        return _host.CompletePlacement(this, x, y);
    }
#endif
}
