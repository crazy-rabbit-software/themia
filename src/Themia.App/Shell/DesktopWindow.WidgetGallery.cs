using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>
/// [S-14910]..[S-14917] [S-16728] The widget gallery on this display, the add page (owner, 2026-11-23: "the widget gallery
/// will be the new add widget page"): opened from Add widget, a window flush under the bar hung from it by a beak ([S-16558]),
/// one of the bar's flyouts (one at a time); a drag off it is the one placement session, landing where the pointer lets go,
/// the gallery stepping aside while it is aimed and coming back after. Its last tab is Not on view ([S-16727]).
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>[S-14911] The gallery's layer: over the cards, their marks and the ghost, under the bar and its flyouts.</summary>
    private readonly Canvas _galleryLayer = new();

    /// <summary>[S-14910] The gallery, while it is open on this display.</summary>
    private WidgetGalleryView? _widgetGallery;

    /// <summary>[S-14916] A placement the gallery's drag began: it lands where the pointer lets go, and the gallery stays for it.</summary>
    private bool _galleryDragging;

    /// <summary>[S-16408] What the gallery's last drag placed: when it lands (on any display) the gallery closes for its settings.</summary>
    private PlacementPayload? _galleryPayload;

    private bool _galleryReleaseHooked;

    /// <summary>[S-16727] A placement of icons dragged off the Not on view tab: the gallery steps aside for it and comes back after.</summary>
    private bool _galleryIcons;

    /// <summary>[S-13018] The lassoed area the gallery was opened for: Enter's widget goes into it; null for none.</summary>
    private FracRect? _galleryArea;

#if DEBUG
    /// <summary>Debug only: the gallery open here, for its render and its check.</summary>
    internal WidgetGalleryView? DebugGallery => _widgetGallery;

    /// <summary>Debug only: a flyout of the bar other than the gallery is open here (the selection's page over it would be).</summary>
    internal bool BarMenuOpenOtherThanGallery => _flyout is not null;

    /// <summary>Debug only: the selection's page is open here (after a drop off the gallery, [S-16408]).</summary>
    internal bool SelectionPageOpen => _open.IsSelection && _flyout is not null;
