using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services.Files;
using Themia.Core.Desktop;
using Themia.Core.Settings;
using Themia.Sdk;

namespace Themia.App.Shell;

/// <summary>
/// [S-13700]..[S-13713] The Unplaced icons page on this display: the desktop's icons the view shown here does not
/// place, read for this display's view each time it is drawn; placing them at the next free cells (a double-click,
/// Enter), dragging them onto the desktop (the one placement session, let go where the pointer lets go); desktop icons
/// moved onto it leaving the view (the inverse, [S-13713]: the page stays open while the desktop is used, [S-13715]); and the
/// entry's badge on the bar. The rules are Core's (<see cref="UnplacedItems"/>).
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>[S-13705] A placement the page's drag began: it lands where the pointer lets go.</summary>
    private bool _unplacedDrag;

    /// <summary>[S-13705] The page was open when its drag began: it opens again once the placement is over.</summary>
    private bool _reopenUnplaced;

    private bool _unplacedReleaseHooked;

    /// <summary>[S-13713] The open page's live part (the last one built), lit while desktop icons dragged over it would land on it.</summary>
    private UnplacedPageView? _unplacedPart;

    /// <summary>[S-13700] The page's live part, over this display's view.</summary>
    private UnplacedPageView UnplacedPart() => _unplacedPart = new(new UnplacedCommands(
        Read: UnplacedNow,
        Place: keys => _host.PlaceDesktopItems(CurrentViewId, keys),
        Drag: BeginUnplacedDrag,
        Icon: HostDesktopItems.Instance.IconAsync));

    /// <summary>[S-13713] The page's live part while its flyout is open here; null otherwise.</summary>
    internal UnplacedPageView? OpenUnplacedPart => _open.PageId == SettingsPageId.DesktopItems && _flyout is not null ? _unplacedPart : null;

    /// <summary>
    /// [S-13713] Whether <paramref name="at"/> (the root's DIPs) is over the Unplaced icons page open on this display, its panel
    /// (the beak not counted): where a move of desktop icons let go takes them off the view.
    /// </summary>
    internal bool OverUnplacedPage(Windows.Foundation.Point at)
    {
        if (!EditMode || _open.PageId != SettingsPageId.DesktopItems || _flyout is not { } flyout)
        {
            return false;
        }

        var panel = flyout.Panel;
        var corner = _flyoutLayer.TransformToVisual(_root).TransformPoint(new Windows.Foundation.Point(panel.X, panel.Y));
        return at.X >= corner.X && at.X < corner.X + panel.Width && at.Y >= corner.Y && at.Y < corner.Y + panel.Height;
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

    /// <summary>[S-13701] How many desktop icons this display's view does not place: the entry's badge.</summary>
    private int UnplacedCount() => _closed ? 0 : _host.UnplacedDesktopItems(CurrentViewId).Count;

    /// <summary>[S-13706] A read of the desktop came in: the badge and an open page follow it.</summary>
    private void OnDesktopListedForUnplaced()
    {
        if (_closed || !EditMode)
        {
            return;
        }

        ShowEditBar();
        if (_open.PageId == SettingsPageId.DesktopItems)
        {
            SyncBarMenu();
        }
    }

    /// <summary>
    /// [S-13705] Items dragged off the page: their placement starts at once (the page closes as every flyout does for a
    /// placement) and lands where the pointer lets go, on this display or another; a press places it as any placement.
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

        _reopenUnplaced = _open.PageId == SettingsPageId.DesktopItems;
        _unplacedDrag = true;
        _host.BeginPlacement(items, Metrics);
        if (_host.Placing is null)
        {
            _unplacedDrag = false;
            _reopenUnplaced = false;
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

    /// <summary>[S-13705] The placement the page's drag began is over (placed or given up): the page opens again.</summary>
    private void ReopenUnplacedAfterPlacing()
    {
        if (!_reopenUnplaced || _host.Placing is not null)
        {
            return;
        }

        _reopenUnplaced = false;
        _unplacedDrag = false;

        // Not inside the release that placed it: the page is put on its layer after that event.
        if (!_root.DispatcherQueue.TryEnqueue(() => OpenPage(SettingsPageId.DesktopItems, [])))
        {
            Core.Diagnostics.Log.Warn(Scope, "the unplaced icons page could not open again after its drag");
        }
    }
}
