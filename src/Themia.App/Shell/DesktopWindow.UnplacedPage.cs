using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services.Files;
using Themia.Core.Desktop;
using Themia.Core.Settings;
using Themia.Sdk;

namespace Themia.App.Shell;

/// <summary>
/// [S-13700]..[S-13713] [S-16727] The Unplaced icons page on this display, the widget gallery's Not on view tab: the desktop's
/// icons the view shown here does not place, read for this display's view each time it is drawn; placing them at the next
/// free cells (a double-click, Enter), dragging them onto the desktop (the one placement session, let go where the pointer
/// lets go, the gallery stepping aside meanwhile); desktop icons moved onto it leaving the view (the inverse, [S-13713]). The
/// rules are Core's (<see cref="UnplacedItems"/>).
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>[S-13705] A placement the page's drag began: it lands where the pointer lets go.</summary>
    private bool _unplacedDrag;

    private bool _unplacedReleaseHooked;

    /// <summary>[S-13700] The page's live part, over this display's view: the gallery's Not on view tab shows it.</summary>
    private UnplacedPageView UnplacedPart() => new(new UnplacedCommands(
        Read: UnplacedNow,
        Place: keys => _host.PlaceDesktopItems(CurrentViewId, keys),
        Drag: BeginUnplacedDrag,
        Icon: HostDesktopItems.Instance.IconAsync));

    /// <summary>[S-13713] [S-16727] The page's live part while the gallery shows its Not on view tab here; null otherwise.</summary>
    internal UnplacedPageView? OpenUnplacedPart => _widgetGallery?.UnplacedPart;

    /// <summary>
    /// [S-13713] [S-16727] Whether <paramref name="at"/> (the root's DIPs) is over the gallery's Not on view tab here, its well:
    /// where a move of desktop icons let go takes them off the view.
    /// </summary>
    internal bool OverUnplacedPage(Windows.Foundation.Point at)
    {
        if (!EditMode || _widgetGallery is not { UnplacedRoom: { } room })
        {
            return false;
        }

        var corner = _galleryLayer.TransformToVisual(_root).TransformPoint(new Windows.Foundation.Point(room.X, room.Y));
        return at.X >= corner.X && at.X < corner.X + room.Width && at.Y >= corner.Y && at.Y < corner.Y + room.Height;
    }

    /// <summary>[S-13713] The page lit as a drop target, or not; a move's every step says which.</summary>
    private void ShowUnplacedDrop(bool over) => OpenUnplacedPart?.DropOver(over);

    /// <summary>
    /// [S-13700] What the page shows now: what this display's view does not place; [S-13707] its icons in
    /// the box the desktop gives an icon on this display (the desktop's own, <c>DeskIconBox</c>: it follows the icon size).
    /// </summary>
    private UnplacedModel UnplacedNow()
    {
        var view = ViewForThisMonitor(_host.State);
        var metrics = Metrics;
        return new UnplacedModel(
            DesktopViews.Holds(view),
            _host.UnplacedDesktopItems(view.Id))
        {
            Box = UnplacedItems.Dips(_host.DeskIconBox(metrics), metrics),
            Title = UnplacedItems.Title(view.Name),
            Pitch = UnplacedItems.Pitch(metrics),
        };
    }

    /// <summary>[S-16727] The Not on view tab's words ("Not on" this display's view) and its count.</summary>
    private (string Title, int Count) UnplacedTab()
    {
        var view = ViewForThisMonitor(_host.State);
        return (UnplacedItems.Title(view.Name), _closed ? 0 : _host.UnplacedDesktopItems(view.Id).Count);
    }

    /// <summary>[S-13706] A read of the desktop came in: the gallery's tab and its page follow it.</summary>
    private void OnDesktopListedForUnplaced()
    {
        if (_closed || !EditMode)
        {
            return;
        }

        _widgetGallery?.SyncUnplaced();
    }

    /// <summary>
    /// [S-13705] Items dragged off the page: their placement starts at once (the gallery steps aside for it, [S-16727]) and
    /// lands where the pointer lets go, on this display or another; a press places it as any placement.
    /// </summary>
    private void BeginUnplacedDrag(IReadOnlyList<string> keys)
    {
        var items = keys.Select(HostDesktopItems.Instance.Find).OfType<DesktopItem>().ToList();
        if (items.Count == 0)
        {
            return;
        }

        if (!_unplacedReleaseHooked)
        {
            _unplacedReleaseHooked = true;
            _root.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnUnplacedDragReleased), handledEventsToo: true);
        }

        _unplacedDrag = true;
        if (_widgetGallery is { } gallery)
        {
            _galleryIcons = true;
            gallery.BeginFollow([]);
        }

        _host.BeginPlacement(items, Metrics);
        if (_host.Placing is null)
        {
            _unplacedDrag = false;
            if (_galleryIcons)
            {
                _galleryIcons = false;
                _widgetGallery?.EndFollow();
            }
        }
    }

    /// <summary>A check's hand: these items dragged off the page, as a press past the dead zone on them does.</summary>
    internal void DebugUnplacedDrag(IReadOnlyList<string> keys) => BeginUnplacedDrag(keys);

    /// <summary>[S-13705] The drag's release: the placement lands at that point.</summary>
    private void OnUnplacedDragReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_unplacedDrag)
        {
            return;
        }

        _unplacedDrag = false;
        if (_host.Placing is not null && EditMode)
        {
            var (x, y) = FractionOfDip(e.GetCurrentPoint(_root).Position);
            _host.CompletePlacement(this, x, y);
            e.Handled = true;
        }
    }
}