#endif

    /// <summary>[S-14910] [S-16728] The bar's Add widget: the gallery opened, or closed again.</summary>
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
    /// [S-14910] [S-14912] The gallery's window on this display: every whole widget the add tree offers, by its subjects,
    /// and [S-16727] the Not on view tab, built afresh each time it opens ([S-1109]). Only in edit mode and not while placing;
    /// any other flyout closes first.
    /// </summary>
    internal void OpenWidgetGallery()
    {
        if (_closed || !EditMode || _host.Placing is not null || _widgetGallery is not null)
        {
            return;
        }

        CloseFlyout(focusEntry: false);
        CloseWidgetGallery();
        _galleryArea = null;
        var page = AddPage.Of([.. ModuleCatalog.Widgets.Select(d => d.Id)], ReadyMadeGroups.Offered());
        var commands = new WidgetGalleryCommands(BeginGalleryDrag, () => _ = _root.Focus(FocusState.Programmatic))
        {
            PlaceHere = PlaceInArea,
            Unplaced = UnplacedPart,
            UnplacedTab = UnplacedTab,
        };
        var gallery = new WidgetGalleryView(commands, Backdrop, _host.State.Design, page);
        _widgetGallery = gallery;
        _galleryLayer.Children.Add(gallery);
        PlaceWidgetGallery();
        Become(OpenFlyout.Gallery);
        Log.Info(Scope, $"widget gallery open on {_monitor.GdiName}: {gallery.Categories.Count} categories");
    }

    /// <summary>[S-14910] Close the gallery, its cards and samples with it; never while its own drag is aimed.</summary>
    private void CloseWidgetGallery()
    {
        if (_widgetGallery is not { } gallery || _galleryDragging || _galleryIcons)
        {
            return;
        }

        _galleryArea = null;

        _galleryLayer.Children.Remove(gallery);
        gallery.Dispose();
        _widgetGallery = null;
        Become(OpenFlyout.None);
    }

    /// <summary>[S-16400] Flush under the bar, as the add page hangs, its cards on this display's grid.</summary>
    private void PlaceWidgetGallery()
    {
        var metrics = Metrics;
        if (_closed || _widgetGallery is not { } gallery || !metrics.IsUsable)
        {
            return;
        }

        var area = WorkArea;
        var bar = EditBarRect(area);
        gallery.Place(metrics, RasterScale, area, bar.Y + bar.Height, GridGeometry.AllowedDips(metrics), SizesNow.Scale, _editBar.GalleryBox(_galleryLayer));
    }

    /// <summary>[S-13018] Enter's one widget into the lassoed area the gallery was opened for, at once; false: none, or it did not go.</summary>
    private bool PlaceInArea(ViewState screen, IReadOnlyList<string> ids)
    {
        if (_galleryArea is not { } area || ids.Count != 1 || screen.Find(ids[0]) is not { } widget || !_host.PlaceWidgetIn(this, WidgetPick.Of(widget.Config), area))
        {
            return false;
        }

        CloseWidgetGallery();
        return true;
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
        _galleryPayload = payload;
        gallery.BeginFollow(payload.RootIds);
        _host.BeginPlacement(payload);
        if (_host.Placing is null)
        {
            // [S-1065] Refused before the ghost appears: the gallery stays as it was.
            _galleryDragging = false;
            _galleryPayload = null;
            gallery.EndFollow();
        }
    }

    /// <summary>
    /// [S-16408] A placement landed (owner, 2026-11-23: "when placing a widget from the bento preview it should open the
    /// combined widgets settings not the bento again"): one the gallery's drag began closes the gallery, and on the display it
    /// landed on the placed widgets, selected, have their page opened (for several, the page of several).
    /// </summary>
    private void OnPlaced(PlacementPayload payload, DesktopWindow surface)
    {
        if (_closed || !ReferenceEquals(payload, _galleryPayload))
        {
            return;
        }

        _galleryPayload = null;
        _galleryDragging = false;
        if (WidgetGallery.AfterPlacing(landed: true) != GalleryAfter.Settings)
        {
            return;
        }

        CloseWidgetGallery();
        if (ReferenceEquals(surface, this))
        {
            SyncProperties();
            OpenSelectionPage(focus: false);
        }
        else
        {
            surface.ReopenSelectionPage();
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

    /// <summary>
    /// [S-14916] The placement the gallery's drag began is over: the gallery comes back as it stood (given up; [S-16408] a
    /// landing closes it right after, <see cref="OnPlaced"/>).
    /// </summary>
    private void GalleryAfterPlacing()
    {
        if (_host.Placing is not null)
        {
            return;
        }

        // [S-16727] Icons dragged off the Not on view tab: placed or given up, the gallery is back on that tab.
        if (_galleryIcons)
        {
            _galleryIcons = false;
            _widgetGallery?.EndFollow();
            _widgetGallery?.SyncUnplaced();
        }

        if (!_galleryDragging)
        {
            return;
        }

        _galleryDragging = false;
        _widgetGallery?.EndFollow();
    }

    /// <summary>[S-14913] A key while the gallery is open: its own first (scrolling, Ctrl+A, Escape on a pick).</summary>
    private bool GalleryKey(KeyRoutedEventArgs args)
        => _widgetGallery is { } gallery && _host.Placing is null && gallery.HandleKey(args.Key, IsDown(Windows.System.VirtualKey.Control));

#if DEBUG
    /// <summary>Debug only: this display's work area (DIPs), for the gallery's check.</summary>
    internal DipRect WorkAreaForCheck => WorkArea;

    /// <summary>Debug only: the gallery's button on the bar (surface DIPs), for the gallery's check ([S-16558]).</summary>
    internal DipRect? GalleryButtonForCheck => _editBar.GalleryBox(_galleryLayer);

    /// <summary>Debug only: the edit bar's foot on this display (DIPs), for the gallery's check ([S-16400]).</summary>
    internal double BarBottomForCheck => EditBarRect(WorkArea) is var bar ? bar.Y + bar.Height : 0;

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
