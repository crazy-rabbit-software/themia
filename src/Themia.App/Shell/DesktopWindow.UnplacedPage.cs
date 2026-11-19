using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services.Files;
using Themia.Core.Desktop;
using Themia.Core.Settings;
using Themia.Sdk;

namespace Themia.App.Shell;

/// <summary>
/// [S-13700]..[S-13706] The Unplaced icons page on this display: the desktop's icons the view shown here does not
/// place, read for this display's view each time it is drawn; placing them at the next free cells, putting them in a
/// fence, dragging them onto the desktop (the one placement session, let go where the pointer lets go); and the
/// entry's badge on the bar. The rules are Core's (<see cref="UnplacedItems"/>).
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>[S-13705] A placement the page's drag began: it lands where the pointer lets go.</summary>
    private bool _unplacedDrag;

    /// <summary>[S-13705] The page was open when its drag began: it opens again once the placement is over.</summary>
    private bool _reopenUnplaced;

    private bool _unplacedReleaseHooked;

    /// <summary>[S-13700] The page's live part, over this display's view.</summary>
    private UnplacedPageView UnplacedPart() => new(new UnplacedCommands(
        Read: UnplacedNow,
        Place: keys => _host.PlaceDesktopItems(CurrentViewId, keys),
        PutIn: (fence, keys) => _host.PutDesktopItemsInFence(CurrentViewId, fence, keys),
        Drag: BeginUnplacedDrag,
        Icon: HostDesktopItems.Instance.IconAsync));

    /// <summary>[S-13700] What the page shows now: this display's view, what it does not place, and its fences.</summary>
    private UnplacedModel UnplacedNow()
    {
        var view = ViewForThisMonitor(_host.State);
        return new UnplacedModel(
            view.Name,
            DesktopViews.Holds(view),
            _host.UnplacedDesktopItems(view.Id),
            [.. UnplacedItems.Fences(view).Select(f => (f.Widget.Id, string.IsNullOrWhiteSpace(f.Fence.Name) ? ModuleCatalog.Of(f.Widget.Kind).Name : f.Fence.Name))]);
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

        ShowToolbar();
        if (_openPage == SettingsPageId.DesktopItems)
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

        _reopenUnplaced = _openPage == SettingsPageId.DesktopItems;
        _unplacedDrag = true;
        _host.BeginPlacement(items, Metrics);
        if (_host.Placing is null)
        {
            _unplacedDrag = false;
            _reopenUnplaced = false;
        }
    }

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
