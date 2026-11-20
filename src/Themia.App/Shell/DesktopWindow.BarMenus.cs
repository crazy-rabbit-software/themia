using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Geometry;
using Themia.Core.Settings;
using Themia.Core.Shell;
using Windows.System;

namespace Themia.App.Shell;

/// <summary>
/// [S-2325]..[S-2327] The edit bar's flyouts on this display, one mechanism for every one of them: a page's
/// entry, or the selection's, opens its flyout hung from it; one open at a time; it closes on its entry
/// again, Escape (after going back out of what it opened), a press anywhere that is not the bar or the
/// flyout, the add menu opening, a placement, and leaving edit mode. Nothing is open as edit mode starts.
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>[S-2325] The flyouts' layer: over the cards and the edit bar, after it in the keyboard's order.</summary>
    private readonly Canvas _flyoutLayer = new();

    /// <summary>The open flyout, if any.</summary>
    private PageFlyout? _flyout;

    /// <summary>The page whose flyout is open; null with the selection's open or none.</summary>
    private SettingsPageId? _openPage;

    /// <summary>[S-2343] The selection's flyout is open.</summary>
    private bool _selectionOpen;

    /// <summary>
    /// [S-2346] The open flyout drawn again: never under a press held on it, never inside the event that asked;
    /// made with the first flyout, when the surface's dispatcher is there.
    /// </summary>
    private FlyoutRedraws? _redraws;

    /// <summary>[S-2327] Whether one of the edit bar's flyouts is open on this display.</summary>
    internal bool BarMenuOpen => _flyout is not null || _addPage is not null;

    /// <summary>[S-2300] A page's rows, as the state and Windows hold them now.</summary>
    private Func<PageModel> PageOf(SettingsPageId page)
    {
        var settings = new SurfaceSettingsHost(this);
        var display = new SurfaceDisplayHost(this);
        var views = new SurfaceViewsHost(this);
        return page switch
        {
            SettingsPageId.General => () => PageRows.Of(SettingsPages.General(settings)),
            SettingsPageId.Accounts => () => PageRows.Of(SettingsPages.Accounts(settings)),
            SettingsPageId.Display => () => PageRows.Of(SettingsPages.Display(display)),
            SettingsPageId.Views => () => PageRows.Of(SettingsPages.Views(views)),

            // [S-13700] Its list: this display's view's unplaced desktop icons.
            SettingsPageId.DesktopItems => () => PageRows.Of(SettingsPages.DesktopItems(), UnplacedPart),
            // [S-2311] Its preview: this display's wallpaper and real cards in the design as it stands.
            _ => () => PageRows.Of(SettingsPages.Appearance(settings), () => new AppearancePreview(Backdrop, Metrics, () => _host.State.Design)),
        };
    }

    /// <summary>[S-2351] An "are you sure?" asked after a step of its own, in place in the card of the open flyout's row keyed <paramref name="at"/>.</summary>
    private void ConfirmOnFlyout(string at, SettingsConfirm confirm) => _flyout?.Page.ConfirmAt(at, PageRows.QuestionOf(confirm));

    /// <summary>[S-2327] A page's entry opens its flyout, or closes it again; another open closes first.</summary>
    private void TogglePage(SettingsPageId page)
    {
        if (_openPage == page)
        {
            CloseFlyout(focusEntry: false);
        }
        else
        {
            OpenPage(page, []);
        }
    }

    /// <summary>
    /// [S-2327] [S-3534] Open a page's flyout on this display at the row along <paramref name="path"/> (a
    /// section's key, then the rows'; empty for its top), the keyboard in it. Only in edit mode and not
    /// while placing: the bar is not there otherwise.
    /// </summary>
    internal void OpenPage(SettingsPageId page, IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (_closed || !EditMode || _host.Placing is not null)
        {
            return;
        }

        if (_openPage != page)
        {
            CloseFlyout(focusEntry: false);
            Show(new PageView(PageOf(page)));
            _openPage = page;
            if (page is SettingsPageId.General or SettingsPageId.Accounts)
            {
                SettingsMenuOpened();
            }
        }

        _flyout!.Page.Show();
        ShowToolbar();
        PlaceFlyout();
        if (path.Count > 0 ? !_flyout.Page.Open(path) : !_flyout.Page.FocusFirst())
        {
            Core.Diagnostics.Log.Debug(Scope, $"the {page} page opened, but not at '{string.Join('/', path)}'");
        }

        PlaceFlyout();
    }

    /// <summary>[S-2343] The selection's flyout: its page built over the selection as it is now.</summary>
    private void OpenSelectionPage(bool focus)
    {
        if (_closed || !EditMode || _host.Placing is not null || !_selectionOffer.Shows)
        {
            return;
        }

        if (!_selectionOpen)
        {
            CloseFlyout(focusEntry: false);
            Show(new PageView(SelectionModel));
            _selectionOpen = true;
        }

        _flyout!.Page.Show();
        ShowToolbar();
        PlaceFlyout();
        if (focus && !_flyout.Page.FocusFirst())
        {
            Core.Diagnostics.Log.Debug(Scope, "the selection's page opened with nothing to focus");
        }
    }

    /// <summary>The flyout of <paramref name="page"/> put on its layer.</summary>
    private void Show(PageView page)
    {
        UiLanguage.ApplyFlow(page);
        _flyout = new PageFlyout(page);
        _flyout.KeyDown += OnFlyoutKey;
        if (_redraws is null)
        {
            _redraws = new FlyoutRedraws(_root.DispatcherQueue, DrawBarMenu);

            // A release anywhere on the surface lets go too: a press dragged off the page before it let go.
            var redraws = _redraws;
            _root.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, _) => redraws.LetGo()), handledEventsToo: true);
        }

        _redraws.Reset();
        _flyout.Pressed += _redraws.Pressed;
        _flyout.LetGo += _redraws.LetGo;

        // [S-2350] Hung again whenever its page grows or shrinks.
        _flyout.PageResized += PlaceFlyout;
        _flyoutLayer.Children.Add(_flyout);
    }

    /// <summary>[S-2327] Close the open flyout; the keyboard back on its entry where it was closed from the keyboard.</summary>
    private void CloseFlyout(bool focusEntry)
    {
        // [S-11707] The add page is one of the bar's flyouts: one at a time, closed with the rest.
        CloseAddPage(focusAdd: false);
        if (_flyout is not { } flyout)
        {
            return;
        }

        var page = _openPage;
        var selection = _selectionOpen;
        flyout.Page.Close();
        if (_redraws is { } redraws)
        {
            flyout.Pressed -= redraws.Pressed;
            flyout.LetGo -= redraws.LetGo;
            redraws.Reset();
        }

        _flyoutLayer.Children.Remove(flyout);
        _flyout = null;
        _openPage = null;
        _selectionOpen = false;
        if (page is SettingsPageId.General or SettingsPageId.Accounts)
        {
            SettingsMenuClosed();
        }

        ShowToolbar();
        if (focusEntry)
        {
            _toolbar.FocusEntry(selection ? null : page);
        }
    }

    /// <summary>
    /// [S-3593] Edit mode ended: the open flyout and the bar's buttons (with the add menu) are dropped;
    /// each is built again as it is next needed.
    /// </summary>
    private void ReleaseEditMenus()
    {
        CloseFlyout(focusEntry: false);
        _selectionShown = null;
        _toolbar.Release();
    }

    /// <summary>
    /// [S-3531] The open flyout shows the state as it is now (an undo, an import, another display's change).
    /// [S-2346] Drawn after the event that asked, never under a press held on it: the row pressed would be gone by
    /// the release, and taking it away inside the pointer's own event throws out of it.
    /// </summary>
    private void SyncBarMenu()
    {
        if (!_closed && _flyout is not null)
        {
            _redraws?.Ask();
        }
    }

    /// <summary>[S-2346] The posted drawing's turn: the open flyout drawn again and hung again.</summary>
    private void DrawBarMenu()
    {
        if (!_closed && _flyout is { } flyout)
        {
            flyout.Page.Show();
            PlaceFlyout();
        }
    }

    /// <summary>[S-3531] A language switch: the open flyout's words in the new language; the keys stay, so what is open stays open.</summary>
    private void RebuildBarMenu() => SyncBarMenu();

    /// <summary>
    /// [S-2327] A press anywhere on the surface that is not the bar or the open flyout closes it, and the
    /// press goes on to do what it does. [S-2343] A press on a widget leaves the selection's page open:
    /// a click on a widget ends with its page open, so closing it only to open it again would flash;
    /// it goes when the widget starts to move (<see cref="ClearForDrag"/>).
    /// </summary>
    private void OnPressForBarMenu(object sender, PointerRoutedEventArgs e)
    {
        FrameworkElement? open = (FrameworkElement?)_flyout ?? _addPage;
        if (open is null || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        for (var node = source; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, open) || ReferenceEquals(node, _toolbar) || (_selectionOpen && node is WidgetCard))
            {
                return;
            }
        }

        CloseFlyout(focusEntry: false);
    }

    /// <summary>[S-2327] Escape goes back out of what the flyout opened, then closes it; Page Up / Down, Home and End scroll it.</summary>
    private void OnFlyoutKey(object sender, KeyRoutedEventArgs e)
    {
        if (_flyout is not { } flyout)
        {
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.Escape:
                if (!flyout.Page.Back())
                {
                    CloseFlyout(focusEntry: true);
                }

                e.Handled = true;
                break;
            case VirtualKey.PageUp or VirtualKey.PageDown or VirtualKey.Home or VirtualKey.End when !TextHasFocus():
                e.Handled = flyout.Scroll(e.Key);
                break;
        }
    }

    /// <summary>[S-2327] Down from the bar: the keyboard into the open flyout.</summary>
    private void FocusFlyout()
    {
        if (_flyout is { } flyout && !flyout.Page.FocusFirst())
        {
            Core.Diagnostics.Log.Debug(Scope, "the open flyout has nothing to focus");
        }
    }

    /// <summary>
    /// [S-2325] Hung from its entry: centred on it, 12 under the bar, inside the work area, as tall as its page up to 70 % of it;
    /// [S-13707] a page as wide as the bar (<see cref="SettingsPages.BarWide"/>) the bar's width, still pointing at its entry.
    /// </summary>
    private void PlaceFlyout()
    {
        if (_closed || _flyout is not { } flyout)
        {
            return;
        }

        var metrics = Metrics;
        if (!metrics.IsUsable)
        {
            return;
        }

        var area = WorkArea;
        var bar = ToolbarRect(area);
        var entry = _toolbar.EntryBox(_selectionOpen ? null : _openPage, _flyoutLayer) ?? new PixelRect(bar.X, bar.Y, bar.Width, bar.Height);

        // [S-13707] The Unplaced icons page is as wide as the bar, as the add page is: measured at that width.
        flyout.Place(!_selectionOpen && _openPage is { } page && SettingsPages.BarWide(page)
            ? FlyoutPlacement.Wide(entry, bar, area, flyout.ContentHeightAt(bar.Width))
            : FlyoutPlacement.Place(entry, bar, area, flyout.ContentHeight));
        ShineSelection();
    }

    /// <summary>
    /// [S-2344] The selection's page lets the selected widgets it covers be seen through it, as their
    /// cards are drawn now; every other page covers what is under it.
    /// </summary>
    private void ShineSelection()
    {
        if (_flyout is not { } flyout)
        {
            return;
        }

        var under = new List<PixelRect>();
        if (_selectionOpen)
        {
            foreach (var card in _cards)
            {
                if (_host.Selection.Contains(card.Model.Id) && card.Visibility == Visibility.Visible && card.Width > 0 && card.Height > 0)
                {
                    under.Add(new PixelRect(Canvas.GetLeft(card), Canvas.GetTop(card), card.Width, card.Height));
                }
            }
        }

        flyout.ShineThrough(under);
    }
}
